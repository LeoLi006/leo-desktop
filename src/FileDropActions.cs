using System.Diagnostics;
using System.Runtime.InteropServices;
namespace LeoLauncher;

static class FileDropActions
{
    public static string[] ValidPaths(IEnumerable<string> paths) => paths.Where(x => !string.IsNullOrWhiteSpace(x) && (File.Exists(x) || Directory.Exists(x)))
        .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToArray();
    public static ProcessStartInfo AppStart(string executable, IEnumerable<string> paths)
    {
        if (!Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(executable)) throw new IOException("请选择可执行的桌面应用");
        var start = new ProcessStartInfo(executable) { UseShellExecute = true };
        foreach (var path in RequirePaths(paths)) start.ArgumentList.Add(path);
        if (start.ArgumentList.Count == 0) throw new IOException("文件已不存在");
        return start;
    }
    public static string QuoteArgument(string value){var output=new System.Text.StringBuilder("\"");int slashes=0;foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"'){output.Append('\\',slashes*2+1).Append(c);slashes=0;continue;}output.Append('\\',slashes).Append(c);slashes=0;}return output.Append('\\',slashes*2).Append('"').ToString();}
    public static string[] RequirePaths(IEnumerable<string> paths)
    {
        var values=paths.ToArray();
        if(values.Length==0||values.Length>100)throw new ArgumentException("一次拖入须为1～100项，未提交打开请求。");
        var result=values.Select(RequirePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return result;
    }
    public static string RequirePath(string path)
    {
        if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path)||(!File.Exists(path)&&!Directory.Exists(path)))throw new IOException("文件或文件夹不存在，请检查路径。");
        return Path.GetFullPath(path);
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CommandLineToArgvW(string command,out int count);
    [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr memory);
    internal static string[] ParseArguments(string arguments)
    {
        var memory=CommandLineToArgvW("leo.exe "+arguments,out int count);
        if(memory==IntPtr.Zero)throw new IOException("无法解析应用启动参数。");
        try{return Enumerable.Range(1,count-1).Select(i=>Marshal.PtrToStringUni(Marshal.ReadIntPtr(memory,i*IntPtr.Size))!).ToArray();}finally{LocalFree(memory);}
    }
    public static ProcessStartInfo ConfiguredStart(Preferences prefs,string module,IEnumerable<string> paths)
    {
        var app=prefs.ApplicationModules.FirstOrDefault(a=>a.Id==module)??throw new ArgumentException("应用模块不存在。");
        if(!app.Enabled)throw new InvalidOperationException("应用模块已停用。");
        var valid=RequirePaths(paths);var start=LocalApplications.Start(app);start.Arguments="";
        foreach(string arg in ParseArguments(app.Arguments))start.ArgumentList.Add(arg);
        foreach(string path in valid)start.ArgumentList.Add(path);
        return start;
    }
    public static ProcessStartInfo FolderStart(string path)
    {
        path=RequirePath(path);
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
        start.ArgumentList.Add(Directory.Exists(path) ? path : "/select," + path);
        return start;
    }
    public static Form? Open(IEnumerable<string> dropped, IEnumerable<AppEntry> catalog)
    {
        var paths = ValidPaths(dropped); if (paths.Length == 0) return null;
        var window = new Form { FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false, Text = "Leo · 文件操作", StartPosition = FormStartPosition.CenterScreen, ClientSize = new Size(560, 330), Font = Appearance.Body(), BackColor = Appearance.Background, ForeColor = Appearance.Ink };
        window.Controls.Add(new Label { Text = $"已接收 {paths.Length} 项 · 选择操作", Bounds = new Rectangle(18, 15, 520, 24) });
        var list = new ListBox { Bounds = new Rectangle(18, 48, 524, 130), BackColor = Appearance.Field, ForeColor = Appearance.Ink, HorizontalScrollbar = true };
        list.Items.AddRange(paths); list.SelectedIndex = 0; window.Controls.Add(list);
        var apps = new ComboBox { Bounds = new Rectangle(18, 224, 340, 28), DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name", BackColor = Appearance.Field, ForeColor = Appearance.Ink };
        apps.Items.AddRange(catalog.Where(x => x.ExePath.Length > 0 && File.Exists(x.ExePath)).DistinctBy(x => x.ExePath, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Name).ToArray());
        if (apps.Items.Count > 0) apps.SelectedIndex = 0; window.Controls.Add(apps);
        var status = new Label { Bounds = new Rectangle(18, 292, 524, 28), ForeColor = Appearance.Muted }; window.Controls.Add(status);
        void Button(string label, Rectangle bounds, Action action)
        {
            var button = new Button { Text = label, Bounds = bounds, FlatStyle = FlatStyle.Flat, BackColor = Appearance.Field, ForeColor = Appearance.Ink };
            button.FlatAppearance.BorderSize = 0; button.Click += (_, _) => { try { action(); } catch (Exception e) { status.Text = e.Message; } }; window.Controls.Add(button);
        }
        Button("复制全部路径", new Rectangle(18, 185, 160, 30), () => { Clipboard.SetText(string.Join(Environment.NewLine, paths)); status.Text = "已复制路径"; });
        Button("打开所在文件夹", new Rectangle(190, 185, 170, 30), () => { if (list.SelectedItem is string path) Process.Start(FolderStart(path)); });
        Button("用所选应用打开", new Rectangle(370, 224, 170, 30), () => {
            if (apps.SelectedItem is not AppEntry app) { status.Text = "请先选择应用"; return; }
            Process.Start(AppStart(app.ExePath, paths)); status.Text = "已交给所选应用";
        });
        Button("浏览其他应用…", new Rectangle(370, 258, 170, 30), () => {
            using var picker = new OpenFileDialog { Title = "选择打开文件的应用", Filter = "桌面应用 (*.exe)|*.exe" };
            if (picker.ShowDialog(window) != DialogResult.OK) return;
            var entry = new AppEntry(Path.GetFileNameWithoutExtension(picker.FileName), picker.FileName, ExePath: picker.FileName);
            apps.Items.Add(entry); apps.SelectedItem = entry;
        });
        window.Show(); return window;
    }
}
