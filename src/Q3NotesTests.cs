using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;

static class Q3NotesTests
{
    static IEnumerable<Control> Walk(Control control){foreach(Control child in control.Controls){yield return child;foreach(var nested in Walk(child))yield return nested;}}
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var window=new Form{Opacity=0,ShowInTaskbar=false};
        window.Shown+=async(_,_)=>{try{var checks=await Check(Path.GetDirectoryName(report)!);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}finally{window.Close();}};Application.Run(window);
    }
    static async Task<Dictionary<string,bool>> Check(string root)
    {
        var c=new Dictionary<string,bool>();root=Path.Combine(root,"fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var notes=new NoteStore(Path.Combine(root,"notes.json"));int legacy=0,opened=0;
        using var bus=new ActionDispatcher(new(),(_,_,_)=>{legacy++;return Task.FromResult("legacy");},new ExtensionStore(Path.Combine(root,"extensions")));
        bus.Registry.Register(new NotesCapability(()=>notes,(_,_,t)=>{t.ThrowIfCancellationRequested();opened++;return Task.CompletedTask;}));
        Task<ActionResult> Run(string action,object value,string? id=null,CancellationToken token=default)=>bus.ExecuteDetailedAsync(new(action,JsonSerializer.Serialize(value),"q3-fixture",id),token);
        c["registry_replaces_old_note_actions_without_duplicates"]=bus.Registry.Read().SelectMany(m=>m.Actions).Count(a=>a=="note_add")==1&&bus.Registry.Resolve("note_add").Id=="notes";
        var first=await bus.ExecuteDetailedAsync(new("note_add","保留身份的便签","chat","save-once"));string id=first.Data!.Value.Deserialize<StickyNote>()!.Id;
        var replay=await bus.ExecuteDetailedAsync(new("note_add","保留身份的便签","voice","save-once"));
        c["same_request_retry_does_not_duplicate_note"]=notes.Items.Count==1&&replay.Data!.Value.Deserialize<StickyNote>()!.Id==id;
        c["replay_preserves_each_source"]=first.Source=="chat"&&replay.Source=="voice";
        c["same_id_different_payload_rejected"]=(await bus.ExecuteDetailedAsync(new("note_add","不能换内容","chat","save-once"))).ErrorCode=="invalid_request";
        await Run("note_pin",new{id,value=true});await Run("note_link",new{id,task="existing-task"});await Run("note_save",new{id,text="修改文字"});
        var restored=new NoteStore(Path.Combine(root,"notes.json")).Items.Single();
        c["old_json_identity_pin_and_task_link_preserved"]=restored.Id==id&&restored.Pinned&&restored.TaskId=="existing-task"&&restored.Text=="修改文字";
        c["search_and_linked_group_use_registered_query"]=(await Run("note_query",new{text="修改",scope="linked"})).Data!.Value.GetArrayLength()==1;
        var snapshot=notes.Items;snapshot.Clear();c["external_snapshot_cannot_mutate_store"]=notes.Items.Count==1;
        c["diagnostic_history_excludes_note_bodies"]=bus.RecentResults.All(r=>r.Data==null);
        c["deleted_note_update_does_not_create_new_note"]=(await Run("note_save",new{id="missing",text="stale"})).Status=="failed"&&notes.Items.Count==1;
        var many=await Task.WhenAll(Enumerable.Range(0,25).Select(i=>bus.ExecuteDetailedAsync(new("note_add","并发 "+i,"search"))));
        c["concurrent_writes_are_serialized_without_lost_notes"]=many.All(r=>r.Status=="succeeded")&&notes.Items.Count==26;
        await Task.WhenAll(Enumerable.Range(0,20).Select(_=>Run("note_query",new{scope="all"})));
        c["read_write_concurrency_preserves_valid_json"]=new NoteStore(Path.Combine(root,"notes.json")).Items.Count==26;
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();c["pre_cancelled_request_does_not_write"]=(await bus.ExecuteDetailedAsync(new("note_add","cancelled"),cancel.Token)).Status=="cancelled"&&notes.Items.Count==26;}
        var structured=await bus.ExecuteStructuredAsync(new("leo-action/1","note_save",JsonSerializer.SerializeToElement(new{id,text="结构化保存"}),"combo"));
        c["structured_and_legacy_requests_share_identity"]=structured.Status=="succeeded"&&notes.Items.Single(n=>n.Id==id).Text=="结构化保存";
        c["structured_unknown_fields_rejected"]=(await bus.ExecuteStructuredAsync(new("leo-action/1","note_save",JsonSerializer.SerializeToElement(new{id,text="ignored",extra=true})))).ErrorCode=="invalid_request";
        c["malformed_request_is_contained"]=(await bus.ExecuteDetailedAsync(new("note_pin","{}"))).Status=="failed";
        c["maximum_chinese_note_remains_supported"]=(await Run("note_save",new{id,text=new string('字',20000)})).Status=="succeeded";
        await Run("note_save",new{id,text="原始保存"});string original=File.ReadAllText(Path.Combine(root,"notes.json"));
        AtomicJson.BeforeReplace=path=>{if(path==Path.Combine(root,"notes.json"))throw new IOException("fixture disk failure");};
        try{c["failed_write_has_storage_error"]=(await Run("note_save",new{id,text="不能覆盖"})).ErrorCode=="storage_failed";}
        finally{AtomicJson.BeforeReplace=null;}
        c["failed_write_keeps_disk_and_memory"]=File.ReadAllText(Path.Combine(root,"notes.json"))==original&&notes.Items.Single(n=>n.Id==id).Text=="原始保存";
        c["manual_retry_after_storage_failure_succeeds"]=(await Run("note_save",new{id,text="重试成功"})).Status=="succeeded";
        int before=notes.Items.Count;await Task.WhenAll(Enumerable.Range(0,4).Select(_=>bus.ExecuteDetailedAsync(new("note_add","相同文字但不同用户操作"))));
        c["independent_identical_writes_are_not_coalesced"]=notes.Items.Count==before+4;
        using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
            var blocked=new NoteStore(Path.Combine(root,"blocked.json"),(path,content)=>{entered.Set();if(!release.Wait(3000))throw new TimeoutException("fixture");ConfigMigration.Write(path,content);});
            using var queued=new ActionDispatcher(new(),(_,_,_)=>throw new Exception("legacy"),new ExtensionStore(Path.Combine(root,"no-extensions")));
            queued.Registry.Register(new NotesCapability(()=>blocked,(_,_,_)=>Task.CompletedTask));
            var running=queued.ExecuteDetailedAsync(new("note_add","first"));if(!entered.Wait(2000))throw new TimeoutException("fixture did not start");
            using var cancel=new CancellationTokenSource();var waiting=queued.ExecuteDetailedAsync(new("note_add","must not commit"),cancel.Token);cancel.Cancel();
            c["queued_write_cancellation_returns_without_waiting_for_disk"]=(await waiting.WaitAsync(TimeSpan.FromSeconds(1))).Status=="cancelled";
            release.Set();await running;await Task.Delay(40);c["cancelled_queued_write_does_not_commit"]=blocked.Items.Count==1;
        }
        using(var cancellation=new CancellationTokenSource()){
            var committed=new NoteStore(Path.Combine(root,"committed.json"),(path,content)=>{ConfigMigration.Write(path,content);cancellation.Cancel();});
            using var commitBus=new ActionDispatcher(new(),(_,_,_)=>throw new Exception("legacy"),new ExtensionStore(Path.Combine(root,"commit-extensions")));
            commitBus.Registry.Register(new NotesCapability(()=>committed,(_,_,_)=>Task.CompletedTask));
            var lostAck=await commitBus.ExecuteDetailedAsync(new("note_add","已落盘但取消等待","chat","committed-request"),cancellation.Token);
            var recovered=await commitBus.ExecuteDetailedAsync(new("note_add","已落盘但取消等待","chat","committed-request"));
            c["retry_after_commit_and_cancel_does_not_duplicate"]=committed.Items.Count==1&&recovered.Status=="succeeded";
            c["cancelled_commit_does_not_claim_rollback"]=lostAck.Status=="cancelled"&&lostAck.Message.Contains("可能已经生效");
        }
        await bus.ExecuteDetailedAsync(new("notes_open","草稿","menu"));await bus.ExecuteDetailedAsync(new("note_quick_open","","hotkey"));
        c["window_entries_and_chat_never_fall_back_to_legacy"]=opened==2&&legacy==0;
        await bus.ExecuteTextAsync("记一下搜索入口便签","search");c["search_phrase_uses_same_note_module"]=notes.Query("搜索入口便签").Count==1;

        var uiNotes=new NoteStore(null);using(var form=new NotesForm(uiNotes,(_,_)=>"fixture-task")){
            form.Show();Application.DoEvents();form.New("界面保存");Walk(form).OfType<Button>().Single(b=>b.Text=="记下 / 保存").PerformClick();
            c["notes_window_saves_through_module"]=uiNotes.Items.Count==1;
            Walk(form).OfType<Button>().Single(b=>b.Text=="置顶").PerformClick();c["notes_window_pin_uses_module"]=uiNotes.Items[0].Pinned;
            var search=Walk(form).OfType<TextBox>().Single(t=>t.AccessibleName=="便签搜索");search.Text="没有匹配";c["notes_window_search_uses_module"]=Walk(form).OfType<ListBox>().Single().Items.Count==0;search.Clear();
            Walk(form).OfType<Button>().Single(b=>b.Text=="转为待办").PerformClick();c["existing_conversion_keeps_note_task_link"]=uiNotes.Items[0].TaskId=="fixture-task";form.Close();
        }
        using(var quick=new QuickNoteForm(uiNotes)){quick.Show();Application.DoEvents();Walk(quick).OfType<TextBox>().Single().Text="速记保存";((Button)quick.AcceptButton!).PerformClick();c["quick_note_enter_path_saves_and_closes"]=quick.IsDisposed&&uiNotes.Query("速记保存").Count==1;}
        var faultStore=new NoteStore(Path.Combine(root,"failure.json"),(_,_)=>throw new IOException("fixture"));
        using(var quick=new QuickNoteForm(faultStore)){quick.Show();Application.DoEvents();var entry=Walk(quick).OfType<TextBox>().Single();entry.Text="保留失败草稿";((Button)quick.AcceptButton!).PerformClick();c["failed_quick_note_keeps_draft"]=!quick.IsDisposed&&entry.Text=="保留失败草稿"&&faultStore.Items.Count==0;quick.Close();}
        bool failLink=true;int conversions=0;var receipts=new Dictionary<string,string>();
        var linking=new NoteStore(Path.Combine(root,"linking.json"),(path,content)=>{if(failLink&&content.Contains("fixture-link"))throw new IOException("fixture link failed");ConfigMigration.Write(path,content);});
        using(var client=NotesClient.Standalone(linking))using(var form=new NotesForm(client,(_,_)=>{conversions++;return "fixture-link";},receipts)){
            form.Show();form.New("转换后失败");Walk(form).OfType<Button>().Single(b=>b.Text=="转为待办").PerformClick();
            c["failed_link_keeps_existing_note_and_conversion_receipt"]=linking.Items.Single().TaskId==null&&receipts.Count==1&&conversions==1;form.Close();
        }
        failLink=false;
        using(var client=NotesClient.Standalone(linking))using(var form=new NotesForm(client,(_,_)=>{conversions++;return "duplicate";},receipts)){
            form.Show();Walk(form).OfType<ListBox>().Single().SelectedIndex=0;Walk(form).OfType<Button>().Single(b=>b.Text=="转为待办").PerformClick();
            c["reopening_and_retrying_link_does_not_create_second_todo"]=conversions==1&&linking.Items.Single().TaskId=="fixture-link"&&receipts.Count==0;form.Close();
        }

        using(var owner=new Form()){owner.Show();var service=new ModuleWindowService(owner);int calls=0;using var cancellation=new CancellationTokenSource();using var scheduled=new ManualResetEventSlim();var task=Task.Run(async()=>{var queued=service.ShowAsync(()=>calls++,cancellation.Token);scheduled.Set();await queued;});if(!scheduled.Wait(1500))throw new TimeoutException("window fixture");cancellation.Cancel();try{await task;}catch(OperationCanceledException){}Application.DoEvents();c["cancelled_queued_window_does_not_open"]=calls==0;owner.Close();}
        using(var owner=new Form()){owner.Show();int uiThread=Environment.CurrentManagedThreadId,actualThread=0;var service=new ModuleWindowService(owner);await Task.Run(()=>service.ShowAsync(()=>actualThread=Environment.CurrentManagedThreadId,default));c["worker_window_request_returns_to_owner_thread"]=actualThread==uiThread;owner.Dispose();bool closed=false;try{await service.ShowAsync(()=>actualThread=0,default);}catch(ObjectDisposedException){closed=true;}c["disposed_host_rejects_window_callback"]=closed&&actualThread==uiThread;}
        int successes=0,failures=0;var feedback=new ModuleFeedbackService(_=>successes++,_=>failures++);feedback.Report(first);feedback.Report(first with{Status="failed"});c["feedback_is_quiet_on_success_and_reports_errors"]=successes==0&&failures==1;
        using(var pet=new PetForm(new(){ApplicationIndexing=false},true)){pet.Show();await pet.VerifyQ3Entries(c);var exitNotes=pet.Q3PrepareExitDraft();pet.Close();c["host_exit_saves_dirty_note_before_dispatcher_shutdown"]=exitNotes.Query("退出前保留的便签").Count==1;}
        var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;long memory=process.PrivateMemorySize64;var clock=Stopwatch.StartNew();
        for(int i=0;i<100;i++)await Run("note_query",new{text="并发",scope="all"});clock.Stop();process.Refresh();
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(root)!,"resource-smoke.json"),JsonSerializer.Serialize(new{queries=100,elapsed_ms=clock.ElapsedMilliseconds,cpu_ms=(process.TotalProcessorTime-cpu).TotalMilliseconds,private_bytes_change=process.PrivateMemorySize64-memory,background_timers_added=0}));
        c["bounded_history_after_query_smoke"]=bus.RecentResults.Count==60&&bus.RecentResults.All(r=>r.Data==null);
        return c;
    }
}

sealed partial class PetForm
{
    internal NoteStore Q3PrepareExitDraft(){ShowNotesCore();notesWindow!.New("退出前保留的便签");return Notes;}
    internal async Task VerifyQ3Entries(Dictionary<string,bool> checks)
    {
        ShowQuickNote();await Task.Delay(30);checks["actual_quick_entry_opens_via_registry"]=quickNoteWindow is{Visible:true}&&commands.RecentResults.Any(r=>r.ActionId=="note_quick_open"&&r.ModuleId=="notes");quickNoteWindow?.Close();
        ShowNotes();await Task.Delay(30);checks["actual_notes_entry_opens_via_registry"]=notesWindow is{Visible:true}&&commands.RecentResults.Any(r=>r.ActionId=="notes_open"&&r.ModuleId=="notes");notesWindow?.Close();
        ShowChat();await chat!.PreviewSendAsync("记一下聊天入口便签");checks["actual_chat_note_save_uses_registry"]=Notes.Query("聊天入口便签").Count==1&&commands.RecentResults.Any(r=>r.Source=="chat"&&r.ActionId=="note_add"&&r.ModuleId=="notes");
        await chat.Q2ReceiveDirectVoice("记一下语音入口便签");checks["actual_voice_note_save_uses_registry"]=Notes.Query("语音入口便签").Count==1&&commands.RecentResults.Any(r=>r.Source=="voice"&&r.ActionId=="note_add");chat.Close();
        var step=BuildComboStep(new("command","记一下组合入口便签"));await step.Item2();checks["existing_combo_command_uses_notes_module"]=Notes.Query("组合入口便签").Count==1&&commands.RecentResults.Any(r=>r.Source=="combo"&&r.ActionId=="note_add");
    }
}
