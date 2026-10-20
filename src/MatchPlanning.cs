using System.Text.Json;
using System.Text.Json.Serialization;
namespace LeoLauncher;

sealed record MatchReference(string Provider,string EventId,string DisplayTimeZone,int ReminderMinutes=10);
sealed record MatchEnvelope(MatchReference Reference,FootballMatch Match,DateTimeOffset? ObservedAt,string SourceUrl);

sealed class MatchPlanningHost(Func<IEnumerable<(FootballMatch Match,DateTimeOffset Updated)>> cached)
{
    readonly Dictionary<string,MatchEnvelope> selected=new();
    public MatchEnvelope Remember(FootballMatch match,DateTimeOffset? observed)
    {
        var envelope=new MatchEnvelope(new("espn",match.Id,TimeZoneInfo.Local.Id),match,observed is {} time&&time!=default?time:null,"https://www.espn.com/soccer/match/_/gameId/"+Uri.EscapeDataString(match.Id));
        if(!selected.TryGetValue(match.Id,out var old)||old.ObservedAt==null||envelope.ObservedAt>=old.ObservedAt)selected[match.Id]=envelope;
        while(selected.Count>512)selected.Remove(selected.Keys.First());return envelope;
    }
    public MatchEnvelope Resolve(MatchReference reference)
    {
        Validate(reference);var candidates=cached().Where(x=>x.Match.Id==reference.EventId).Select(x=>new MatchEnvelope(reference,x.Match,x.Updated==default?null:x.Updated,"https://www.espn.com/soccer/match/_/gameId/"+Uri.EscapeDataString(reference.EventId))).ToList();
        if(selected.TryGetValue(reference.EventId,out var clicked))candidates.Add(clicked with{Reference=reference});
        return candidates.OrderByDescending(x=>x.ObservedAt).FirstOrDefault()??throw new ArgumentException("未找到已查询的比赛，请先查询或刷新赛程");
    }
    public static void Validate(MatchReference r){if(r.Provider!="espn"||string.IsNullOrWhiteSpace(r.EventId)||r.EventId.Length>128||r.EventId.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'&&c!='_')||r.DisplayTimeZone!=TimeZoneInfo.Local.Id||r.ReminderMinutes!=10)throw new ArgumentException("比赛来源、编号、显示时区或提醒参数无效");}
}

sealed class MatchPlanningCapability(Func<bool> enabled,MatchPlanningHost host,TodoStore tasks,ModuleWindowService ui):IStructuredLeoModule,IActionRequestAdapter,IBrainContributor,IModuleContractSource
{
    public ModuleContract DescribeModule()=>ModuleContracts.Match(this);
    public DependencyState[] ReadDependencies()=>[new("match-source","unknown"),new("todo-store",tasks.Error==null?"ready":"degraded")];

    public IReadOnlyList<BrainAction> BrainActions=>[
        new("match_lookup","查询比赛安排参数","match-id",true,"先查赛程，使用实际返回的Id",ResultProperty:"Reference",ResultKind:"match-reference"),
        new("match_add","比赛加入清单并提醒","match-reference",Hint:"使用已查询的比赛Id，或引用查询步骤的Reference；不自行编造时间")];
    public string Id=>"match-planning";public string Name=>"比赛加入清单";public string Version=>"1";public bool Enabled=>enabled();public bool ConfigurationIsCurrent=>Enabled;
    public IReadOnlyList<string> Actions=>["match_lookup","match_add"];
    public CapabilityDefinition Describe(string action)=>new(action,Id,"in-process","match-reference/v1",action=="match_lookup"?"read":"write","coalesce-inflight",15000);
    public string FailureCode(Exception error)=>error is ArgumentException or JsonException?"invalid_request":error is IOException or UnauthorizedAccessException?"storage_failed":"match_plan_failed";
    public ActionRequest Adapt(StructuredActionRequest request){if(request.Protocol!="leo-action/1")throw new ArgumentException("不支持的请求协议");return new(request.ActionId,request.Parameters.GetRawText(),request.Source,request.RequestId,request.TraceId,request.Deadline);}
    public async Task<string> ExecuteAsync(ActionRequest request,CancellationToken token)=>(await ExecuteStructuredAsync(request,token)).Message;
    public async Task<ActionPayload> ExecuteStructuredAsync(ActionRequest request,CancellationToken token)
    {
        var reference=request.Value.StartsWith('{')?JsonSerializer.Deserialize<MatchReference>(request.Value,new JsonSerializerOptions{UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow})??throw new ArgumentException("比赛引用为空"):new("espn",request.Value,TimeZoneInfo.Local.Id);
        ActionPayload? result=null;
        await ui.ShowAsync(()=>{
            token.ThrowIfCancellationRequested();if(!Enabled)throw new OperationCanceledException(token);var envelope=host.Resolve(reference);
            if(request.Action=="match_lookup"){result=new("已取得比赛来源与安排参数。",JsonSerializer.SerializeToElement(envelope),"cached",true);return;}
            var plan=tasks.Client.From("match-bridge").AddMatch(envelope);
            result=new(JsonSerializer.Serialize(new{success=true,plan.Added,plan.Task}),JsonSerializer.SerializeToElement(new{plan.Added,plan.Task,reference=envelope.Reference,envelope.ObservedAt,envelope.SourceUrl,kickoffUtc=envelope.Match.Kickoff.ToUniversalTime()}),"committed",true,"比赛清单操作已完成");
        },token);return result!;
    }
}

sealed partial class TodoStore
{
    internal Func<FootballMatch,DateTimeOffset?,Task<MatchPlan>>? MatchImporter;
    public Task<MatchPlan> ImportMatchAsync(FootballMatch match,DateTimeOffset? observed=null)=>MatchImporter?.Invoke(match,observed)??Task.FromResult(Client.From("football-ui").AddMatch(new(new("espn",match.Id,TimeZoneInfo.Local.Id),match,observed,"https://www.espn.com/soccer/match/_/gameId/"+Uri.EscapeDataString(match.Id))));
    public MatchPlan AddMatch(MatchEnvelope input,DateTimeOffset now,CancellationToken token=default)
    {
        MatchPlanningHost.Validate(input.Reference);if(input.Reference.EventId!=input.Match.Id)throw new ArgumentException("比赛编号不一致");token.ThrowIfCancellationRequested();
        return AddMatchCore(input.Match,now,input.ObservedAt,input.Reference.DisplayTimeZone);
    }
}

sealed partial class PetForm
{
    MatchPlanningHost? matchPlanning;
    void RegisterMatchPlanning()
    {
        matchPlanning=new(()=>footballQueries.CachedMatches(previewMode?[]:FootballFollowing.Current.Clubs));
        commands.Registry.Register(new MatchPlanningCapability(()=>prefs.FootballModule,matchPlanning,todoStore,new ModuleWindowService(this)));
        todoStore.MatchImporter=async(match,observed)=>{
            var envelope=matchPlanning.Remember(match,observed);var lookup=await commands.ExecuteDetailedAsync(new("match_lookup",JsonSerializer.Serialize(envelope.Reference),"football-ui"));lookup.ToLegacy(default);
            var reference=lookup.Data!.Value.GetProperty("Reference");var result=await commands.ExecuteDetailedAsync(new("match_add",reference.GetRawText(),"football-ui"));result.ToLegacy(default);
            var data=result.Data!.Value;return new(data.GetProperty("Task").Deserialize<DailyTask>()!,data.GetProperty("Added").GetBoolean());
        };
    }
}
