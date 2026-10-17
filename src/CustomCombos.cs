using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;

sealed record ComboStep(string Kind, string Value = "", string Label = "");
sealed record CustomCombo(string Id, string Name, List<ComboStep> Steps);
sealed class ComboStore
{
    readonly string? path;
    public List<CustomCombo> Items { get; private set; }
    public string? Error { get; private set; }
    public static string FilePath => Path.Combine(Store.Root, "leo-custom-combos.json");
    public ComboStore(string? file)
    {
        path = file; Items = [FromLegacy(new WatchScene())];
        if (file == null) return;
        if (File.Exists(file))
        {
            try { Items = JsonSerializer.Deserialize<List<CustomCombo>>(File.ReadAllText(file)) ?? throw new IOException(); Items=Items.Select(c=>c with {Steps=c.Steps.Select(s=>s.Kind=="playlist"?new ComboStep("qq_client",Label:"打开QQ音乐"):s).ToList()}).ToList(); foreach (var item in Items) Validate(item); if (Items.Select(i=>i.Id).Distinct().Count()!=Items.Count || Items.Select(i=>i.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Items.Count) throw new IOException(); }
            catch { Items = []; Error = "自定义组合文件无法读取，已保留原文件"; }
        }
        else
        {
            try { string legacy = Path.Combine(Path.GetDirectoryName(file)!, "leo-scenes.json"); if (File.Exists(legacy)){var item=FromLegacy(JsonSerializer.Deserialize<WatchScene>(File.ReadAllText(legacy)) ?? new());Items=item.Steps.Count>0?[item]:[];} } catch { }
        }
    }
    public static CustomCombo FromLegacy(WatchScene old)
    {
        var steps = new List<ComboStep>();
        if (old.Proxy) steps.Add(new("proxy_on", Label:"开启系统代理"));
        if (old.Website) steps.Add(new("watch", old.WatchIndex.ToString(), "观赛入口"));
        if (old.Music) steps.Add(new("qq_client",Label:"打开QQ音乐"));
        return new("watch", "准备看球", steps);
    }
    public static void Validate(CustomCombo combo)
    {
        if (string.IsNullOrWhiteSpace(combo.Id) || combo.Name.Trim().Length is <1 or >24 || combo.Steps == null || combo.Steps.Count is <1 or >12) throw new ArgumentException("名称需为1至24个字，每个组合包含1至12步");
        foreach (var step in combo.Steps)
        {
            if (step == null || step.Value == null || step.Label == null || step.Value.Length > 2048 || step.Label.Length > 150) throw new ArgumentException("组合步骤无效");
            switch (step.Kind)
            {
                case "url": _=FootTools.EdgeUrlStart(step.Value); break;
                case "app": if (string.IsNullOrWhiteSpace(step.Value)) throw new ArgumentException("请选择应用"); break;
                case "watch": if (!int.TryParse(step.Value,out int index) || index is < -1 or >3) throw new ArgumentException("请选择观赛入口"); break;
                case "qq_client": case "proxy_on": case "proxy_off": case "todos": case "football": case "chat": break;
                default: throw new ArgumentException("不支持的组合操作");
            }
        }
    }
    void Save(List<CustomCombo> next)
    {
        if (Error != null) throw new IOException(Error);
        if (next.Count > 24) throw new ArgumentException("最多保存24个组合");
        if (path != null) { File.WriteAllText(path+".tmp",JsonSerializer.Serialize(next)); File.Move(path+".tmp",path,true); }
        Items = next;
    }
    public void Put(CustomCombo item)
    {
        item=item with {Name=item.Name.Trim()}; Validate(item);
        if (Items.Any(i=>i.Id!=item.Id&&i.Name.Equals(item.Name,StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("已有同名组合，请换一个名称");
        Save(Items.Any(i=>i.Id==item.Id)?Items.Select(i=>i.Id==item.Id?item:i).ToList():Items.Append(item).ToList());
    }
    public void Delete(string id) => Save(Items.Where(i=>i.Id!=id).ToList());
}

sealed partial class PetForm
{
    bool comboBusy;
    Task<string> PrepareWatch()=>RunComboByName("准备看球");
    Task<string> RunComboByName(string name)
    {
        var item=new ComboStore(previewMode?null:ComboStore.FilePath).Items.SingleOrDefault(i=>i.Name.Equals(name.Trim(),StringComparison.OrdinalIgnoreCase));
        if(item==null){OperationFeedback.Error("未找到组合："+name);return Task.FromResult("未找到自定义组合，请在右键 → 自定义中创建。");}
        return RunCombo(item.Id);
    }
    async Task<string> RunCombo(string id)
    {
        if(comboBusy)return "已有组合正在执行，请稍候";
        if(previewMode)return "示例模式，不执行组合的外部操作";
        try
        {
            var store=new ComboStore(ComboStore.FilePath);
            if(store.Error!=null)throw new IOException(store.Error);
            var combo=store.Items.SingleOrDefault(i=>i.Id==id)??throw new ArgumentException("组合已删除");
            ComboStore.Validate(combo);
            var steps=combo.Steps.Select(BuildComboStep).ToArray();
            var run=new SceneRun(steps);
            async Task<string> Continue()
            {
                if(comboBusy||IsDisposed)return "组合正在执行或Leo已关闭";
                comboBusy=true;
                try{await run.Execute(step=>OperationFeedback.Pending(combo.Name+" · "+step));OperationFeedback.Show(combo.Name+" · 已完成");return combo.Name+"已执行；音乐仅确认已发送客户端。";}
                catch(Exception e){OperationFeedback.Error($"{combo.Name}：已完成 {run.Completed}/{steps.Length} 步 · "+e.Message,()=>Continue());return "组合未完成："+e.Message;}
                finally{comboBusy=false;}
            }
            return await Continue();
        }
        catch(Exception e){OperationFeedback.Error(e.Message,()=>{EditCombo(id);return Task.CompletedTask;},"编辑");return "组合未执行："+e.Message;}
    }
    (string,Func<Task>) BuildComboStep(ComboStep step)
    {
        Modules.Require(prefs,step.Kind is "proxy_on" or "proxy_off"?"clash":step.Kind is "url" or "watch"?"edge":step.Kind=="qq_client"?"music":null);
        string label=step.Label.Length>0?step.Label:step.Kind;
        switch(step.Kind)
        {
            case "proxy_on":case "proxy_off":bool enabled=step.Kind=="proxy_on";return(label,async()=>{var status=await Task.Run(()=>ClashProxy.SetEnabled(enabled));if(status.Enabled!=enabled)throw new IOException("代理状态未确认");});
            case "url":var start=FootTools.EdgeUrlStart(step.Value);return(label,()=>{Process.Start(start);return Task.CompletedTask;});
            case "watch":var links=new WatchLinkStore(Path.Combine(Store.Root,"leo-watch-links.json")).Items;int index=int.Parse(step.Value);var link=index<0?links.FirstOrDefault(l=>l.Url.Length>0):links.ElementAtOrDefault(index);if(link==null||link.Url.Length==0)throw new ArgumentException("请先在足球窗口填写观赛网址，或编辑组合改用网址操作");var watch=FootTools.EdgeUrlStart(link.Url);return(link.Name,()=>{Process.Start(watch);return Task.CompletedTask;});
            case "app":var app=launcher.FileApplications.FirstOrDefault(a=>a.Target.Equals(step.Value,StringComparison.OrdinalIgnoreCase))??throw new ArgumentException("应用未找到或索引尚未就绪，请稍后重试或重新选择应用");return(app.Name,()=>{launcher.LaunchFromAssistant(app);return Task.CompletedTask;});
            case "qq_client":return("打开QQ音乐",()=>{QQMusic.OpenClient();return Task.CompletedTask;});
            case "todos":return("打开清单",()=>{ShowTodos();return Task.CompletedTask;});
            case "football":return("打开足球",()=>{launcher.OpenFootballFromAssistant();return Task.CompletedTask;});
            case "chat":return("打开聊天",()=>{ShowChat();return Task.CompletedTask;});
            default:throw new ArgumentException("不支持的组合操作");
        }
    }
    void EditCombo(string? id=null,Rectangle? near=null)
    {
        using var editor=new ComboEditor(new ComboStore(previewMode?null:ComboStore.FilePath),id,()=>launcher.FileApplications,previewMode);
        var anchor=near??new Rectangle(Cursor.Position,new Size(1,1));editor.StartPosition=FormStartPosition.Manual;editor.Location=PopupPlacement.Beside(anchor,editor.Size,Screen.FromRectangle(anchor).WorkingArea);editor.ShowDialog();
    }
    void WireComboMenu(ContextMenuStrip menu)
    {
        var root=new ToolStripMenuItem("自定义");
        menu.Items.Insert(Math.Max(0,menu.Items.Count-1),root);BarcaMenus.StyleSubmenu(root,menu.Font);
        menu.Opening+=(_,_)=>
        {
            foreach(ToolStripItem old in root.DropDownItems.Cast<ToolStripItem>().ToArray())old.Dispose();root.DropDownItems.Clear();
            var store=new ComboStore(previewMode?null:ComboStore.FilePath);
            foreach(var combo in store.Items){string id=combo.Id;root.DropDownItems.Add(combo.Name,null,async(_,_)=>await RunCombo(id));}
            if(store.Items.Count==0)root.DropDownItems.Add(new ToolStripMenuItem(store.Error??"暂无自定义组合"){Enabled=false});
            root.DropDownItems.Add(new ToolStripSeparator());root.DropDownItems.Add("＋ 新建自定义…",null,(_,_)=>EditCombo(null,root.DropDown.Bounds));
            var edit=new ToolStripMenuItem("编辑 / 删除");foreach(var combo in store.Items){string id=combo.Id;edit.DropDownItems.Add(combo.Name,null,(_,_)=>EditCombo(id,edit.DropDown.Bounds));}edit.Enabled=store.Items.Count>0;root.DropDownItems.Add(edit);BarcaMenus.StyleSubmenu(edit,menu.Font);
        };
    }
}
