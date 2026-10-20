using System.Text.Json;
namespace LeoLauncher;
static class TodoCapabilityTests
{
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};
        ui.Shown+=async(_,_)=>{try{var checks=await Check(Path.GetDirectoryName(report)!,ui);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(x=>x),checks,real_api_requests=0,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}finally{AtomicJson.BeforeReplace=null;ui.Close();}};Application.Run(ui);
    }
    static ToolCall Tool(string name,object value)=>new(Guid.NewGuid().ToString("N"),name,JsonSerializer.Serialize(value));
    static object Step(string action,string value)=>new{action,value,from_step="",result_property=""};
    static async Task<Dictionary<string,bool>> Check(string root,Form ui)
    {
        var c=new Dictionary<string,bool>();root=Path.Combine(root,"fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string file=Path.Combine(root,"todos.json");var store=new TodoStore(file);int windows=0;var windowHost=new ModuleWindowService(ui);
        using var bus=new ActionDispatcher(new(),(_,_,_)=>throw new Exception("legacy todo bypass"),new ExtensionStore(Path.Combine(root,"modules")));
        var module=new TodoCapability(store,windowHost.ShowAsync,_=>windows++,draft=>draft);bus.Registry.Register(module);var client=new TodoClient(store,bus);store.SharedClient=client;
        async Task<ActionResult> Run(string action,string value="",string? id=null,CancellationToken token=default)=>await bus.ExecuteDetailedAsync(new(action,value,"fixture",id),token);
        async Task<ActionResult> Json(string action,object value,string? id=null)=>await Run(action,JsonSerializer.Serialize(value),id);
        var today=DateOnly.FromDateTime(DateTime.Today);var now=DateTimeOffset.Now;var create=new{title="私密事项甲",day=today,reminder=now.AddHours(3)};
        var added=await Json("todo_add",create,"create-a");string id=added.Data!.Value.GetProperty("task").GetProperty("Id").GetString()!;
        c["creation_is_registered_private_and_committed"]=added.Status=="succeeded"&&added.ModuleId=="todos"&&added.Evidence=="committed"&&store.Items.Count==1;
        var replay=await Json("todo_add",create,"create-a");c["same_request_replays_without_second_task"]=replay.Data!.Value.GetRawText()==added.Data.Value.GetRawText()&&store.Items.Count==1;
        c["same_identity_with_different_content_is_rejected"]=(await Json("todo_add",new{title="另一事项",day=today},"create-a")).Status=="failed";
        var restarted=new TodoStore(file);var restartClient=restarted.Client;var again=await restartClient.Dispatcher.ExecuteDetailedAsync(new("todo_add",JsonSerializer.Serialize(create),"fixture","create-a"));c["create_identity_survives_restart"]=again.Status=="succeeded"&&restarted.Items.Count==1&&restarted.Items.Single().Id==id;
        var structured=await bus.ExecuteStructuredAsync(new("leo-action/1","todo_edit",JsonSerializer.SerializeToElement(new{id,title="更新后的私密标题"}),"fixture"));c["structured_and_legacy_use_same_service"]=structured.Status=="succeeded"&&store.Items.Single().Title=="更新后的私密标题";
        c["extra_fields_are_rejected"]=(await Json("todo_move",new{id,day=today,unexpected=true})).Status=="failed";
        c["duplicate_fields_are_rejected"]=(await Run("todo_edit","{\"id\":\""+id+"\",\"title\":\"甲\",\"title\":\"乙\"}")).Status=="failed";
        c["unknown_task_id_is_rejected"]=(await Run("todo_complete","invented")).Status=="failed";
        c["invalid_date_is_rejected"]=(await Json("todo_move",new{id,day="2026-02-30"})).Status=="failed";
        c["reminder_without_timezone_is_rejected"]=(await Json("todo_remind",new{id,time=DateTime.Now.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss")})).Status=="failed";
        c["past_reminder_is_rejected"]=(await Json("todo_remind",new{id,time=now.AddMinutes(-1).ToString("O")})).Status=="failed";
        await Json("todo_move",new{id,day=today.AddDays(1)});c["move_shifts_reminder_once"]=store.Items.Single().Day==today.AddDays(1)&&store.Items.Single().Reminder==now.AddHours(3).AddDays(1);
        await Run("todo_complete",id);var completed=store.Items.Single().Completed;await Run("todo_complete",id);c["completion_is_idempotent"]=completed!=null&&store.Items.Single().Completed==completed;
        client.SetCompleted(id,false);c["editor_can_restore_incomplete"]=store.Items.Single().Completed==null;
        client.SetImportant(id,true);c["important_flag_is_preserved"]=store.Items.Single().Important;
        await Json("todo_snooze",new{id,minutes=10},"snooze-a");var snoozed=store.Items.Single().Reminder;await Json("todo_snooze",new{id,minutes=10},"snooze-a");c["snooze_retry_does_not_extend_again"]=snoozed==store.Items.Single().Reminder&&store.Items.Single().SnoozedUntil==snoozed;
        c["invalid_snooze_is_rejected"]=(await Json("todo_snooze",new{id,minutes=11})).Status=="failed";
        var recurring=client.AddRepeating("每日习惯",today,"daily",now.AddHours(5));int count=store.Items.Count;client.EnsureThrough(today.AddDays(35));c["generation_deduplicates_instances"]=store.Items.Count==count;
        client.SkipOccurrence(recurring.Id);client.EnsureThrough(today.AddDays(36));c["skipped_occurrence_stays_skipped"]=store.Items.All(t=>t.Id!=recurring.Id);
        var next=store.Items.First(t=>t.SeriesId==recurring.SeriesId);client.Rename(next.Id,"后续标题",true);c["future_title_updates_share_service"]=store.Items.Where(t=>t.SeriesId==recurring.SeriesId&&t.Day>=next.Day).All(t=>t.Title=="后续标题");
        client.SetReminder(next.Id,now.AddDays(1).AddHours(1),true);client.StopRepeating(recurring.SeriesId!,next.Day);c["series_stop_preserves_current_and_history"]=store.RuleFor(recurring.SeriesId)?.Active==false&&store.Items.Any(t=>t.Id==next.Id)&&store.Items.All(t=>t.SeriesId!=recurring.SeriesId||t.Day<=next.Day);
        int before=store.Items.Count;var note=client.Add("便签内容",today,origin:"note-fixture");var noteAgain=new TodoStore(file).Client.Add("便签内容",today,origin:"note-fixture");c["note_conversion_has_durable_origin_identity"]=note.Id==noteAgain.Id&&new TodoStore(file).Items.Count==before+1;
        var match=new FootballMatch("123456",now.AddHours(6),"fixture","83","Barcelona","86","Real Madrid","pre","Scheduled","VS",true);var envelope=new MatchEnvelope(new("espn",match.Id,TimeZoneInfo.Local.Id),match,now,"https://www.espn.com/");var plan=client.AddMatch(envelope);var duplicate=client.AddMatch(envelope);
        c["match_import_reuses_data_service_and_identity"]=plan.Added&&!duplicate.Added&&plan.Task.Id==duplicate.Task.Id&&plan.Task.Reminder==match.Kickoff.AddMinutes(-10);
        client.SyncMatches([match with{Kickoff=match.Kickoff.AddHours(1)}],now.AddMinutes(1));c["match_reschedule_updates_same_task_and_reminder"]=store.Items.Single(t=>t.Id==plan.Task.Id).Reminder==match.Kickoff.AddHours(1).AddMinutes(-10);
        var due=store.Add("已到时间",today,now.AddMinutes(-1));var ticket=new ReminderTicket(due.Id,due.Reminder,due.SnoozedUntil);client.Snooze(due.Id,10,now);await Json("todo_ack",new[]{ticket});c["stale_ack_cannot_consume_rescheduled_reminder"]=store.Items.Single(t=>t.Id==due.Id).Notified==null;
        var due2=store.Add("另一个提醒",today,now.AddMinutes(-1));var dueRows=client.Due(DateTimeOffset.Now);client.Acknowledge(dueRows);c["matching_reminder_ack_is_once"]=store.Items.Single(t=>t.Id==due2.Id).Notified!=null&&!client.Due(DateTimeOffset.Now).Any(t=>t.Id==due2.Id);
        client.SaveQuiet(new(false,22*60,7*60));c["quiet_settings_use_local_atomic_storage"]=client.Quiet()==new QuietHours(false,22*60,7*60)&&File.Exists(Path.Combine(root,"leo-reminder-settings.json"));
        c["invalid_quiet_hours_rejected"]=(await Json("todo_quiet_save",new QuietHours(true,60,60))).Status=="failed";
        string previousFile=File.ReadAllText(file);int previousCount=store.Items.Count;bool previousUndo=store.CanUndo;AtomicJson.BeforeReplace=path=>{if(path==file)throw new IOException("injected-save-failure");};
        var failed=await Json("todo_add",new{title="不能丢旧数据",day=today},"write-fail");AtomicJson.BeforeReplace=null;c["failed_write_preserves_disk_memory_and_undo"]=failed.ErrorCode=="storage_failed"&&File.ReadAllText(file)==previousFile&&store.Items.Count==previousCount&&store.CanUndo==previousUndo;
        var retry=await Json("todo_add",new{title="不能丢旧数据",day=today},"write-fail");c["failed_write_can_retry_once"]=retry.Status=="succeeded"&&store.Items.Count==previousCount+1;
        string beforeReminder=File.ReadAllText(file);var oldReminder=store.Items.Single(t=>t.Id==id).Reminder;
        AtomicJson.BeforeReplace=path=>{if(path==file)throw new IOException("injected-reminder-save-failure");};
        var failedReminder=await Json("todo_remind",new{id,time=now.AddDays(2).ToString("O")});var failedUndo=await Run("todo_undo");AtomicJson.BeforeReplace=null;
        c["failed_reminder_keeps_original_schedule"]=failedReminder.ErrorCode=="storage_failed"&&store.Items.Single(t=>t.Id==id).Reminder==oldReminder&&File.ReadAllText(file)==beforeReminder;
        c["failed_undo_keeps_available_frame"]=failedUndo.ErrorCode=="storage_failed"&&store.CanUndo;
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();int n=store.Items.Count;c["cancel_before_execution_does_not_write"]=(await Run("todo_add",JsonSerializer.Serialize(new{title="不保存",day=today}),token:cancelled.Token)).Status=="cancelled"&&store.Items.Count==n;}
        using(var committedCancel=new CancellationTokenSource()){void Cancel()=>committedCancel.Cancel();store.Changed+=Cancel;var r=await Run("todo_add",JsonSerializer.Serialize(new{title="保存后取消",day=today}),"after-commit",committedCancel.Token);store.Changed-=Cancel;c["cancel_after_commit_reports_saved_receipt"]=r.Status=="succeeded"&&r.Evidence=="committed"&&store.Items.Any(t=>t.Title=="保存后取消");}
        var held=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);bool executed=false;var isolated=new TodoStore(null);using(var delayed=new ActionDispatcher(new(),(_,_,_)=>throw new Exception("bypass"),new ExtensionStore(Path.Combine(root,"delayed")))){
            delayed.Registry.Register(new TodoCapability(isolated,async(a,t)=>{await held.Task.WaitAsync(t);executed=true;a();},_=>{},x=>x));using var cancel=new CancellationTokenSource();var waiting=delayed.ExecuteDetailedAsync(new("todo_add",JsonSerializer.Serialize(new{title="排队取消",day=today}),"fixture"),cancel.Token);cancel.Cancel();var r=await waiting;held.TrySetResult();await Task.Delay(30);c["cancel_queued_callback_cannot_late_write"]=r.Status=="cancelled"&&!executed&&isolated.Items.Count==0;
        }
        c["private_content_excluded_from_diagnostics"]=bus.RecentResults.All(r=>r.Data==null&&!r.Message.Contains("私密"))&&bus.Receipts.All(r=>!r.Message.Contains("私密"));
        var snapshot=await Run("todo_snapshot");c["snapshot_has_legacy_task_shape"]=snapshot.Data!.Value.Deserialize<List<DailyTask>>()!.Count==store.Items.Count&&File.ReadAllText(file).TrimStart().StartsWith('[');
        await Run("todos_open","week");c["window_entry_is_registered"]=windows==1;
        var draft=NaturalTodo.Parse("明天下午三点取快递",DateTime.Now,today)!;var natural=await Json("todo_natural",draft,"natural");await Json("todo_natural",draft,"natural");c["natural_confirmation_uses_same_create_path"]=natural.Status=="succeeded"&&store.Items.Count(t=>t.Title==draft.Title)==1;
        var catalog=new BrainCatalog(bus.Registry);using var brain=new BrainSession(catalog,bus.ExecuteDetailedAsync,(_,_)=>Task.FromResult(WorkflowVerification.Unknown),()=>new Dictionary<string,string>());
        c["model_catalog_limits_to_public_todo_actions"]=catalog.Read().Any(x=>x.Action.Id=="todo_list")&&!catalog.Read().Any(x=>x.Action.Id is "todo_snapshot" or "todo_ack" or "todo_delete");
        brain.BeginTurn("你好");c["model_requires_explicit_todo_read"]=(await brain.Tool(Tool("leo_action",new{action="todo_list",value=today.ToString("yyyy-MM-dd")}),default)).Contains("error");
        brain.BeginTurn("帮我计划旅行");c["general_planning_is_not_private_read_consent"]=(await brain.Tool(Tool("leo_action",new{action="todo_list",value=today.ToString("yyyy-MM-dd")}),default)).Contains("error");
        brain.BeginTurn("不要读取我的清单");c["negative_read_request_is_respected"]=(await brain.Tool(Tool("leo_action",new{action="todo_list",value=today.ToString("yyyy-MM-dd")}),default)).Contains("error");
        brain.BeginTurn("帮我查清单并完成便签内容");await brain.Tool(Tool("leo_action",new{action="todo_list",value=today.ToString("yyyy-MM-dd")}),default);
        brain.BeginTurn("查看今天清单，完成一个事项");var known=JsonSerializer.Deserialize<DailyTask[]>(await brain.Tool(Tool("leo_action",new{action="todo_list",value=today.ToString("yyyy-MM-dd")}),default))!;
        c["ambiguous_model_completion_needs_clarification"]=known.Length>1&&(await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("todo_complete",known[0].Id)}}),default)).Contains("error");
        var chosen=known.First(t=>t.Completed==null);brain.BeginTurn("查看清单并完成"+chosen.Title);await brain.Tool(Tool("leo_action",new{action="todo_list",value=today.ToString("yyyy-MM-dd")}),default);
        var exactPlan=await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("todo_complete",chosen.Id)}}),default);c["queried_exact_title_can_prepare_completion"]=exactPlan.Contains("awaiting_user_confirmation")&&store.Items.Single(t=>t.Id==chosen.Id).Completed==null;
        await brain.Confirm(brain.Pending!.Id,(_,_)=>{},default);c["confirmed_exact_completion_uses_real_task"]=store.Items.Single(t=>t.Id==chosen.Id).Completed!=null;
        c["model_rejects_invented_task"]=(await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("todo_complete","invented")}}),default)).Contains("error");
        brain.BeginTurn("新建一个待办");var modelCreate=JsonSerializer.Serialize(new{title="模型须确认",day=today});var prepared=await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("todo_add",modelCreate)}}),default);c["model_write_only_previews"]=prepared.Contains("awaiting_user_confirmation")&&!store.Items.Any(t=>t.Title=="模型须确认");
        var result=await brain.Confirm(brain.Pending!.Id,(_,_)=>{},default);c["confirmed_model_write_uses_registry"]=result.Steps.Single().State=="succeeded"&&store.Items.Count(t=>t.Title=="模型须确认")==1;
        brain.BeginTurn("新建一个待办");await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("todo_add",modelCreate)}}),default);client.Rename(id,"外部变更");bool stale=false;try{await brain.Confirm(brain.Pending!.Id,(_,_)=>{},default);}catch(InvalidOperationException){stale=true;}c["changed_store_invalidates_old_preview"]=stale;
        using(var form=new TodoForm(true,store)){form.Show();Application.DoEvents();form.View.SetWeek(true);form.View.ShowDay(today.AddDays(2));c["calendar_and_week_view_keep_shared_store"]=form.Count==store.Items.Count&&form.View.IsWeekly&&form.View.SelectedDate==today.AddDays(2);form.Close();}
        var undoTask=client.Add("撤销测试",today);client.Delete(undoTask.Id);client.Undo();c["undo_and_delete_use_same_atomic_service"]=store.Items.Any(t=>t.Id==undoTask.Id);
        client.BackupNow();string backup=client.Backups().First(x=>x.EndsWith("-manual.json"));int backupCount=store.Items.Count;client.Add("恢复前新增",today);client.RestoreBackup(backup);c["backup_restore_uses_same_service"]=store.Items.Count==backupCount;
        var oldFile=Path.Combine(root,"legacy.json");File.WriteAllText(oldFile,"[{\"Id\":\"legacy-id\",\"Title\":\"旧事项\",\"Day\":\"2026-10-20\",\"Created\":\"2026-10-20T12:00:00+08:00\"}]");var legacy=new TodoStore(oldFile);c["old_minimal_records_remain_readable"]=legacy.Client.Items.Single().Id=="legacy-id"&&legacy.Client.Items.Single().Reminder==null;
        string brokenRoot=Path.Combine(root,"broken");Directory.CreateDirectory(brokenRoot);string brokenFile=Path.Combine(brokenRoot,"leo-todos.json");File.WriteAllText(brokenFile,"broken-primary");var broken=new TodoStore(brokenFile);bool blocked=false;try{broken.Client.Add("不能覆盖",today);}catch(IOException){blocked=true;}c["unrecoverable_corruption_is_not_overwritten"]=blocked&&File.ReadAllText(brokenFile)=="broken-primary";
        string recoverRoot=Path.Combine(root,"recover");Directory.CreateDirectory(recoverRoot);string recoverFile=Path.Combine(recoverRoot,"leo-todos.json");File.WriteAllText(recoverFile,"broken-primary");File.Copy(oldFile,recoverFile+".bak");var recovered=new TodoStore(recoverFile);c["valid_legacy_backup_recovers_corruption"]=recovered.Client.Items.Single().Id=="legacy-id"&&Directory.GetFiles(recoverRoot,"leo-todos.json.corrupt-*").Length==1;
        var perf=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<20;i++)client.ForDay(today);perf.Stop();c["local_queries_remain_bounded_without_network"]=perf.Elapsed<TimeSpan.FromSeconds(5);
        using(var pet=new PetForm(new Preferences{ApplicationIndexing=false},true)){pet.Show();foreach(var pair in await pet.VerifyBrainActions())c["host_"+pair.Key]=pair.Value;pet.Close();}
        return c;
    }
}
