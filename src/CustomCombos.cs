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
                case "url": FootTools.ValidateUrl(step.Value); break;
                case "command":if(string.IsNullOrWhiteSpace(step.Value)||step.Value.Length>200)throw new ArgumentException("请填写不超过200字的本地指令");break;
                case "media_toggle":case "media_pause":case "media_next":case "media_previous":break;
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
        if (path != null) AtomicJson.Write(path,JsonSerializer.Serialize(next));
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
    ReliableWorkflow? comboWorkflow;CancellationTokenSource? comboCancellation;
    Task<string> PrepareWatch()=>commands.ExecuteAsync(new("scene_watch","","combo"));
    Task<string> RunComboByName(string name)=>commands.ExecuteAsync(new("combo_name",name,"combo"));
    async Task<string> RunCombo(string id)
    {
        bool submitted=false;
        try{var store=new ComboStore(previewMode?null:ComboStore.FilePath);if(store.Error!=null)throw new IOException(store.Error);var combo=store.Items.SingleOrDefault(i=>i.Id==id)??throw new ArgumentException("组合已删除");submitted=true;return await RunComboByName(combo.Name);}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){if(!submitted)OperationFeedback.Error(e.Message);return "组合未完成："+e.Message;}
    }
    void RegisterWorkflowCapability()
    {
        var ui=new ModuleWindowService(this);
        commands.Registry.Register(new WorkflowCapability(async(request,token)=>{
            Task<ActionPayload>? task=null;await ui.ShowAsync(()=>task=ExecuteWorkflow(request,token),token);return await task!;
        }));
    }
    async Task<ActionPayload> ExecuteWorkflow(ActionRequest request,CancellationToken token)
    {
        string name=request.Action=="scene_watch"?"准备看球":request.Value;bool confirmed=false;
        if(request.Action=="combo_resume"){using var doc=JsonDocument.Parse(request.Value);name=doc.RootElement.GetProperty("name").GetString()??"";confirmed=doc.RootElement.GetProperty("confirmUnknown").GetBoolean();}
        if(previewMode)return new("示例模式，不执行组合的外部操作");
        if(comboCancellation!=null)throw new InvalidOperationException("已有组合正在执行，请稍候");
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(token);comboCancellation=linked;
        try{
            var store=new ComboStore(ComboStore.FilePath);if(store.Error!=null)throw new IOException(store.Error);
            var combo=store.Items.SingleOrDefault(i=>i.Name.Equals(name.Trim(),StringComparison.OrdinalIgnoreCase))??throw new ArgumentException("未找到自定义组合，请在右键菜单中创建");ComboStore.Validate(combo);
            var steps=combo.Steps.Select(PlanComboStep).ToArray();
            foreach(var step in steps)if(!commands.Registry.Resolve(step.Request.Action).Enabled)throw new InvalidOperationException("组合使用的模块已停用，请先启用或编辑组合");
            comboWorkflow??=new(new WorkflowJournal(Path.Combine(Store.Root,"leo-flow-runs.json")),commands.ExecuteDetailedAsync,VerifyWorkflowStep);
            var result=await comboWorkflow.Run(combo.Id,steps,(index,step)=>OperationFeedback.Sink?.Invoke(new(combo.Name+" · "+(index+1)+"/"+steps.Length+" · "+step.Label,Retry:()=>{if(ReferenceEquals(comboCancellation,linked))try{linked.Cancel();}catch(ObjectDisposedException){}return Task.CompletedTask;},Busy:true,ActionLabel:"停止",Module:"workflows",OperationId:combo.Id)),linked.Token,confirmed);
            OperationFeedback.Show(combo.Name+" · 已完成");return new("组合已完成；客户端操作仅确认已提交。",JsonSerializer.SerializeToElement(new{result.RunId,completed=result.Steps.Count(s=>s.State=="succeeded")}),"steps-completed");
        }catch(OperationCanceledException){OperationFeedback.Show("组合已停止，已完成的步骤保留；不会继续执行后续操作。");throw;}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){
            bool explicitRetry=e is WorkflowRecoveryRequired;
            OperationFeedback.Error(e.Message,async()=>{try{await commands.ExecuteAsync(new("combo_resume",JsonSerializer.Serialize(new{name,confirmUnknown=explicitRetry}),"workflow-retry"));}catch(Exception failure)when(FeatureFaults.Recoverable(failure)){}},explicitRetry?"确认重试此步":"检查并重试",module:"workflows");throw;
        }finally{if(ReferenceEquals(comboCancellation,linked))comboCancellation=null;}
    }
    WorkflowStep PlanComboStep(ComboStep step,int index)
    {
        var request=step.Kind=="command"?new ActionRequest("command_text",step.Value,"combo"):ComboRequest(step);
        if(request.Action=="command_text"&&CommandTranslation.From(QuickRouter.Route(request.Value),"combo") is {} translated&&ModuleCatalog.Owner(translated.Action) is "clash" or "media" or "music")request=translated;
        if(request.Action=="watch_open"){
            var link=new WatchLinkStore(previewMode?null:Path.Combine(Store.Root,"leo-watch-links.json")).Items[int.Parse(request.Value)-1];
            request=new("web_open",link.Url,"combo"); // Pin the actual target into this plan's fingerprint.
        }
        return new("step-"+(index+1),request,step.Label.Length>0?step.Label:step.Kind,TimeoutMs:commands.Registry.Describe(request.Action).TimeoutMs??30000);
    }
    async Task<WorkflowVerification> VerifyWorkflowStep(ActionRequest request,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(request.Action is "proxy_on" or "proxy_off"){var result=await commands.ExecuteDetailedAsync(new("proxy_status","","workflow-verify"),token);if(result.Status!="succeeded"||result.Data==null)return WorkflowVerification.Unknown;var status=result.Data.Value.Deserialize<ProxyStatus>()!;return (request.Action=="proxy_on"?status.Running&&status.Enabled==true:status.Enabled==false)?WorkflowVerification.Confirmed:WorkflowVerification.NotApplied;}
        if(request.Action=="music_client"){var result=await commands.ExecuteDetailedAsync(new("music_status","","workflow-verify"),token);if(result.Status!="succeeded"||result.Data==null)return WorkflowVerification.Unknown;return result.Data.Value.Deserialize<MusicClientStatus>()!.Ready?WorkflowVerification.Confirmed:WorkflowVerification.Unknown;}
        if(request.Action=="match_add")return WorkflowVerification.NotApplied; // The bridge deduplicates by persisted provider/event identity.
        if(request.Action is "todos_open" or "football_open" or "chat_open" or "match_lookup")return WorkflowVerification.NotApplied;
        return WorkflowVerification.Unknown;
    }
    (string,Func<Task>) BuildComboStep(ComboStep step)
    {
        string label=step.Label.Length>0?step.Label:step.Kind;
        if(step.Kind=="command")return(label,async()=>{await commands.ExecuteTextAsync(step.Value,"combo");});
        var request=ComboRequest(step);Modules.Require(prefs,Modules.ForAction(request.Action));return(label,async()=>{await commands.ExecuteAsync(request);});
    }
    ActionRequest ComboRequest(ComboStep step)
    {
        string action=step.Kind switch{"url"=>"web_open","app"=>"app_open","watch"=>"watch_open","qq_client"=>"music_client","todos"=>"todos_open","football"=>"football_open","chat"=>"chat_open",_=>step.Kind};string value=step.Value;
        if(step.Kind=="app"){var app=launcher.FileApplications.FirstOrDefault(a=>a.Target.Equals(value,StringComparison.OrdinalIgnoreCase))??throw new ArgumentException("应用未找到或索引尚未就绪");value=AppTools.Id(app);}
        if(step.Kind=="url")FootTools.ValidateUrl(value);
        if(step.Kind=="watch"){var links=new WatchLinkStore(previewMode?null:Path.Combine(Store.Root,"leo-watch-links.json")).Items;int index=int.Parse(value);if(index<0)index=Array.FindIndex(links,l=>l.Url.Length>0);if(index<0||index>=links.Length||links[index].Url.Length==0)throw new ArgumentException("请先填写观赛网址");value=(index+1).ToString();}
        return new(action,value,"combo");
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
            foreach(var combo in store.Items){string id=combo.Id;var choice=new ToolStripMenuItem(combo.Name);UiLanguage.Preserve(choice);choice.Click+=async(_,_)=>await RunCombo(id);root.DropDownItems.Add(choice);}
            if(store.Items.Count==0)root.DropDownItems.Add(new ToolStripMenuItem(store.Error??"暂无自定义组合"){Enabled=false});
            root.DropDownItems.Add(new ToolStripSeparator());root.DropDownItems.Add("＋ 新建自定义…",null,(_,_)=>EditCombo(null,root.DropDown.Bounds));
            var edit=new ToolStripMenuItem("编辑 / 删除");foreach(var combo in store.Items){string id=combo.Id;var choice=new ToolStripMenuItem(combo.Name);UiLanguage.Preserve(choice);choice.Click+=(_,_)=>EditCombo(id,edit.DropDown.Bounds);edit.DropDownItems.Add(choice);}edit.Enabled=store.Items.Count>0;root.DropDownItems.Add(edit);BarcaMenus.StyleSubmenu(edit,menu.Font);
        };
    }
}
