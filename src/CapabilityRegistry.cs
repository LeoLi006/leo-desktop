namespace LeoLauncher;

// Host-internal minimum contract v1. This does not change the LeoModule/1 wire format.
sealed record CapabilityDefinition(string Id,string ModuleId,string Runtime,string Input,string Effect,string Concurrency,int? TimeoutMs,bool Retry=false);
sealed class CapabilityRegistry
{
    readonly object gate=new();
    internal ApplicationHostFaults ApplicationFaults{get;}=new();
    internal ApplicationHostFaults ClientFaults{get;}=new();
    readonly Preferences prefs;readonly Func<string,string,CancellationToken,Task<string>> legacy;
    readonly Func<ILeoModule[]> extensions;readonly ApplicationCapabilityHost? applicationHost;
    sealed record BuiltinSnapshot(ILeoModule[] Modules,Dictionary<string,ILeoModule> Actions);
    readonly Lazy<BuiltinSnapshot> builtins;
    readonly Dictionary<string,ILeoModule> registered=new(StringComparer.Ordinal);
    ILeoModule[]? remainingBuiltins;
    public void Register(ILeoModule module)
        =>Register(module,false);
    public void ReplaceBuiltin(ILeoModule module)=>Register(module,true);
    void Register(ILeoModule module,bool replace)
    {
        if(module is IModuleContractSource contracted)ModuleContracts.Validate(contracted);
        lock(gate){
            if(replace&&(!ModuleCatalog.Builtins.Any(m=>m.Id==module.Id)||module.Actions.Any(a=>ModuleCatalog.Owner(a)!=module.Id)||!module.Actions.OrderBy(a=>a).SequenceEqual(LeoActions.Names.Where(a=>ModuleCatalog.Owner(a)==module.Id).OrderBy(a=>a))))throw new InvalidOperationException("只能替换该内置模块拥有的动作");
            var others=registered.Values.Where(m=>!replace||m.Id!=module.Id).ToArray();
            if(string.IsNullOrWhiteSpace(module.Id)||!replace&&(ModuleCatalog.Builtins.Any(m=>m.Id==module.Id)||registered.ContainsKey(module.Id))||module.Actions.Count==0||module.Actions.Any(string.IsNullOrWhiteSpace)||module.Actions.Distinct().Count()!=module.Actions.Count||others.Any(m=>m.Actions.Intersect(module.Actions).Any()))throw new InvalidOperationException("模块或动作重复或无效");
            var all=others.Append(module).ToArray();var replaced=all.SelectMany(m=>m.Actions).ToHashSet();
            var next=builtins.Value.Modules.Where(m=>!all.Any(r=>r.Id==m.Id)).Select(m=>(ILeoModule)new BuiltinLeoModule(m.Id,m.Name,()=>m.Enabled,m.Actions.Where(a=>!replaced.Contains(a)).ToArray(),legacy)).ToArray();
            registered[module.Id]=module;remainingBuiltins=next;
        }
    }
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
        var duplicateIds=next.GroupBy(a=>a.Id,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1).Select(g=>g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var valid=next.Where(a=>!duplicateIds.Contains(a.Id)&&Guid.TryParseExact(a.Id,"N",out _)).ToArray();
        var accepted=new List<ILeoModule>();
        foreach(var app in valid){try{var module=new LocalApplicationModule(app,legacy,applicationHost,services:new ApplicationHostServices(app.Id,()=>FindApplication(app.Id),ApplicationFaults));ModuleContracts.Validate(module);accepted.Add(module);}catch(Exception e)when(FeatureFaults.Recoverable(e)){/* Keep unrelated applications usable. */}}
        var modules=accepted.ToArray();var index=Index(modules);
        applications=next;locals=modules;localActions=index;
    }
    internal LocalApplication? FindApplication(string id){var matches=(prefs.ApplicationModules??[]).Where(a=>a.Id.Equals(id,StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();return matches.Length==1?matches[0]:null;}
    public ILeoModule Resolve(string action)
    {
        lock(gate){var replacement=registered.Values.FirstOrDefault(m=>m.Actions.Contains(action));if(replacement!=null)return replacement;if(action.StartsWith("local:",StringComparison.Ordinal)){RefreshLocal();return localActions.GetValueOrDefault(action)??throw new CapabilityException("module_removed","应用模块已移除");}if(builtins.Value.Actions.TryGetValue(action,out var builtin))return builtin;return Index(extensions()).GetValueOrDefault(action)??throw new ArgumentException("操作不存在，或对应扩展未安装");}
    }
    internal int HostServiceScopes(){lock(gate){RefreshLocal();return locals.Length+registered.Values.Count(m=>m is ProxyCapability or MusicCapability or MediaCapability);}}
    // Passive lookup for entry validation. No dependency probes or action execution.
    internal string? EntryOwner(string action)
    {
        try{return Resolve(action).Id;}catch(Exception e)when(FeatureFaults.Recoverable(e)){return null;}
    }
    public ILeoModule[] Read(){lock(gate){RefreshLocal();var result=(remainingBuiltins??builtins.Value.Modules).Concat(registered.Values).Concat(locals).Concat(extensions()).ToArray();Index(result);return result;}}
    public ModuleContractSnapshot[] ReadContracts()=>Read().OfType<IModuleContractSource>().Select(ModuleContracts.Snapshot).ToArray();
    public CapabilityDefinition Describe(string action)
    {
        var module=Resolve(action);return module is IStructuredLeoModule structured?structured.Describe(action):new(action,module.Id,"legacy-adapter","legacy-string","legacy","legacy",null);
    }
}
