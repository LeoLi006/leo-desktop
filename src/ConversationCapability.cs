using System.Text.Json;
namespace LeoLauncher;

interface ISpeechIO:IDisposable
{
    int InputDevice{get;set;} bool Listening{get;} bool Finishing{get;} bool Speaking{get;}
    long Generation{get;} bool IsCurrent(long generation);
    event Action<string,bool,long>? Recognized;event Action<string,long>? Status;event Action<long>? Changed;
    void Start();void FinishListening();void StopListening();void Speak(string text);void StopSpeaking();void Stop();
}
sealed class ChatWorkException(string code,string message):InvalidOperationException(message){public string Code=>code;}
sealed class ConversationSession(ISpeechIO speech)
{
    readonly object gate=new();long epoch;bool closed,active;readonly HashSet<long> recognized=new();
    public ISpeechIO Speech=>speech;public bool HadTools{get;private set;}
    public long Generation{get{lock(gate)return epoch;}}
    public bool Current(long generation){lock(gate)return !closed&&active&&epoch==generation;}
    public long Begin(){lock(gate){if(closed)throw new ObjectDisposedException("chat session");if(active)throw new InvalidOperationException("已有回复正在处理");active=true;HadTools=false;return ++epoch;}}
    public void Tool(long generation){lock(gate){if(!closed&&epoch==generation)HadTools=true;}}
    public void End(){lock(gate)active=false;}
    public void Invalidate(){lock(gate)epoch++;}
    public bool Accept(long generation){lock(gate){if(closed||!speech.IsCurrent(generation)||!recognized.Add(generation))return false;while(recognized.Count>32)recognized.Remove(recognized.Min());return true;}}
    public void Close(){lock(gate){if(closed)return;closed=true;epoch++;}speech.Dispose();}
}
sealed class ConversationJob(string session,string action,Func<CancellationToken,Task<ChatReply>> run,Action cancel)
{
    public bool Started;public string Session=>session;public string Action=>action;public Func<CancellationToken,Task<ChatReply>> Run=>run;public Action Cancel=>cancel;
}

// Prompts, audio and credentials stay in bounded in-process jobs. Registry requests contain handles only.
sealed class ConversationHost:IDisposable
{
    readonly object gate=new();readonly Dictionary<string,ConversationSession> sessions=new();readonly Dictionary<string,ConversationJob> jobs=new();
    internal bool IsClosed{get{lock(gate)return disposed;}}
    readonly ActionDispatcher bus;readonly ModuleWindowService ui;readonly Action show;readonly IModuleWindowPort? window;readonly bool owns;bool disposed;
    public ConversationHost(ActionDispatcher bus,ModuleWindowService ui,Action show,bool owns=false,IModuleWindowPort? window=null){this.window=window;this.bus=bus;this.ui=ui;this.show=show;this.owns=owns;bus.Registry.Register(new ConversationCapability(this));}
    public static ConversationHost Standalone(Control owner){var bus=new ActionDispatcher(new(),(_,_,_)=>throw new InvalidOperationException("会话不可绕回旧入口"),new ExtensionStore(Path.Combine(Path.GetTempPath(),"leo-chat-noextensions")));return new(bus,new(owner),()=>{},true);}
    public ConversationClient Attach(ISpeechIO speech){lock(gate){if(disposed)throw new ObjectDisposedException(nameof(ConversationHost));if(sessions.Count>=8)throw new InvalidOperationException("会话数量已达上限");string id=Guid.NewGuid().ToString("N");var session=new ConversationSession(speech);sessions[id]=session;return new(this,bus,id,session);}}
    internal string Add(ConversationJob job){lock(gate){if(disposed||!sessions.ContainsKey(job.Session))throw new ObjectDisposedException("chat session");if(jobs.Count>=16)throw new InvalidOperationException("正在等待前一项会话操作结束");string id=Guid.NewGuid().ToString("N");jobs[id]=job;return id;}}
    internal void Remove(string id){lock(gate)jobs.Remove(id);}
    internal void RemoveUnstarted(string id){lock(gate){if(jobs.TryGetValue(id,out var job)&&!job.Started)jobs.Remove(id);}}
    internal bool Attached(string id){lock(gate)return sessions.ContainsKey(id);}
    internal int JobCount{get{lock(gate)return jobs.Count;}}
    static void CancelJob(ConversationJob job){try{job.Cancel();}catch(ObjectDisposedException){}catch(Exception e)when(FeatureFaults.Recoverable(e)){FeatureFaults.Report("会话取消",new InvalidOperationException("取消回调未正常结束"));}}
    internal void Detach(string id){ConversationSession? session;ConversationJob[] active;lock(gate){sessions.Remove(id,out session);active=jobs.Values.Where(j=>j.Session==id).ToArray();}foreach(var job in active)CancelJob(job);if(session!=null)FeatureFaults.Run("会话释放",session.Close);}
    public void Dispose(){string[] ids;lock(gate){if(disposed)return;disposed=true;ids=sessions.Keys.ToArray();}foreach(var id in ids)Detach(id);if(owns)bus.Dispose();}
    internal async Task<ActionPayload> Execute(ActionRequest r,CancellationToken token)
    {
        if(r.Action=="chat_open"){if(r.Value.Length!=0)throw new ArgumentException("聊天窗口不接受参数");if(IsClosed)throw new ObjectDisposedException("conversation host");await (window?.ShowAsync(token)??ui.ShowAsync(show,token));return new("已打开聊天。",Evidence:"window-shown");}
        if(r.Source.StartsWith("brain-",StringComparison.Ordinal))throw new ArgumentException("会话内部动作不提供给模型");
        if(r.Action is "chat_reply" or "voice_speak"){
            ConversationJob job;lock(gate){job=jobs.GetValueOrDefault(r.Value)??throw new ArgumentException("会话请求已失效");job.Started=true;}
            try{
                if(job.Action!=r.Action)throw new ArgumentException("会话句柄类型不符");
                token.ThrowIfCancellationRequested();ChatReply? result=null;if(r.Action=="voice_speak")await ui.ShowAsync(()=>result=job.Run(token).GetAwaiter().GetResult(),token);else result=await job.Run(token);token.ThrowIfCancellationRequested();
                return new(r.Action=="voice_speak"?"已提交朗读请求。":"回复已完成。",JsonSerializer.SerializeToElement(result),r.Action=="voice_speak"?"accepted":"completed",true,r.Action=="voice_speak"?"已提交朗读请求。":"聊天回复已完成。");
            }finally{Remove(r.Value);}
        }
        ConversationSession session;string id=r.Value;long generation=0;
        if(r.Action=="voice_accept"){using var d=JsonDocument.Parse(id);var p=d.RootElement;if(p.ValueKind!=JsonValueKind.Object||p.EnumerateObject().Count()!=2||!p.TryGetProperty("session",out var s)||!p.TryGetProperty("generation",out var g))throw new ArgumentException("识别回执参数无效");id=s.GetString()!;generation=g.GetInt64();}
        lock(gate)session=sessions.GetValueOrDefault(id)??throw new ObjectDisposedException("chat session");
        ActionPayload? payload=null;await ui.ShowAsync(()=>{
            token.ThrowIfCancellationRequested();
            switch(r.Action){
                case "chat_close":Detach(id);payload=new("会话已关闭。",Evidence:"released",PrivateData:true);return;
                case "chat_cancel":session.Invalidate();ConversationJob[] active;lock(gate)active=jobs.Values.Where(j=>j.Session==id).ToArray();foreach(var job in active)CancelJob(job);session.Speech.Stop();break;
                case "voice_start":session.Speech.Start();break;
                case "voice_finish":session.Speech.FinishListening();break;
                case "voice_stop":session.Speech.Stop();break;
                case "voice_stop_listening":session.Speech.StopListening();break;
                case "voice_stop_speaking":session.Speech.StopSpeaking();break;
                case "voice_accept":payload=new("已核验识别回执。",JsonSerializer.SerializeToElement(session.Accept(generation)),"snapshot",true);return;
                default:throw new ArgumentException("会话动作无效");
            }
            payload=new("会话操作已处理。",Evidence:"accepted",PrivateData:true);
        },token);return payload!;
    }
}
sealed class ConversationCapability(ConversationHost host):IStructuredLeoModule,IActionRequestAdapter,IBrainContributor,IModuleContractSource,IContractInputBoundary
{
    public ModuleContract DescribeModule()=>ModuleContracts.Conversation(this);
    public DependencyState[] ReadDependencies()=>[new("conversation-host",host.IsClosed?"missing":"ready"),new("model-endpoint","unknown"),new("speech-input","unknown"),new("speech-output","unknown")];
    public bool AcceptsStructuredAction(string action)=>action=="chat_open";

    public string Id=>"conversation";public string Name=>"聊天与语音";public string Version=>"1";public bool Enabled=>true;public bool ConfigurationIsCurrent=>true;
    public IReadOnlyList<string> Actions=>["chat_open","chat_reply","chat_close","chat_cancel","voice_start","voice_finish","voice_stop","voice_stop_listening","voice_stop_speaking","voice_accept","voice_speak"];
    public IReadOnlyList<BrainAction> BrainActions=>[new("chat_open","打开聊天窗口","none",Check:v=>{if(v.Length!=0)throw new ArgumentException("聊天窗口不接受参数");})];
    public CapabilityDefinition Describe(string action)=>new(action,Id,"in-process","private-session-handle",action=="chat_open"?"window":"session","coalesce-inflight",action=="chat_reply"?120000:15000);
    public string FailureCode(Exception e)=>e is ChatWorkException w?w.Code:e is ObjectDisposedException?"session_closed":e is ArgumentException or JsonException?"invalid_request":"conversation_failed";
    public ActionRequest Adapt(StructuredActionRequest r){if(r.Protocol!="leo-action/1"||!AcceptsStructuredAction(r.ActionId)||r.Parameters.ValueKind!=JsonValueKind.Object||r.Parameters.EnumerateObject().Any())throw new ArgumentException("会话内部句柄不接受外部结构化参数");return new(r.ActionId,"",r.Source,r.RequestId,r.TraceId,r.Deadline);}
    public async Task<ActionPayload> ExecuteStructuredAsync(ActionRequest r,CancellationToken t){try{return await host.Execute(r,t);}catch(OperationCanceledException){throw;}catch(ChatWorkException){throw;}catch(ObjectDisposedException){throw new ChatWorkException("session_closed","会话已关闭，请重新打开");}catch(ArgumentException){throw new ChatWorkException("invalid_request","会话参数无效或请求已过期");}catch(Exception e)when(FeatureFaults.Recoverable(e)){throw new ChatWorkException("conversation_failed","会话操作未完成，请检查本地模型、麦克风或连接设置");}}
    public async Task<string> ExecuteAsync(ActionRequest r,CancellationToken t)=>(await ExecuteStructuredAsync(r,t)).Message;
}
sealed class ConversationClient(ConversationHost host,ActionDispatcher bus,string id,ConversationSession session):IDisposable
{
    public long Generation=>session.Generation;public bool Current(long generation)=>session.Current(generation);public bool HadTools=>session.HadTools;
    internal ActionDispatcher Dispatcher=>bus;
    public void Voice(string action){var r=bus.ExecuteDetailedAsync(new(action,id,"voice-ui")).GetAwaiter().GetResult();r.ToLegacy(default);}
    public bool AcceptVoice(long generation){var r=bus.ExecuteDetailedAsync(new("voice_accept",JsonSerializer.Serialize(new{session=id,generation}),"voice-ui")).GetAwaiter().GetResult();r.ToLegacy(default);return r.Data!.Value.GetBoolean();}
    public void Cancel(){Voice("chat_cancel");}
    public void Speak(string text){string ticket=host.Add(new(id,"voice_speak",_=>{session.Speech.Speak(text);return Task.FromResult(new ChatReply("",false,false));},()=>{}));try{bus.ExecuteDetailedAsync(new("voice_speak",ticket,"voice-ui")).GetAwaiter().GetResult().ToLegacy(default);}finally{host.Remove(ticket);}}
    public async Task<ChatReply> Reply(Func<Action<string>,Action<string>,CancellationToken,Task<ChatReply>> run,Action<string> text,Action<string> detail,string source,string key,CancellationToken token)
    {
        long generation=session.Begin();using var request=CancellationTokenSource.CreateLinkedTokenSource(token);string? ticket=null;
        try{
            ticket=host.Add(new(id,"chat_reply",async moduleToken=>{
                using var linked=CancellationTokenSource.CreateLinkedTokenSource(request.Token,moduleToken);
                try{var result=await run(delta=>{if(session.Current(generation)&&!linked.IsCancellationRequested)text(delta);},value=>{if(session.Current(generation)&&!linked.IsCancellationRequested){session.Tool(generation);detail(value);}},linked.Token);linked.Token.ThrowIfCancellationRequested();if(!session.Current(generation))throw new OperationCanceledException(linked.Token);return result;}
                catch(OperationCanceledException){throw;}
                catch(Exception e)when(FeatureFaults.Recoverable(e)){
                    string code=e is System.Net.Http.HttpRequestException?"connection_failed":"reply_failed";var status=System.Text.RegularExpressions.Regex.Match(e.Message,@"^HTTP (\d{3})");
                    var quota=System.Text.RegularExpressions.Regex.Match(e.Message,@"^已达到今日 API 请求上限（(\d+) 次）");
                    string message=quota.Success?"已达到今日 API 请求上限（"+quota.Groups[1].Value+" 次）。本地功能仍可使用。":e.Message.StartsWith("API 限额配置损坏")?"API 限额配置无法读取，请在用量页检查。":e.Message.StartsWith("API 用量记录无法读取")?"API 用量记录无法读取，已暂停调用，请检查用量页。":status.Success?"HTTP "+status.Groups[1].Value+" · 接口未接受请求，请检查连接设置":code=="connection_failed"?"连接失败，请检查中转站地址、网络与系统代理":"回复未完成，请重试或检查连接设置";
                    if(key.Length>0)message=message.Replace(key,"[已隐藏]",StringComparison.Ordinal);throw new ChatWorkException(code,message);
                }
            },()=>request.Cancel()));
            var result=await bus.ExecuteDetailedAsync(new("chat_reply",ticket,source),request.Token);result.ToLegacy(request.Token);return result.Data!.Value.Deserialize<ChatReply>()!;
        }finally{if(ticket!=null)host.RemoveUnstarted(ticket);session.End();}
    }
    bool disposed;
    public void Dispose(){if(disposed)return;disposed=true;if(!host.Attached(id))return;try{bus.ExecuteDetailedAsync(new("chat_close",id,"chat-ui")).GetAwaiter().GetResult().ToLegacy(default);}catch(Exception e)when(FeatureFaults.Recoverable(e)){host.Detach(id);}}
}
sealed partial class PetForm
{
    ConversationHost conversations=null!;
    void RegisterConversationCapability()=>conversations=new(commands,new ModuleWindowService(this),ShowChatCore,window:presentation.For(ModuleWindowTarget.Chat));
}
