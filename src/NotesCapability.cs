using System.Text.Json;
namespace LeoLauncher;

sealed class NotesCapability(Func<NoteStore> store,Func<string,string,CancellationToken,Task> show):IStructuredLeoModule,IActionRequestAdapter,IBrainContributor
{
    readonly object gate=new();
    readonly Dictionary<string,(string Action,string Value,ActionPayload Payload)> completed=new();
    public ActionRequest Adapt(StructuredActionRequest request)
    {
        if(request.Protocol!="leo-action/1"||!Actions.Contains(request.ActionId)||request.Parameters.ValueKind!=JsonValueKind.Object)throw new ArgumentException("便签动作请求无效");
        var p=request.Parameters;string[] allowed=request.ActionId switch{"notes_open" or "note_add" or "note_quick_open"=>["text"],"note_save"=>["id","text"],"note_query"=>["text","scope"],"note_remove"=>["id"],"note_pin"=>["id","value"],"note_link"=>["id","task"],_=>[]};
        if(p.EnumerateObject().Any(item=>!allowed.Contains(item.Name))||p.EnumerateObject().Select(item=>item.Name).Distinct().Count()!=p.EnumerateObject().Count())throw new ArgumentException("便签参数包含多余或重复字段");
        var value=request.ActionId is "notes_open" or "note_add" or "note_quick_open"?(p.TryGetProperty("text",out var text)?text.GetString()??"":""):p.GetRawText();
        return new(request.ActionId,value,request.Source,request.RequestId,request.TraceId,request.Deadline);
    }
    public IReadOnlyList<BrainAction> BrainActions=>[
        new("note_query","查询便签","notes-query",true,"value为JSON {text,scope}；scope为active、linked或all",Check:value=>{using var doc=JsonDocument.Parse(value);var p=doc.RootElement;if(p.ValueKind!=JsonValueKind.Object||p.EnumerateObject().Any(x=>x.Name is not("text" or "scope"))||p.TryGetProperty("scope",out var scope)&&scope.GetString() is not("active" or "linked" or "all"))throw new ArgumentException("便签查询参数无效");}),
        new("note_add","保存新便签","text",Hint:"1～2000字符；只记下用户明确提供的内容",Check:value=>{if(value.Trim().Length is <1 or >2000)throw new ArgumentException("便签内容须为1～2000字符");}),
        new("notes_open","打开便签","text",Hint:"可用空字符串，非空文字只作为草稿",Check:value=>{if(value.Length>2000)throw new ArgumentException("草稿过长");}),
        new("note_quick_open","打开一句话速记","none",Check:value=>{if(value.Length!=0)throw new ArgumentException("速记入口不接受额外参数");})];
    public string Id=>"notes";public string Name=>"临时便签";public string Version=>"1";public bool Enabled=>true;
    public bool ConfigurationIsCurrent=>true;
    public IReadOnlyList<string> Actions=>["notes_open","note_quick_open","note_add","note_save","note_query","note_remove","note_pin","note_link"];
    public int MaximumInputLength=>125000;
    public CapabilityDefinition Describe(string action)=>new(action,Id,"in-process","legacy-text-or-json",action is "notes_open" or "note_quick_open"?"window":action=="note_query"?"read":"write","serialized-store",15000);
    public string FailureCode(Exception e)=>e is ObjectDisposedException?"host_closed":e is ArgumentException or JsonException or KeyNotFoundException or InvalidOperationException?"invalid_request":e is IOException or UnauthorizedAccessException?"storage_failed":"note_failed";
    public async Task<string> ExecuteAsync(ActionRequest request,CancellationToken token)=>(await ExecuteStructuredAsync(request,token)).Message;
    public Task<ActionPayload> ExecuteStructuredAsync(ActionRequest request,CancellationToken token)
    {
        if(request.Action is "notes_open" or "note_quick_open")return Open();
        return Task.Run(()=>ExecuteData(request,token),token);
        async Task<ActionPayload> Open(){await show(request.Action,request.Value,token);return new("已打开临时便签。",Evidence:"window-shown");}
    }
    ActionPayload ExecuteData(ActionRequest request,CancellationToken token)
    {
        lock(gate){
            token.ThrowIfCancellationRequested();
            if(request.RequestId is {} key&&completed.TryGetValue(key,out var saved)){
                if(saved.Action!=request.Action||saved.Value!=request.Value)throw new ArgumentException("同一请求标识不能用于不同内容");return saved.Payload;
            }
            var notes=store();ActionPayload payload;
            if(request.Action=="note_add")payload=Payload("已记下便签。",notes.Save(null,request.Value,token));
            else{
                using var doc=JsonDocument.Parse(request.Value);var p=doc.RootElement;
                string Text(string name)=>p.GetProperty(name).GetString()??throw new ArgumentException("缺少参数 "+name);
                payload=request.Action switch{
                    "note_save"=>Payload("已保存到本机",notes.Save(p.TryGetProperty("id",out var id)&&id.ValueKind!=JsonValueKind.Null?id.GetString():null,Text("text"),token)),
                    "note_query"=>Payload("已查询便签。",notes.Query(p.TryGetProperty("text",out var text)?text.GetString()??"":"",p.TryGetProperty("scope",out var scope)?scope.GetString()??"active":"active"),"snapshot"),
                    "note_remove"=>Edit(()=>notes.Remove(Text("id"),token)),
                    "note_pin"=>Edit(()=>notes.Pin(Text("id"),p.GetProperty("value").GetBoolean(),token)),
                    "note_link"=>Edit(()=>notes.Link(Text("id"),Text("task"),token)),
                    _=>throw new ArgumentException("未知便签动作")
                };
            }
            // In-memory replay protects explicit retries; no business text is written to a diagnostic log.
            if(request.Action!="note_query"&&request.RequestId is {} requestId){completed[requestId]=(request.Action,request.Value,payload);while(completed.Count>128)completed.Remove(completed.Keys.First());}
            return payload;
        }
    }
    static ActionPayload Edit(Action action){action();return new("便签已更新。",Evidence:"committed",PrivateData:true);}
    static ActionPayload Payload<T>(string message,T data,string evidence="committed")=>new(message,JsonSerializer.SerializeToElement(data),evidence,PrivateData:true);
}

// Preserve synchronous editor save/close semantics. Store work runs on the pool and never waits for UI callbacks.
// Window actions must use the asynchronous dispatcher entry instead of this facade.
sealed class NotesClient(NoteStore store,ActionDispatcher dispatcher,string source="notes-ui",bool ownsDispatcher=false):IDisposable
{
    string? pendingCreateText,pendingCreateRequest;
    public void Dispose(){if(ownsDispatcher)dispatcher.Dispose();}
    public event Action? Changed{add=>store.Changed+=value;remove=>store.Changed-=value;}
    public List<StickyNote> Items=>Query(scope:"all");
    ActionResult Run(string action,object value,string? requestId=null)
    {var result=dispatcher.ExecuteDetailedAsync(new(action,JsonSerializer.Serialize(value),source,requestId)).GetAwaiter().GetResult();result.ToLegacy(default);return result;}
    public StickyNote Save(string? id,string text)
    {
        string? request=null;if(id==null){if(pendingCreateText!=text){pendingCreateText=text;pendingCreateRequest=Guid.NewGuid().ToString("N");}request=pendingCreateRequest;}
        var note=Run("note_save",new{id,text},request).Data!.Value.Deserialize<StickyNote>()!;
        if(id==null){pendingCreateText=null;pendingCreateRequest=null;}return note;
    }
    public List<StickyNote> Query(string text="",string scope="active")=>Run("note_query",new{text,scope}).Data!.Value.Deserialize<List<StickyNote>>()!;
    public void Remove(string id)=>Run("note_remove",new{id});
    public void Pin(string id,bool value)=>Run("note_pin",new{id,value});
    public void Link(string id,string task)=>Run("note_link",new{id,task});
    public static NotesClient Standalone(NoteStore store){var dispatcher=new ActionDispatcher(new(),(_,_,_)=>throw new InvalidOperationException("便签适配器不应调用旧入口"));dispatcher.Registry.Register(new NotesCapability(()=>store,(_,_,_)=>Task.CompletedTask));return new(store,dispatcher,ownsDispatcher:true);}
}

sealed partial class PetForm
{
    void RegisterNotesCapability()
    {
        var windows=new ModuleWindowService(this);
        commands.Registry.Register(new NotesCapability(()=>Notes,(action,value,token)=>windows.ShowAsync(()=>{if(action=="note_quick_open")ShowQuickNoteCore();else ShowNotesCore(value);},token)));
    }
    async void RunNotesEntry(string action,string value,string source)
    {
        try{var result=await commands.ExecuteDetailedAsync(new(action,value,source));if(!IsDisposed&&!Disposing)new ModuleFeedbackService(OperationFeedback.Show,text=>OperationFeedback.Error(text)).Report(result);}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){FeatureFaults.Report("便签入口",e);}
    }
}
