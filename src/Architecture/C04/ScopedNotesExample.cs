using System.Text.Json;
namespace LeoLauncher;

// Compiled, opt-in example only. Never registered by the production host.
sealed record ExampleNotesConfiguration(bool Enabled,int MaximumTextLength=2000);
interface IExampleNotesServices
{
    ExampleNotesConfiguration Configuration{get;}
    DependencyState[] Dependencies();
    Task<ActionPayload> Query(string text,CancellationToken token);
    Task<ActionPayload> Save(string text,string requestId,CancellationToken token);
}
// Host binds one store. The module receives no path, Preferences, dispatcher or Form.
// Reuse the existing notes implementation for persistence and replay semantics.
sealed class ScopedNotesExampleServices: IExampleNotesServices,IDisposable
{
    readonly Func<ExampleNotesConfiguration> configuration;readonly NotesCapability notes;
    readonly ActivityOwner life=new("example-scoped-notes");bool closed;
    public ScopedNotesExampleServices(NoteStore store,Func<ExampleNotesConfiguration> configuration)
    {this.configuration=configuration;notes=new(()=>store,(_,_,_)=>throw new InvalidOperationException("样例不提供窗口"));}
    public ExampleNotesConfiguration Configuration=>closed?new(false):configuration();
    public DependencyState[] Dependencies()=>[new("scoped-notes",closed?"missing":"ready")];
    async Task<ActionPayload> Execute(string action,object parameters,string? id,CancellationToken token)
    {
        using var request=life.Begin("data",token);request.Token.ThrowIfCancellationRequested();
        if(!Configuration.Enabled)throw new InvalidOperationException("样例已停用");
        var result=await notes.ExecuteStructuredAsync(new(action,JsonSerializer.Serialize(parameters),"example-service",id),request.Token);
        if(action=="note_query")request.ThrowIfNotCurrent();
        return result;
    }
    public Task<ActionPayload> Query(string text,CancellationToken token)=>Execute("note_query",new{text,scope="all"},null,token);
    public Task<ActionPayload> Save(string text,string requestId,CancellationToken token)=>Execute("note_save",new{text},requestId,token);
    public void Dispose(){if(closed)return;closed=true;life.Dispose();}
}
sealed class ScopedNotesExampleModule(IExampleNotesServices services):IStructuredLeoModule,IActionRequestAdapter,IModuleContractSource
{
    public string Id=>"example-scoped-notes";public string Name=>"限定服务接入样例";public string Version=>"1";
    public bool Enabled=>services.Configuration.Enabled;public bool ConfigurationIsCurrent=>true;
    public IReadOnlyList<string> Actions=>["example_notes_query","example_notes_save"];
    public int MaximumInputLength=>12000;
    public DependencyState[] ReadDependencies()=>services.Dependencies();
    public CapabilityDefinition Describe(string action)=>new(action,Id,"in-process","text-object",action==Actions[0]?"read":"write","serialized-store",5000);
    public ModuleContract DescribeModule()=>ModuleContracts.Build(this,"example",Name,"optional","host.scoped-example-notes",
        [new("scoped-notes","host-service","required","reject-unavailable",Actions.ToArray())],action=>
        ("object","只接受text字符串；保存要求非空，受限定配置的长度约束。","notes-example", "现有便签快照或已提交记录，含私有数据。",true,action==Actions[0]?["snapshot"]:["committed"]));
    public ActionRequest Adapt(StructuredActionRequest request)
    {
        if(request.Protocol!="leo-action/1"||!Actions.Contains(request.ActionId))throw new ArgumentException("无效样例请求");
        Validate(request.ActionId,request.Parameters,false);
        return new(request.ActionId,request.Parameters.GetRawText(),request.Source,request.RequestId,request.TraceId,request.Deadline);
    }
    string Validate(string action,JsonElement parameters,bool readConfiguration=true)
    {
        if(!Actions.Contains(action)||parameters.ValueKind!=JsonValueKind.Object)throw new ArgumentException("无效样例参数");
        var fields=parameters.EnumerateObject().ToArray();
        if(fields.Length!=1||fields[0].Name!="text"||fields[0].Value.ValueKind!=JsonValueKind.String)throw new ArgumentException("只允许text参数");
        // Pure input adaptation must not call a potentially failing host service;
        // configuration reads belong inside the dispatcher's execution boundary.
        var config=readConfiguration?services.Configuration:new(true);string text=fields[0].Value.GetString()!;
        if(config.MaximumTextLength is <1 or >2000||text.Length>config.MaximumTextLength||action==Actions[1]&&string.IsNullOrWhiteSpace(text))throw new ArgumentException("样例文字长度无效");
        return text;
    }
    public Task<ActionPayload> ExecuteStructuredAsync(ActionRequest request,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();using var doc=JsonDocument.Parse(request.Value);string text=Validate(request.Action,doc.RootElement);
        if(!services.Dependencies().Any(d=>d.Id=="scoped-notes"&&d.State=="ready"))throw new InvalidOperationException("数据服务不可用");
        return request.Action==Actions[0]?services.Query(text,token):services.Save(text,request.RequestId??throw new ArgumentException("缺少请求标识"),token);
    }
    public async Task<string> ExecuteAsync(ActionRequest request,CancellationToken token)=>(await ExecuteStructuredAsync(request,token)).Message;
    public string FailureCode(Exception error)=>error is IOException or UnauthorizedAccessException?"storage_failed":error is ObjectDisposedException?"host_closed":error is ArgumentException or JsonException or InvalidOperationException?"invalid_request":"service_failed";
}
