using System.Net.Http;
using System.Text.Json;
namespace LeoLauncher;
sealed record Club(string Name,string Id,string League,string LeagueName,string English="",string[]? Aliases=null,string Kind="club")
{
    public bool IsNational=>Kind=="national";
    public string CompetitionName=>NationalCompetitions.Name(this);
    public static Club For(string name)=>ClubCatalog.Find(name)??throw new ArgumentException("球队尚未收录："+name);
}
sealed record LeagueRow(string Group,string Id,string Name,string Rank,string Played,string Won,string Drawn,string Lost,string Goals,string Difference,string Points);
sealed class TeamData
{
    public DateTimeOffset MatchesUpdated {get;set;}
    public DateTimeOffset TableUpdated {get;set;}
    public string Season {get;set;}="";
    public List<FootballMatch> Matches {get;set;}=new();
    public List<LeagueRow> Rows {get;set;}=new();
    public static string CachePath(Club club)=>Path.Combine(Store.Root,"leo-team-"+club.Id+(club.IsNational?"-"+club.League:"")+".json");
    public static TeamData Load(Club club){try{return JsonSerializer.Deserialize<TeamData>(File.ReadAllText(CachePath(club)))??new();}catch{return new();}}
    public static (string Season,List<LeagueRow> Rows) ParseTable(string json)
    {
        using var doc=JsonDocument.Parse(json);var rows=new List<LeagueRow>();
        string Text(JsonElement e,string key)=>e.TryGetProperty(key,out var value)?value.ToString():"";
        void Visit(JsonElement node,string group){
            string name=Text(node,"name");if(name.Length>0)group=name;
            if(node.TryGetProperty("standings",out var standings)&&standings.TryGetProperty("entries",out var entries)){
                foreach(var entry in entries.EnumerateArray()){
                    var team=entry.GetProperty("team");var stats=entry.GetProperty("stats").EnumerateArray().ToArray();
                    string Stat(string key){foreach(var stat in stats)if(Text(stat,"name")==key)return Text(stat,"displayValue");return "—";}
                    rows.Add(new(group,Text(team,"id"),Text(team,"displayName"),Stat("rank"),Stat("gamesPlayed"),Stat("wins"),Stat("ties"),Stat("losses"),Stat("pointsFor")+":"+Stat("pointsAgainst"),Stat("pointDifferential"),Stat("points")));
                }
            }
            if(node.TryGetProperty("children",out var children))foreach(var child in children.EnumerateArray())Visit(child,group);
        }
        Visit(doc.RootElement,"");
        if(rows.Count==0)throw new FormatException("暂无积分榜数据");
        string season=doc.RootElement.TryGetProperty("season",out var s)?Text(s,"displayName"):"";
        if(season.Length==0)season=rows[0].Group;
        return(season,rows);
    }
    public static async Task<(TeamData Data,bool MatchesFailed,bool TableFailed)> Fetch(Club club,TeamData old,CancellationToken token)
    {
        using var moduleRequest=Modules.FootballRequest(token);token=moduleRequest.Token;
        using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(20)};
        async Task<string?> Get(string url){try{return await http.GetStringAsync(url,token);}catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}catch{return null;}}
        var responses=await Task.WhenAll(Get(FootballFeed.Base+"all/teams/"+club.Id+"/schedule"),Get(FootballFeed.Base+"all/teams/"+club.Id+"/schedule?fixture=true"),Get("https://site.api.espn.com/apis/v2/sports/soccer/"+club.League+"/standings"));
        token.ThrowIfCancellationRequested();var data=new TeamData{Matches=old.Matches,Rows=old.Rows,MatchesUpdated=old.MatchesUpdated,TableUpdated=old.TableUpdated,Season=old.Season};
        bool mf=true,tf=true;
        if(responses[0]!=null&&responses[1]!=null)try{
            var all=FootballFeed.Parse(responses[0]!).Concat(FootballFeed.Parse(responses[1]!)).Where(m=>m.HomeId==club.Id||m.AwayId==club.Id).DistinctBy(m=>m.Id).ToArray();
            data.Matches=all.Where(m=>m.State=="post").OrderByDescending(m=>m.Kickoff).Take(10).Concat(all.Where(m=>m.State!="post"&&m.Kickoff>=DateTimeOffset.UtcNow.AddDays(-1)).OrderBy(m=>m.Kickoff).Take(10)).ToList();
            data.MatchesUpdated=DateTimeOffset.UtcNow;mf=false;
        }catch{}
        if(responses[2]!=null)try{NationalCompetitions.ValidateTable(club,responses[2]!);var table=ParseTable(responses[2]!);if(club.IsNational&&table.Rows.Any(row=>ClubCatalog.Find(row.Id) is {IsNational:false}))throw new FormatException("国家队积分榜返回了俱乐部数据");data.Rows=table.Rows;data.Season=table.Season;data.TableUpdated=DateTimeOffset.UtcNow;tf=false;}catch{}
        if(!mf||!tf)try{File.WriteAllText(CachePath(club)+".tmp",JsonSerializer.Serialize(data));File.Move(CachePath(club)+".tmp",CachePath(club),true);}catch{}
        return(data,mf,tf);
    }
}
sealed class TeamView:UserControl
{
    Club club;ComboBox? competitionSelector;
    readonly TodoStore? tasks;
    readonly bool preview;
    TeamData data;
    int tab;
    readonly FlowLayoutPanel list=new(){Dock=DockStyle.Fill,AutoScroll=true,WrapContents=false,FlowDirection=FlowDirection.TopDown,Padding=new Padding(20,12,20,12)};
    readonly Label status=new(){Dock=DockStyle.Bottom,Height=36,Padding=new Padding(24,8,12,0),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f)};
    readonly BarcaButton matches=new(){Text="近期比赛",Bounds=new Rectangle(24,9,112,34),Selected=true},table=new(){Text="联赛积分榜",Bounds=new Rectangle(146,9,125,34)},refresh=new(){Text="刷新 ↻",Bounds=new Rectangle(285,9,90,34)};
    readonly CancellationTokenSource lifetime=new();
    bool busy,disposed,matchesFailed,tableFailed;
    public TeamView(string name,bool previewMode,Action goBack,TodoStore? planner=null)
    {
        tasks=planner;club=Club.For(name);preview=previewMode;data=TeamData.Load(club);Dock=DockStyle.Fill;BackColor=BarcaUi.Canvas;ForeColor=BarcaUi.Text;Font=BarcaUi.Font();
        var chrome=new Panel{Dock=DockStyle.Top,Height=66};
        var back=new BarcaButton{Text="‹ 返回",Bounds=new Rectangle(24,15,86,33),AccessibleName="返回赛程"};back.Click+=(_,_)=>goBack();chrome.Controls.Add(back);
        var competitionTitle=new Label{Text=club.Name+"  /  "+club.CompetitionName,Bounds=new Rectangle(130,12,600,29),Font=BarcaUi.Font(14,true),ForeColor=BarcaUi.Text};chrome.Controls.Add(competitionTitle);
        chrome.Controls.Add(new Label{Text="最近10场赛果 · 未来10场赛程",Bounds=new Rectangle(132,42,600,20),Font=BarcaUi.Font(8.5f),ForeColor=BarcaUi.Muted});
        var toolbar=new Panel{Dock=DockStyle.Top,Height=54};toolbar.Controls.AddRange([matches,table,refresh]);matches.Click+=(_,_)=>{tab=0;Render();};table.Click+=(_,_)=>{tab=1;Render();};refresh.Click+=async(_,_)=>await LoadAsync(true);
        if(club.IsNational){table.Text="赛事积分榜";var competitions=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new Rectangle(390,12,228,29),BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text};CompanionUi.ThemeCombo(competitions);competitionSelector=competitions;var choices=NationalCompetitions.For(club);competitions.Items.AddRange(choices.Select(c=>c.Name).Cast<object>().ToArray());competitions.SelectedIndex=0;competitions.SelectedIndexChanged+=async(_,_)=>{if(busy)return;var choice=choices[competitions.SelectedIndex];club=club with{League=choice.Id};competitionTitle.Text=club.Name+"  /  "+club.CompetitionName;data=TeamData.Load(club);tab=1;Render();competitions.Enabled=false;try{await LoadAsync(true);}finally{if(!IsDisposed)competitions.Enabled=true;}};toolbar.Controls.Add(competitions);}
        Controls.Add(list);Controls.Add(status);Controls.Add(toolbar);Controls.Add(chrome);Resize+=(_,_)=>Render();Load+=async(_,_)=>{Render();if(!preview)await LoadAsync(false);};Render();
    }
    async Task LoadAsync(bool force)
    {
        if(busy||preview)return;
        if(!force&&data.MatchesUpdated>DateTimeOffset.UtcNow.AddMinutes(-10)&&data.TableUpdated>DateTimeOffset.UtcNow.AddMinutes(-10)){Render();return;}
        busy=true;if(competitionSelector!=null)competitionSelector.Enabled=false;refresh.Enabled=false;status.Text="正在更新球队赛程与积分榜…";
        try{var result=await TeamData.Fetch(club,data,lifetime.Token);data=result.Data;if(!result.MatchesFailed)tasks?.SyncMatches(data.Matches,data.MatchesUpdated);matchesFailed=result.MatchesFailed;tableFailed=result.TableFailed;if(!IsDisposed)Render();}
        catch(OperationCanceledException){}
        finally{busy=false;if(!IsDisposed){refresh.Enabled=true;if(competitionSelector!=null)competitionSelector.Enabled=true;}}
    }
    void Heading(string text){list.Controls.Add(new Label{Text=text,Width=Math.Max(700,list.ClientSize.Width-55),Height=35,Padding=new Padding(5,8,0,0),Font=BarcaUi.Font(10,true),ForeColor=BarcaUi.Gold});}
    void Render()
    {
        if(IsDisposed)return;matches.Selected=tab==0;table.Selected=tab==1;matches.Invalidate();table.Invalidate();
        list.SuspendLayout();foreach(Control c in list.Controls.Cast<Control>().ToArray())c.Dispose();
        int width=Math.Max(710,list.ClientSize.Width-58);
        if(tab==0){
            foreach(bool completed in new[]{true,false}){
                Heading(completed?"近期赛果":"即将开赛");
                var rows=completed?data.Matches.Where(m=>m.State=="post").OrderByDescending(m=>m.Kickoff):data.Matches.Where(m=>m.State!="post").OrderBy(m=>m.Kickoff);
                foreach(var m in rows){var card=new MatchCard(m,club.Name,tasks){Width=width};card.Click+=(_,_)=>OpenSource("https://www.espn.com/soccer/match/_/gameId/"+m.Id);list.Controls.Add(card);}
                if(!rows.Any())list.Controls.Add(new Label{Text="暂无已公布的数据",Width=width,Height=42,Padding=new Padding(5,10,0,0),ForeColor=BarcaUi.Muted});
            }
        }else{
            var year=System.Text.RegularExpressions.Regex.Match(data.Season,@"\d{4}(?:-\d{2,4})?");Heading(club.CompetitionName+"  ·  "+(club.IsNational&&year.Success?year.Value:data.Season));
            if(club.IsNational&&data.Rows.Count>0&&!data.Rows.Any(r=>r.Id==club.Id))Heading(club.Name+"未出现在该届积分榜中 · 下方显示完整赛事");
            foreach(var group in data.Rows.GroupBy(r=>r.Group)){
                if(data.Rows.Select(r=>r.Group).Distinct().Count()>1)Heading(ClubNames.Translate(group.Key));
                list.Controls.Add(new StandingsLine(null,false){Width=width});
                foreach(var row in group.OrderBy(r=>int.TryParse(r.Rank,out int rank)?rank:int.MaxValue))list.Controls.Add(new StandingsLine(row,row.Id==club.Id){Width=width});
            }
            if(data.Rows.Count==0)Heading(club.IsNational?"当前赛事暂无小组积分榜（未开赛或已进入淘汰赛），可切换其他赛事":"暂无积分榜数据，请稍后刷新");
        }
        var updated=tab==0?data.MatchesUpdated:data.TableUpdated;bool failed=tab==0?matchesFailed:tableFailed;
        status.Text="ESPN  ·  "+(updated==default?"暂无缓存":updated.ToLocalTime().ToString("MM-dd HH:mm")+" 更新")+(failed?" · 更新失败，显示已有缓存":" · 缓存10分钟")+"  ·  "+club.CompetitionName;
        list.ResumeLayout();
    }
    void OpenSource(string url){if(preview)return;try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});}catch{status.Text="无法打开浏览器";}}
    public void PreviewTable(){tab=1;Render();}
    public void PreviewBack()=>Controls.OfType<Panel>().SelectMany(p=>p.Controls.OfType<BarcaButton>()).Single(b=>b.AccessibleName=="返回赛程").PerformClick();
    public bool VerifySwitching(){for(int i=0;i<4;i++){table.PerformClick();matches.PerformClick();}return tab==0&&matches.Selected&&!table.Selected;}
    protected override void Dispose(bool disposing){if(disposing&&!disposed){disposed=true;lifetime.Cancel();lifetime.Dispose();}base.Dispose(disposing);}
}
sealed class StandingsLine:Control
{
    readonly LeagueRow? row;readonly bool selected;readonly Font text=BarcaUi.Font(9.5f),bold=BarcaUi.Font(10,true);
    public StandingsLine(LeagueRow? data,bool highlight){row=data;selected=highlight;Height=data==null?36:47;Margin=new Padding(0);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.Clear(selected?BarcaUi.Raised:BarcaUi.Canvas);int start=Width-430;var color=selected?BarcaUi.Gold:BarcaUi.Text;
        TextRenderer.DrawText(g,row?.Rank??"#",text,new Rectangle(7,12,34,25),row==null?BarcaUi.Muted:color);
        if(row!=null)BarcaUi.DrawBadge(g,row.Id,new Rectangle(43,10,27,27));
        TextRenderer.DrawText(g,row==null?"球队":FootballFeed.Name(row.Name),selected?bold:text,new Rectangle(row==null?45:80,12,start-(row==null?45:80),25),row==null?BarcaUi.Muted:color,TextFormatFlags.EndEllipsis);
        string[] cells=row==null?new[]{"场","胜","平","负","进:失","净胜","积分"}:new[]{row.Played,row.Won,row.Drawn,row.Lost,row.Goals,row.Difference,row.Points};
        for(int i=0;i<cells.Length;i++)TextRenderer.DrawText(g,cells[i],i==6?bold:text,new Rectangle(start+i*60,12,60,25),row==null?BarcaUi.Muted:i==6?BarcaUi.Gold:color,TextFormatFlags.HorizontalCenter);
        using var pen=new Pen(BarcaUi.Line);g.DrawLine(pen,7,Height-1,Width-7,Height-1);
    }
    protected override void Dispose(bool disposing){if(disposing){text.Dispose();bold.Dispose();}base.Dispose(disposing);}
}
