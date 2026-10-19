using System.Collections.Concurrent;
namespace LeoLauncher;
sealed record FeatureFailure(string Feature,string ErrorType,DateTimeOffset Time,int Count);
static class FeatureFaults
{
    public static bool IsTestMode;
    static readonly ConcurrentDictionary<string,FeatureFailure> failures=new();static int reporting;static bool started;
    internal static Action<FeatureFailure>? Observer=null;
    public static bool Recoverable(Exception e)=>e is not (OutOfMemoryException or StackOverflowException or AccessViolationException)&&(e is not AggregateException aggregate||aggregate.InnerExceptions.All(Recoverable));
    public static bool Run(string feature,Action work,Action? recover=null){try{work();return true;}catch(OperationCanceledException){return false;}catch(Exception e)when(Recoverable(e)){try{recover?.Invoke();}catch(Exception secondary)when(Recoverable(secondary)){}Report(feature,e);return false;}}
    public static async Task<bool> RunAsync(string feature,Func<Task> work){try{await work();return true;}catch(OperationCanceledException){return false;}catch(Exception e)when(Recoverable(e)){Report(feature,e);return false;}}
    public static void Report(string feature,Exception error,Func<Task>? retry=null){if(Interlocked.Exchange(ref reporting,1)!=0)return;try{var old=failures.GetValueOrDefault(feature);var failure=failures.AddOrUpdate(feature,new FeatureFailure(feature,error.GetType().Name,DateTimeOffset.Now,1),(_,old)=>new(feature,error.GetType().Name,DateTimeOffset.Now,old.Count+1));try{Observer?.Invoke(failure);}catch{}if(old!=null&&failure.Time-old.Time<TimeSpan.FromSeconds(30))return;try{OperationFeedback.Error(feature+"暂时不可用，其他功能可继续使用。",retry,module:null,operationId:"feature:"+feature);}catch{}}finally{Volatile.Write(ref reporting,0);}}
    public static FeatureFailure[] Snapshot()=>failures.Values.OrderByDescending(f=>f.Time).ToArray();
    public static void Timer(System.Windows.Forms.Timer timer,string feature,Action work,Action? recover=null,Action? retry=null,Func<bool>? enabled=null){timer.Tick+=(_,_)=>{if(enabled?.Invoke()==false){timer.Stop();return;}try{work();}catch(Exception e)when(Recoverable(e)){timer.Stop();try{recover?.Invoke();}catch(Exception secondary)when(Recoverable(secondary)){}Report(feature,e,()=>{retry?.Invoke();timer.Start();return Task.CompletedTask;});}};}
    public static void Start(){if(started)return;started=true;Application.ThreadException+=(_,e)=>HandleUi(e.Exception,Form.ActiveForm,false);TaskScheduler.UnobservedTaskException+=(_,e)=>{if(Recoverable(e.Exception)){e.SetObserved();Report("后台任务",e.Exception);}};}
    sealed class WindowFault{public string Type="";public DateTimeOffset Last;public int Count;}
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form,WindowFault> windowFaults=new();
    internal static void HandleUi(Exception error,Form? window,bool quarantine=true){if(!Recoverable(error))throw error;if(!quarantine&&window!=null){var state=windowFaults.GetOrCreateValue(window);var now=DateTimeOffset.Now;state.Count=state.Type==error.GetType().Name&&now-state.Last<TimeSpan.FromSeconds(3)?state.Count+1:1;state.Last=now;state.Type=error.GetType().Name;quarantine=state.Count>=2;}if(quarantine)Run("故障窗口收起",()=>{if(window!=null&&!window.IsDisposed&&!WindowInteraction.IsCompanion(window)){if(window.Modal)window.DialogResult=DialogResult.Cancel;window.Dispose();}});Report("界面操作",error);}
}
