using System.Text;
using System.Text.Json;
namespace LeoLauncher;
static class ContractInventory
{
    // Export definition metadata only, excluding current enablement, paths and dependency observations.
    public static ModuleContract[] PublicDefinitions(CapabilityRegistry registry)
    {
        int app=0;return registry.ReadContracts().Select(s=>s.Contract).OrderBy(d=>d.Kind).ThenBy(d=>d.Id).Select(d=>{
            if(d.Kind!="application")return d;
            string alias="local:example-"+(++app).ToString("00");
            return d with{Id=alias,Name="本机应用启动",Actions=d.Actions.Select(a=>a with{Id=alias,Policy=a.Policy with{Id=alias,ModuleId=alias}}).ToArray(),Dependencies=d.Dependencies.Select(dep=>dep with{Actions=dep.Actions.Select(_=>alias).ToArray()}).ToArray()};
        }).ToArray();
    }
    public static (string Json,string Markdown) Generate(CapabilityRegistry registry)
    {
        var definitions=PublicDefinitions(registry);string json=JsonSerializer.Serialize(new{format=ModuleContracts.Format,modules=definitions},new JsonSerializerOptions{WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        var text=new StringBuilder("# 实际注册表生成的契约清册\n\n此文件由隔离预览宿主生成。只列已接入契约的模块；不代表旧扩展已迁移。应用编号已替换，未导出启用状态、设备依赖状态、路径或业务数据。\n\n");
        foreach(var d in definitions){text.Append("## ").Append(d.Kind).Append(" · ").Append(d.Id).Append("\n\n");text.Append("协议：").Append(d.Format).Append("；运行类型：").Append(d.Runtime).Append("；数据归属：").Append(d.DataOwner).Append("。\n\n");text.Append("| 动作 | 参数编码 | 旧参数类型 | 结果类型 | 副作用 | 并发 | 超时ms | 取消 |\n| --- | --- | --- | --- | --- | --- | --- | --- |\n");foreach(var a in d.Actions)text.Append("| ").Append(string.Join(" | ",new[]{a.Id,a.Input.Encoding,a.Input.LegacyProfile,a.Output.DataProfile,a.Policy.Effect,a.Policy.Concurrency,a.Policy.TimeoutMs.ToString(),a.Cancellation})).Append(" |\n");text.Append("\n依赖：").Append(string.Join("；",d.Dependencies.Select(dep=>dep.Id+"（"+dep.Requirement+" / "+dep.UnavailableBehavior+"）"))).Append("。\n\n");}
        text.Append("详细输入、输出及原执行策略见 inventory.json。参数描述是接口说明，不是第二套JSON Schema或执行器。\n");return(json,text.ToString());
    }
    public static void ExportExample(string directory)
    {
        Directory.CreateDirectory(directory);var prefs=new Preferences{ApplicationIndexing=false,ApplicationModules=[new("0123456789abcdef0123456789abcdef","fixture private name",@"C:\fixture-private\app.exe")]};
        using var pet=new PetForm(prefs,true,true);var registry=pet.ContractRegistry;var files=Generate(registry);File.WriteAllText(Path.Combine(directory,"inventory.json"),files.Json);File.WriteAllText(Path.Combine(directory,"GENERATED.md"),files.Markdown);
    }
}
sealed partial class PetForm{internal CapabilityRegistry ContractRegistry=>commands.Registry;}
