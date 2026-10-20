using System.Net.Http;
namespace LeoLauncher;

// The timer never downloads packages. All failures remain silent until a manual check.
sealed class UpdateNotifications:IDisposable
{
    readonly System.Windows.Forms.Timer timer=new(){Interval=60_000};
    readonly Func<CancellationToken,Task<LeoRelease?>> check;readonly Action<LeoRelease> notify;
    readonly Func<DateTimeOffset> now;
    readonly OwnedActivityTimer activity;DateTimeOffset next;bool enabled,disposed;
    public UpdateNotifications(Func<CancellationToken,Task<LeoRelease?>> check,Action<LeoRelease> notify,Func<DateTimeOffset>? clock=null)
    {this.check=check;this.notify=notify;now=clock??(()=>DateTimeOffset.Now);activity=new(timer,"update-check","自动检查更新","update-request",CheckCore);}
    public void Configure(bool value){if(disposed||enabled==value)return;enabled=value;if(value){next=now().AddSeconds(30);timer.Interval=30_000;}activity.Configure(value);}
    internal static bool ShouldNotify(LeoRelease? release,Version current,string last)=>release!=null&&release.Version>current&&LeoUpdates.TagVersion(last)!=release.Version;
    internal Task Tick()=>activity.RunNowAsync();
    async Task CheckCore(CancellationToken token)
    {
        if(disposed||!enabled||now()<next)return;next=now().AddDays(1);timer.Interval=60_000;
        try{var found=await check(token);token.ThrowIfCancellationRequested();if(!disposed&&enabled&&found!=null)notify(found);}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){/* Auto checks remain silent; manual diagnostics are separate. */}
    }
    public void Dispose(){if(disposed)return;disposed=true;enabled=false;activity.Dispose();}

}
sealed partial class PetForm
{
    UpdateNotifications? updateNotifications;
    void StartUpdateNotifications(){if(previewMode||updateNotifications!=null)return;updateNotifications=new(async token=>{using var http=LeoUpdates.Client();return await LeoUpdates.Check(http,token);},release=>{
        if(!UpdateNotifications.ShouldNotify(release,LeoUpdates.Current,prefs.LastNotifiedUpdate))return;
        prefs.LastNotifiedUpdate=release.Tag;try{ConfigMigration.Write(Store.FilePath,System.Text.Json.JsonSerializer.Serialize(prefs));}catch(IOException){}catch(UnauthorizedAccessException){}
        Func<Task> open=()=>{if(IsDisposed)return Task.CompletedTask;using var dialog=new UpdateForm();WindowPositions.Prepare(dialog,"update",Bounds,false);dialog.ShowDialog();return Task.CompletedTask;};
        var text="发现 Leo 新版本 "+release.Tag+"，可查看更新。";
        notices.Add(text,"update",unique:"update:"+release.Tag,action:open,label:"查看更新");
        if(!hiddenForFullscreen&&!focus.Active(DateTimeOffset.Now)&&!QuietHours.Load().Contains(DateTime.Now)){feedback??=new LeoToast(()=>Bounds);feedback.Present(new(text,Retry:open,ActionLabel:"查看"));}
    });updateNotifications.Configure(prefs.AutoUpdateNotifications);}
}
