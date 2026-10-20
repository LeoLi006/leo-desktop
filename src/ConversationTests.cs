using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
namespace LeoLauncher;
static class ConversationTests
{
    sealed class Speech:ISpeechIO
    {
        public int InputDevice{get;set;}=-1;public bool Listening{get;private set;}public bool Finishing{get;private set;}public bool Speaking{get;private set;}public bool Disposed,ThrowOnStop;public int Starts,Stops,Speaks;public long Generation{get;private set;}
        public bool IsCurrent(long g)=>!Disposed&&g==Generation;
        public event Action<string,bool,long>? Recognized;public event Action<string,long>? Status;public event Action<long>? Changed;
        public void Start(){Generation++;Listening=true;Finishing=false;Starts++;Changed?.Invoke(Generation);Status?.Invoke("fixture",Generation);}
        public void FinishListening(){Listening=false;Finishing=true;Changed?.Invoke(Generation);}
        public void StopListening(){Generation++;Listening=Finishing=false;Changed?.Invoke(Generation);}
        public void StopSpeaking(){Speaking=false;Changed?.Invoke(Generation);}
        public void Stop(){if(ThrowOnStop)throw new IOException("fixture-audio-failure");Stops++;StopListening();StopSpeaking();}
        public void Speak(string text){Speaks++;Speaking=true;Changed?.Invoke(Generation);}
        public void EmitStatus(string text,long generation)=>Status?.Invoke(text,generation);
        public void Emit(string text,bool confident=true,long? generation=null)=>Recognized?.Invoke(text,confident,generation??Generation);
        public void Dispose(){Disposed=true;Stop();}
    }
    sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken t)=>send(r,t);}
    static ChatReply Reply(string text="fixture")=>new(text,true,false);
    static async Task<bool> Throws<T>(Func<Task> action)where T:Exception{try{await action();return false;}catch(T){return true;}}
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{try{var checks=await Check(ui,Path.GetDirectoryName(report)!);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,real_api_requests=0,microphone_captures=0,native_asr_decodes=0,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}finally{ui.Close();}};Application.Run(ui);
    }
    static async Task<Dictionary<string,bool>> Check(Form ui,string root)
    {
        var c=new Dictionary<string,bool>();using var bus=new ActionDispatcher(new(),(_,_,_)=>throw new Exception("legacy conversation bypass"),new ExtensionStore(Path.Combine(root,"empty-modules")));int opened=0;
        using var host=new ConversationHost(bus,new(ui),()=>opened++);var speech=new Speech();using var client=host.Attach(speech);
        var result=await client.Reply((text,detail,t)=>{text("片段");return Task.FromResult(Reply("私密回复"));},_=>{},_=>{},"chat",ChatTests.FixtureKey,default);
        c["reply_runs_as_private_registered_action"]=result.Text=="私密回复"&&bus.RecentResults.Last().ModuleId=="conversation"&&bus.RecentResults.Last().Data==null;
        c["completed_job_is_released"]=host.JobCount==0;
        Action<string>? lateText=null,lateDetail=null;int lateUpdates=0;
        await client.Reply((text,detail,t)=>{lateText=text;lateDetail=detail;return Task.FromResult(Reply());},_=>lateUpdates++,_=>lateUpdates++,"chat",ChatTests.FixtureKey,default);lateText!("完成后的片段");lateDetail!("完成后的状态");
        c["callbacks_after_completion_are_ignored"]=lateUpdates==0&&!client.HadTools;
        c["diagnostics_exclude_reply_and_credential"]=bus.RecentResults.All(r=>!JsonSerializer.Serialize(r).Contains(ChatTests.FixtureKey)&&!r.Message.Contains("私密"));
        await bus.ExecuteDetailedAsync(new("chat_open","","fixture"));c["chat_open_is_registered"]=opened==1;
        c["unknown_reply_handle_rejected"]=(await bus.ExecuteDetailedAsync(new("chat_reply","invented","fixture"))).Status=="failed";
        c["external_structured_session_data_rejected"]=(await bus.ExecuteStructuredAsync(new("leo-action/1","chat_reply",JsonSerializer.SerializeToElement(new{prompt="private",key=ChatTests.FixtureKey}),"fixture"))).Status=="failed";
        var catalog=new BrainCatalog(bus.Registry);c["model_discovers_only_chat_window"]=catalog.Read().Any(e=>e.Action.Id=="chat_open")&&!catalog.Read().Any(e=>e.Action.Id is "chat_reply" or "voice_start" or "voice_speak" or "voice_accept");
        client.Voice("voice_start");long generation=speech.Generation;c["voice_start_uses_module"]=speech.Starts==1&&speech.Listening&&bus.RecentResults.Last().ActionId=="voice_start";
        c["recognition_receipt_is_once"]=client.AcceptVoice(generation)&&!client.AcceptVoice(generation);
        client.Voice("voice_finish");c["finish_moves_to_transcription"]=speech.Finishing;
        client.Voice("voice_stop");c["stopped_recognition_is_rejected"]=!client.AcceptVoice(generation)&&!speech.Listening&&!speech.Finishing;
        client.Speak("私密朗读");client.Voice("voice_stop_speaking");c["speech_text_uses_private_handle"]=speech.Speaks==1&&!speech.Speaking&&bus.RecentResults.All(r=>!r.Message.Contains("私密朗读"));
        int received=0,details=0;var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var pending=client.Reply(async(text,detail,t)=>{await release.Task;text("晚到文字");detail("晚到工具");return Reply("晚到回复");},_=>received++,_=>details++,"voice",ChatTests.FixtureKey,default);
        client.Cancel();c["cancel_finishes_wait_without_waiting_ignored_provider"]=await Throws<OperationCanceledException>(async()=>await pending);
        c["cancelled_provider_remains_bounded_until_it_finishes"]=host.JobCount==1;release.TrySetResult();await Task.Delay(35);
        c["cancelled_callbacks_cannot_update_new_state"]=received==0&&details==0&&host.JobCount==0;
        var fresh=await client.Reply((text,detail,t)=>Task.FromResult(Reply("新请求")),_=>{},_=>{},"chat",ChatTests.FixtureKey,default);c["new_request_works_after_cancel"]=fresh.Text=="新请求";
        c["stream_failure_is_structured_and_sanitized"]=await Throws<ChatWorkException>(()=>client.Reply((_,_,_)=>throw new HttpRequestException("private prompt "+ChatTests.FixtureKey),_=>{},_=>{},"chat",ChatTests.FixtureKey,default));
        c["failure_receipts_exclude_provider_text"]=bus.RecentResults.Last().ErrorCode=="connection_failed"&&!bus.RecentResults.Last().Message.Contains(ChatTests.FixtureKey)&&!bus.RecentResults.Last().Message.Contains("private prompt");
        await Throws<ChatWorkException>(()=>client.Reply((text,detail,t)=>{detail("执行状态");throw new InvalidOperationException("HTTP 400 private");},_=>{},_=>{},"chat",ChatTests.FixtureKey,default));c["tool_activity_is_recorded_before_ui_queue"]=client.HadTools;
        string quotaMessage="";try{await client.Reply((_,_,_)=>throw new InvalidOperationException("已达到今日 API 请求上限（2 次）。"),_=>{},_=>{},"chat",ChatTests.FixtureKey,default);}catch(ChatWorkException e){quotaMessage=e.Message;}c["usage_limit_remains_understandable_without_network"]=quotaMessage.Contains("今日 API 请求上限（2 次）");
        var active=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var another=host.Attach(new Speech());var current=another.Reply(async(_,_,_)=>{await active.Task;return Reply();},_=>{},_=>{},"chat",ChatTests.FixtureKey,default);
        c["concurrent_send_is_not_duplicated"]=await Throws<InvalidOperationException>(()=>another.Reply((_,_,_)=>Task.FromResult(Reply()),_=>{},_=>{},"chat",ChatTests.FixtureKey,default));active.TrySetResult();await current;another.Dispose();
        await UiChecks();await CapacityChecks();return c;

        async Task UiChecks()
        {
            var s=new Speech();using(var window=new ChatForm(true,ChatTests.Settings(),()=>ChatTests.FixtureApi(),speech:s)){
                window.Show();await window.PreviewSendAsync("你好");c["window_streaming_reply_uses_registered_path"]=window.PreviewReplyComplete&&window.ConversationReceipts.Any(r=>r.ActionId=="chat_reply"&&r.Status=="succeeded");window.Close();c["window_close_disposes_voice"]=s.Disposed&&window.ConversationReceipts.Any(r=>r.ActionId=="chat_close"&&r.Evidence=="released");
            }
            s=new();using(var window=new ChatForm(true,ChatTests.Settings(),()=>ChatTests.FixtureApi(true),speech:s)){
                window.Show();var sending=window.PreviewSendAsync("你好");await Task.Delay(25);await window.PreviewSendAsync("重复点击");window.PreviewStop();await sending;c["window_stop_cancels_stream_and_restores_controls"]=window.PreviewWasStopped;c["duplicate_window_send_keeps_one_turn"]=window.ConversationTurnCount==1;window.Close();
            }
            s=new();using(var window=new ChatForm(true,ChatTests.Settings(),()=>ChatTests.FixtureApi(true),speech:s)){
                window.Show();var sending=window.PreviewSendAsync("你好");await Task.Delay(25);window.Dispose();await sending;c["dispose_during_stream_finishes_safely"]=s.Disposed;
            }
            s=new();using(var window=new ChatForm(true,ChatTests.Settings(),speech:s)){
                window.Show();s.Start();long old=s.Generation;Task.Run(()=>s.Emit("过期识别",false,old)).GetAwaiter().GetResult();s.StopListening();Application.DoEvents();Task.Run(()=>s.EmitStatus("过期状态",old)).GetAwaiter().GetResult();Application.DoEvents();c["queued_old_status_is_ignored"]=window.PreviewState!="过期状态";c["queued_old_recognition_cannot_fill_draft"]=window.ConversationDraft.Length==0;
                s.Start();long now=s.Generation;s.Emit("一次草稿",false,now);s.Emit("一次草稿",false,now);c["duplicate_recognition_does_not_append_twice"]=window.ConversationDraft=="一次草稿";
                window.Dispose();s.Emit("关闭后的文字",false,now);Application.DoEvents();c["closed_window_detaches_recognition_callback"]=s.Disposed;
            }
            s=new();using(var window=new ChatForm(true,ChatTests.Settings(),()=>ChatTests.FixtureApi(true),speech:s)){
                window.Show();var sending=window.PreviewSendAsync("你好");s.ThrowOnStop=true;window.PreviewStop();await sending;c["voice_stop_failure_does_not_break_chat_cancel"]=window.PreviewWasStopped;s.ThrowOnStop=false;window.Close();
            }
            var origins=new List<string>();int requests=0;s=new();var tools=new AppTools(()=>[],_=>{},leoActions:(_,_,_)=>Task.FromResult("完成")){OriginDispatcher=(a,v,source,t)=>{origins.Add(source);return Task.FromResult("完成");}};
            var settings=ChatTests.Settings();settings.VoiceAutoSend=true;using(var window=new ChatForm(true,settings,()=>{requests++;return ChatTests.FixtureApi();},applicationTools:tools,speech:s)){
                window.Show();s.Start();long now=s.Generation;s.Emit("打开记事本",true,now);s.Emit("打开记事本",true,now);await Task.Delay(25);c["voice_local_command_uses_same_action_source_once"]=origins.Count==1&&origins[0]=="voice"&&requests==0;window.Close();
            }
            int attempts=0;s=new();using(var window=new ChatForm(true,ChatTests.Settings(),()=>new ChatApi(new Handler((_,_)=>{attempts++;return Task.FromResult(attempts==1?new HttpResponseMessage(HttpStatusCode.ServiceUnavailable):new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(ChatTests.Events,Encoding.UTF8,"text/event-stream")});})),speech:s)){
                window.Show();await window.PreviewSendAsync("你好");c["failed_reply_exposes_explicit_retry"]=window.ReplyRetryAvailable&&attempts==1;await window.PreviewSendAsync("你好");c["explicit_retry_does_not_duplicate_auto_request"]=attempts==2&&window.PreviewReplyComplete;window.Close();
            }
            var notes=new NoteStore(null);bus.Registry.Register(new NotesCapability(()=>notes,(_,_,_)=>Task.CompletedTask));var brain=new BrainSession(new BrainCatalog(bus.Registry),bus.ExecuteDetailedAsync,(_,_)=>Task.FromResult(WorkflowVerification.Unknown),()=>new Dictionary<string,string>());
            var controlledTools=new AppTools(()=>[],_=>{},leoActions:(_,_,_)=>Task.FromResult("本地")){Brain=brain};int rounds=0;settings=ChatTests.Settings();settings.VoiceAutoSend=true;s=new();
            using(var window=new ChatForm(true,settings,()=>new ChatApi(new Handler((_,_)=>{int round=++rounds;object message=round==1?new{content="",tool_calls=new[]{new{id="plan",type="function",function=new{name="leo_plan",arguments=JsonSerializer.Serialize(new{steps=new[]{new{action="note_add",value="确认后保存",from_step="",result_property=""}}})}}}}:(object)new{content="等待确认"};return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message,finish_reason=round==1?"tool_calls":"stop"}}}),Encoding.UTF8,"application/json")});})),applicationTools:controlledTools,speech:s)){
                window.Show();s.Start();s.Emit("你好吗",true);for(int i=0;i<100&&window.IsThinking;i++)await Task.Delay(10);
                c["voice_model_actions_keep_q5_preview_confirmation"]=rounds==1&&brain.Pending!=null&&notes.Items.Count==0;
                c["voice_model_plan_preserves_request_origin"]=brain.Pending?.Steps.Single().Request.Source=="brain-plan:voice"&&window.ConversationReceipts.Any(r=>r.ActionId=="chat_reply"&&r.Source=="voice");window.Close();
            }
            using(var pet=new PetForm(new Preferences{ApplicationIndexing=false},true)){pet.Show();foreach(var p in await pet.VerifyConversationRegistry())c["host_"+p.Key]=p.Value;pet.Close();}
        }
        async Task CapacityChecks()
        {
            using var owner=new ConversationHost(new ActionDispatcher(new(),(_,_,_)=>throw new Exception("bypass"),new ExtensionStore(Path.Combine(root,"capacity"))),new(ui),()=>{},true);using var session=owner.Attach(new Speech());var finish=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            for(int i=0;i<16;i++){using var cancellation=new CancellationTokenSource();var task=session.Reply(async(_,_,_)=>{await finish.Task;return Reply();},_=>{},_=>{},"fixture",ChatTests.FixtureKey,cancellation.Token);cancellation.Cancel();await Throws<OperationCanceledException>(async()=>await task);}
            c["ignored_cancel_work_has_fixed_capacity"]=owner.JobCount==16&&await Throws<InvalidOperationException>(()=>session.Reply((_,_,_)=>Task.FromResult(Reply()),_=>{},_=>{},"fixture",ChatTests.FixtureKey,default));finish.TrySetResult();await Task.Delay(40);c["finished_quarantined_jobs_are_released"]=owner.JobCount==0;
        }
    }
}
sealed partial class ChatForm
{
    internal IReadOnlyList<ActionResult> ConversationReceipts=>conversation.Dispatcher.RecentResults;
    internal int ConversationTurnCount=>turns.Count;
    internal string ConversationDraft=>input.Text;
}
sealed partial class PetForm
{
    internal async Task<Dictionary<string,bool>> VerifyConversationRegistry(){var r=await commands.ExecuteDetailedAsync(new("chat_open","","fixture"));var ok=r.Status=="succeeded"&&r.ModuleId=="conversation"&&chat?.Visible==true;chat?.Close();return new(){["open_uses_registered_conversation"]=ok,["internal_operations_not_model_tools"]=!new BrainCatalog(commands.Registry).Read().Any(x=>x.Action.Id is "chat_reply" or "voice_start")};}
}
