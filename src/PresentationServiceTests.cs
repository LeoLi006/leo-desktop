using System.Text.Json;
namespace LeoLauncher;
static class PresentationServiceTests
{
    static Task Queued(Func<Task> action)
    {
        using var queued=new ManualResetEventSlim();
        var task=Task.Run(async()=>{Task pending;try{pending=action();}finally{queued.Set();}await pending;});
        if(!queued.Wait(3000))throw new TimeoutException("dispatch did not queue");return task;
    }
    static async Task<bool> Cancelled(Task task){try{await task;return false;}catch(OperationCanceledException){return true;}}
    static ActionResult Result(string module,string status="succeeded")=>new("request","trace","fixture","fixture",module,"operation",status,"fixture",null,null,"unknown");
    static bool Rejected(Action action){try{action();return false;}catch(ArgumentException){return true;}}
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{var c=new Dictionary<string,bool>();try{await Check(ui,c);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,external_process_starts=0}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{ui.Close();}};Application.Run(ui);
    }
    static async Task Check(Form ui,Dictionary<string,bool> c)
    {
        var enabled=Enum.GetValues<ModuleWindowTarget>().ToDictionary(t=>t,_=>true);var shown=new List<ModuleWindowRequest>();var notices=new List<LeoNotice>();var dismissed=new List<string>();bool failWindow=false,failFeedback=false;
        var legacy=new ModuleWindowService(ui);var host=new ModulePresentationService(legacy,t=>enabled[t],r=>{if(failWindow)throw new IOException("fixture window");shown.Add(r);},n=>{if(failFeedback)throw new IOException("fixture feedback");notices.Add(n);},dismissed.Add);
        host.Configure();
        c["port_does_not_expose_controls"]=typeof(IModuleWindowPort).GetMethods().All(m=>m.GetParameters().All(p=>!typeof(Control).IsAssignableFrom(p.ParameterType)&&p.ParameterType!=typeof(Action)));
        int oldCalls=0;Func<Action,CancellationToken,Task> oldMethod=legacy.ShowAsync;await oldMethod(()=>oldCalls++,CancellationToken.None);c["old_window_method_group_compatible"]=oldCalls==1;
        foreach(var target in Enum.GetValues<ModuleWindowTarget>()){
            var port=host.For(target);int before=shown.Count;await port.ShowAsync(CancellationToken.None);c[target+"_fixed_target"]=shown.Count==before+1&&shown.Last().Target==target;
            var pending=Queued(()=>port.ShowAsync(CancellationToken.None));host.Invalidate(target);c[target+"_closed_generation_drops_queued_show"]=await Cancelled(pending)&&shown.Count==before+1;
            using(var stop=new CancellationTokenSource()){pending=Queued(()=>port.ShowAsync(stop.Token));stop.Cancel();c[target+"_cancel_drops_queued_show"]=await Cancelled(pending)&&shown.Count==before+1;}
            pending=Queued(()=>port.ShowAsync(CancellationToken.None));enabled[target]=false;host.Configure();enabled[target]=true;host.Configure();c[target+"_disable_reenable_drops_old_request"]=await Cancelled(pending)&&shown.Count==before+1;
            await port.ShowAsync(CancellationToken.None);c[target+"_fresh_request_recovers"]=shown.Count==before+2;
        }
        await host.ShowAsync(new(ModuleWindowTarget.Applications,"fixture query",true),CancellationToken.None);
        c["application_query_web_options_preserved"]=shown.Last() is {Query:"fixture query",Web:true};
        await host.ShowAsync(new(ModuleWindowTarget.Notes,"note draft"),CancellationToken.None);
        c["notes_draft_request_preserved"]=shown.Last().Query=="note draft";
        c["quick_note_draft_rejected"]=Rejected(()=>host.ShowAsync(new(ModuleWindowTarget.QuickNote,"invalid"),CancellationToken.None));
        c["todo_bad_view_rejected"]=Rejected(()=>host.ShowAsync(new(ModuleWindowTarget.Todos,View:"invalid"),CancellationToken.None));
        c["non_todo_intent_rejected"]=Rejected(()=>host.ShowAsync(new(ModuleWindowTarget.Todos,PlannerIntent:new(QuickKind.OpenApp,"invalid")),CancellationToken.None));
        c["other_window_query_rejected"]=Rejected(()=>host.ShowAsync(new(ModuleWindowTarget.Football,"invalid"),CancellationToken.None));
        c["unknown_window_rejected"]=Rejected(()=>host.For((ModuleWindowTarget)99).ShowAsync(CancellationToken.None));
        failWindow=true;bool failed=false;try{await host.For(ModuleWindowTarget.Football).ShowAsync(CancellationToken.None);}catch(IOException){failed=true;}failWindow=false;await host.For(ModuleWindowTarget.Media).ShowAsync(CancellationToken.None);c["one_window_failure_isolated"]=failed&&shown.Last().Target==ModuleWindowTarget.Media;
        var delivery=host.Feedback(ModuleWindowTarget.Music,CancellationToken.None);int count=notices.Count;await delivery.ReportAsync(Result("music"));c["success_stays_quiet_without_ack"]=notices.Count==count;
        await delivery.ReportAsync(Result("music"),true);c["ack_keeps_normal_notice_policy"]=notices.Last() is {Error:false,Module:"music"}&&!FeedbackPolicy.ShouldPopup(notices.Last());
        await delivery.ReportAsync(Result("music","failed"));c["error_remains_attention"]=notices.Last().Error&&FeedbackPolicy.ShouldPopup(notices.Last());
        count=notices.Count;await delivery.ReportAsync(Result("music","cancelled"),true);c["cancelled_result_not_published"]=notices.Count==count;
        c["foreign_result_rejected"]=Rejected(()=>delivery.ReportAsync(Result("media"),true));
        var pendingFeedback=Queued(()=>delivery.ReportAsync(Result("music","failed")));host.Invalidate(ModuleWindowTarget.Music);await pendingFeedback;c["closed_window_drops_old_feedback"]=notices.Count==count;
        using(var stop=new CancellationTokenSource()){delivery=host.Feedback(ModuleWindowTarget.Music,stop.Token);pendingFeedback=Queued(()=>delivery.ReportAsync(Result("music","failed")));stop.Cancel();await pendingFeedback;c["cancel_drops_queued_feedback"]=notices.Count==count;}
        delivery=host.Feedback(ModuleWindowTarget.Music,CancellationToken.None);failFeedback=true;await delivery.ReportAsync(Result("music","failed"));failFeedback=false;await delivery.ReportAsync(Result("music"),true);c["feedback_sink_failure_does_not_poison_host"]=notices.Count==count+1;
        count=notices.Count;
        var progress=new ModuleProgressLease(host.Feedback(ModuleWindowTarget.Media,CancellationToken.None),"media");
        var queuedProgress=Task.Run(()=>{progress.Publish(new("waiting",Busy:true,Module:"foreign",OperationId:"owned-operation"));});queuedProgress.Wait(3000);progress.Dispose();await Task.Delay(30);c["disposed_progress_cannot_reappear"]=notices.Count==count&&dismissed.Contains("owned-operation");
        var originalSink=OperationFeedback.Sink;var liveNotices=new List<LeoNotice>();OperationFeedback.Sink=liveNotices.Add;
        try{
            using var pet=new PetForm(new Preferences{ApplicationIndexing=false},true,true);pet.Opacity=0;pet.ShowInTaskbar=false;pet.Show();
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var live=(ModulePresentationService)typeof(PetForm).GetField("presentation",flags)!.GetValue(pet)!;
            var launcher=(LauncherForm)typeof(PetForm).GetField("launcher",flags)!.GetValue(pet)!;
            foreach(var target in Enum.GetValues<ModuleWindowTarget>()){
                await live.For(target).ShowAsync(CancellationToken.None);
                Form window=target switch{
                    ModuleWindowTarget.Applications=>launcher,
                    ModuleWindowTarget.Clipboard=>(Form)typeof(LauncherForm).GetField("clipboardWindow",flags)!.GetValue(launcher)!,
                    ModuleWindowTarget.Music or ModuleWindowTarget.Football=>(Form)typeof(LauncherForm).GetField(target==ModuleWindowTarget.Music?"music":"football",flags)!.GetValue(launcher)!,
                    _=>(Form)typeof(PetForm).GetField(target switch{ModuleWindowTarget.Media=>"mediaWindow",ModuleWindowTarget.Todos=>"todoWindow",ModuleWindowTarget.Notes=>"notesWindow",ModuleWindowTarget.Chat=>"chat",_=>"quickNoteWindow"},flags)!.GetValue(pet)!};
                c[target+"_real_host_opens_existing_window"]=window.Visible;
                var oldFeedback=live.Feedback(target,CancellationToken.None);int before=liveNotices.Count;
                EscapeWindows.Dismiss(window);
                string module=target switch{ModuleWindowTarget.Applications=>"core",ModuleWindowTarget.Music=>"music",ModuleWindowTarget.Media=>"media",ModuleWindowTarget.Todos=>"todos",ModuleWindowTarget.Chat=>"conversation",ModuleWindowTarget.Clipboard=>"clipboard",ModuleWindowTarget.Notes or ModuleWindowTarget.QuickNote=>"notes",_=>"football-data"};
                await oldFeedback.ReportAsync(Result(module),true);
                c[target+"_real_close_invalidates_feedback"]=!window.Visible&&liveNotices.Count==before;
            }
            await live.ShowAsync(new(ModuleWindowTarget.Todos,View:"week",PlannerIntent:new(QuickKind.Todos,DayOffset:2)),CancellationToken.None);
            var todo=(TodoForm)typeof(PetForm).GetField("todoWindow",flags)!.GetValue(pet)!;
            c["todo_week_and_date_preserved"]=todo.View.IsWeekly&&todo.View.SelectedDate==DateOnly.FromDateTime(DateTime.Today).AddDays(2);
            var store=(TodoStore)typeof(PetForm).GetField("todoStore",flags)!.GetValue(pet)!;
            var reminder=store.Add("fixture reminder",DateOnly.FromDateTime(DateTime.Today),DateTime.Now.AddMinutes(-1));
            var capability=(IStructuredLeoModule)pet.ContractRegistry.Resolve("todos_open");
            var weekReceipt=await capability.ExecuteStructuredAsync(new("todos_open","week"),CancellationToken.None);
            c["registered_todo_week_receipt_preserved"]=todo.View.IsWeekly&&weekReceipt.Evidence=="window-shown";
            await capability.ExecuteStructuredAsync(new("todos_open"),CancellationToken.None);
            c["registered_todo_daily_view_preserved"]=!todo.View.IsWeekly;
            var oldTodoOpen=Queued(async()=>{await capability.ExecuteStructuredAsync(new("todos_open","week"),CancellationToken.None);});
            EscapeWindows.Dismiss(todo);
            c["registered_todo_old_open_dropped_after_close"]=await Cancelled(oldTodoOpen)&&!todo.Visible;
            int savedTasks=store.Items.Count;
            var oldIntent=Queued(()=>live.ShowAsync(new(ModuleWindowTarget.Todos,PlannerIntent:new(QuickKind.AddTodo,"must not save")),CancellationToken.None));
            live.Invalidate(ModuleWindowTarget.Todos);
            c["cancelled_legacy_intent_does_not_save"]=await Cancelled(oldIntent)&&store.Items.Count==savedTasks;
            c["closing_todo_keeps_due_reminder"]=store.Items.Any(t=>t.Id==reminder.Id)&&store.Due(DateTimeOffset.Now).Any(t=>t.Id==reminder.Id);
            await live.For(ModuleWindowTarget.QuickNote).ShowAsync(CancellationToken.None);
            var quick=(QuickNoteForm)typeof(PetForm).GetField("quickNoteWindow",flags)!.GetValue(pet)!;
            var oldQuickFeedback=live.Feedback(ModuleWindowTarget.QuickNote,CancellationToken.None);
            quick.Controls.OfType<TextBox>().Single().Text="saved fixture";((Button)quick.AcceptButton!).PerformClick();
            var notes=(NoteStore)typeof(PetForm).GetProperty("Notes",flags)!.GetValue(pet)!;
            c["quick_note_saved_once_before_close"]=quick.IsDisposed&&notes.Query("saved fixture").Count==1&&quick.SavedResult is {Status:"succeeded",Evidence:"committed"};
            c["committed_quick_note_confirmation_preserved"]=liveNotices.Count(n=>n.Text=="已记下")==1;
            int afterSave=liveNotices.Count;await oldQuickFeedback.ReportAsync(Result("notes","failed"));
            c["old_quick_note_feedback_does_not_replay_save"]=liveNotices.Count==afterSave&&notes.Query("saved fixture").Count==1;
            var clipModule=(IStructuredLeoModule)pet.ContractRegistry.Resolve("clipboard_open");
            var clipReceipt=await clipModule.ExecuteStructuredAsync(new("clipboard_open"),CancellationToken.None);
            c["registered_clipboard_window_receipt"]=clipReceipt.Evidence=="window-shown";
            var clips=launcher.ClipboardService;
            var clipboardWindow=(Form)typeof(LauncherForm).GetField("clipboardWindow",flags)!.GetValue(launcher)!;
            var pendingClip=Queued(async()=>{await clipModule.ExecuteStructuredAsync(new("clipboard_open"),CancellationToken.None);});
            clips.Enabled=false;
            c["recording_off_drops_old_clipboard_open"]=await Cancelled(pendingClip)&&!clips.Enabled;
            await clipModule.ExecuteStructuredAsync(new("clipboard_open"),CancellationToken.None);
            c["recording_off_keeps_history_window_available"]=clipboardWindow.Visible&&!clips.IsClosed;
            EscapeWindows.Dismiss(clipboardWindow);clips.Enabled=true;
            c["clipboard_window_close_keeps_service"]=!clips.IsClosed&&clips.Enabled;
            var chatModule=(IStructuredLeoModule)pet.ContractRegistry.Resolve("chat_open");
            var chatReceipt=await chatModule.ExecuteStructuredAsync(new("chat_open"),CancellationToken.None);
            c["registered_chat_window_receipt"]=chatReceipt.Evidence=="window-shown";
            var chatWindow=(Form)typeof(PetForm).GetField("chat",flags)!.GetValue(pet)!;
            var pendingChat=Queued(async()=>{await chatModule.ExecuteStructuredAsync(new("chat_open"),CancellationToken.None);});
            EscapeWindows.Dismiss(chatWindow);
            c["chat_close_drops_old_registered_open"]=await Cancelled(pendingChat)&&chatWindow.IsDisposed;
            var conversationHost=(ConversationHost)typeof(PetForm).GetField("conversations",flags)!.GetValue(pet)!;
            pendingChat=Queued(async()=>{await chatModule.ExecuteStructuredAsync(new("chat_open"),CancellationToken.None);});conversationHost.Dispose();
            c["conversation_shutdown_drops_queued_open"]=await Cancelled(pendingChat);
            var staleClip=live.Feedback(ModuleWindowTarget.Clipboard,CancellationToken.None);int beforeDispose=liveNotices.Count;clips.Dispose();
            await staleClip.ReportAsync(Result("clipboard","failed"));
            c["clipboard_disposal_drops_old_feedback"]=liveNotices.Count==beforeDispose;
            c["production_progress_uses_host"]=new[]{"musicCapabilityQueue","mediaCapabilityQueue"}.All(name=>((NativeCapabilityQueue)typeof(PetForm).GetField(name,flags)!.GetValue(pet)!).Feedback!=null);
        }finally{OperationFeedback.Sink=originalSink;}
        int captures=0,transitions=0;
        using(var clips=new ClipboardHistoryService(new Preferences(),true,captureOverride:()=>captures++)){
            clips.StateChanged+=()=>transitions++;
            var window=ClipboardTool.Open(clips)!;window.Close();int beforeCapture=captures;clips.Capture();
            c["closed_window_keeps_simulated_capture"]=captures==beforeCapture+1&&!clips.IsClosed;
            clips.Enabled=false;beforeCapture=captures;clips.Capture();c["recording_disabled_stops_simulated_capture"]=captures==beforeCapture;
            clips.Enabled=true;clips.Capture();c["reenable_does_not_duplicate_capture"]=captures==beforeCapture+1&&transitions==2;
        }
        using(var owner=new Form{Opacity=0,ShowInTaskbar=false}){
            owner.Show();var dying=new ModuleWindowService(owner);int shows=0;var pending=Queued(()=>dying.ShowAsync(()=>shows++,CancellationToken.None));owner.Dispose();bool disposed=false;try{await pending;}catch(ObjectDisposedException){disposed=true;}c["disposed_owner_drops_pending_show"]=disposed&&shows==0;
        }
    }
}
