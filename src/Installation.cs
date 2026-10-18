using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
namespace LeoLauncher;

sealed class ReleaseManifest
{
    public string Version{get;set;}="";
    public Dictionary<string,string> Files{get;set;}=new(StringComparer.OrdinalIgnoreCase);
}
sealed class InstallBackup
{
    public string Target{get;set;}="";
    public Dictionary<string,bool> Files{get;set;}=new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string,string> Hashes{get;set;}=new(StringComparer.OrdinalIgnoreCase);
}
static class LeoInstaller
{
    internal const string ManifestName="release-manifest.json";
    static readonly JsonSerializerOptions json=new(){WriteIndented=true};
    internal static string Inside(string root,string relative)
    {
        if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains(':')||relative.Split('/','\\').Any(s=>s is ".." or "." or ""))throw new IOException("发布包包含不安全的文件路径。");
        string path=Path.GetFullPath(Path.Combine(root,relative));string prefix=Path.GetFullPath(root).TrimEnd('\\','/')+Path.DirectorySeparatorChar;
        if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("文件超出了安装目录。");
        for(var entry=new FileInfo(path) as FileSystemInfo;entry!=null;entry=entry is DirectoryInfo dir?dir.Parent:((FileInfo)entry).Directory){if(entry.Exists&&(entry.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("安装路径不能包含链接目录。");}
        return path;
    }
    internal static bool Allowed(string name)
    {
        string normalized=name.Replace('\\','/');string file=Path.GetFileName(name);
        if(normalized is "assets/clubs.json" or "assets/pinyin.json")return true;
        if(normalized.StartsWith("assets/",StringComparison.OrdinalIgnoreCase))return Path.GetExtension(file).ToLowerInvariant() is ".png" or ".ico" or ".jpg" or ".webp" or ".txt" or ".md";
        if(normalized.StartsWith("licenses/",StringComparison.OrdinalIgnoreCase))return Path.GetExtension(file).ToLowerInvariant() is ".txt" or ".md" or "";
        if(System.Text.RegularExpressions.Regex.IsMatch(normalized,@"^[a-z]{2,3}(?:-[A-Za-z]{2,4})?/[^/]+\.resources\.dll$"))return true;
        if(normalized.Contains('/'))return false;
        return file is "LeoLauncher.runtimeconfig.json" or "LeoLauncher.deps.json" or "release-manifest.json" or "USER_GUIDE.md" or "RELEASE_NOTES.md" or "leo.ico"||Path.GetExtension(file).ToLowerInvariant() is ".dll" or ".exe";
    }
    internal static ReleaseManifest Verify(string source)
    {
        var manifest=JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(Path.Combine(source,ManifestName)))??throw new IOException("缺少发布清单");
        if(!manifest.Files.ContainsKey("LeoLauncher.exe")||!manifest.Files.ContainsKey("LeoLauncher.dll")||!manifest.Files.ContainsKey("coreclr.dll")||!manifest.Files.ContainsKey("System.Windows.Forms.dll"))throw new IOException("发布包缺少必要运行文件。");
        var unique=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var pair in manifest.Files){if(!Allowed(pair.Key)||pair.Key.Equals(ManifestName,StringComparison.OrdinalIgnoreCase)||!unique.Add(pair.Key))throw new IOException("发布文件清单不合法："+pair.Key);string path=Inside(source,pair.Key);if(pair.Value.Length!=64||!File.Exists(path)||!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).Equals(pair.Value,StringComparison.OrdinalIgnoreCase))throw new IOException("发布文件损坏，请重新下载："+Path.GetFileName(pair.Key));}
        return manifest;
    }
    static void CopyAtomic(string from,string to){Directory.CreateDirectory(Path.GetDirectoryName(to)!);string temp=to+".leo-install-"+Guid.NewGuid().ToString("N");try{File.Copy(from,temp);File.Move(temp,to,true);}finally{if(File.Exists(temp))File.Delete(temp);}}
    internal static void StopTarget(string target)
    {
        string exe=Path.GetFullPath(Path.Combine(target,"LeoLauncher.exe"));foreach(var process in Process.GetProcessesByName("LeoLauncher")){using(process){if(process.Id==Environment.ProcessId)continue;string? actual;try{actual=process.MainModule?.FileName;}catch{continue;}if(!string.Equals(actual,exe,StringComparison.OrdinalIgnoreCase))continue;if(process.CloseMainWindow())process.WaitForExit(2500);if(!process.HasExited){process.Kill();process.WaitForExit(3000);if(!process.HasExited)throw new IOException("请退出安装目录中的 Leo 后重试。");}}}
    }
    internal static string Install(string source,string target,Action<string>? progress=null,Action<string,string>? copy=null,bool stop=true)
    {
        source=Path.GetFullPath(source).TrimEnd('\\','/');target=Path.GetFullPath(target).TrimEnd('\\','/');
        if(target.Length<4||source.StartsWith(target+"\\",StringComparison.OrdinalIgnoreCase)||target.StartsWith(source+"\\",StringComparison.OrdinalIgnoreCase)||source.Equals(target,StringComparison.OrdinalIgnoreCase))throw new IOException("请选择与解压目录分开的 Leo 专用安装文件夹。");
        Inside(target,"LeoLauncher.exe");var manifest=Verify(source);using var mutex=new Mutex(false,"Local\\LeoInstall"+Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(target.ToUpperInvariant()))));if(!mutex.WaitOne(0))throw new IOException("这个目录正在更新，请稍后重试。");
        try{
            if(Directory.Exists(target)&&Directory.EnumerateFileSystemEntries(target).Any()&&!File.Exists(Path.Combine(target,"LeoLauncher.exe")))throw new IOException("此文件夹含有其他文件，请选择空文件夹或已有的 Leo 安装目录。");
            if(stop)StopTarget(target);Directory.CreateDirectory(target);progress?.Invoke("正在备份程序与配置…");
            string backup=Inside(target,"update-backups/"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);Directory.CreateDirectory(backup);
            var names=new HashSet<string>(manifest.Files.Keys,StringComparer.OrdinalIgnoreCase){ManifestName};string previous=Path.Combine(target,ManifestName);
            if(File.Exists(previous)){var old=JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(previous))??throw new IOException("原版本发布清单损坏，请先恢复备份。");foreach(string name in old.Files.Keys){if(!Allowed(name))throw new IOException("原版本发布清单包含非程序文件。");Inside(target,name);names.Add(name);}}
            var record=new InstallBackup{Target=target};foreach(string name in names){string current=Inside(target,name);bool exists=File.Exists(current);record.Files[name]=exists;if(exists){CopyAtomic(current,Inside(backup,"runtime/"+name));record.Hashes[name]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(current)));}}
            foreach(string config in Directory.EnumerateFiles(target,"leo-*.json",SearchOption.TopDirectoryOnly)){if((File.GetAttributes(config)&FileAttributes.ReparsePoint)!=0)throw new IOException("配置文件不能是链接。");CopyAtomic(config,Inside(backup,"config/"+Path.GetFileName(config)));}
            File.WriteAllText(Path.Combine(backup,"backup.json"),JsonSerializer.Serialize(record,json));
            try{progress?.Invoke("正在安装并校验…");copy??=CopyAtomic;foreach(string name in manifest.Files.Keys)copy(Inside(source,name),Inside(target,name));CopyAtomic(Path.Combine(source,ManifestName),Path.Combine(target,ManifestName));Verify(target);
                foreach(string name in names.Where(n=>n!=ManifestName&&!manifest.Files.ContainsKey(n))){string path=Inside(target,name);if(File.Exists(path))File.Delete(path);}
                File.WriteAllText(Path.Combine(backup,"complete"),"ok");return backup;
            }catch{Restore(target,backup,stop:false);throw;}
        }finally{mutex.ReleaseMutex();}
    }
    internal static string? LatestBackup(string target)
    {
        string root=Inside(target,"update-backups");if(!Directory.Exists(root))return null;return Directory.EnumerateDirectories(root).OrderDescending(StringComparer.OrdinalIgnoreCase).FirstOrDefault(d=>File.Exists(Path.Combine(d,"complete"))&&File.Exists(Path.Combine(d,"runtime",ManifestName)));
    }
    internal static void Restore(string target,string backup,bool stop=true)
    {
        target=Path.GetFullPath(target).TrimEnd('\\','/');string checkedBackup=Inside(target,Path.GetRelativePath(target,backup));
        if(!checkedBackup.StartsWith(Path.Combine(target,"update-backups")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("备份不属于当前安装。");
        var record=JsonSerializer.Deserialize<InstallBackup>(File.ReadAllText(Inside(checkedBackup,"backup.json")))??throw new IOException("备份记录损坏");if(!record.Target.Equals(target,StringComparison.OrdinalIgnoreCase))throw new IOException("备份对应其他安装目录。");
        foreach(var pair in record.Files){if(!Allowed(pair.Key))throw new IOException("备份含非程序文件");Inside(target,pair.Key);if(pair.Value){string saved=Inside(backup,"runtime/"+pair.Key);if(!File.Exists(saved)||!record.Hashes.TryGetValue(pair.Key,out var hash)||!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(saved))).Equals(hash,StringComparison.OrdinalIgnoreCase))throw new IOException("备份文件缺失或损坏，未修改现有程序。");}}
        if(stop)StopTarget(target);foreach(var pair in record.Files){string path=Inside(target,pair.Key);if(pair.Value)CopyAtomic(Inside(backup,"runtime/"+pair.Key),path);else if(File.Exists(path))File.Delete(path);}
        // Settings remain current. Their pre-update copies are available separately in config/.
    }
}

sealed class InstallForm:BarcaWindow
{
    readonly TextBox path=new(){BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text};bool busy;
    public InstallForm()
    {
        Text="Leo · 安装与更新";ClientSize=new(590,355);BackColor=BarcaUi.Canvas;ForeColor=BarcaUi.Text;Font=BarcaUi.Font(10);StartPosition=FormStartPosition.CenterScreen;MaximizeBox=false;
        var chrome=BarcaUi.Chrome(this,"LEO  /  安装与更新","首次安装无需开发环境。更新保留个人数据，并自动备份。");
        path.Bounds=new(24,116,430,30);path.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Leo");var browse=new BarcaButton{Text="选择…",Bounds=new(466,114,98,33)};
        browse.Click+=(_,_)=>{using var picker=new FolderBrowserDialog{Description="选择 Leo 专用文件夹",UseDescriptionForTitle=true,SelectedPath=path.Text};if(picker.ShowDialog(this)==DialogResult.OK)path.Text=picker.SelectedPath;};
        var shortcut=new CheckBox{Text="创建开始菜单入口",Checked=true,Bounds=new(24,164,250,29),ForeColor=BarcaUi.Text};var launch=new CheckBox{Text="完成后启动 Leo",Checked=true,Bounds=new(300,164,250,29),ForeColor=BarcaUi.Text};
        var status=new Label{Text="更新前会备份程序和个人配置；安装失败自动恢复程序。",Bounds=new(24,210,540,67),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)};var install=new BarcaButton{Text="安装 / 更新",Bounds=new(424,294,140,36)};var rollback=new BarcaButton{Text="回退上次更新",Bounds=new(258,294,150,36)};
        void Progress(string text){if(IsHandleCreated&&!IsDisposed)BeginInvoke((Action)(()=>status.Text=text));}
        async Task Execute(bool restore){if(busy)return;string target=path.Text.Trim();bool makeShortcut=shortcut.Checked,start=launch.Checked;busy=true;install.Enabled=rollback.Enabled=browse.Enabled=path.Enabled=shortcut.Enabled=launch.Enabled=false;
            try{await Task.Run(()=>{if(restore){string backup=LeoInstaller.LatestBackup(target)??throw new IOException("没有可回退的旧版本。");LeoInstaller.Restore(target,backup);Progress("已恢复上次程序，当前个人配置保留。");}else{LeoInstaller.Install(AppContext.BaseDirectory,target,Progress);Progress("安装完成，个人配置已保留。");}});
                if(makeShortcut){try{CreateShortcut(target);}catch{status.Text="安装成功，开始菜单入口创建失败；可直接启动安装目录里的 Leo。";}}
                if(start)Process.Start(new ProcessStartInfo(Path.Combine(target,"LeoLauncher.exe")){UseShellExecute=true,WorkingDirectory=target,WindowStyle=ProcessWindowStyle.Hidden});
            }catch(Exception e){status.Text="未完成："+e.Message;}finally{busy=false;install.Enabled=rollback.Enabled=browse.Enabled=path.Enabled=shortcut.Enabled=launch.Enabled=true;}}
        install.Click+=async(_,_)=>await Execute(false);rollback.Click+=async(_,_)=>await Execute(true);FormClosing+=(_,e)=>{if(busy){e.Cancel=true;status.Text="正在安全写入或恢复，请稍候完成。";}};
        Controls.AddRange([path,browse,shortcut,launch,status,install,rollback,chrome]);MinimumSize=MaximumSize=Size;
    }
    static void CreateShortcut(string target){Type type=Type.GetTypeFromProgID("WScript.Shell")??throw new IOException("无法创建入口");dynamic shell=Activator.CreateInstance(type)!;dynamic link=shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Leo.lnk"));link.TargetPath=Path.Combine(target,"LeoLauncher.exe");link.WorkingDirectory=target;link.IconLocation=Path.Combine(target,"leo.ico");link.Save();System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
}

sealed class FirstRunForm:BarcaWindow
{
    public FirstRunForm(Preferences prefs)
    {
        Text="欢迎使用 Leo";ClientSize=new(510,480);BackColor=BarcaUi.Canvas;ForeColor=BarcaUi.Text;Font=BarcaUi.Font(10);StartPosition=FormStartPosition.CenterScreen;MaximizeBox=MinimizeBox=false;
        var header=BarcaUi.Chrome(this,"HOLA  /  你好，Leo","应用搜索、清单与聊天始终可用，其他功能按需开启。");
        var fields=new Dictionary<string,CheckBox>();int y=111;foreach(var (id,name) in new[]{("terminal","管理员终端"),("clash","Clash Verge · 系统代理"),("music","QQ 音乐 · 快速点歌"),("edge","Edge · 搜索与观赛网址"),("football","足球 · 赛程与关注球队")}){var check=new CheckBox{Text=name,Checked=id is "edge" or "football",Bounds=new(26,y,450,32),ForeColor=BarcaUi.Text};fields[id]=check;Controls.Add(check);y+=46;}
        var note=new Label{Text="设置中可随时更改。客户端需自行安装；聊天 API 与本地语音可稍后配置。",Bounds=new(26,351,450,48),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)};var start=new BarcaButton{Text="开始使用",Bounds=new(349,418,135,36)};
        start.Click+=(_,_)=>{prefs.TerminalModule=fields["terminal"].Checked;prefs.ClashModule=fields["clash"].Checked;prefs.MusicModule=fields["music"].Checked;prefs.EdgeModule=fields["edge"].Checked;prefs.FootballModule=fields["football"].Checked;try{File.WriteAllText(Store.FilePath,JsonSerializer.Serialize(prefs,new JsonSerializerOptions{WriteIndented=true}));DialogResult=DialogResult.OK;}catch{note.Text="当前目录无法保存，请使用安装入口选择可写的安装文件夹。";}};
        Controls.AddRange([note,start,header]);AcceptButton=start;MinimumSize=MaximumSize=Size;
    }
}
