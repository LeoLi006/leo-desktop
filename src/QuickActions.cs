using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
namespace LeoLauncher;
sealed partial class ChatForm
{
    readonly Panel bodyHost=new(){Dock=DockStyle.Fill,Padding=new Padding(16,8,16,0)};
    readonly FlowLayoutPanel results=new(){Dock=DockStyle.Fill,AutoScroll=true,WrapContents=false,FlowDirection=FlowDirection.TopDown,Padding=new Padding(8)};
    readonly Panel options=new(){Dock=DockStyle.Bottom,Height=99,BackColor=BarcaUi.Surface,Visible=false};
    readonly Panel topPanel=new();
    readonly Button settingsButton=new BarcaButton{Text="设置",Size=new Size(66,32)};
    readonly Action<QuickIntent>? todoAction;
    readonly Action? openClipboard;
    readonly TodoStore? planStore;
    string lastAnswer="";
    void ShowView(Control view){if(IsDisposed||Disposing)return;foreach(Control c in bodyHost.Controls)c.Visible=c==view;view.BringToFront();}
    void ClearResults(){if(IsDisposed||Disposing)return;foreach(Control c in results.Controls.Cast<Control>().ToArray())c.Dispose();ShowView(results);}
    int ResultWidth=>Math.Max(250,results.ClientSize.Width-30);
    void ResultText(string text,bool heading=false){if(IsDisposed||Disposing)return;results.Controls.Add(new Label{Text=text,Width=ResultWidth,Height=heading?39:Math.Clamp(TextRenderer.MeasureText(text,Font,new Size(ResultWidth-12,2000),TextFormatFlags.WordBreak).Height+16,35,400),Padding=new Padding(4,heading?8:4,4,3),ForeColor=heading?BarcaUi.Text:BarcaUi.Muted,Font=heading?BarcaUi.Font(13,true):Font});}
    void Choice(string text,Action action){if(IsDisposed||Disposing)return;var button=new BarcaButton{Text=text,Width=ResultWidth,Height=41,Margin=new Padding(0,2,0,6)};button.Click+=(_,_)=>{try{action();}catch(Exception e){state.Text=e.Message;}};results.Controls.Add(button);}
    void ShowHome(){ClearResults();ResultText("有什么交给我？",true);ResultText("打开应用、安排清单、查比赛，直接说就好。");Choice("看看今晚的比赛",()=>{input.Text="今晚看球";_=SendAsync();});Choice("看看今天的清单",()=>{input.Text="看看今天的清单";_=SendAsync();});Choice("准备看球",()=>{input.Text="准备看球";_=SendAsync();});if(turns.Count>0)Choice("继续刚才的话题",()=>ShowView(transcript));lastAnswer="";}
    void ShowTeamChoices(){ClearResults();ResultText("想看看哪支球队？",true);if(FootballForm.FollowingNames.Length==0)ResultText("还没有关注球队，可在足球窗口的管理关注中添加。");foreach(string name in FootballForm.FollowingNames)Choice(name,()=>{input.Text=name+"近况";_=SendAsync();});}
    void LocalDone(string text){if(IsDisposed||Disposing)return;lastAnswer=text;state.Text="本地完成 · 无 AI 请求";}
    async Task HandleQuickAsync(QuickIntent intent,string prompt)
    {
        var request=CancellationTokenSource.CreateLinkedTokenSource(chatLife.Token);request.CancelAfter(TimeSpan.FromSeconds(90));pending=request;send.Enabled=false;stop.Enabled=true;UpdateVoice();
        try{
            var unified=CommandTranslation.From(intent,"chat");if(appTools?.HasLeo==true&&unified!=null&&intent.Kind is not(QuickKind.Football or QuickKind.SearchApp)){string message=await appTools.ExecuteLeoAsync(unified.Action,unified.Value,request.Token);ClearResults();ResultText(message);LocalDone(message);return;}
            switch(intent.Kind){
                case QuickKind.LeoCommand:if(appTools==null)throw new InvalidOperationException("功能尚未就绪");ClearResults();var result=await appTools.ExecuteLeoAsync(intent.Value,intent.Argument,request.Token);ResultText(result);LocalDone(result);break;
                case QuickKind.Todos:case QuickKind.AddTodo:case QuickKind.CompleteTodo:if(appTools?.HasLeo==true){var requestAction=CommandTranslation.From(intent,"chat")!;var completed=await appTools.ExecuteLeoAsync(requestAction.Action,requestAction.Value,request.Token);ClearResults();ResultText(completed);LocalDone(completed);break;}if(todoAction==null)throw new InvalidOperationException("请从 Leo 脚下清单图标打开每日清单");todoAction(intent);LocalDone("已在独立清单中处理");break;
                case QuickKind.Clipboard:if(openClipboard==null)throw new InvalidOperationException("请从 Leo 入口打开剪贴板");openClipboard();ClearResults();ResultText("剪贴板已打开",true);LocalDone("已打开剪贴板");break;
                case QuickKind.Web:if(appTools!=null)await appTools.ExecuteLeoAsync("web_search",intent.Value,request.Token);else if(!preview)throw new InvalidOperationException("网页搜索尚未就绪");ClearResults();ResultText("已在浏览器搜索",true);ResultText(intent.Value);LocalDone("已打开网页搜索");break;
                case QuickKind.OpenApp:case QuickKind.SearchApp:
                    ClearResults();ResultText("应用",true);
                    if(intent.Value is "应用" or "软件"||string.IsNullOrWhiteSpace(intent.Value)){ResultText("输入或说：打开微信、打开浏览器。");break;}
                    if(appTools==null)throw new InvalidOperationException("本机应用索引尚未就绪");var apps=appTools.FindLocal(intent.Value);var exact=apps.Where(a=>Catalog.Normalize(a.Name)==Catalog.Normalize(intent.Value)).ToArray();
                    if(intent.Kind==QuickKind.OpenApp&&(apps.Length==1||exact.Length==1)){var app=exact.Length==1?exact[0]:apps[0];string message=await appTools.OpenLocalAsync(app,request.Token);ResultText(message);LocalDone(message);}
                    else {ResultText(apps.Length>0?"点选要打开的应用":"未找到应用，索引可能尚未完成，也可以试试完整名称。");foreach(var app in apps)Choice(app.Name,async()=>{try{LocalDone(await appTools.OpenLocalAsync(app,chatLife.Token));}catch(Exception e){if(!chatReleased&&!IsDisposed)state.Text=e.Message;}});}break;
                case QuickKind.Football:Modules.RequireFootball();await ShowFootball(intent,request.Token);break;
            }
        }catch(OperationCanceledException){if(!chatReleased&&!IsDisposed)state.Text="已停止";}catch(Exception e){if(!chatReleased&&!IsDisposed)state.Text=e.Message.Length>180?e.Message[..180]:e.Message;}
        finally{pending=null;request.Dispose();if(!chatReleased&&!IsDisposed){send.Enabled=true;UpdateVoice();input.Focus();}}
    }
    FootballClient? fallbackFootball;
    Task<string> FootballFacts(string value,CancellationToken token)=>appTools?.HasLeo==true?appTools.ExecuteLeoAsync("football_facts",value,token):(fallbackFootball??=FootballClient.Standalone(preview)).Facts(value,token);
    async Task<(TeamData Data,Club Club,bool Stale)> TeamFacts(string name,CancellationToken token)
    {
        Modules.RequireFootball();var club=Club.For(name);using var reply=JsonDocument.Parse(await FootballFacts("view:"+name,token));return(reply.RootElement.GetProperty("data").Deserialize<TeamData>()??new(),club,reply.RootElement.GetProperty("stale").GetBoolean());
    }
    async Task ShowFootball(QuickIntent intent,CancellationToken token)
    {
        ClearResults();state.Text="正在查询赛程…";
        if(intent.Value.Contains("球队近况")&&intent.Team.Length==0){ShowTeamChoices();return;}
        if(intent.Team.Length>0){
            var fact=await TeamFacts(intent.Team,token);if(IsDisposed)return;
            bool ranking=System.Text.RegularExpressions.Regex.IsMatch(intent.Value,"积分|排名|第几");
            ResultText(fact.Club.Name+(ranking?" · "+fact.Club.LeagueName+"积分榜":" · 近期比赛"),true);
            if(ranking){var selected=fact.Data.Rows.FirstOrDefault(r=>r.Id==fact.Club.Id);var group=fact.Data.Rows.Where(r=>selected==null||r.Group==selected.Group).ToArray();results.Controls.Add(new StandingsLine(null,false){Width=ResultWidth});foreach(var row in group.Where((r,i)=>i<5||r.Id==fact.Club.Id))results.Controls.Add(new StandingsLine(row,row.Id==fact.Club.Id){Width=ResultWidth});lastAnswer=selected==null?"暂无排名数据":fact.Club.Name+"目前排名第"+selected.Rank+"，积分"+selected.Points+"。";}
            else{bool recent=System.Text.RegularExpressions.Regex.IsMatch(intent.Value,"近况|最近|上一场|昨天|前天|赛果|比分");IEnumerable<FootballMatch> matches=recent?fact.Data.Matches.Where(m=>m.State=="post").OrderByDescending(m=>m.Kickoff):fact.Data.Matches.Where(m=>m.State!="post"&&m.Kickoff>DateTimeOffset.UtcNow.AddHours(-3)).OrderBy(m=>m.Kickoff);if(System.Text.RegularExpressions.Regex.IsMatch(intent.Value,"昨天|前天|明天|今天")){int offset=intent.Value.Contains("昨天")?-1:intent.Value.Contains("前天")?-2:intent.DayOffset;var date=DateTime.Today.AddDays(offset);matches=fact.Data.Matches.Where(m=>m.Kickoff.LocalDateTime.Date==date).OrderBy(m=>m.Kickoff);}ShowMatches(matches.Take(System.Text.RegularExpressions.Regex.IsMatch(intent.Value,"下一场|上一场")?1:3));lastAnswer="已显示"+fact.Club.Name+"近期比赛。";}
            var time=ranking?fact.Data.TableUpdated:fact.Data.MatchesUpdated;ResultText("ESPN · "+(time==default?"暂无缓存":time.ToLocalTime().ToString("MM-dd HH:mm")+" 更新")+(fact.Stale?" · 更新失败，含旧数据":""));
        }else{
            FootballCache cache;bool stale;using(var reply=JsonDocument.Parse(await FootballFacts("view:"+intent.Value,token))){cache=reply.RootElement.GetProperty("cache").Deserialize<FootballCache>()??new();stale=reply.RootElement.GetProperty("stale").GetBoolean();}
            if(IsDisposed)return;bool overnight=intent.Value.Contains("今晚");var start=DateTimeOffset.Now.Date.AddDays(intent.DayOffset).AddHours(overnight?16:0);var end=start.AddHours(overnight?16:24);
            bool timed=intent.Value.Contains("今晚")||intent.Value.Contains("明天")||intent.Value.Contains("今天");
            var matches=cache.Matches.Where(m=>(FootballFeed.FollowTags(m,FootballForm.FollowingNames).Length>0||FootballFeed.IsFocus(m))&&(timed?m.Kickoff.LocalDateTime>=start&&m.Kickoff.LocalDateTime<end:m.Kickoff>=DateTimeOffset.UtcNow&&m.Kickoff<=DateTimeOffset.UtcNow.AddHours(48))).OrderBy(m=>m.Kickoff).Take(8).ToArray();
            ResultText(timed?start.ToString("MM-dd HH:mm")+" — "+end.ToString("MM-dd HH:mm")+" · 看球清单":"近期值得关注的比赛",true);ShowMatches(matches);ResultText("按关注球队与焦点对阵筛选 · ESPN "+cache.Updated.ToLocalTime().ToString("MM-dd HH:mm")+(stale||cache.FailedSources>0?" · 含旧缓存":""));lastAnswer=matches.Length>0?"找到"+matches.Length+"场比赛。":"这个时段暂无已公布的匹配比赛。";
        }
        state.Text="赛程查询 · 无 AI 请求";
    }
    void ShowMatches(IEnumerable<FootballMatch> matches){var rows=matches.ToArray();if(rows.Length==0)ResultText("暂无已公布的匹配比赛。");foreach(var m in rows){var card=new MatchCard(m,"",planStore){Width=ResultWidth};card.Click+=(_,_)=>{if(!preview&&m.Id.Length>0&&m.Id.All(char.IsAsciiDigit))try{Process.Start(new ProcessStartInfo("https://www.espn.com/soccer/match/_/gameId/"+m.Id){UseShellExecute=true});}catch{state.Text="无法打开比赛来源";}};results.Controls.Add(card);}}
    async Task<string> AnalysisFacts(QuickIntent intent,CancellationToken token)
    {
        if(intent.Team.Length==0)return "当前时间："+DateTimeOffset.Now.ToString("O")+"。以下为ESPN公开比赛实际查询结果，注意缓存时间与stale标记。只依据数据分析，不得编造伤停、新闻、比赛过程和比赛编号。来源https://www.espn.com/soccer/scoreboard 。\n"+await FootballFacts(intent.Value,token);
        string team=intent.Team;var fact=await TeamFacts(team,token);
        return "当前时间："+DateTimeOffset.Now.ToString("O")+"。以下为 ESPN 结构化查询结果。只依据这些事实分析，明确区分推测；没有查询伤停、首发、新闻和比赛过程，不得编造。若现有数据不足，直接说明。来源比赛页：https://www.espn.com/soccer/match/_/gameId/{Id}；积分榜：https://www.espn.com/soccer/standings/_/league/"+fact.Club.League+"。\n"+JsonSerializer.Serialize(new{team, fact.Data.MatchesUpdated,fact.Data.TableUpdated,stale=fact.Stale,matches=fact.Data.Matches.Take(12),table=fact.Data.Rows.Where(r=>r.Id==fact.Club.Id)});
    }
}
