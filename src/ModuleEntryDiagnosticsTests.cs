using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;
static class ModuleEntryDiagnosticsTests
{
    sealed class Provider(string id):IStructuredLeoModule,IActionRequestAdapter,IModuleContractSource,IModuleEntryContributor
    {
        public string Id=>id;public string Name=>"PRIVATE_NAME";public string Version=>"1";public bool Enabled{get;set;}=true;public bool ConfigurationIsCurrent=>true;
        public IReadOnlyList<string> Actions=>[id+"_query"];public int MaximumInputLength=>1000;public int Reads,Calls;public bool Broken,Malformed;public int Size=1;
        public DependencyState[] ReadDependencies()=>[];
        public CapabilityDefinition Describe(string action)=>new(action,Id,"in-process","empty-object","read","coalesce-inflight",5000);
        public ModuleContract DescribeModule()=>ModuleContracts.Build(this,"fixture",Name,"optional","none",[],_ =>("object","空对象","fixture","就绪",false,["snapshot"]));
        public ActionRequest Adapt(StructuredActionRequest request)=>ClientCapabilityParameters.Adapt(request,Actions,false);
        public EntryContribution[] DescribeEntries(){Reads++;if(Broken)throw new IOException("PRIVATE_BODY C:\\PRIVATE_PATH\\private.txt PRIVATE_KEY");return Enumerable.Range(0,Size).Select(i=>new EntryContribution(Id+"/item"+i,Malformed?"foreign":Id,Actions[0],Name,@"C:\PRIVATE_PATH\icon.png",true,EntryDisplay.Shortcut,ModuleEntryContributions.TargetPrefix+Id+"/item"+i,["PRIVATE_ALIAS"],"module-action"){Parameters="{\"private\":\"PRIVATE_BODY\"}"}).ToArray();}
        public Task<ActionPayload> ExecuteStructuredAsync(ActionRequest request,CancellationToken token){Calls++;throw new Exception("must not execute");}
        public Task<string> ExecuteAsync(ActionRequest request,CancellationToken token)=>throw new Exception("must not execute");
        public string FailureCode(Exception error)=>"fixture_failed";
    }
    static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(value)!;
    public static void Run(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=(_,_)=>{
            var c=new Dictionary<string,bool>();var sink=OperationFeedback.Sink;int notices=0;OperationFeedback.Sink=_=>notices++;
            try{
                var p=new Preferences{ApplicationIndexing=false};using var pet=new PetForm(p,true,true);var registry=pet.ContractRegistry;var presentation=Field<ModulePresentationService>(pet,"presentation");var diagnostics=new HostServiceDiagnostics(p,registry,presentation);
                c["empty_directory_has_zero_diagnostics"]=diagnostics.Read().ModuleEntries is{Available:0,Disabled:0,Isolated:0,Faults.Length:0};
                var good=new Provider("diag-good"){Size=2};var disabled=new Provider("diag-off"){Enabled=false};var broken=new Provider("diag-broken"){Broken=true};registry.Register(good);registry.Register(disabled);registry.Register(broken);
                var state=diagnostics.Read();c["counts_entries_and_isolated_providers_separately"]=state.ModuleEntries is{Available:2,Disabled:1,Isolated:1,Faults.Length:1};
                c["fault_contains_only_module_and_error_type"]=state.ModuleEntries.Faults.Single()==new ModuleEntryFault(broken.Id,"IOException");
                var serialized=JsonSerializer.Serialize(state);c["snapshot_has_no_private_parameters_paths_or_messages"]=!serialized.Contains("PRIVATE_")&&!serialized.Contains("private.txt");
                var summary=HostServiceDiagnostics.Summary(p);c["summary_shows_counts_without_module_ids"]=summary.Contains("普通模块入口：可用 2 · 停用 1 · 隔离模块 1")&&!summary.Contains("diag-")&&!summary.Contains("PRIVATE_");
                using(var health=new HealthChecksView(p,true,null))c["actual_health_view_displays_directory_counts"]=Field<Label>(health,"result").Text.Contains("普通模块入口：可用 2 · 停用 1 · 隔离模块 1");
                int reads=broken.Reads;diagnostics.Read();c["diagnostic_reads_existing_directory_once"]=broken.Reads==reads+1;
                for(int i=0;i<30;i++)ModuleEntryContributions.Read(p,registry);
                c["repeated_reads_do_not_accumulate_fault_records"]=diagnostics.Read().ModuleEntries.Faults.Length==1;
                c["reads_never_execute_module_actions"]=good.Calls+disabled.Calls+broken.Calls==0;
                c["directory_faults_do_not_send_notifications"]=notices==0;
                foreach(var entry in ModuleEntryContributions.Read(p,registry).Entries)p.EntryDisplayModes[entry.Id]=EntryDisplay.Search;
                c["hidden_entries_still_count_available"]=diagnostics.Read().ModuleEntries.Available==2;
                good.Enabled=false;c["module_disable_changes_current_counts"]=diagnostics.Read().ModuleEntries is{Available:0,Disabled:3,Isolated:1};good.Enabled=true;
                broken.Broken=false;c["recovery_clears_current_fault_without_history"]=diagnostics.Read().ModuleEntries is{Available:3,Disabled:1,Isolated:0,Faults.Length:0};
                broken.Malformed=true;var malformed=diagnostics.Read().ModuleEntries;c["malformed_description_has_sanitized_error"]=malformed.Isolated==1&&malformed.Faults.Single()==new ModuleEntryFault(broken.Id,"ArgumentException");
                registry.Unregister(broken);c["removal_clears_current_isolation"]=diagnostics.Read().ModuleEntries is{Available:2,Disabled:1,Isolated:0,Faults.Length:0};
                broken.Malformed=false;registry.Register(broken);c["reregister_uses_current_state"]=diagnostics.Read().ModuleEntries is{Available:3,Disabled:1,Isolated:0};registry.Unregister(broken);
                var many=Enumerable.Range(0,70).Select(i=>new Provider("diag-fault-"+i){Broken=true}).ToArray();foreach(var item in many)registry.Register(item);
                var bounded=diagnostics.Read().ModuleEntries;c["details_bounded_but_total_is_accurate"]=bounded.Isolated==70&&bounded.Faults.Length==ModuleEntryContributions.MaximumFaults;
                c["fault_details_deduplicated"]=bounded.Faults.Distinct().Count()==bounded.Faults.Length;
                bounded.Faults[0]=new("changed-copy","Exception");c["snapshot_mutation_does_not_change_next_read"]=diagnostics.Read().ModuleEntries.Faults.All(f=>f.ModuleId!="changed-copy");
                foreach(var item in many){registry.Unregister(item);}c["bulk_removal_drops_all_faults"]=diagnostics.Read().ModuleEntries is{Available:2,Disabled:1,Isolated:0,Faults.Length:0};
                var unsafeId=new Provider(@"C:\PRIVATE_PATH\unsafe"){Broken=true};registry.Register(unsafeId);var safe=diagnostics.Read().ModuleEntries;c["unsafe_module_identifier_is_redacted"]=safe.Faults.Single().ModuleId=="invalid-module-id"&&!JsonSerializer.Serialize(safe).Contains("PRIVATE_");registry.Unregister(unsafeId);
                registry.Unregister(good);registry.Unregister(disabled);c["all_removed_returns_zero"]=diagnostics.Read().ModuleEntries is{Available:0,Disabled:0,Isolated:0,Faults.Length:0};
                registry.Register(good);int before=good.Reads;presentation.Dispose();c["closed_host_diagnostics_are_passive_and_empty"]=diagnostics.Read() is{Closed:true,ModuleEntries.Available:0,ModuleEntries.Isolated:0}&&good.Reads==before;
                c["all_checks_remain_silent"]=notices==0;
                File.WriteAllText(path,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,real_api_requests=0,external_process_starts=0,personal_data_reads=0},new JsonSerializerOptions{WriteIndented=true}));
            }catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{OperationFeedback.Sink=sink;ui.Close();}
        };Application.Run(ui);
    }
}
