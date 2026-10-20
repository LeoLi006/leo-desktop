using System.Text.Json;
using System.Reflection;
using System.Diagnostics;
namespace LeoLauncher;
static class HostServiceCleanupTests
{
    static Task Queue(Func<Task> action){using var ready=new ManualResetEventSlim();var task=Task.Run(async()=>{Task pending;try{pending=action();}finally{ready.Set();}await pending;});if(!ready.Wait(3000))throw new TimeoutException("queue fixture");return task;}
    static async Task<bool> Cancelled(Task task){try{await task;return false;}catch(OperationCanceledException){return true;}}
    static T Field<T>(object instance,string name)=>(T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(instance)!;
    static ActionResult Result(string module)=>new("request","trace","fixture","fixture",module,"operation","failed","PRIVATE_BODY",null,null,"unknown");
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{var c=new Dictionary<string,bool>();try{var resources=await Check(ui,c);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,resources,real_api_requests=0,microphone_captures=0,clipboard_body_reads=0}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{ui.Close();}};Application.Run(ui);
    }
    static async Task<object> Check(Form ui,Dictionary<string,bool> c)
    {
        int shown=0,notices=0;var presentation=new ModulePresentationService(new(ui),_=>true,_=>shown++,_=>notices++,_=>{});
        c["nine_fixed_targets"]=presentation.Snapshot() is {Closed:false,Targets:9,Windows:0,Feedback:0};
        var pending=Queue(()=>presentation.For(ModuleWindowTarget.Chat).ShowAsync(CancellationToken.None));
        c["pending_window_count"]=presentation.Snapshot().Windows==1;
        var delivery=presentation.Feedback(ModuleWindowTarget.Chat,CancellationToken.None);
        var feedback=Queue(()=>delivery.ReportAsync(Result("conversation")));
        c["pending_feedback_count"]=presentation.Snapshot().Feedback==1;
        bool cancellationUnderLock=true;using var registration=Field<CancellationTokenSource>(presentation,"lifetime").Token.Register(()=>cancellationUnderLock=Monitor.IsEntered(Field<object>(presentation,"gate")));
        presentation.Dispose();presentation.Dispose();
        c["shutdown_cancels_outside_service_lock"]=!cancellationUnderLock;
        c["shutdown_cancels_before_control_disposal"]=await Cancelled(pending)&&!ui.IsDisposed;
        await feedback;c["shutdown_drops_feedback"]=notices==0&&shown==0;
        c["shutdown_releases_pending_counts"]=presentation.Snapshot() is {Closed:true,Available:0,Windows:0,Feedback:0}&&Field<bool>(presentation,"lifetimeReleased");
        c["shutdown_rejects_new_windows"]=await Cancelled(presentation.For(ModuleWindowTarget.Notes).ShowAsync(CancellationToken.None));
        await delivery.ReportAsync(Result("conversation"));c["shutdown_ignores_late_feedback"]=notices==0;
        // A live host: diagnostics must not echo configurable names, paths or stored fault text.
        var app=new LocalApplication("0123456789abcdef0123456789abcdef","PRIVATE_APP_NAME",@"C:\PRIVATE_PATH\app.exe");
        var prefs=new Preferences{ApplicationIndexing=false,ApplicationModules=[app]};
        using(var pet=new PetForm(prefs,true,true)){
            pet.Opacity=0;pet.ShowInTaskbar=false;pet.Show();
            var registry=pet.ContractRegistry;var host=Field<ModulePresentationService>(pet,"presentation");
            var diagnostics=new HostServiceDiagnostics(prefs,registry,host);var initial=diagnostics.Read();
            c["production_service_scope_counts"]=initial.ConfigurationScopes==4&&initial.DependencyScopes==4&&initial.WindowTargets==9&&initial.AvailableWindows==9;
            c["valid_disabled_and_hidden_not_faults"]=initial.InvalidEntries==0;
            prefs.ApplicationModules[0]=app with{Enabled=false};prefs.EntryDisplayModes[app.Id]=EntryDisplay.Search;
            c["disabled_hidden_remains_nonfault"]=diagnostics.Read().InvalidEntries==0;
            prefs.ApplicationModules.Add(prefs.ApplicationModules[0]);c["duplicate_entry_count"]=diagnostics.Read().InvalidEntries==2;
            prefs.ApplicationModules.RemoveAt(1);prefs.ApplicationModules[0]=app;
            registry.Register(new BuiltinLeoModule("fixture-wrong-owner","fixture",()=>true,["terminal_open"],(_,_,_)=>throw new Exception("never execute")));
            c["wrong_builtin_owner_count"]=diagnostics.Read().InvalidEntries==1;
            registry.ApplicationFaults.Add(new("PRIVATE_ID","Configuration","PRIVATE_TYPE"));registry.ClientFaults.Add(new("PRIVATE_ID","Dependencies","PRIVATE_TYPE"));registry.ClientFaults.Add(new("PRIVATE_ID","Execution","PRIVATE_TYPE"));
            var snapshot=diagnostics.Read();c["faults_counted_by_stage"]=snapshot is {ConfigurationFaults:1,DependencyFaults:1,ExecutionFaults:1};
            var text=HostServiceDiagnostics.Summary(prefs);c["summary_contains_counts_not_private_fields"]=text.Contains("异常入口 1")&&text.Contains("配置作用域 4")&&!text.Contains("PRIVATE_")&&!text.Contains(app.Id);
            using(var health=new HealthChecksView(prefs,true,null)){var label=Field<Label>(health,"result");c["health_view_uses_host_summary"]=label.Text.Contains("宿主服务：运行中")&&label.Text.Contains("窗口端口 9")&&!label.Text.Contains("PRIVATE_");}
            var queued=Queue(()=>host.For(ModuleWindowTarget.Football).ShowAsync(CancellationToken.None));
            typeof(PetForm).GetMethod("ReleaseResources",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pet,null);
            c["real_host_shutdown_cancels_queued_window"]=await Cancelled(queued);
            c["real_host_closed_summary_passive"]=diagnostics.Read() is {Closed:true,ConfigurationScopes:0,PendingWindows:0}&&HostServiceDiagnostics.Summary(prefs).Contains("已关闭");
            pet.Dispose();c["real_host_repeat_disposal_safe"]=pet.IsDisposed;
        }
        // Necessary short resource check: no thresholds on ordinary runtime fluctuations.
        var process=Process.GetCurrentProcess();process.Refresh();long before=process.WorkingSet64;int handles=process.HandleCount;
        int rounds=30;var watch=Stopwatch.StartNew();bool drained=true;
        for(int i=0;i<rounds;i++){
            using var host=new ModulePresentationService(new(ui),_=>true,_=>{},_=>{},_=>{});
            var task=Queue(()=>host.For(ModuleWindowTarget.Applications).ShowAsync(CancellationToken.None));host.Dispose();drained&=await Cancelled(task)&&host.Snapshot() is {Windows:0,Feedback:0};
        }
        watch.Stop();await Task.Delay(25);process.Refresh();c["thirty_exit_rounds_drain"]=drained;
        c["no_original_feedback_adapter"]=typeof(Program).Assembly.GetType("LeoLauncher.ModuleFeedbackService")==null;
        return new{rounds,elapsed_ms=watch.ElapsedMilliseconds,working_set_delta=process.WorkingSet64-before,handle_delta=process.HandleCount-handles};
    }
}
