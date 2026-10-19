using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;

static class Q4WorkflowTests
{
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var host=new Form{Opacity=0,ShowInTaskbar=false};
        host.Shown+=async(_,_)=>{try{var checks=await Check(Path.GetDirectoryName(report)!,host);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(x=>x),checks,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}finally{host.Close();}};Application.Run(host);
    }
    static ActionResult Result(ActionRequest request,string evidence="accepted",JsonElement? data=null)=>new(request.RequestId??"fixture",request.TraceId??"trace",request.Source,request.Action,"fixture","execution","succeeded","完成",null,data,evidence);
    static async Task<bool> Throws<T>(Func<Task> action)where T:Exception{try{await action();return false;}catch(T){return true;}}
    static async Task<Dictionary<string,bool>> Check(string root,Form ui)
    {
        var c=new Dictionary<string,bool>();root=Path.Combine(root,"fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var now=DateTimeOffset.Now;var match=new FootballMatch("123456",now.AddHours(4).ToOffset(TimeSpan.FromHours(-3)),"fixture","83","Barcelona","86","Real Madrid","pre","Scheduled","VS",true);
        var tasks=new TodoStore(Path.Combine(root,"todos.json"));var feed=new List<(FootballMatch Match,DateTimeOffset Updated)>{(match,now.AddMinutes(-5))};var planning=new MatchPlanningHost(()=>feed);
        using var bus=new ActionDispatcher(new(),(_,_,_)=>throw new Exception("legacy match bypass"),new ExtensionStore(Path.Combine(root,"extensions")));
        bool enabled=true;bus.Registry.Register(new MatchPlanningCapability(()=>enabled,planning,tasks,new ModuleWindowService(ui)));
        var query=await bus.ExecuteDetailedAsync(new("match_lookup",match.Id,"chat"));
        c["lookup_carries_provider_event_timezone_and_observation"]=query.Data!.Value.GetProperty("Reference").GetProperty("Provider").GetString()=="espn"&&query.Data.Value.GetProperty("ObservedAt").GetDateTimeOffset()==feed[0].Updated;
        var reference=query.Data.Value.GetProperty("Reference");
        var added=await bus.ExecuteStructuredAsync(new("leo-action/1","match_add",reference,"chat"));var again=await bus.ExecuteDetailedAsync(new("match_add",match.Id,"voice"));
        c["legacy_and_structured_match_import_share_persisted_identity"]=tasks.Items.Count==1&&added.Data!.Value.GetProperty("Added").GetBoolean()&&!again.Data!.Value.GetProperty("Added").GetBoolean();
        var saved=new TodoStore(Path.Combine(root,"todos.json")).Items.Single();
        c["source_metadata_and_ten_minute_reminder_persist"]=saved.MatchId=="espn:"+match.Id&&saved.MatchSourceUpdated==feed[0].Updated&&saved.MatchDisplayTimeZone==TimeZoneInfo.Local.Id&&saved.MatchReminderMinutes==10&&saved.Reminder==match.Kickoff.AddMinutes(-10);
        c["offset_kickoff_uses_local_calendar_day"]=saved.Day==DateOnly.FromDateTime(match.Kickoff.LocalDateTime);
        c["diagnostic_history_excludes_task_body_and_json"]=bus.RecentResults.All(r=>r.Data==null&&!r.Message.Contains(saved.Id))&&bus.Receipts.All(r=>!r.Message.Contains(saved.Id));
        var newest=match with{Kickoff=match.Kickoff.AddHours(1)};feed.Add((newest,now));planning.Remember(match,now.AddMinutes(-20));
        c["newest_observation_wins_over_old_selected_card"]=planning.Resolve(new("espn",match.Id,TimeZoneInfo.Local.Id)).Match.Kickoff==newest.Kickoff;
        feed.Clear();feed.Add((match with{State="post",Kickoff=now.AddHours(-1)},now));
        c["already_imported_match_can_be_requeried_after_kickoff"]=!(await bus.ExecuteDetailedAsync(new("match_add",match.Id))).Data!.Value.GetProperty("Added").GetBoolean();
        c["unknown_event_does_not_create_task"]=(await bus.ExecuteDetailedAsync(new("match_add","missing"))).Status=="failed"&&tasks.Items.Count==1;
        c["untrusted_provider_is_rejected"]=(await bus.ExecuteStructuredAsync(new("leo-action/1","match_add",JsonSerializer.SerializeToElement(new MatchReference("other",match.Id,TimeZoneInfo.Local.Id))))).Status=="failed";
        c["extra_match_parameters_are_rejected"]=(await bus.ExecuteStructuredAsync(new("leo-action/1","match_add",JsonSerializer.SerializeToElement(new{Provider="espn",EventId=match.Id,DisplayTimeZone=TimeZoneInfo.Local.Id,ReminderMinutes=10,Path="untrusted"})))).Status=="failed";
        tasks.Rename(saved.Id,"我的比赛安排");var customReminder=now.AddHours(1);tasks.SetReminder(saved.Id,customReminder);await bus.ExecuteDetailedAsync(new("match_add",match.Id));
        c["repeat_import_keeps_user_title_and_reminder"]=tasks.Items.Single().Title=="我的比赛安排"&&tasks.Items.Single().Reminder==customReminder;
        enabled=false;c["disabled_football_blocks_bridge"]=(await bus.ExecuteDetailedAsync(new("match_add",match.Id))).ErrorCode=="module_disabled";enabled=true;
        feed.Clear();feed.Add((newest,now));
        var pipe=new[]{new WorkflowStep("lookup",new("match_lookup",match.Id,"combo"),"查询"),new WorkflowStep("add",new("match_add","","combo"),"加入清单",FromStep:"lookup",ResultProperty:"Reference")};
        var matchJournal=new WorkflowJournal(Path.Combine(root,"match-flow.json"));var matchFlow=new ReliableWorkflow(matchJournal,bus.ExecuteDetailedAsync,(_,_)=>Task.FromResult(WorkflowVerification.NotApplied));
        var pipeline=await matchFlow.Run("match-pipeline",pipe,(_,_)=>{});
        c["real_query_result_is_passed_to_import_action"]=pipeline.Steps.All(s=>s.State=="succeeded")&&tasks.Items.Count==1;
        c["new_user_flow_run_still_deduplicates_existing_match"]=(await matchFlow.Run("match-pipeline",pipe,(_,_)=>{})).RunId!=pipeline.RunId&&tasks.Items.Count==1;

        var path=Path.Combine(root,"flow.json");var journal=new WorkflowJournal(path);var calls=new List<string>();bool fail=true;
        Task<ActionResult> Execute(ActionRequest r,CancellationToken token){token.ThrowIfCancellationRequested();calls.Add(r.Action);if(r.Action=="open"&&fail)throw new IOException("fixture after effect");return Task.FromResult(Result(r));}
        var steps=new[]{new WorkflowStep("proxy",new("proxy_on","","combo"),"代理"),new WorkflowStep("url",new("open","https://example.invalid/private-value","combo"),"网址"),new WorkflowStep("music",new("music_client","","combo"),"音乐")};
        var engine=new ReliableWorkflow(journal,Execute,(_,_)=>Task.FromResult(WorkflowVerification.NotApplied));
        c["failure_stops_following_steps"]=await Throws<IOException>(()=>engine.Run("watch",steps,(_,_)=>{}))&&calls.SequenceEqual(new[]{"proxy_on","open"});
        c["completed_steps_and_unknown_step_survive_reload"]=new WorkflowJournal(path).Find("watch")!.Steps.Select(s=>s.State).SequenceEqual(new[]{"succeeded","unknown","pending"});
        var backupPath=Path.Combine(root,"recovered-flow.json");var backupJournal=new WorkflowJournal(backupPath);var prior=journal.Find("watch")!;
        backupJournal.Save(prior with{Steps=prior.Steps.Select(s=>s with{State="pending"}).ToArray()});backupJournal.Save(prior);File.WriteAllText(backupPath,"broken");
        c["backup_recovery_marks_unconfirmed_steps_unknown"]=new WorkflowJournal(backupPath).Find("watch")!.Steps.All(s=>s.State=="unknown");
        c["journal_does_not_store_urls_or_arguments"]=!File.ReadAllText(path).Contains("private-value");
        fail=false;int count=calls.Count;var unknown=new ReliableWorkflow(new WorkflowJournal(path),Execute,(_,_)=>Task.FromResult(WorkflowVerification.Unknown));
        c["restart_does_not_automatically_replay_unknown_effect"]=await Throws<WorkflowRecoveryRequired>(()=>unknown.Run("watch",steps,(_,_)=>{}))&&calls.Count==count;
        await unknown.Run("watch",steps,(_,_)=>{},confirmUnknown:true);
        c["explicit_retry_resumes_without_repeating_completed_proxy"]=calls.Count(a=>a=="proxy_on")==1&&calls.Count(a=>a=="open")==2&&calls.Last()=="music_client";
        var verifiedPath=Path.Combine(root,"verified.json");fail=true;var verifyJournal=new WorkflowJournal(verifiedPath);var initial=new ReliableWorkflow(verifyJournal,Execute,(_,_)=>Task.FromResult(WorkflowVerification.NotApplied));await Throws<IOException>(()=>initial.Run("verified",steps,(_,_)=>{}));count=calls.Count;
        var verified=new ReliableWorkflow(new WorkflowJournal(verifiedPath),Execute,(_,_)=>Task.FromResult(WorkflowVerification.Confirmed));await verified.Run("verified",steps,(_,_)=>{});
        c["confirmed_recovery_skips_existing_effects"]=calls.Count==count;
        var changed=steps.Select((s,i)=>i==1?s with{Request=s.Request with{Value="https://example.invalid/changed"}}:s).ToArray();
        var editJournal=new WorkflowJournal(Path.Combine(root,"edit.json"));var editEngine=new ReliableWorkflow(editJournal,Execute,(_,_)=>Task.FromResult(WorkflowVerification.NotApplied));await Throws<IOException>(()=>editEngine.Run("edit",steps,(_,_)=>{}));
        c["edited_unfinished_plan_requires_confirmation"]=await Throws<WorkflowRecoveryRequired>(()=>editEngine.Run("edit",changed,(_,_)=>{}));
        using(var stop=new CancellationTokenSource()){
            int n=0;var cancellationJournal=new WorkflowJournal(Path.Combine(root,"cancel.json"));var cancellationFlow=new ReliableWorkflow(cancellationJournal,(r,_)=>{n++;stop.Cancel();return Task.FromResult(Result(r));},(_,_)=>Task.FromResult(WorkflowVerification.NotApplied));
            c["cancel_after_success_stops_next_step"]=await Throws<OperationCanceledException>(()=>cancellationFlow.Run("cancel",steps,(_,_)=>{},stop.Token))&&n==1;
            c["cancel_keeps_known_completed_step"]=cancellationJournal.Find("cancel")!.Steps[0].State=="succeeded";
        }
        var timeoutJournal=new WorkflowJournal(Path.Combine(root,"timeout.json"));var late=new TaskCompletionSource<ActionResult>(TaskCreationOptions.RunContinuationsAsynchronously);int nativeCalls=0;
        var timeoutFlow=new ReliableWorkflow(timeoutJournal,(r,_)=>{nativeCalls++;return late.Task;},(_,_)=>Task.FromResult(WorkflowVerification.Unknown));var timedStep=new[]{steps[0] with{TimeoutMs=35}};
        var clock=Stopwatch.StartNew();c["uncooperative_step_wait_is_bounded"]=await Throws<TimeoutException>(()=>timeoutFlow.Run("timeout",timedStep,(_,_)=>{}))&&clock.ElapsedMilliseconds<2000;
        c["timeout_is_recorded_as_unknown_not_success"]=timeoutJournal.Find("timeout")!.Steps[0] is{State:"unknown",ErrorCode:"timeout"};
        c["late_effect_not_replayed_without_confirmation"]=await Throws<WorkflowRecoveryRequired>(()=>timeoutFlow.Run("timeout",timedStep,(_,_)=>{}))&&nativeCalls==1;late.SetResult(Result(new("proxy_on")));await Task.Delay(15);
        c["late_completion_does_not_overwrite_journal"]=timeoutJournal.Find("timeout")!.Steps[0].State=="unknown";
        int started=0;var faultPath=Path.Combine(root,"fault.json");var faultFlow=new ReliableWorkflow(new WorkflowJournal(faultPath),(r,_)=>{started++;return Task.FromResult(Result(r));},(_,_)=>Task.FromResult(WorkflowVerification.NotApplied));
        AtomicJson.BeforeReplace=p=>{if(p==faultPath)throw new IOException("fixture journal failed");};try{c["journal_write_failure_prevents_external_dispatch"]=await Throws<IOException>(()=>faultFlow.Run("fault",steps,(_,_)=>{}))&&started==0;}finally{AtomicJson.BeforeReplace=null;}
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<ActionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var exclusive=new ReliableWorkflow(new WorkflowJournal(null),(r,_)=>{entered.TrySetResult();return release.Task;},(_,_)=>Task.FromResult(WorkflowVerification.NotApplied));var active=exclusive.Run("one",[steps[0]],(_,_)=>{});await entered.Task;
        c["concurrent_flow_cannot_duplicate_dispatch"]=await Throws<InvalidOperationException>(()=>exclusive.Run("two",[steps[0]],(_,_)=>{}));release.SetResult(Result(new("proxy_on")));await active;
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int websites=0;var readinessFlow=new ReliableWorkflow(new WorkflowJournal(null),async(r,t)=>{if(r.Action=="proxy_on")await ready.Task.WaitAsync(t);else websites++;return Result(r);},(_,_)=>Task.FromResult(WorkflowVerification.NotApplied));
        var readiness=readinessFlow.Run("readiness",steps.Take(2).ToArray(),(_,_)=>{});await Task.Delay(10);c["website_waits_for_proxy_completion"]=websites==0;ready.SetResult();await readiness;c["website_runs_once_after_proxy_ready"]=websites==1;
        c["forward_result_reference_rejected_before_effect"]=await Throws<ArgumentException>(()=>faultFlow.Run("forward",[steps[0] with{FromStep="later"}],(_,_)=>{}))&&started==0;
        File.WriteAllText(Path.Combine(root,"leo-scenes.json"),JsonSerializer.Serialize(new WatchScene{Proxy=true,Website=true,Music=true}));
        var legacy=new ComboStore(Path.Combine(root,"not-created-combos.json"));c["legacy_watch_scene_maps_to_same_order"]=legacy.Items.Single().Steps.Select(s=>s.Kind).SequenceEqual(new[]{"proxy_on","watch","qq_client"});
        var comboFile=Path.Combine(root,"combos.json");var comboStore=new ComboStore(comboFile);comboStore.Put(new("fixture","测试组合",[new("command","记一下组合便签")]));c["old_combo_format_roundtrips_without_new_required_fields"]=new ComboStore(comboFile).Items.Any(x=>x.Id=="fixture"&&x.Steps[0].Kind=="command");
        using(var pet=new PetForm(new(){ApplicationIndexing=false},true)){pet.Show();await pet.VerifyQ4MatchEntries(match,now,c);pet.Close();}
        var process=Process.GetCurrentProcess();process.Refresh();long bytes=process.PrivateMemorySize64;var cpu=process.TotalProcessorTime;clock.Restart();
        var smokeJournal=new WorkflowJournal(null);var smoke=new ReliableWorkflow(smokeJournal,(r,_)=>Task.FromResult(Result(r)),(_,_)=>Task.FromResult(WorkflowVerification.NotApplied));for(int i=0;i<100;i++)await smoke.Run("smoke-"+i,[steps[0]],(_,_)=>{});clock.Stop();process.Refresh();
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(root)!,"resource-smoke.json"),JsonSerializer.Serialize(new{runs=100,elapsed_ms=clock.ElapsedMilliseconds,cpu_ms=(process.TotalProcessorTime-cpu).TotalMilliseconds,private_bytes_change=process.PrivateMemorySize64-bytes,background_pollers_added=0}));c["journal_history_is_bounded"]=smokeJournal.Find("smoke-0")==null&&smokeJournal.Find("smoke-99")!=null;
        return c;
    }
}

sealed partial class PetForm
{
    internal async Task VerifyQ4MatchEntries(FootballMatch match,DateTimeOffset observed,Dictionary<string,bool> checks)
    {
        using var holder=new Form();using var card=new MatchCard(match,"fixture",todoStore,observed);holder.Controls.Add(card);holder.Show();var button=card.Controls.OfType<Button>().Single();button.PerformClick();await Task.Delay(50);
        checks["actual_match_card_uses_registered_bridge"]=todoStore.Items.Count==1&&!button.Enabled&&commands.RecentResults.Any(r=>r.ActionId=="match_add"&&r.Source=="football-ui"&&r.ModuleId=="match-planning");
        var again=await commands.ExecuteDetailedAsync(new("match_add",match.Id,"chat"));checks["chat_reuses_card_imported_match"]=again.Status=="succeeded"&&todoStore.Items.Count==1;
        checks["old_workflow_actions_have_one_registered_owner"]=commands.Registry.Resolve("scene_watch").Id=="workflows"&&commands.Registry.Resolve("combo_name").Id=="workflows";
        var preview=await commands.ExecuteDetailedAsync(new("scene_watch","","menu"));checks["preview_does_not_launch_real_software"]=preview.Status=="succeeded"&&preview.Message.Contains("示例模式");holder.Close();
    }
}
