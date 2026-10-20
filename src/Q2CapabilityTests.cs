using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;

static class Q2CapabilityTests
{
    internal static object? EntryReceiptSummary;
    static TaskCompletionSource<bool> Gate()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    static async Task Until(Func<bool> test){var clock=Stopwatch.StartNew();while(!test()&&clock.ElapsedMilliseconds<2500)await Task.Delay(10);if(!test())throw new TimeoutException("Fixture deadline");}
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var window=new Form{Opacity=0,ShowInTaskbar=false};
        window.Shown+=async(_,_)=>{try{var checks=await Check(Path.GetDirectoryName(report)!);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,entries=EntryReceiptSummary,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}finally{window.Close();}};Application.Run(window);
    }
    static async Task<Dictionary<string,bool>> Check(string directory)
    {
        var c=new Dictionary<string,bool>();var app=new LocalApplication(Guid.NewGuid().ToString("N"),"试点应用",Environment.ProcessPath!,true,"","--fixture \"中文 空格\"",directory,true);
        ExtensionStore Empty()=>new(Path.Combine(directory,"extensions-"+Guid.NewGuid().ToString("N")));
        Preferences Prefs()=>new(){ApplicationModules=[app],ApplicationIndexing=false};
        ApplicationCapabilityHost Host(Func<ProcessStartInfo,CancellationToken,Task<bool>> start,Action<LocalApplication>? remember=null,Func<LocalApplication,CancellationToken,Task<LocalApplication>>? prepare=null)=>new(prepare??((a,_)=>Task.FromResult(a)),start,remember);
        ActionRequest Request(string source="search")=>new("local:"+app.Id,"",source);
        {
            var p=Prefs();int calls=0,history=0;var gate=Gate();CancellationToken workToken=default;ProcessStartInfo? captured=null;
            using var bus=new ActionDispatcher(p,(_,_,_)=>throw new InvalidOperationException("Legacy app path used"),Empty(),Host(async(info,t)=>{calls++;captured=info;workToken=t;return await gate.Task.WaitAsync(t);},_=>history++));
            var first=bus.Registry.Resolve("local:"+app.Id);var second=bus.Registry.Resolve("local:"+app.Id);
            c["registry_reuses_unchanged_local_definition"]=ReferenceEquals(first,second);
            c["registry_describes_local_effect_timeout_and_no_retry"]=bus.Registry.Describe("local:"+app.Id) is {Effect:"external",TimeoutMs:15000,Retry:false};
            using var cancel=new CancellationTokenSource();var a=bus.ExecuteDetailedAsync(Request("chat"),cancel.Token);var b=bus.ExecuteDetailedAsync(new("local_app_open",app.Id,"shortcut"));
            c["old_and_canonical_alias_coalesce_before_side_effect"]=calls==1&&bus.Metrics is {Requests:2,Executions:1,Coalesced:1};
            cancel.Cancel();var stopped=await a;c["one_subscriber_cancel_keeps_other_launch_alive"]=stopped.Status=="cancelled"&&!workToken.IsCancellationRequested;
            gate.SetResult(true);var done=await b;c["remaining_subscriber_receives_accepted_not_confirmed"]=done.Status=="succeeded"&&done.Evidence=="accepted"&&done.Data?.GetProperty("accepted").GetBoolean()==true;
            c["coalesced_requests_have_distinct_ids_and_shared_execution"]=done.RequestId!=stopped.RequestId&&done.ExecutionId==stopped.ExecutionId;
            c["source_is_per_request"]=stopped.Source=="chat"&&done.Source=="shortcut";
            c["configured_arguments_directory_admin_are_preserved"]=captured is {UseShellExecute:true,Verb:"runas"}&&captured.FileName==app.Path&&captured.Arguments==app.Arguments&&captured.WorkingDirectory==app.WorkingDirectory;
            c["one_history_entry_for_one_actual_launch"]=history==1;
            c["metrics_separate_requests_work_and_results"]=bus.Metrics is {Requests:2,Executions:1,Succeeded:1,Cancelled:1};
            p.EntryDisplayModes[app.Id]="search";c["entry_visibility_does_not_disable_capability"]=bus.Registry.Resolve("local:"+app.Id).Enabled;
            p.ApplicationModules[0]=app with{Enabled=false};bus.CancelDisabled();var disabled=await bus.ExecuteDetailedAsync(new("local_app_open",app.Id));
            c["disabled_capability_rejects_alias_without_starting"]=disabled.ErrorCode=="module_disabled"&&calls==1&&!bus.Modules.Single(m=>m.Id=="local:"+app.Id).Enabled;
            p.ApplicationModules.Clear();bus.CancelDisabled();var removed=await bus.ExecuteDetailedAsync(Request());c["removed_capability_is_not_resolved"]=removed.ErrorCode=="module_removed"&&!bus.Modules.Any(m=>m.Id=="local:"+app.Id);
        }
        {
            var p=Prefs();int calls=0;using var bus=new ActionDispatcher(p,(_,_,_)=>Task.FromResult("legacy"),Empty(),Host((_,_)=>{calls++;return Task.FromResult(true);}));
            var typed=new StructuredActionRequest("leo-action/1","local_app_open",JsonSerializer.SerializeToElement(new{applicationId=app.Id}),"voice","fixture-request","fixture-trace");var good=await bus.ExecuteStructuredAsync(typed);
            c["typed_request_returns_correlated_structured_data"]=good is {Status:"succeeded",RequestId:"fixture-request",TraceId:"fixture-trace",Source:"voice"}&&good.Data?.GetProperty("applicationId").GetString()==app.Id;
            c["typed_unknown_protocol_rejected"]=(await bus.ExecuteStructuredAsync(typed with{Protocol="leo-action/999"})).ErrorCode=="invalid_request";
            c["typed_extra_launch_parameters_rejected"]=(await bus.ExecuteStructuredAsync(typed with{Parameters=JsonSerializer.SerializeToElement(new{applicationId=app.Id,arguments="override"})})).ErrorCode=="invalid_request";
            c["typed_wrong_parameter_type_rejected"]=(await bus.ExecuteStructuredAsync(typed with{Parameters=JsonSerializer.SerializeToElement(new{applicationId=42})})).ErrorCode=="invalid_request";
            c["typed_fixed_action_identity_must_match"]=(await bus.ExecuteStructuredAsync(typed with{ActionId="local:"+Guid.NewGuid().ToString("N")})).ErrorCode=="invalid_request";
            c["invalid_typed_calls_have_no_side_effect"]=calls==1;
            var future=await bus.ExecuteStructuredAsync(typed with{Deadline=DateTimeOffset.UtcNow.AddYears(2)});c["far_future_deadline_does_not_break_wait_cleanup"]=future.Status=="succeeded"&&calls==2;
            var expired=await bus.ExecuteDetailedAsync(Request() with{Deadline=DateTimeOffset.UtcNow.AddSeconds(-1)});c["expired_request_never_launches"]=expired.ErrorCode=="timeout"&&calls==2;
            c["empty_request_identity_rejected"]=(await bus.ExecuteDetailedAsync(Request() with{RequestId=""})).Status=="failed";
            p.ApplicationModules.Add(app);bool duplicate=false;try{_ =bus.Modules;}catch(InvalidOperationException){duplicate=true;}c["duplicate_registration_rejected"]=duplicate;
        }
        {
            int calls=0;using var bus=new ActionDispatcher(Prefs(),(_,_,_)=>Task.FromResult("legacy"),Empty(),Host((_,_)=>{calls++;throw new IOException("fixture launch failure");}));
            var a=await bus.ExecuteDetailedAsync(Request());c["launch_failure_has_stable_code_and_unknown_evidence"]=a is {Status:"failed",ErrorCode:"launch_failed",Evidence:"unknown",Retryable:false};
            await Task.Delay(30);c["failure_is_not_automatically_retried"]=calls==1;await bus.ExecuteDetailedAsync(Request());c["explicit_retry_starts_one_new_execution"]=calls==2;
        }
        {
            var p=Prefs();var second=app with{Id=Guid.NewGuid().ToString("N"),Name="第二应用",Arguments="--second",Icon=app.Path};p.ApplicationModules.Add(second);var arguments=new List<string>();
            using var bus=new ActionDispatcher(p,(_,_,_)=>throw new InvalidOperationException("Unexpected legacy branch"),Empty(),Host((info,_)=>{arguments.Add(info.Arguments);return Task.FromResult(true);}));
            await bus.ExecuteDetailedAsync(Request());await bus.ExecuteDetailedAsync(new("local_app_open",second.Id));
            c["second_application_registers_without_host_branch"]=bus.Modules.Count(m=>m.Id.StartsWith("local:"))==2&&arguments.SequenceEqual(new[]{app.Arguments,second.Arguments});
            c["registry_preserves_second_application_icon_and_identity"]=bus.Registry.Resolve("local:"+second.Id) is LocalApplicationModule module&&module.Configuration.Icon==second.Icon;
        }
        {
            var p=Prefs();var entered=Gate();var release=Gate();int calls=0,history=0;
            using var bus=new ActionDispatcher(p,(_,_,_)=>Task.FromResult("legacy"),Empty(),Host(async(_,_)=>{calls++;entered.TrySetResult(true);return await release.Task;},_=>history++)){StructuredTimeoutOverride=TimeSpan.FromMilliseconds(60)};
            var operation=bus.ExecuteDetailedAsync(Request());await entered.Task;var timed=await operation.WaitAsync(TimeSpan.FromSeconds(2));
            c["uncooperative_start_times_out_observer_promptly"]=timed is {Status:"cancelled",ErrorCode:"timeout",Evidence:"unknown"};
            var again=await bus.ExecuteDetailedAsync(new("local_app_open",app.Id));c["timeout_keeps_unfinished_native_work_reserved"]=again.Status=="cancelled"&&calls==1;
            release.SetResult(true);await Task.Delay(30);c["late_completion_does_not_record_success_or_history"]=history==0&&bus.Metrics.Succeeded==0;
        }
        {
            var p=Prefs();var gate=Gate();int history=0,calls=0;
            using var bus=new ActionDispatcher(p,(_,_,_)=>Task.FromResult("legacy"),Empty(),Host(async(_,_)=>{calls++;return await gate.Task;},_=>history++));
            var first=bus.ExecuteDetailedAsync(Request());p.ApplicationModules[0]=app with{Enabled=false};bus.CancelDisabled();var result=await first;
            c["disable_cancels_waiting_request"]=result.Status=="cancelled";
            p.ApplicationModules[0]=app;var pending=await bus.ExecuteDetailedAsync(Request());c["reenable_does_not_overlap_unfinished_start"]=pending.Status=="cancelled"&&calls==1;
            gate.SetResult(true);await Task.Delay(30);c["disabled_generation_cannot_publish_late_history"]=history==0;
            var next=await bus.ExecuteDetailedAsync(Request());c["reenabled_module_can_start_after_old_work_finishes"]=next.Status=="succeeded"&&calls==2&&history==1;
        }
        {
            var p=Prefs();var gate=Gate();int starts=0;
            using var bus=new ActionDispatcher(p,(_,_,_)=>Task.FromResult("legacy"),Empty(),Host((_,_)=>{starts++;return Task.FromResult(true);},prepare:async(a,t)=>{await gate.Task.WaitAsync(t);return a;}));
            var operation=bus.ExecuteDetailedAsync(Request());p.ApplicationModules[0]=app with{Arguments="changed"};gate.SetResult(true);var result=await operation;
            c["changed_configuration_is_rechecked_before_start"]=result is {Status:"cancelled",ErrorCode:"configuration_changed"}&&starts==0;
        }
        {
            var p=Prefs();var gate=Gate();CancellationToken work=default;string repairedPath=Path.Combine(directory,"repaired.exe");File.WriteAllText(repairedPath,"fake-not-executed");
            var repaired=app with{Path=repairedPath};ProcessStartInfo? captured=null;
            using var bus=new ActionDispatcher(p,(_,_,_)=>Task.FromResult("legacy"),Empty(),Host(async(info,t)=>{captured=info;work=t;return await gate.Task;},prepare:(a,_)=>{p.ApplicationModules[0]=repaired;return Task.FromResult(repaired);}));
            var operation=bus.ExecuteDetailedAsync(Request());bus.CancelDisabled();c["confirmed_path_repair_updates_running_generation"]=!work.IsCancellationRequested&&captured?.FileName==repairedPath&&captured.Arguments==app.Arguments;gate.SetResult(true);c["repaired_application_can_finish_after_registry_refresh"]=(await operation).Status=="succeeded";
        }
        {
            var p=Prefs();CancellationToken work=default;using var one=new CancellationTokenSource();using var two=new CancellationTokenSource();
            using var bus=new ActionDispatcher(p,(_,_,_)=>Task.FromResult("legacy"),Empty(),Host(async(_,t)=>{work=t;await Task.Delay(Timeout.Infinite,t);return true;}));
            var a=bus.ExecuteDetailedAsync(Request(),one.Token);var b=bus.ExecuteDetailedAsync(Request(),two.Token);one.Cancel();await a;two.Cancel();await b;
            c["all_subscribers_cancel_the_shared_work"]=work.IsCancellationRequested;
        }
        {
            using var bus=new ActionDispatcher(Prefs(),(_,_,_)=>Task.FromResult("legacy"),Empty(),Host((_,_)=>Task.FromResult(true),_=>throw new IOException("fixture history failure")));
            c["history_failure_does_not_turn_accepted_launch_into_retry"]=(await bus.ExecuteDetailedAsync(Request())).Status=="succeeded"&&bus.Metrics.Executions==1;
        }
        {
            var gate=Gate();int calls=0;var p=Prefs();using var bus=new ActionDispatcher(p,(_,_,_)=>Task.FromResult("legacy"),Empty(),Host(async(_,t)=>{calls++;return await gate.Task.WaitAsync(t);}));
            var shortWait=bus.ExecuteDetailedAsync(Request() with{Deadline=DateTimeOffset.UtcNow.AddMilliseconds(50)});var longWait=bus.ExecuteDetailedAsync(Request("voice"));
            c["per_request_deadline_does_not_cancel_other_subscriber"]=(await shortWait).ErrorCode=="timeout";gate.SetResult(true);c["other_subscriber_completes_after_first_deadline"]=(await longWait).Status=="succeeded"&&calls==1;
        }
        {
            var gate=Gate();var p=Prefs();int history=0;var bus=new ActionDispatcher(p,(_,_,_)=>Task.FromResult("legacy"),Empty(),Host(async(_,_)=>await gate.Task,_=>history++));
            var operation=bus.ExecuteDetailedAsync(Request());bus.Dispose();bus.Dispose();c["shutdown_cancels_inflight_wait"]=(await operation).Status=="cancelled";gate.SetResult(true);await Task.Delay(30);c["shutdown_discards_late_completion"]=history==0;
            c["shutdown_rejects_new_requests"]=(await bus.ExecuteDetailedAsync(Request())).ErrorCode=="dispatcher_closed";
        }
        {
            int legacy=0;using var bus=new ActionDispatcher(Prefs(),(_,_,_)=>{legacy++;return Task.FromResult("legacy response");},Empty());var result=await bus.ExecuteDetailedAsync(new("todo_list","","combo"));c["unmigrated_action_keeps_legacy_adapter"]=result is {Status:"succeeded",Evidence:"unknown",Message:"legacy response"}&&legacy==1;
            var ext=new ExtensionManifest("LeoModule/1","fixture-extension","Fixture","1.0.0",[new("open","打开试点",[],"local_app_open",app.Id)]);bus.Extensions.Install(ext);c["legacy_extension_still_dispatches"]=(await bus.ExecuteAsync(new("ext:fixture-extension:open")))=="legacy response"&&legacy==2;
            c["all_old_action_ids_have_one_registered_owner"]=LeoActions.Names.All(a=>bus.Modules.Count(m=>m.Actions.Contains(a))==1);
            bus.Extensions.Install(ext with{Version="1.1.0"});c["registry_observes_legacy_extension_update"]=bus.Modules.Single(m=>m.Id=="ext:fixture-extension").Version=="1.1.0";
            bus.Extensions.Remove(ext.Id);c["registry_observes_legacy_extension_removal"]=!bus.Modules.Any(m=>m.Id=="ext:fixture-extension");
        }
        {
            ActionDispatcher? recursive=null;using(recursive=new ActionDispatcher(new(),(a,v,t)=>recursive!.ExecuteAsync(new(a,v,"nested"),t),Empty())){var result=await recursive.ExecuteDetailedAsync(new("todos_open"));c["legacy_recursive_calls_fail_without_deadlock"]=result.Status=="failed"&&result.Failure is InvalidOperationException;}
            var p=new Preferences();int starts=0;var gate=Gate();using var bus=new ActionDispatcher(p,async(_,_,t)=>{starts++;await gate.Task.WaitAsync(t);return "legacy";},Empty());
            var a=bus.ExecuteDetailedAsync(new("proxy_on","","chat"));var b=bus.ExecuteDetailedAsync(new("proxy_on","","voice"));c["legacy_matching_requests_still_coalesce"]=starts==1;p.ClashModule=false;bus.CancelDisabled();c["legacy_disable_still_cancels_all_subscribers"]=(await a).Status=="cancelled"&&(await b).Status=="cancelled";
        }
        {
            int calls=0,history=0;var p=Prefs();using var pet=new PetForm(p,true,applicationOverride:Host((_,_)=>{calls++;return Task.FromResult(true);},_=>history++));pet.Show();Application.DoEvents();
            await pet.VerifyQ2ApplicationEntries(app,c);c["four_real_entries_produce_four_fake_starts"]=calls==4&&history==4;pet.Close();
        }
        {
            int history=0;CancellationToken work=default;using var pet=new PetForm(Prefs(),true,applicationOverride:Host(async(_,t)=>{work=t;await Task.Delay(Timeout.Infinite,t);return true;},_=>history++));pet.Show();
            c["closing_chat_cancels_its_application_subscription"]=await pet.VerifyQ2ChatClose(app)&&work.IsCancellationRequested&&history==0;pet.Close();
        }
        return c;
    }
}

sealed partial class PetForm
{
    internal async Task<bool> VerifyQ2ChatClose(LocalApplication app){ShowChat();var task=chat!.PreviewSendAsync("打开"+app.Name);chat.Dispose();await task.WaitAsync(TimeSpan.FromSeconds(2));await Task.Delay(20);return commands.RecentResults.Last().Status=="cancelled";}
    internal async Task VerifyQ2ApplicationEntries(LocalApplication app,Dictionary<string,bool> checks)
    {
        launcher.Open();var button=Walk(launcher).OfType<Button>().Single(b=>b.AccessibleName==app.Name);button.PerformClick();await Task.Delay(40);
        checks["actual_shortcut_preserves_source"]=commands.RecentResults.Last() is {Source:"shortcut",Status:"succeeded"} r&&r.ActionId=="local:"+app.Id;
        launcher.Open();launcher.Q2LaunchSearchResult(app.Name);await Task.Delay(40);
        checks["actual_search_normalizes_before_execution"]=commands.RecentResults.Last() is {Source:"search",Status:"succeeded"} s&&s.ActionId=="local:"+app.Id;
        ShowChat();await chat!.PreviewSendAsync("打开"+app.Name);
        checks["actual_chat_uses_same_application_capability"]=commands.RecentResults.Last() is {Source:"chat",Status:"succeeded"} h&&h.ActionId=="local:"+app.Id;
        await chat.Q2ReceiveDirectVoice("打开"+app.Name);
        checks["actual_voice_uses_same_application_capability"]=commands.RecentResults.Last() is {Source:"voice",Status:"succeeded"} v&&v.ActionId=="local:"+app.Id;
        // Conversation lifecycle actions also have receipts; assert the four application requests only.
        var appReceipts=commands.RecentResults.Where(r=>r.ActionId=="local:"+app.Id).ToArray();
        Q2CapabilityTests.EntryReceiptSummary=new{all_request_ids=commands.RecentResults.Select(r=>r.RequestId).Distinct().Count(),application_receipts=appReceipts.Length,application_request_ids=appReceipts.Select(r=>r.RequestId).Distinct().Count()};
        checks["four_entries_use_unique_request_ids"]=appReceipts.Length==4&&appReceipts.Select(r=>r.RequestId).Distinct().Count()==4&&appReceipts.Select(r=>r.Source).ToHashSet().SetEquals(["shortcut","search","chat","voice"]);
        chat.Close();
        static IEnumerable<Control> Walk(Control control){yield return control;foreach(Control child in control.Controls)foreach(var item in Walk(child))yield return item;}
    }
}
sealed partial class LauncherForm
{
    internal void Q2LaunchSearchResult(string name){query.Text=name;debounce.Stop();Filter();selection=0;LaunchSelected();}
}
sealed partial class ChatForm
{
    internal Task Q2ReceiveDirectVoice(string text){directVoice=true;return ReceiveVoiceAsync(text,true);}
}
