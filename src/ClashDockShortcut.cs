using System.Diagnostics;
namespace LeoLauncher;

// Local launch shortcut only. ClashDock owns single-instance activation and its UI.
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
    public static void Open(IWin32Window owner)
    {
        string path = File.Exists(PathFile) ? File.ReadAllText(PathFile).Trim() : "";
        if (!File.Exists(path))
        {
            using var picker = new OpenFileDialog { Title = "选择 ClashDock（仅首次需要）", Filter = "ClashDock|ClashDock.exe", CheckFileExists = true };
            if (picker.ShowDialog(owner) != DialogResult.OK) return;
            path = picker.FileName;
            _ = StartInfo(path);
            File.WriteAllText(PathFile, path);
        }
        using var process = Process.Start(StartInfo(path));
        if (process == null) throw new IOException("Windows 未能启动 ClashDock。");
    }
}