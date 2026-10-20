using System.Runtime.CompilerServices;
namespace LeoLauncher;

// Value descriptions only: the window owns its fonts; WinForms owns DPI scaling.
sealed record ModuleThemeFont(string Family,float Points,FontStyle Style);
sealed record ModuleThemeSnapshot(int Revision,string Skin,string Name,string Team,string Motto,
    Color Canvas,Color Surface,Color Raised,Color Text,Color Muted,Color Accent,
    int Radius,double Opacity,ModuleThemeFont Font,double LogicalScale);

// A pilot window uses this binding instead of the global open-window refresh.
// No timers, shared Fonts, controls or mutable user configuration in the snapshot.
sealed class ModuleThemeBinding:IDisposable
{
    static readonly ConditionalWeakTable<Form,ModuleThemeBinding> bindings=new();
    readonly Form window;readonly ModuleWindowService ui;
    CancellationTokenSource? subscription;CancellationToken subscriptionToken;Action<ModuleThemeSnapshot>? listener;long generation;bool disposed;
    public ModuleThemeSnapshot Current{get;private set;}
    internal static bool Managed(Form window)=>bindings.TryGetValue(window,out _);
    internal bool Subscribed=>subscription!=null;
    public ModuleThemeBinding(Form window)
    {
        this.window=window;ui=new(window);Current=UiTheme.Snapshot();
        UiTheme.Attach(window);bindings.Add(window,this);
        window.VisibleChanged+=Visibility;window.Disposed+=Closed;
        Visibility(null,EventArgs.Empty);
    }
    void Visibility(object? sender,EventArgs e)
    {
        Stop();if(disposed||!window.Visible||window.IsDisposed||window.Disposing)return;
        subscription=new();subscriptionToken=subscription.Token;var token=subscriptionToken;
        listener=snapshot=>Enqueue(snapshot,token);UiTheme.Changed+=listener;Apply(UiTheme.Snapshot());
    }
    void Apply(ModuleThemeSnapshot snapshot){Current=snapshot;UiTheme.Attach(window);}
    void Changed(ModuleThemeSnapshot snapshot)
    {
        if(subscription==null)return;Enqueue(snapshot,subscriptionToken);
    }
    void Enqueue(ModuleThemeSnapshot snapshot,CancellationToken token)
    {
        if(disposed||token.IsCancellationRequested)return;
        long version=Interlocked.Increment(ref generation);
        _=Deliver(snapshot,token,version);
    }
    async Task Deliver(ModuleThemeSnapshot snapshot,CancellationToken token,long version)
    {
        try{await ui.ShowAsync(()=>Apply(snapshot),token,()=>!disposed&&version==Interlocked.Read(ref generation)&&window.Visible&&snapshot.Revision==UiTheme.Snapshot().Revision);}
        catch(OperationCanceledException){}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){FeatureFaults.Run("主题投递",()=>throw e);}
    }
    void Stop(){Interlocked.Increment(ref generation);if(listener!=null)UiTheme.Changed-=listener;listener=null;var old=subscription;subscription=null;old?.Cancel();old?.Dispose();}
    void Closed(object? sender,EventArgs e)=>Dispose();
    public void Dispose(){if(disposed)return;disposed=true;Stop();window.VisibleChanged-=Visibility;window.Disposed-=Closed;bindings.Remove(window);}
}
