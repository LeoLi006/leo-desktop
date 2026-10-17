using Microsoft.Win32;
using System.Text.Json;
namespace LeoLauncher;

static class LeoStartup
{
    const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Approval = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";
    public static string Command(string executable) => "\"" + Path.GetFullPath(executable) + "\"";
    public static void Log(string message) { try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"startup-last.log"),DateTimeOffset.Now.ToString("O")+" · "+message+Environment.NewLine); }catch { } }
    static string Shortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Leo.lnk");
    static bool Blocked(string group,string name) { using var key=Registry.CurrentUser.OpenSubKey(Approval+@"\"+group);return key?.GetValue(name) is byte[] data&&data.Length>0&&data[0] is 3 or 7; }
    static (string Target,string Directory,string Arguments) ReadShortcut()
    {
        object? shell=null,link=null;
        try { shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);dynamic shortcut=link=((dynamic)shell!).CreateShortcut(Shortcut);return ((string)shortcut.TargetPath,(string)shortcut.WorkingDirectory,(string)shortcut.Arguments); }
        finally { if(link!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);if(shell!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
    }
    public static bool Enabled { get {
        try { if(File.Exists(Shortcut)){var link=ReadShortcut();return string.Equals(link.Target,Application.ExecutablePath,StringComparison.OrdinalIgnoreCase)&&string.Equals(link.Directory,AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)&&link.Arguments=="--startup"&&!Blocked("StartupFolder","Leo.lnk");}
        using var key=Registry.CurrentUser.OpenSubKey(Run);return string.Equals(key?.GetValue("Leo") as string,Command(Application.ExecutablePath),StringComparison.OrdinalIgnoreCase)&&!Blocked("Run","Leo"); }catch{return false;}
    } }
    public static void SetEnabled(bool enabled)
    {
        if(enabled){
            Directory.CreateDirectory(Path.GetDirectoryName(Shortcut)!);object? shell=null,link=null;
            try { shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);dynamic shortcut=link=((dynamic)shell!).CreateShortcut(Shortcut);shortcut.TargetPath=Application.ExecutablePath;shortcut.WorkingDirectory=AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);shortcut.Arguments="--startup";shortcut.IconLocation=Application.ExecutablePath+",0";shortcut.Description="Leo 桌面助手";shortcut.Save(); }
            finally { if(link!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);if(shell!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
            using var approval=Registry.CurrentUser.OpenSubKey(Approval+@"\StartupFolder",true);approval?.DeleteValue("Leo.lnk",false);
        }else if(File.Exists(Shortcut))File.Delete(Shortcut);
        using var key=Registry.CurrentUser.CreateSubKey(Run);key.DeleteValue("Leo",false);
        if(Enabled!=enabled)throw new IOException("开机启动设置未保存成功，请检查 Windows 启动应用权限");
    }
}

static class LeoHotkeys
{
    public static Keys[] Values(Preferences p) => [p.SearchHotkey, p.VoiceHotkey, p.MusicModule?p.LyricsHotkey:Keys.None];
    public static string Display(Keys keys) => keys == Keys.None ? "未启用" : new KeysConverter().ConvertToString(keys) ?? "";
    public static void Validate(Keys[] keys)
    {
        var active = keys.Where(k => k != Keys.None).ToArray();
        if (active.Distinct().Count() != active.Length) throw new ArgumentException("不同功能不能使用相同的快捷键");
        foreach (var chord in active)
        {
            var key = chord & Keys.KeyCode;
            if ((chord & (Keys.Control | Keys.Alt)) == 0 || key is Keys.None or Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin)
                throw new ArgumentException("快捷键至少包含 Ctrl 或 Alt，再加一个字母、数字或功能键");
        }
    }
    public static bool Register(IntPtr window, int id, Keys chord)
    {
        if (chord == Keys.None) return true;
        uint mods = 0x4000;
        if ((chord & Keys.Control) != 0) mods |= 2;
        if ((chord & Keys.Alt) != 0) mods |= 1;
        if ((chord & Keys.Shift) != 0) mods |= 4;
        return Native.RegisterHotKey(window, id, mods, (uint)(chord & Keys.KeyCode));
    }
    // Restore previous bindings if any new binding is unavailable.
    public static void Replace(Keys[] previous, Keys[] next, Action<int> unregister, Func<int, Keys, bool> register)
    {
        Validate(next);
        for (int i = 1; i <= 3; i++) unregister(i);
        for (int i = 0; i < 3; i++)
        {
            if (register(i + 1, next[i])) continue;
            for (int j = 1; j <= 3; j++) unregister(j);
            for (int j = 0; j < 3; j++) register(j + 1, previous[j]);
            throw new InvalidOperationException(Display(next[i]) + " 已被其他应用占用，请换一个组合");
        }
    }
}

sealed class HotkeyField : TextBox
{
    Keys chord;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Keys Chord { get => chord; set { chord = value; Text = LeoHotkeys.Display(value); } }
    public HotkeyField(Keys value)
    {
        ReadOnly = true; ShortcutsEnabled = false; BackColor = BarcaUi.Raised; ForeColor = BarcaUi.Text; BorderStyle = BorderStyle.FixedSingle;
        Chord = value;
        KeyDown += (_, e) =>
        {
            e.SuppressKeyPress = true;
            if (e.KeyCode is Keys.Back or Keys.Delete) { Chord = Keys.None; return; }
            if (e.KeyCode is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin) return;
            if ((e.Modifiers & (Keys.Control | Keys.Alt)) != 0) Chord = e.KeyData;
        };
    }
}

sealed class SettingsForm : BarcaWindow
{
    public SettingsForm(Preferences p, Action<Preferences, bool> apply, Action apiChanged, bool preview = false, Action<bool>? capture = null,Func<IEnumerable<AppEntry>>? applications=null,Func<string>? hotkeyStatus=null)
    {
        Text = "Leo · 设置"; ClientSize = new Size(640, 660); StartPosition = FormStartPosition.CenterScreen; TopMost = false; ShowInTaskbar = true;
        var header = BarcaUi.Chrome(this, "LEO  /  设置", "把 Leo 调整成顺手的样子。Visca el Barça!");
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(22, 10, 0, 0) };
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 4, 22, 8) };
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 77, Padding = new Padding(24, 8, 24, 12) };
        var general = new Panel { Dock = DockStyle.Fill }; var shortcuts = new Panel { Dock = DockStyle.Fill }; var api = new Panel { Dock = DockStyle.Fill };
        var scene=new SceneSettings(preview,applications);var modules=new ModulesView(p);var checks=new HealthChecksView(p,preview,hotkeyStatus);var usage=new UsageSettingsView(preview);host.Controls.AddRange([general, shortcuts, api,scene,modules,checks,usage]);
        var status = new Label { Bounds = new Rectangle(24, 7, 440, 59), ForeColor = BarcaUi.Muted, Font = BarcaUi.Font(8.5f) };
        var save = new BarcaButton { Text = "保存设置", Bounds = new Rectangle(500, 14, 112, 36), ForeColor = BarcaUi.Gold };
        footer.Controls.AddRange([status, save]);
        Control[] pages = [general, shortcuts, api,scene,modules,checks,usage]; var tabs = new List<BarcaButton>();
        void Select(int index) { for (int i = 0; i < pages.Length; i++) { pages[i].Visible = i == index; tabs[i].Selected = i == index; tabs[i].Invalidate(); } save.Visible = index < 2||index==4; status.Text = index==4?"功能开关与停靠设置保存后立即生效。":index==5?"检查默认只读取本机状态；API 连接需手动检测。":index==6?"限额单独保存；本地操作不占用模型请求额度。":index == 3 ? "新建或编辑组合后保存；执行时不调用模型 API。" : index == 2 ? "API 单独保存；Key 只在本机加密存储。" : "常规与快捷键统一保存，立即生效。"; }
        foreach (var (label, index) in new[] { ("常规", 0), ("快捷键", 1), ("API", 2), ("自定义",3),("功能",4),("运行检查",5),("用量",6) }) { var tab = new BarcaButton { Text = label, Size = new Size(77, 31), Margin = new Padding(0, 0, 6, 0) }; tab.Click += (_, _) => Select(index); tabs.Add(tab); navigation.Controls.Add(tab); }
        Label Label(string text, int y, bool title = false) { var label = new Label { Text = text, Bounds = new Rectangle(5, y, 550, title ? 30 : 24), ForeColor = title ? BarcaUi.Gold : BarcaUi.Muted, Font = BarcaUi.Font(title ? 11 : 9, title) }; general.Controls.Add(label); return label; }
        Label("外观与启动", 10, true);
        Label("Leo 大小", 55);
        var scale = new TrackBar { Bounds = new Rectangle(105, 48, 350, 45), Minimum = 80, Maximum = 230, Value = Math.Clamp(p.PetSize, 80, 230), TickStyle = TickStyle.None, AccessibleName = "Leo 大小" };
        var sizeValue = new Label { Bounds = new Rectangle(475, 55, 75, 25), ForeColor = BarcaUi.Text };
        void SizeText() => sizeValue.Text = scale.Value + " px"; scale.ValueChanged += (_, _) => SizeText(); SizeText(); general.Controls.AddRange([scale, sizeValue]);
        CheckBox Check(string text, int y, bool value) { var check = new CheckBox { Text = text, Bounds = new Rectangle(8, y, 550, 28), Checked = value, ForeColor = BarcaUi.Text, Font = BarcaUi.Font(9.5f) }; general.Controls.Add(check); return check; }
        var startup = Check("登录 Windows 后自动启动 Leo", 100, !preview && LeoStartup.Enabled);
        checks.StartupFixed=()=>startup.Checked=LeoStartup.Enabled;
        var full = Check("游戏或视频全屏时自动隐藏", 140, p.HideInFullscreen);
        Label("应用与剪贴板", 195, true);
        var history = Check("记住最近使用的应用", 235, p.RememberHistory);
        var context = Check("结合前台应用与使用时段排序", 275, p.ContextAware);
        var clipboard = Check("保存剪贴板历史", 315, p.ClipboardHistory);
        var fields = new[]{p.SearchHotkey,p.VoiceHotkey,p.LyricsHotkey}.Select(k => new HotkeyField(k)).ToArray();
        foreach(var field in fields){field.Enter+=(_,_)=>capture?.Invoke(true);field.Leave+=(_,_)=>capture?.Invoke(false);}
        Deactivate+=(_,_)=>capture?.Invoke(false);
        Activated+=(_,_)=>{if(fields.Any(field=>field.Focused))capture?.Invoke(true);};
        FormClosed+=(_,_)=>capture?.Invoke(false);
        shortcuts.Controls.Add(new Label { Text = "点击输入框，再按组合键", Bounds = new Rectangle(5, 12, 370, 30), ForeColor = BarcaUi.Gold, Font = BarcaUi.Font(11, true) });
        var names = new[] { "应用搜索", "直接说话", "显示 / 隐藏歌词" };
        for (int i = 0; i < 3; i++) { int y = 65 + i * 68; shortcuts.Controls.Add(new Label { Text = names[i], Bounds = new Rectangle(8, y + 5, 180, 27), ForeColor = BarcaUi.Text }); fields[i].SetBounds(210, y, 340, 31); fields[i].AccessibleName = names[i] + "快捷键"; shortcuts.Controls.Add(fields[i]); }
        shortcuts.Controls.Add(new Label { Text = "使用 Ctrl / Alt，可配合 Shift。Backspace 或 Delete 停用。\n保存时检查占用；若冲突，将保留原来的快捷键。", Bounds = new Rectangle(8, 250, 550, 52), ForeColor = BarcaUi.Muted, Font = BarcaUi.Font(9) });
        var reset = new BarcaButton { Text = "恢复默认按键", Bounds = new Rectangle(405, 8, 145, 32) }; reset.Click += (_, _) => { var defaults = LeoHotkeys.Values(new Preferences()); for (int i = 0; i < 3; i++) fields[i].Chord = defaults[i]; }; shortcuts.Controls.Add(reset);
        var apiEditor = new ApiSettingsForm(preview ? new ChatSettings() : ChatSettings.Load(), preview, true, apiChanged); api.Controls.Add(apiEditor); apiEditor.Show();
        save.Click += (_, _) => { try { var next = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(p))!; next.PetSize = scale.Value; next.RememberHistory = history.Checked; next.ContextAware = context.Checked; next.HideInFullscreen = full.Checked; next.ClipboardHistory = clipboard.Checked; next.SearchHotkey = fields[0].Chord; next.VoiceHotkey = fields[1].Chord; next.LyricsHotkey = fields[2].Chord;modules.Apply(next); LeoHotkeys.Validate(LeoHotkeys.Values(next)); apply(next, startup.Checked); status.Text = "已保存，设置立即生效。";if(!preview)OperationFeedback.Show("设置已保存"); } catch (Exception e) { status.Text = e.Message; } };
        Controls.Add(host); Controls.Add(footer); Controls.Add(navigation); Controls.Add(header); MinimumSize = MaximumSize = Size; Select(0);
    }
}

sealed partial class PetForm
{
    SettingsForm? settingsWindow;
    void SetHotkeyCapture(bool capture)
    {
        if(previewMode||IsDisposed)return;
        for(int i=1;i<=3;i++)Native.UnregisterHotKey(Handle,i);
        if(!capture)RegisterConfiguredHotkeys();
    }
    readonly string[] hotkeyChecks=["待检查","待检查","待检查"];
    string ShortcutStatus()=>"搜索 "+hotkeyChecks[0]+" / 语音 "+hotkeyChecks[1]+" / 歌词 "+hotkeyChecks[2];
    void RegisterConfiguredHotkeys()
    {
        var keys = LeoHotkeys.Values(prefs);
        for (int i = 0; i < 3; i++) { Native.UnregisterHotKey(Handle,i+1);bool registered = LeoHotkeys.Register(Handle, i + 1, keys[i]); hotkeyChecks[i]=keys[i]==Keys.None?"未启用":registered?"正常":"被占用";if (i == 2) lyricsHotkeyRegistered = keys[i] != Keys.None && registered; }
    }
    void ApplySettings(Preferences next, bool startup)
    {
        var previousKeys = LeoHotkeys.Values(prefs); var nextKeys = LeoHotkeys.Values(next);
        if (!previewMode)
        {
            bool wasStartup = LeoStartup.Enabled;
            LeoHotkeys.Replace(previousKeys, nextKeys, id => Native.UnregisterHotKey(Handle, id), (id, chord) => LeoHotkeys.Register(Handle, id, chord));
            try { LeoStartup.SetEnabled(startup); Store.Save(next); }
            catch { LeoStartup.SetEnabled(wasStartup); LeoHotkeys.Replace(nextKeys, previousKeys, id => Native.UnregisterHotKey(Handle, id), (id, chord) => LeoHotkeys.Register(Handle, id, chord)); throw; }
        }
        prefs.TerminalModule=next.TerminalModule;prefs.ClashModule=next.ClashModule;prefs.MusicModule=next.MusicModule;prefs.EdgeModule=next.EdgeModule;prefs.EdgeDocking=next.EdgeDocking;
        foot.ConfigureModules(prefs);launcher.RefreshModules();
        prefs.PetSize = next.PetSize; prefs.RememberHistory = next.RememberHistory; prefs.ContextAware = next.ContextAware; prefs.HideInFullscreen = next.HideInFullscreen; prefs.ClipboardHistory = next.ClipboardHistory;
        prefs.SearchHotkey = next.SearchHotkey; prefs.VoiceHotkey = next.VoiceHotkey; prefs.LyricsHotkey = next.LyricsHotkey; lyricsHotkeyRegistered = next.MusicModule&&next.LyricsHotkey != Keys.None;
        if(!previewMode)RegisterConfiguredHotkeys();
        launcher.PreviewSize(prefs.PetSize); ResizePet(); PollFullscreen();
    }
}
