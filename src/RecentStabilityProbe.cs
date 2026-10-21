using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace LeoLauncher;
static class RecentStabilityProbe
{
    [DllImport("user32.dll")]static extern int GetGuiResources(IntPtr process,int kind);
    sealed record Sample(double Seconds,int Cycles,double CpuSeconds,long PrivateBytes,long WorkingSet,long ManagedHeap,int Handles,int Gdi,int User,int Activities,int Themes,int Subscriptions,int PendingThemes,int Windows);
    static Sample Take(Process process,Stopwatch watch,int cycles){process.Refresh();var theme=ModuleThemeBinding.Diagnostics();return new(Math.Round(watch.Elapsed.TotalSeconds,2),cycles,process.TotalProcessorTime.TotalSeconds,process.PrivateMemorySize64,process.WorkingSet64,GC.GetTotalMemory(false),process.HandleCount,GetGuiResources(process.Handle,0),GetGuiResources(process.Handle,1),ModuleActivities.Snapshot().Sum(a=>a.Active),theme.Bindings,theme.Subscriptions,theme.Pending,Application.OpenForms.Count);}
    static ToolCall Tool(string name,object value)=>new(Guid.NewGuid().ToString("N"),name,JsonSerializer.Serialize(value));
    static object Step(string action,string value="",string from_step="",string result_property="")=>new{action,value,from_step,result_property};
    public static void Run(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{
            var checks=new Dictionary<string,bool>();var samples=new List<Sample>();try{await Check(path,ui,checks,samples);}catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks,samples}));}finally{ui.Close();}
        };Application.Run(ui);
    }
    static async Task Check(string path,Form ui,Dictionary<string,bool> checks,List<Sample> samples)
    {
        var watch=Stopwatch.StartNew();using var process=Process.GetCurrentProcess();var before=Take(process,watch,0);int cycles=0,cancelled=0,chains=0,windows=0;
        bool activitiesDrained=true,subscriptionsDrained=true,stableCounts=true;
        var prefs=new Preferences{ApplicationIndexing=false,ContextAware=false,RememberHistory=false};var todos=new TodoStore(null);var notes=new NoteStore(null);var note=notes.Save(null,"isolated conversion fixture");var now=DateTimeOffset.UtcNow;var match=new FootballMatch("123456",now.AddHours(5),"fixture","83","Barcelona","86","Real Madrid","pre","Scheduled","VS",true);
        bool slow=false;var sampleConfig=new ExampleNotesConfiguration(true);using(var services=new ScopedNotesExampleServices(notes,()=>sampleConfig))
        using(var queries=new FootballQueries(()=>true,()=>"fixture",loadSchedule:()=>new(){Updated=now,SchemaVersion=2,Matches=[match]},loadTeam:_=>new(),fetchSchedule:async(old,t)=>{if(slow)await Task.Delay(50,t);return new(){Updated=now,SchemaVersion=2,Matches=[match]};},saveSchedule:_=>{},saveTeam:(_,_)=>{}))
        using(var bus=new ActionDispatcher(prefs,(_,_,_)=>throw new Exception("no legacy"),new ExtensionStore(Path.Combine(Path.GetDirectoryName(path)!,"no-extensions"))))
        using(var clips=new ClipboardHistoryService(prefs,true))
        using(var launcher=new LauncherForm(prefs,()=>{},true,true,todos)){
            bus.Registry.Register(new TodoCapability(todos,(work,t)=>{t.ThrowIfCancellationRequested();work();return Task.CompletedTask;},_=>{},_=>null));var todoClient=new TodoClient(todos,bus);todos.SharedClient=todoClient;
            bus.Registry.Register(new NotesCapability(()=>notes,(_,_,_)=>Task.CompletedTask));var notesClient=new NotesClient(notes,bus);
            bus.Registry.Register(new FootballCapability(()=>true,queries,bus.ExecuteDetailedAsync,_=>Task.CompletedTask,()=>["巴塞罗那"]));
            bus.Registry.Register(new MatchPlanningCapability(()=>true,new MatchPlanningHost(()=>new[]{(match,now)}),todos,new ModuleWindowService(ui)));
            bus.Registry.Register(new ClipboardCapability(clips,prefs,(work,t)=>{t.ThrowIfCancellationRequested();work();return Task.CompletedTask;},()=>{}));var clipClient=new ClipboardClient(clips,bus);clips.History.Add(new("text","isolated clip","fixture",now));
            var contributor=new ScopedNotesExampleModule(services);bus.Registry.Register(contributor);EntryContributions.Bind(prefs,bus.Registry,bus);var catalog=new BrainCatalog(bus.Registry);
            async Task Cycle(){
                launcher.Open();launcher.Location=new(-19000,-19000);launcher.PreviewTyping("示例查询");launcher.RefreshModules();launcher.Hide();
                sampleConfig=sampleConfig with{Enabled=cycles%4!=0};bus.CancelDisabled();launcher.RefreshModules();ModuleEntryContributions.Read(prefs,bus.Registry);
                var rows=await queries.Schedule(false,default);rows.Cache.Matches.Clear();clipClient.Search("fixture").Clear();todoClient.Items.Clear();
                if(cycles%10==0){
                    using(var window=ClipboardTool.Open(clips,clipClient)!){window.Location=new(-19000,-19000);await Task.Delay(20);window.Close();}
                    using(var window=new NotesForm(new NotesClient(notes,bus),(n,day)=>todoClient.Add(n.Text,day,null,n.Id).Id)){window.Show();window.Location=new(-19000,-19000);await Task.Delay(20);window.Close();}windows+=2;
                    slow=true;using var stop=new CancellationTokenSource();stop.CancelAfter(2);try{await queries.Schedule(true,stop.Token);throw new Exception("cancelled query published");}catch(OperationCanceledException){cancelled++;}finally{slow=false;}
                }
                if(cycles%20==0){
                    using var brain=new BrainSession(catalog,bus.ExecuteDetailedAsync,(_,_)=>Task.FromResult(WorkflowVerification.Unknown),()=>new Dictionary<string,string>());
                    brain.BeginTurn("下一场巴萨比赛加入清单");await brain.Tool(Tool("leo_action",new{action="football_facts",value="今晚比赛"}),default);await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("match_lookup",match.Id),Step("match_add","","step-1","Reference")}}),default);
                    if(brain.Pending==null)throw new Exception("missing fixture plan");await brain.Confirm(brain.Pending.Id,(_,_)=>{},default);
                    var task=todoClient.Add(note.Text,DateOnly.FromDateTime(DateTime.Today),null,note.Id);notesClient.Link(note.Id,task.Id);chains++;
                }
                stableCounts&=todos.Items.Count==2&&notes.Items.Count==1&&clips.History.Items.Count==1&&queries.CachedSchedule().Matches.Count==1;
                await Task.Delay(10);activitiesDrained&=ModuleActivities.Snapshot().Sum(a=>a.Active)==before.Activities;subscriptionsDrained&=ModuleThemeBinding.Diagnostics().Subscriptions==before.Subscriptions;
                cycles++;
            }
            for(int i=0;i<10;i++)await Cycle();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();watch.Restart();samples.Add(Take(process,watch,cycles));
            double checkpoint=30;
            while(watch.Elapsed.TotalSeconds<300){await Cycle();if(watch.Elapsed.TotalSeconds>=checkpoint){samples.Add(Take(process,watch,cycles));File.WriteAllText(path+".progress",JsonSerializer.Serialize(new{samples}));checkpoint+=30;}await Task.Delay(450);}
            checks["five_minutes_completed"]=watch.Elapsed.TotalSeconds>=300;checks["data_counts_remain_bounded"]=stableCounts;checks["activities_drain_between_cycles"]=activitiesDrained;checks["window_subscriptions_drain_between_cycles"]=subscriptionsDrained;checks["cancelled_requests_do_not_publish"]=cancelled>0;checks["both_existing_chains_exercised"]=chains>1;
            bus.Registry.Unregister(contributor);bus.CancelDisabled();services.Dispose();queries.Dispose();launcher.Dispose();clips.Dispose();
        }
        await Task.Delay(100);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Task.Delay(100);var final=Take(process,watch,cycles);samples.Add(final);
        checks["all_owned_activities_released"]=final.Activities==before.Activities;checks["all_theme_bindings_and_subscriptions_released"]=final.Themes==before.Themes&&final.Subscriptions==before.Subscriptions&&final.PendingThemes==before.PendingThemes;checks["all_fixture_windows_closed"]=final.Windows==before.Windows;
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,duration_seconds=watch.Elapsed.TotalSeconds,cycles,cancelled_queries=cancelled,chain_rounds=chains,temporary_windows=windows,before,final,samples,notes="Warm-up and final retained-heap samples use forced GC; intermediate samples do not. Resource trends require review, not a universal MB threshold.",real_api_requests=0,personal_data_reads=0,system_clipboard_body_reads=0,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));
    }
}
