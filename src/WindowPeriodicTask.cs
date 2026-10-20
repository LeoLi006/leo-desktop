namespace LeoLauncher;

// UI-thread scoped service. The caller keeps its existing visibility policy;
// the service owns timer registration, activity accounting and final release.
interface IWindowPeriodicTask:IPeriodicTask;
sealed class WindowPeriodicTask:IWindowPeriodicTask
{
    readonly Form window;readonly OwnedActivityTimer activity;bool disposed;
    public bool Enabled=>!disposed&&activity.Enabled;
    public bool Running=>activity.Running;
    public WindowPeriodicTask(Form window,System.Windows.Forms.Timer timer,string module,string feature,string kind,Func<CancellationToken,Task> work)
    {
        this.window=window;
        activity=new(timer,module,feature,kind,async token=>{
            token.ThrowIfCancellationRequested();
            if(window.IsDisposed||window.Disposing)throw new OperationCanceledException(token);
            await work(token);
            token.ThrowIfCancellationRequested();
        });
        window.Disposed+=Closed;
    }
    public void Configure(bool enabled){if(!disposed)activity.Configure(enabled&&!window.IsDisposed&&!window.Disposing);}
    public Task RunNowAsync()=>disposed?Task.CompletedTask:activity.RunNowAsync();
    public void Retry(){if(!disposed&&!window.IsDisposed&&!window.Disposing)activity.Retry();}
    void Closed(object? sender,EventArgs e)=>Dispose();
    public void Dispose(){if(disposed)return;disposed=true;window.Disposed-=Closed;activity.Dispose();}
}
