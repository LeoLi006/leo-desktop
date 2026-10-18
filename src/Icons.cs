using System.Runtime.InteropServices;
using System.Collections.Concurrent;

namespace LeoLauncher;

sealed partial class AppIcons : IDisposable
{
    readonly Dictionary<string, Bitmap> images = new(StringComparer.OrdinalIgnoreCase);
    readonly object gate = new();
    bool disposed;
    readonly Bitmap fallback = SystemIcons.Application.ToBitmap();
    readonly HashSet<string> requested = new(StringComparer.OrdinalIgnoreCase);
    readonly BlockingCollection<AppEntry> queue = new();
    public event Action? IconReady;
    public int NativeLoadedCount { get; private set; }
    public AppIcons()
    {
        var worker = new Thread(() => {
            foreach (var entry in queue.GetConsumingEnumerable())
            {
                lock (gate) { if (disposed) break; }
                try { LoadNow(entry); IconReady?.Invoke(); } catch { }
            }
        }) { IsBackground = true };
        worker.SetApartmentState(ApartmentState.STA); worker.Start();
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct SHFILEINFO
    {
        public IntPtr Icon; public int Index; public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string Type;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path, uint attributes, out SHFILEINFO info, uint size, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfoW")] static extern IntPtr SHGetFileInfoPidl(IntPtr pidl, uint attributes, out SHFILEINFO info, uint size, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHParseDisplayName(string name, IntPtr context, out IntPtr pidl, uint input, out uint attributes);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    public Bitmap Get(AppEntry entry)
    {
        lock (gate)
        {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (images.TryGetValue(entry.Target, out var cached)) return cached;
        if (requested.Add(entry.Target)) queue.Add(entry);
        return fallback;
        }
    }
    public void LoadNow(AppEntry entry)
    {
        lock (gate) { if (disposed || images.ContainsKey(entry.Target)) return; }
        Bitmap? cached = null;
        bool native = false;
        if (entry.Target == ClipboardTool.Target)
        {
            cached = new Bitmap(32, 32);
            using (var graphics = Graphics.FromImage(cached))
            using (var font = new Font("Segoe UI Emoji", 20)) graphics.DrawString("📋", font, Brushes.Gold, new PointF(-1, -2));
            lock (gate) { if (disposed) cached.Dispose(); else images[entry.Target] = cached; }
            return;
        }
        SHFILEINFO info = default;
        IntPtr pidl = IntPtr.Zero;
        try
        {
            if (entry.Target.StartsWith("shell:"))
            {
                if (SHParseDisplayName(entry.Target, IntPtr.Zero, out pidl, 0, out _) == 0)
                    SHGetFileInfoPidl(pidl, 0, out info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x100 | 0x8);
            }
            else SHGetFileInfo(entry.ExePath.Length > 0 ? entry.ExePath : entry.Target, 0, out info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x100);
            if (info.Icon != IntPtr.Zero)
            {
                using var icon = Icon.FromHandle(info.Icon);
                cached = icon.ToBitmap();
                native = true;
            }
            else cached = SystemIcons.Application.ToBitmap();
        }
        catch { cached = SystemIcons.Application.ToBitmap(); }
        finally { if (info.Icon != IntPtr.Zero) DestroyIcon(info.Icon); if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl); }
        lock (gate)
        {
            if (disposed || images.ContainsKey(entry.Target)) cached.Dispose();
            else { images[entry.Target] = cached; if (native) NativeLoadedCount++; }
        }
    }
    public void Dispose() { lock (gate) { disposed = true; queue.CompleteAdding(); foreach (var bitmap in images.Values) bitmap.Dispose(); images.Clear(); fallback.Dispose(); } }
}

static class Native
{
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
