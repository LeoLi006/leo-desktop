using System.Diagnostics;
namespace LeoLauncher;

// Only short-lived helpers started here are owned; QQ Music and PotPlayer are not registered.
static class OwnedHelpers
{
    static readonly object gate=new();static readonly Dictionary<int,Process> processes=new();static bool stopping;
    public static Process Start(ProcessStartInfo start){lock(gate){if(stopping)throw new OperationCanceledException("Leo正在退出");var process=Process.Start(start)??throw new IOException("无法启动本地辅助进程");processes[process.Id]=process;return process;}}
    public static IDisposable Lease(Process process)=>new Registration(process.Id);
    sealed class Registration(int id):IDisposable{public void Dispose(){lock(gate)processes.Remove(id);}}
    public static void Shutdown(){Process[] owned;lock(gate){stopping=true;owned=processes.Values.ToArray();}foreach(var process in owned)try{if(!process.HasExited){process.Kill(true);process.WaitForExit(1000);}}catch(Exception e)when(e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException){} }
}
