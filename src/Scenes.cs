using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;

sealed class WatchScene
{
    public bool Proxy { get; set; } = true;
    public bool Website { get; set; } = true;
    public bool Music { get; set; } = true;
    public int WatchIndex { get; set; } = -1;
    static string FilePath => Path.Combine(Store.Root, "leo-scenes.json");
    public static WatchScene Load() { try { return JsonSerializer.Deserialize<WatchScene>(File.ReadAllText(FilePath)) ?? new(); } catch { return new(); } }
    public void Save() { if (WatchIndex is < -1 or > 3) throw new ArgumentException("请选择有效的观赛入口"); File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this)); File.Move(FilePath + ".tmp", FilePath, true); }
}

sealed class SceneRun
{
    readonly (string Name, Func<Task> Run)[] steps;
    int next;
    bool busy;
    public int Completed => next;
    public SceneRun(params (string Name, Func<Task> Run)[] actions) { steps = actions; }
    public async Task Execute(Action<string> progress)
    {
        if (busy) return;
        busy = true;
        try { while (next < steps.Length) { progress(steps[next].Name); await steps[next].Run(); next++; } }
        finally { busy = false; }
    }
}
