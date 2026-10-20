namespace LeoLauncher;

// Developer example only. Not registered automatically, not an installable code package.
sealed class MinimalContractModule:IStructuredLeoModule,IActionRequestAdapter,IModuleContractSource
{
    public string Id=>"example-contract";public string Name=>"契约最小样例";public string Version=>"1.0";
    public bool Enabled=>true;public bool ConfigurationIsCurrent=>true;
    public IReadOnlyList<string> Actions=>["example_status"];
    public CapabilityDefinition Describe(string action)=>new(action,Id,"in-process","empty-object","read","coalesce-inflight",5000);
    public ModuleContract DescribeModule()=>ModuleContracts.Build(this,"example",Name,"host-core","none",[],_=>
        ("object","parameters必须为空对象；旧value必须为空。","example-status","返回内存内的ready布尔值，不访问设备或个人数据。",false,["snapshot"]));
    public DependencyState[] ReadDependencies()=>[];
    public ActionRequest Adapt(StructuredActionRequest request)=>ClientCapabilityParameters.Adapt(request,Actions,false);
    public Task<ActionPayload> ExecuteStructuredAsync(ActionRequest request,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(!Actions.Contains(request.Action)||request.Value.Length!=0)throw new ArgumentException("样例只接受空参数");
        return Task.FromResult(new ActionPayload("样例就绪",System.Text.Json.JsonSerializer.SerializeToElement(new{ready=true}),"snapshot"));
    }
    public async Task<string> ExecuteAsync(ActionRequest request,CancellationToken token)=>(await ExecuteStructuredAsync(request,token)).Message;
    public string FailureCode(Exception error)=>"invalid_request";
}
