namespace LeoLauncher;

enum ModuleWindowTarget { Applications,Football,Music,Media,Todos,Notes,QuickNote,Chat,Clipboard }
sealed record ModuleWindowRequest(ModuleWindowTarget Target,string Query="",bool Web=false,string View="",QuickIntent? PlannerIntent=null);
interface IModuleWindowPort { Task ShowAsync(CancellationToken token); }
sealed class ModulePresentationService(ModuleWindowService ui,Func<ModuleWindowTarget,bool> enabled,
    Action<ModuleWindowRequest> open,Action<LeoNotice> publish,Action<string> dismiss)
{
    public const string Protocol="leo-host-presentation/1";
    readonly object gate=new();readonly long[] generations=new long[Enum.GetValues<ModuleWindowTarget>().Length];readonly bool[] states=Enumerable.Repeat(true,Enum.GetValues<ModuleWindowTarget>().Length).ToArray();
    static int Index(ModuleWindowTarget target)=>Enum.IsDefined(target)?(int)target:throw new ArgumentException("未知窗口目标");
    public void Configure(){foreach(var target in Enum.GetValues<ModuleWindowTarget>()){bool state=enabled(target);lock(gate){int i=Index(target);if(states[i]!=state){states[i]=state;generations[i]++;}}}}
    public void Invalidate(ModuleWindowTarget target){lock(gate)generations[Index(target)]++;}
    internal long Capture(ModuleWindowTarget target){lock(gate)return generations[Index(target)];}
    internal bool Current(ModuleWindowTarget target,long generation){lock(gate)return generations[Index(target)]==generation&&enabled(target);}
    public IModuleWindowPort For(ModuleWindowTarget target)=>new Port(this,target);
    sealed class Port(ModulePresentationService owner,ModuleWindowTarget target):IModuleWindowPort
    {public Task ShowAsync(CancellationToken token)=>owner.ShowAsync(new(target),token);}
    public Task ShowAsync(ModuleWindowRequest request,CancellationToken token)
    {
        Index(request.Target);
        bool notes=request.Target==ModuleWindowTarget.Notes,applications=request.Target==ModuleWindowTarget.Applications,todos=request.Target==ModuleWindowTarget.Todos;
        if(request.Query.Length>(notes?20000:2000)||!applications&&request.Web||!applications&&!notes&&request.Query.Length>0||
            !todos&&(request.View.Length>0||request.PlannerIntent!=null)||todos&&request.View is not("" or "day" or "week")||
            request.PlannerIntent is {} intent&&(intent.Kind is not(QuickKind.Todos or QuickKind.AddTodo or QuickKind.CompleteTodo)||intent.Value.Length>20000))throw new ArgumentException("窗口请求参数无效");
        long generation=Capture(request.Target);
        return ui.ShowAsync(()=>open(request),token,()=>Current(request.Target,generation));
    }
    public ModulePresentationFeedback Feedback(ModuleWindowTarget target,CancellationToken token)=>new(this,target,Capture(target),token);
    internal async Task Emit(ModuleWindowTarget target,long generation,LeoNotice notice,CancellationToken token,Func<bool>? active=null)
    {
        try{await ui.ShowAsync(()=>publish(notice),token,()=>Current(target,generation)&&(active?.Invoke()??true));}
        catch(OperationCanceledException){}catch(ObjectDisposedException){}catch(Exception e)when(FeatureFaults.Recoverable(e)){System.Diagnostics.Trace.WriteLine("Module feedback: "+e.GetType().Name);}
    }
    internal async Task Dismiss(string operation)
    {
        try{await ui.ShowAsync(()=>dismiss(operation),CancellationToken.None);}catch(Exception e)when(FeatureFaults.Recoverable(e)){/* Cleanup cannot fail a completed action. */}
    }
}
sealed class ModulePresentationFeedback(ModulePresentationService host,ModuleWindowTarget target,long generation,CancellationToken token)
{
    public Task ReportAsync(ActionResult result,bool acknowledge=false)
    {
        string module=target switch{ModuleWindowTarget.Applications=>"core",ModuleWindowTarget.Football=>"football-data",ModuleWindowTarget.Music=>"music",ModuleWindowTarget.Media=>"media",ModuleWindowTarget.Todos=>"todos",ModuleWindowTarget.Chat=>"conversation",ModuleWindowTarget.Clipboard=>"clipboard",_=>"notes"};
        if(result.ModuleId!=module&&!(target==ModuleWindowTarget.Applications&&result.ModuleId.StartsWith("local:",StringComparison.Ordinal)))throw new ArgumentException("反馈归属不一致");
        if(result.Status=="failed")return host.Emit(target,generation,new(result.Message,true,Module:module,OperationId:result.ExecutionId),token);
        if(acknowledge&&result.Status=="succeeded")return host.Emit(target,generation,new(result.Message,Module:module,OperationId:result.ExecutionId),token);
        return Task.CompletedTask;
    }
    internal Task ProgressAsync(LeoNotice notice,Func<bool> active)=>host.Emit(target,generation,notice,token,active);
    internal Task DismissAsync(string operation)=>host.Dismiss(operation);
}
sealed class ModuleProgressLease(ModulePresentationFeedback feedback,string module):IDisposable
{
    string? operation;volatile bool disposed;
    internal void Publish(LeoNotice notice){if(disposed)return;operation=notice.OperationId;_ = SafePublish(notice with{Module=module});}
    async Task SafePublish(LeoNotice notice){try{await feedback.ProgressAsync(notice,()=>!disposed);}catch(Exception e)when(FeatureFaults.Recoverable(e)){/* Feedback failure cannot stop client work. */}}
    public void Dispose(){disposed=true;if(operation!=null)_=feedback.DismissAsync(operation);}
}
sealed partial class PetForm
{
    readonly ModulePresentationService presentation;
    void OpenPresentation(ModuleWindowRequest request)
    {
        switch(request.Target){
            case ModuleWindowTarget.Applications:ShowLauncher();if(request.Web)launcher.PreviewTyping("网页搜索 ");else if(request.Query.Length>0)launcher.PreviewTyping(request.Query);break;
            case ModuleWindowTarget.Football:launcher.OpenFootballFromAssistant();break;
            case ModuleWindowTarget.Music:launcher.OpenMusicCore();break;
            case ModuleWindowTarget.Media:ShowMediaCore();break;
            case ModuleWindowTarget.Todos:ShowTodosCore(request.PlannerIntent);if(request.View.Length>0)todoWindow!.View.SetWeek(request.View=="week");break;
            case ModuleWindowTarget.Notes:ShowNotesCore(request.Query);break;
            case ModuleWindowTarget.QuickNote:ShowQuickNoteCore();break;
            case ModuleWindowTarget.Chat:ShowChatCore();break;
            case ModuleWindowTarget.Clipboard:launcher.OpenClipboardCore();break;
        }
    }
    void DismissPresentationFeedback(string operation){if(feedback?.Matches(new("",OperationId:operation))==true&&feedback.IsBusy)feedback.Hide();}
}
