using System.Text.Json;
using System.Reflection;
namespace LeoLauncher;
static class M05ClientTests
{
    sealed class FakeMedia:IMediaProvider
    {
        public string Id=>"fake";public MediaSession[] Rows=[new("fake:one","PotPlayer","Fixture one","","Paused")];
        public int Controls,Reads;public bool FailVerify,KeepUnknown;public string LastId="";public TaskCompletionSource? Hold;
        public Task<MediaSession[]> ReadAsync(CancellationToken t){t.ThrowIfCancellationRequested();Reads++;if(FailVerify&&Controls>0)throw new IOException("verification unavailable");return Task.FromResult(Rows.ToArray());}
        public async Task<string> ControlAsync(MediaSession s,string a,CancellationToken t){Controls++;LastId=s.Id;if(Hold!=null)await Hold.Task.WaitAsync(t);t.ThrowIfCancellationRequested();if(!KeepUnknown)Rows=Rows.Select(x=>x.Id!=s.Id?x:x with{Status=a switch{"play"=>"Playing","pause"=>"Paused","stop"=>"Stopped",_=>x.Status}}).ToArray();return "accepted";}
    }
    static IEnumerable<Control> Walk(Control c){foreach(Control child in c.Controls){yield return child;foreach(var nested in Walk(child))yield return nested;}}
    static async Task Until(Func<bool> p){var end=DateTime.UtcNow.AddSeconds(3);while(!p()&&DateTime.UtcNow<end)await Task.Delay(10);if(!p())throw new TimeoutException("fixture deadline");}
    static ToolCall Tool(string name,object value)=>new(Guid.NewGuid().ToString("N"),name,JsonSerializer.Serialize(value));
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{try{var checks=await Check(Path.GetDirectoryName(report)!);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(x=>x),checks,real_api_requests=0,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}finally{ui.Close();}};Application.Run(ui);
    }
    static async Task<Dictionary<string,bool>> Check(string root)
    {
        var c=new Dictionary<string,bool>();var prefs=new Preferences{ApplicationIndexing=false};string config="initial";bool available=true;int sets=0,legacy=0,opens=0,windows=0;ProxyStatus status=new(false,null,"not running");
        var proxyQueue=new NativeCapabilityQueue();var mediaQueue=new NativeCapabilityQueue();var mediaState=new MediaCapabilityState();var provider=new FakeMedia();var service=new MediaService([provider]);
        TaskCompletionSource? proxyHold=null;bool unconfirmed=false;
        ProxyCapability Proxy()=>new(()=>prefs.ClashModule,()=>config,()=>available,t=>{t.ThrowIfCancellationRequested();return Task.FromResult(status);},async(on,t,report)=>{sets++;report("正在静默启动 Clash…");if(proxyHold!=null)await proxyHold.Task.WaitAsync(t);t.ThrowIfCancellationRequested();status=unconfirmed?new(true,null,"unknown"):new(true,on,"observed");return status;},proxyQueue);
        MediaCapability Media()=>new(()=>prefs.MediaModule,()=>config,service.ReadAsync,service.ExecuteVerifiedAsync,(file,t)=>{t.ThrowIfCancellationRequested();opens++;return Task.CompletedTask;},t=>{t.ThrowIfCancellationRequested();windows++;return Task.CompletedTask;},mediaQueue,mediaState);
        using var bus=new ActionDispatcher(prefs,(_,_,_)=>{legacy++;return Task.FromResult("legacy");},new ExtensionStore(Path.Combine(root,"extensions")));
        bus.Registry.ReplaceBuiltin(Proxy());bus.Registry.ReplaceBuiltin(Media());
        c["registry_has_single_owner_per_migrated_module"]=bus.Modules.Count(m=>m.Id=="clash")==1&&bus.Modules.Count(m=>m.Id=="media")==1&&bus.Registry.Resolve("media_pause") is MediaCapability&&bus.Registry.Resolve("proxy_on") is ProxyCapability;
        c["proxy_rejects_legacy_extra_arguments"]=(await bus.ExecuteDetailedAsync(new("proxy_on","shell.exe"))).ErrorCode=="invalid_request"&&sets==0;
        var extra=await bus.ExecuteStructuredAsync(new("leo-action/1","proxy_on",JsonSerializer.SerializeToElement(new{extra="bad"})));c["proxy_structured_extra_fields_rejected"]=extra.ErrorCode=="invalid_request"&&sets==0;
        c["media_structured_extra_fields_rejected"]=(await bus.ExecuteStructuredAsync(new("leo-action/1","media_play",JsonSerializer.SerializeToElement(new{session="fake:one",extra="bad"})))).ErrorCode=="invalid_request";
        var queried=await bus.ExecuteDetailedAsync(new("proxy_status"));c["proxy_unknown_read_is_not_claimed_as_successful_switch"]=queried.Evidence=="unknown"&&JsonSerializer.Deserialize<ProxyStatus>(queried.Message)!.Enabled==null;
        proxyHold=new(TaskCreationOptions.RunContinuationsAsynchronously);var one=bus.ExecuteDetailedAsync(new("proxy_on","","chat"));var two=bus.ExecuteDetailedAsync(new("proxy_on","","voice"));await Until(()=>sets==1);proxyHold.SetResult();var receipts=await Task.WhenAll(one,two);proxyHold=null;
        c["cross_entry_duplicate_proxy_requests_share_execution"]=sets==1&&receipts[0].ExecutionId==receipts[1].ExecutionId&&receipts.Select(x=>x.Source).SequenceEqual(new[]{"chat","voice"});
        c["cold_proxy_start_waits_for_confirmed_result"]=receipts.All(x=>x.Evidence=="state-observed"&&x.Data!.Value.Deserialize<ProxyStatus>()!.Enabled==true);
        proxyHold=new(TaskCreationOptions.RunContinuationsAsynchronously);int competingBefore=sets;var off=bus.ExecuteDetailedAsync(new("proxy_off"));var on=bus.ExecuteDetailedAsync(new("proxy_on"));await Until(()=>sets==competingBefore+1);c["opposite_proxy_targets_are_serialized"]=!off.IsCompleted&&!on.IsCompleted;proxyHold.SetResult();await Task.WhenAll(off,on);proxyHold=null;c["queued_proxy_finishes_with_last_explicit_target"]=status.Enabled==true&&sets==competingBefore+2;
        await bus.ExecuteTextAsync("关闭系统代理","search");c["local_proxy_command_stays_local"]=status.Enabled==false&&legacy==0;
        unconfirmed=true;var unknown=await bus.ExecuteDetailedAsync(new("proxy_on"));c["unconfirmed_switch_is_failure"]=unknown.Status=="failed"&&unknown.ErrorCode=="proxy_unconfirmed";unconfirmed=false;
        var catalog=new BrainCatalog(bus.Registry);available=false;c["missing_proxy_dependency_removed_from_brain"]=!catalog.Read().Any(e=>e.Module=="clash");available=true;
        using(var cancel=new CancellationTokenSource()){
            proxyHold=new(TaskCreationOptions.RunContinuationsAsynchronously);int before=sets;var a=bus.ExecuteDetailedAsync(new("proxy_on"),cancel.Token);await Until(()=>sets==before+1);cancel.Cancel();c["proxy_wait_can_be_cancelled"]=(await a).Status=="cancelled";proxyHold.SetResult();proxyHold=null;await Task.Delay(25);
        }
        proxyHold=new(TaskCreationOptions.RunContinuationsAsynchronously);int n=sets;var active=bus.ExecuteDetailedAsync(new("proxy_on"));await Until(()=>sets==n+1);prefs.ClashModule=false;bus.CancelDisabled();c["disable_cancels_active_proxy_and_hides_model_tools"]=(await active).Status=="cancelled"&&!catalog.Read().Any(x=>x.Module=="clash");proxyHold.SetResult();proxyHold=null;prefs.ClashModule=true;await Task.Delay(25);
        c["media_control_not_advertised_before_session_query"]=!catalog.Read().Any(e=>e.Action.Id=="media_pause");
        var list=await bus.ExecuteDetailedAsync(new("media_status"));c["media_status_activity_excludes_private_titles"]=ModuleActivity.Get("media") is {} activity&&!activity.Detail.Contains("Fixture one");
        c["media_status_keeps_legacy_array_shape"]=list.Data!.Value.ValueKind==JsonValueKind.Array&&JsonSerializer.Deserialize<MediaSession[]>(list.Message)!.Length==1;
        c["media_control_discovered_after_actual_query"]=catalog.Read().Any(e=>e.Action.Id=="media_pause");
        var play=await bus.ExecuteDetailedAsync(new("media_play","fake:one"));c["media_observed_playback_has_evidence"]=play.Evidence=="state-observed"&&play.Data!.Value.Deserialize<MediaCommandReceipt>()!.Status=="Playing";
        provider.KeepUnknown=true;provider.Rows=[new("fake:one","PotPlayer","Fixture one","","Unknown")];var accepted=await bus.ExecuteDetailedAsync(new("media_pause","fake:one"));c["unknown_playback_only_reports_accepted"]=accepted.Evidence=="accepted"&&accepted.Message.Contains("尚未确认");provider.KeepUnknown=false;
        int beforeControl=provider.Controls;provider.FailVerify=true;var verifyFail=await bus.ExecuteDetailedAsync(new("media_pause","fake:one"));provider.FailVerify=false;
        // This injection fails preflight when an earlier command already exists. Use a fresh service for post-send verification below.
        c["media_preflight_failure_does_not_send_another_command"]=verifyFail.Status=="failed"&&provider.Controls==beforeControl;
        var verifyProvider=new FakeMedia{FailVerify=true};var verifyService=new MediaService([verifyProvider]);var verification=await verifyService.ExecuteVerifiedAsync("play","fake:one",default);c["post_send_verification_failure_never_replays_command"]=verifyProvider.Controls==1&&!verification.Observed&&verification.Message.Contains("已发送");
        var missing=await bus.ExecuteDetailedAsync(new("media_next","fake:gone"));c["missing_explicit_player_does_not_fall_back"]=missing.Status=="failed"&&provider.Controls==beforeControl;
        provider.Hold=new(TaskCreationOptions.RunContinuationsAsynchronously);int count=provider.Controls;var next1=bus.ExecuteDetailedAsync(new("media_next","fake:one","media-ui"));var next2=bus.ExecuteDetailedAsync(new("media_next","fake:one","combo"));await Until(()=>provider.Controls==count+1);provider.Hold.SetResult();var nextResults=await Task.WhenAll(next1,next2);provider.Hold=null;c["duplicate_media_commands_coalesce"]=provider.Controls==count+1&&nextResults[0].ExecutionId==nextResults[1].ExecutionId;
        provider.Hold=new(TaskCreationOptions.RunContinuationsAsynchronously);bus.StructuredTimeoutOverride=TimeSpan.FromMilliseconds(80);var timed=await bus.ExecuteDetailedAsync(new("media_next","fake:one"));c["media_deadline_stops_wait_and_reports_unknown_effect"]=timed.Status=="cancelled"&&timed.ErrorCode=="timeout"&&timed.Evidence=="unknown";provider.Hold.SetResult();provider.Hold=null;bus.StructuredTimeoutOverride=null;await Task.Delay(25);
        var previousSink=OperationFeedback.Sink;var notices=new List<LeoNotice>();OperationFeedback.Sink=notice=>{lock(notices)notices.Add(notice);};
        try{provider.Hold=new(TaskCreationOptions.RunContinuationsAsynchronously);using var cancellation=new CancellationTokenSource();var longWait=bus.ExecuteDetailedAsync(new("media_next","fake:one"),cancellation.Token);await Until(()=>{lock(notices)return notices.Any(x=>x.Busy&&x.Retry!=null);});cancellation.Cancel();await longWait;provider.Hold.SetResult();provider.Hold=null;await Task.Delay(25);lock(notices)c["long_operation_cancel_feedback_is_closed_by_matching_receipt"]=notices.Any(x=>x.Busy&&x.Retry!=null&&notices.Any(y=>!y.Busy&&y.OperationId==x.OperationId));}finally{OperationFeedback.Sink=previousSink;}
        c["media_status_diagnostics_exclude_private_titles"]=bus.RecentResults.Where(x=>x.ActionId=="media_status").All(x=>x.Data==null&&!x.Message.Contains("Fixture one"));
        c["media_file_rejects_missing_target_without_opening"]=(await bus.ExecuteDetailedAsync(new("media_file",Path.Combine(root,"missing.mp4")))).Status=="failed"&&opens==0;
        c["media_open_player_uses_shared_execution"]=(await bus.ExecuteDetailedAsync(new("media_file"))).Evidence=="accepted"&&opens==1;
        c["media_open_window_uses_shared_execution"]=(await bus.ExecuteDetailedAsync(new("media_open"))).Evidence=="window-shown"&&windows==1;
        using(var brain=new BrainSession(catalog,bus.ExecuteDetailedAsync,(_,_)=>Task.FromResult(WorkflowVerification.Unknown),()=>new Dictionary<string,string>())){
            brain.BeginTurn("请暂停播放器");var forbidden=await brain.Tool(Tool("leo_action",new{action="media_pause",value="fake:one"}),default);c["model_cannot_directly_control_media"]=forbidden.Contains("error");
            await brain.Tool(Tool("leo_action",new{action="media_status",value=""}),default);int before=provider.Controls;
            await brain.Tool(Tool("leo_plan",new{steps=new[]{new{action="media_pause",value="fake:one",from_step="",result_property=""}}}),default);
            c["model_media_plan_waits_for_host_confirmation"]=brain.Pending!=null&&brain.Pending.Steps[0].TimeoutMs==45000&&provider.Controls==before;await brain.Confirm(brain.Pending!.Id,(_,_)=>{},default);c["model_media_plan_reuses_same_executor"]=provider.Controls==before+1;
            provider.Rows=[new("fake:one","PotPlayer","One","","Playing"),new("fake:two","Browser","Two","","Playing")];brain.BeginTurn("暂停一下播放器");await brain.Tool(Tool("leo_action",new{action="media_status",value=""}),default);var ambiguous=await brain.Tool(Tool("leo_plan",new{steps=new[]{new{action="media_pause",value="fake:one",from_step="",result_property=""}}}),default);c["multiple_players_require_clarification"]=brain.Pending==null&&ambiguous.Contains("error");
            brain.BeginTurn("暂停 PotPlayer");await brain.Tool(Tool("leo_action",new{action="media_status",value=""}),default);await brain.Tool(Tool("leo_plan",new{steps=new[]{new{action="media_pause",value="fake:one",from_step="",result_property=""}}}),default);c["named_player_resolves_ambiguity"]=brain.Pending!=null;
            config="changed";bool rejected=false;try{await brain.Confirm(brain.Pending!.Id,(_,_)=>{},default);}catch(Exception e)when(e is InvalidOperationException or ArgumentException){rejected=true;}c["changed_configuration_revokes_old_plan"]=rejected;
        }
        bus.Registry.ReplaceBuiltin(Proxy());bus.Registry.ReplaceBuiltin(Media());
        provider.Hold=new(TaskCreationOptions.RunContinuationsAsynchronously);int startCount=provider.Controls;var wait=bus.ExecuteDetailedAsync(new("media_pause","fake:one"));await Until(()=>provider.Controls==startCount+1);config="changed again";bus.Registry.ReplaceBuiltin(Proxy());bus.Registry.ReplaceBuiltin(Media());bus.CancelDisabled();c["configuration_generation_cancels_old_media_wait"]=(await wait).ErrorCode=="configuration_changed";provider.Hold.SetResult();provider.Hold=null;await Task.Delay(25);
        prefs.MediaModule=false;bus.CancelDisabled();c["disabled_media_rejects_all_entries"]=(await bus.ExecuteDetailedAsync(new("media_open"))).ErrorCode=="module_disabled"&&!catalog.Read().Any(e=>e.Module=="media");prefs.MediaModule=true;
        var flow=new ReliableWorkflow(new WorkflowJournal(null),bus.ExecuteDetailedAsync,(_,_)=>Task.FromResult(WorkflowVerification.Unknown));
        var steps=new[]{new WorkflowStep("proxy",new("proxy_on","","combo"),"代理",60000),new WorkflowStep("media",new("media_pause","fake:one","combo"),"媒体")};
        int proxyBefore=sets;provider.FailVerify=true;try{await flow.Run("fixture-watch",steps,(_,_)=>{});}catch(IOException){}provider.FailVerify=false;
        await flow.Run("fixture-watch",steps,(_,_)=>{},confirmUnknown:true,token:default);c["workflow_retry_preserves_completed_proxy_step"]=sets==proxyBefore+1;
        var originalService=MediaControl.Service;MediaControl.Service=service;
        try{
            using(var form=new MediaForm((a,v,t)=>bus.ExecuteAsync(new(a,v,"media-ui"),t))){form.StartPosition=FormStartPosition.Manual;form.Location=new(-19000,-19000);form.Show();await Until(()=>bus.Receipts.Any(x=>x.Source=="media-ui"&&x.Action=="media_status"));Walk(form).OfType<Button>().Single(x=>x.Text=="打开PotPlayer").PerformClick();await Until(()=>opens==2);c["actual_media_window_routes_open_and_query_to_dispatcher"]=opens==2;form.Close();}
            using(var foot=new FootTools(130,false)){foot.ProxyAction=(a,t)=>bus.ExecuteAsync(new(a,"","foot-ui"),t);foot.ConfigureModules(prefs);foot.StartPosition=FormStartPosition.Manual;foot.Location=new(-19000,-19000);foot.Show();await Until(()=>bus.Receipts.Any(x=>x.Source=="foot-ui"&&x.Action=="proxy_status"));c["actual_foot_proxy_status_uses_dispatcher"]=true;int footBefore=sets;Walk(foot).OfType<Button>().Single(x=>x.AccessibleName=="展开更多工具").PerformClick();Walk(foot).OfType<Button>().Single(x=>x.AccessibleName=="Clash Verge 系统代理").PerformClick();await Until(()=>sets==footBefore+1);c["actual_foot_proxy_button_uses_dispatcher"]=bus.Receipts.Any(x=>x.Source=="foot-ui"&&x.Action=="proxy_off");foot.Close();}
        }finally{MediaControl.Service=originalService;}
        using(var pet=new PetForm(new Preferences{ApplicationIndexing=false},true,true)){pet.Show();Application.DoEvents();var dispatcher=(ActionDispatcher)typeof(PetForm).GetField("commands",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pet)!;c["actual_host_registers_migrated_capabilities"]=dispatcher.Registry.Resolve("proxy_on") is ProxyCapability&&dispatcher.Registry.Resolve("media_pause") is MediaCapability;var comboStep=(WorkflowStep)typeof(PetForm).GetMethod("PlanComboStep",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pet,[new ComboStep("command","开启系统代理"),0])!;c["combo_local_command_inherits_proxy_cold_start_deadline"]=comboStep.Request.Action=="proxy_on"&&comboStep.TimeoutMs==60000;var oldProxy=dispatcher.Registry.Resolve("proxy_on");string repaired=Path.Combine(root,"clash-verge.exe");File.WriteAllText(repaired,"fixture, never executed");typeof(PetForm).GetMethod("RememberBuiltinRepair",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pet,["clash",repaired]);c["confirmed_path_repair_refreshes_client_generation"]=!ReferenceEquals(oldProxy,dispatcher.Registry.Resolve("proxy_on"))&&((IStructuredLeoModule)dispatcher.Registry.Resolve("proxy_on")).ConfigurationIsCurrent;pet.Close();}
        c["migrated_requests_never_hit_legacy_core"]=legacy==0;
        return c;
    }
}
