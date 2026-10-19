using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;

sealed class ApplicationCapabilityHost(
    Func<LocalApplication,CancellationToken,Task<LocalApplication>> prepare,
    Func<ProcessStartInfo,CancellationToken,Task<bool>> start,
    Action<LocalApplication>? remember=null,
    bool preview=false)
{
    public async Task<ActionPayload> Execute(LocalApplication original,Func<LocalApplication?> current,CancellationToken token,Action<LocalApplication>? prepared=null)
    {
        token.ThrowIfCancellationRequested();EnsureCurrent(original,current);
        var app=await prepare(original,token);token.ThrowIfCancellationRequested();EnsureCurrent(app,current);
        prepared?.Invoke(app);
        var info=LocalApplications.Start(app);token.ThrowIfCancellationRequested();
        if(preview)return new("示例模式，不启动应用。",JsonSerializer.SerializeToElement(new{applicationId=app.Id,accepted=false}),"unknown");
        bool accepted=await start(info,token);token.ThrowIfCancellationRequested();EnsureCurrent(app,current);
        if(!accepted)throw new IOException("系统未接收应用启动请求");
        // History is ancillary: a failed history write must never ask to launch again.
        try{remember?.Invoke(app);}catch(Exception e)when(FeatureFaults.Recoverable(e)){FeatureFaults.Report("应用启动历史",e);}
        return new("已启动 "+app.Name,JsonSerializer.SerializeToElement(new{applicationId=app.Id,accepted=true}),"accepted");
    }
    static void EnsureCurrent(LocalApplication expected,Func<LocalApplication?> read)
    {
        var latest=read();if(latest==null||!latest.Enabled||latest!=expected)throw new ApplicationConfigurationChangedException();
    }
    // Shell associations can accept a request without returning a new process handle.
    public static Task<bool> StartProcess(ProcessStartInfo info,CancellationToken token)=>Task.Run(()=>{token.ThrowIfCancellationRequested();try{using var process=Process.Start(info);return true;}catch(Win32Exception e)when(e.NativeErrorCode==1223){throw new OperationCanceledException("已取消管理员启动",e,token);}},token);
}
