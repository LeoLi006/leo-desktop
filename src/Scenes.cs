using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;

sealed class WatchScene
{
    public bool Proxy { get; set; } = true;
    public bool Website { get; set; } = true;
    public bool Music { get; set; } = true;
    public int WatchIndex { get; set; } = -1;
    public string PlaylistId { get; set; } = "";
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

sealed partial class PetForm
{
    bool watchSceneBusy;
    async Task<string> PrepareWatch()
    {
        if (watchSceneBusy) return "看球场景正在执行，请稍候";
        if (previewMode) { OperationFeedback.Show("示例模式：准备看球，不执行外部操作"); return "示例模式，不执行外部操作"; }
        try
        {
            var config = WatchScene.Load();
            var steps = new List<(string, Func<Task>)>();
            if (config.Proxy) steps.Add(("开启系统代理", async () => { var status = await Task.Run(() => ClashProxy.SetEnabled(true)); if (status.Enabled != true) throw new IOException("未确认系统代理已开启"); }));
            if (config.Website)
            {
                var links = new WatchLinkStore(Path.Combine(Store.Root, "leo-watch-links.json")).Items;
                var link = config.WatchIndex < 0 ? links.FirstOrDefault(l => l.Url.Length > 0) : links.ElementAtOrDefault(config.WatchIndex);
                if (link == null || link.Url.Length == 0) throw new ArgumentException("请先在足球窗口填写观赛网址，再到设置 → 场景选择入口");
                steps.Add(("打开 " + link.Name, () => { Process.Start(FootTools.EdgeUrlStart(link.Url)); return Task.CompletedTask; }));
            }
            if (config.Music)
            {
                var panel = launcher.MusicWithoutOpening();
                var playlist = config.PlaylistId.Length > 0 ? panel.Library.Items.FirstOrDefault(p => p.Id == config.PlaylistId) : panel.Library.Items.FirstOrDefault(p => p.Name == "赛前热身");
                if (playlist == null || playlist.Songs.Count == 0) throw new ArgumentException("请在设置 → 场景选择有歌曲的热身歌单");
                string id = playlist.Id;
                steps.Add(("播放 " + playlist.Name, async () => { if (!await panel.PlayPlaylist(id)) throw new IOException("未能发送歌单，请检查QQ音乐客户端"); }));
            }
            if (steps.Count == 0) throw new ArgumentException("请先在设置 → 场景启用至少一项操作");
            var run = new SceneRun(steps.ToArray());
            async Task<string> Continue()
            {
                if (watchSceneBusy || IsDisposed) return "场景正在执行或窗口已关闭";
                watchSceneBusy = true;
                try { await run.Execute(step => OperationFeedback.Pending("准备看球 · " + step)); OperationFeedback.Show("准备看球完成 · 已发送所选操作");return "准备看球完成："+string.Join("、",steps.Select(s=>s.Item1))+"。音乐仅确认已发送客户端。"; }
                catch (Exception e) { OperationFeedback.Error($"已完成 {run.Completed}/{steps.Count} 项：" + e.Message, ()=>Continue());return "看球场景未完成："+e.Message+"；可在Leo旁重试。"; }
                finally { watchSceneBusy = false; }
            }
            return await Continue();
        }
        catch (Exception e) { OperationFeedback.Error(e.Message, () => { OpenSettings(); return Task.CompletedTask; },"去设置");return "未执行看球场景："+e.Message; }
    }
}

sealed class SceneSettings : Panel
{
    public SceneSettings(bool preview)
    {
        Dock = DockStyle.Fill;
        var config = preview ? new WatchScene() : WatchScene.Load();
        var links = new WatchLinkStore(preview ? null : Path.Combine(Store.Root, "leo-watch-links.json")).Items;
        var lists = new MusicLibrary(preview ? null : Path.Combine(Store.Root, "leo-playlists.json")).Items;
        Controls.Add(new Label { Text = "准备看球", Bounds = new Rectangle(8, 12, 550, 28), ForeColor = BarcaUi.Gold, Font = BarcaUi.Font(11, true) });
        Controls.Add(new Label { Text = "右键 Leo 或直接说“准备看球”，本地执行以下操作。", Bounds = new Rectangle(8, 47, 560, 30), ForeColor = BarcaUi.Muted, Font = BarcaUi.Font(9) });
        CheckBox Toggle(string text, int y, bool value) { var box = new CheckBox { Text = text, Checked = value, Bounds = new Rectangle(8, y, 550, 28), ForeColor = BarcaUi.Text }; Controls.Add(box); return box; }
        var proxy = Toggle("开启系统代理（未运行则启动 Clash）", 87, config.Proxy);
        var website = Toggle("打开观赛网址", 130, config.Website);
        var watch = new ComboBox { Bounds = new Rectangle(35, 164, 500, 30), DropDownStyle = ComboBoxStyle.DropDownList };
        watch.Items.Add("自动选择第一个已填写的入口"); foreach (var link in links) watch.Items.Add(link.Name + (link.Url.Length == 0 ? " · 未填写" : "")); watch.SelectedIndex = Math.Clamp(config.WatchIndex + 1, 0, 4); Controls.Add(watch);
        var music = Toggle("播放热身歌单（未运行则启动QQ音乐）", 211, config.Music);
        var playlist = new ComboBox { Bounds = new Rectangle(35, 245, 500, 30), DropDownStyle = ComboBoxStyle.DropDownList };
        playlist.Items.Add("自动选择“赛前热身”"); foreach (var list in lists) playlist.Items.Add(list.Name + " · " + list.Songs.Count + "首"); playlist.SelectedIndex = Math.Max(0, lists.FindIndex(p => p.Id == config.PlaylistId) + 1); Controls.Add(playlist);
        var save = new BarcaButton { Text = "保存场景", Bounds = new Rectangle(8, 286, 120, 33), ForeColor = BarcaUi.Gold };
        var status = new Label { Text = "失败后重试只继续剩余步骤；不会重复打开网址。", Bounds = new Rectangle(8, 325, 560, 24), ForeColor = BarcaUi.Muted, Font = BarcaUi.Font(8.5f) }; Controls.AddRange([save, status]);
        save.Click += (_, _) => { if (preview) { status.Text = "示例模式，不保存场景"; return; } try { new WatchScene { Proxy = proxy.Checked, Website = website.Checked, Music = music.Checked, WatchIndex = watch.SelectedIndex - 1, PlaylistId = playlist.SelectedIndex <= 0 ? "" : lists[playlist.SelectedIndex - 1].Id }.Save(); status.Text = "看球场景已保存。"; OperationFeedback.Show("看球场景已保存"); } catch (Exception e) { status.Text = e.Message; } };
    }
}
