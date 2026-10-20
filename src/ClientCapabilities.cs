using System.Text.Json;
namespace LeoLauncher;

// One resource queue per native client, shared across configuration generations.
sealed class NativeCapabilityQueue
{
    readonly SemaphoreSlim gate=new(1,1);
    public async Task<T> Run<T>(string resource,Func<CancellationToken,Action<string>,Task<T>> work,CancellationToken token,bool feedback=true)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(token);
        string operationId=resource+":"+Guid.NewGuid().ToString("N");bool cancelShown=false;
        var context=new ClientOperation(n=>{if(feedback)try{if(n.Retry!=null)cancelShown=true;OperationFeedback.Sink?.Invoke(n with{Module=resource,OperationId=operationId});}catch(Exception e)when(FeatureFaults.Recoverable(e)){FeatureFaults.Report("客户端操作反馈",e);}});
        using var caller=token.Register(context.Cancel);using var cancelled=context.Token.Register(linked.Cancel);
        if(feedback)_=context.OfferCancel();bool entered=false;
        try{context.Report("正在等待前一个操作…");await gate.WaitAsync(linked.Token);entered=true;linked.Token.ThrowIfCancellationRequested();return await work(linked.Token,context.Report);}
        finally{context.Finish();if(entered)gate.Release();if(cancelShown)try{OperationFeedback.Sink?.Invoke(new("操作等待已结束，请查看执行结果。",Module:resource,OperationId:operationId));}catch(Exception e)when(FeatureFaults.Recoverable(e)){FeatureFaults.Report("客户端操作反馈",e);}}
    }
}
sealed class ProxyCapability(Func<bool> enabled,Func<string> configuration,Func<bool> available,Func<CancellationToken,Task<ProxyStatus>> read,Func<bool,CancellationToken,Action<string>,Task<ProxyStatus>> set,NativeCapabilityQueue queue,bool preview=false):IStructuredLeoModule,IActionRequestAdapter,IBrainContributor,IModuleContractSource
{
    public ModuleContract DescribeModule()=>ModuleContracts.Proxy(this);
    public DependencyState[] ReadDependencies()=>[new("clash-client","unknown")];

    readonly string generation=configuration();
    public string Id=>"clash";public string Name=>"Clash 系统代理";public string Version=>"1";public bool Enabled=>enabled();
    public bool ConfigurationIsCurrent=>generation==configuration();public string BrainStamp=>configuration();
    public bool BrainAvailable{get{try{return ConfigurationIsCurrent&&available();}catch(Exception e)when(FeatureFaults.Recoverable(e)){return false;}}}
    public IReadOnlyList<string> Actions=>["proxy_status","proxy_on","proxy_off"];
    public IReadOnlyList<BrainAction> BrainActions=>Actions.Select(a=>new BrainAction(a,a switch{"proxy_status"=>"查询系统代理状态","proxy_on"=>"开启系统代理",_=>"关闭系统代理"},"none",a=="proxy_status",Check:Empty,TimeoutMs:a=="proxy_status"?5000:60000)).ToArray();
    static void Empty(string value){if(value.Length!=0)throw new ArgumentException("代理动作不接受额外参数");}
    public ActionRequest Adapt(StructuredActionRequest r)=>ClientCapabilityParameters.Adapt(r,Actions,false);
    public CapabilityDefinition Describe(string a)=>new(a,Id,"in-process","none",a=="proxy_status"?"read":"external","coalesce-inflight",a=="proxy_status"?5000:60000);
    public string FailureCode(Exception e)=>e is ArgumentException?"invalid_request":e is ApplicationConfigurationChangedException?"configuration_changed":"proxy_unconfirmed";
    void Current(CancellationToken t){t.ThrowIfCancellationRequested();if(!Enabled||!ConfigurationIsCurrent)throw new ApplicationConfigurationChangedException();}
    public async Task<ActionPayload> ExecuteStructuredAsync(ActionRequest r,CancellationToken token)
    {
        Empty(r.Value);Current(token);
        if(r.Action=="proxy_status"){var s=preview?new(true,false,"示例代理已关闭"):await read(token);Current(token);return new(JsonSerializer.Serialize(s),JsonSerializer.SerializeToElement(s),s.Enabled==null?"unknown":"state-observed",DiagnosticMessage:s.Description);}
        if(!Actions.Contains(r.Action))throw new ArgumentException("未知代理动作");
        if(preview)return new("示例模式，不切换系统代理",Evidence:"unknown");
        return await queue.Run("clash",async(t,report)=>{Current(t);report("正在检查 Clash…");bool desired=r.Action=="proxy_on";await set(desired,t,report);Current(t);var s=await read(t);Current(t);if(s.Enabled!=desired)throw new IOException("系统代理状态尚未确认，请先检查状态再重试");return new ActionPayload(desired?"系统代理已开启":"系统代理已关闭",JsonSerializer.SerializeToElement(s),"state-observed");},token);
    }
    public async Task<string> ExecuteAsync(ActionRequest r,CancellationToken t)=>(await ExecuteStructuredAsync(r,t)).Message;
}
static class ClientCapabilityParameters
{
    public static ActionRequest Adapt(StructuredActionRequest r,IReadOnlyList<string> actions,bool media)
    {
        if(r.Protocol!="leo-action/1"||!actions.Contains(r.ActionId)||r.Parameters.ValueKind!=JsonValueKind.Object)throw new ArgumentException("客户端动作请求无效");
        string? field=media?r.ActionId=="media_file"?"path":r.ActionId is "media_status" or "media_open"?null:"session":null;
        var props=r.Parameters.EnumerateObject().ToArray();if(props.Length>(field==null?0:1)||props.Any(p=>p.Name!=field||p.Value.ValueKind!=JsonValueKind.String))throw new ArgumentException("参数包含重复、额外或无效字段");
        return new(r.ActionId,props.Length==0?"":props[0].Value.GetString()!,r.Source,r.RequestId,r.TraceId,r.Deadline);
    }
}
sealed class MediaCapabilityState
{
    readonly object gate=new();MediaSession[] sessions=[];DateTimeOffset observed;
    public void Update(MediaSession[] rows){lock(gate){sessions=rows.ToArray();observed=DateTimeOffset.UtcNow;}}
    public MediaSession[] Read(){lock(gate)return DateTimeOffset.UtcNow-observed<TimeSpan.FromMinutes(5)?sessions.ToArray():[];}
}
sealed class MediaCapability(Func<bool> enabled,Func<string> configuration,Func<CancellationToken,Task<MediaSession[]>> read,Func<string,string,CancellationToken,Task<MediaCommandReceipt>> control,Func<string,CancellationToken,Task> open,Func<CancellationToken,Task> show,NativeCapabilityQueue queue,MediaCapabilityState state,bool preview=false):IStructuredLeoModule,IActionRequestAdapter,IBrainContributor,IModuleContractSource
{
    public ModuleContract DescribeModule()=>ModuleContracts.Media(this);
    public DependencyState[] ReadDependencies()=>[new("media-provider",state.Read().Length>0?"ready":"unknown"),new("potplayer","unknown")];

    readonly string generation=configuration();
    public string Id=>"media";public string Name=>"媒体控制";public string Version=>"1";public bool Enabled=>enabled();public bool ConfigurationIsCurrent=>generation==configuration();public string BrainStamp=>configuration();
    public bool BrainAvailable=>ConfigurationIsCurrent;
    public IReadOnlyList<string> Actions=>["media_open","media_file","media_status","media_toggle","media_play","media_pause","media_next","media_previous","media_stop"];
    public IReadOnlyList<BrainAction> BrainActions=>new[]{new BrainAction("media_status","查询本机播放器","none",true,Check:Empty),new BrainAction("media_open","打开媒体控制","none",Check:Empty)}.Concat(state.Read().Length==0?[]:Actions.Where(a=>a is not("media_status" or "media_open" or "media_file")).Select(a=>new BrainAction(a,a switch{"media_play"=>"播放媒体","media_pause"=>"暂停媒体","media_next"=>"媒体下一项","media_previous"=>"媒体上一项","media_stop"=>"停止媒体",_=>"媒体播放/暂停"},"media-session",Hint:"必须先查询媒体会话，使用返回的精确Id；多播放器含糊时先询问",Check:value=>{ValidateSession(value);if(value.Length==0||!state.Read().Any(s=>s.Id==value))throw new ArgumentException("请先查询并明确选择播放器");},TimeoutMs:45000))).ToArray();
    static void Empty(string v){if(v.Length!=0)throw new ArgumentException("此入口不接受额外参数");}
    static void ValidateSession(string v){if(v.Length>256||v.Any(char.IsControl)||v.Length>0&&!v.Contains(':'))throw new ArgumentException("播放器身份无效");}
    void Current(CancellationToken t){t.ThrowIfCancellationRequested();if(!Enabled||!ConfigurationIsCurrent)throw new ApplicationConfigurationChangedException();}
    public ActionRequest Adapt(StructuredActionRequest r)=>ClientCapabilityParameters.Adapt(r,Actions,true);
    public CapabilityDefinition Describe(string a)=>new(a,Id,"in-process","media-session-or-path",a=="media_status"?"read":a=="media_open"?"window":"external","coalesce-inflight",45000);
    public string FailureCode(Exception e)=>e is ArgumentException?"invalid_request":"media_unconfirmed";
    public async Task<ActionPayload> ExecuteStructuredAsync(ActionRequest r,CancellationToken token)
    {
        Current(token);if(!Actions.Contains(r.Action))throw new ArgumentException("未知媒体动作");
        if(r.Action is "media_status" or "media_open")Empty(r.Value);else if(r.Action=="media_file"){if(r.Value.Length>0){if(!Path.IsPathFullyQualified(r.Value)||!File.Exists(r.Value))throw new ArgumentException("媒体文件路径无效");}}else ValidateSession(r.Value);
        if(r.Action=="media_open"){await show(token);Current(token);return new("已打开媒体控制。",Evidence:"window-shown");}
        return await queue.Run("media",async(t,report)=>{
            Current(t);
            if(r.Action=="media_status"){var rows=preview?new[]{new MediaSession("potplayer:preview","PotPlayer","示例视频","","Unknown")}:await read(t);Current(t);state.Update(rows);return new ActionPayload(JsonSerializer.Serialize(rows),JsonSerializer.SerializeToElement(rows),"state-observed",PrivateData:true,DiagnosticMessage:"已查询媒体会话。");}
            if(preview)return new ActionPayload("示例模式，不控制播放器",Evidence:"unknown");
            if(r.Action=="media_file"){report("正在启动 PotPlayer…");await open(r.Value,t);Current(t);return new ActionPayload("已向系统提交 PotPlayer 启动请求，请选择媒体；不代表已开始播放。",Evidence:"accepted",PrivateData:true,DiagnosticMessage:"已提交播放器启动请求。");}
            report("正在控制播放器并确认状态…");var result=await control(r.Action[6..],r.Value,t);Current(t);
            return new ActionPayload(result.Message,JsonSerializer.SerializeToElement(result),result.Observed?"state-observed":"accepted",PrivateData:true,DiagnosticMessage:result.Observed?"已观察到媒体状态变化。":"媒体指令已发送，未确认播放状态。");
        },token,r.Action!="media_status");
    }
    public async Task<string> ExecuteAsync(ActionRequest r,CancellationToken t)=>(await ExecuteStructuredAsync(r,t)).Message;
}
