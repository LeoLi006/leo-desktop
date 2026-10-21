using System.Text.Json;
namespace LeoLauncher;
// Internal adapter only: keep the LeoModule/1 package and stdio wire format unchanged.
sealed class ExtensionLeoModule:IStructuredLeoModule,IActionRequestAdapter,IModuleContractSource,IModuleEntryContributor
{
    readonly ExtensionManifest manifest;readonly Preferences prefs;readonly ActionDispatcher dispatcher;readonly string definition;
    public ExtensionLeoModule(ExtensionManifest manifest,Preferences prefs,ActionDispatcher dispatcher){ExtensionStore.Validate(manifest);this.manifest=JsonSerializer.Deserialize<ExtensionManifest>(JsonSerializer.Serialize(manifest))!;this.prefs=prefs;this.dispatcher=dispatcher;definition=JsonSerializer.Serialize(this.manifest);}
    public string Id=>"ext:"+manifest.Id;public string Name=>manifest.Name;public string Version=>System.Version.Parse(manifest.Version).ToString();
    public bool Enabled=>!prefs.DisabledExtensions.Contains(manifest.Id);
    public IReadOnlyList<string> Actions=>manifest.Commands.Select(c=>"ext:"+manifest.Id+":"+c.Id).ToArray();
    public bool ConfigurationIsCurrent=>dispatcher.Extensions.Load().Any(m=>m.Id==manifest.Id&&JsonSerializer.Serialize(m)==definition);
    public int MaximumInputLength=>16000; // Preserve the old dispatcher limit for explicit worker overrides.
    public CapabilityDefinition Describe(string action)=>new(action,Id,manifest.Runtime=="stdio"?"stdio":"in-process","extension-value/v1","external","coalesce-inflight",manifest.Runtime=="stdio"?manifest.TimeoutSeconds*1000:120000);
    public ModuleContract DescribeModule()=>ModuleContracts.Build(this,"legacy-extension","旧扩展适配","optional","host.extension-data",[new("extension-target",manifest.Runtime=="stdio"?"local-file":"host-service","required","existing-execution-check",Actions.ToArray())],_ =>("value-wrapper","parameters仅接受可选value字符串；声明式扩展不得覆盖固定参数；旧字符串入口保留原处理。","extension-result","声明式转交真实目标的结构化结果；worker沿用ok/message及退出码，完成响应不证明任意外部效果。",true,["unknown","accepted","snapshot","committed","window-shown","state-observed","completed","released","cached","steps-completed"]));
    public DependencyState[] ReadDependencies()
    {
        string state="unknown";try{state=manifest.Runtime=="stdio"?(File.Exists(dispatcher.Extensions.ExecutablePath(manifest))?"ready":"missing"):(manifest.Commands.All(c=>dispatcher.Registry.Resolve(c.Action).Enabled)?"ready":"missing");}catch(Exception e)when(FeatureFaults.Recoverable(e)){state="missing";}return [new("extension-target",state)];
    }
    public EntryContribution[] DescribeEntries()=>manifest.Commands.Select(c=>new EntryContribution(Id+"/"+c.Id,Id,"ext:"+manifest.Id+":"+c.Id,c.Title,"",Enabled,EntryDisplay.Search,ModuleEntryContributions.TargetPrefix+Id+"/"+c.Id,c.Phrases.ToArray(),"module-action"){Parameters="{}",Glyph="terminal",Hint="扩展命令",MenuActions=[new(c.Title,"ext:"+manifest.Id+":"+c.Id,"{}")] }).ToArray();
    public ActionRequest Adapt(StructuredActionRequest request)
    {
        if(request.Protocol!="leo-action/1"||!Actions.Contains(request.ActionId)||request.Parameters.ValueKind!=JsonValueKind.Object)throw new ArgumentException("扩展请求协议或动作无效");
        var fields=request.Parameters.EnumerateObject().ToArray();if(fields.Length>1||fields.Any(f=>f.Name!="value"||f.Value.ValueKind!=JsonValueKind.String))throw new ArgumentException("扩展只接受可选value字符串");
        string value=fields.Length==0?"":fields[0].Value.GetString()!;if(value.Length>MaximumInputLength||manifest.Runtime=="actions"&&value.Length>0)throw new ArgumentException("扩展参数过长，或声明式参数不可覆盖");
        return new(request.ActionId,value,request.Source,request.RequestId,request.TraceId,request.Deadline);
    }
    public async Task<ActionPayload> ExecuteStructuredAsync(ActionRequest request,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(!Enabled)throw new InvalidOperationException(Name+"已停用");if(!ConfigurationIsCurrent)throw new ApplicationConfigurationChangedException();
        var command=manifest.Commands.Single(c=>request.Action=="ext:"+manifest.Id+":"+c.Id);
        if(manifest.Runtime=="stdio"){string message=await RunWorker(request,command,token);return new(message,Evidence:"completed",PrivateData:true,DiagnosticMessage:"扩展响应已完成。");}
        var result=await dispatcher.ExecuteDetailedAsync(new(command.Action,command.Value,"extension:"+manifest.Id,TraceId:request.TraceId,Deadline:request.Deadline),token);result.ToLegacy(token);
        return new(result.Message,result.Data,result.Evidence,true,"扩展目标操作已完成。");
    }
    public async Task<string> ExecuteAsync(ActionRequest request,CancellationToken token)=>(await ExecuteStructuredAsync(request,token)).Message;
    public string FailureCode(Exception error)=>error is ArgumentException or JsonException?"invalid_request":error is ApplicationConfigurationChangedException?"configuration_changed":error is TimeoutException?"worker_timeout":error is IOException or UnauthorizedAccessException?"extension_failed":"extension_failed";
    async Task<string> RunWorker(ActionRequest request,ExtensionCommand command,CancellationToken token){using var reservation=ModuleWorker.Reserve(manifest.Id);return await ClientOperations.Hub.Run("module:"+manifest.Id,command.Id+":"+request.Value,op=>{if(!Enabled)throw new InvalidOperationException(Name+"已停用");op.Report("正在运行扩展 · "+Name);return ModuleWorker.ExecuteAsync(dispatcher.Extensions,manifest,request.Value.Length>0?command with{Value=request.Value}:command,op.Token,request.Source);},token);}
}
