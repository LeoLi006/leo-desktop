using System.Runtime.InteropServices;
using System.Text;
namespace LeoLauncher;

static class FullscreenMonitor
{
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder text, int length);
    public static bool CoversMonitor(Rectangle window, Rectangle monitor, string windowClass, bool ownProcess)
        => !ownProcess && windowClass is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
        && window.Left <= monitor.Left + 2 && window.Top <= monitor.Top + 2 && window.Right >= monitor.Right - 2 && window.Bottom >= monitor.Bottom - 2;
    public static bool IsFullscreenOn(Screen petScreen)
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || !IsWindowVisible(window) || IsIconic(window)) return false;
        GetWindowThreadProcessId(window, out var pid);
        if (pid == Environment.ProcessId || Screen.FromHandle(window).DeviceName != petScreen.DeviceName) return false;
        if (!GetWindowRect(window, out var r)) return false;
        var cls = new StringBuilder(160); GetClassName(window, cls, cls.Capacity);
        return CoversMonitor(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom), petScreen.Bounds, cls.ToString(), false);
    }
}
