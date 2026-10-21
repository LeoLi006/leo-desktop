using System.Text.Json;
using System.Text.RegularExpressions;
namespace LeoLauncher;

sealed record ActionRequest(string Action,string Value="",string Source="ui",string? RequestId=null,string? TraceId=null,DateTimeOffset? Deadline=null);
sealed record ActionReceipt(DateTimeOffset Time,string Action,string Source,bool Success,string Message);
interface ILeoModule
{
    string Id{get;} string Name{get;} string Version{get;}
    bool Enabled{get;} IReadOnlyList<string> Actions{get;}
    Task<string> ExecuteAsync(ActionRequest request,CancellationToken token);
}
sealed class BuiltinLeoModule(string id,string name,Func<bool> enabled,string[] actions,Func<string,string,CancellationToken,Task<string>> execute):ILeoModule
{
    public string Id=>id;public string Name=>name;public string Version=>typeof(Program).Assembly.GetName().Version?.ToString()??"1.0";public bool Enabled=>enabled();public IReadOnlyList<string> Actions=>actions;
    public Task<string> ExecuteAsync(ActionRequest request,CancellationToken token){if(!Enabled)throw new InvalidOperationException(Name+"已停用，请在设置 → 功能中开启。");return execute(request.Action,request.Value,token);}
}
static class ModuleCatalog
{
    public static readonly (string Id,string Name)[] Builtins=[("core","核心功能"),("terminal","管理员终端"),("clash","Clash Verge"),("music","QQ 音乐"),("football","足球"),("edge","Edge"),("media","统一媒体控制")];
    public static string Owner(string action)=>action.StartsWith("football_")||action=="match_add"?"football":action.StartsWith("music_")?"music":action.StartsWith("media_")?"media":action.StartsWith("proxy_")?"clash":action=="terminal_open"?"terminal":action is "web_search" or "web_open" or "watch_open"?"edge":"core";
}
sealed partial class ActionDispatcher:IDisposable
{
    public bool RecognizesExtension(string text)=>Extensions.Load().Any(m=>m.Commands.Any(c=>c.Title.Equals(text.Trim(),StringComparison.OrdinalIgnoreCase)||c.Phrases.Any(p=>p.Equals(text.Trim(),StringComparison.OrdinalIgnoreCase))));
    public async Task<string> ExecuteTextAsync(string text,string source,CancellationToken token=default)
    {
        text=text.Trim();if(LocalPath.LooksLike(text))return await ExecuteAsync(new("path_open",text,source),token);if(text.StartsWith("ext:"))return await ExecuteAsync(new(text,"",source),token);
        var intent=QuickRouter.Route(text);var action=CommandTranslation.From(intent,source);if(action!=null)return await ExecuteAsync(action,token);
        var matches=Extensions.Load().SelectMany(m=>m.Commands.Select(c=>(Module:m,Command:c))).Where(x=>x.Command.Title.Equals(text.Trim(),StringComparison.OrdinalIgnoreCase)||x.Command.Phrases.Any(p=>p.Equals(text.Trim(),StringComparison.OrdinalIgnoreCase))).ToArray();if(matches.Length>1)throw new ArgumentException("有多个扩展使用同一口令，请修改扩展口令后重试。");var extension=matches.FirstOrDefault();
        if(extension.Module!=null)return await ExecuteAsync(new("ext:"+extension.Module.Id+":"+extension.Command.Id,"",source),token);
        var draft=NaturalTodo.Parse(text,DateTime.Now,DateOnly.FromDateTime(DateTime.Today));
        if(draft!=null)return await ExecuteAsync(new("todo_natural",JsonSerializer.Serialize(draft),source),token);
        throw new ArgumentException("没有识别到本地指令。可输入“打开微信”“明天待办：取快递”“播放暂停”，或用聊天处理复杂请求。");
    }
}
static class CommandTranslation
{
    public static ActionRequest? From(QuickIntent intent,string source)=>intent.Kind switch{
        QuickKind.LeoCommand=>new(intent.Value,intent.Argument,source),QuickKind.Clipboard=>new("clipboard_open","",source),QuickKind.Web=>new("web_search",intent.Value,source),
        QuickKind.Todos=>new("todos_open","",source),QuickKind.AddTodo=>new("todo_add",JsonSerializer.Serialize(new{title=intent.Value,day=DateTime.Today.AddDays(intent.DayOffset).ToString("yyyy-MM-dd")}),source),
        QuickKind.CompleteTodo=>new("todo_complete_title",intent.Value,source),QuickKind.OpenApp=>new("app_open_name",intent.Value,source),QuickKind.SearchApp=>new("app_search",intent.Value,source),QuickKind.Football=>new(source=="search"?"football_open":"football_facts",intent.Value,source),_=>null};
}

sealed record ExtensionCommand(string Id,string Title,string[] Phrases,string Action,string Value="");
sealed record ExtensionManifest(string Format,string Id,string Name,string Version,List<ExtensionCommand> Commands,string Runtime="actions",string? Executable=null,int TimeoutSeconds=15,string? HostMin=null,string? HostMax=null,string[]? Dependencies=null);
sealed class ExtensionStore(string root)
{
    public string Root=>root;
    internal Preferences? Preferences{get;set;}
    internal Action<string>? BeforeInstallStep;
    public string? LastWarning{get;private set;}
    public List<string> Errors{get;}=[];
    public static void Validate(ExtensionManifest m)
    {
        if(m==null||m.Id==null||m.Format!="LeoModule/1"||!Regex.IsMatch(m.Id,@"^[a-z][a-z0-9-]{1,39}$")||string.IsNullOrWhiteSpace(m.Name)||m.Name.Length>40||!Version.TryParse(m.Version,out _)||m.Commands==null||m.Commands.Count is <1 or >16)throw new ArgumentException("扩展格式、标识、名称或版本无效。");
        ExtensionCompatibility.ValidateMetadata(m);
        if(m.Runtime is not("actions" or "stdio")||m.TimeoutSeconds is <1 or >120||m.Runtime=="stdio"&&(m.Executable==null||!Regex.IsMatch(m.Executable,@"^[a-zA-Z0-9_.-]+\.exe$")))throw new ArgumentException("扩展运行协议或程序名无效");
        if(m.Commands.Any(c=>c==null))throw new ArgumentException("扩展命令不能为空");
        if(m.Commands.Select(c=>c.Id).Distinct().Count()!=m.Commands.Count)throw new ArgumentException("扩展命令标识重复");
        foreach(var c in m.Commands)if(c.Id==null||c.Action==null||!Regex.IsMatch(c.Id,@"^[a-z][a-z0-9-]{0,39}$")||string.IsNullOrWhiteSpace(c.Title)||c.Title.Length>40||c.Phrases==null||c.Phrases.Length>12||c.Phrases.Any(p=>string.IsNullOrWhiteSpace(p)||p.Length>60)||(m.Runtime=="stdio"?!Regex.IsMatch(c.Action,@"^worker:[a-z][a-z0-9-]{0,39}$"):!LeoActions.Names.Contains(c.Action))||c.Action is "combo_name" or "scene_watch" or "command_text" or "module_run"||c.Value==null||c.Value.Length>4000)throw new ArgumentException("扩展命令无效，只支持当前公开的动作接口。");
    }
    string Folder(string id){if(!Regex.IsMatch(id,@"^[a-z][a-z0-9-]{1,39}$"))throw new ArgumentException("扩展标识无效");var path=Path.GetFullPath(Path.Combine(root,id));if(!path.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("扩展路径无效");if(Directory.Exists(path)&&File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))throw new IOException("不支持链接目录中的扩展");return path;}
    public ExtensionManifest Inspect(string file,bool checkCompatibility=true){if(new FileInfo(file).Length>128000)throw new ArgumentException("扩展文件超过128KB");var value=JsonSerializer.Deserialize<ExtensionManifest>(File.ReadAllText(file))??throw new ArgumentException("扩展内容为空");Validate(value);if(checkCompatibility)ExtensionCompatibility.Require(value,this);return value;}
    public ExtensionManifest[] Load(){Errors.Clear();if(!Directory.Exists(root))return [];var list=new List<ExtensionManifest>();foreach(var folder in Directory.GetDirectories(root)){if(Path.GetFileName(folder).StartsWith('.'))continue;try{var m=Inspect(Path.Combine(folder,"module.json"),false);if(!string.Equals(Path.GetFullPath(folder),Folder(m.Id),StringComparison.OrdinalIgnoreCase))throw new IOException("标识与目录不一致");list.Add(m);}catch(Exception e)when(e is IOException or ArgumentException or JsonException or UnauthorizedAccessException){Errors.Add(Path.GetFileName(folder)+"："+e.Message);}}return list.ToArray();}
    public void Install(ExtensionManifest manifest){LastWarning=null;Validate(manifest);ExtensionCompatibility.Require(manifest,this);if(ModuleWorker.Running(manifest.Id))throw new InvalidOperationException("模块正在执行，请稍后更新。");if(manifest.Runtime!="actions")throw new ArgumentException("独立进程扩展请导入ZIP模块包");var existing=Load().FirstOrDefault(m=>m.Id==manifest.Id);if(existing!=null&&Version.Parse(manifest.Version)<=Version.Parse(existing.Version))throw new ArgumentException("更新版本必须高于已安装版本");string folder=Folder(manifest.Id);Directory.CreateDirectory(folder);ConfigMigration.Write(Path.Combine(folder,"module.json"),JsonSerializer.Serialize(manifest,new JsonSerializerOptions{WriteIndented=true}));}
    public string ExecutablePath(ExtensionManifest manifest){Validate(manifest);string file=Path.Combine(Folder(manifest.Id),manifest.Executable??"");if(!File.Exists(file))throw new IOException("扩展程序不存在，请重新安装模块包。");return file;}
    public ExtensionManifest InspectPackage(string file){var manifest=ModulePackage.Inspect(file);ExtensionCompatibility.Require(manifest,this);return manifest;}
    string Owned(string path){var absolute=Path.GetFullPath(path);if(!absolute.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("扩展操作路径越界");return absolute;}
    void DeleteOwned(string path){path=Owned(path);if(Directory.Exists(path))Directory.Delete(path,true);}
    public void InstallPackage(string file)
    {
        LastWarning=null;var manifest=InspectPackage(file);if(ModuleWorker.Running(manifest.Id))throw new InvalidOperationException("模块正在执行，请稍后更新。");var existing=Load().FirstOrDefault(m=>m.Id==manifest.Id);if(existing!=null&&Version.Parse(manifest.Version)<=Version.Parse(existing.Version))throw new ArgumentException("更新版本必须高于已安装版本");
        Directory.CreateDirectory(root);string staging=Owned(Path.Combine(root,".install-"+Guid.NewGuid().ToString("N"))),backup=Owned(Path.Combine(root,".previous-"+Guid.NewGuid().ToString("N"))),folder=Owned(Folder(manifest.Id));Directory.CreateDirectory(staging);bool moved=false;
        try{
            using(var archive=System.IO.Compression.ZipFile.OpenRead(file)){ModulePackage.ValidateEntries(archive);foreach(var entry in archive.Entries){string target=Path.GetFullPath(Path.Combine(staging,entry.FullName.Replace('\\',Path.DirectorySeparatorChar)));if(!target.StartsWith(staging+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("模块文件越界");if(entry.FullName.EndsWith('/')){Directory.CreateDirectory(target);continue;}Directory.CreateDirectory(Path.GetDirectoryName(target)!);using var source=entry.Open();using var destination=File.Create(target);source.CopyTo(destination);}}
            var staged=Inspect(Path.Combine(staging,"module.json"));if(JsonSerializer.Serialize(staged)!=JsonSerializer.Serialize(manifest)||manifest.Runtime=="stdio"&&!File.Exists(Path.Combine(staging,manifest.Executable!)))throw new IOException("暂存模块与预览不一致，未替换当前模块");
            BeforeInstallStep?.Invoke("prepared");if(ModuleWorker.Running(manifest.Id))throw new InvalidOperationException("模块正在执行，请稍后更新。");
            if(Directory.Exists(folder)){Directory.Move(Owned(folder),Owned(backup));moved=true;}
            try{BeforeInstallStep?.Invoke("promote");Directory.Move(Owned(staging),Owned(folder));}
            catch(Exception updateError){if(moved)try{BeforeInstallStep?.Invoke("restore");Directory.Move(Owned(backup),Owned(folder));}catch(Exception restoreError)when(restoreError is IOException or UnauthorizedAccessException){throw new IOException("更新未生效，自动恢复未完成；旧包保留在模块目录的 "+Path.GetFileName(backup)+"，请关闭Leo后恢复。",new AggregateException(updateError,restoreError));}throw;}
            if(moved)try{BeforeInstallStep?.Invoke("cleanup");DeleteOwned(backup);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){LastWarning="新版本已生效，旧目录清理未完成，请检查保留文件。";}
        }finally{try{DeleteOwned(staging);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){LastWarning??="暂存目录未能清理，当前模块及个人数据保留。";}}
    }
    public void Remove(string id){if(ModuleWorker.Running(id))throw new InvalidOperationException("模块正在执行，请完成或取消后再移除。");string folder=Folder(id);if(Directory.Exists(folder))Directory.Delete(folder,true);}
}
