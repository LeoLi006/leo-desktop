using System.Text.Json;
namespace LeoLauncher;
static class ClientHostServiceTests
{
    sealed class Fake(string id):IClientHostServices
    {
        public ClientHostConfiguration Configuration=new(id,true,"initial");
        public DependencyState[] States=[new("fixture","unknown")];
        public Exception? ConfigError,DependencyError,ExecutionError,AvailabilityError;
        public bool BrokenLog;public int Executions,AvailabilityCalls;public readonly ApplicationHostFaults Journal=new();
        public ClientHostConfiguration ReadConfiguration()=>ConfigError==null?Configuration:throw ConfigError;
        public DependencyState[] ReadDependencies()=>DependencyError==null?States:throw DependencyError;
        public bool ClientAvailable(){AvailabilityCalls++;if(AvailabilityError!=null)throw AvailabilityError;return true;}
        public void RecordFault(ApplicationHostPhase phase,Type error){if(BrokenLog)throw new IOException("PRIVATE_LOG");Journal.Add(new(id,phase.ToString(),error.Name));}
        public void Run(){Executions++;if(ExecutionError!=null)throw ExecutionError;}
    }
    static IStructuredLeoModule Create(string id,Fake fake)
    {
        bool ForbiddenEnabled()=>throw new Exception("old enabled callback used");
        string ForbiddenConfig()=>throw new Exception("old config callback used");
        bool ForbiddenAvailable()=>throw new Exception("old availability callback used");
        if(id=="clash")return new ProxyCapability(ForbiddenEnabled,ForbiddenConfig,ForbiddenAvailable,_=>{fake.Run();return Task.FromResult(new ProxyStatus(true,false,"fixture"));},(_,_,_)=>throw new Exception("never set proxy"),new(),services:fake);
        if(id=="media")return new MediaCapability(ForbiddenEnabled,ForbiddenConfig,_=>{fake.Run();return Task.FromResult<MediaSession[]>([]);},(_,_,_)=>throw new Exception("never control media"),(_,_)=>throw new Exception("never open client"),_=>Task.CompletedTask,new(),new(),services:fake);
        return new MusicCapability(ForbiddenEnabled,ForbiddenConfig,ForbiddenAvailable,new(new(null)),(_,_)=>throw new Exception("never network"),(_,_,_)=>throw new Exception("never play"),(_,_)=>throw new Exception("never pause"),(_,_)=>throw new Exception("never lyrics"),(_,_)=>throw new Exception("never open"),()=>{fake.Run();return new(false,false);},_=>Task.CompletedTask,new(),services:fake);
    }
    static bool Throws(Action action,Type expected){try{action();return false;}catch(Exception e){return e.GetType()==expected;}}
    static async Task<bool> ThrowsAsync(Func<Task> action,Type expected){try{await action();return false;}catch(Exception e){return e.GetType()==expected;}}
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{var c=new Dictionary<string,bool>();try{await Check(c);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,real_client_starts=0,real_api_requests=0,proxy_changes=0}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{ui.Close();}};Application.Run(ui);
    }
    static async Task Check(Dictionary<string,bool> c)
    {
        c["service_does_not_expose_preferences"]=typeof(IClientHostServices).GetMethods().All(m=>m.ReturnType!=typeof(Preferences)&&m.GetParameters().All(p=>p.ParameterType!=typeof(Preferences)));
        foreach(string id in new[]{"clash","music","media"}){
            var fake=new Fake(id);var module=Create(id,fake);var brain=(IBrainContributor)module;
            var request=new ActionRequest(id=="clash"?"proxy_status":id=="music"?"music_status":"media_status");
            c[id+"_uses_scoped_configuration"]=module.Enabled&&module.ConfigurationIsCurrent&&brain.BrainStamp=="initial";
            var dependencies=((IModuleContractSource)module).ReadDependencies();dependencies[0]=new("changed","ready");
            c[id+"_dependency_snapshot_is_detached"]=fake.States[0].Id=="fixture";
            c[id+"_dependency_read_is_passive"]=fake.AvailabilityCalls==0&&fake.Executions==0;
            var result=await module.ExecuteStructuredAsync(request,CancellationToken.None);
            c[id+"_existing_execution_preserved"]=result.Evidence=="state-observed"&&fake.Executions==1;
            fake.Configuration=fake.Configuration with{Generation="new"};
            c[id+"_configuration_change_detected"]=!module.ConfigurationIsCurrent&&await ThrowsAsync(async()=>{await module.ExecuteStructuredAsync(request,CancellationToken.None);},typeof(ApplicationConfigurationChangedException));
            c[id+"_configuration_change_not_fault"]=fake.Journal.Read().Length==0;
            fake.Configuration=fake.Configuration with{Generation="initial",Enabled=false};
            c[id+"_disable_stops_execution"]=!module.Enabled&&await ThrowsAsync(async()=>{await module.ExecuteStructuredAsync(request,CancellationToken.None);},typeof(ApplicationConfigurationChangedException))&&fake.Executions==1;
            fake.Configuration=fake.Configuration with{Enabled=true};
            using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();c[id+"_cancel_preserved"]=await ThrowsAsync(async()=>{await module.ExecuteStructuredAsync(request,cancelled.Token);},typeof(OperationCanceledException))&&fake.Executions==1&&fake.Journal.Read().Length==0;}
            fake.ConfigError=new IOException("PRIVATE_KEY PRIVATE_PATH");
            c[id+"_configuration_fault_classified"]=Throws(()=>{_=module.Enabled;},typeof(IOException))&&fake.Journal.Read().Last() is {Phase:"Configuration",ErrorType:"IOException"};fake.ConfigError=null;
            fake.DependencyError=new UnauthorizedAccessException("PRIVATE_BODY");
            c[id+"_dependency_fault_classified"]=Throws(()=>((IModuleContractSource)module).ReadDependencies(),typeof(UnauthorizedAccessException))&&fake.Journal.Read().Last() is {Phase:"Dependencies",ErrorType:"UnauthorizedAccessException"};fake.DependencyError=null;
            fake.ExecutionError=new IOException("PRIVATE_TITLE PRIVATE_KEY");
            c[id+"_execution_failure_preserved"]=await ThrowsAsync(async()=>{await module.ExecuteStructuredAsync(request,CancellationToken.None);},typeof(IOException))&&fake.Journal.Read().Last() is {Phase:"Execution",ErrorType:"IOException"};
            fake.BrokenLog=true;c[id+"_broken_log_keeps_original"]=await ThrowsAsync(async()=>{await module.ExecuteStructuredAsync(request,CancellationToken.None);},typeof(IOException));fake.BrokenLog=false;fake.ExecutionError=null;
            fake.Configuration=fake.Configuration with{Module="foreign"};
            c[id+"_foreign_scope_rejected"]=Throws(()=>{_=module.Enabled;},typeof(InvalidOperationException));fake.Configuration=fake.Configuration with{Module=id};
            if(id!="media"){
                fake.AvailabilityError=new IOException("PRIVATE_CLIENT_PATH");
                if(id=="clash")c[id+"_availability_fault_preserves_filter"]=!brain.BrainAvailable;
                else c[id+"_availability_fault_preserves_filter"]=!brain.BrainActions.Any(a=>a.Id=="music_play");
                c[id+"_availability_fault_classified"]=fake.Journal.Read().Last() is {Phase:"Dependencies",ErrorType:"IOException"};
            }
            c[id+"_fault_records_redacted"]=!JsonSerializer.Serialize(fake.Journal.Read()).Contains("PRIVATE_")&&fake.Journal.Read().All(f=>f.Module==id);
        }
        var journal=new ApplicationHostFaults();var services=new ClientHostServices("clash",()=>true,()=>"stamp",()=>[new("clash-client","unknown")],faults:journal);
        for(int i=0;i<180;i++)services.RecordFault(ApplicationHostPhase.Execution,typeof(IOException));
        c["shared_fault_format_bounded"]=journal.Read().Length==128&&journal.Read().All(f=>f.Module=="clash");
        using var pet=new PetForm(new Preferences{ApplicationIndexing=false},true,true);
        foreach(var id in new[]{"clash","music","media"}){
            var request=id=="clash"?new ActionRequest("proxy_status","invalid"):id=="music"?new ActionRequest("music_step","invalid"):new ActionRequest("media_status","invalid");
            await ((IStructuredLeoModule)pet.ContractRegistry.Resolve(request.Action)).ExecuteStructuredAsync(request,CancellationToken.None).ContinueWith(t=>{_=t.Exception;});
            c[id+"_production_host_records_scoped_fault"]=pet.ContractRegistry.ClientFaults.Read().Any(f=>f.Module==id&&f.Phase=="Execution"&&f.ErrorType=="ArgumentException");
        }
    }
}
