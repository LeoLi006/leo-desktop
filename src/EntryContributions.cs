namespace LeoLauncher;

// Host-owned data only. No delegates, controls, launch arguments or executable payloads.
sealed record ApplicationEntry(string Id,string ModuleId,string ActionId,string Name,string Icon,
    bool Enabled,string Mode,string Target,string[] Aliases,string Editor="local-application")
{
    public bool Shortcut=>Enabled&&Mode==EntryDisplay.Shortcut;
    public bool Menu=>Enabled&&Mode!=EntryDisplay.Search;
    public AppEntry SearchEntry=>new(Name,Target,"自定义应用",Target);
}
static class EntryContributions
{
    public const string Protocol="leo-entry/1";
    public static ApplicationEntry Describe(Preferences prefs,LocalApplication app)=>new(app.Id,"local:"+app.Id,"local:"+app.Id,
        app.Name,app.Icon.Length>0?app.Icon:app.Path,app.Enabled,EntryDisplay.Mode(prefs,app.Id),app.Path,
        (prefs.Aliases.GetValueOrDefault(app.Path)??[]).ToArray());
    public static void Validate(ApplicationEntry entry,LocalApplication owner)
    {
        if(entry.Id!=owner.Id||entry.ModuleId!="local:"+owner.Id||entry.ActionId!=entry.ModuleId||
            entry.Editor!="local-application"||!Guid.TryParseExact(entry.Id,"N",out _)||
            !EntryDisplay.Modes.Any(m=>m.Value==entry.Mode))
            throw new ArgumentException("应用入口与动作归属不一致。");
    }
    public static ApplicationEntry[] Read(Preferences prefs)
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
