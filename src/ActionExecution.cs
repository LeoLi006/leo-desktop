using System.Text.Json;
namespace LeoLauncher;

sealed partial class ActionDispatcher
{
    sealed class Execution(ActionRequest request,ILeoModule module,CancellationTokenSource cancellation,DateTimeOffset? expires)
    {
        public readonly string Id=Guid.NewGuid().ToString("N");
        public readonly ActionRequest Request=request;
        public readonly ILeoModule Module=module;
        public readonly CancellationTokenSource Cancellation=cancellation;
        public readonly CancellationToken Token=cancellation.Token;
        public readonly DateTimeOffset? Expires=expires;
        public readonly TaskCompletionSource<ActionResult> Completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public System.Threading.Timer? Timeout;
        public int Subscribers;
        public string CancelReason="cancelled";
    }
    readonly CancellationTokenSource lifetime=new();readonly CancellationToken lifetimeToken;
    readonly Preferences prefs;bool disposed,lifetimeDisposed;
    readonly Dictionary<string,Execution> pending=new(StringComparer.Ordinal);
    readonly Queue<ActionReceipt> receipts=new();readonly AsyncLocal<string[]?> ancestry=new();
    readonly Queue<ActionResult> results=new();
    readonly AsyncLocal<ActionRequest?> context=new();
    long requests,executions,coalesced,succeeded,failed,cancelled,timedOut;
    internal TimeSpan? StructuredTimeoutOverride{get;set;}
    internal Func<ActionRequest,ActionRequest>? ApplicationAliases;
    public CapabilityRegistry Registry{get;}
    public ExtensionStore Extensions{get;}
    public string CurrentSource=>context.Value?.Source??"ui";
    public IReadOnlyList<ActionReceipt> Receipts{get{lock(pending)return receipts.ToArray();}}
    public IReadOnlyList<ActionResult> RecentResults{get{lock(pending)return results.ToArray();}}
    public ActionMetrics Metrics{get{lock(pending)return new(requests,executions,coalesced,succeeded,failed,cancelled,timedOut);}}
    public ActionDispatcher(Preferences preferences,Func<string,string,CancellationToken,Task<string>> core,ExtensionStore? extensions=null,ApplicationCapabilityHost? applications=null)
    {
        lifetimeToken=lifetime.Token;prefs=preferences;Extensions=extensions??new ExtensionStore(Path.Combine(Store.Root,"modules"));
        Extensions.Preferences=preferences;Registry=new(preferences,core,()=>Extensions.Load().Select(m=>(ILeoModule)new ExtensionLeoModule(m,prefs,this)).ToArray(),applications);
    }
    public ILeoModule[] Modules=>Registry.Read();
    public async Task<string> ExecuteAsync(ActionRequest request,CancellationToken token=default)=>(await ExecuteDetailedAsync(request,token)).ToLegacy(token);
    public Task<ActionResult> ExecuteStructuredAsync(StructuredActionRequest request,CancellationToken token=default)
    {
        try{return ExecuteDetailedAsync(Registry.Resolve(request.ActionId) is IActionRequestAdapter adapter?adapter.Adapt(request):ApplicationRequestAdapter.Adapt(request),token);}
        catch(Exception e)when(e is ArgumentException or JsonException or InvalidOperationException){var old=new ActionRequest(request.ActionId,"",request.Source,request.RequestId,request.TraceId,request.Deadline);lock(pending)requests++;return Task.FromResult(Record(Failure(Identify(old),"","","invalid_request",e)));}
    }
    ActionRequest Identify(ActionRequest request)=>request with{RequestId=request.RequestId??Guid.NewGuid().ToString("N"),TraceId=request.TraceId??context.Value?.TraceId??Guid.NewGuid().ToString("N")};
    public Task<ActionResult> ExecuteDetailedAsync(ActionRequest original,CancellationToken caller=default)
    {
        var request=Identify(original);lock(pending)requests++;
        try{
            if(request.Action==null||request.Value==null||request.Value.Length>125000||string.IsNullOrWhiteSpace(request.Source)||request.Source.Length>128||request.Source.Any(char.IsControl)||string.IsNullOrWhiteSpace(request.RequestId)||string.IsNullOrWhiteSpace(request.TraceId)||request.RequestId.Length>96||request.TraceId.Length>96||request.RequestId.Any(char.IsControl)||request.TraceId.Any(char.IsControl))throw new ArgumentException("动作参数或请求标识无效");
            if(caller.IsCancellationRequested)return Task.FromResult(Record(Cancelled(request,"","","cancelled")));
            if(request.Deadline<=DateTimeOffset.UtcNow)return Task.FromResult(Record(Cancelled(request,"","","timeout")));
            request=ApplicationAliases?.Invoke(request)??request;
            if(request.Action=="local_app_open"){
                if(!Guid.TryParseExact(request.Value,"N",out _))throw new ArgumentException("应用模块标识无效");
                request=request with{Action="local:"+request.Value,Value=""};
            }else if(request.Action.StartsWith("local:",StringComparison.Ordinal))request=request with{Value=""};
            var module=Registry.Resolve(request.Action);
            if(request.Value.Length>Math.Clamp((module as IStructuredLeoModule)?.MaximumInputLength??16000,1,125000))throw new ArgumentException("动作内容超出模块支持的长度");
            var policy=(module as IStructuredLeoModule)?.Describe(request.Action);
            string key=request.Action+"\n"+request.Value;
            string recursionKey=key;
            if(policy?.Concurrency=="serialized-store")key+="\n"+Guid.NewGuid().ToString("N");
            if(ancestry.Value?.Contains(recursionKey)==true||ancestry.Value?.Length>12)throw new InvalidOperationException("检测到循环调用或过深的组合，请检查自定义步骤。");
            if(!module.Enabled)throw new CapabilityException("module_disabled","此模块已停用，请在设置 → 功能中开启。");
            Execution work;bool start=false;
            lock(pending){
                if(disposed)throw new ObjectDisposedException(nameof(ActionDispatcher));
                if(pending.TryGetValue(key,out work!)&&(!work.Token.IsCancellationRequested||module is not IRestartableQueryModule||policy?.Effect!="read"))coalesced++;
                else{
                    var definition=policy;
                    if(definition!=null&&(definition.Id!=request.Action||definition.ModuleId!=module.Id||definition.Concurrency is not("coalesce-inflight" or "serialized-store")||definition.TimeoutMs is not(>=1 and <=120000)||definition.Retry))throw new CapabilityException("invalid_definition","当前宿主不支持此动作的执行策略");
                    var cancellation=definition!=null?CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken):CancellationTokenSource.CreateLinkedTokenSource(caller,lifetimeToken);
                    TimeSpan? timeout=definition!=null?StructuredTimeoutOverride??TimeSpan.FromMilliseconds(definition.TimeoutMs!.Value):null;
                    DateTimeOffset? expires=timeout is {} duration?DateTimeOffset.UtcNow+duration:null;
                    work=new(request,module,cancellation,expires);if(timeout is {} limit)work.Timeout=new System.Threading.Timer(_=>Cancel(work,"timeout"),null,limit,Timeout.InfiniteTimeSpan);pending[key]=work;executions++;start=true;
                }
                work.Subscribers++;
            }
            // Subscribe before invoking a possibly synchronous implementation.
            var result=Observe(work,request,caller);
            if(start)_=Run(work,key);
            return result;
        }catch(Exception e)when(FeatureFaults.Recoverable(e)){
            string code=e is CapabilityException capability?capability.Code:e is ObjectDisposedException?"dispatcher_closed":e is ArgumentException?"invalid_action":e is InvalidOperationException?"unavailable":"action_failed";
            return Task.FromResult(Record(Failure(request,"","",code,e)));
        }
    }
    async Task<ActionResult> Observe(Execution work,ActionRequest request,CancellationToken caller)
    {
        using var waiting=CancellationTokenSource.CreateLinkedTokenSource(caller,work.Token);
        try{
            if(request.Deadline is {} deadline)waiting.CancelAfter(TimeSpan.FromMilliseconds(Math.Clamp((deadline-DateTimeOffset.UtcNow).TotalMilliseconds,0,int.MaxValue)));
            var result=await work.Completion.Task.WaitAsync(waiting.Token).ConfigureAwait(false);
            return Record(result with{RequestId=request.RequestId!,TraceId=request.TraceId!,Source=request.Source});
        }catch(OperationCanceledException){if(work.Module is ICommittedActionModule committed&&committed.TryCommitted(work.Request,out var saved))return Record(new(request.RequestId!,request.TraceId!,request.Source,request.Action,work.Module.Id,work.Id,"succeeded",saved.Message,null,saved.Data,saved.Evidence){PrivateData=saved.PrivateData,DiagnosticMessage=saved.DiagnosticMessage});string reason=caller.IsCancellationRequested?"cancelled":request.Deadline<=DateTimeOffset.UtcNow?"timeout":Reason(work);return Record(Cancelled(request,work.Module.Id,work.Id,reason));}
        finally{
            bool cancel;lock(pending){work.Subscribers--;cancel=work.Module is IStructuredLeoModule&&work.Subscribers==0&&!work.Completion.Task.IsCompleted;}
            if(cancel)Cancel(work,"cancelled");
        }
    }
    async Task Run(Execution work,string key)
    {
        var previous=ancestry.Value;var previousContext=context.Value;ancestry.Value=(previous??[]).Append(work.Request.Action+"\n"+work.Request.Value).ToArray();context.Value=work.Request;
        long ticket=0;
        try{
            ticket=ModuleActivity.Begin(work.Module.Id);
            work.Token.ThrowIfCancellationRequested();if(!work.Module.Enabled)throw new OperationCanceledException();
            var payload=work.Module is IStructuredLeoModule structured?await structured.ExecuteStructuredAsync(work.Request,work.Token).ConfigureAwait(false):new ActionPayload(await work.Module.ExecuteAsync(work.Request,work.Token));
            if(!(work.Module is ICommittedActionModule committed&&committed.TryCommitted(work.Request,out _)))work.Token.ThrowIfCancellationRequested();ModuleActivity.Complete(work.Module.Id,ticket,ModuleState.Ready,payload.DiagnosticMessage??payload.Message);
            work.Completion.TrySetResult(new(work.Request.RequestId!,work.Request.TraceId!,work.Request.Source,work.Request.Action,work.Module.Id,work.Id,"succeeded",payload.Message,null,payload.Data,payload.Evidence){PrivateData=payload.PrivateData,DiagnosticMessage=payload.DiagnosticMessage});
        }catch(OperationCanceledException e){
            if(work.Module is ICommittedActionModule committed&&committed.TryCommitted(work.Request,out var saved)){
                ModuleActivity.Complete(work.Module.Id,ticket,ModuleState.Ready,saved.DiagnosticMessage??"本地操作已保存。");
                work.Completion.TrySetResult(new(work.Request.RequestId!,work.Request.TraceId!,work.Request.Source,work.Request.Action,work.Module.Id,work.Id,"succeeded",saved.Message,null,saved.Data,saved.Evidence){PrivateData=saved.PrivateData,DiagnosticMessage=saved.DiagnosticMessage});
            }else{ModuleActivity.Complete(work.Module.Id,ticket,ModuleState.Ready,"等待已结束，可确认状态后重新操作。");work.Completion.TrySetResult(Cancelled(work.Request,work.Module.Id,work.Id,e is ApplicationConfigurationChangedException?"configuration_changed":Reason(work)));}
        }
        catch(Exception e)when(FeatureFaults.Recoverable(e)){ModuleActivity.Complete(work.Module.Id,ticket,ModuleState.Failed,e.Message);work.Completion.TrySetResult(Failure(work.Request,work.Module.Id,work.Id,FailureCode(work.Module,e),e));}
        finally{
            ancestry.Value=previous;context.Value=previousContext;
            lock(pending){if(pending.GetValueOrDefault(key)==work)pending.Remove(key);if(disposed&&pending.Count==0)DisposeLifetime();}
            work.Timeout?.Dispose();work.Cancellation.Dispose();
        }
    }
    static string FailureCode(ILeoModule module,Exception error)
    {try{return module is IStructuredLeoModule structured?structured.FailureCode(error):"action_failed";}catch(Exception e)when(FeatureFaults.Recoverable(e)){return "action_failed";}}
    string Reason(Execution work)=>work.CancelReason!="cancelled"?work.CancelReason:work.Expires<=DateTimeOffset.UtcNow?"timeout":"cancelled";
    static ActionResult Failure(ActionRequest r,string module,string execution,string code,Exception e)=>new(r.RequestId!,r.TraceId!,r.Source,r.Action,module,execution,"failed",e.Message,code,null,"unknown"){Failure=e};
    static ActionResult Cancelled(ActionRequest r,string module,string execution,string code)=>new(r.RequestId!,r.TraceId!,r.Source,r.Action,module,execution,"cancelled",code=="timeout"?"等待已超时，请先确认操作状态。":code is "module_disabled" or "configuration_changed"?"模块已停用、移除或更改，已停止后续操作。":r.Action.StartsWith("local:")?"已停止等待；已提交的启动请求不会撤回。":"已停止等待；已提交的操作可能已经生效，请确认结果。",code,null,"unknown");
    ActionResult Record(ActionResult result)
    {
        var recorded=result with{Data=result.PrivateData?null:result.Data,Message=result.DiagnosticMessage??result.Message};
        lock(pending){if(result.Status=="succeeded")succeeded++;else if(result.Status=="cancelled"){cancelled++;if(result.ErrorCode=="timeout")timedOut++;}else failed++;receipts.Enqueue(new(DateTimeOffset.Now,result.ActionId,result.Source,result.Status=="succeeded",recorded.Message));results.Enqueue(recorded);while(receipts.Count>60)receipts.Dequeue();while(results.Count>60)results.Dequeue();}return result;
    }
    static void Cancel(Execution work,string reason){if(!work.Token.IsCancellationRequested||reason!="cancelled")work.CancelReason=reason;try{work.Cancellation.Cancel();}catch(ObjectDisposedException){}catch(Exception e)when(FeatureFaults.Recoverable(e)){FeatureFaults.Report("动作取消",e);}}
    public void CancelDisabled()
    {
        var enabled=Modules.Where(m=>m.Enabled).Select(m=>m.Id).ToHashSet();Execution[] active;lock(pending)active=pending.Values.ToArray();
        foreach(var work in active){if(!enabled.Contains(work.Module.Id))Cancel(work,"module_disabled");else if(work.Module is IStructuredLeoModule structured&&!structured.ConfigurationIsCurrent)Cancel(work,"configuration_changed");}
    }
    void DisposeLifetime(){if(lifetimeDisposed)return;lifetimeDisposed=true;lifetime.Dispose();}
    public void Dispose(){lock(pending){if(disposed)return;disposed=true;foreach(var work in pending.Values)work.CancelReason="dispatcher_closed";}try{lifetime.Cancel();}catch(Exception e)when(FeatureFaults.Recoverable(e)){FeatureFaults.Report("动作退出清理",e);}lock(pending){if(pending.Count==0)DisposeLifetime();}}
}
