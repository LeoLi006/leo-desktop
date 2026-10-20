using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;
static class AdditionalContractTests
{
    sealed class Speech:ISpeechIO
    {
        public int InputDevice{get;set;}=-1;public bool Listening{get;private set;}public bool Finishing{get;private set;}public bool Speaking{get;private set;}public long Generation{get;private set;}public int Starts,Speaks;public bool Closed;
        public bool IsCurrent(long g)=>!Closed&&g==Generation;
        public event Action<string,bool,long>? Recognized{add{}remove{}}public event Action<string,long>? Status{add{}remove{}}public event Action<long>? Changed{add{}remove{}}
        public void Start(){Generation++;Starts++;Listening=true;}public void FinishListening(){Listening=false;Finishing=true;}public void StopListening(){Generation++;Listening=Finishing=false;}public void Speak(string text){Speaks++;Speaking=true;}public void StopSpeaking()=>Speaking=false;public void Stop(){StopListening();StopSpeaking();}public void Dispose(){Closed=true;Stop();}
    }
    sealed class Escalated(ConversationCapability inner):IStructuredLeoModule,IActionRequestAdapter,IContractInputBoundary,IModuleContractSource
    {
        public string Id=>inner.Id;public string Name=>inner.Name;public string Version=>inner.Version;public bool Enabled=>true;public bool ConfigurationIsCurrent=>true;public IReadOnlyList<string> Actions=>inner.Actions;
        public bool AcceptsStructuredAction(string a)=>inner.AcceptsStructuredAction(a);public CapabilityDefinition Describe(string a)=>inner.Describe(a);public string FailureCode(Exception e)=>inner.FailureCode(e);public ActionRequest Adapt(StructuredActionRequest r)=>inner.Adapt(r);public Task<ActionPayload> ExecuteStructuredAsync(ActionRequest r,CancellationToken t)=>inner.ExecuteStructuredAsync(r,t);public Task<string> ExecuteAsync(ActionRequest r,CancellationToken t)=>inner.ExecuteAsync(r,t);
        public DependencyState[] ReadDependencies()=>inner.ReadDependencies();public ModuleContract DescribeModule(){var d=inner.DescribeModule();return d with{Actions=d.Actions.Select(a=>a.Id!="chat_reply"?a:a with{Input=a.Input with{Protocol="leo-action/1",Adapter="module-adapter",Encoding="object"}}).ToArray()};}
    }
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{var c=new Dictionary<string,bool>();try{await Check(ui,Path.GetDirectoryName(report)!,c);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,real_api_requests=0,microphone_starts=0,system_clipboard_reads=0,system_clipboard_writes=0,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{ui.Close();}};Application.Run(ui);
    }
    static async Task Check(Form ui,string root,Dictionary<string,bool> c)
    {
        root=Path.Combine(root,"fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var prefs=new Preferences{ApplicationIndexing=false};
        using var bus=new ActionDispatcher(prefs,(_,_,_)=>throw new Exception("legacy bypass"),new ExtensionStore(Path.Combine(root,"extensions")));var windows=new ModuleWindowService(ui);
        int noteReads=0,availabilityReads=0,proxyReads=0,sets=0,searches=0,plays=0,clipWindows=0,workflowRuns=0,matchReads=0;
        var notes=new NoteStore(null);var noteModule=new NotesCapability(()=>{noteReads++;return notes;},(_,_,_)=>Task.CompletedTask);bus.Registry.Register(noteModule);
        bool on=false;string config="initial";
        var proxy=new ProxyCapability(()=>prefs.ClashModule,()=>config,()=>{availabilityReads++;return true;},_=>{proxyReads++;return Task.FromResult(new ProxyStatus(true,on,"fixture proxy"));},(value,_,_)=>{sets++;on=value;return Task.FromResult(new ProxyStatus(true,on,"fixture proxy"));},new());bus.Registry.ReplaceBuiltin(proxy);
        var musicSession=new MusicSession(new(null));MusicSong[] songs=[new("123","fixture","PRIVATE_SONG_TITLE","PRIVATE_ARTIST","")];
        var music=new MusicCapability(()=>prefs.MusicModule,()=>"initial",()=>{availabilityReads++;return true;},musicSession,(_,_)=>{searches++;return Task.FromResult(songs);},(_,_,_)=>{plays++;return Task.CompletedTask;},(_,_)=>Task.FromResult(new MusicControlReceipt("fixture pause",true)),(_,_)=>Task.FromResult(new MusicControlReceipt("fixture lyrics")),(_,_)=>Task.CompletedTask,()=>new(true,true),_=>Task.CompletedTask,new());bus.Registry.ReplaceBuiltin(music);
        using var clipboard=new ClipboardHistoryService(prefs,true);var clip=new ClipboardCapability(clipboard,prefs,(work,t)=>{t.ThrowIfCancellationRequested();work();return Task.CompletedTask;},()=>clipWindows++);bus.Registry.Register(clip);
        using var conversationHost=new ConversationHost(bus,windows,()=>{});var conversation=(ConversationCapability)bus.Registry.Resolve("chat_open");
        var now=DateTimeOffset.Now;var match=new FootballMatch("123456",now.AddHours(4),"fixture","83","Barcelona","86","Real Madrid","pre","Scheduled","VS",true);var tasks=new TodoStore(null);
        var planning=new MatchPlanningHost(()=>{matchReads++;return new[]{(match,now)};});var matchModule=new MatchPlanningCapability(()=>prefs.FootballModule,planning,tasks,windows);bus.Registry.Register(matchModule);
        var workflow=new WorkflowCapability((r,t)=>{t.ThrowIfCancellationRequested();workflowRuns++;return Task.FromResult(new ActionPayload("fixture completed",JsonSerializer.SerializeToElement(new{completed=1}),"steps-completed",true));});bus.Registry.Register(workflow);
        var snapshots=bus.Registry.ReadContracts();c["seven_capability_families_registered"]=snapshots.Length==7;c["all_added_action_policies_match_original_dispatch"]=snapshots.SelectMany(s=>s.Contract.Actions).All(a=>a.Policy==bus.Registry.Describe(a.Id));
        c["dependency_snapshots_do_not_load_lazy_notes"]=noteReads==0;c["dependency_snapshots_do_not_probe_clients_or_network"]=availabilityReads==0&&proxyReads==0&&sets==0&&searches==0&&plays==0;
        c["dependency_snapshots_do_not_read_match_sources_or_run_workflows"]=matchReads==0&&workflowRuns==0;c["lazy_note_dependency_is_explicitly_unknown"]=ModuleContracts.Snapshot(noteModule).Dependencies.Single().State=="unknown";
        Task<ActionResult> Run(string a,string v="",string source="fixture")=>bus.ExecuteDetailedAsync(new(a,v,source));
        Task<ActionResult> Typed(string a,object parameters,string source="fixture")=>bus.ExecuteStructuredAsync(new("leo-action/1",a,JsonSerializer.SerializeToElement(parameters),source));
        var added=await Typed("note_add",new{text="PRIVATE_NOTE_BODY"});c["note_structured_input_keeps_existing_save"]=added.Status=="succeeded"&&notes.Items.Count==1;
        c["note_result_evidence_matches_contract"]=noteModule.DescribeModule().Actions.Single(a=>a.Id=="note_add").Output.Evidence.Contains(added.Evidence);
        c["legacy_note_query_keeps_data_shape"]=(await Run("note_query","{\"text\":\"\",\"scope\":\"all\"}")).Data!.Value.ValueKind==JsonValueKind.Array;
        c["note_private_results_do_not_leak_into_diagnostics"]=bus.RecentResults.Where(r=>r.ModuleId=="notes").All(r=>r.Data==null&&!r.Message.Contains("PRIVATE_NOTE_BODY"));
        c["proxy_actions_keep_original_state_verification"]=(await Typed("proxy_on",new{})).Evidence=="state-observed"&&on&&sets==1;
        config="changed";c["proxy_configuration_change_is_visible"]=!ModuleContracts.Snapshot(proxy).ConfigurationIsCurrent;config="initial";
        prefs.ClashModule=false;c["disabled_proxy_still_blocks_legacy_action"]=(await Run("proxy_on")).ErrorCode=="module_disabled";prefs.ClashModule=true;
        c["proxy_extra_parameters_remain_rejected"]=(await Typed("proxy_on",new{path="not allowed"})).Status=="failed";
        var musicRows=await Typed("music_search",new{query="fixture"});c["music_query_keeps_actual_candidate_ids"]=musicRows.Status=="succeeded"&&musicRows.Data!.Value[0].GetProperty("Id").GetString()=="123";
        c["music_selection_dependency_uses_only_existing_snapshot"]=ModuleContracts.Snapshot(music).Dependencies.Single(d=>d.Id=="song-selection").State=="ready";
        c["music_point_song_keeps_accepted_evidence"]=(await Run("music_play","123")).Evidence=="accepted"&&plays==1;
        var paused=await Run("music_pause");c["confirmed_pause_evidence_is_described"]=paused.Evidence=="state-observed"&&music.DescribeModule().Actions.Single(a=>a.Id=="music_pause").Output.Evidence.Contains(paused.Evidence);
        prefs.MusicModule=false;c["disabled_music_keeps_contract_and_blocks_call"]=!ModuleContracts.Snapshot(music).Enabled&&(await Run("music_play","123")).ErrorCode=="module_disabled";prefs.MusicModule=true;
        c["clipboard_local_text_pin_keeps_existing_path"]=(await Typed("clipboard_pin_text",new{text="PRIVATE_CLIPBOARD_BODY"})).Status=="succeeded"&&clipboard.History.Items.Count==1;
        c["model_cannot_query_clipboard_body"]=(await Typed("clipboard_query",new{query=""},"brain-fixture")).Status=="failed";
        c["model_cannot_pin_clipboard_body"]=(await Typed("clipboard_pin_text",new{text="PRIVATE_BLOCKED"},"brain-fixture")).Status=="failed"&&clipboard.History.Items.Count==1;
        c["model_can_only_open_existing_clipboard_window"]=(await Typed("clipboard_open",new{},"brain-fixture")).Status=="succeeded"&&clipWindows==1;
        await Typed("clipboard_recording",new{enabled=false});c["clipboard_recording_off_keeps_saved_items_and_capability"]=ModuleContracts.Snapshot(clip).Enabled&&clipboard.History.Items.Count==1&&!prefs.ClipboardHistory;
        c["conversation_private_actions_are_not_advertised_as_structured"]=conversation.DescribeModule().Actions.Where(a=>a.Id!="chat_open").All(a=>a.Input.Protocol=="ActionRequest"&&a.Input.Adapter=="host-private"&&a.Input.Encoding=="private-handle");
        foreach(var action in conversation.Actions.Where(a=>a!="chat_open"))c["external_"+action+"_rejected"]=(await Typed(action,new{})).Status=="failed";
        c["conversation_open_keeps_empty_object_adapter"]=(await Typed("chat_open",new{})).Status=="succeeded";
        bool escalated=false;try{ModuleContracts.Validate(new Escalated(conversation));}catch(CapabilityException e)when(e.Code=="invalid_contract"){escalated=true;}c["contract_cannot_relabel_private_action_as_public"]=escalated;
        var speech=new Speech();using var client=conversationHost.Attach(speech);client.Voice("voice_start");c["host_voice_control_uses_fake_backend_only"]=speech.Starts==1&&speech.Listening;
        c["voice_receipt_still_deduplicates_generation"]=client.AcceptVoice(speech.Generation)&&!client.AcceptVoice(speech.Generation);
        client.Speak("PRIVATE_SPEECH_TEXT");c["host_speech_request_keeps_existing_private_ticket"]=speech.Speaks==1;
        var held=new TaskCompletionSource<ChatReply>(TaskCreationOptions.RunContinuationsAsynchronously);var reply=client.Reply((text,detail,t)=>{text("PRIVATE_CHAT_TEXT");return held.Task;},_=>{},_=>{},"chat","PRIVATE_API_KEY",default);
        var sessionIds=((System.Collections.IDictionary)typeof(ConversationHost).GetField("sessions",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(conversationHost)!).Keys.Cast<string>().ToArray();
        var jobIds=((System.Collections.IDictionary)typeof(ConversationHost).GetField("jobs",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(conversationHost)!).Keys.Cast<string>().ToArray();
        var inventory=ContractInventory.Generate(bus.Registry);string combined=inventory.Json+inventory.Markdown;
        c["inventory_excludes_notes_clips_songs_and_chat_text"]=new[]{"PRIVATE_NOTE_BODY","PRIVATE_CLIPBOARD_BODY","PRIVATE_SONG_TITLE","PRIVATE_ARTIST","PRIVATE_SPEECH_TEXT","PRIVATE_CHAT_TEXT","PRIVATE_API_KEY"}.All(s=>!combined.Contains(s));
        c["inventory_excludes_live_session_and_job_handles"]=sessionIds.Length==1&&jobIds.Length==1&&sessionIds.Concat(jobIds).All(id=>!combined.Contains(id));
        held.SetResult(new("PRIVATE_RESPONSE_BODY",false,false));await reply;c["conversation_completion_evidence_matches_existing_reply"]=bus.RecentResults.Last(r=>r.ActionId=="chat_reply").Evidence=="completed";
        c["conversation_diagnostics_keep_reply_private"]=bus.RecentResults.Where(r=>r.ModuleId=="conversation").All(r=>r.Data==null&&!r.Message.Contains("PRIVATE_RESPONSE_BODY"));
        var lookup=await Run("match_lookup",match.Id);c["match_lookup_keeps_cached_reference_evidence"]=lookup.Evidence=="cached"&&lookup.Data!.Value.GetProperty("Reference").GetProperty("EventId").GetString()==match.Id;
        await Run("match_add",match.Id);var duplicate=await Run("match_add",match.Id);c["match_reexecution_keeps_original_todo_dedup"]=tasks.Items.Count==1&&duplicate.Data!.Value.GetProperty("Added").GetBoolean()==false;
        c["match_unknown_identifier_still_rejected"]=(await Run("match_add","999999")).Status=="failed";
        c["workflow_describes_legacy_only_input"]=workflow.DescribeModule().Actions.All(a=>a.Input.Protocol=="ActionRequest"&&a.Input.Adapter=="legacy-only"&&a.Input.Encoding=="legacy-value");
        c["workflow_does_not_gain_structured_permission"]=(await Typed("combo_name",new{value="PRIVATE_COMBO_NAME"})).Status=="failed"&&workflowRuns==0;
        var completed=await Run("combo_name","PRIVATE_COMBO_NAME");c["workflow_keeps_existing_delegate_and_evidence"]=completed.Evidence=="steps-completed"&&workflowRuns==1;
        c["workflow_contract_does_not_export_chosen_combo"]=!ContractInventory.Generate(bus.Registry).Json.Contains("PRIVATE_COMBO_NAME");
        var brain=new BrainCatalog(bus.Registry).Read().Select(x=>x.Action.Id).ToHashSet();c["contract_does_not_expand_clipboard_brain_tools"]=brain.Where(a=>a.StartsWith("clipboard_")).ToHashSet().SetEquals(["clipboard_open"]);c["contract_does_not_expand_private_voice_brain_tools"]=brain.Where(a=>conversation.Actions.Contains(a)).ToHashSet().SetEquals(["chat_open"]);
        client.Dispose();c["session_close_preserves_released_evidence"]=bus.RecentResults.Any(r=>r.ActionId=="chat_close"&&r.Evidence=="released");
        conversationHost.Dispose();clipboard.Dispose();c["closed_conversation_dependency_is_reported"]=ModuleContracts.Snapshot(conversation).Dependencies.Single(d=>d.Id=="conversation-host").State=="missing";c["closed_clipboard_dependency_is_reported"]=ModuleContracts.Snapshot(clip).Dependencies.Single(d=>d.Id=="history-store").State=="missing";
        using var pet=new PetForm(new Preferences{ApplicationIndexing=false,ApplicationModules=[new("0123456789abcdef0123456789abcdef","PRIVATE_APPLICATION_NAME",@"C:\PRIVATE_APPLICATION_PATH\app.exe")]},true,true);var actual=pet.ContractRegistry.ReadContracts();
        c["actual_host_has_eleven_contract_families"]=actual.Length==11;c["actual_host_inventory_covers_ninety_one_actions"]=actual.Sum(s=>s.Contract.Actions.Length)==91;
        var publicFiles=ContractInventory.Generate(pet.ContractRegistry);c["expanded_inventory_keeps_application_privacy"]=!publicFiles.Json.Contains("PRIVATE_APPLICATION_NAME")&&!publicFiles.Json.Contains("PRIVATE_APPLICATION_PATH")&&!publicFiles.Json.Contains("0123456789abcdef0123456789abcdef");
    }
}
