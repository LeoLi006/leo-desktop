using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
namespace LeoLauncher;

sealed record FootballMatch(string Id, DateTimeOffset Kickoff, string League, string HomeId, string Home, string AwayId, string Away, string State, string Status, string Score, bool TimeKnown);
sealed class FootballCache
{
    public DateTimeOffset Updated { get; set; }
    public List<FootballMatch> Matches { get; set; } = new();
    public int FailedSources { get; set; }
    public int SchemaVersion { get; set; }
}
static class FootballFeed
{
    public const string Base = "https://site.api.espn.com/apis/site/v2/sports/soccer/";
    public static string CachePath => Path.Combine(Store.Root, "leo-football-cache.json");
    static string Str(JsonElement e, string key) => e.TryGetProperty(key, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ValueKind == JsonValueKind.Object && v.TryGetProperty("displayValue", out var d) ? d.GetString() ?? "" : v.ToString() : "";
    public static List<FootballMatch> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new List<FootballMatch>();
        if (!doc.RootElement.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array) throw new FormatException("缺少赛程列表");
        foreach (var ev in events.EnumerateArray())
        {
            try {
                var c = ev.GetProperty("competitions")[0];
                var sides = c.GetProperty("competitors").EnumerateArray().ToArray();
                var home = sides.First(x => Str(x, "homeAway") == "home"); var away = sides.First(x => Str(x, "homeAway") == "away");
                var h = home.GetProperty("team"); var a = away.GetProperty("team");
                var status = c.TryGetProperty("status", out var cs) ? cs : ev.GetProperty("status"); var type = status.GetProperty("type");
                string league = ev.TryGetProperty("league", out var l) ? Str(l, "name") : Str(c, "altGameNote");
                if (league.Length == 0 && ev.TryGetProperty("season", out var season)) league = Str(season, "slug");
                bool timeKnown = !c.TryGetProperty("timeValid", out var tv) || tv.ValueKind != JsonValueKind.False;
                string description = Str(type, "description"), state = Str(type, "state");
                if (description.Contains("TBD", StringComparison.OrdinalIgnoreCase) || description.Contains("TBA", StringComparison.OrdinalIgnoreCase)) timeKnown = false;
                string score = state == "pre" ? "VS" : Str(home, "score") + " : " + Str(away, "score");
                result.Add(new(Str(ev, "id"), DateTimeOffset.Parse(Str(ev, "date"), CultureInfo.InvariantCulture), league, Str(h, "id"), Str(h, "displayName"), Str(a, "id"), Str(a, "displayName"), state, description, score, timeKnown));
            } catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException or IndexOutOfRangeException) { }
        }
        if (events.GetArrayLength() > 0 && result.Count == 0) throw new FormatException("赛程字段已变更");
        return result;
    }
    public static FootballCache LoadCache() { try { return AtomicJson.Read(CachePath,json=>{var data=JsonSerializer.Deserialize<FootballCache>(json)??throw new FormatException("赛程缓存为空");FootballQueries.Validate(data);return data;}); } catch { return new(); } }
    public static bool IsRecent(FootballMatch m, DateTimeOffset now) => m.Kickoff >= new DateTimeOffset(now.ToLocalTime().Date.AddDays(-3), TimeZoneInfo.Local.GetUtcOffset(now.ToLocalTime().Date.AddDays(-3))) && m.Kickoff <= now.AddDays(14);
    public static string[] FollowTags(FootballMatch m, IEnumerable<string> following)
    {
        bool Has(string id) => m.HomeId == id || m.AwayId == id;
        return following.Where(x=>ClubCatalog.Find(x) is {} club&&Has(club.Id)).ToArray();
    }
    static readonly HashSet<string> BigClubs = new() { "83", "86", "1068", "382", "364", "360", "359", "363", "367", "160", "132", "111", "110", "103", "124", "114", "104", "105", "131", "11420", "176", "167", "174" };
    static readonly HashSet<string> BigNationals=["202","205","478","164","481","482","448","162","449","459","477","212","208"];
    public static bool IsFocus(FootballMatch m) => BigClubs.Contains(m.HomeId) && BigClubs.Contains(m.AwayId)||BigNationals.Contains(m.HomeId)&&BigNationals.Contains(m.AwayId);
    static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase) {
        ["Barcelona"]="巴塞罗那", ["Inter Miami CF"]="迈阿密国际", ["Manchester City"]="曼城", ["Argentina"]="阿根廷", ["Real Madrid"]="皇家马德里", ["Atlético Madrid"]="马德里竞技", ["Atletico Madrid"]="马德里竞技", ["Liverpool"]="利物浦", ["Arsenal"]="阿森纳", ["Chelsea"]="切尔西", ["Manchester United"]="曼联", ["Tottenham Hotspur"]="热刺", ["Bayern Munich"]="拜仁慕尼黑", ["Borussia Dortmund"]="多特蒙德", ["Paris Saint-Germain"]="巴黎圣日耳曼", ["Juventus"]="尤文图斯", ["AC Milan"]="AC米兰", ["Internazionale"]="国际米兰", ["Spanish LALIGA"]="西甲", ["English Premier League"]="英超", ["UEFA Champions League"]="欧冠", ["MLS"]="美职联", ["Italian Serie A"]="意甲", ["German Bundesliga"]="德甲", ["French Ligue 1"]="法甲"
    };
    public static string Name(string text)
    {
        if (text.StartsWith("UEFA Champions League", StringComparison.OrdinalIgnoreCase)) return "欧冠";
        if(text.Contains("World Cup Qualif",StringComparison.OrdinalIgnoreCase))return "世界杯预选赛";
        if(text.Contains("FIFA World Cup",StringComparison.OrdinalIgnoreCase))return "世界杯";
        if(text.Contains("International Friendly",StringComparison.OrdinalIgnoreCase))return "国际友谊赛";
        foreach(var (english,chinese) in new[]{("UEFA European Championship","欧洲杯"),("UEFA Nations League","欧国联"),("Copa America","美洲杯"),("Copa América","美洲杯"),("Asian Cup","亚洲杯"),("Africa Cup of Nations","非洲杯"),("Gold Cup","中北美金杯赛")})if(text.Contains(english,StringComparison.OrdinalIgnoreCase))return chinese;
        var known=ClubCatalog.Find(text);if(known!=null)return known.Name;
        return text switch { "LALIGA" => "西甲", "Bundesliga" => "德甲", "Serie A" => "意甲", "Ligue 1" => "法甲", "Ipswich Town" => "伊普斯维奇", "Real Betis" => "皇家贝蒂斯", "Atlanta United FC" => "亚特兰大联", "AEK Athens" => "雅典AEK", "Aston Villa" => "阿斯顿维拉", "Red Bull New York" => "纽约红牛", "New York Red Bulls" => "纽约红牛", "FC Cincinnati" => "辛辛那提FC", _ => ClubNames.Translate(Names.GetValueOrDefault(text, text)) };
    }
    public static string Status(FootballMatch m) => m.Status.Contains("Postpon", StringComparison.OrdinalIgnoreCase) ? "延期" : m.Status.Contains("Cancel", StringComparison.OrdinalIgnoreCase) ? "取消" : m.State == "in" ? "进行中" : m.State == "post" ? "已结束" : "未开赛";
    public static async Task<FootballCache> FetchAsync(FootballCache old, CancellationToken token)
    {
        using var moduleRequest=Modules.FootballRequest(token);token=moduleRequest.Token;
        var paths = Enumerable.Range(-4, 12).Select(i => "all/scoreboard?dates=" + DateTimeOffset.UtcNow.AddDays(i).ToString("yyyyMMdd") + "&limit=1000").ToList();
        paths.AddRange(FootballFollowing.Current.Ids.Select(id=>"all/teams/"+id+"/schedule?fixture=true"));
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(18) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("LeoFootball/1.0");
        using var gate = new SemaphoreSlim(4);
        var tasks = paths.Select(async path => {
            await gate.WaitAsync(token);
            try { return (Matches: Parse(await http.GetStringAsync(Base + path, token)), Failed: false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { return (Matches: new List<FootballMatch>(), Failed: true); }
            finally { gate.Release(); }
        });
        var results = await Task.WhenAll(tasks);
        int failed = results.Count(x => x.Failed);
        if (failed == paths.Count) throw new HttpRequestException("赛程源暂时无法连接");
        var now = DateTimeOffset.UtcNow;
        // Scoreboards carry newer match status than fixture lists; prefer their first occurrence.
        var matches = results.SelectMany(x => x.Matches).Where(m => IsRecent(m, now)).DistinctBy(m => m.Id).ToList();
        if (failed > 0) matches.AddRange(old.Matches.Where(m => IsRecent(m, now) && matches.All(n => n.Id != m.Id)));
        var cache = new FootballCache { Updated = now, Matches = matches, FailedSources = failed, SchemaVersion = 2 };
        token.ThrowIfCancellationRequested();
        return cache;
    }
}

sealed class FootballForm : BarcaWindow,IWindowBack
{
    readonly ModuleThemeBinding theme;
    readonly bool preview;
    readonly FootballClient queries;readonly bool ownsQueries;
    readonly TodoStore tasks;
    public static string[] FollowingNames=>FootballFollowing.Current.Names;
    readonly FootballFollowing following;
    readonly FlowLayoutPanel followed=new(){Width=180,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
    readonly Panel home=new(){Dock=DockStyle.Fill};
    readonly Panel pages=new(){Dock=DockStyle.Fill};
    TeamView? detail;
    Point homeScroll;
    FootballCache cache;
    readonly FlowLayoutPanel list=new(){Dock=DockStyle.Fill,AutoScroll=true,WrapContents=false,FlowDirection=FlowDirection.TopDown,Padding=new Padding(8,8,18,8),BackColor=BarcaUi.Canvas};
    readonly Label status=new(){Dock=DockStyle.Bottom,Height=43,Padding=new Padding(24,8,12,0),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f)};
    readonly BarcaButton refresh=new(){Text="刷新赛程 ↻",Width=105,Height=34};
    readonly List<BarcaButton> tabs=new();
    readonly Dictionary<string,FollowButton> follows=new();
    readonly CancellationTokenSource lifetime=new();
    int filterIndex;bool busy,disposed;
    public FootballForm(bool previewMode=false,TodoStore? planner=null,FootballFollowing? favorites=null,FootballClient? sharedQueries=null)
    {
        following=favorites??(previewMode?new FootballFollowing(null):FootballFollowing.Current);preview=previewMode;queries=sharedQueries??FootballClient.Standalone(preview,following);ownsQueries=sharedQueries==null;tasks=planner??new(preview?null:Path.Combine(Store.Root,"leo-todos.json"));cache=queries.CachedSchedule();
        Text="Leo · 足球";StartPosition=FormStartPosition.Manual;ClientSize=new Size(900,760);MinimumSize=new Size(780,580);TopMost=false;
        var chrome=BarcaUi.Chrome(this,"LEO  /  MATCHDAY","BLAUGRANA  ·  你的足球日程");
        CanResize=false;MaximizeBox=false;MinimumSize=Size;MaximumSize=Size;
        var content=new Panel(){Dock=DockStyle.Fill,Padding=new Padding(20,14,12,0)};
        var sidebar=new FlowLayoutPanel(){Dock=DockStyle.Left,Width=198,WrapContents=false,FlowDirection=FlowDirection.TopDown,Padding=new Padding(0,6,16,0)};
        sidebar.Controls.Add(new Label{Text="我的关注",Width=174,Height=31,ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)});
        var manage=new BarcaButton{Text="＋ 管理关注",Width=174,Height=31,Margin=new Padding(0,0,0,9)};manage.Click+=async(_,_)=>{using var dialog=new FollowingManager(following);if(dialog.ShowDialog(this)==DialogResult.OK){RenderFollowing();Render();if(!preview)await LoadAsync(true);}};sidebar.Controls.Add(manage);sidebar.Controls.Add(followed);RenderFollowing();
        sidebar.Controls.Add(new WatchLinksPanel(preview));

        var main=new Panel{Dock=DockStyle.Fill};var toolbar=new Panel{Dock=DockStyle.Top,Height=51};
        string[] names={"我的关注","焦点对阵"};
        for(int i=0;i<2;i++){int index=i;var tab=new BarcaButton{Text=names[i],Bounds=new Rectangle(8+i*105,3,98,34),Selected=i==0};tab.Click+=(_,_)=>{filterIndex=index;Render();};tabs.Add(tab);toolbar.Controls.Add(tab);}
        refresh.Anchor=AnchorStyles.Top|AnchorStyles.Right;toolbar.Controls.Add(refresh);toolbar.Resize+=(_,_)=>refresh.Location=new Point(toolbar.Width-123,3);
        refresh.Click+=async(_,_)=>await LoadAsync(true);main.Controls.Add(list);main.Controls.Add(toolbar);content.Controls.Add(main);content.Controls.Add(sidebar);
        home.Controls.Add(content);home.Controls.Add(status);pages.Controls.Add(home);Controls.Add(pages);Controls.Add(chrome);Shown+=async(_,_)=>{Render();if(!preview)await LoadAsync(false);};
        KeyPreview=true;KeyDown+=(_,e)=>{if(detail!=null&&(e.KeyCode==Keys.BrowserBack||e.Alt&&e.KeyCode==Keys.Left)){BackToSchedule();e.Handled=true;e.SuppressKeyPress=true;}};Render();theme=new(this);
    }
    void RenderFollowing(){foreach(Control c in followed.Controls.Cast<Control>().ToArray())c.Dispose();follows.Clear();foreach(var club in following.Clubs){var button=new FollowButton(club.Name,club.Id){Width=157};button.Click+=(_,_)=>OpenTeam(club.Name);follows[club.Name]=button;followed.Controls.Add(button);}followed.Height=Math.Clamp(following.Clubs.Length*76,42,250);if(follows.Count==0)followed.Controls.Add(new Label{Text="尚未关注球队",ForeColor=BarcaUi.Muted,Width=170,Height=35});}
    internal int QueryCacheCount=>cache.Matches.Count;
    public bool VerifyNoResize(){var before=Size;foreach(var point in new[]{new Point(1,1),new Point(Width-2,1),new Point(1,Height-2),new Point(Width-2,Height-2)}){var screen=PointToScreen(point);var msg=Message.Create(Handle,0x84,IntPtr.Zero,(IntPtr)((screen.Y<<16)|(screen.X&0xffff)));WndProc(ref msg);if(msg.Result.ToInt64() is >=10 and <=17)return false;}var command=Message.Create(Handle,0x112,(IntPtr)0xF000,IntPtr.Zero);WndProc(ref command);return Size==before&&MinimumSize==MaximumSize&&!CanResize;}
    void OpenTeam(string name)
    {
        if(detail!=null)detail.Dispose();
        homeScroll=new Point(0,-list.AutoScrollPosition.Y);home.Hide();
        detail=new TeamView(name,preview,BackToSchedule,tasks,queries);pages.Controls.Add(detail);detail.BringToFront();
    }
    public bool TryGoBack(){if(detail==null)return false;BackToSchedule();return true;}
    void BackToSchedule()
    {
        detail?.Dispose();detail=null;home.Show();home.BringToFront();list.AutoScrollPosition=homeScroll;
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        if(detail!=null&&(keyData==(Keys.Alt|Keys.Left)||keyData==Keys.BrowserBack)){BackToSchedule();return true;}
        return base.ProcessCmdKey(ref msg,keyData);
    }
    async Task LoadAsync(bool force)
    {
        if(busy||preview)return;
        busy=true;refresh.Enabled=false;status.Text="正在更新公开赛程…";
        try{var result=await queries.Schedule(force,lifetime.Token);if(IsDisposed||lifetime.IsCancellationRequested)return;cache=result.Cache;if(!result.Stale)tasks.Client.From("football-sync").SyncMatches(cache.Matches,cache.Updated);Render();UpdateStatus();if(result.Stale)status.Text+=" · 显示已有缓存";if(result.Error!=null)status.Text+=" · "+result.Error;}
        catch(OperationCanceledException){}
        catch{if(!IsDisposed){Render();status.Text=cache.Updated==default?"暂时无法联网 · 暂无缓存，请稍后刷新":"联网失败 · 显示旧缓存 "+cache.Updated.ToLocalTime().ToString("MM-dd HH:mm");}}
        finally{busy=false;if(!IsDisposed)refresh.Enabled=true;}
    }
    void UpdateStatus()=>status.Text=cache.Updated==default?"暂无缓存 · 联网刷新后显示比赛": "ESPN  ·  "+cache.Updated.ToLocalTime().ToString("MM-dd HH:mm")+" 更新  ·  "+(cache.FailedSources>0?"部分来源失败，含旧缓存":"缓存 10 分钟")+"  ·  近3天赛果 / 未来赛程  ·  本机时区";
    void Render()
    {
        if(IsDisposed)return;for(int i=0;i<tabs.Count;i++){tabs[i].Selected=i==filterIndex;tabs[i].Invalidate();}
        list.SuspendLayout();foreach(Control c in list.Controls.Cast<Control>().ToArray())c.Dispose();
        var rows=cache.Matches.Where(m=>FootballFeed.IsRecent(m,DateTimeOffset.UtcNow)).Select(m=>(Match:m,Tags:FootballFeed.FollowTags(m,following.Names))).Where(x=>filterIndex==0?x.Tags.Length>0:FootballFeed.IsFocus(x.Match)).OrderBy(x=>x.Match.State=="post"?1:0).ThenBy(x=>(x.Match.State=="post"?-x.Match.Kickoff.UtcTicks:x.Match.Kickoff.UtcTicks)).Take(80).ToArray();
        if(rows.Length==0)list.Controls.Add(new Label{Text="暂无已公布的匹配赛程\n可稍后刷新",Width=480,Height=100,Padding=new Padding(22,30,0,0),ForeColor=BarcaUi.Muted});
        string day="";
        foreach(var row in rows){var m=row.Match;string next=m.Kickoff.ToLocalTime().ToString(UiLanguage.English?"MMM dd, dddd":"MM月dd日 dddd",UiLanguage.Culture);if(next!=day){day=next;list.Controls.Add(new Label{Text=day,Width=Math.Max(400,list.ClientSize.Width-35),Height=34,Padding=new Padding(3,8,0,0),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)});}
            string tag=row.Tags.Length>0?string.Join(" · ",row.Tags):"焦点对阵";var card=new MatchCard(m,tag,tasks,cache.Updated){Width=Math.Max(500,list.ClientSize.Width-38)};
            card.Click+=(_,_)=>{if(preview)return;try{if(m.Id.Length>0&&m.Id.All(char.IsAsciiDigit))Process.Start(new ProcessStartInfo("https://www.espn.com/soccer/match/_/gameId/"+m.Id){UseShellExecute=true});}catch{status.Text="无法打开浏览器";}};list.Controls.Add(card);
        }list.ResumeLayout();
    }
    public bool VerifyFollowing()=>follows.Keys.Order().SequenceEqual(following.Names.Order());
    public bool VerifyTabs(){for(int pass=0;pass<4;pass++)foreach(var tab in tabs)tab.PerformClick();tabs[0].PerformClick();return tabs.Count==2&&filterIndex==0&&tabs[0].Selected&&!tabs[1].Selected;}
    public bool VerifyTeamNavigation()
    {
        int windows=Application.OpenForms.Count;bool ok=true;
        tabs[1].PerformClick();
        foreach(var button in follows.Values){typeof(Control).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(button,new object[]{EventArgs.Empty});Application.DoEvents();ok&=detail!=null&&detail.Visible&&detail.FindForm()==this&&Application.OpenForms.Count==windows&&!home.Visible;if(button.AccessibleName=="曼城"){var msg=Message.Create(Handle,0,IntPtr.Zero,IntPtr.Zero);ok&=ProcessCmdKey(ref msg,Keys.Alt|Keys.Left);}else detail?.PreviewBack();ok&=detail==null&&home.Visible&&filterIndex==1;}
        tabs[0].PerformClick();return ok;
    }
    public void PreviewData(IEnumerable<FootballMatch> matches){cache=new(){Updated=DateTimeOffset.Now,Matches=matches.ToList()};Render();UpdateStatus();}
    public bool PreviewTeam(){OpenTeam("巴塞罗那");Application.DoEvents();return detail!.VerifySwitching();}
    public void PreviewTeamTable()=>detail?.PreviewTable();
    protected override void OnFormClosed(FormClosedEventArgs e){lifetime.Cancel();base.OnFormClosed(e);}
    protected override void Dispose(bool disposing){if(disposing&&!disposed){disposed=true;theme.Dispose();lifetime.Cancel();lifetime.Dispose();if(ownsQueries)queries.Dispose();}base.Dispose(disposing);}
}
