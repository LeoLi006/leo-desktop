using System.Runtime.CompilerServices;
namespace LeoLauncher;

sealed record HostServiceSnapshot(bool Closed,int ConfigurationScopes,int DependencyScopes,int WindowTargets,int AvailableWindows,
    int PendingWindows,int PendingFeedback,int InvalidEntries,int ConfigurationFaults,int DependencyFaults,int ExecutionFaults,int CheckFailures);
sealed class HostServiceDiagnostics(Preferences prefs,CapabilityRegistry registry,ModulePresentationService presentation)
{
    static readonly ConditionalWeakTable<Preferences,HostServiceDiagnostics> hosts=new();
    public static void Bind(Preferences prefs,CapabilityRegistry registry,ModulePresentationService presentation){hosts.Remove(prefs);hosts.Add(prefs,new(prefs,registry,presentation));}
    public static string Summary(Preferences prefs)=>hosts.TryGetValue(prefs,out var host)?Format(host.Read()):"宿主服务：未连接 · 配置作用域 0 · 窗口端口 0";
    internal HostServiceSnapshot Read()
    {
        var window=presentation.Snapshot();if(window.Closed)return new(true,0,0,window.Targets,0,window.Windows,window.Feedback,0,0,0,0,0);
        int scopes=0,invalid=0,failures=0;
        try{scopes=registry.HostServiceScopes();invalid=(prefs.ApplicationModules?.Count??0)-EntryContributions.ReadAvailable(prefs).Length+EntryContributions.ReadBuiltins(prefs).Count(e=>e.Fault.Length>0);}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){failures++;}
        var faults=registry.ApplicationFaults.Read().Concat(registry.ClientFaults.Read()).ToArray();
        return new(false,scopes,scopes,window.Targets,window.Available,window.Windows,window.Feedback,Math.Max(0,invalid),
            faults.Count(f=>f.Phase=="Configuration"),faults.Count(f=>f.Phase=="Dependencies"),faults.Count(f=>f.Phase=="Execution"),failures);
    }
    internal static string Format(HostServiceSnapshot snapshot)=>
        "宿主服务："+(snapshot.Closed?"已关闭":"运行中")+" · 配置作用域 "+snapshot.ConfigurationScopes+" · 依赖作用域 "+snapshot.DependencyScopes+
        "\n窗口端口 "+snapshot.WindowTargets+" · 可用 "+snapshot.AvailableWindows+" · 在途窗口 "+snapshot.PendingWindows+" · 在途反馈 "+snapshot.PendingFeedback+
        "\n异常入口 "+snapshot.InvalidEntries+" · 核验失败 "+snapshot.CheckFailures+
        "\n内存故障记录：配置 "+snapshot.ConfigurationFaults+" · 依赖 "+snapshot.DependencyFaults+" · 执行 "+snapshot.ExecutionFaults;
}
