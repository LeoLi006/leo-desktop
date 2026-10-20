using System.Net.Http;
using System.Text.Json;
namespace LeoLauncher;

sealed record ScheduleSnapshot(FootballCache Cache,bool Stale,bool RefreshFailed=false,bool CacheSaved=true,string? Error=null);
sealed record TeamSnapshot(TeamData Data,bool MatchesFailed,bool TableFailed,bool Stale,bool CacheSaved=true,string? Error=null);
sealed record EventSnapshot(FootballMatch[] Matches,DateTimeOffset Updated,bool Stale,int FailedCount);

// Providers return data only. This host owns cache publication and cancellation generations.
sealed class FootballQueries:IDisposable
{
    readonly object gate=new();readonly Func<bool> enabled;readonly Func<string> configuration;readonly Func<DateTimeOffset> clock;
    readonly Func<FootballCache,CancellationToken,Task<FootballCache>> fetchSchedule;
    readonly Func<Club,TeamData,CancellationToken,Task<(TeamData Data,bool MatchesFailed,bool TableFailed)>> fetchTeam;
    readonly Func<FootballCache> loadSchedule;readonly Func<Club,TeamData> loadTeam;
    readonly Action<FootballCache> saveSchedule;readonly Action<Club,TeamData> saveTeam;
    readonly Func<string[],CancellationToken,Task<FootballMatch[]>> fetchEvents;
    readonly Dictionary<string,(FootballMatch Match,DateTimeOffset Updated)> events=new();
    readonly Dictionary<string,SemaphoreSlim> lanes=new();readonly Dictionary<string,long> revisions=new();
    readonly Dictionary<string,TeamSnapshot> teams=new();ScheduleSnapshot? schedule;
    CancellationTokenSource life=new();long epoch;bool disposed;string? scheduleConfig;
    public FootballQueries(Func<bool> enabled,Func<string> configuration,bool preview=false,
        Func<FootballCache,CancellationToken,Task<FootballCache>>? fetchSchedule=null,
        Func<Club,TeamData,CancellationToken,Task<(TeamData Data,bool MatchesFailed,bool TableFailed)>>? fetchTeam=null,
        Func<FootballCache>? loadSchedule=null,Func<Club,TeamData>? loadTeam=null,
        Action<FootballCache>? saveSchedule=null,Action<Club,TeamData>? saveTeam=null,Func<DateTimeOffset>? clock=null,Func<string[],CancellationToken,Task<FootballMatch[]>>? fetchEvents=null)
    {
        this.enabled=enabled;this.configuration=configuration;this.clock=clock??(()=>DateTimeOffset.UtcNow);
        this.fetchEvents=fetchEvents??MatchSyncService.FetchIds;
        this.fetchSchedule=fetchSchedule??FootballFeed.FetchAsync;this.fetchTeam=fetchTeam??TeamData.Fetch;
        this.loadSchedule=loadSchedule??(preview?()=>new():FootballFeed.LoadCache);this.loadTeam=loadTeam??TeamData.Load;
        this.saveSchedule=saveSchedule??(preview?_=>{}:data=>AtomicJson.Write(FootballFeed.CachePath,JsonSerializer.Serialize(data)));
        this.saveTeam=saveTeam??(preview?(_,_)=>{}:(club,data)=>AtomicJson.Write(TeamData.CachePath(club),JsonSerializer.Serialize(data)));
        Preview=preview;
    }
    public bool Preview{get;}
    bool Fresh(DateTimeOffset time)=>time>clock().AddMinutes(-10)&&time<=clock().AddMinutes(1);
    static T Copy<T>(T data)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(data))!;
    static string Key(Club c)=>c.Id+":"+c.League;
    ScheduleSnapshot ReadSchedule(){if(schedule==null){schedule=new(loadSchedule(),true);scheduleConfig=configuration();}return schedule with{Stale=scheduleConfig!=configuration()||schedule.RefreshFailed||schedule.Cache.FailedSources>0||schedule.Cache.SchemaVersion<2||!Fresh(schedule.Cache.Updated)};}
    TeamSnapshot ReadTeam(Club c){string key=Key(c);if(!teams.TryGetValue(key,out var data)){data=new(loadTeam(c),false,false,true);teams[key]=data;}return data with{Stale=data.MatchesFailed||data.TableFailed||!Fresh(data.Data.MatchesUpdated)||!Fresh(data.Data.TableUpdated)};}
    public FootballCache CachedSchedule(){lock(gate)return Copy(ReadSchedule().Cache);}
    public TeamData CachedTeam(Club club){lock(gate)return Copy(ReadTeam(club).Data);}
    public IEnumerable<(FootballMatch Match,DateTimeOffset Updated)> CachedMatches(IEnumerable<Club> clubs)
    {lock(gate){foreach(var club in clubs)ReadTeam(club);return ReadSchedule().Cache.Matches.Select(m=>(m,ReadSchedule().Cache.Updated)).Concat(teams.Values.SelectMany(s=>s.Data.Matches.Select(m=>(m,s.Data.MatchesUpdated)))).Concat(events.Values.Select(e=>(e.Match,e.Updated))).ToArray();}}
    public async Task<EventSnapshot> Events(string[] ids,CancellationToken token)
    {
        if(ids.Length>24||ids.Distinct().Count()!=ids.Length||ids.Any(id=>id.Length is <1 or >128||!id.All(char.IsAsciiDigit)))throw new ArgumentException("比赛编号无效，最多24场");
        var job=Begin("events",token);using var request=job.Token;token=request.Token;await job.Lane.WaitAsync(token).ConfigureAwait(false);
        try{
            lock(gate){Current(job.Epoch,job.Config,token);if(Preview)return new([],clock(),true,ids.Length);if(ids.All(id=>events.TryGetValue(id,out var e)&&Fresh(e.Updated)))return new(ids.Select(id=>events[id].Match).ToArray(),events.Values.Where(e=>ids.Contains(e.Match.Id)).Select(e=>e.Updated).DefaultIfEmpty(clock()).Min(),false,0);}
            FootballMatch[] rows;try{rows=await fetchEvents(ids,token).ConfigureAwait(false);if(rows.Any(m=>m==null||!ids.Contains(m.Id))||rows.Select(m=>m.Id).Distinct().Count()!=rows.Length)throw new FormatException("比赛详情编号不符");}
            catch(OperationCanceledException)when(token.IsCancellationRequested||!enabled()){throw;}
            catch(Exception e)when(QueryFailure(e)){rows=[];}
            lock(gate){Current(job.Epoch,job.Config,token);var now=clock();foreach(var row in rows)events[row.Id]=(row,now);while(events.Count>128)events.Remove(events.MinBy(p=>p.Value.Updated).Key);return new(rows,now,rows.Length!=ids.Length,ids.Length-rows.Length);}
        }finally{job.Lane.Release();}
    }
    (SemaphoreSlim Lane,long Revision,long Epoch,string Config,CancellationTokenSource Token) Begin(string key,CancellationToken token)
    {
        lock(gate){Require();if(!lanes.TryGetValue(key,out var lane))lanes[key]=lane=new(1,1);return(lane,revisions.GetValueOrDefault(key),epoch,configuration(),CancellationTokenSource.CreateLinkedTokenSource(token,life.Token));}
    }
    void Require(){if(disposed)throw new ObjectDisposedException(nameof(FootballQueries));if(!enabled())throw new OperationCanceledException("足球模块已停用");}
    void Current(long generation,string config,CancellationToken token){token.ThrowIfCancellationRequested();Require();if(epoch!=generation||configuration()!=config)throw new OperationCanceledException("关注配置已改变，请重新查询",token);}
    public async Task<ScheduleSnapshot> Schedule(bool force,CancellationToken token)
    {
        var job=Begin("schedule",token);using var request=job.Token;token=request.Token;await job.Lane.WaitAsync(token).ConfigureAwait(false);
        try{
            ScheduleSnapshot old;lock(gate){Current(job.Epoch,job.Config,token);old=ReadSchedule();if(Preview||!force&&!old.Stale||revisions.GetValueOrDefault("schedule")!=job.Revision)return Copy(old);}
            ScheduleSnapshot result;
            try{var data=await fetchSchedule(Copy(old.Cache),token).ConfigureAwait(false);Validate(data);result=new(data,data.FailedSources>0,data.FailedSources>0);}
            catch(OperationCanceledException)when(token.IsCancellationRequested||!enabled()){throw;}
            catch(Exception e)when(QueryFailure(e)){result=old with{Stale=true,RefreshFailed=true,Error="赛程源暂时无法连接，保留已有缓存"};}
            lock(gate){Current(job.Epoch,job.Config,token);if(!result.RefreshFailed||result.Cache!=old.Cache){try{saveSchedule(result.Cache);}catch(Exception e)when(QueryFailure(e)){result=result with{CacheSaved=false,Error="本次已取得赛程，但缓存未能保存"};}}schedule=result;if(!result.RefreshFailed)scheduleConfig=job.Config;revisions["schedule"]=revisions.GetValueOrDefault("schedule")+1;return Copy(result);}
        }finally{job.Lane.Release();}
    }
    public async Task<TeamSnapshot> Team(Club club,bool force,CancellationToken token)
    {
        string key=Key(club);var job=Begin(key,token);using var request=job.Token;token=request.Token;await job.Lane.WaitAsync(token).ConfigureAwait(false);
        try{
            TeamSnapshot old;lock(gate){Current(job.Epoch,job.Config,token);old=ReadTeam(club);if(Preview||!force&&!old.Stale||revisions.GetValueOrDefault(key)!=job.Revision)return Copy(old);}
            TeamSnapshot result;
            try{var response=await fetchTeam(club,Copy(old.Data),token).ConfigureAwait(false);Validate(response.Data);var data=Copy(response.Data);if(response.MatchesFailed){data.Matches=old.Data.Matches;data.MatchesUpdated=old.Data.MatchesUpdated;}if(response.TableFailed){data.Rows=old.Data.Rows;data.TableUpdated=old.Data.TableUpdated;data.Season=old.Data.Season;}result=new(data,response.MatchesFailed,response.TableFailed,response.MatchesFailed||response.TableFailed);}
            catch(OperationCanceledException)when(token.IsCancellationRequested||!enabled()){throw;}
            catch(Exception e)when(QueryFailure(e)){result=old with{Stale=true,MatchesFailed=true,TableFailed=true,Error="球队数据暂时无法更新，保留已有缓存"};}
            lock(gate){Current(job.Epoch,job.Config,token);if(!result.MatchesFailed||!result.TableFailed)try{saveTeam(club,result.Data);}catch(Exception e)when(QueryFailure(e)){result=result with{CacheSaved=false,Error="本次已取得球队数据，但缓存未能保存"};}teams[key]=result;revisions[key]=revisions.GetValueOrDefault(key)+1;return Copy(result);}
        }finally{job.Lane.Release();}
    }
    internal static bool QueryFailure(Exception e)=>e is TimeoutException or OperationCanceledException or HttpRequestException or IOException or UnauthorizedAccessException or JsonException or FormatException or InvalidOperationException or ArgumentException;
    internal static void Validate(FootballCache data){if(data.Matches==null||data.Matches.Count>20000||data.FailedSources<0||data.Matches.Any(m=>m==null||string.IsNullOrWhiteSpace(m.Id)))throw new FormatException("赛程缓存无效");}
    internal static void Validate(TeamData data){if(data.Matches==null||data.Rows==null||data.Matches.Count>20000||data.Rows.Count>2000||data.Matches.Any(m=>m==null||string.IsNullOrWhiteSpace(m.Id))||data.Rows.Any(r=>r==null))throw new FormatException("球队缓存无效");}
    public void Suspend(){lock(gate){if(disposed)return;epoch++;var old=life;life=new();old.Cancel();old.Dispose();}}
    public void Dispose(){lock(gate){if(disposed)return;disposed=true;life.Cancel();life.Dispose();}}
}
