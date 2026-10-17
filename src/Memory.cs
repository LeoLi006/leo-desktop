using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text;

namespace LeoLauncher;

sealed class Usage
{
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public DateTimeOffset LastUtc { get; set; }
    public int Count { get; set; }
    public Dictionary<string, int> Contexts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, int> Hours { get; set; } = new();
}

sealed class Memory
{
    readonly string? file;
    public List<Usage> Entries { get; private set; } = new();
    public string? LastSaveError { get; private set; }
    public Memory(string? path)
    {
        file = path;
        if (path == null) return;
        try { Entries = JsonSerializer.Deserialize<List<Usage>>(File.ReadAllText(path)) ?? new(); }
        catch { Entries = new(); }
    }
    public Usage? Find(AppEntry app) => Entries.FirstOrDefault(x => x.Target.Equals(app.Target, StringComparison.OrdinalIgnoreCase));
    public void Record(AppEntry app, string context, DateTimeOffset now, bool save = true)
    {
        var item = Find(app);
        if (item == null) { item = new() { Target = app.Target, Name = app.Name }; Entries.Add(item); }
        var countVisit = now - item.LastUtc > TimeSpan.FromSeconds(3);
        item.Name = app.Name;
        if (now > item.LastUtc) item.LastUtc = now;
        if (countVisit)
        {
            item.Count++;
            if (!string.IsNullOrEmpty(context)) item.Contexts[context] = item.Contexts.GetValueOrDefault(context) + 1;
            var hour = now.ToLocalTime().Hour / 4;
            item.Hours[hour] = item.Hours.GetValueOrDefault(hour) + 1;
        }
        Entries = Entries.OrderByDescending(x => x.LastUtc).Take(500).ToList();
        if (save) Save();
    }
    void Save()
    {
        if (file == null) return;
        try
        {
            var temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Entries, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, file, true);
            LastSaveError = null;
        }
        catch (Exception e) { LastSaveError = e.Message; }
    }
    public List<AppEntry> Recent(IEnumerable<AppEntry> apps) => apps.Where(x => Find(x) != null).OrderByDescending(x => Find(x)!.LastUtc).Take(14).ToList();
    double Score(AppEntry app, string context, DateTimeOffset now, bool contextual)
    {
        var item = Find(app);
        if (item == null) return 0;
        var hours = Math.Max(0, (now - item.LastUtc).TotalHours);
        return 12.0 / (1 + hours) + Math.Log2(1 + item.Count)
            + (contextual ? 8 * Math.Log2(1 + item.Contexts.GetValueOrDefault(context)) + 1.5 * Math.Log2(1 + item.Hours.GetValueOrDefault(now.ToLocalTime().Hour / 4)) : 0);
    }
    public List<AppEntry> Search(IEnumerable<AppEntry> apps, string text, string context, DateTimeOffset now, bool contextual, Dictionary<string, string[]>? aliases = null)
    {
        var query = Catalog.Normalize(text);
        return apps.Select(x => (App: x, Name: Catalog.Normalize(x.Name), Rank: AppSearch.Rank(x, text, aliases)))
            .Where(x => x.Rank < 99)
            .OrderBy(x => x.Rank)
            .ThenByDescending(x => Score(x.App, context, now, contextual))
            .ThenBy(x => x.App.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(40).Select(x => x.App).ToList();
    }
}

record ForegroundApp(string Process, string ExePath, string AppId = "");

static class DesktopContext
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetApplicationUserModelId(IntPtr process, ref uint size, StringBuilder? value);
    static string ReadAppId(uint id)
    {
        var handle = OpenProcess(0x1000, false, id);
        if (handle == IntPtr.Zero) return "";
        try
        {
            uint size = 0;
            if (GetApplicationUserModelId(handle, ref size, null) != 122 || size > 4096) return "";
            var value = new StringBuilder((int)size);
            return GetApplicationUserModelId(handle, ref size, value) == 0 ? value.ToString() : "";
        }
        finally { CloseHandle(handle); }
    }
    public static ForegroundApp? Read()
    {
        try
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var id);
            if (id == 0 || id == Environment.ProcessId) return null;
            using var process = Process.GetProcessById((int)id);
            string path = "";
            try { path = process.MainModule?.FileName ?? ""; } catch { }
            return new(process.ProcessName, path, ReadAppId(id));
        }
        catch { return null; }
    }
    public static AppEntry? Match(IEnumerable<AppEntry> apps, ForegroundApp active)
    {
        if (active.AppId.Length > 0)
        {
            var packaged = apps.FirstOrDefault(x => x.Target.Equals("shell:AppsFolder\\" + active.AppId, StringComparison.OrdinalIgnoreCase));
            if (packaged != null) return packaged;
        }
        var exact = apps.FirstOrDefault(x => active.ExePath.Length > 0 && x.ExePath.Equals(active.ExePath, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;
        return apps.FirstOrDefault(x => x.ExePath.Length > 0 && Path.GetFileNameWithoutExtension(x.ExePath).Equals(active.Process, StringComparison.OrdinalIgnoreCase));
    }
}
