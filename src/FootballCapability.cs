using System.Text.Json;
namespace LeoLauncher;
sealed class FootballCapability(Func<bool> enabled,FootballQueries queries,Func<ActionRequest,CancellationToken,Task<ActionResult>> execute,Func<CancellationToken,Task> show,Func<string[]> following):IStructuredLeoModule,IActionRequestAdapter,IBrainContributor,IRestartableQueryModule
{
    public string Id=>"football-data";public string Name=>"足球查询";public string Version=>"1";public bool Enabled=>enabled();public bool ConfigurationIsCurrent=>Enabled;
    public IReadOnlyList<string> Actions=>["football_facts","football_open","football_schedule","football_team","football_events"];
    public IReadOnlyList<BrainAction> BrainActions=>[new("football_facts","查询足球赛程与积分榜","text",true,"球队名、今晚比赛；返回来源、缓存时间和实际比赛编号",Check:CheckText),new("football_open","打开足球日程","none",Check:v=>{if(v.Length!=0)throw new ArgumentException("足球窗口不接受参数");})];
    static void CheckText(string v){if(v.Length>200||v.Any(char.IsControl))throw new ArgumentException("足球查询文字过长或无效");}
    public CapabilityDefinition Describe(string a)=>new(a,Id,"in-process","football-query/v1",a=="football_open"?"window":"read","coalesce-inflight",a=="football_open"?15000:90000);
    public string FailureCode(Exception e)=>e is ArgumentException or JsonException or KeyNotFoundException or FormatException or InvalidOperationException?"invalid_request":e is ObjectDisposedException?"host_closed":"football_query_failed";
    public ActionRequest Adapt(StructuredActionRequest r)
    {
        if(r.Protocol!="leo-action/1"||!Actions.Contains(r.ActionId)||r.Parameters.ValueKind!=JsonValueKind.Object)throw new ArgumentException("足球请求协议无效");
        if(r.ActionId is "football_team" or "football_schedule" or "football_events"){Validate(r.ActionId,r.Parameters);return new(r.ActionId,r.Parameters.GetRawText(),r.Source,r.RequestId,r.TraceId,r.Deadline);}
        var fields=r.Parameters.EnumerateObject().ToArray();if(fields.Length>1||fields.Any(x=>x.Name!="query"||x.Value.ValueKind!=JsonValueKind.String)||r.ActionId=="football_open"&&fields.Length>0)throw new ArgumentException("足球查询参数无效");return new(r.ActionId,fields.Length==0?"":fields[0].Value.GetString()!,r.Source,r.RequestId,r.TraceId,r.Deadline);
    }
    static void Validate(string action,JsonElement p)
    {
        if(action=="football_events"){if(p.ValueKind!=JsonValueKind.Object||p.EnumerateObject().Count()!=1||!p.TryGetProperty("ids",out var ids)||ids.ValueKind!=JsonValueKind.Array)throw new ArgumentException("需要比赛编号列表");return;}
        string[] names=action=="football_team"?["id","league","force"]:["force"];if(p.ValueKind!=JsonValueKind.Object||p.EnumerateObject().Count()!=names.Length||p.EnumerateObject().Select(x=>x.Name).Distinct().Count()!=names.Length||p.EnumerateObject().Any(x=>!names.Contains(x.Name)))throw new ArgumentException("足球参数缺失、多余或重复");p.GetProperty("force").GetBoolean();
    }
    internal static Club Target(string id,string league)
    {
        var club=ClubCatalog.Find(id)??throw new ArgumentException("球队不在目录中");
        if(club.League!=league&&(!club.IsNational||!NationalCompetitions.For(club).Any(c=>c.Id==league)))throw new ArgumentException("球队赛事不在可选目录中");return club with{League=league};
    }
    public async Task<string> ExecuteAsync(ActionRequest r,CancellationToken t)=>(await ExecuteStructuredAsync(r,t)).Message;
    public async Task<ActionPayload> ExecuteStructuredAsync(ActionRequest r,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(!Enabled)throw new OperationCanceledException(token);
        if(r.Action=="football_open"){if(r.Value.Length!=0)throw new ArgumentException("足球窗口不接受参数");await show(token);return new("已打开足球日程、球队详情与观赛网址。",Evidence:"window-shown");}
        object data;
        if(r.Action=="football_facts"){
            CheckText(r.Value);string team=QuickRouter.Team(r.Value);bool view=r.Value.StartsWith("view:",StringComparison.Ordinal);
            if(team.Length>0){var club=Club.For(team);var result=await execute(new("football_team",FootballClient.TeamValue(club,false),r.Source),token);result.ToLegacy(token);var s=result.Data!.Value.Deserialize<TeamSnapshot>()!;data=view?new{data=s.Data,stale=s.Stale,s.CacheSaved,s.Error}:(object)new{source="ESPN",now=DateTimeOffset.Now,team,stale=s.Stale,s.CacheSaved,s.Error,s.Data.MatchesUpdated,s.Data.TableUpdated,matches=s.Data.Matches.OrderByDescending(m=>m.Kickoff).Take(25),table=s.Data.Rows};}
            else{var result=await execute(new("football_schedule",FootballClient.ScheduleValue(false),r.Source),token);result.ToLegacy(token);var s=result.Data!.Value.Deserialize<ScheduleSnapshot>()!;data=view?new{cache=s.Cache,stale=s.Stale,s.CacheSaved,s.Error}:(object)new{source="ESPN",s.Cache.Updated,s.Cache.FailedSources,stale=s.Stale,s.CacheSaved,s.Error,matches=s.Cache.Matches.Where(m=>FootballFeed.IsRecent(m,DateTimeOffset.UtcNow)&&(FootballFeed.FollowTags(m,following()).Length>0||FootballFeed.IsFocus(m))).OrderBy(m=>m.Kickoff).Take(30)};}
        }else{
            using var d=JsonDocument.Parse(r.Value);var p=d.RootElement;Validate(r.Action,p);if(r.Action=="football_events"){data=await queries.Events(p.GetProperty("ids").Deserialize<string[]>()!,token);return new(JsonSerializer.Serialize(data),JsonSerializer.SerializeToElement(data),"snapshot",true,"已查询足球比赛详情。");}bool force=p.GetProperty("force").GetBoolean();
            data=r.Action=="football_schedule"?await queries.Schedule(force,token):await queries.Team(Target(p.GetProperty("id").GetString()!,p.GetProperty("league").GetString()!),force,token);
        }
        token.ThrowIfCancellationRequested();return new(JsonSerializer.Serialize(data),JsonSerializer.SerializeToElement(data),"snapshot",true,"已查询足球公开数据。");
    }
}

sealed class FootballClient(ActionDispatcher bus,FootballQueries queries,bool owns=false):IDisposable
{
    public static string ScheduleValue(bool force)=>JsonSerializer.Serialize(new{force});
    public static string TeamValue(Club club,bool force)=>JsonSerializer.Serialize(new{id=club.Id,league=club.League,force});
    public FootballCache CachedSchedule()=>queries.CachedSchedule();
    public TeamData CachedTeam(Club club)=>queries.CachedTeam(club);
    async Task<T> Query<T>(string action,string value,CancellationToken token){var r=await bus.ExecuteDetailedAsync(new(action,value,"football-ui"),token);r.ToLegacy(token);return r.Data!.Value.Deserialize<T>()!;}
    public Task<ScheduleSnapshot> Schedule(bool force,CancellationToken token)=>Query<ScheduleSnapshot>("football_schedule",ScheduleValue(force),token);
    public Task<EventSnapshot> Events(string[] ids,CancellationToken token)=>Query<EventSnapshot>("football_events",JsonSerializer.Serialize(new{ids=ids.Distinct().Order().ToArray()}),token);
    public Task<TeamSnapshot> Team(Club club,bool force,CancellationToken token)=>Query<TeamSnapshot>("football_team",TeamValue(club,force),token);
    public async Task<string> Facts(string value,CancellationToken token){var r=await bus.ExecuteDetailedAsync(new("football_facts",value,"chat"),token);return r.ToLegacy(token);}
    public void Dispose(){if(owns){bus.Dispose();queries.Dispose();}}
    public static FootballClient Standalone(bool preview,FootballFollowing? following=null)
    {
        var prefs=new Preferences();var favorites=following??(preview?new FootballFollowing(null):FootballFollowing.Current);var service=new FootballQueries(()=>prefs.FootballModule,()=>string.Join(',',favorites.Ids),preview);
        var bus=new ActionDispatcher(prefs,(_,_,_)=>throw new InvalidOperationException("足球不可绕回旧入口"),new ExtensionStore(Path.Combine(Path.GetTempPath(),"leo-football-noextensions")));
        bus.Registry.Register(new FootballCapability(()=>prefs.FootballModule,service,bus.ExecuteDetailedAsync,_=>Task.CompletedTask,()=>favorites.Names));return new(bus,service,true);
    }
}

sealed partial class PetForm
{
    FootballQueries footballQueries=null!;FootballClient footballClient=null!;
    void RegisterFootballCapability()
    {
        footballQueries=new(()=>prefs.FootballModule,()=>previewMode?"preview":string.Join(',',FootballFollowing.Current.Ids),previewMode);
        footballClient=new(commands,footballQueries);var ui=new ModuleWindowService(this);
        commands.Registry.Register(new FootballCapability(()=>prefs.FootballModule,footballQueries,commands.ExecuteDetailedAsync,t=>ui.ShowAsync(()=>launcher.OpenFootballFromAssistant(),t),()=>previewMode?new FootballFollowing(null).Names:FootballForm.FollowingNames));
    }
}
