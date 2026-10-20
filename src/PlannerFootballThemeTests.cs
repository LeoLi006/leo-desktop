using System.Reflection;
using System.Text.Json;
using System.Diagnostics;
namespace LeoLauncher;
static class PlannerFootballThemeTests
{
    static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control child in root.Controls)foreach(var c in Walk(child))yield return c;}
    static void Queue(ModuleThemeBinding binding,ModuleThemeSnapshot snapshot)=>Task.Run(()=>typeof(ModuleThemeBinding).GetMethod("Changed",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(binding,[snapshot])).GetAwaiter().GetResult();
    public static void Run(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var host=new Form{Opacity=0,ShowInTaskbar=false};
        host.Shown+=async(_,_)=>{
            var c=new Dictionary<string,bool>();var original=UiTheme.Snapshot();var options=UiTheme.Options;
            try{
                using var fixtures=new FootballTestCache();var todos=new TodoStore(null);var notes=new NoteStore(null);
                var day=DateOnly.FromDateTime(DateTime.Today);todos.Client.Add("隔离主题事项",day);notes.Save(null,"隔离主题便签");
                using var todo=new TodoForm(true,todos);using var note=new NotesForm(notes,(_,_)=>throw new Exception("no conversion"));
                using var quick=new QuickNoteForm(new NoteStore(null));using var football=new FootballForm(true,todos,new FootballFollowing(null));
                Form[] windows=[todo,note,quick,football];var sizes=windows.Select(w=>w.Size).ToArray();var fonts=windows.Select(w=>w.Font).ToArray();
                var bindings=windows.Select(w=>Field<ModuleThemeBinding>(w,"theme")).ToArray();var client=Field<FootballClient>(football,"queries");
                foreach(var w in windows){w.ShowInTaskbar=false;w.StartPosition=FormStartPosition.Manual;w.Location=new(-20000,-20000);w.Show();}
                var calendar=todo.View.Calendar;var calendarFont=calendar.Font;var calendarBounds=calendar.Bounds;var ids=todos.Client.Items.Select(t=>t.Id).ToArray();
                c["four_windows_subscribed"]=bindings.All(b=>b.Subscribed);
                foreach(var skin in new[]{"382","20232","202","164","83"}){
                    UiTheme.Configure(skin,"Leo",new ThemeOptions(null,105,12,98));
                    c["theme_"+skin]=bindings.All(b=>b.Current==UiTheme.Snapshot())&&windows.All(w=>w.BackColor==BarcaUi.Canvas&&w.ForeColor==BarcaUi.Text&&w.Opacity==.98);
                }
                c["layout_and_fonts_unchanged"]=windows.Select((w,i)=>w.Size==sizes[i]&&ReferenceEquals(w.Font,fonts[i])).All(v=>v)&&ReferenceEquals(calendar.Font,calendarFont)&&calendar.Bounds==calendarBounds;
                // Rebuilt rows must inherit current tokens without another theme change.
                todos.Client.Add("换肤后新增事项",day);todo.View.Render();
                c["dynamic_todo_row_current"]=Walk(todo).OfType<Label>().Any(l=>l.Text.Contains("换肤后新增事项")&&l.ForeColor==BarcaUi.Text);
                notes.Save(null,"换肤后新增便签");c["notes_refresh_without_data_change"]=Field<ListBox>(note,"list").Items.Count==2;
                football.PreviewData([new FootballMatch("9000002",DateTimeOffset.Now.AddHours(2),"西甲","83","Barcelona","86","Real Madrid","pre","Scheduled","VS",true)]);
                c["dynamic_match_card_current"]=Walk(football).OfType<MatchCard>().Any(m=>m.BackColor==BarcaUi.Surface);
                football.PreviewTeam();UiTheme.Configure("382","Leo");football.PreviewTeamTable();
                c["team_details_and_table_follow_parent"]=Walk(football).OfType<TeamView>().Any(v=>v.BackColor==BarcaUi.Canvas)&&football.TryGoBack();
                c["following_and_cache_preserved"]=football.VerifyFollowing()&&football.QueryCacheCount==1&&ReferenceEquals(client,Field<FootballClient>(football,"queries"));
                c["calendar_and_data_preserved"]=todo.View.Calendar==calendar&&todos.Client.Items.Count==2&&todos.Client.Items.Any(t=>t.Id==ids[0])&&notes.Items.Count==2;
                foreach(var pair in windows.Zip(bindings)){
                    var old=pair.Second.Current;Queue(pair.Second,old);pair.First.Hide();UiTheme.Configure(UiTheme.Skin=="164"?"202":"164","Leo");await Task.Delay(15);
                    c[pair.First.GetType().Name+"_hide_drops_pending"]=!pair.Second.Subscribed&&pair.Second.Current==old;
                    pair.First.Show();c[pair.First.GetType().Name+"_reopen_latest"]=pair.Second.Subscribed&&pair.Second.Current==UiTheme.Snapshot();
                    Queue(pair.Second,pair.Second.Current);pair.First.Close();await Task.Delay(15);
                    c[pair.First.GetType().Name+"_close_releases"]=!pair.Second.Subscribed&&!ModuleThemeBinding.Managed(pair.First);
                }
                // Window subscriptions do not own the shared data or host background services.
                todos.Client.Add("关闭窗口后仍可安排",day);notes.Save(null,"关闭窗口后仍可保存");
                c["shared_stores_survive_window_close"]=todos.Client.Items.Count==3&&notes.Items.Count==3;
                var process=Process.GetCurrentProcess();process.Refresh();var memory=process.WorkingSet64;int handles=process.HandleCount;var watch=Stopwatch.StartNew();bool drained=true;
                for(int i=0;i<12;i++){using var w=new TodoForm(true);w.ShowInTaskbar=false;w.Location=new(-20000,-20000);w.Show();var b=Field<ModuleThemeBinding>(w,"theme");Queue(b,b.Current);w.Hide();w.Dispose();drained&=!b.Subscribed&&!ModuleThemeBinding.Managed(w);}
                await Task.Delay(25);process.Refresh();watch.Stop();c["twelve_real_window_lifecycles_drained"]=drained;
                File.WriteAllText(path,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,resources=new{rounds=12,elapsed_ms=watch.ElapsedMilliseconds,handle_delta=process.HandleCount-handles,working_set_delta=process.WorkingSet64-memory},real_api_requests=0,live_football_requests=0}));
            }catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}
            finally{UiTheme.Configure(original.Skin,original.Name,options);host.Close();}
        };Application.Run(host);
    }
}
