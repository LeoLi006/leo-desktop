namespace LeoLauncher;

// Host-internal minimum contract v1. This does not change the LeoModule/1 wire format.
sealed record CapabilityDefinition(string Id,string ModuleId,string Runtime,string Input,string Effect,string Concurrency,int? TimeoutMs,bool Retry=false);
sealed class CapabilityRegistry
{
    readonly object gate=new();
    readonly Preferences prefs;readonly Func<string,string,CancellationToken,Task<string>> legacy;
    readonly Func<ILeoModule[]> extensions;readonly ApplicationCapabilityHost? applicationHost;
    sealed record BuiltinSnapshot(ILeoModule[] Modules,Dictionary<string,ILeoModule> Actions);
    readonly Lazy<BuiltinSnapshot> builtins;
    LocalApplication[] applications=[];ILeoModule[] locals=[];Dictionary<string,ILeoModule> localActions=new();
    public CapabilityRegistry(Preferences prefs,Func<string,string,CancellationToken,Task<string>> legacy,Func<ILeoModule[]> extensions,ApplicationCapabilityHost? applicationHost)
    {
        this.prefs=prefs;this.legacy=legacy;this.extensions=extensions;this.applicationHost=applicationHost;
        builtins=new(()=>{
            var modules=ModuleCatalog.Builtins.Select(m=>(ILeoModule)new BuiltinLeoModule(m.Id,m.Name,()=>Modules.Enabled(prefs,m.Id),LeoActions.Names.Where(a=>ModuleCatalog.Owner(a)==m.Id).ToArray(),legacy)).ToArray();
            return new(modules,Index(modules));
        });
    }
    static Dictionary<string,ILeoModule> Index(IEnumerable<ILeoModule> modules)
    {
        var result=new Dictionary<string,ILeoModule>(StringComparer.Ordinal);var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var module in modules){if(!ids.Add(module.Id))throw new InvalidOperationException("模块标识重复");foreach(string action in module.Actions)if(!result.TryAdd(action,module))throw new InvalidOperationException("动作标识重复");}return result;
    }
    void RefreshLocal()
    {
        var next=(prefs.ApplicationModules??[]).ToArray();if(applications.SequenceEqual(next))return;
        // Build completely before replacing the snapshot. Disabled definitions remain discoverable.
        var modules=next.Select(a=>(ILeoModule)new LocalApplicationModule(a,legacy,applicationHost,()=>FindApplication(a.Id))).ToArray();var index=Index(modules);
        applications=next;locals=modules;localActions=index;
    }
    internal LocalApplication? FindApplication(string id)=>(prefs.ApplicationModules??[]).SingleOrDefault(a=>a.Id==id);
    public ILeoModule Resolve(string action)
    {
        lock(gate){if(action.StartsWith("local:",StringComparison.Ordinal)){RefreshLocal();return localActions.GetValueOrDefault(action)??throw new CapabilityException("module_removed","应用模块已移除");}if(builtins.Value.Actions.TryGetValue(action,out var builtin))return builtin;return Index(extensions()).GetValueOrDefault(action)??throw new ArgumentException("操作不存在，或对应扩展未安装");}
    }
    public ILeoModule[] Read(){lock(gate){RefreshLocal();var result=builtins.Value.Modules.Concat(locals).Concat(extensions()).ToArray();Index(result);return result;}}
    public CapabilityDefinition Describe(string action)
    {
        var module=Resolve(action);return module is IStructuredLeoModule structured?structured.Describe(action):new(action,module.Id,"legacy-adapter","legacy-string","legacy","legacy",null);
    }
}
