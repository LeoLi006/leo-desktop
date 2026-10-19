namespace LeoLauncher;

sealed class ClientOperation
{
    readonly CancellationTokenSource cancellation=new();readonly Action<LeoNotice> notify;
    string phase="正在准备…";bool cancellable,finished;
    internal ClientOperation(Action<LeoNotice> feedback){notify=feedback;}
    public CancellationToken Token=>cancellation.Token;
    public void Report(string text){lock(this){if(finished||text==phase)return;phase=text;Publish();}}
    void Publish()=>notify(new(phase,Retry:cancellable?()=>{Cancel();return Task.CompletedTask;}:null,Busy:true,ActionLabel:"取消"));
    internal async Task OfferCancel(){await Task.Delay(1800);lock(this){if(finished)return;cancellable=true;Publish();}}
    internal void Finish(){lock(this){if(finished)return;finished=true;cancellation.Dispose();}}
    internal void Cancel(){lock(this)if(!finished)cancellation.Cancel();}
}

sealed class ClientOperationHub:IDisposable
{
    readonly object gate=new();readonly Dictionary<string,Task<string>> pending=new();readonly Dictionary<string,Task> resources=new();
    readonly Action<LeoNotice> notify;long sequence;bool disposed;
    readonly Dictionary<string,CancellationTokenSource> resourceLife=new();readonly HashSet<string> disabledResources=new();
    public void Configure(string resource,bool enabled){CancellationTokenSource? previous=null;lock(gate){if(disposed)return;if(enabled){disabledResources.Remove(resource);return;}disabledResources.Add(resource);resourceLife.Remove(resource,out previous);}if(previous!=null){previous.Cancel();previous.Dispose();}}
    public ClientOperationHub(Action<LeoNotice> feedback){notify=feedback;}
    public void Dispose(){CancellationTokenSource[] sources;lock(gate){if(disposed)return;disposed=true;sources=resourceLife.Values.ToArray();resourceLife.Clear();}foreach(var source in sources){source.Cancel();source.Dispose();}}
    public Task<string> Run(string resource,string request,Func<ClientOperation,Task<string>> work,CancellationToken caller=default)
    {
        void Emit(LeoNotice notice){if(!disposed)notify(notice with{Module=resource=="music"?"music":resource=="clash"?"clash":notice.Module,OperationId=resource+":"+request});}
        Task<string> task;lock(gate){if(disposed)throw new ObjectDisposedException(nameof(ClientOperationHub));if(disabledResources.Contains(resource))throw new InvalidOperationException("此模块已停用。");if(!resourceLife.TryGetValue(resource,out var life)){life=new();resourceLife[resource]=life;}var resourceToken=life.Token;string key=resource+"\n"+request;
            if(!pending.TryGetValue(key,out task!)){
                var completion=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);task=completion.Task;pending[key]=task;
                var previous=resources.GetValueOrDefault(resource)??Task.CompletedTask;resources[resource]=task;
                long id=++sequence;
                _=Task.Run(async()=>{
                    long statusTicket=ModuleActivity.Begin(resource);var context=new ClientOperation(notice=>{ModuleActivity.Set(resource,ModuleState.Waiting,notice.Text);lock(gate)if(id==sequence)Emit(notice);});
                    using var linked=caller.Register(context.Cancel);using var moduleCancellation=resourceToken.Register(context.Cancel);_=context.OfferCancel();
                    try{context.Report("正在等待前一个操作…");try{await previous.WaitAsync(context.Token);}catch(Exception)when(!context.Token.IsCancellationRequested){}
                    context.Token.ThrowIfCancellationRequested();string result=await work(context);context.Token.ThrowIfCancellationRequested();ModuleActivity.Complete(resource,statusTicket,ModuleState.Ready,result);context.Finish();lock(gate){pending.Remove(key);if(id==sequence)Emit(new(result));}completion.TrySetResult(result);}
                    catch(OperationCanceledException){ModuleActivity.Complete(resource,statusTicket,ModuleState.Ready,"已取消，可重新操作。");context.Finish();lock(gate)if(id==sequence)Emit(new("已停止后续操作，已启动的软件保留"));completion.TrySetCanceled();}
                    catch(Exception e){ModuleActivity.Complete(resource,statusTicket,ModuleState.Failed,e.Message);context.Finish();lock(gate){pending.Remove(key);if(id==sequence)Emit(new(e.Message,true,()=>Run(resource,request,work),ActionLabel:"重试"));}completion.TrySetException(e);}
                    finally{lock(gate){if(pending.GetValueOrDefault(key)==task)pending.Remove(key);if(resources.GetValueOrDefault(resource)==task)resources.Remove(resource);}}
                });
            }
        }
        return caller.CanBeCanceled?task.WaitAsync(caller):task;
    }
}

static class ClientOperations
{
    internal static readonly ClientOperationHub Hub=new(notice=>OperationFeedback.Sink?.Invoke(notice));
}
