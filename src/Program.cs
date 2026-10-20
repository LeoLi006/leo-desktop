using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LeoLauncher;

record AppEntry(string Name, string Target, string Hint = "应用", string ExePath = "");

sealed class Preferences
{
    public Dictionary<string,string> ModuleExecutables {get;set;}=new();
    public List<LocalApplication> ApplicationModules {get;set;}=[];
    public Dictionary<string,InstalledApplicationPackage> ApplicationPackages {get;set;}=new();
    public List<string> ShortcutOrder {get;set;}=[];
    public Dictionary<string,string> EntryDisplayModes {get;set;}=new();
    public Dictionary<string,string> ModuleIcons {get;set;}=new();
    public string[]? PlannerQuotes {get;set;}
    public bool AutoUpdateNotifications {get;set;}=true;
    public string LastNotifiedUpdate {get;set;}="";
    public bool ApplicationIndexing {get;set;}=true;
    public string ActiveProfile {get;set;}="";
    public int X { get; set; } = -1;
    public int Y { get; set; } = -1;
    public int PetSize { get; set; } = 130;
    public Keys SearchHotkey { get; set; } = Keys.Control | Keys.Alt | Keys.Space;
    public Keys VoiceHotkey { get; set; } = Keys.Control | Keys.Alt | Keys.V;
    public Keys LyricsHotkey { get; set; } = Keys.Control | Keys.Alt | Keys.L;
    public Keys QuickNoteHotkey {get;set;}=Keys.Control|Keys.Alt|Keys.N;
    public bool RememberHistory { get; set; } = true;
    public bool ContextAware { get; set; } = true;
    public List<string> Favorites { get; set; } = new();
    public bool ClipboardHistory { get; set; } = true;
    public DateTimeOffset? ClipboardPauseUntil{get;set;}
    public int ClipboardExpireDays{get;set;}=7;
    public bool ClipboardBlockSecrets{get;set;}=true;
    public List<string> ClipboardBlockedApps{get;set;}=["1Password","Bitwarden","KeePass","KeePassXC","LastPass","Dashlane"];
    public string? SkinId{get;set;}
    public string DisplayName{get;set;}="Leo";
    public bool MediaModule{get;set;}=true;
    public List<string> DisabledExtensions{get;set;}=[];
    public Dictionary<string,ThemeOptions> SkinThemes{get;set;}=new();
    // Read old saved settings once, then omit these retired fields.
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? MatchDayOutfits{get;set;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? MatchDayKit{get;set;}
    public Dictionary<string, string[]> Aliases { get; set; } = new();
    public bool HideInFullscreen { get; set; } = true;
    public bool EdgeDocking { get; set; } = true;
    public bool TerminalModule { get; set; } = true;
    public bool ClashModule { get; set; } = true;
    public bool MusicModule { get; set; } = true;
    public bool FootballModule { get; set; } = true;
    public bool EdgeModule { get; set; } = true;
}

static class Store
{
    // Portable: keep preferences beside the executable, not in the user's home.
    public static readonly string Root = AppContext.BaseDirectory;
    public static string FilePath => Path.Combine(Root, "leo-settings.json");
    public static bool LoadFailed{get;private set;}
    public static Preferences Load()
    {
        Migrate();
        try {return AtomicJson.Read(FilePath,Validate);}
        catch { LoadFailed=File.Exists(FilePath)||File.Exists(FilePath+".bak");return new(); }
    }
    internal static Preferences Validate(string text){using var document=JsonDocument.Parse(text);var result=document.RootElement.Deserialize<Preferences>()??throw new IOException("设置内容为空");if(result.PetSize is <80 or >230||result.Favorites==null||result.Aliases==null||result.ModuleExecutables==null||result.SkinThemes==null||result.DisabledExtensions==null)throw new IOException("设置内容不完整");ApplicationPackages.ValidateState(result);PlannerPhrases.Validate(result.PlannerQuotes);QuickNoteCompatibility.Migrate(document.RootElement,result);return result;}
    static void Migrate()
    {
        var parent = Directory.GetParent(Root.TrimEnd(Path.DirectorySeparatorChar))?.FullName;
        if (parent == null) return;
        foreach (var name in new[] { "leo-settings.json", "leo-history.json", "leo-clipboard.json", "leo-ai.json", "leo-chat-history.json", "leo-todos.json", "leo-watch-links.json", "leo-music.json", "leo-playlists.json", "leo-reminder-settings.json", "leo-football-cache.json" })
        {
            var target = Path.Combine(Root, name);
            if (File.Exists(target)) continue;
            foreach (var version in new[] { "LeoLauncher-v37", "LeoLauncher-v36", "LeoLauncher-v35", "LeoLauncher-v34", "LeoLauncher-v33", "LeoLauncher-v32", "LeoLauncher-v31", "LeoLauncher-v30", "LeoLauncher-v29", "LeoLauncher-v28", "LeoLauncher-v27", "LeoLauncher-v26", "LeoLauncher-v25", "LeoLauncher-v24", "LeoLauncher-v23", "LeoLauncher-v22", "LeoLauncher-v21", "LeoLauncher-v20", "LeoLauncher-v19", "LeoLauncher-v18", "LeoLauncher-v17", "LeoLauncher-v16", "LeoLauncher-v15", "LeoLauncher-v14", "LeoLauncher-v13", "LeoLauncher-v12", "LeoLauncher-v11", "LeoLauncher-v10", "LeoLauncher-v9", "LeoLauncher-v8", "LeoLauncher-v7", "LeoLauncher-v6", "LeoLauncher-v5", "LeoLauncher-v4", "LeoLauncher-v3", "LeoLauncher-v2", "LeoLauncher" })
            {
                var source = Path.Combine(parent, version, name);
                if (!File.Exists(source) || Path.GetFullPath(source) == Path.GetFullPath(target)) continue;
                try { File.Copy(source, target, false); } catch { }
                break;
            }
        }
    }
    public static void Save(Preferences p)
    {
        try { AtomicJson.Write(FilePath, JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception e) { MessageBox.Show("无法保存设置：" + e.Message, "Leo"); }
    }
}

static class Catalog
{
    public static List<AppEntry> Scan(CancellationToken token=default)
    {
        var result = new List<AppEntry>();
        var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) };
        foreach (var root in roots.Distinct()) Walk(root, result,token);
        // Windows applications without Start Menu shortcuts.
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type != null)
            {
                dynamic shell = Activator.CreateInstance(type)!;
                try
                {
                    dynamic folder = shell.NameSpace("shell:AppsFolder");
                    if (folder != null)
                    {
                        dynamic items = folder.Items();
                        foreach (dynamic item in items)
                        {
                            token.ThrowIfCancellationRequested();
                            try
                            {
                                string name = item.Name;
                                string id = item.Path;
                                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
                                {
                                    var target = Path.IsPathRooted(id) && File.Exists(id) ? id : "shell:AppsFolder\\" + id;
                                    result.Add(new(name, target, "Windows 应用", Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase) ? target : ""));
                                }
                            }
                            finally { if (Marshal.IsComObject(item)) Marshal.ReleaseComObject(item); }
                        }
                        Marshal.ReleaseComObject(items);
                        Marshal.ReleaseComObject(folder);
                    }
                }
                finally { Marshal.ReleaseComObject(shell); }
            }
        }
        catch { /* Ordinary shortcuts remain available when the shell catalog is unavailable. */ }
        token.ThrowIfCancellationRequested();
        return result.GroupBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).Select(g => g.First()).Select(x=>{token.ThrowIfCancellationRequested();return ResolveExecutable(x);})
            .Where(x => !Path.GetExtension(x.Target).Equals(".lnk", StringComparison.OrdinalIgnoreCase) || (x.ExePath.Length > 0 && !new[] { "help", "uninstall", "readme", "帮助", "卸载", "官网", "参考文档" }.Any(word => x.Name.Contains(word, StringComparison.OrdinalIgnoreCase))))
            .OrderBy(x => x.Name).ToList();
    }
    static AppEntry ResolveExecutable(AppEntry entry)
    {
        if (Path.GetExtension(entry.Target).Equals(".exe", StringComparison.OrdinalIgnoreCase)) return entry with { ExePath = entry.Target };
        if (!Path.GetExtension(entry.Target).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) return entry;
        object? shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            shortcut = ((dynamic)shell!).CreateShortcut(entry.Target);
            string target = ((dynamic)shortcut).TargetPath;
            if (Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase)) return entry with { ExePath = target };
        }
        catch { }
        finally
        {
            if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.ReleaseComObject(shortcut);
            if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
        }
        return entry;
    }
    static void Walk(string root, List<AppEntry> result,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return;
        try
        {
            foreach (var file in Directory.GetFiles(root))
                if (new[] { ".lnk", ".appref-ms" }.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    result.Add(new(Path.GetFileNameWithoutExtension(file), file));
            foreach (var dir in Directory.GetDirectories(root))
                if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) == 0) Walk(dir, result,token);
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }
    public static string Normalize(string s) => string.Concat(s.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    public static IEnumerable<AppEntry> Search(IEnumerable<AppEntry> entries, string query)
    {
        var q = Normalize(query);
        return entries.Select(x => (Entry: x, Name: Normalize(x.Name)))
            .Where(x => q.Length == 0 || x.Name.Contains(q))
            .OrderBy(x => x.Name == q ? 0 : x.Name.StartsWith(q) ? 1 : 2)
            .ThenBy(x => x.Entry.Name.Length).Take(40).Select(x => x.Entry);
    }
    public static void Launch(AppEntry entry)
    {
        if (entry.Target.StartsWith("shell:AppsFolder\\", StringComparison.Ordinal))
        {
            var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            psi.ArgumentList.Add(entry.Target);
            Process.Start(psi);
        }
        else Process.Start(new ProcessStartInfo(entry.Target) { UseShellExecute = true });
    }
}

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if(args.Length==1&&args[0]=="--enable-startup"){LeoStartup.SetEnabled(true);return;}
        Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled);
        Application.EnableVisualStyles();
        if(args.Length>0&&args[0]=="--dock-animation-test")Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        FeatureFaults.IsTestMode=args.Any(a=>a.EndsWith("-test",StringComparison.Ordinal))||args.Contains("--package-verify");
        if(!FeatureFaults.IsTestMode){Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);FeatureFaults.Start();}
        BarcaScrollTheme.Start(); UiTheme.Start();
        Application.AddMessageFilter(new EscapeWindows());
        Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length==2&&args[0]=="--m05-conversation-test"){ConversationTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--m05-football-test"){FootballCapabilityTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--m05-todo-test"){TodoCapabilityTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--m05-music-clipboard-test"){MusicClipboardTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--m05-client-test"){M05ClientTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--q6-package-test"){Q6PackageTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--q5-brain-test"){Q5BrainTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--q4-workflow-test"){Q4WorkflowTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--q3-notes-test"){Q3NotesTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--q2-capability-test"){Q2CapabilityTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--baseline-test"){BaselineProbe.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--baseline-runtime-test"){BaselineProbe.Run(args[1],true);return;}
        if(args.Length==2&&args[0]=="--baseline-companion-test"){BaselineProbe.Run(args[1],true,true);return;}
        if(args.Length==2&&args[0]=="--companion-layer-test"){CompanionLayerTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--quiet-planner-test"){QuietPlannerTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--soak-test"){SoakProbe.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--robustness-test"){RobustnessTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--performance-test"){PerformanceProbe.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--feedback-pointer-test"){FeedbackPointerTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--interaction-batch-test"){InteractionBatchTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--profile-batch-test"){ProfileBatchTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--status-update-test"){StatusUpdateTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--entry-display-test"){EntryDisplayTests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--release100-test"){Release100Tests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--platform53-test"){Platform53Tests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--platform52-test"){Platform52Tests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--package-smoke-test"){try{var checks=PackageSmokeTests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks}));}catch(Exception e){File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}return;}
        if(args.Length==2&&args[0]=="--package-verify"){try{var release=LeoInstaller.Verify(AppContext.BaseDirectory);bool selfContained=new[]{typeof(object).Assembly.Location,typeof(Form).Assembly.Location}.All(p=>p.StartsWith(AppContext.BaseDirectory,StringComparison.OrdinalIgnoreCase));File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=selfContained,version=release.Version,files=release.Files.Count,self_contained=selfContained}));}catch(Exception e){File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=false,error=e.Message}));Environment.ExitCode=1;}return;}
        if(args.Length>=1&&args[0]=="--install-wizard"){string? target=args.Length==3&&args[1]=="--target"?args[2]:null;Application.Run(new InstallForm(target));return;}
        if(args.Length==2&&args[0]=="--ask-data-test")
        {
            int models=0;using var window=new ChatForm(false,new ChatSettings(),()=>{models++;throw new InvalidOperationException("不应请求模型");});window.Opacity=0;window.Show();Application.DoEvents();
            void Pump(Task task){var watch=Stopwatch.StartNew();while(!task.IsCompleted&&watch.ElapsedMilliseconds<85000){Application.DoEvents();Thread.Sleep(10);}if(!task.IsCompleted)throw new TimeoutException();task.GetAwaiter().GetResult();}
            Pump(window.PreviewSendAsync("曼城排第几"));bool table=window.PreviewTableRows>0;string first=window.PreviewState;Pump(window.PreviewSendAsync("巴萨下一场什么时候"));bool matches=window.PreviewMatchCards>0;
            File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=table&&matches&&models==0,standings_rendered=table,matches_rendered=matches,first_state=first,last_state=window.PreviewState,model_requests=models},new JsonSerializerOptions{WriteIndented=true}));return;
        }
        if(args.Length==2&&args[0]=="--pet-transparency-test"){try{var checks=PetTransparencyTests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}return;}
        if(args.Length==2&&args[0]=="--media-read-test"){Platform51Tests.MediaProbe(args[1]);return;}
        if(args.Length==2&&args[0]=="--platform51-test"){Platform51Tests.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--theme-desktop-test"){try{var checks=ThemeDesktopTests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}return;}
        if(args.Length==2&&args[0]=="--skin-animation-test"){try{var checks=SkinAnimationTests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}return;}
        if(args.Length==2&&args[0]=="--upgrade46-test"){try{var checks=Upgrade46Tests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}return;}
        if(args.Length==2&&args[0]=="--recovery45-test"){var checks=new Dictionary<string,bool>();Upgrade45Tests.Recovery(checks,Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,external_requests=0}));return;}
        if(args.Length==2&&args[0]=="--upgrade45-test"){var checks=Upgrade45Tests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--client-operations-test"){var checks=Task.Run(ClientOperationTests.Check).GetAwaiter().GetResult();File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks},new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--proxy-cold-test"){var result=Task.Run(ProxyFeatureTests.ColdLive).GetAwaiter().GetResult();File.WriteAllText(args[1],JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--proxy-startup-test"){var checks=ProxyFeatureTests.Startup();File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks},new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--proxy-silent-test"){var checks=ProxyFeatureTests.Silent();File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks},new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--proxy-feature-test"){var checks=ProxyFeatureTests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks},new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--proxy-live-test"){var result=Task.Run(ProxyFeatureTests.Live).GetAwaiter().GetResult();File.WriteAllText(args[1],JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--dock-animation-test"){try{var checks=DockAnimationTests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}return;}
        if(args.Length==2&&args[0]=="--upgrade38-test"){var checks=Upgrade38Tests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--qq-pause-send"){try{string result=QQPlayback.ToggleAsync().GetAwaiter().GetResult();File.WriteAllText(args[1],JsonSerializer.Serialize(new{sent=true,result}));}catch(Exception e){File.WriteAllText(args[1],JsonSerializer.Serialize(new{sent=false,error=e.Message}));Environment.ExitCode=1;}return;}
        if(args.Length==2&&args[0]=="--startup-live-test"){
            var result=Task.Run(async()=>{
                bool coldMusic=System.Diagnostics.Process.GetProcessesByName("QQMusic").Length==0;
                var before=ClashProxy.ReadStatus();
                var song=new MusicSong("97773","0039MnYb0qxYhV","晴天","周杰伦");
                bool music=false,proxy=false;string musicError="",proxyError="";
                try{await QQNative.PlayAsync(song);for(int i=0;i<60;i++){await Task.Delay(250);if(QQNative.PlaybackTitle().Contains(song.Name)){music=true;break;}}}catch(Exception e){musicError=e.Message;}
                try{proxy=ClashProxy.SetEnabled(true).Enabled==true;}catch(Exception e){proxyError=e.Message;}
                finally{if(proxy&&before.Enabled!=true)ClashProxy.SetEnabled(false);}
                return new{ok=music&&proxy,cold_music=coldMusic,cold_clash=!before.Running,music,proxy,musicError,proxyError,proxy_restored=ClashProxy.ReadStatus().Enabled==(before.Enabled==true)};
            }).GetAwaiter().GetResult();
            File.WriteAllText(args[1],JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));return;
        }
        if(args.Length==2&&args[0]=="--qq-native-live38"){File.WriteAllText(args[1],JsonSerializer.Serialize(QQNativeLive38.Run(),new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--upgrade37-test"){var checks=Upgrade37Tests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--upgrade36-test"){var checks=Upgrade36Tests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--upgrade35-test"){var checks=Upgrade35Tests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--music-live-test"){var results=Task.Run(async()=>{using var token=new CancellationTokenSource(TimeSpan.FromSeconds(30));var songs=await QQMusic.Search("晴天",token.Token);return new{ok=songs.Length>0,results=songs.Take(3),client_detected=QQMusic.ClientPath()!=null,model_requests=0,playback_requests=0};}).GetAwaiter().GetResult();File.WriteAllText(args[1],JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--planner-theme-test"){PlannerThemeTests.Save(args[1]);return;}
        if(args.Length==2&&args[0]=="--planner-feature-test"){var checks=PlannerFeatureTests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(x=>x),checks,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--preview-footer"){using var footer=new FootTools(130,true);footer.Show();Application.DoEvents();using(var b=new Bitmap(footer.Width,footer.Height)){footer.DrawToBitmap(b,footer.ClientRectangle);b.Save(args[1]);}footer.PreviewUtilities();Application.DoEvents();using(var b=new Bitmap(footer.Width,footer.Height)){footer.DrawToBitmap(b,footer.ClientRectangle);b.Save(Path.ChangeExtension(args[1],".expanded.png"));}return;}
        if(args.Length==2&&args[0]=="--upgrade-self-test"){var checks=UpgradeTests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(x=>x),checks,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));return;}
        if(args.Length==2&&args[0]=="--preview-planner"){PlannerPreview.Save(args[1]);return;}
        if(args.Length==2&&args[0]=="--quick-self-test")
        {
            var checks=QuickTests.Check(Path.GetDirectoryName(args[1])!);File.WriteAllText(args[1],JsonSerializer.Serialize(new{ok=checks.Values.All(x=>x),checks,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));return;
        }
        if(args.Length==2&&args[0]=="--preview-ask")
        {
            using var window=new ChatForm(true);window.Show();Application.DoEvents();window.PreviewHome();
            using(var bitmap=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(bitmap,new Rectangle(0,0,window.Width,window.Height));bitmap.Save(args[1]);}
            using var daily=new TodoForm(true);daily.Apply(new QuickIntent(QuickKind.AddTodo,"散步20分钟"));daily.Show();Application.DoEvents();using(var bitmap=new Bitmap(daily.Width,daily.Height)){daily.DrawToBitmap(bitmap,daily.ClientRectangle);bitmap.Save(Path.ChangeExtension(args[1],".todos.png"));}return;
        }
        if(args.Length==2&&args[0]=="--team-live-test")
        {
            var results=new List<object>();
            foreach(string name in new[]{"巴塞罗那","曼城","迈阿密国际"}){
                var club=Club.For(name);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
                try{var result=Task.Run(()=>TeamData.Fetch(club,TeamData.Load(club),timeout.Token)).GetAwaiter().GetResult();results.Add(new{name,ok=!result.MatchesFailed&&!result.TableFailed&&result.Data.Rows.Any(r=>r.Id==club.Id),matches=result.Data.Matches.Count,finished=result.Data.Matches.Count(m=>m.State=="post"),table_rows=result.Data.Rows.Count,groups=result.Data.Rows.Select(r=>r.Group).Distinct().ToArray()});}
                catch(Exception e){results.Add(new{name,ok=false,error_type=e.GetType().Name});}
            }
            File.WriteAllText(args[1],JsonSerializer.Serialize(new{results,ai_api_requests=0},new JsonSerializerOptions{WriteIndented=true}));return;
        }
        if(args.Length==2&&args[0]=="--preview-team")
        {
            using var form=new FootballForm(true);form.PreviewData(FootballFeed.LoadCache().Matches);form.Show();Application.DoEvents();bool switching=form.PreviewTeam();
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,form.ClientRectangle);image.Save(Path.ChangeExtension(args[1],".matches.png"));}
            form.PreviewTeamTable();Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,form.ClientRectangle);image.Save(args[1]);}
            File.WriteAllText(Path.ChangeExtension(args[1],".interaction.json"),JsonSerializer.Serialize(new{ok=switching,team_tabs_switch_correctly=switching}));return;
        }
        if (args.Length == 2 && args[0] == "--football-self-test")
        {
            File.WriteAllText(args[1], JsonSerializer.Serialize(FootballTests.Check(), new JsonSerializerOptions { WriteIndented = true })); return;
        }
        if (args.Length == 2 && args[0] == "--football-live-test")
        {
            object report;
            try {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                var cache = Task.Run(() => FootballFeed.FetchAsync(FootballFeed.LoadCache(), timeout.Token)).GetAwaiter().GetResult();
                var following = FootballForm.FollowingNames;
                report = new { ok = cache.Matches.Count > 0, cache.Updated, cache.FailedSources, matches = cache.Matches.Count,
                    followed_matches = cache.Matches.Count(m => FootballFeed.FollowTags(m, following).Length > 0), focus_matches = cache.Matches.Count(FootballFeed.IsFocus),
                    sample = cache.Matches.Where(m => FootballFeed.FollowTags(m, following).Length > 0).Take(5), ai_api_requests = 0 };
            } catch (Exception e) { report = new { ok = false, error_type = e.GetType().Name, ai_api_requests = 0 }; }
            File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })); return;
        }
        if (args.Length == 2 && args[0] == "--preview-football")
        {
            using var form = new FootballForm(true);
            form.PreviewData(FootballFeed.LoadCache().Matches); form.Show(); Application.DoEvents();
            var uiChecks = new Dictionary<string,bool> { ["border_resize_disabled"] = form.VerifyNoResize(), ["following_sidebar_matches_preferences"] = form.VerifyFollowing(), ["tabs_survive_repeated_switching"] = form.VerifyTabs(), ["team_card_opens_detail"] = form.VerifyTeamNavigation(), ["football_is_not_topmost"] = !form.TopMost && form.Owner == null };
            File.WriteAllText(Path.ChangeExtension(args[1], ".interaction.json"), JsonSerializer.Serialize(new { ok=uiChecks.Values.All(x=>x), checks=uiChecks },new JsonSerializerOptions{WriteIndented=true}));
            using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, form.ClientRectangle with { Width = form.Width, Height = form.Height }); bitmap.Save(args[1]); return;
        }
        if (args.Length == 2 && args[0] == "--agent-live-test")
        {
            var report = Task.Run(async () => {
                var config = JsonSerializer.Deserialize<ChatSettings>(File.ReadAllText(ChatSettings.SettingsPath))!;
                int simulatedLaunches = 0;
                var tools = new AppTools(() => new[] { new AppEntry("记事本", "fixture-notepad.lnk") }, _ => simulatedLaunches++);
                using var api = new ChatApi(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(100));
                var events = new List<string>();
                try {
                    var result = await ChatAgent.RunAsync(api, config, config.ReadKey(), new() {
                        new("system", "你正在测试模拟应用工具。必须先 search_apps 搜索记事本，再用返回的 app_id 调用 open_app，最后简短回答。执行器仅计数，不实际打开程序。"),
                        new("user", "请打开记事本。") }, _ => { }, tools, events.Add, timeout.Token);
                    return (object)new { ok = simulatedLaunches == 1 && result.Text.Length > 0, simulated_launches = simulatedLaunches, tool_events = events.Count, reply_received = result.Text.Length > 0, real_app_launches = 0 };
                } catch (Exception e) { return new { ok = false, error_type = e.GetType().Name, real_app_launches = 0 }; }
            }).GetAwaiter().GetResult();
            File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })); return;
        }
        if (args.Length == 2 && args[0] == "--speech-self-test")
        {
            File.WriteAllText(args[1], JsonSerializer.Serialize(SpeechIO.VerifyToFile(Path.ChangeExtension(args[1], ".wav")), new JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        if (args.Length == 2 && args[0] == "--chat-self-test")
        {
            var checks = Task.Run(ChatTests.CheckAsync).GetAwaiter().GetResult();
            File.WriteAllText(args[1], JsonSerializer.Serialize(new { ok = checks.Values.All(x => x), checks, external_requests = 0 }, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        if (args.Length == 2 && args[0] == "--preview-chat")
        {
            void Pump(Task task)
            {
                var wait = Stopwatch.StartNew();
                while (!task.IsCompleted && wait.ElapsedMilliseconds < 12000) { Application.DoEvents(); Thread.Sleep(5); }
                if (!task.IsCompleted) throw new TimeoutException("Chat UI fixture timed out");
                task.GetAwaiter().GetResult(); Application.DoEvents();
            }
            var checks = new Dictionary<string, bool>();
            using (var window = new ChatForm(true, ChatTests.Settings(), () => ChatTests.FixtureApi()))
            {
                window.Show(); Pump(window.PreviewSendAsync("这是模拟接口测试，不会发送给中转站。"));
                checks["chat_ui_displays_streamed_reply"] = window.PreviewReplyComplete;
                using var image = new Bitmap(window.Width, window.Height); window.DrawToBitmap(image, new Rectangle(Point.Empty, window.Size)); image.Save(args[1]);
            }
            using (var window = new ChatForm(true, ChatTests.Settings(), () => ChatTests.FixtureApi(true)))
            {
                window.Show(); var request = window.PreviewSendAsync("停止测试"); window.PreviewStop(); Pump(request);
                checks["chat_stop_button_cancels_request"] = window.PreviewWasStopped;
            }
            using (var config = new ApiSettingsForm(new ChatSettings(), true))
            {
                config.Show(); Application.DoEvents();
                using var image = new Bitmap(config.Width, config.Height); config.DrawToBitmap(image, new Rectangle(Point.Empty, config.Size));
                image.Save(Path.Combine(Path.GetDirectoryName(args[1])!, "preview-api-settings.png"));
            }
            File.WriteAllText(Path.ChangeExtension(args[1], ".interaction.json"), JsonSerializer.Serialize(new { ok = checks.Values.All(x => x), checks, external_requests = 0 }, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        if (args.Length == 2 && args[0] == "--preview-clipboard-history")
        {
            using var service = new ClipboardHistoryService(new Preferences(), true);
            service.History.Add(new("text", "Leo 剪切板历史\r\n\r\n在左侧选择记录，点击复制即可再次使用。", "Leo 剪切板历史 · 示例", DateTimeOffset.Now));
            service.History.Add(new("text", "下午三点开会，记得带上设计稿。", "下午三点开会，记得带上设计稿。", DateTimeOffset.Now));
            using var window = ClipboardTool.Open(service)!;
            Application.DoEvents();
            var bar = window.Controls.OfType<FlowLayoutPanel>().Single();
            bar.Controls.OfType<Button>().Single(x => x.Text == "收藏").PerformClick();
            bool pinned = service.History.Items.Count(x => x.Pinned) == 1;
            bar.Controls.OfType<CheckBox>().Single(x => x.Text == "只看收藏").Checked = true;
            bar.Controls.OfType<Button>().Single(x => x.Text == "清空历史").PerformClick();
            var uiChecks = new Dictionary<string, bool> {
                ["clipboard_favorite_button_pins_selected_item"] = pinned,
                ["clear_history_button_keeps_favorite"] = service.History.Items.Count == 1 && service.History.Items[0].Pinned
            };
            File.WriteAllText(Path.ChangeExtension(args[1], ".interaction.json"), JsonSerializer.Serialize(new { ok = uiChecks.Values.All(x => x), checks = uiChecks }, new JsonSerializerOptions { WriteIndented = true }));
            using var bitmap = new Bitmap(window.Width, window.Height);
            window.DrawToBitmap(bitmap, new Rectangle(0, 0, window.Width, window.Height)); bitmap.Save(args[1]);
            return;
        }
        if (args.Length == 2 && args[0] == "--proxy-inspect")
        {
            var status = ClashProxy.ReadStatus();
            bool found = false;
            try { found = ClashProxy.InspectSwitch(); } catch { }
            File.WriteAllText(args[1], JsonSerializer.Serialize(new { running = status.Running, enabled = status.Enabled, description = status.Description, exact_proxy_switch_found = found, ui = ClashProxy.InspectDetails(), live_toggle_test = "not performed: preserve current network state" }, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        if (args.Length == 2 && args[0] == "--self-test")
        {
            var data = new[] { new AppEntry("Visual Studio Code", "test"), new AppEntry("微信", "test2"), new AppEntry("Code", "test3") };
            var checks = new Dictionary<string, bool> {
                ["search_prefix_ranking"] = Catalog.Search(data, "code").First().Name == "Code",
                ["search_ignores_spacing_case"] = Catalog.Search(data, "VISUAL studio").Single().Name == "Visual Studio Code",
                ["search_chinese"] = Catalog.Search(data, "微信").Single().Name == "微信",
                ["empty_search"] = Catalog.Search(data, "").Count() == 3,
                ["sprite_dimensions"] = false
            };
            using var image = Image.FromFile(Path.Combine(Store.Root, "assets", "leo.png"));
            checks["sprite_dimensions"] = image.Width == 1536 && image.Height == 2288;
            MemoryTests.Check(checks, Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
            FeatureTests.Check(checks, Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
            var discovered = Catalog.Scan();
            using var icons = new AppIcons();
            foreach (var app in discovered.Take(12)) icons.LoadNow(app);
            checks["native_app_icons_extracted"] = icons.NativeLoadedCount > 0;
            File.WriteAllText(args[1], JsonSerializer.Serialize(new { ok = checks.Values.All(x => x), checks, discovered_apps = discovered.Count, sample = discovered.Take(10).Select(x => x.Name) }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.ExitCode = checks.Values.All(x => x) ? 0 : 1;
            return;
        }
        if(args.Contains("--startup")) { Environment.CurrentDirectory=AppContext.BaseDirectory; LeoStartup.Log("登录启动已执行"); }
        bool isPreview = args.Length == 2 && args[0].StartsWith("--preview");
        using var mutex = new Mutex(true, isPreview ? "Local\\LeoLauncherPreview" + Environment.ProcessId : "Local\\LeoLauncherOriginalGoat", out bool created);
        if (!created && args.Contains("--startup")) return;
        if (!created) { MessageBox.Show("Leo 已在运行。请先右键旧版小羊并退出，再启动新版。", "Leo"); return; }
        try
        {
            var current=isPreview?new Preferences():Store.Load();
            if(!isPreview&&!File.Exists(Store.FilePath)){using var welcome=new FirstRunForm(current);if(welcome.ShowDialog()!=DialogResult.OK)return;}
            using var pet = new PetForm(current, isPreview, args.Length == 2 && args[0] == "--preview-demo");
            if (args.Length == 2 && args[0].StartsWith("--preview"))
            {
                pet.Show();
                pet.ShowLauncher();
                var previewWait = Stopwatch.StartNew();
                while (pet.IsIndexing && previewWait.ElapsedMilliseconds < 8000) { Application.DoEvents(); Thread.Sleep(15); }
                var iconWait = Stopwatch.StartNew();
                while (iconWait.ElapsedMilliseconds < 1800) { Application.DoEvents(); Thread.Sleep(15); }
                pet.SavePreview(args[1]);
                pet.SaveClipboardSearchPreview(Path.Combine(Path.GetDirectoryName(args[1])!, "preview-clipboard.png"));
                pet.SaveInteractionReport(Path.ChangeExtension(args[1], ".interaction.json"));
                return;
            }
            if(args.Contains("--startup"))pet.Shown+=(_,_)=>LeoStartup.Log("登录启动成功，Leo 已显示");
            Application.Run(pet);
        }
        catch (Exception e) { if(args.Contains("--startup"))LeoStartup.Log("启动失败："+e.GetType().Name+" · "+e.Message);else MessageBox.Show("Leo 无法启动：" + e.Message, "Leo"); }
    }
}

sealed partial class PetForm : Form
{
    readonly Preferences prefs;
    readonly Bitmap atlas;
    readonly Bitmap verticalAtlas;
    readonly LayeredPet.Surface layeredSurface=new();
    Bitmap? paintSurface;
    readonly System.Windows.Forms.Timer animation = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly LauncherForm launcher;
    readonly NotifyIcon tray;
    readonly HitSurface hit;
    readonly FootTools foot;
    ChatForm? chat;
    TodoForm? todoWindow;
    readonly bool previewMode;
    Point mouseDown, origin;
    bool dragging;
    bool launcherVisibleOnPress;
    bool movedWhileLocked;
    int row, frame;
    long rowUntil;
    int launcherOpens;
    Point previousDragPoint;
    readonly ActionDispatcher commands;
    readonly DragTransition dragTransition = new();
    readonly System.Windows.Forms.Timer fullscreenTimer = new() { Interval = 750 };
    bool hiddenForFullscreen;
    public PetForm(Preferences p, bool preview = false, bool demo = false,ApplicationCapabilityHost? applicationOverride=null,Action<string>? constructionProbe=null)
    {
        constructionProbe?.Invoke("body_enter");
        if(!preview)Modules.SetFootball(p.FootballModule);
        notices=new(preview?null:Path.Combine(Store.Root,"leo-notices.json"));focus=new(preview?null:Path.Combine(Store.Root,"leo-focus.json"));
        PlannerPhrases.Configure(p.PlannerQuotes);prefs = p;p.EntryDisplayModes??=new();p.ApplicationModules??=[];p.ModuleIcons??=new();p.ShortcutOrder??=[];p.SkinThemes??=new();p.DisabledExtensions??=[]; UiTheme.Configure(SkinSelection.Id(p),p.DisplayName,p.SkinThemes.GetValueOrDefault(SkinSelection.Id(p)));p.DisplayName=UiTheme.Name; if(!preview)ModulePaths.Configure(p.ModuleExecutables);previewMode = preview;todoStore=new(preview?null:Path.Combine(Store.Root,"leo-todos.json"));
        atlas = new Bitmap(Path.Combine(Store.Root, "assets", "leo.png"));
        verticalAtlas = new Bitmap(Path.Combine(Store.Root, "assets", "vertical-drag.png"));
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = Color.Black;
        DoubleBuffered = false; StartPosition = FormStartPosition.Manual;
        var size = Math.Clamp(p.PetSize, 80, 230);
        ClientSize = new Size(size, size * 208 / 192);
        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = p.X == -1 && p.Y == -1 ? new Point(screen.Right - Width - 36, screen.Bottom - Height - 36) : Clamp(new Point(p.X, p.Y));
        constructionProbe?.Invoke("assets_and_window_ready");
        var applicationHost=applicationOverride??new ApplicationCapabilityHost((app,token)=>{token.ThrowIfCancellationRequested();return Task.FromResult(!preview&&!File.Exists(app.Path)?RepairLocalApplication(app):app);},ApplicationCapabilityHost.StartProcess,app=>launcher?.RememberConfiguredLaunch(app),preview);
        commands=new ActionDispatcher(prefs,ExecuteLeoActionCore,preview?new ExtensionStore(Path.Combine(Path.GetTempPath(),"leo-preview-no-extensions")):null,applicationHost);
        RegisterNotesCapability();RegisterConversationCapability();RegisterTodoCapability();RegisterFootballCapability();RegisterMatchPlanning();RegisterWorkflowCapability();RegisterClientCapabilities();
        constructionProbe?.Invoke("dispatcher_ready");
        launcher = new LauncherForm(p, ResizePet, preview, demo,todoStore);if(!preview)MediaControl.Applications=()=>launcher.FileApplications;launcher.MediaRequested=ShowMedia;launcher.RecognizesCommand=commands.RecognizesExtension;launcher.CommandRequested=(text,token)=>commands.ExecuteTextAsync(text,"search",token);launcher.ActionRequested=(action,value,token)=>commands.ExecuteAsync(new(action,value,"search"),token);launcher.OriginActionRequested=(action,value,source,token)=>commands.ExecuteAsync(new(action,value,source),token);launcher.ApplicationsDropped=AddApplicationModules;launcher.RepairApplication=RepairLocalApplication;
        launcher.SharedFootballClient=footballClient;commands.ApplicationAliases=launcher.ResolveApplicationRequest;RegisterMusicCapability();launcher.SharedMusicClient=new MusicClient(musicSession!,commands);RegisterClipboardCapability();
        if(!preview){ModuleRepair.Applications=()=>InvokeRequired?(AppEntry[])Invoke(()=>launcher.FileApplications.ToArray()):launcher.FileApplications.ToArray();ModulePaths.RepairRequested=RepairBuiltinPath;}
        constructionProbe?.Invoke("launcher_ready");
        foot = new FootTools(p.PetSize, preview);foot.ProxyAction=(a,t)=>commands.ExecuteAsync(new(a,"","foot-ui"),t);
        if(!preview)ConfigureClientActivity();
        foot.ManageShortcut=ManageQuickEntry;launcher.ManageShortcut=ManageQuickEntry;foot.ConfigureModules(prefs);
        foot.ChatRequested += ShowChat;
        foot.TodosRequested+=()=>ShowTodos();
        foot.VoiceRequested+=ShowVoice;
        hit = new HitSurface { Bounds = Bounds };
        hit.AllowDrop = true;
        hit.DragEnter += (_, e) => e.Effect = DropContent.Accepts(e.Data)?DragDropEffects.Copy:DragDropEffects.None;
        hit.DragDrop += (_, e) => { if(e.Data!=null)AcceptDrop(e.Data); };
        launcher.PetBounds = () => Bounds;
        if(!preview){WindowPositions.PetBounds=()=>Bounds;WindowPositions.MonitorsChanged=OnMonitorsChanged;OperationFeedback.Sink=PresentFeedback;}
        launcher.PointerOverPet = () => hit.Bounds.Contains(Cursor.Position);
        var menu = new ContextMenuStrip();
        menu.Items.Add("每日清单",null,(_,_)=>ShowTodos());
        menu.Items.Add("设置",null,(_,_)=>OpenSettings());
        menu.Items.Add("退出 Leo", null, (_, _) => Close());
        constructionProbe?.Invoke("input_surfaces_ready");
        WireMusicMenu(menu);WireComboMenu(menu);WireCompanionMenu(menu);WireSkinMenu(menu);
        PetMenuHierarchy.Organize(menu,()=>{try{ShowNotes();}catch(Exception e){OperationFeedback.Error(e.Message);}},ShowChat,ShowVoice,ShowLauncher,()=>{launcher.Hide();using var editor=new ThemeEditor(prefs,next=>ApplySettings(next,!previewMode&&LeoStartup.Enabled),previewMode);WindowPositions.Prepare(editor,"theme",Bounds,previewMode);editor.ShowDialog();});
        var quickNoteItem=new ToolStripMenuItem("一句话速记…");quickNoteItem.Click+=(_,_)=>ShowQuickNote();menu.Items.OfType<ToolStripMenuItem>().Single(i=>i.Text=="清单与记录").DropDownItems.Insert(0,quickNoteItem);
        WireOptionalEntryMenus(menu);
        menu.Opening+=(_,_)=>{CancelPetPress();hit.Capture=false;launcher.Hide();};
        ContextMenuStrip = menu;
        hit.ContextMenuStrip = menu;
        tray = new NotifyIcon { Text = "Leo · 桌面伙伴", Icon = LeoBrand.Icon, Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => ShowLauncher();
        // Dock hover is handled by the animation timer so shrinking hit bounds cannot cancel tucking.
        hit.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) BeginPetPress(hit.PointToScreen(e.Location)); };
        hit.MouseMove += (_, e) => {
            if (!pointerPressed || noticePress || !hit.Capture || (Control.MouseButtons & MouseButtons.Left) == 0) return;
            var delta = new Size(Cursor.Position.X - mouseDown.X, Cursor.Position.Y - mouseDown.Y);
            ApplyDrag(delta);
        };
        hit.MouseUp += (_, e) => {
            if (e.Button != MouseButtons.Left || !pointerPressed) return;
            bool openNotice=noticePress&&NoticeHitBounds.Contains(PointToClient(hit.PointToScreen(e.Location)));
            bool wasNotice=noticePress;pointerPressed=false;noticePress=false;
            bool revealOnly=dockClickReveal;dockClickReveal=false;
            hit.Capture = false;
            if(wasNotice){if(openNotice)FeatureFaults.Run("通知收纳盒",ShowNotices);return;}
            if (movedWhileLocked) return;
            if (dragging) { if(prefs.EdgeDocking){var areas=DesktopDock.Displays();var area=DesktopDock.Area(Bounds,areas);dockSide=DesktopDock.Choose(Bounds,area,foot.ReservedSpace,lastDragMovement,areas);Location=DesktopDock.Snap(Bounds,area,foot.ReservedSpace,dockSide);} prefs.X = Left; prefs.Y = Top; if(!previewMode)Store.Save(prefs); dragTransition.Reset(); row = 0; frame = 0; dragging=false; BeginDockTuck(); }
            else if(!revealOnly) {
                row = 3; rowUntil = clock.ElapsedMilliseconds + 800; frame = 0;
                if (launcherVisibleOnPress) launcher.Hide(); else ShowLauncher();
            }
        };
        hit.MouseCaptureChanged+=(_,_)=>{if(!hit.Capture)CancelPetPress();};
        constructionProbe?.Invoke("menus_ready");
        Shown+=(_,_)=>{StartHoverSummary();StartDockAnimation();StartUpdateNotifications();if(!previewMode&&Store.LoadFailed)PresentFeedback(new("设置文件无法读取，已使用默认设置；损坏原文件仍保留。",true));else if(!previewMode&&AtomicJson.Recovered.Count>0)PresentFeedback(new("已从本地备份恢复数据，损坏原文件已保留。请核对最近的设置和记录。",Attention:true));};
        Shown += (_, _) => { Location = Clamp(Location); hit.Bounds = Bounds; if(!hit.Visible)hit.Show(this); PositionFoot(); if(!foot.Visible)foot.Show(this); };
        LocationChanged += (_, _) => OnDockLocationChanged();
        ClientSizeChanged += (_, _) => { SyncDockHit(); PositionFoot(); };
        animation.Interval = 140;
        FeatureFaults.Timer(animation,"桌宠动画",Animate);
        animation.Start();
        FeatureFaults.Timer(fullscreenTimer,"全屏检测",PollFullscreen);
        if (!preview) fullscreenTimer.Start();
        FeatureFaults.Timer(reminderTimer,"清单提醒",CheckReminders);tray.BalloonTipClicked+=(_,_)=>{if(reminders!=null&&!reminders.IsDisposed)reminders.Show();else ShowTodos();};if(!preview){Shown+=(_,_)=>{if(prefs.MusicModule&&prefs.LyricsHotkey!=Keys.None&&!lyricsHotkeyRegistered){tray.BalloonTipTitle=UiTheme.Name+" · 歌词快捷键";tray.BalloonTipText="歌词快捷键未启用或被占用，可在设置中调整，也可从右键音乐菜单切换歌词。";tray.ShowBalloonTip(5000);}};reminderTimer.Start();Shown+=async(_,_)=>{await SyncMatchesAsync();CheckReminders();};matchSyncTimer.Tick+=async(_,_)=>await SyncMatchesAsync();if(prefs.FootballModule)matchSyncTimer.Start();}
    }
    protected override bool ShowWithoutActivation => true;
    void ShowChat()
    {
        RevealDock(true);
        launcher.Hide(); foot.CollapseSearch();
        if (chat == null || chat.IsDisposed) chat = new ChatForm(previewMode, applicationTools: new AppTools(() => launcher.FileApplications, launcher.LaunchFromAssistant, () => prefs.Aliases,ExecuteLeoAction){Brain=CreateBrainSession(),RecognizesExtension=commands.RecognizesExtension,OriginDispatcher=(a,v,s,t)=>commands.ExecuteAsync(new(a,v,s),t)}, clipboardAction: launcher.OpenClipboardFromAssistant,onTodo:intent=>ShowTodos(intent),planner:todoStore,sessionHost:conversations);
        if (!chat.Visible) WindowPositions.Prepare(chat,"chat",Bounds,previewMode);
        chat.TopMost = false; if (chat.WindowState == FormWindowState.Minimized) chat.WindowState = FormWindowState.Normal; WindowInteraction.Show(chat);
    }
    void ShowVoice(){ShowChat();chat!.ToggleVoiceInput();}
    void ShowTodos(QuickIntent? intent=null){RevealDock(true);launcher.Hide();foot.CollapseSearch();if(todoWindow==null||todoWindow.IsDisposed)todoWindow=new TodoForm(previewMode,todoStore);if(!todoWindow.Visible)WindowPositions.Prepare(todoWindow,"todos",Bounds,previewMode);WindowInteraction.Show(todoWindow);if(intent!=null)todoWindow.Apply(intent);}
    void PollFullscreen()
    {
        if(resourcesDisposed||IsDisposed||Disposing||!IsHandleCreated)return;
        bool hide = prefs.HideInFullscreen && FullscreenMonitor.IsFullscreenOn(Screen.FromControl(this));
        SetFullscreenHidden(hide);
        if(!hide)FeatureFaults.Run("桌宠图层恢复",RestoreCompanionLayer);
    }
    void SetFullscreenHidden(bool hide)
    {
        if(resourcesDisposed||IsDisposed||Disposing||!IsHandleCreated)return;
        if (hide == hiddenForFullscreen) return;
        hiddenForFullscreen = hide;
        if (hide) { hoverCard?.Hide();feedback?.Hide();if(reminders?.Visible==true)reminders.Hide();launcher.Hide(); foot.CollapseSearch(); hit.Hide(); foot.Hide(); Hide(); }
        else { Show(); if(!hit.Visible)hit.Show(this); PositionFoot(); if(dockMotion.Progress<.001f&&!foot.Visible)foot.Show(this);if(reminders!=null&&!reminders.IsDisposed&&reminders.PendingCount>0&&!QuietHours.Load().Contains(DateTime.Now))reminders.Present([]); }
        if(hide){animation.Stop();dockTimer.Stop();hoverTimer.Stop();}else{animation.Start();if(!previewMode&&!dockDisabled)dockTimer.Start();if(!previewMode)hoverTimer.Start();}
    }
    void Animate()
    {
        if(resourcesDisposed||IsDisposed||Disposing||hiddenForFullscreen||dockMotion.Progress>=.999f)return;
        if (dragging) { int next = dragTransition.Advance(clock.ElapsedMilliseconds); if (row != next) { row = next; frame = 0; } }
        bool thinking = launcher.HasTextInput || foot.HasTextInput || chat?.IsThinking == true;
        if(focus.Active(DateTimeOffset.Now)&&!dragging&&!thinking){row=frame=0;animation.Interval=1000;Invalidate();return;}
        int desired = dragging ? row : thinking ? 8 : row != 0 && row != 8 && clock.ElapsedMilliseconds <= rowUntil ? row : 0;
        if (desired != row) { row = desired; frame = 0; }
        animation.Interval = dragging && dragTransition.IsPending ? 40 : row == 8 ? 420 : row is 11 or 12 ? 180 : 140;
        if (!(dragging && dragTransition.IsPending)) frame = (frame + 1) % (row is 1 or 2 ? 8 : row is 3 or 11 or 12 ? 4 : row==4?5:6);
        Invalidate();
    }
    static int DragRow(Size movement) => Math.Abs(movement.Height) > Math.Abs(movement.Width) ? movement.Height < 0 ? 11 : 12 : movement.Width >= 0 ? 1 : 2;
    void ApplyDrag(Size delta)
    {
        if (launcherVisibleOnPress || launcher.Visible)
        {
            if (Math.Abs(delta.Width) + Math.Abs(delta.Height) > 5) movedWhileLocked = true;
            return;
        }
        if (Math.Abs(delta.Width) + Math.Abs(delta.Height) > 5) dragging = true;
        if (dragging)
        {
            var point = origin + delta;
            var movement = new Size(point.X - previousDragPoint.X, point.Y - previousDragPoint.Y);
            previousDragPoint = point;if(movement.Width!=0||movement.Height!=0)lastDragMovement=movement;
            Location = Clamp(point);
            if (movement.Width != 0 || movement.Height != 0)
            {
                int desired = dragTransition.Request(DragRow(movement), clock.ElapsedMilliseconds);
                if (desired != row) { row = desired; frame = 0; }
            }
        }
    }
    void PositionFoot() { if (foot != null) foot.Follow(Bounds, Screen.FromControl(this).WorkingArea); }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if(!previewMode)RegisterConfiguredHotkeys();
    }
    protected override void OnHandleDestroyed(EventArgs e)
    {
        Native.UnregisterHotKey(Handle, 4);
        Native.UnregisterHotKey(Handle, 3);
        Native.UnregisterHotKey(Handle, 2);
        Native.UnregisterHotKey(Handle, 1);
        base.OnHandleDestroyed(e);
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x312&&m.WParam.ToInt32()==4){ShowQuickNote();return;}
        if(m.Msg==0x312&&m.WParam.ToInt32()==3){_=ToggleLyricsWithFeedback();return;}
        if(m.Msg==0x312&&m.WParam.ToInt32()==2){ShowVoice();return;}
        if (m.Msg == 0x312 && m.WParam.ToInt32() == 1)
        {
            ToggleLauncher();
            return;
        }
        base.WndProc(ref m);
    }
    Point Clamp(Point point)
    {
        var area = Screen.FromPoint(point).WorkingArea;
        var margin = Math.Max(0, ((foot?.ScaledCompactWidth ?? FootTools.CompactWidth) - Width) / 2);
        return new(Math.Clamp(point.X, area.Left + margin, Math.Max(area.Left + margin, area.Right - Width - margin)), Math.Clamp(point.Y, area.Top, Math.Max(area.Top, area.Bottom - Height - (foot?.ReservedSpace ?? FootTools.Space))));
    }
    protected override CreateParams CreateParams {get{var value=base.CreateParams;value.ExStyle|=LayeredPet.Style;return value;}}
    internal int LayeredFrames{get;private set;}
    protected override void OnPaintBackground(PaintEventArgs e){}
    protected override void OnPaint(PaintEventArgs e)
    {
        if(resourcesDisposed||ClientSize.Width<1||ClientSize.Height<1)return;
        if(paintSurface?.Size!=ClientSize){paintSurface?.Dispose();paintSurface=new Bitmap(ClientSize.Width,ClientSize.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);}
        PaintPetSurface(paintSurface);
        layeredSurface.Present(Handle,Location,paintSurface);LayeredFrames++;
    }
    internal Bitmap RenderPetSurface()
    {
        var surface=new Bitmap(ClientSize.Width,ClientSize.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        PaintPetSurface(surface);return surface;
    }
    void PaintPetSurface(Bitmap surface)
    {
        using(var drawing=Graphics.FromImage(surface)){
            drawing.Clear(Color.Transparent);
            drawing.InterpolationMode=InterpolationMode.HighQualityBicubic;
            DrawPet(drawing,PetDrawingBounds(ClientSize));
        }
        PetTransparency.ResolveCoverage(surface);
    }
    void DrawPet(Graphics graphics, RectangleF destination)
    {
        if(DrawDragPickup(graphics,destination))return;try{if(!dockDisabled&&DrawDockPet(graphics,destination))return;}catch(Exception e)when(FeatureFaults.Recoverable(e)){DisableDockingForSession();FeatureFaults.Report("吸附动画",e,()=>{dockDisabled=false;dockTimer.Start();return Task.CompletedTask;});}
        DrawAnimationFrame(graphics,destination);
        DrawCompanionBadges(graphics,destination);
    }
    static RectangleF PetDrawingBounds(Size size)
    {
        float scale = Math.Min(size.Width / 192f, size.Height / 208f);
        float width = 192 * scale, height = 208 * scale;
        return new RectangleF((size.Width - width) / 2, size.Height - height, width, height);
    }
    public void ShowLauncher()
    {
        RevealDock(true);
        if (hiddenForFullscreen) SetFullscreenHidden(false);
        launcherOpens++;
        var area = Screen.FromControl(this).WorkingArea;
        var layout = PopupPlacement.Arrange(Bounds, launcher.Size, area);
        if (!launcher.Visible) Location = layout.Pet;
        launcher.Location = layout.Popup;
        launcher.Open();
    }
    void ToggleLauncher()
    {
        if (launcher.Visible) launcher.Hide(); else ShowLauncher();
    }
    void ResizePet()
    {
        RevealDock(true);
        foot.ApplyScale(prefs.PetSize);
        ClientSize = new Size(prefs.PetSize, prefs.PetSize * 208 / 192);
        Location = Clamp(Location);
        if (launcher.Visible)
        {
            var layout = PopupPlacement.Arrange(Bounds, launcher.Size, Screen.FromControl(this).WorkingArea);
            Location = layout.Pet; launcher.Location = layout.Popup;
        }
    }
    void OpenSettings()
    {
        RevealDock(true);
        launcher.Hide();
        if(settingsWindow==null||settingsWindow.IsDisposed)settingsWindow=new SettingsForm(prefs,ApplySettings,()=>chat?.ReloadConfiguration(),previewMode,SetHotkeyCapture,()=>launcher.FileApplications,ShortcutStatus,()=>{todoStore.ReloadImported();launcher.ReloadImportedConfiguration();todoWindow?.View.Render();});
        if(settingsWindow.WindowState==FormWindowState.Minimized)settingsWindow.WindowState=FormWindowState.Normal;
        if(!settingsWindow.Visible)WindowPositions.Prepare(settingsWindow,"settings",Bounds,previewMode);
        WindowInteraction.Show(settingsWindow);
    }
    public void SavePreview(string path)
    {
        using var raw = new Bitmap(launcher.Width, launcher.Height);
        launcher.DrawToBitmap(raw, launcher.ClientRectangle);
        using var image = new Bitmap(launcher.Width, launcher.Height);
        using (var painter = Graphics.FromImage(image))
        {
            if (launcher.Region != null) painter.SetClip(launcher.Region, CombineMode.Replace);
            painter.DrawImageUnscaled(raw, 0, 0);
        }
        image.Save(path);
        var union = Rectangle.Union(Rectangle.Union(Bounds, foot.Bounds), launcher.Bounds);
        using var scene = new Bitmap(union.Width + 24, union.Height + 24);
        using var graphics = Graphics.FromImage(scene);
        graphics.Clear(Color.FromArgb(53, 61, 78));
        graphics.DrawImage(image, launcher.Left - union.Left + 12, launcher.Top - union.Top + 12);
        var petBounds = PetDrawingBounds(ClientSize);
        petBounds.Offset(Left - union.Left + 12, Top - union.Top + 12);
        DrawPet(graphics, petBounds);
        using var tools = new Bitmap(foot.Width, foot.Height);
        foot.DrawToBitmap(tools, foot.ClientRectangle);
        var state = graphics.Save();
        graphics.TranslateTransform(foot.Left - union.Left + 12, foot.Top - union.Top + 12);
        if (foot.Region != null) graphics.SetClip(foot.Region, CombineMode.Replace);
        graphics.DrawImageUnscaled(tools, 0, 0); graphics.Restore(state);
        scene.Save(Path.Combine(Path.GetDirectoryName(path)!, "preview-desktop.png"));
    }
    public bool IsIndexing => launcher.IsIndexing;
    public void SaveClipboardSearchPreview(string path)
    {
        launcher.PreviewClipboardSearch();
        var wait = Stopwatch.StartNew();
        while (wait.ElapsedMilliseconds < 400) { Application.DoEvents(); Thread.Sleep(15); }
        SavePreview(path);
    }
    public void SaveInteractionReport(string path)
    {
        var checks = new Dictionary<string, bool>();
        launcher.Hide(); Application.DoEvents();
        foreach (bool deactivateBeforePress in new[] { true, false })
        {
            ShowLauncher(); var opens = launcherOpens;
            if (deactivateBeforePress) launcher.ProcessDeactivation(true);
            hit.SimulateClick(new Point(Width / 2, Height / 2), () => launcher.ProcessDeactivation(true));
            var settle = Stopwatch.StartNew();
            while (settle.ElapsedMilliseconds < 200) { Application.DoEvents(); Thread.Sleep(10); }
            checks[deactivateBeforePress ? "focus_loss_before_press_stays_closed" : "focus_loss_between_press_release_stays_closed"] = !launcher.Visible && launcherOpens == opens;
        }
        ShowLauncher(); launcher.ProcessDeactivation(false);
        checks["outside_focus_loss_still_closes"] = !launcher.Visible;
        var points = new Dictionary<string, Point> {
            ["head"] = new Point(Width / 2, Height / 4),
            ["belly"] = new Point(Width / 2, Height * 3 / 5),
            ["feet"] = new Point(Width / 2, Height * 9 / 10)
        };
        checks["sprite_preserves_original_aspect_ratio"] = new[] { new Size(80, 86), new Size(117, 126), new Size(230, 249), new Size(230, 100), new Size(100, 230) }.All(size => {
            var bounds = PetDrawingBounds(size);
            return Math.Abs(bounds.Width / bounds.Height - 192f / 208f) < .00001 && bounds.Width <= size.Width + .001 && bounds.Height <= size.Height + .001;
        });
        checks["clipboard_search_returns_tool_in_icon_row"] = launcher.PreviewClipboardSearch();
        checks["favorites_menu_updates_row"] = launcher.PreviewFavorite();
        checks["size_slider_updates_pet"] = launcher.PreviewResize() && Width == prefs.PetSize;
        launcher.PreviewSize(80); var smallFooter = foot.Size;
        bool smallIcons = foot.Controls.OfType<GlyphButton>().All(x => x.Width == (int)Math.Round(28 * 80 / 130f));
        launcher.PreviewSize(230);
        checks["slider_scales_both_footer_icons_and_container"] = smallIcons && foot.Width > smallFooter.Width && foot.Height > smallFooter.Height
            && foot.Controls.OfType<GlyphButton>().All(x => x.Width == (int)Math.Round(28 * 230 / 130f));
        launcher.PreviewSize(160);
        ShowLauncher();
        checks["popup_does_not_cover_leo"] = !launcher.Bounds.IntersectsWith(Bounds);
        checks["compact_layout_has_no_section_heading"] = launcher.Height == 248 && !launcher.Controls.OfType<Label>().Any(x => x.Text.Contains("最近使用"));
        checks["application_grid_seven_icons_two_rows"] = launcher.VerifyTwoRows();
        checks["favorite_tile_drop_event_reorders_icons"] = launcher.VerifyFavoriteDrop();
        launcher.PreviewTyping("test"); Animate();
        checks["application_text_triggers_slow_thinking"] = row == 8 && animation.Interval == 420;
        int thoughtStart = frame; Animate(); Animate();
        checks["thinking_keeps_animating"] = row == 8 && frame != thoughtStart;
        launcher.PreviewTyping(""); Animate();
        checks["clearing_text_stops_thinking"] = row == 0;
        foot.PreviewTyping("test"); Animate();
        checks["edge_text_triggers_slow_thinking"] = row == 8 && animation.Interval == 420;
        foot.CollapseSearch(); Animate();
        checks["closing_edge_stops_thinking"] = row == 0;
        checks["vertical_drag_direction_and_assets"] = DragRow(new Size(1, -20)) == 11 && DragRow(new Size(1, 20)) == 12 && verticalAtlas.Size == new Size(768, 416);
        checks["foot_icons_survive_twenty_expand_redraw_cycles"] = foot.VerifyRepeatedRendering();
        var fixedPosition = Location;
        hit.SimulateClick(new Point(Width / 2, Height / 2), () => ApplyDrag(new Size(30, -20)));
        checks["drag_while_search_open_keeps_position_and_search"] = Location == fixedPosition && launcher.Visible;
        launcher.Hide(); launcherVisibleOnPress = false; origin = Location; dragging = false;
        ApplyDrag(new Size(30, -20));
        checks["drag_when_search_closed_moves_pet"] = Location != fixedPosition;
        origin = fixedPosition; previousDragPoint = origin; dragTransition.Reset();
        ApplyDrag(new Size(0, -20));
        checks["upward_drag_plays_new_frames"] = row == 11;
        ApplyDrag(new Size(0, 20));
        checks["direction_change_waits_before_switch"] = row == 11;
        var transition = new DragTransition();
        transition.Request(11, 0); transition.Request(12, 10);
        checks["direction_transition_holds_then_neutral_then_new_animation"] = transition.Advance(149) == 11 && transition.Advance(150) == 0 && transition.Advance(249) == 0 && transition.Advance(250) == 12;
        transition.Request(1, 300); transition.Request(12, 350);
        checks["brief_direction_jitter_is_cancelled"] = transition.Advance(600) == 12 && !transition.IsPending;
        dragTransition.Reset(); ApplyDrag(new Size(0, 40));
        checks["downward_drag_plays_new_frames"] = row == 12;
        Location = fixedPosition; dragging = false; row = 0;
        checks["foot_tools_follow_below_pet"] = foot.Top == Bottom + foot.Gap;
        checks["compact_foot_icons_toggle_search_input"] = foot.VerifySearchToggle();
        checks["footer_search_icon_is_last"] = foot.VerifyButtonOrder();
        checks["terminal_requests_elevation_only_on_click"] = FootTools.TerminalStart().Verb == "runas";
        checks["edge_query_escaped"] = FootTools.SearchUrl("a & 中文") == "https://www.bing.com/search?q=a%20%26%20%E4%B8%AD%E6%96%87";
        launcher.Hide(); Application.DoEvents();
        foreach (var pair in points)
        {
            checks[pair.Key + "_os_hit_test"] = hit.ReceivesPoint(pair.Value);
            var count = launcherOpens;
            hit.SimulateClick(pair.Value); Application.DoEvents();
            checks[pair.Key + "_opens_launcher"] = launcherOpens == count + 1 && launcher.Visible;
            hit.SimulateClick(pair.Value); Application.DoEvents();
            checks[pair.Key + "_second_click_closes_launcher"] = !launcher.Visible && launcherOpens == count + 1;
            launcher.Hide(); Application.DoEvents();
        }
        SetFullscreenHidden(true);
        checks["fullscreen_hides_pet_hit_surface_footer_and_search"] = !Visible && !hit.Visible && !foot.Visible && !launcher.Visible;
        SetFullscreenHidden(false);
        checks["leaving_fullscreen_restores_pet_and_footer"] = Visible && hit.Visible && foot.Visible;
        var folder = Path.Combine(Path.GetDirectoryName(path)!, "checks"); Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "dropped example.txt"); File.WriteAllText(file, "preview fixture");
        var fileData = new DataObject(DataFormats.FileDrop, new[] { file });
        var drop = new DragEventArgs(fileData, 0, Left + Width / 2, Top + Height / 2, DragDropEffects.Copy, DragDropEffects.None);
        typeof(Control).GetMethod("OnDragEnter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(hit, new object[] { drop });
        typeof(Control).GetMethod("OnDragDrop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(hit, new object[] { drop });
        Application.DoEvents();
        var actions = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "Leo · 文件操作");
        checks["file_drop_event_opens_action_picker_without_launching_file"] = drop.Effect == DragDropEffects.Copy && actions != null;
        if (actions != null)
        {
            using var image = new Bitmap(actions.Width, actions.Height);
            actions.DrawToBitmap(image, new Rectangle(Point.Empty, actions.Size));
            image.Save(Path.Combine(Path.GetDirectoryName(path)!, "preview-file-actions.png")); actions.Close();
        }
        checks["footer_utilities_are_hidden_until_arrow_click"]=foot.VerifyDisclosure();foot.PreviewUtilities();
        foot.Controls.OfType<GlyphButton>().Single(x => x.AccessibleName == "问 Leo").PerformClick();
        checks["chat_bubble_opens_native_chat_window"] = chat?.Visible == true;
        checks["chat_is_independent_non_topmost_window"] = chat != null && chat.Owner == null && !chat.TopMost;
        checks["football_button_opens_independent_panel"] = launcher.VerifyFootballEntry();
        checks["music_entry_opens_independent_panel"]=launcher.VerifyMusicEntry();
        ShowTodos();checks["daily_list_has_independent_non_topmost_window"]=todoWindow!=null&&todoWindow.Visible&&todoWindow.Owner==null&&!todoWindow.TopMost;checks["daily_list_keeps_requested_initial_position"]=todoWindow!=null&&todoWindow.StartPosition==FormStartPosition.Manual&&todoWindow.Location==PopupPlacement.Place(Bounds,todoWindow.Size,Screen.FromControl(this).WorkingArea);todoWindow?.Close();
        var voiceMessage=Message.Create(Handle,0x312,(IntPtr)2,IntPtr.Zero);WndProc(ref voiceMessage);checks["voice_shortcut_opens_chat_directly"]=chat?.Visible==true&&chat.PreviewState=="示例模式，不启用麦克风";
        chat?.Close();
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok = checks.Values.All(x => x), checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
    bool resourcesDisposed;
    void ReleaseResources()
    {
        if(resourcesDisposed)return;resourcesDisposed=true;
        layeredSurface.Dispose();paintSurface?.Dispose();paintSurface=null;
        updateNotifications?.Dispose();quickNoteWindow?.Dispose();companionTimer.Stop();companionTimer.Dispose();noticeWindow?.Dispose();focusWindow?.Dispose();dockBridge?.Dispose();foreach(var skin in animationSkins.Values)skin.Dispose();animationSkins.Clear();
        dockTimer.Stop();dockTimer.Dispose();dockSprites?.Dispose();var pickup=dragPickup;dragPickup=null;pickup?.Dispose();
        fullscreenTimer.Stop();fullscreenTimer.Dispose();animation.Stop();animation.Dispose();hoverTimer.Stop();reminderTimer.Stop();matchSyncTimer.Stop();
        notesWindow?.Dispose();comboCancellation?.Cancel();footballQueries?.Dispose();conversations?.Dispose();commands.Dispose();todoStore.MatchImporter=null;if(!previewMode){ClientOperations.Hub.Dispose();OwnedHelpers.Shutdown();}
        hoverTimer.Dispose();hoverCard?.Dispose();dropWindow?.Dispose();if(!previewMode){ModulePaths.RepairRequested=null;ModuleRepair.Applications=null;MediaControl.Applications=null;OperationFeedback.Sink=null;WindowPositions.MonitorsChanged=null;WindowPositions.PetBounds=null;}feedback?.Dispose();settingsWindow?.Dispose();mediaWindow?.Dispose();matchSyncTimer.Dispose();matchSyncLife.Cancel();matchSyncLife.Dispose();reminders?.Close();reminders?.Dispose();reminderTimer.Dispose();todoWindow?.Close();todoWindow?.Dispose();chat?.Close(); chat?.Dispose(); foot.Dispose(); hit.Dispose(); launcher.Dispose(); tray.Dispose(); atlas.Dispose(); verticalAtlas.Dispose();
    }
    protected override void OnFormClosed(FormClosedEventArgs e){ReleaseResources();base.OnFormClosed(e);}
    protected override void Dispose(bool disposing){if(disposing)ReleaseResources();base.Dispose(disposing);}
}
