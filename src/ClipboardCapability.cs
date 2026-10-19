using System.Text.Json;
namespace LeoLauncher;
sealed class ClipboardCapability(ClipboardHistoryService service,Preferences prefs,Func<Action,CancellationToken,Task> ui,Action show):IStructuredLeoModule,IActionRequestAdapter,IBrainContributor
{
    public string Id=>"clipboard";public string Name=>"剪贴板历史与收藏";public string Version=>"1";public bool Enabled=>true;public bool ConfigurationIsCurrent=>true;
    public IReadOnlyList<string> Actions=>["clipboard_open","clipboard_pin_text","clipboard_query","clipboard_copy","clipboard_pin","clipboard_delete","clipboard_clear","clipboard_undo","clipboard_recording"];
    public IReadOnlyList<BrainAction> BrainActions=>[new("clipboard_open","打开剪贴板历史与收藏","none",Check:v=>{if(v.Length!=0)throw new ArgumentException("剪贴板窗口不接受正文参数");})];
    public CapabilityDefinition Describe(string a)=>new(a,Id,"in-process","clipboard-local",a=="clipboard_open"?"window":a=="clipboard_query"?"read":"write","serialized-store",15000);
    public int MaximumInputLength=>100000;
    public string FailureCode(Exception e)=>e is ArgumentException?"invalid_request":e is IOException or UnauthorizedAccessException?"storage_failed":"clipboard_failed";
    public ActionRequest Adapt(StructuredActionRequest r)
    {
        if(r.Protocol!="leo-action/1"||!Actions.Contains(r.ActionId)||r.Parameters.ValueKind!=JsonValueKind.Object)throw new ArgumentException("剪贴板请求无效");
        var fields=r.Parameters.EnumerateObject().ToArray();string? key=r.ActionId switch{"clipboard_query"=>"query","clipboard_pin_text"=>"text","clipboard_pin" or "clipboard_delete" or "clipboard_copy"=>"id","clipboard_recording"=>"enabled",_=>null};
        if(fields.Length>(key==null?0:1)||fields.Any(x=>x.Name!=key||(r.ActionId=="clipboard_recording"?x.Value.ValueKind is not(JsonValueKind.True or JsonValueKind.False or JsonValueKind.String):x.Value.ValueKind!=JsonValueKind.String)))throw new ArgumentException("剪贴板参数包含额外、重复或无效字段");
        return new(r.ActionId,fields.Length==0?"":fields[0].Value.ValueKind==JsonValueKind.String?fields[0].Value.GetString()!:fields[0].Value.GetRawText(),r.Source,r.RequestId,r.TraceId,r.Deadline);
    }
    public async Task<ActionPayload> ExecuteStructuredAsync(ActionRequest r,CancellationToken t)
    {
        if(r.Source.StartsWith("brain-",StringComparison.Ordinal)&&r.Action!="clipboard_open")throw new ArgumentException("剪贴板正文仅在本地窗口管理，不向模型提供");
        ActionPayload? result=null;await ui(()=>{t.ThrowIfCancellationRequested();var history=service.History;
            ClipItem Item()=>history.Items.SingleOrDefault(x=>x.Key==r.Value)??throw new ArgumentException("记录已失效，请刷新后选择");
            switch(r.Action){
                case "clipboard_open":if(r.Value.Length!=0)throw new ArgumentException("此窗口不接受正文参数");show();result=new("已打开剪贴板历史与收藏。",Evidence:"window-shown");return;
                case "clipboard_query":if(r.Value.Length>1000)throw new ArgumentException("搜索文字过长");var keys=history.Search(r.Value).Select(x=>x.Key).ToArray();result=new("已查询本地剪贴板。",JsonSerializer.SerializeToElement(keys),"snapshot",true,"已查询本地剪贴板。");return;
                case "clipboard_pin_text":if(string.IsNullOrWhiteSpace(r.Value)||r.Value.Length>100000)throw new ArgumentException("文字为空或过长");if(prefs.ClipboardBlockSecrets&&ClipboardPrivacy.Sensitive(r.Value))throw new ArgumentException("敏感内容保护已阻止收藏此段文字");var item=new ClipItem("text",r.Value,r.Value.Replace('\r',' ').Replace('\n',' ')[..Math.Min(100,r.Value.Length)],DateTimeOffset.Now);if(!history.AddPinned(item))throw new IOException(history.Error??"剪贴板收藏已达上限");break;
                case "clipboard_copy":service.Copy(Item());result=new("已处理剪贴板复制。"+(history.Error==null?"":" 历史保存失败，原记录仍保留。"),Evidence:"accepted",PrivateData:true,DiagnosticMessage:"已处理剪贴板复制。");return;
                case "clipboard_pin":var selected=Item();if(!history.Pin(selected,!selected.Pinned))throw new IOException(history.Error??"剪贴板收藏已达上限");break;
                case "clipboard_delete":history.Delete(Item());break;
                case "clipboard_clear":Empty();history.Clear();break;
                case "clipboard_undo":Empty();history.Undo();break;
                case "clipboard_recording":if(r.Value is not("true" or "false"))throw new ArgumentException("记录开关无效");service.Enabled=r.Value=="true";result=new("剪贴板记录开关已更新。",Evidence:"snapshot");return;
                default:throw new ArgumentException("剪贴板动作不存在");
            }
            if(history.Error!=null)throw new IOException(history.Error);result=new("剪贴板操作已完成。",Evidence:"snapshot",PrivateData:true,DiagnosticMessage:"剪贴板操作已完成。");
            void Empty(){if(r.Value.Length!=0)throw new ArgumentException("此操作不接受正文参数");}
        },t);return result!;
    }
    public async Task<string> ExecuteAsync(ActionRequest r,CancellationToken t)=>(await ExecuteStructuredAsync(r,t)).Message;
}
sealed class ClipboardClient(ClipboardHistoryService service,ActionDispatcher bus,string source="clipboard-ui",bool ownsDispatcher=false):IDisposable
{
    ActionResult Run(string action,string value=""){var result=bus.ExecuteDetailedAsync(new(action,value,source)).GetAwaiter().GetResult();result.ToLegacy(default);return result;}
    public List<ClipItem> Search(string query){var keys=Run("clipboard_query",query).Data!.Value.Deserialize<string[]>()!.ToHashSet();return service.History.Items.Where(x=>keys.Contains(x.Key)).ToList();}
    public void Execute(string action,string value="")=>Run(action,value);
    public void Dispose(){if(ownsDispatcher)bus.Dispose();}
    public static ClipboardClient Standalone(ClipboardHistoryService service,Preferences prefs,Action show){var bus=new ActionDispatcher(prefs,(_,_,_)=>throw new InvalidOperationException("剪贴板不能绕回旧入口"));bus.Registry.Register(new ClipboardCapability(service,prefs,(a,t)=>{t.ThrowIfCancellationRequested();a();return Task.CompletedTask;},show));return new(service,bus,ownsDispatcher:true);}
}
