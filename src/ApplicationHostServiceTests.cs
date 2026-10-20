using System.Text.Json;
namespace LeoLauncher;
static class ApplicationHostServiceTests
{
    sealed class Fake(LocalApplication app):IApplicationHostServices
    {
        public LocalApplication? Current=app;public Exception? ReadError,DependencyError;public bool BrokenLog;public int Probes;
        public readonly List<(ApplicationHostPhase,Type)> Faults=[];
        public LocalApplication? ReadConfiguration()=>ReadError==null?Current:throw ReadError;
        public DependencyState[] ReadDependencies(LocalApplication configuration){Probes++;if(DependencyError!=null)throw DependencyError;return [new("application-file","ready"),new("working-directory","ready")];}
        public void RecordFault(ApplicationHostPhase phase,Type type){if(BrokenLog)throw new IOException("PRIVATE_SECONDARY");Faults.Add((phase,type));}
    }
    static bool Throws(Action action,Type type){try{action();return false;}catch(Exception e){return e.GetType()==type;}}
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};
        ui.Shown+=async(_,_)=>{var c=new Dictionary<string,bool>();try{await Check(Path.GetDirectoryName(report)!,c);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,real_process_starts=0}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{ui.Close();}};Application.Run(ui);
    }
    static async Task Check(string root,Dictionary<string,bool> c)
    {
        string path=Path.Combine(root,"PRIVATE_APP.exe");File.WriteAllText(path,"fixture only, never executable");
        var app=new LocalApplication("0123456789abcdef0123456789abcdef","fixture",path,Arguments:"--PRIVATE_ARGUMENT",WorkingDirectory:root,Administrator:true);
        var journal=new ApplicationHostFaults();LocalApplication? live=app;
        var service=new ApplicationHostServices(app.Id,()=>live,journal);
        c["service_has_no_preferences_api"]=typeof(IApplicationHostServices).GetMethods().All(m=>m.ReturnType!=typeof(Preferences)&&m.GetParameters().All(p=>p.ParameterType!=typeof(Preferences)));
        c["scoped_configuration"]=service.ReadConfiguration()==app;
        c["passive_dependency_ready"]=service.ReadDependencies(app).All(d=>d.State=="ready");
        c["missing_dependency_preserved"]=service.ReadDependencies(app with{Path=Path.Combine(root,"missing.exe")})[0].State=="missing";
        c["missing_directory_preserved"]=service.ReadDependencies(app with{WorkingDirectory=Path.Combine(root,"missing")})[1].State=="missing";
        c["foreign_dependency_rejected"]=Throws(()=>service.ReadDependencies(app with{Id="1123456789abcdef0123456789abcdef"}),typeof(InvalidOperationException));
        live=app with{Id="1123456789abcdef0123456789abcdef"};c["foreign_configuration_rejected"]=Throws(()=>service.ReadConfiguration(),typeof(InvalidOperationException));live=app;
        var fake=new Fake(app);int starts=0;System.Diagnostics.ProcessStartInfo? captured=null;
        var host=new ApplicationCapabilityHost((a,t)=>Task.FromResult(a),(info,t)=>{starts++;captured=info;return Task.FromResult(true);});
        var module=new LocalApplicationModule(app,(_,_,_)=>throw new Exception("legacy bypass"),host,services:fake);
        c["fake_dependency_used"]=module.BrainAvailable&&fake.Probes==1;
        c["initial_configuration_current"]=module.ConfigurationIsCurrent&&module.Enabled;
        var result=await module.ExecuteStructuredAsync(new(module.Id),CancellationToken.None);
        c["original_executor_retained"]=result.Evidence=="accepted"&&starts==1;
        c["launch_options_preserved"]=captured is {Arguments:"--PRIVATE_ARGUMENT",Verb:"runas"}&&captured.WorkingDirectory==root;
        fake.Current=app with{Name="renamed"};c["configuration_change_detected"]=!module.ConfigurationIsCurrent;
        bool changed=false;try{await module.ExecuteStructuredAsync(new(module.Id),CancellationToken.None);}catch(ApplicationConfigurationChangedException){changed=true;}
        c["stale_configuration_does_not_launch"]=changed&&starts==1;
        fake.Current=app with{Enabled=false};c["disabled_is_visible"]=!module.Enabled;
        fake.Current=null;c["removed_is_visible"]=!module.Enabled&&!module.ConfigurationIsCurrent;
        fake.Current=app;fake.ReadError=new IOException("PRIVATE_PATH PRIVATE_KEY PRIVATE_ARGUMENT");
        c["configuration_fault_preserved"]=Throws(()=>{_=module.Enabled;},typeof(IOException));
        c["configuration_fault_classified"]=fake.Faults.Last()==(ApplicationHostPhase.Configuration,typeof(IOException));
        fake.BrokenLog=true;c["logging_failure_keeps_original"]=Throws(()=>{_=module.Enabled;},typeof(IOException));
        fake.BrokenLog=false;fake.ReadError=null;fake.DependencyError=new UnauthorizedAccessException("PRIVATE_PATH");
        c["dependency_fault_preserved"]=Throws(()=>module.ReadDependencies(),typeof(UnauthorizedAccessException));
        c["dependency_fault_classified"]=fake.Faults.Last()==(ApplicationHostPhase.Dependencies,typeof(UnauthorizedAccessException));
        fake.DependencyError=null;fake.Current=app with{Id="1123456789abcdef0123456789abcdef"};
        c["module_checks_fake_scope"]=Throws(()=>{_=module.Enabled;},typeof(InvalidOperationException));fake.Current=app;
        int before=fake.Faults.Count;using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();bool stopped=false;try{await module.ExecuteStructuredAsync(new(module.Id),cancelled.Token);}catch(OperationCanceledException){stopped=true;}c["cancel_does_not_launch"]=stopped&&starts==1;}
        c["cancel_not_logged_as_fault"]=fake.Faults.Count==before;
        var failingHost=new ApplicationCapabilityHost((a,t)=>Task.FromResult(a),(_,_)=>throw new IOException("PRIVATE_KEY PRIVATE_PATH"));
        var realModule=new LocalApplicationModule(app,(_,_,_)=>throw new Exception("bypass"),failingHost,services:service);
        bool failed=false;try{await realModule.ExecuteStructuredAsync(new(realModule.Id),CancellationToken.None);}catch(IOException){failed=true;}
        c["execution_failure_preserved"]=failed;
        c["fault_record_has_three_fields"]=typeof(ApplicationHostFault).GetProperties().Length==3;
        var data=JsonSerializer.Serialize(journal.Read());
        c["fault_record_redacted"]=journal.Read().Single() is {Phase:"Execution",ErrorType:"IOException"}&&!data.Contains("PRIVATE_")&&!data.Contains(root.Replace("\\","\\\\"));
        for(int i=0;i<200;i++)service.RecordFault(ApplicationHostPhase.Execution,typeof(IOException));
        c["fault_journal_bounded"]=journal.Read().Length==128;
        var snapshot=journal.Read();snapshot[0]=new("bad","bad","bad");c["fault_snapshot_is_detached"]=journal.Read()[0].Module==module.Id;
        // Production registry uses the scoped service and same host pipeline, with no real start.
        var prefs=new Preferences{ApplicationModules=[app]};using var bus=new ActionDispatcher(prefs,(_,_,_)=>throw new Exception("bypass"),new ExtensionStore(Path.Combine(root,"extensions")),failingHost);
        var receipt=await bus.ExecuteDetailedAsync(new("local_app_open",app.Id));
        c["old_action_uses_services"]=receipt.Status=="failed"&&receipt.ErrorCode=="launch_failed"&&bus.Registry.ApplicationFaults.Read().Any(f=>f.Phase=="Execution");
        prefs.ApplicationModules[0]=app with{Enabled=false};c["registry_refresh_preserves_disable"]=!bus.Registry.Resolve(module.Id).Enabled;
    }
}
