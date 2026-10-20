using System.Text.Json;
namespace LeoLauncher;
static class ModuleContractTests
{
    sealed class Variant(Func<ModuleContract,ModuleContract> mutate,DependencyState[]? states=null,Func<DependencyState[]>? probe=null):IStructuredLeoModule,IActionRequestAdapter,IModuleContractSource
    {
        readonly MinimalContractModule inner=new();public string Id=>inner.Id;public string Name=>inner.Name;public string Version=>inner.Version;public bool Enabled=>true;public bool ConfigurationIsCurrent=>true;public IReadOnlyList<string> Actions=>inner.Actions;
        public CapabilityDefinition Describe(string a)=>inner.Describe(a);public ModuleContract DescribeModule()=>mutate(inner.DescribeModule());public DependencyState[] ReadDependencies()=>probe?.Invoke()??states??[];
        public ActionRequest Adapt(StructuredActionRequest r)=>inner.Adapt(r);public Task<ActionPayload> ExecuteStructuredAsync(ActionRequest r,CancellationToken t)=>inner.ExecuteStructuredAsync(r,t);public Task<string> ExecuteAsync(ActionRequest r,CancellationToken t)=>inner.ExecuteAsync(r,t);public string FailureCode(Exception e)=>inner.FailureCode(e);
    }
    sealed class Legacy:ILeoModule{public string Id=>"legacy-fixture";public string Name=>"旧接口样例";public string Version=>"1";public bool Enabled=>true;public IReadOnlyList<string> Actions=>["legacy_fixture"];public Task<string> ExecuteAsync(ActionRequest r,CancellationToken t)=>Task.FromResult("legacy works");}
    static bool Rejected(Action action){try{action();return false;}catch(CapabilityException e)when(e.Code=="invalid_contract"){return true;}}
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{var c=new Dictionary<string,bool>();try{await Check(Path.GetDirectoryName(report)!,c);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=c.Values.All(x=>x),checks=c,real_api_requests=0,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{ui.Close();}};Application.Run(ui);
    }
    static async Task Check(string root,Dictionary<string,bool> c)
    {
        root=Path.Combine(root,"fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string exe=Path.Combine(root,"private-program.exe");File.WriteAllText(exe,"fixture, never executed");
        var app=new LocalApplication(Guid.NewGuid().ToString("N"),"PRIVATE_APPLICATION_NAME",exe);var prefs=new Preferences{ApplicationModules=[app]};int starts=0,prepares=0;bool repair=false;
        var host=new ApplicationCapabilityHost((a,t)=>{prepares++;if(!File.Exists(a.Path)){if(repair)File.WriteAllText(a.Path,"fixture repaired");else throw new IOException("fixture missing");}return Task.FromResult(a);},(_,_)=>{starts++;return Task.FromResult(true);});
        using var bus=new ActionDispatcher(prefs,(_,_,_)=>throw new Exception("legacy core bypass"),new ExtensionStore(Path.Combine(root,"extensions")),host);
        var sample=new MinimalContractModule();bus.Registry.Register(sample);c["sample_contract_is_valid"]=ModuleContracts.Validate(sample).Format==ModuleContracts.Format;
        var sampleResult=await bus.ExecuteStructuredAsync(new("leo-action/1","example_status",JsonSerializer.SerializeToElement(new{})));c["sample_runs_through_existing_structured_dispatcher"]=sampleResult.Status=="succeeded"&&sampleResult.Data!.Value.GetProperty("ready").GetBoolean();
        c["unknown_request_protocol_is_rejected"]=(await bus.ExecuteStructuredAsync(new("leo-action/99","example_status",JsonSerializer.SerializeToElement(new{})))).Status=="failed";
        c["sample_extra_parameter_rejected"]=(await bus.ExecuteStructuredAsync(new("leo-action/1","example_status",JsonSerializer.SerializeToElement(new{extra=true})))).Status=="failed";
        var mutations=new Dictionary<string,Func<ModuleContract,ModuleContract>>{
            ["throwing_descriptor_has_stable_error"]=_=>throw new IOException("private fixture error"),["missing_contract_rejected"]=_=>null!,["unknown_contract_version_rejected"]=d=>d with{Format="leo-host-contract/99"},["wrong_module_id_rejected"]=d=>d with{Id="wrong"},["missing_module_name_rejected"]=d=>d with{Name=""},["wrong_module_version_rejected"]=d=>d with{Version="9.0"},["unsupported_runtime_rejected"]=d=>d with{Runtime="worker"},["missing_actions_rejected"]=d=>d with{Actions=[]},["duplicate_actions_rejected"]=d=>d with{Actions=[d.Actions[0],d.Actions[0]]},["policy_mismatch_rejected"]=d=>d with{Actions=[d.Actions[0] with{Policy=d.Actions[0].Policy with{TimeoutMs=123}}]},["missing_input_description_rejected"]=d=>d with{Actions=[d.Actions[0] with{Input=d.Actions[0].Input with{Description=""}}]},["wrong_adapter_rejected"]=d=>d with{Actions=[d.Actions[0] with{Input=d.Actions[0].Input with{Adapter="application-adapter"}}]},["wrong_result_envelope_rejected"]=d=>d with{Actions=[d.Actions[0] with{Output=d.Actions[0].Output with{Envelope="new-result/99"}}]},["unsupported_evidence_rejected"]=d=>d with{Actions=[d.Actions[0] with{Output=d.Actions[0].Output with{Evidence=["pretend-success"]}}]},["input_limit_mismatch_rejected"]=d=>d with{Actions=[d.Actions[0] with{MaximumInputLength=1}]},["unknown_dependency_action_rejected"]=d=>d with{Dependencies=[new("dep","host-service","required","existing-check",["invented"])]},["duplicate_dependency_rejected"]=d=>d with{Dependencies=[new("dep","host-service","required","existing-check",["example_status"]),new("dep","host-service","required","existing-check",["example_status"])]}};
        foreach(var pair in mutations)c[pair.Key]=Rejected(()=>bus.Registry.ReplaceBuiltin(new Variant(pair.Value)))&&bus.Registry.Resolve("example_status")==sample;
        c["throwing_dependency_probe_has_stable_error"]=Rejected(()=>ModuleContracts.Snapshot(new Variant(d=>d,probe:()=>throw new IOException("private fixture dependency error"))));
        c["unknown_dependency_state_rejected"]=Rejected(()=>ModuleContracts.Snapshot(new Variant(d=>d with{Dependencies=[new("dep","host-service","required","existing-check",["example_status"])]},[new("dep","invented")])));
        c["invalid_dependency_snapshot_rejected"]=Rejected(()=>ModuleContracts.Snapshot(new Variant(d=>d,[new("invented","ready")])));
        c["failed_registration_preserves_previous_executor"]=(await bus.ExecuteDetailedAsync(new("example_status"))).Status=="succeeded";
        bool duplicate=false;try{bus.Registry.Register(new MinimalContractModule());}catch(InvalidOperationException){duplicate=true;}c["duplicate_action_registration_rejected"]=duplicate;
        bus.Registry.Register(new Legacy());c["legacy_module_remains_usable"]=(await bus.ExecuteDetailedAsync(new("legacy_fixture"))).Message=="legacy works";
        c["legacy_description_still_has_explicit_adapter"]=bus.Registry.Describe("legacy_fixture").Runtime=="legacy-adapter";
        var appModule=(IModuleContractSource)bus.Registry.Resolve("local:"+app.Id);c["local_app_contract_is_live"]=ModuleContracts.Snapshot(appModule).Dependencies.All(d=>d.State=="ready");
        c["legacy_application_alias_keeps_executor"]=(await bus.ExecuteDetailedAsync(new("local_app_open",app.Id))).Status=="succeeded"&&starts==1;
        File.Delete(exe);var missing=ModuleContracts.Snapshot(appModule);c["missing_application_file_is_reported_without_launch"]=missing.Dependencies.Single(d=>d.Id=="application-file").State=="missing"&&starts==1&&prepares==1;
        c["missing_dependency_does_not_fake_success"]=(await bus.ExecuteDetailedAsync(new("local_app_open",app.Id))).Status=="failed"&&starts==1;
        repair=true;c["repairable_dependency_keeps_existing_prepare_path"]=(await bus.ExecuteDetailedAsync(new("local_app_open",app.Id))).Status=="succeeded"&&starts==2;
        prefs.ApplicationModules=[app with{Enabled=false}];var disabled=bus.Registry.ReadContracts().Single(d=>d.Contract.Id=="local:"+app.Id);c["disabled_contract_keeps_metadata_but_blocks_execution"]=!disabled.Enabled&&(await bus.ExecuteDetailedAsync(new("local_app_open",app.Id))).ErrorCode=="module_disabled";
        prefs.ApplicationModules=[app];
        var store=new TodoStore(null);int todoWindows=0;var todo=new TodoCapability(store,(work,t)=>{t.ThrowIfCancellationRequested();work();return Task.CompletedTask;},_=>todoWindows++,draft=>draft);bus.Registry.Register(todo);
        c["todo_descriptions_cover_all_existing_actions"]=ModuleContracts.Validate(todo).Actions.Length==todo.Actions.Count;
        c["todo_nested_json_compatibility_is_explicit"]=todo.DescribeModule().Actions.Single(a=>a.Id=="todo_restore").Input.Encoding=="value-wrapper";
        var created=await bus.ExecuteStructuredAsync(new("leo-action/1","todo_add",JsonSerializer.SerializeToElement(new{title="fixture todo",day=DateTime.Today.ToString("yyyy-MM-dd")})));c["todo_structured_input_uses_original_adapter"]=created.Status=="succeeded"&&store.Items.Count==1;
        var listed=await bus.ExecuteDetailedAsync(new("todo_list",DateTime.Today.ToString("yyyy-MM-dd")));c["todo_legacy_input_and_data_shape_unchanged"]=listed.Status=="succeeded"&&listed.Data!.Value.ValueKind==JsonValueKind.Array;
        var now=DateTimeOffset.UtcNow;int fetches=0;using var queries=new FootballQueries(()=>prefs.FootballModule,()=>"fixture",fetchSchedule:(_,_)=>{fetches++;throw new System.Net.Http.HttpRequestException("offline");},loadSchedule:()=>new(){Updated=now,SchemaVersion=2},saveSchedule:_=>{});
        var football=new FootballCapability(()=>prefs.FootballModule,queries,bus.ExecuteDetailedAsync,_=>Task.CompletedTask,()=>[]);bus.Registry.Register(football);
        var footballContract=ModuleContracts.Snapshot(football);c["football_network_unknown_is_optional"]=footballContract.Dependencies.Single(d=>d.Id=="football-source").State=="unknown"&&footballContract.Contract.Dependencies.Single(d=>d.Id=="football-source").Requirement=="optional";
        var cached=await bus.ExecuteStructuredAsync(new("leo-action/1","football_schedule",JsonSerializer.SerializeToElement(new{force=true})));c["offline_football_keeps_original_cache_fallback"]=cached.Status=="succeeded"&&fetches==1&&cached.Data!.Value.GetProperty("RefreshFailed").GetBoolean();
        var mediaState=new MediaCapabilityState();int reads=0;var media=new MediaCapability(()=>prefs.MediaModule,()=>"fixture",_=>{reads++;return Task.FromResult<MediaSession[]>([]);},(_,_,_)=>Task.FromResult(new MediaCommandReceipt("fixture:one","Fixture","Paused",false,"fixture")),(_,_)=>Task.CompletedTask,_=>Task.CompletedTask,new(),mediaState);
        bus.Registry.ReplaceBuiltin(media);c["media_empty_discovery_is_unknown_not_missing"]=ModuleContracts.Snapshot(media).Dependencies.Single(d=>d.Id=="media-provider").State=="unknown";
        c["dependency_description_does_not_probe_media_backend"]=reads==0;
        c["unknown_media_provider_does_not_block_status_query"]=(await bus.ExecuteStructuredAsync(new("leo-action/1","media_status",JsonSerializer.SerializeToElement(new{})))).Status=="succeeded"&&reads==1;
        mediaState.Update([new("fixture:one","Fixture","PRIVATE_MEDIA_TITLE","","Paused")]);c["media_dependency_state_updates_from_existing_discovery"]=ModuleContracts.Snapshot(media).Dependencies.Single(d=>d.Id=="media-provider").State=="ready";
        prefs.MediaModule=false;c["disabled_media_preserves_contract_but_blocks_calls"]=!ModuleContracts.Snapshot(media).Enabled&&(await bus.ExecuteDetailedAsync(new("media_status"))).ErrorCode=="module_disabled";prefs.MediaModule=true;
        var definitions=bus.Registry.ReadContracts();c["registry_exposes_four_pilots_and_opt_in_example"]=definitions.Length==5&&definitions.Select(d=>d.Contract.Kind).ToHashSet().SetEquals(["application","todos","football","media","example"]);
        c["contract_policies_match_existing_runtime_definitions"]=definitions.SelectMany(d=>d.Contract.Actions).All(a=>a.Policy==bus.Registry.Describe(a.Id));
        var exported=ContractInventory.Generate(bus.Registry);c["inventory_excludes_private_application_configuration"]=!exported.Json.Contains(app.Id)&&!exported.Json.Contains(app.Name)&&!exported.Json.Contains(root)&&!exported.Json.Contains("PRIVATE_MEDIA_TITLE");
        c["inventory_uses_anonymous_application_ids"]=exported.Json.Contains("local:example-01")&&exported.Markdown.Contains("local:example-01");
        c["inventory_does_not_export_current_dependency_observations"]=!exported.Json.Contains("configurationIsCurrent")&&!exported.Json.Contains("enabled");
        queries.Dispose();c["closed_query_service_is_reported"]=ModuleContracts.Snapshot(football).Dependencies.Single(d=>d.Id=="query-service").State=="missing";
        c["closed_query_service_cannot_return_fake_success"]=(await bus.ExecuteDetailedAsync(new("football_schedule","{\"force\":true}"))).Status=="failed";
        string corrupt=Path.Combine(root,"corrupt.json");File.WriteAllText(corrupt,"broken");var broken=new TodoStore(corrupt);var degraded=new TodoCapability(broken,(work,_)=>{work();return Task.CompletedTask;},_=>{},d=>d);c["degraded_store_is_reported_without_disclosing_error_text"]=ModuleContracts.Snapshot(degraded).Dependencies.Single().State=="degraded";
        using(var brokenBus=new ActionDispatcher(new(),(_,_,_)=>Task.FromResult("legacy"),new ExtensionStore(Path.Combine(root,"empty")))){brokenBus.Registry.Register(degraded);var failed=await brokenBus.ExecuteDetailedAsync(new("todo_add",JsonSerializer.Serialize(new{title="fixture",day=DateTime.Today.ToString("yyyy-MM-dd")})));c["degraded_store_keeps_existing_write_failure_protection"]=failed.Status=="failed"&&broken.Items.Count==0;}
        using(var pet=new PetForm(new Preferences{ApplicationIndexing=false},true,true))c["actual_host_registers_all_migrated_contracts"]=pet.ContractRegistry.ReadContracts().Select(d=>d.Contract.Kind).ToHashSet().SetEquals(["todos","football","media","notes","proxy","music","clipboard","conversation","match-planning","workflows"]);
    }
}

