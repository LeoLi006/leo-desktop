using System.Text.Json;
namespace LeoLauncher;
static class ScopedServicesTests
{
    sealed class GateServices(IExampleNotesServices inner):IExampleNotesServices
    {
        public TaskCompletionSource? Hold;public bool Fail,Missing,Entered,FailConfiguration,FailDependencies;
        public ExampleNotesConfiguration Configuration=>FailConfiguration?throw new IOException("fixture configuration failed"):inner.Configuration;
        public DependencyState[] Dependencies()=>FailDependencies?throw new IOException("fixture dependencies failed"):Missing?[new("scoped-notes","missing")]:inner.Dependencies();
        public Task<ActionPayload> Query(string text,CancellationToken token)=>Fail?throw new IOException("fixture service failed"):inner.Query(text,token);
        public async Task<ActionPayload> Save(string text,string id,CancellationToken token){Entered=true;if(Hold!=null)await Hold.Task.WaitAsync(token);return await inner.Save(text,id,token);}
    }
    public static void Run(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{
            var c=new Dictionary<string,bool>();try{
                string root=Path.Combine(Path.GetDirectoryName(path)!,"fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
                string file=Path.Combine(root,"notes.json");bool writeFails=false;int writes=0,changed=0;
                var store=new NoteStore(file,(p,s)=>{if(writeFails)throw new IOException("injected save failure");ConfigMigration.Write(p,s);writes++;});store.Changed+=()=>changed++;
                var unrelated=new NoteStore(null);unrelated.Save(null,"unrelated isolated data");
                var config=new ExampleNotesConfiguration(true,40);using var services=new ScopedNotesExampleServices(store,()=>config);var gate=new GateServices(services);
                using var bus=new ActionDispatcher(new(),(_,_,_)=>throw new Exception("legacy bypass"),new ExtensionStore(Path.Combine(root,"empty-modules")));
                var module=new ScopedNotesExampleModule(gate);bus.Registry.Register(module);
                Task<ActionResult> Send(string action,object parameters,string? id=null,CancellationToken token=default)=>bus.ExecuteStructuredAsync(new("leo-action/1",action,JsonSerializer.SerializeToElement(parameters),"isolated-example",id,"example-trace"),token);
                c["registered_contract_with_scoped_dependency"]=ModuleContracts.Validate(module).DataOwner=="host.scoped-example-notes"&&ModuleContracts.Snapshot(module).Dependencies.Single().State=="ready";
                var first=await Send("example_notes_save",new{text="isolated example"},"once");var saved=first.Data!.Value.Deserialize<StickyNote>()!;
                c["structured_result_and_commit_evidence"]=first.Status=="succeeded"&&first.ModuleId==module.Id&&first.TraceId=="example-trace"&&first.Evidence=="committed"&&first.PrivateData;
                var again=await Send("example_notes_save",new{text="isolated example"},"once");c["replay_reuses_existing_notes_dedup"]=again.Status=="succeeded"&&again.Data!.Value.Deserialize<StickyNote>()!.Id==saved.Id&&writes==1&&changed==1;
                c["reused_id_with_changed_content_rejected"]=(await Send("example_notes_save",new{text="different"},"once")).Status=="failed"&&writes==1;
                c["unknown_fields_rejected"]=(await Send("example_notes_save",new{text="x",path="not allowed"})).ErrorCode=="invalid_request"&&writes==1;
                c["duplicate_fields_rejected"]=(await bus.ExecuteStructuredAsync(new("leo-action/1","example_notes_save",JsonDocument.Parse("{\"text\":\"a\",\"text\":\"b\"}").RootElement.Clone(),"fixture"))).Status=="failed";
                var read=await Send("example_notes_query",new{text="isolated"});c["query_is_private_snapshot"]=read.Status=="succeeded"&&read.Evidence=="snapshot"&&read.Data!.Value.GetArrayLength()==1&&read.PrivateData;
                var copy=store.Query();copy.Clear();c["store_query_returns_independent_list"]=store.Items.Count==1;
                c["service_cannot_reach_other_store"]=unrelated.Items.Count==1&&store.Items.Single().Id==saved.Id;
                string before=File.ReadAllText(file);writeFails=true;var failed=await Send("example_notes_save",new{text="retry this save"},"retry");
                c["failed_save_preserves_disk_memory_and_events"]=failed.ErrorCode=="storage_failed"&&File.ReadAllText(file)==before&&store.Items.Count==1&&changed==1;
                writeFails=false;c["failed_request_can_be_explicitly_retried"]=(await Send("example_notes_save",new{text="retry this save"},"retry")).Status=="succeeded"&&store.Items.Count==2;
                config=config with{MaximumTextLength=3};c["scoped_config_change_applies_without_global_preferences"]=(await Send("example_notes_save",new{text="too long"})).Status=="failed";config=config with{MaximumTextLength=40};
                config=config with{Enabled=false};c["disabled_module_rejected"]=(await Send("example_notes_save",new{text="disabled"})).Status=="failed"&&store.Items.Count==2;config=config with{Enabled=true};
                gate.Missing=true;c["missing_dependency_blocks_data_access"]=(await Send("example_notes_save",new{text="missing"})).Status=="failed"&&store.Items.Count==2;gate.Missing=false;
                gate.Fail=true;c["service_failure_is_structured"]=(await Send("example_notes_query",new{text=""})).ErrorCode=="storage_failed";gate.Fail=false;
                gate.FailConfiguration=true;c["configuration_failure_stays_inside_dispatch_boundary"]=(await Send("example_notes_query",new{text=""})).Status=="failed";gate.FailConfiguration=false;
                gate.FailDependencies=true;c["dependency_service_failure_is_structured"]=(await Send("example_notes_query",new{text=""})).Status=="failed";gate.FailDependencies=false;
                c["service_failure_does_not_break_other_registered_module"]=await CheckSibling(bus);
                gate.Hold=new(TaskCreationOptions.RunContinuationsAsynchronously);gate.Entered=false;using var cancel=new CancellationTokenSource();var pending=Send("example_notes_save",new{text="cancelled"},"cancel",cancel.Token);
                while(!gate.Entered)await Task.Delay(5);cancel.Cancel();var cancelled=await pending;gate.Hold.TrySetResult();await Task.Delay(20);
                c["cancel_before_data_call_does_not_save"]=cancelled.Status=="cancelled"&&store.Items.Count==2;
                gate.Hold=new(TaskCreationOptions.RunContinuationsAsynchronously);gate.Entered=false;var closing=Send("example_notes_save",new{text="closed"},"close");while(!gate.Entered)await Task.Delay(5);services.Dispose();services.Dispose();gate.Hold.TrySetResult();
                var closed=await closing;c["service_close_rejects_late_save"]=closed.Status!="succeeded"&&store.Items.Count==2;
                c["closed_service_cannot_restart"]=(await Send("example_notes_save",new{text="after close"})).Status=="failed";
                c["closed_service_releases_data_activity"]=!ModuleActivities.Snapshot().Any(r=>r.Module==module.Id);
                c["diagnostics_exclude_business_payloads"]=bus.RecentResults.Where(r=>r.ModuleId==module.Id).All(r=>r.Data==null)&&!JsonSerializer.Serialize(bus.RecentResults).Contains("isolated example");
                bus.Dispose();c["dispatcher_close_rejects_new_sample_work"]=(await Send("example_notes_query",new{text=""})).Status!="succeeded";
                File.WriteAllText(path,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,real_api_requests=0,external_process_starts=0,personal_data_reads=0}));
            }catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{ui.Close();}
        };Application.Run(ui);
    }
    static async Task<bool> CheckSibling(ActionDispatcher bus){bus.Registry.Register(new MinimalContractModule());return (await bus.ExecuteStructuredAsync(new("leo-action/1","example_status",JsonSerializer.SerializeToElement(new{}),"fixture"))).Status=="succeeded";}
}
