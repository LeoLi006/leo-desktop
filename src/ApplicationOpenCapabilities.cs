using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
namespace LeoLauncher;

// Rules reference existing application modules; executable/flags/icon/UAC remain owned there.
sealed record ApplicationOpenRule(string Id,string Name,string[] Extensions,bool Folders=false,bool Multiple=false,
    string Adapter="app",string ModuleId="",bool Enabled=true,string[]? DetectExecutables=null);
sealed record ResolvedOpenApplication(ApplicationOpenRule Rule,LocalApplication? Application,string State,string Source)
{
    public bool Available=>State=="ready";
    public string Stamp=>BrainCatalog.Hash(System.Text.Json.JsonSerializer.Serialize(new{Rule,Application}));
    public string Path=>Application?.Path??"";
    public string Icon=>Application is {} app?(app.Icon.Length>0?app.Icon:app.Path):"";
}
static class ApplicationOpenCapabilities
{
    public static ApplicationOpenRule[] Defaults()=>[
        new("code","VS Code",Ext("txt md markdown json jsonl xml yaml yml toml ini cfg conf log csv tsv html htm css scss less js jsx ts tsx py ipynb c h cpp hpp cs java go rs rb php sql sh ps1 bat cmd r tex bib gitignore dockerfile"),true,true,"code",DetectExecutables:["Code.exe"]),
        new("potplayer","PotPlayer",Ext("mp3 flac wav m4a aac ogg opus wma mp4 mkv avi mov webm wmv ts mpeg mpg m4v"),Adapter:"media",DetectExecutables:["PotPlayerMini64.exe","PotPlayerMini.exe","PotPlayer64.exe","PotPlayer.exe"]),
        new("annotate","手写 / 批注",[]),
        new("foxit","福昕 PDF",Ext("pdf"),DetectExecutables:["FoxitPDFReader.exe","FoxitReader.exe","FoxitPDFEditor.exe","FoxitPhantomPDF.exe"]),
        new("word","Word",Ext("doc docx rtf docm dot dotx dotm"),DetectExecutables:["WINWORD.EXE"]),
        new("excel","Excel",Ext("xls xlsx xlsm xlsb csv xlt xltx xltm"),DetectExecutables:["EXCEL.EXE"])
    ];
    public static string[] Ext(string value)=>value.Split(new[]{' ', ',', ';','\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(s=>"."+s.Trim().TrimStart('.').ToLowerInvariant()).Distinct().ToArray();
    public static ApplicationOpenRule[] Rules(Preferences p){var defaults=Defaults();var rules=p.ApplicationOpenRules??[];return defaults.Select(d=>rules.FirstOrDefault(r=>r.Id==d.Id)??d).Concat(rules.Where(r=>!defaults.Any(d=>d.Id==r.Id))).ToArray();}
    public static void Validate(IEnumerable<ApplicationOpenRule>? rules)
    {
        if(rules==null)return;var all=rules.ToArray();
        if(all.Length>40||all.Any(r=>r==null)||all.Select(r=>r.Id).Distinct().Count()!=all.Length)throw new ArgumentException("打开能力配置重复或过多。");
        foreach(var r in all){if(new[]{"default","choose_app","configure_open","copy_paths","copy_text","reveal","note","todo","add_module","copy_image_file","open_url","save_image","copy_image"}.Contains(r.Id)||!Regex.IsMatch(r.Id??"",@"^[a-z][a-z0-9-]{1,39}$")||string.IsNullOrWhiteSpace(r.Name)||r.Name.Length>24||r.Name.Any(char.IsControl)||r.Extensions==null||r.Extensions.Length>120||r.Extensions.Any(e=>!Regex.IsMatch(e??"",@"^\.[a-zA-Z0-9_-]{1,24}$"))||r.Adapter is not("app" or "code" or "media")||(r.ModuleId==null||r.ModuleId.Length>0&&!Guid.TryParseExact(r.ModuleId,"N",out _))||r.DetectExecutables?.Length>16||r.DetectExecutables?.Any(e=>!Regex.IsMatch(e??"",@"^[a-zA-Z0-9 _.-]+\.exe$",RegexOptions.IgnoreCase))==true)throw new ArgumentException("打开能力的名称、类型或应用配置无效。");
            if(r.Adapter=="media"&&(r.Id!="potplayer"||r.Multiple||r.Folders))throw new ArgumentException("PotPlayer 媒体适配目前仅支持单个媒体文件。");
        }
    }
    public static bool Matches(ApplicationOpenRule r,string[] paths)=>paths.Length>0&&paths.All(path=>Directory.Exists(path)?r.Folders:r.Extensions.Contains(System.IO.Path.GetExtension(path),StringComparer.OrdinalIgnoreCase));
    public static string Identity(string path)
    {
        try{if(System.IO.Path.GetExtension(path).Equals(".lnk",StringComparison.OrdinalIgnoreCase))path=Catalog.ResolveExecutable(new("",path)).ExePath;
        return path.Length>0?System.IO.Path.GetFullPath(path):"";}catch(Exception e)when(FeatureFaults.Recoverable(e)){return "";}
    }
    public static ResolvedOpenApplication[] Resolve(Preferences p,IEnumerable<AppEntry>? catalog=null,
        Func<string,string?>? registered=null,Func<string,string?>? association=null)
    {
        registered??=Registered;association??=Association;var apps=(p.ApplicationModules??[]).ToArray();var entries=(catalog??[]).ToArray();
        var output=new List<ResolvedOpenApplication>();
        foreach(var rule in Rules(p)){
            LocalApplication? app=null;string source="Leo 类型匹配",state="unconfigured";
            try{
                if(rule.ModuleId.Length>0){app=apps.FirstOrDefault(a=>a.Id==rule.ModuleId);if(app==null){output.Add(new(rule,null,"removed",source));continue;}}
                else if(rule.DetectExecutables is{Length:>0} names){
                    bool Known(string path)=>names.Contains(System.IO.Path.GetFileName(Identity(path)),StringComparer.OrdinalIgnoreCase);
                    app=apps.FirstOrDefault(a=>Known(a.Path));
                    if(app==null){
                        string? path=null;
                        if(rule.Adapter=="media"&&p.ModuleExecutables.TryGetValue("media",out var saved))path=saved;
                        else{
                            path=entries.Select(e=>e.ExePath).FirstOrDefault(e=>e.Length>0&&Known(e)&&File.Exists(e));
                            path??=names.Select(registered).FirstOrDefault(File.Exists);
                            path??=rule.Extensions.Take(4).Select(association).FirstOrDefault(e=>e!=null&&Known(e)&&File.Exists(e));
                            if(path==null&&rule.Adapter=="code")path=LauncherFileSearch.FindCode(entries);
                            if(path==null&&rule.Adapter=="media"){using var passive=ModulePaths.PassiveRead();path=MediaControl.PotPlayerPath();}
                        }
                        if(path!=null)app=new(GuidFrom(rule.Id),rule.Name,path,Icon:p.ModuleIcons.GetValueOrDefault(rule.Adapter=="media"?"media":rule.Id)??"");
                    }
                }
                if(!rule.Enabled||rule.Adapter=="media"&&!p.MediaModule||app?.Enabled==false)state="disabled";
                else if(app!=null){LocalApplications.Validate(app);if(!File.Exists(Identity(app.Path)))throw new IOException();if(rule.Adapter=="code"&&!System.IO.Path.GetFileName(Identity(app.Path)).Equals("Code.exe",StringComparison.OrdinalIgnoreCase))throw new IOException();if(rule.Adapter=="media")ModulePaths.Validate("media",Identity(app.Path));state="ready";}
            }catch(Exception e)when(FeatureFaults.Recoverable(e)){state="invalid-path";}
            output.Add(new(rule,app,state,source));
        }
        return output.ToArray();
    }
    public static (ResolvedOpenApplication[] Apps,bool Default,string DefaultPath) Candidates(Preferences p,string[] paths,IEnumerable<AppEntry>? catalog=null,Func<string,string?>? registered=null,Func<string,string?>? association=null)
    {
        association??=Association;var matches=Resolve(p,catalog,registered,association).Where(a=>Matches(a.Rule,paths)).GroupBy(a=>Identity(a.Path) is{Length:>0} identity?identity:"rule:"+a.Rule.Id,StringComparer.OrdinalIgnoreCase).Select(g=>g.FirstOrDefault(a=>a.Available&&(paths.Length==1||a.Rule.Multiple))??g.FirstOrDefault(a=>a.Available)??g.First()).ToArray();
        string defaultPath=paths.All(File.Exists)&&paths.Select(System.IO.Path.GetExtension).Distinct(StringComparer.OrdinalIgnoreCase).Count()==1?association(System.IO.Path.GetExtension(paths[0]))??"":"";
        var same=matches.FirstOrDefault(a=>a.Available&&defaultPath.Length>0&&Identity(a.Path).Equals(Identity(defaultPath),StringComparison.OrdinalIgnoreCase));
        bool replaceDefault=same!=null&&(paths.Length==1||same.Rule.Multiple);
        if(same!=null&&!replaceDefault)matches=matches.Where(a=>a!=same).ToArray();
        if(same!=null&&replaceDefault)matches=matches.Select(a=>a==same?a with{Source="系统默认 / 类型匹配"}:a).ToArray();
        return(matches,!replaceDefault&&!paths.Any(DropOpenExecution.MayRun),defaultPath);
    }
    static string GuidFrom(string id)=>Convert.ToHexString(System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes("leo-open:"+id))).ToLowerInvariant();
    public static ProcessStartInfo Start(ResolvedOpenApplication value,string[] paths)
    {
        if(!value.Available||value.Application==null)throw new IOException("应用已停用、移除或程序路径无效。");
        var r=value.Rule;paths=FileDropActions.RequirePaths(paths);
        if(!Matches(r,paths))throw new ArgumentException("所选应用的文件类型配置不匹配。");
        if(paths.Length>1&&!r.Multiple)throw new ArgumentException("此应用打开能力仅支持单项，请分开拖入。");
        var app=value.Application;var info=LocalApplications.Start(app);info.Arguments="";
        var flags=FileDropActions.ParseArguments(app.Arguments);
        if(r.Adapter=="code"){
            LauncherFileSearch.CodeStart(Identity(app.Path),paths[0]);
            if(flags.Any(f=>f is "-r" or "--reuse-window"))throw new ArgumentException("VS Code 打开能力不能使用复用窗口参数。");
            info.FileName=Identity(app.Path);
        }
        foreach(string flag in flags)info.ArgumentList.Add(flag);
        if(r.Adapter=="code"&&!flags.Contains("--new-window"))info.ArgumentList.Add("--new-window");
        foreach(string path in paths)info.ArgumentList.Add(path);
        return info;
    }
    [DllImport("shlwapi.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]static extern int AssocQueryStringW(uint flags,uint kind,string association,string? extra,StringBuilder output,ref uint length);
    public static string? Association(string extension)
    {try{uint n=32768;var b=new StringBuilder((int)n);return AssocQueryStringW(0x100,2,extension,"open",b,ref n)==0&&File.Exists(b.ToString())?b.ToString():null;}catch(Exception e)when(FeatureFaults.Recoverable(e)){return null;}}
    public static string? Registered(string executable)
    {
        foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32}){
            try{using var root=RegistryKey.OpenBaseKey(hive,view);using var key=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\"+executable);if(key?.GetValue("") is string path&&File.Exists(path.Trim('"')))return path.Trim('"');}catch(Exception e)when(FeatureFaults.Recoverable(e)){}
        }return null;
    }
}
