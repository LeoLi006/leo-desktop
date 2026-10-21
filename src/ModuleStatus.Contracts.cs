namespace LeoLauncher;
static partial class ModuleStatuses
{
    internal static ModuleStatus FromContract(string id,string name,string source,string action,ModuleContractSnapshot? snapshot)
    {
        ModuleStatus Row(ModuleState state,string detail)=>new(id,name,state,detail,action){Source=source};
        if(snapshot==null)return Row(ModuleState.Unknown,"未连接当前契约，或配置草稿与运行配置不同；未执行检测。");
        string expected=Guid.TryParseExact(id,"N",out _)?"local:"+id:id;
        if(snapshot.Contract.Id!=expected)return Row(ModuleState.Invalid,"契约归属不一致，未采用该检测结果。");
        if(!snapshot.ConfigurationIsCurrent)return Row(ModuleState.Unknown,"运行配置已变化，请保存设置后重新检查。");
        if(!snapshot.Enabled)return Row(ModuleState.Disabled,"当前能力不可用；入口显示设置不等同于模块启停。");
        var missing=snapshot.Dependencies.Where(d=>d.State=="missing").Select(d=>d.Id).ToArray();
        if(missing.Length>0)return Row(ModuleState.Invalid,"被动检查发现依赖不可用："+string.Join("、",missing)+"；未启动依赖。");
        if(snapshot.Dependencies.Any(d=>d.State is "unknown" or "degraded"))return Row(ModuleState.Unknown,"依赖状态尚未确认或仅部分可用；未进行启动、联网或运行验证。");
        return Row(ModuleState.Ready,"契约及被动依赖检查通过；不代表程序已启动或运行正常。");
    }
    internal static ModuleStatus[] ReadConfigured(Preferences prefs,ExtensionStore store,Func<string,LocalApplication?,ModuleContractSnapshot?> read)
    {
        var rows=new List<ModuleStatus>();
        foreach(var group in (prefs.ApplicationModules??[]).GroupBy(a=>a.Id,StringComparer.OrdinalIgnoreCase)){
            var app=group.First();string source="自定义应用"+(prefs.ApplicationPackages.TryGetValue(app.Id,out var package)?" · 数据包 v"+package.Manifest.Version:"")+" · "+(app.Enabled?"已启用":"已停用");
            ModuleStatus Row(ModuleState state,string detail)=>new(app.Id,app.Name,state,detail,"应用配置"){Source=source};
            if(group.Count()!=1){rows.Add(Row(ModuleState.Invalid,"应用标识重复，请在原应用配置中处理。"));continue;}
            if(!app.Enabled){rows.Add(Row(ModuleState.Disabled,"能力已停用；原显示位置及启动配置保留。"));continue;}
            try{LocalApplications.Validate(app);rows.Add(FromContract(app.Id,app.Name,source,"应用配置",read("local:"+app.Id,app)));}
            catch(Exception e)when(FeatureFaults.Recoverable(e)){rows.Add(Row(ModuleState.Invalid,"配置、契约或依赖检查失败；请检查程序路径、工作目录及原应用配置。"));}
        }
        var installed=store.Load();var errors=store.Errors.ToArray();
        foreach(var extension in installed){
            string id="ext:"+extension.Id;bool disabled=prefs.DisabledExtensions.Contains(extension.Id);string source=(extension.Runtime=="stdio"?"独立进程扩展":"声明式扩展")+" · v"+extension.Version+" · "+(disabled?"已停用":"已启用");
            ModuleStatus Row(ModuleState state,string detail)=>new(id,extension.Name,state,detail,"管理扩展"){Source=source};
            if(disabled){rows.Add(Row(ModuleState.Disabled,"扩展已停用；可在原扩展管理中启用。"));continue;}
            try{
                var compatible=ExtensionCompatibility.Read(extension,store);
                if(!compatible.Compatible){rows.Add(Row(ModuleState.Invalid,string.Join("；",compatible.Reasons)));continue;}
                rows.Add(FromContract(id,extension.Name,source,"管理扩展",read("ext:"+extension.Id+":"+extension.Commands[0].Id,null)));
            }catch(Exception e)when(FeatureFaults.Recoverable(e)){rows.Add(Row(ModuleState.Unknown,"契约或被动依赖状态无法读取；未执行扩展，可在原扩展管理中检查。"));}
        }
        foreach(var error in errors.Take(64))rows.Add(new("ext:invalid","扩展模块",ModuleState.Invalid,"存在无法读取的扩展声明；请在原扩展管理中检查。","管理扩展"){Source="扩展声明"});
        return rows.ToArray();
    }
}
