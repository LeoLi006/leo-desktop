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
    public bool Shortcut=>Enabled&&Mode==EntryDisplay.Shortcut;
    public bool Menu=>Enabled&&Mode!=EntryDisplay.Search;
    public AppEntry SearchEntry=>new(Name,Target,"自定义应用",Target);
}
static class EntryContributions
{
    public const string Protocol="leo-entry/1";
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
        return new(id,definition.Owner,definition.Action,definition.Name,prefs.ModuleIcons.GetValueOrDefault(id)??"",
            Modules.Enabled(prefs,id),EntryDisplay.Mode(prefs,id),"",[],id=="football"?"football-icon":"builtin-application")
        {MenuName=definition.Menu,SettingsName=definition.Settings,Hint=definition.Hint,Glyph=definition.Glyph,
            MenuActions=BuiltinMenu(id,definition.Menu,definition.Action),
            Dependency=path.Length==0?"unknown":File.Exists(path)?"ready":"missing"};
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
        Read(prefs).Any(e=>e.Id==id)?prefs.ApplicationModules.Single(a=>a.Id==id):null;
    public static LocalApplication? ForTarget(Preferences prefs,string target)
    {
        var entries=Read(prefs);
        var entry=entries.LastOrDefault(e=>e.Enabled&&e.Target.Equals(target,StringComparison.OrdinalIgnoreCase))
            ??entries.LastOrDefault(e=>e.Target.Equals(target,StringComparison.OrdinalIgnoreCase));
        return entry==null?null:prefs.ApplicationModules.Single(a=>a.Id==entry.Id);
    }
    public static string CurrentAction(Preferences prefs,string id)
    {
        var entry=Read(prefs).SingleOrDefault(e=>e.Id==id)??throw new InvalidOperationException("应用已移除，请重新选择。");
        if(!entry.Enabled)throw new InvalidOperationException(entry.Name+"已停用");
        return entry.ActionId;
    }
}
