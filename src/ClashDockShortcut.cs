using System.Diagnostics;
namespace LeoLauncher;

// Optional local dependency. Only explicit configuration opens a picker.
static class ClashDockShortcut
{
    internal static string PathFile => Path.Combine(AppContext.BaseDirectory, "leo-clashdock-path.txt");
    internal static ProcessStartInfo StartInfo(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable) ||
            !string.Equals(Path.GetFileName(executable), "ClashDock.exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("请选择有效的 ClashDock.exe。");
        return new ProcessStartInfo(executable) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(executable)! };
    }
    internal static string ReadPath()
    {
        if(!File.Exists(PathFile))return "";
        if(new FileInfo(PathFile).Length>8192)throw new IOException("ClashDock 路径配置无效。");
        return File.ReadAllText(PathFile).Trim();
    }
    internal static bool TryOpen(Func<string> read,Func<ProcessStartInfo,bool> launch,Action<string> notify)
    {
        ProcessStartInfo info;
        try{info=StartInfo(read());}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){notify(UiLanguage.T("ClashDock 未配置或不可用；可右键按钮设置路径。"));return false;}
        try{if(!launch(info))throw new IOException("Windows 未能启动 ClashDock。");return true;}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){notify(UiLanguage.T("无法打开 ClashDock：")+UiLanguage.T(e.Message));return false;}
    }
    public static void Open(IWin32Window owner)=>TryOpen(ReadPath,info=>{using var process=Process.Start(info);return process!=null;},OperationFeedback.Show);
    public static void Configure(IWin32Window owner)
    {
        using var picker=new OpenFileDialog{Title=UiLanguage.T("选择 ClashDock（仅首次需要）"),Filter="ClashDock|ClashDock.exe",CheckFileExists=true};
        if(picker.ShowDialog(owner)!=DialogResult.OK)return;
        _=StartInfo(picker.FileName);File.WriteAllText(PathFile,picker.FileName);
    }
}
