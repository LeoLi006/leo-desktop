using System.Text.Json;
using System.Text.RegularExpressions;
namespace LeoLauncher;

// Host-internal metadata v1, not a replacement for LeoModule/1 or a package format.
interface IContractInputBoundary{bool AcceptsStructuredAction(string action);}
interface IModuleContractSource:ILeoModule
{
    ModuleContract DescribeModule();
    DependencyState[] ReadDependencies();
}
sealed record DependencyContract(string Id,string Kind,string Requirement,string UnavailableBehavior,string[] Actions);
sealed record DependencyState(string Id,string State);
sealed record ParameterContract(string Protocol,string Adapter,string Encoding,string LegacyProfile,string Description);
sealed record ResultContract(string Envelope,string DataProfile,string Description,bool MayContainPrivateData,string[] Evidence);
sealed record ContractAction(string Id,CapabilityDefinition Policy,ParameterContract Input,ResultContract Output,int MaximumInputLength,string Cancellation);
sealed record ModuleContract(string Format,string Id,string Name,string Version,string Kind,string Runtime,string Enablement,string Configuration,string DataOwner,DependencyContract[] Dependencies,ContractAction[] Actions);
sealed record ModuleContractSnapshot(ModuleContract Contract,bool Enabled,bool ConfigurationIsCurrent,DependencyState[] Dependencies);

static partial class ModuleContracts
{
    public const string Format="leo-host-contract/1";
    static bool Text(string? value,int max=240)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=max&&!value.Any(char.IsControl);
    static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool valid){if(!valid)throw new CapabilityException("invalid_contract","模块契约缺失、重复或与当前执行接口不一致");}
    public static ModuleContract Validate(IModuleContractSource source)
    {
        try{return ValidateCore(source);}catch(CapabilityException){throw;}catch(Exception e)when(FeatureFaults.Recoverable(e)){throw new CapabilityException("invalid_contract","无法读取有效的模块契约");}
    }
    static ModuleContract ValidateCore(IModuleContractSource source)
    {
        var d=source.DescribeModule();Require(d!=null);Require(d.Format==Format&&d.Id==source.Id&&Text(d.Id,128)&&Text(d.Name)&&d.Version==source.Version&&Regex.IsMatch(d.Version??"",@"^\d+(?:\.\d+){0,3}$"));
        Require(Text(d.Kind,60)&&d.Runtime is "in-process" or "application-adapter" or "stdio"&&d.Enablement is "optional" or "host-core"&&d.Configuration=="preserve-existing"&&Text(d.DataOwner));
        Require(source is IStructuredLeoModule&&d.Actions!=null&&d.Actions.Length is >0 and <=512&&d.Actions.All(a=>a!=null)&&d.Actions.Select(a=>a.Id).Distinct().Count()==d.Actions.Length&&d.Actions.Select(a=>a.Id).Order().SequenceEqual(source.Actions.Order()));
        var structured=(IStructuredLeoModule)source;
        foreach(var action in d.Actions){
            var p=action.Policy;Require(Text(action.Id,128)&&p!=null&&p==structured.Describe(action.Id)&&p.Id==action.Id&&p.ModuleId==d.Id&&p.Runtime==d.Runtime&&p.Effect is "read" or "write" or "window" or "external" or "session"&&p.Concurrency is "coalesce-inflight" or "serialized-store"&&p.TimeoutMs is >=1 and <=120000&&!p.Retry);
            var input=action.Input;var route=InputRoute(source,action.Id);Require(input!=null&&input.Protocol==route.Protocol&&input.Adapter==route.Adapter&&(route.Adapter=="host-private"?input.Encoding=="private-handle":route.Adapter=="legacy-only"?input.Encoding=="legacy-value":input.Encoding is "object" or "value-wrapper")&&input.LegacyProfile==p.Input&&Text(input.Description,1200));
            var output=action.Output;Require(output!=null&&output.Envelope=="ActionResult"&&Text(output.DataProfile)&&Text(output.Description,1200)&&output.Evidence!=null&&output.Evidence.Length>0&&output.Evidence.Distinct().Count()==output.Evidence.Length&&output.Evidence.All(e=>e is "unknown" or "accepted" or "snapshot" or "committed" or "window-shown" or "state-observed" or "completed" or "released" or "cached" or "steps-completed"));
            Require(action.MaximumInputLength==structured.MaximumInputLength&&action.Cancellation==(source is ICommittedActionModule?"commit-aware":"host-cooperative"));
        }
        Require(d.Dependencies!=null&&d.Dependencies.All(x=>x!=null)&&d.Dependencies.Select(x=>x.Id).Distinct().Count()==d.Dependencies.Length);
        foreach(var dependency in d.Dependencies)Require(Text(dependency.Id,80)&&dependency.Kind is "host-service" or "local-file" or "local-directory" or "network" or "local-application"&&dependency.Requirement is "required" or "optional" or "repairable"&&Text(dependency.UnavailableBehavior)&&dependency.Actions!=null&&dependency.Actions.Length>0&&dependency.Actions.Distinct().Count()==dependency.Actions.Length&&dependency.Actions.All(a=>source.Actions.Contains(a)));
        return d;
    }
    public static ModuleContractSnapshot Snapshot(IModuleContractSource source)
    {
        try{return SnapshotCore(source);}catch(CapabilityException){throw;}catch(Exception e)when(FeatureFaults.Recoverable(e)){throw new CapabilityException("invalid_contract","无法读取有效的模块依赖状态");}
    }
    static ModuleContractSnapshot SnapshotCore(IModuleContractSource source)
    {
        var d=Validate(source);var states=source.ReadDependencies();Require(states!=null&&states.All(s=>s!=null)&&states.Select(s=>s.Id).Distinct().Count()==states.Length&&states.Select(s=>s.Id).Order().SequenceEqual(d.Dependencies.Select(s=>s.Id).Order())&&states.All(s=>s.State is "ready" or "missing" or "unknown" or "degraded"));
        return new(d,source.Enabled,((IStructuredLeoModule)source).ConfigurationIsCurrent,states);
    }
    static (string Protocol,string Adapter) InputRoute(ILeoModule module,string action)=>module is IContractInputBoundary boundary&&!boundary.AcceptsStructuredAction(action)?("ActionRequest","host-private"):module is IActionRequestAdapter?("leo-action/1","module-adapter"):module is LocalApplicationModule?("leo-action/1","application-adapter"):("ActionRequest","legacy-only");
    public static ModuleContract Build(IStructuredLeoModule module,string kind,string name,string enablement,string dataOwner,DependencyContract[] dependencies,Func<string,(string Encoding,string Description,string Profile,string Result,bool Private,string[] Evidence)> describe)
    {
        var actions=module.Actions.Select(id=>{var p=module.Describe(id);var doc=describe(id);var route=InputRoute(module,id);return new ContractAction(id,p,new(route.Protocol,route.Adapter,doc.Encoding,p.Input,doc.Description),new("ActionResult",doc.Profile,doc.Result,doc.Private,doc.Evidence),module.MaximumInputLength,module is ICommittedActionModule?"commit-aware":"host-cooperative");}).ToArray();
        return new(Format,module.Id,name,module.Version,kind,actions[0].Policy.Runtime,enablement,"preserve-existing",dataOwner,dependencies,actions);
    }
    public static ModuleContract Application(LocalApplicationModule module)=>Build(module,"application","本机应用启动","optional","host.application-settings",[
        new("application-file","local-file","repairable","existing-path-prepare",[module.Id]),new("working-directory","local-directory","repairable","existing-configuration-check",[module.Id])],_=>
        ("object","parameters包含唯一applicationId（32位应用标识），与local动作一致；旧local_app_open的value仍为应用标识，local动作沿用旧参数归一。","application-start","Data为applicationId、accepted；系统接收启动请求不证明窗口就绪。",false,["accepted","unknown"]));
    public static ModuleContract Todos(TodoCapability module)=>Build(module,"todos","清单与提醒","host-core","host.todo-store",[
        new("todo-store","host-service","required","existing-store-validation",module.Actions.ToArray())],a=>
        (TodoCapability.Fields(a).Length>0?"object":"value-wrapper",TodoInput(a),"todo-payload",a=="todos_open"?"打开现有清单视图，Data可为空。":"沿用现有事项、列表、布尔、数字或空值Data；提交回执保留已写入结果，未提交失败不写成功。",true,a=="todos_open"?["window-shown"]:["snapshot","committed"]));
    static string TodoInput(string a)
    {
        var fields=TodoCapability.Fields(a);if(fields.Length>0)return "parameters直接使用现有字段："+string.Join(", ",fields)+"；必填、日期、时间及真实事项编号继续由TodoCapability适配与业务校验，不由描述替代。";
        return a switch{
            "todos_open"=>"parameters为可选value文字：空或week。",
            "todo_list"=>"parameters.value为日期yyyy-MM-dd、week:yyyy-MM-dd或空，沿用当前日期默认值。",
            "todo_snapshot" or "todo_undo" or "todo_backup" or "todo_quiet"=>"parameters为可选value文字；旧value必须为空。",
            "todo_restore"=>"parameters.value是序列化DailyTask的JSON文字；继续使用原恢复校验。",
            "todo_postpone_ids"=>"parameters.value是事项编号数组的JSON文字；不是直接数组参数。",
            "todo_match_add"=>"parameters.value是MatchEnvelope的JSON文字，保留来源、比赛编号、时区与提醒参数。",
            "todo_ack"=>"parameters.value是ReminderTicket数组的JSON文字，保留提醒时间与稍后提醒票据。",
            "todo_quiet_save"=>"parameters.value是QuietHours的JSON文字，沿用原免打扰设置。",
            "todo_natural"=>"parameters.value是NaturalTodoDraft的JSON文字；添加前沿用现有确认界面。",
            "todo_due"=>"parameters.value为带时区的日期时间文字。",
            "todo_ensure"=>"parameters.value为yyyy-MM-dd日期文字。",
            "todo_stop_repeat"=>"parameters直接使用series_id和after日期，沿用原重复规则校验。",
            "todo_restore_backup"=>"parameters.value为用户选择的清单备份引用，沿用既有备份目录限制。",
            "todo_postpone"=>"parameters.value为真实事项编号或day:yyyy-MM-dd，沿用原顺延选择规则。",
            "todo_complete_title"=>"parameters.value为当天唯一匹配的事项标题，不能猜测多个匹配。",
            _=>"parameters.value为现有真实事项编号，沿用模块对事项和重复实例的校验。"};
    }
    public static ModuleContract Football(FootballCapability module)=>Build(module,"football","足球查询","optional","host.football-cache",[
        new("query-service","host-service","required","existing-query-validation",module.Actions.ToArray()),new("football-source","network","optional","cached-fallback",module.Actions.Where(a=>a!="football_open").ToArray())],a=>
        ("object",a switch{
            "football_schedule"=>"parameters为唯一force布尔字段。","football_team"=>"parameters为id、league、force；球队与赛事必须来自当前目录。","football_events"=>"parameters为ids数组，最多24个真实比赛编号。","football_facts"=>"parameters为可选query文字（球队名或比赛问题），保留旧view:查询形状。",_=>"parameters为空对象；窗口动作不接受额外参数。"},a=="football_open"?"window-result":"football-snapshot",a=="football_open"?"Data可为空，返回窗口已显示证据。":"Data保持现有缓存、球队表格或比赛快照，包含更新时间、过期/失败状态；缺少来源时不虚构比赛。",true,a=="football_open"?["window-shown"]:["snapshot"]));
    public static ModuleContract Media(MediaCapability module)=>Build(module,"media","媒体控制","optional","host.media-session-state",[
        new("media-provider","host-service","optional","existing-provider-verification",module.Actions.Where(a=>a is not("media_open" or "media_file")).ToArray()),new("potplayer","local-application","optional","existing-path-resolution",["media_file"])],a=>
        ("object",a is "media_open" or "media_status"?"parameters为空对象；不接受额外参数。":a=="media_file"?"parameters为可选path文字；非空时为已存在的绝对文件路径。":"parameters为可选session文字；使用已查询的精确会话编号，旧空值自动选择逻辑保留。",a=="media_status"?"media-sessions":"media-receipt",a=="media_status"?"Data为实际媒体会话数组。":"Data沿用媒体控制回执或为空；accepted不等于实际播放，状态核验后才报告state-observed。",true,a=="media_open"?["window-shown"]:["unknown","accepted","state-observed"]));
}
