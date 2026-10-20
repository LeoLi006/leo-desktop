using System.Text;
using System.Text.Json;
namespace LeoLauncher;
static class EntryInventory
{
    public static (string Json,string Markdown) Generate(Preferences prefs)
    {
        var builtins=EntryContributions.ReadBuiltins(prefs);var apps=EntryContributions.ReadAvailable(prefs);
        var rows=builtins.Select(e=>new{entry=e.Id,module=e.ModuleId,action=e.ActionId,editor=e.Editor,
            menu=e.MenuActions.Select(m=>new{action=m.ActionId,value=m.Value,owner=e.Id=="edge"?"core":e.ModuleId}).ToArray()})
            .Concat(apps.Select((e,i)=>new{entry="example-"+(i+1).ToString("00"),module="local:example-"+(i+1).ToString("00"),
                action="local:example-"+(i+1).ToString("00"),editor="local-application",menu=Array.Empty<object>().Select(_=>new{action="",value="",owner=""}).ToArray()})).ToArray();
        var json=JsonSerializer.Serialize(new{format=EntryContributions.Protocol,entries=rows},new JsonSerializerOptions{WriteIndented=true});
        var md=new StringBuilder("# 入口接入清册\n\n由隔离预览宿主生成并与真实注册表核对。只导出固定协议字段；不含名称、图标、路径、简称、启用状态或业务正文。本机应用编号为示例编号。\n\n| 入口 | 模块 | 主动作 | 固定编辑器 |\n| --- | --- | --- | --- |\n");
        foreach(var r in rows)md.Append($"| {r.entry} | {r.module} | {r.action} | {r.editor} |\n");
        md.Append("\nEdge 的 launcher_open(web) 是 core 宿主转接；先核验 Edge 能力归属与启用，再执行通用窗口动作。详细菜单动作见 inventory.json。\n");
        return(json,md.ToString());
    }
    public static void ExportExample(string directory)
    {
        Directory.CreateDirectory(directory);var prefs=new Preferences{ApplicationIndexing=false,ApplicationModules=[new("0123456789abcdef0123456789abcdef","PRIVATE_NAME",@"C:\PRIVATE_PATH\app.exe")]};
        using var pet=new PetForm(prefs,true,true);
        if(EntryContributions.ReadBuiltins(prefs).Any(e=>e.Fault.Length>0)||EntryContributions.ReadAvailable(prefs).Length!=1)throw new InvalidOperationException("示例入口与注册表不一致");
        var files=Generate(prefs);File.WriteAllText(Path.Combine(directory,"inventory.json"),files.Json);File.WriteAllText(Path.Combine(directory,"GENERATED.md"),files.Markdown);
    }
}
