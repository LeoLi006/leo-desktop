using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace LeoLauncher;

sealed class CapabilityException(string code,string message):InvalidOperationException(message){public string Code=>code;}
sealed class ApplicationConfigurationChangedException():OperationCanceledException("应用配置已改变");

sealed record ActionPayload(string Message,JsonElement? Data=null,string Evidence="unknown",bool PrivateData=false);
interface IActionRequestAdapter{ActionRequest Adapt(StructuredActionRequest request);}
interface IStructuredLeoModule:ILeoModule
{
    int MaximumInputLength=>16000;
    CapabilityDefinition Describe(string action);
    bool ConfigurationIsCurrent{get;}
    Task<ActionPayload> ExecuteStructuredAsync(ActionRequest request,CancellationToken token);
    string FailureCode(Exception error);
}
sealed record ActionResult(string RequestId,string TraceId,string Source,string ActionId,string ModuleId,string ExecutionId,string Status,string Message,string? ErrorCode,JsonElement? Data,string Evidence,bool Retryable=false)
{
    [JsonIgnore]public bool PrivateData{get;init;}
    [JsonIgnore]public Exception? Failure{get;init;}
    public string ToLegacy(CancellationToken caller)
    {
        if(Status=="succeeded")return Message;
        if(ErrorCode=="timeout")throw new TimeoutException(Message);
        if(Status=="cancelled")throw new OperationCanceledException(Message,caller);
        if(Failure!=null)ExceptionDispatchInfo.Capture(Failure).Throw();
        throw new InvalidOperationException(Message);
    }
}
sealed record ActionMetrics(long Requests,long Executions,long Coalesced,long Succeeded,long Failed,long Cancelled,long TimedOut);
sealed record StructuredActionRequest(string Protocol,string ActionId,JsonElement Parameters,string Source="ui",string? RequestId=null,string? TraceId=null,DateTimeOffset? Deadline=null);

static class ApplicationRequestAdapter
{
    public static ActionRequest Adapt(StructuredActionRequest request)
    {
        if(request.Protocol!="leo-action/1")throw new ArgumentException("不支持的动作请求版本");
        if(request.ActionId!="local_app_open"&&!request.ActionId.StartsWith("local:",StringComparison.Ordinal))throw new ArgumentException("此动作尚未迁移到结构化接口");
        var p=request.Parameters;
        if(p.ValueKind!=JsonValueKind.Object||p.EnumerateObject().Count()!=1||!p.TryGetProperty("applicationId",out var id)||id.ValueKind!=JsonValueKind.String||!Guid.TryParseExact(id.GetString(),"N",out _))throw new ArgumentException("需要唯一的 applicationId，不支持额外参数");
        string value=id.GetString()!;if(request.ActionId.StartsWith("local:")&&request.ActionId!="local:"+value)throw new ArgumentException("应用标识与动作不一致");
        return new("local_app_open",value,request.Source,request.RequestId,request.TraceId,request.Deadline);
    }
}
