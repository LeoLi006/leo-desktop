using System.Text.Json;
using System.Text.Json.Serialization;
namespace LeoLauncher;

// Data-only delivery protocol. No program, script, arguments or personal paths in the package.
sealed record ApplicationPackage(string Format,int Protocol,string Id,string Version,string Name,string HostMin,string HostMax,string[] Dependencies);
sealed record InstalledApplicationPackage(ApplicationPackage Manifest,LocalApplication Local,bool Installed=true);
static class ApplicationPackages
{
    public const int HostProtocol=1;
    static Version HostVersion {get{var v=typeof(ApplicationPackages).Assembly.GetName().Version!;return new(v.Major,v.Minor,v.Build);}}
    static readonly JsonSerializerOptions Options=new(){WriteIndented=true,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static ApplicationPackage Create(LocalApplication app)=>new("LeoApplicationPackage",1,Guid.NewGuid().ToString("N"),"1.0.0",app.Name,"1.04.0","1.99.99",["windows","local-application/v1"]);
    static Version Semver(string value){if(value==null||!System.Text.RegularExpressions.Regex.IsMatch(value,@"^\d{1,5}\.\d{1,5}\.\d{1,5}$")||!System.Version.TryParse(value,out var v))throw new ArgumentException("版本须为三个非负整数，例如1.0.0");return v;}
    public static void Validate(ApplicationPackage p,bool compatibility=true)
    {
        if(p.Format!="LeoApplicationPackage"||p.Protocol!=HostProtocol)throw new ArgumentException("不支持的模块包格式或协议");
        if(p.Id==null||!Guid.TryParseExact(p.Id,"N",out _)||p.Id!=p.Id.ToLowerInvariant())throw new ArgumentException("模块包身份无效");
        if(string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>24||p.Name.Any(char.IsControl))throw new ArgumentException("模块名称须为1～24个字符");
        Semver(p.Version);var min=Semver(p.HostMin);var max=Semver(p.HostMax);var host=HostVersion;
        if(min>max||compatibility&&(host<min||host>max))throw new ArgumentException("模块包不兼容当前宿主"+host);
        if(p.Dependencies==null||p.Dependencies.Length!=2||p.Dependencies.Distinct().Count()!=2||!p.Dependencies.Contains("windows")||!p.Dependencies.Contains("local-application/v1"))throw new ArgumentException("依赖不支持；不会自动下载依赖");
    }
    public static ApplicationPackage Parse(string text)
    {
        if(text.Length>16384)throw new ArgumentException("模块包超过16KB限制");
        using var doc=JsonDocument.Parse(text,new(){MaxDepth=8});
        if(doc.RootElement.ValueKind!=JsonValueKind.Object||doc.RootElement.EnumerateObject().Count()!=8||doc.RootElement.EnumerateObject().Select(x=>x.Name).Distinct().Count()!=8)throw new ArgumentException("模块包字段缺少、重复或无效");
        var p=JsonSerializer.Deserialize<ApplicationPackage>(text,Options)??throw new ArgumentException("模块包为空");Validate(p);return p;
    }
    public static ApplicationPackage Read(string file){if(new FileInfo(file).Length>65536)throw new ArgumentException("模块包文件过大");return Parse(File.ReadAllText(file));}
    public static string Export(ApplicationPackage p){Validate(p);return JsonSerializer.Serialize(p,Options);}
    public static void CheckUpdate(Preferences prefs,ApplicationPackage package)
    {
        Validate(package);var old=prefs.ApplicationPackages.Values.SingleOrDefault(x=>x.Manifest.Id==package.Id);
        if(old!=null&&(Semver(package.Version)<Semver(old.Manifest.Version)||old.Installed&&Semver(package.Version)==Semver(old.Manifest.Version)))throw new ArgumentException("已安装相同或更新版本；不覆盖当前模块");
    }
    public static Preferences Install(Preferences current,ApplicationPackage package,LocalApplication local)
    {
        CheckUpdate(current,package);LocalApplications.Validate(local);
        var old=current.ApplicationPackages.SingleOrDefault(x=>x.Value.Manifest.Id==package.Id);
        if(old.Value!=null&&old.Key!=local.Id)throw new ArgumentException("更新或重装必须保留原应用身份");
        if(current.ApplicationPackages.TryGetValue(local.Id,out var bound)&&bound.Manifest.Id!=package.Id)throw new ArgumentException("应用已属于另一个模块包");
        var next=LocalApplications.Clone(current);next.ApplicationModules.RemoveAll(x=>x.Id==local.Id);next.ApplicationModules.Add(local);
        next.ApplicationPackages[local.Id]=new(package,local);ValidateState(next);return next;
    }
    public static Preferences Remove(Preferences current,string id)
    {
        var next=LocalApplications.Clone(current);var app=next.ApplicationModules.SingleOrDefault(x=>x.Id==id)??throw new ArgumentException("应用已移除");
        if(next.ApplicationPackages.TryGetValue(id,out var package))next.ApplicationPackages[id]=package with{Local=app,Installed=false};
        next.ApplicationModules.RemoveAll(x=>x.Id==id);return next;
    }
    public static string Summary(Preferences p,string id)
    {
        var app=p.ApplicationModules.FirstOrDefault(x=>x.Id==id);var package=p.ApplicationPackages.GetValueOrDefault(id);
        string dependency=app==null?"已移除 · 本机配置已保留":!File.Exists(app.Path)?"程序路径失效":app.WorkingDirectory.Length>0&&!Directory.Exists(app.WorkingDirectory)?"工作目录失效":app.Enabled?"依赖就绪（不代表已运行）":"已停用";
        return (package==null?"本机创建":$"模块包 v{package.Manifest.Version} · 协议{package.Manifest.Protocol}")+" · "+dependency;
    }
    public static void ValidateState(Preferences p)
    {
        if(p.ApplicationPackages==null||p.ApplicationPackages.Count>256||p.ApplicationModules==null||p.ApplicationModules.Any(x=>x==null)||p.ApplicationModules.Select(x=>x.Id).Distinct().Count()!=p.ApplicationModules.Count)throw new ArgumentException("模块包配置数量或应用身份无效");
        var identities=new HashSet<string>();foreach(var (id,package) in p.ApplicationPackages){
            if(package?.Manifest==null||package.Local==null||id!=package.Local.Id||!Guid.TryParseExact(id,"N",out _)||!identities.Add(package.Manifest.Id))throw new ArgumentException("模块包配置身份冲突");Validate(package.Manifest,false);
        }
    }
}
