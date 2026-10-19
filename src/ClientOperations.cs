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

sealed class ClientOperationHub
{
    readonly object gate=new();readonly Dictionary<string,Task<string>> pending=new();readonly Dictionary<string,Task> resources=new();
    readonly Action<LeoNotice> notify;long sequence;
    public ClientOperationHub(Action<LeoNotice> feedback){notify=feedback;}
    public Task<string> Run(string resource,string request,Func<ClientOperation,Task<string>> work,CancellationToken caller=default)
    {
        void Emit(LeoNotice notice)=>notify(notice with{Module=resource=="music"?"music":resource=="clash"?"clash":notice.Module,OperationId=resource+":"+request});
        Task<string> task;lock(gate){string key=resource+"\n"+request;
            if(!pending.TryGetValue(key,out task!)){
                var completion=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);task=completion.Task;pending[key]=task;
                var previous=resources.GetValueOrDefault(resource)??Task.CompletedTask;resources[resource]=task;
                long id=++sequence;
                _=Task.Run(async()=>{
                    long statusTicket=ModuleActivity.Begin(resource);var context=new ClientOperation(notice=>{ModuleActivity.Set(resource,ModuleState.Waiting,notice.Text);lock(gate)if(id==sequence)Emit(notice);});
                    using var linked=caller.Register(context.Cancel);_=context.OfferCancel();
                    try{context.Report("正在等待前一个操作…");try{await previous.WaitAsync(context.Token);}catch(Exception)when(!context.Token.IsCancellationRequested){}
                    context.Token.ThrowIfCancellationRequested();string result=await work(context);ModuleActivity.Complete(resource,statusTicket,ModuleState.Ready,result);context.Finish();lock(gate){pending.Remove(key);if(id==sequence)Emit(new(result));}completion.TrySetResult(result);}
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
    public static Task<string> Proxy(bool? desired,CancellationToken token=default)=>Hub.Run("clash",desired?.ToString()??"toggle",async op=>{
        op.Report("正在检查 Clash…");var before=ClashProxy.ReadStatus();bool target=desired??(before.Enabled!=true);
        var after=await Task.Run(()=>ClashProxy.SetEnabled(target,op.Token,op.Report),op.Token);
        if(after.Enabled!=target)throw new IOException("系统代理状态尚未确认");return target?"系统代理已开启":"系统代理已关闭";
    },token);
}
