using System.Text.Json;
namespace LeoLauncher;

// Optional internal contract. Entries contain data only and grant no execution rights.
interface IModuleEntryContributor:ILeoModule { EntryContribution[] DescribeEntries(); }
sealed record ModuleEntrySnapshot(EntryContribution[] Entries,EntryContribution[] Buttons,EntryContribution[] Menu,EntryContribution[] Search);
static class ModuleEntryContributions
{
    public const string TargetPrefix="leo-module-entry:";
    public static bool IsTarget(string target)=>target.StartsWith(TargetPrefix,StringComparison.OrdinalIgnoreCase);
    static bool Text(string? s,int max)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=max&&!s.Any(char.IsControl);
    static void Parameters(string value){if(value.Length>125000)throw new ArgumentException("入口参数过长");using var json=JsonDocument.Parse(value);if(json.RootElement.ValueKind!=JsonValueKind.Object)throw new ArgumentException("入口参数必须为对象");}
    internal static void Validate(EntryContribution entry,ILeoModule module,CapabilityRegistry registry)
    {
        if(!Text(entry.Id,160)||!EntryDisplay.ValidKey(entry.Id)||!entry.Id.StartsWith(module.Id+"/",StringComparison.Ordinal)||entry.Id.Length==module.Id.Length+1||
            entry.ModuleId!=module.Id||entry.Editor!="module-action"||entry.Target!="leo-module-entry:"+entry.Id||!Text(entry.Name,120)||
            !EntryDisplay.Modes.Any(m=>m.Value==entry.Mode)||entry.Aliases==null||entry.Aliases.Length>24||entry.Aliases.Any(a=>!Text(a,80))||
            entry.MenuActions==null||entry.MenuActions.Length>16||!module.Actions.Contains(entry.ActionId)||EntryContributions.Check(entry,registry.EntryOwner).Length!=0)
            throw new ArgumentException("模块入口无效或动作归属不一致");
        Parameters(entry.Parameters);
        foreach(var menu in entry.MenuActions){if(!Text(menu.Name,120)||!module.Actions.Contains(menu.ActionId))throw new ArgumentException("菜单动作归属不一致");Parameters(menu.Value);}
    }
    public static ModuleEntrySnapshot Read(Preferences prefs,CapabilityRegistry registry)
    {
        var entries=new List<EntryContribution>();
        foreach(var provider in registry.EntryContributors())try{
            var declared=provider.DescribeEntries();
            if(declared==null||declared.Length>32||declared.Any(e=>e==null)||declared.Select(e=>e.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=declared.Length)throw new ArgumentException("模块入口重复或无效");
            var owned=new List<EntryContribution>();
            foreach(var entry in declared){
                Validate(entry,provider,registry);
                string mode=prefs.EntryDisplayModes.ContainsKey(entry.Id)?EntryDisplay.Mode(prefs,entry.Id):entry.Mode;
                owned.Add(entry with{Enabled=entry.Enabled&&provider.Enabled,Mode=mode,Fault="",Aliases=entry.Aliases.ToArray(),MenuActions=entry.MenuActions.ToArray()});
            }
            entries.AddRange(owned);
        }catch(Exception e)when(FeatureFaults.Recoverable(e)){/* One provider cannot remove other providers' entries. */}
        var all=entries.ToArray();return new(all,all.Where(e=>e.Shortcut).ToArray(),all.Where(e=>e.Menu).ToArray(),all.Where(e=>e.Available).ToArray());
    }
    // UI passes identity only; stale descriptors/targets are never executed directly.
    public static Task<ActionResult> Execute(ActionDispatcher dispatcher,Preferences prefs,string id,string source,CancellationToken token=default,int? menuIndex=null)
    {
        token.ThrowIfCancellationRequested();var entry=Read(prefs,dispatcher.Registry).Entries.SingleOrDefault(e=>e.Id==id);
        if(entry==null||!entry.Available)throw new InvalidOperationException("模块入口已移除或停用");
        string action=entry.ActionId,parameters=entry.Parameters;
        if(menuIndex is {} index){if(index<0||index>=entry.MenuActions.Length)throw new ArgumentException("菜单项不存在");var menu=entry.MenuActions[index];action=menu.ActionId;parameters=menu.Value;}
        using var parsed=JsonDocument.Parse(parameters);
        return dispatcher.ExecuteStructuredAsync(new("leo-action/1",action,parsed.RootElement.Clone(),source),token);
    }
}
