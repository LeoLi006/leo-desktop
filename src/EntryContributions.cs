using System.Runtime.CompilerServices;
namespace LeoLauncher;

// Host-owned data only. No delegates, controls, launch arguments or executable payloads.
sealed record EntryMenuAction(string Name,string ActionId,string Value="");
sealed record EntryContribution(string Id,string ModuleId,string ActionId,string Name,string Icon,
    bool Enabled,string Mode,string Target,string[] Aliases,string Editor="local-application")
{
    public string MenuName{get;init;}=Name;
    public string SettingsName{get;init;}=Name;
    public string Hint{get;init;}="";
    public EntryMenuAction[] MenuActions{get;init;}=[];
    public string Glyph{get;init;}="terminal";
    // Passive configured-path status; never a second execution permission gate.
    public string Dependency{get;init;}="unknown";
    public string Fault{get;init;}="";
    public string Parameters{get;init;}="{}";
    public bool Available=>Enabled&&Fault.Length==0;
    public bool Shortcut=>Available&&Mode==EntryDisplay.Shortcut;
    public bool Menu=>Available&&Mode!=EntryDisplay.Search;
    public AppEntry SearchEntry=>new(Name,Target,"自定义应用",Target);
}
static class EntryContributions
{
    public const string Protocol="leo-entry/1";
    sealed record Binding(CapabilityRegistry Registry,ActionDispatcher? Dispatcher,Action? Refresh);
    static readonly ConditionalWeakTable<Preferences,Binding> bindings=new();
    public static void Bind(Preferences prefs,CapabilityRegistry registry,ActionDispatcher? dispatcher=null,Action? refresh=null){bindings.Remove(prefs);bindings.Add(prefs,new(registry,dispatcher,refresh));}
    public static void RefreshExtensions(Preferences prefs){if(bindings.TryGetValue(prefs,out var host)){host.Dispatcher?.CancelDisabled();host.Refresh?.Invoke();}}
    public static void BindReadOnlyView(Preferences source,Preferences view){if(bindings.TryGetValue(source,out var host))Bind(view,host.Registry);}
    // Read-only status projection; never exposes the executor or mutates configuration.
    internal static ModuleContractSnapshot? ReadStatusContract(Preferences prefs,string action,LocalApplication? expected=null)
    {
        if(!bindings.TryGetValue(prefs,out var host))return null;
        var module=host.Registry.Resolve(action);
        if(expected!=null&&(module is not LocalApplicationModule app||app.Configuration!=expected))return null;
        return module is IModuleContractSource source?ModuleContracts.Snapshot(source):null;
    }
    public static ModuleEntrySnapshot ReadModules(Preferences prefs)=>bindings.TryGetValue(prefs,out var host)?ModuleEntryContributions.Read(prefs,host.Registry):new([],[],[],[]);
    public static Task<ActionResult> ExecuteModule(Preferences prefs,string id,string source,CancellationToken token=default,int? menuIndex=null)
    {
        token.ThrowIfCancellationRequested();
        if(!bindings.TryGetValue(prefs,out var host)||host.Dispatcher==null)throw new InvalidOperationException("模块入口执行器未连接");
        return ModuleEntryContributions.Execute(host.Dispatcher,prefs,id,source,token,menuIndex);
    }
    public static string Check(EntryContribution entry,Func<string,string?> owner)
    {
        try{
            if(owner(entry.ActionId)!=entry.ModuleId)return "action_owner_mismatch";
            foreach(var menu in entry.MenuActions){
                // Explicit host transfer, not an action owned by Edge.
                string expected=entry.Id=="edge"&&menu.ActionId=="launcher_open"&&menu.Value=="web"?"core":entry.ModuleId;
                if(owner(menu.ActionId)!=expected)return "menu_owner_mismatch";
            }
            return "";
        }catch(Exception e)when(FeatureFaults.Recoverable(e)){return "registry_unavailable";}
    }
    static EntryContribution Checked(Preferences prefs,EntryContribution entry)=>!bindings.TryGetValue(prefs,out var binding)?entry:
        entry with{Fault=Check(entry,binding.Registry.EntryOwner)};
    public static EntryContribution[] ReadAvailable(Preferences prefs)
    {
        var apps=prefs.ApplicationModules??[];
        var duplicates=apps.GroupBy(a=>a.Id,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1).Select(g=>g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result=new List<EntryContribution>();
        foreach(var app in apps){
            if(duplicates.Contains(app.Id))continue;
            try{var entry=Describe(prefs,app);Validate(entry,app);entry=Checked(prefs,entry);if(entry.Fault.Length==0)result.Add(entry);}
            catch(Exception e)when(FeatureFaults.Recoverable(e)){/* Isolate malformed entry; strict draft validation remains available. */}
        }
        return result.ToArray();
    }
    static readonly (string Id,string Owner,string Action,string Name,string Menu,string Settings,string Hint,string Glyph)[] builtins=[
        ("football","football-data","football_open","足球赛程","足球赛程…","足球","打开足球","football"),
        ("music","music","music_open","QQ音乐点歌","音乐","QQ 音乐","点歌","music"),
        ("media","media","media_open","PotPlayer · 媒体控制","媒体控制…","PotPlayer · 媒体控制","打开媒体控制","play"),
        ("terminal","terminal","terminal_open","打开管理员终端","管理员终端","管理员终端","打开终端","terminal"),
        ("clash","clash","proxy_status","Clash Verge 系统代理","Clash 系统代理","Clash Verge","开启系统代理 / 关闭系统代理","proxy"),
        ("edge","edge","web_search","展开 Edge 搜索","Edge 搜索…","Edge 搜索","网页搜索 关键词","search")];
    static EntryMenuAction[] BuiltinMenu(string id,string name,string action)=>id switch{
        "clash"=>[new("开启系统代理","proxy_on"),new("关闭系统代理","proxy_off"),new("查看状态","proxy_status")],
        "edge"=>[new(name,"launcher_open","web")],_=>[new(name,action)]};
    public static EntryContribution Builtin(Preferences prefs,string id)
    {
        var definition=builtins.Single(b=>b.Id==id);
        var path=prefs.ModuleExecutables.GetValueOrDefault(id)??"";
        return Checked(prefs,new EntryContribution(id,definition.Owner,definition.Action,definition.Name,prefs.ModuleIcons.GetValueOrDefault(id)??"",
            Modules.Enabled(prefs,id),EntryDisplay.Mode(prefs,id),"",[],id=="football"?"football-icon":"builtin-application")
        {MenuName=definition.Menu,SettingsName=definition.Settings,Hint=definition.Hint,Glyph=definition.Glyph,
            MenuActions=BuiltinMenu(id,definition.Menu,definition.Action),
            Dependency=path.Length==0?"unknown":File.Exists(path)?"ready":"missing"});
    }
    public static void ValidateBuiltin(EntryContribution entry)
    {
        var owner=builtins.SingleOrDefault(b=>b.Id==entry.Id);
        if(owner.Id==null||entry.ModuleId!=owner.Owner||entry.ActionId!=owner.Action||
            entry.Editor!=(entry.Id=="football"?"football-icon":"builtin-application")||
            !EntryDisplay.Modes.Any(m=>m.Value==entry.Mode)||!entry.MenuActions.SequenceEqual(BuiltinMenu(owner.Id,owner.Menu,owner.Action)))throw new ArgumentException("内置入口与动作归属不一致。");
    }
    public static EntryContribution[] ReadBuiltins(Preferences prefs)=>builtins.Select(b=>{
        var entry=Builtin(prefs,b.Id);ValidateBuiltin(entry);return entry;
    }).ToArray();
    public static string BuiltinAction(Preferences prefs,string id)
    {
        var entry=Builtin(prefs,id);ValidateBuiltin(entry);
        if(entry.Fault.Length>0)throw new InvalidOperationException("此入口暂不可用，请运行检查。");
        if(!entry.Enabled)throw new InvalidOperationException(entry.SettingsName+"已停用");
        return entry.ActionId;
    }
    public static EntryContribution Describe(Preferences prefs,LocalApplication app)=>new(app.Id,"local:"+app.Id,"local:"+app.Id,
        app.Name,app.Icon.Length>0?app.Icon:app.Path,app.Enabled,EntryDisplay.Mode(prefs,app.Id),app.Path,
        (prefs.Aliases.GetValueOrDefault(app.Path)??[]).ToArray());
    public static void Validate(EntryContribution entry,LocalApplication owner)
    {
        if(entry.Id!=owner.Id||entry.ModuleId!="local:"+owner.Id||entry.ActionId!=entry.ModuleId||
            entry.Editor!="local-application"||!Guid.TryParseExact(entry.Id,"N",out _)||
            !EntryDisplay.Modes.Any(m=>m.Value==entry.Mode))
            throw new ArgumentException("应用入口与动作归属不一致。");
    }
    public static EntryContribution[] Read(Preferences prefs)
    {
        var apps=prefs.ApplicationModules??[];
        var entries=apps.Select(app=>{var entry=Describe(prefs,app);Validate(entry,app);return entry;}).ToArray();
        if(entries.Select(e=>e.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=entries.Length)
            throw new ArgumentException("应用入口编号重复。");
        return entries;
    }
    public static LocalApplication? Configuration(Preferences prefs,string id)=>
        ReadAvailable(prefs).Any(e=>e.Id==id)?prefs.ApplicationModules.Single(a=>a.Id==id):null;
    public static LocalApplication? ForTarget(Preferences prefs,string target)
    {
        var matches=(prefs.ApplicationModules??[]).Where(a=>a.Path.Equals(target,StringComparison.OrdinalIgnoreCase)).ToArray();
        var candidate=matches.LastOrDefault(a=>a.Enabled)??matches.LastOrDefault();
        if(candidate==null)return null;
        if(!ReadAvailable(prefs).Any(e=>e.Id==candidate.Id))throw new InvalidOperationException("此应用入口暂不可用，请检查模块配置。");
        return candidate;
    }
    public static string CurrentAction(Preferences prefs,string id)
    {
        var entry=ReadAvailable(prefs).SingleOrDefault(e=>e.Id==id)??throw new InvalidOperationException("应用已移除，请重新选择。");
        if(!entry.Enabled)throw new InvalidOperationException(entry.Name+"已停用");
        return entry.ActionId;
    }
}
