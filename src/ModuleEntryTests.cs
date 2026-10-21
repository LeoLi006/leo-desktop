using System.Text.Json;
namespace LeoLauncher;
static class ModuleEntryTests
{
    sealed class Fixture(string id):IStructuredLeoModule,IActionRequestAdapter,IModuleContractSource,IModuleEntryContributor
    {
        public string Id=>id;public string Name=>"隔离入口";public string Version=>"1";public bool Enabled{get;set;}=true;public bool ConfigurationIsCurrent=>true;
        public bool Duplicate,WrongOwner,ThrowEntries,ThrowExecution,WrongMenu;public int Calls;public string? ActionOverride;public TaskCompletionSource? Hold;
        public IReadOnlyList<string> Actions=>[ActionOverride??id+"_status"];public int MaximumInputLength=>1000;
        public DependencyState[] ReadDependencies()=>[];
        public CapabilityDefinition Describe(string action)=>new(action,Id,"in-process","empty-object","read","coalesce-inflight",5000);
        public ModuleContract DescribeModule()=>ModuleContracts.Build(this,"fixture",Name,"optional","none",[],_ =>("object","空对象","fixture","就绪",false,["snapshot"]));
        public ActionRequest Adapt(StructuredActionRequest request)=>ClientCapabilityParameters.Adapt(request,Actions,false);
        public EntryContribution[] DescribeEntries(){if(ThrowEntries)throw new IOException("fixture entries");var entry=new EntryContribution(Id+"/status",WrongOwner?"other":Id,Actions[0],Name,"",true,EntryDisplay.Shortcut,"leo-module-entry:"+Id+"/status",[],"module-action"){MenuActions=[new("查询",WrongMenu?"terminal_open":Actions[0],"{}")]};return Duplicate?[entry,entry]:[entry];}
        public async Task<ActionPayload> ExecuteStructuredAsync(ActionRequest request,CancellationToken token){token.ThrowIfCancellationRequested();Calls++;if(ThrowExecution)throw new IOException("fixture execution");if(Hold!=null)await Hold.Task.WaitAsync(token);return new ActionPayload("就绪",JsonSerializer.SerializeToElement(new{ready=true}),"snapshot");}
        public async Task<string> ExecuteAsync(ActionRequest request,CancellationToken token)=>(await ExecuteStructuredAsync(request,token)).Message;
        public string FailureCode(Exception error)=>"fixture_failed";
    }
    static async Task<bool> Rejected(Func<Task> action){try{await action();return false;}catch(InvalidOperationException){return true;}catch(OperationCanceledException){return true;}}
    public static void Run(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{
            var c=new Dictionary<string,bool>();try{
                var prefs=new Preferences{ApplicationIndexing=false};string root=Path.Combine(Path.GetDirectoryName(path)!,"empty-extensions");using var bus=new ActionDispatcher(prefs,(_,_,_)=>throw new Exception("legacy bypass"),new ExtensionStore(root));
                var config=new ExampleNotesConfiguration(true);var notes=new NoteStore(null);notes.Save(null,"isolated entry fixture");using var services=new ScopedNotesExampleServices(notes,()=>config);var sample=new ScopedNotesExampleModule(services);bus.Registry.Register(sample);
                const string id="example-scoped-notes/query";ModuleEntrySnapshot Read()=>ModuleEntryContributions.Read(prefs,bus.Registry);
                var snapshot=Read();c["one_registration_contributes_three_views"]=snapshot.Entries.Length==1&&snapshot.Buttons.Single().Id==id&&snapshot.Menu.Single().Id==id&&snapshot.Search.Single().Id==id;
                var main=await ModuleEntryContributions.Execute(bus,prefs,id,"button");var menu=await ModuleEntryContributions.Execute(bus,prefs,id,"menu",menuIndex:0);var search=await ModuleEntryContributions.Execute(bus,prefs,id,"search");
                c["all_views_reuse_registered_execution"]=new[]{main,menu,search}.All(r=>r.Status=="succeeded"&&r.ModuleId==sample.Id&&r.ActionId=="example_notes_query"&&r.Data!.Value.GetArrayLength()==1)&&menu.Source=="menu"&&search.Source=="search";
                c["query_entry_does_not_save"]=notes.Items.Count==1;
                prefs.EntryDisplayModes[id]=EntryDisplay.Search;snapshot=Read();c["search_only_hides_button_and_menu"]=snapshot.Buttons.Length==0&&snapshot.Menu.Length==0&&snapshot.Search.Length==1;
                c["hidden_entry_keeps_capability_enabled"]=(await ModuleEntryContributions.Execute(bus,prefs,id,"search")).Status=="succeeded";
                prefs.EntryDisplayModes[id]=EntryDisplay.Menu;snapshot=Read();c["menu_only_mode_preserved"]=snapshot.Buttons.Length==0&&snapshot.Menu.Length==1&&snapshot.Search.Length==1;
                config=config with{Enabled=false};bus.CancelDisabled();snapshot=Read();c["module_disable_removes_all_visible_views"]=snapshot.Entries.Length==1&&snapshot.Buttons.Length==0&&snapshot.Menu.Length==0&&snapshot.Search.Length==0;
                c["stale_disabled_click_rejected"]=await Rejected(()=>ModuleEntryContributions.Execute(bus,prefs,id,"stale"));config=config with{Enabled=true};
                c["reenable_reads_current_mode"]=Read().Menu.Length==1&&Read().Buttons.Length==0;
                var peer=new Fixture("peer-entry");bus.Registry.Register(peer);c["second_module_needs_no_catalog_branch"]=Read().Entries.Length==2;
                peer.Duplicate=true;c["duplicate_entry_isolates_provider"]=Read().Entries.Length==1&&Read().Search.Single().ModuleId==sample.Id;peer.Duplicate=false;
                peer.WrongOwner=true;c["wrong_owner_isolates_provider"]=Read().Entries.Length==1;peer.WrongOwner=false;
                peer.WrongMenu=true;c["cross_module_menu_rejected"]=Read().Entries.Length==1;peer.WrongMenu=false;
                peer.ThrowEntries=true;c["provider_exception_preserves_other_module"]=Read().Entries.Single().ModuleId==sample.Id;peer.ThrowEntries=false;
                peer.ThrowExecution=true;var failure=await ModuleEntryContributions.Execute(bus,prefs,"peer-entry/status","fixture");c["execution_failure_is_structured_and_isolated"]=failure.Status=="failed"&&(await ModuleEntryContributions.Execute(bus,prefs,id,"fixture")).Status=="succeeded";peer.ThrowExecution=false;
                using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();int before=peer.Calls;c["cancelled_click_never_executes"]=await Rejected(()=>ModuleEntryContributions.Execute(bus,prefs,"peer-entry/status","fixture",cancelled.Token))&&peer.Calls==before;}
                var held=Read().Search.Single(e=>e.Id==id);held.Aliases[0]="changed snapshot";c["snapshot_changes_do_not_rewrite_provider"]=Read().Search.Single(e=>e.Id==id).Aliases[0]=="示例查询";
                peer.Hold=new(TaskCreationOptions.RunContinuationsAsynchronously);var pending=ModuleEntryContributions.Execute(bus,prefs,"peer-entry/status","pending");
                c["remove_uses_registered_identity"]=!bus.Registry.Unregister(new Fixture("peer-entry"))&&bus.Registry.Unregister(peer);bus.CancelDisabled();
                c["removal_cancels_pending_through_existing_dispatcher"]=(await pending).Status=="cancelled";peer.Hold.TrySetResult();
                c["removed_entry_disappears_from_all_views"]=Read().Entries.All(e=>e.ModuleId!=peer.Id);
                c["stale_removed_click_never_falls_back_to_path"]=await Rejected(()=>ModuleEntryContributions.Execute(bus,prefs,"peer-entry/status","stale"));
                c["duplicate_module_registration_rejected"]=await Rejected(()=>{bus.Registry.Register(new ScopedNotesExampleModule(services));return Task.CompletedTask;});
                c["provider_cannot_take_over_builtin_action"]=await Rejected(()=>{bus.Registry.Register(new Fixture("hijack-fixture"){ActionOverride="terminal_open"});return Task.CompletedTask;});
                c["sample_parameters_are_not_sent_to_model_catalog"]=!new BrainCatalog(bus.Registry).Read().Any(e=>e.Module==sample.Id);
                c["unregister_is_idempotent"]=bus.Registry.Unregister(sample)&&!bus.Registry.Unregister(sample);bus.CancelDisabled();c["all_sample_entries_removed"]=Read().Entries.Length==0;
                c["legacy_builtin_descriptors_unchanged"]=EntryContributions.ReadBuiltins(prefs).Length==6&&EntryContributions.ReadAvailable(prefs).Length==0;
                File.WriteAllText(path,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,real_api_requests=0,external_process_starts=0,personal_data_reads=0}));
            }catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{ui.Close();}
        };Application.Run(ui);
    }
}
