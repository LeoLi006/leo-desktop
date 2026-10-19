using System.Text.Json;
namespace LeoLauncher;

// Publish a new snapshot only after the atomic disk replacement succeeds.
sealed class ModuleJsonData<T>
{
    readonly object gate=new();readonly string? path;readonly Func<T,T> copy;
    readonly Action<string,string> write;T value;
    public ModuleJsonData(string? path,T initial,Func<string,T> validate,Func<T,T> copy,Action<string,string>? write=null)
    {this.path=path;this.copy=copy;this.write=write??ConfigMigration.Write;value=path!=null&&(File.Exists(path)||File.Exists(path+".bak"))?AtomicJson.Read(path,validate):initial;}
    public R Read<R>(Func<T,R> read){lock(gate)return read(value);}
    public R Change<R>(Func<T,R> edit,CancellationToken token=default)
    {
        lock(gate){token.ThrowIfCancellationRequested();var next=copy(value);var result=edit(next);token.ThrowIfCancellationRequested();if(path!=null)write(path,JsonSerializer.Serialize(next));value=next;return result;}
    }
}

sealed class ModuleWindowService(Control owner)
{
    readonly int ownerThread=Environment.CurrentManagedThreadId;
    public async Task ShowAsync(Action show,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(owner.IsDisposed||owner.Disposing)throw new ObjectDisposedException("module window host");
        if(Environment.CurrentManagedThreadId==ownerThread){show();return;}
        if(!owner.IsHandleCreated)throw new InvalidOperationException("窗口宿主尚未就绪");
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration=token.Register(()=>done.TrySetCanceled(token));
        EventHandler closed=(_,_)=>done.TrySetException(new ObjectDisposedException("module window host"));
        owner.Disposed+=closed;
        try{owner.BeginInvoke((Action)(()=>{try{if(!done.Task.IsCompleted){token.ThrowIfCancellationRequested();if(owner.IsDisposed||owner.Disposing)throw new ObjectDisposedException("module window host");show();done.TrySetResult();}}catch(Exception e)when(FeatureFaults.Recoverable(e)){done.TrySetException(e);}}));await done.Task.ConfigureAwait(false);}
        finally{owner.Disposed-=closed;}
    }
}

sealed class ModuleFeedbackService(Action<string> success,Action<string> failure)
{
    public void Report(ActionResult result,bool acknowledge=false)
    {if(result.Status=="failed")failure(result.Message);else if(acknowledge&&result.Status=="succeeded")success(result.Message);}
}
