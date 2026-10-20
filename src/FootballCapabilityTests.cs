using System.Net.Http;
using System.Text.Json;
namespace LeoLauncher;
static class FootballCapabilityTests
{
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};
        ui.Shown+=async(_,_)=>{try{var checks=await Check(Path.GetDirectoryName(report)!,ui);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(x=>x),checks,real_api_requests=0,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}finally{AtomicJson.BeforeReplace=null;ui.Close();}};Application.Run(ui);
    }
    static ToolCall Tool(string name,object value)=>new(Guid.NewGuid().ToString("N"),name,JsonSerializer.Serialize(value));
    static async Task<Dictionary<string,bool>> Check(string root,Form ui)
    {
        root=Path.Combine(root,"fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var checks=new Dictionary<string,bool>();var now=DateTimeOffset.UtcNow;var club=Club.For("巴塞罗那");var match=new FootballMatch("123456",now.AddHours(4),"西甲","83","Barcelona","86","Real Madrid","pre","Scheduled","VS",true);
        FootballCache Cache(string id="123456",bool fresh=false)=>new(){Updated=fresh?now:now.AddHours(-1),SchemaVersion=2,Matches=[match with{Id=id}]};
        TeamData Team(bool fresh=false)=>new(){MatchesUpdated=fresh?now:now.AddHours(-1),TableUpdated=fresh?now:now.AddHours(-1),Season="fixture",Matches=[match],Rows=[new("fixture",club.Id,club.Name,"1","1","1","0","0","1:0","1","3")]};
        var prefs=new Preferences();string config="initial";int schedules=0,teamCalls=0,scheduleWrites=0,teamWrites=0,windows=0,eventCalls=0;bool failSchedule=false,failTable=false,failCache=false;var held=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);CancellationToken providerToken=default;
        using var queries=new FootballQueries(()=>prefs.FootballModule,()=>config,
            fetchSchedule:async(old,t)=>{schedules++;providerToken=t;if(schedules==1)await held.Task.WaitAsync(t);if(failSchedule)throw new HttpRequestException("fixture");return Cache(fresh:true);},
            fetchTeam:(c,old,t)=>{teamCalls++;return Task.FromResult((Team(true),false,failTable));},loadSchedule:()=>Cache("old"),loadTeam:_=>Team(),
            saveSchedule:data=>{if(failCache)throw new IOException("fixture");scheduleWrites++;},saveTeam:(_,_)=>teamWrites++,clock:()=>now,fetchEvents:(ids,t)=>{eventCalls++;return Task.FromResult(ids.Where(id=>id!="999999").Select(id=>match with{Id=id}).ToArray());});
        using var bus=new ActionDispatcher(prefs,(_,_,_)=>throw new Exception("legacy football bypass"),new ExtensionStore(Path.Combine(root,"modules")));
        var module=new FootballCapability(()=>prefs.FootballModule,queries,bus.ExecuteDetailedAsync,_=>{windows++;return Task.CompletedTask;},()=>[club.Name]);bus.Registry.Register(module);var client=new FootballClient(bus,queries);
        Task<ActionResult> Run(string action,string value="",CancellationToken token=default)=>bus.ExecuteDetailedAsync(new(action,value,"fixture"),token);
        using(var cancel=new CancellationTokenSource()){
            var first=Run("football_schedule",FootballClient.ScheduleValue(false),cancel.Token);var second=Run("football_facts","view:今晚比赛");
            checks["window_and_chat_share_inflight_provider"]=schedules==1;cancel.Cancel();var cancelled=await first;checks["one_subscriber_can_cancel_without_cancelling_others"]=cancelled.Status=="cancelled"&&!providerToken.IsCancellationRequested;
            held.TrySetResult();var completed=await second;checks["remaining_subscriber_receives_actual_data"]=completed.Status=="succeeded"&&completed.ModuleId=="football-data"&&completed.Data!.Value.GetProperty("cache").GetProperty("Matches")[0].GetProperty("Id").GetString()==match.Id;
        }
        var warm=await client.Schedule(false,default);checks["fresh_cache_avoids_network"]=schedules==1&&!warm.Stale&&warm.Cache.Updated==now;
        warm.Cache.Matches.Clear();checks["caller_cannot_mutate_shared_cache"]=queries.CachedSchedule().Matches.Count==1;
        await client.Schedule(true,default);checks["explicit_refresh_bypasses_fresh_cache"]=schedules==2&&scheduleWrites==2;
        config="changed";await client.Schedule(false,default);checks["following_change_invalidates_schedule_cache"]=schedules==3;
        failSchedule=true;var failed=await client.Schedule(true,default);checks["network_failure_keeps_data_and_timestamp"]=failed.Stale&&failed.RefreshFailed&&failed.Cache.Matches.Single().Id==match.Id&&failed.Cache.Updated==now&&scheduleWrites==3;
        failSchedule=false;failCache=true;var unsaved=await client.Schedule(true,default);checks["cache_save_failure_does_not_discard_fetched_data"]=!unsaved.CacheSaved&&unsaved.Cache.Matches.Single().Id==match.Id&&scheduleWrites==3;failCache=false;
        var team=await client.Team(club,false,default);checks["team_schedule_and_table_share_registered_query"]=teamCalls==1&&!team.Stale&&team.Data.Rows.Single().Id==club.Id&&teamWrites==1;
        using(var facts=JsonDocument.Parse(await client.Facts("view:巴萨",default)))checks["legacy_team_view_shape_is_preserved"]=facts.RootElement.GetProperty("data").GetProperty("Rows").GetArrayLength()==1&&!facts.RootElement.GetProperty("stale").GetBoolean();
        await client.Facts("巴萨积分榜",default);checks["team_facts_reuse_warm_cache"]=teamCalls==1;
        failTable=true;var partial=await client.Team(club,true,default);checks["partial_failure_keeps_previous_table_and_season"]=partial.Stale&&partial.TableFailed&&!partial.MatchesFailed&&partial.Data.Rows.Single().Id==club.Id&&partial.Data.Season=="fixture"&&partial.Data.TableUpdated==now;
        var legacy=await Run("football_facts","今晚比赛");checks["legacy_schedule_shape_keeps_source_time_and_real_ids"]=legacy.Data!.Value.GetProperty("source").GetString()=="ESPN"&&legacy.Data.Value.GetProperty("matches")[0].GetProperty("Id").GetString()==match.Id&&legacy.Data.Value.TryGetProperty("stale",out _);
        checks["diagnostics_exclude_query_json"]=bus.RecentResults.All(r=>r.Data==null&&!r.Message.Contains(match.Id));
        await Run("football_open");checks["window_action_uses_registry"]=windows==1;
        checks["window_rejects_extra_value"]=(await Run("football_open","extra")).Status=="failed";
        checks["structured_query_is_supported"]=(await bus.ExecuteStructuredAsync(new("leo-action/1","football_facts",JsonSerializer.SerializeToElement(new{query="巴萨"}),"fixture"))).Status=="succeeded";
        checks["structured_extra_field_rejected"]=(await bus.ExecuteStructuredAsync(new("leo-action/1","football_facts",JsonSerializer.SerializeToElement(new{query="巴萨",extra=true}),"fixture"))).Status=="failed";
        checks["unknown_team_rejected"]=(await Run("football_team",JsonSerializer.Serialize(new{id="invented",league=club.League,force=false}))).Status=="failed";
        checks["invalid_competition_rejected"]=(await Run("football_team",JsonSerializer.Serialize(new{id=club.Id,league="../secret",force=false}))).Status=="failed";
        checks["duplicate_parameters_rejected"]=(await Run("football_schedule","{\"force\":false,\"force\":true}")).Status=="failed";
        checks["query_controls_and_long_text_rejected"]=(await Run("football_facts",new string('x',201))).Status=="failed"&&(await Run("football_facts","test\nquery")).Status=="failed";
        var national=Club.For("阿根廷");var competition=NationalCompetitions.For(national).Last();checks["national_competition_selection_is_preserved"]=FootballCapability.Target(national.Id,competition.Id).League==competition.Id;
        prefs.FootballModule=false;queries.Suspend();bus.CancelDisabled();checks["disabled_module_blocks_calls"]=(await Run("football_facts","今晚比赛")).Status=="failed";checks["disabled_module_removed_from_brain"]=!new BrainCatalog(bus.Registry).Read().Any(a=>a.Action.Id is "football_facts" or "football_open");prefs.FootballModule=true;
        var catalog=new BrainCatalog(bus.Registry);checks["brain_discovers_facts_only_once"]=catalog.Read().Count(a=>a.Action.Id=="football_facts")==1;checks["brain_does_not_expose_internal_cache_queries"]=!catalog.Read().Any(a=>a.Action.Id is "football_schedule" or "football_team");
        var todos=new TodoStore(null);bus.Registry.Register(new MatchPlanningCapability(()=>prefs.FootballModule,new MatchPlanningHost(()=>queries.CachedMatches([club])),todos,new ModuleWindowService(ui)));
        using(var brain=new BrainSession(catalog,bus.ExecuteDetailedAsync,(_,_)=>Task.FromResult(WorkflowVerification.Unknown),()=>new Dictionary<string,string>())){
            brain.BeginTurn("查询今晚比赛");var facts=await brain.Tool(Tool("leo_action",new{action="football_facts",value="今晚比赛"}),default);checks["brain_query_returns_real_provider_match"]=facts.Contains(match.Id);
            var unknown=await brain.Tool(Tool("leo_plan",new{steps=new[]{new{action="match_add",value="invented",from_step="",result_property=""}}}),default);checks["brain_rejects_unqueried_match_id"]=unknown.Contains("error");
        }
        var lookup=await Run("match_lookup",match.Id);checks["match_planning_uses_shared_query_snapshot"]=lookup.Status=="succeeded"&&lookup.Data!.Value.GetProperty("Match").GetProperty("Id").GetString()==match.Id;
        await Run("match_add",match.Id);await Run("match_add",match.Id);checks["query_to_todo_preserves_match_identity"]=todos.Items.Count==1&&todos.Items.Single().MatchId=="espn:"+match.Id;
        using(var form=new FootballForm(true,todos,new FootballFollowing(null),client)){form.Show();Application.DoEvents();checks["football_view_uses_shared_cached_schedule"]=form.QueryCacheCount==queries.CachedSchedule().Matches.Count;checks["team_page_stays_in_same_window"]=form.PreviewTeam();form.Close();}
        
        var events=await client.Events([match.Id],default);await client.Events([match.Id],default);checks["event_details_reuse_short_cache"]=eventCalls==1&&events.Matches.Single().Id==match.Id&&!events.Stale;
        var missing=await client.Events([match.Id,"999999"],default);checks["missing_event_does_not_invent_or_erase_reminder"]=missing.Stale&&missing.FailedCount==1&&missing.Matches.Length==1&&todos.Items.Single().MatchId=="espn:"+match.Id;
        checks["event_details_reject_invalid_id"]=(await Run("football_events",JsonSerializer.Serialize(new{ids=new[]{"../fake"}}))).Status=="failed";
        checks["queried_unfollowed_team_available_to_match_bridge"]=queries.CachedMatches([]).Any(x=>x.Match.Id==match.Id);
        checks["event_capability_not_exposed_to_model"]=!catalog.Read().Any(a=>a.Action.Id=="football_events");
        using(var pet=new PetForm(new Preferences{ApplicationIndexing=false},true)){pet.Show();foreach(var pair in await pet.VerifyFootballRegistry())checks["host_"+pair.Key]=pair.Value;pet.Close();}
        await CancellationChecks();await FailureChecks();return checks;

        async Task CancellationChecks()
        {
            bool enabled=true;int calls=0,writes=0;var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var service=new FootballQueries(()=>enabled,()=>"fixed",fetchSchedule:async(_,t)=>{int call=Interlocked.Increment(ref calls);if(call==1)await release.Task;return Cache(call==1?"stale-late":"new",true);},loadSchedule:()=>Cache("original"),loadTeam:_=>new(),saveSchedule:_=>writes++,saveTeam:(_,_)=>{});
            var settings=new Preferences();using var dispatcher=new ActionDispatcher(settings,(_,_,_)=>throw new Exception("bypass"),new ExtensionStore(Path.Combine(root,"cancel-modules")));dispatcher.Registry.Register(new FootballCapability(()=>enabled,service,dispatcher.ExecuteDetailedAsync,_=>Task.CompletedTask,()=>[club.Name]));
            using var cancellation=new CancellationTokenSource();var old=dispatcher.ExecuteDetailedAsync(new("football_schedule",FootballClient.ScheduleValue(true),"fixture"),cancellation.Token);cancellation.Cancel();checks["cancelled_wait_finishes_before_ignored_provider"]= (await old).Status=="cancelled"&&writes==0;
            enabled=false;service.Suspend();dispatcher.CancelDisabled();enabled=true;var next=dispatcher.ExecuteDetailedAsync(new("football_schedule",FootballClient.ScheduleValue(true),"fixture"));release.TrySetResult();var fresh=await next;checks["reenable_does_not_join_cancelled_execution"]=fresh.Status=="succeeded"&&calls==2&&writes==1&&service.CachedSchedule().Matches.Single().Id=="new";
            var late=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);string stamp="a";int lateWrites=0;
            using var configured=new FootballQueries(()=>true,()=>stamp,fetchSchedule:async(_,_)=>{await late.Task;return Cache("late",true);},loadSchedule:()=>Cache("old"),loadTeam:_=>new(),saveSchedule:_=>lateWrites++,saveTeam:(_,_)=>{});
            var request=configured.Schedule(true,default);stamp="b";late.TrySetResult();bool stopped=false;try{await request;}catch(OperationCanceledException){stopped=true;}checks["configuration_change_discards_late_result"]=stopped&&lateWrites==0&&configured.CachedSchedule().Matches.Single().Id=="old";
            var never=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int disposeWrites=0;
            var closing=new FootballQueries(()=>true,()=>"",fetchSchedule:async(_,t)=>{await never.Task;return Cache("late",true);},loadSchedule:()=>Cache(),loadTeam:_=>new(),saveSchedule:_=>disposeWrites++,saveTeam:(_,_)=>{});
            var active=closing.Schedule(true,default);closing.Dispose();never.TrySetResult();stopped=false;try{await active;}catch(OperationCanceledException){stopped=true;}checks["dispose_discards_outstanding_callback"]=stopped&&disposeWrites==0;
        }
        async Task FailureChecks()
        {
            var old=Team();int saves=0;
            using var failing=new FootballQueries(()=>true,()=>"",fetchSchedule:(_,_)=>throw new HttpRequestException("offline"),fetchTeam:(_,_,_)=>throw new HttpRequestException("offline"),loadSchedule:()=>Cache("saved"),loadTeam:_=>old,saveSchedule:_=>saves++,saveTeam:(_,_)=>saves++);
            var all=await failing.Schedule(false,default);var t=await failing.Team(club,false,default);checks["offline_schedule_and_team_keep_old_content"]=all.Stale&&all.Cache.Matches.Single().Id=="saved"&&t.Stale&&t.Data.Rows.Count==1&&t.Data.TableUpdated==old.TableUpdated&&saves==0;
            using var timeout=new FootballQueries(()=>true,()=>"",fetchSchedule:(_,_)=>throw new TaskCanceledException("provider timeout"),loadSchedule:()=>Cache(),loadTeam:_=>new(),saveSchedule:_=>{},saveTeam:(_,_)=>{});checks["provider_timeout_is_stale_data_not_user_cancel"]=(await timeout.Schedule(true,default)).RefreshFailed;
            using var partial=new FootballQueries(()=>true,()=>"",fetchTeam:(_,_,_)=>Task.FromResult((new TeamData{MatchesUpdated=now,Matches=[match with{Id="new-team"}],Rows=[],TableUpdated=now},false,true)),loadSchedule:()=>new(),loadTeam:_=>old,saveSchedule:_=>{},saveTeam:(_,_)=>{});var mixed=await partial.Team(club,true,default);checks["failed_table_cannot_erase_old_rows"]=mixed.Data.Rows.Count==1&&mixed.Data.TableUpdated==old.TableUpdated&&mixed.Data.Matches.Single().Id=="new-team";
            using var malformed=new FootballQueries(()=>true,()=>"",fetchSchedule:(_,_)=>Task.FromResult(new FootballCache{Matches=null!}),loadSchedule:()=>Cache("safe"),loadTeam:_=>new(),saveSchedule:_=>throw new Exception("should not save"),saveTeam:(_,_)=>{});checks["malformed_provider_data_retains_valid_cache"]=(await malformed.Schedule(true,default)).Cache.Matches.Single().Id=="safe";
            string disk=Path.Combine(root,"cache.json");AtomicJson.Write(disk,JsonSerializer.Serialize(Cache("disk")));string before=File.ReadAllText(disk);AtomicJson.BeforeReplace=path=>{if(path==disk)throw new IOException("fixture");};
            using var storage=new FootballQueries(()=>true,()=>"",fetchSchedule:(_,_)=>Task.FromResult(Cache("fetched",true)),loadSchedule:()=>Cache("disk"),loadTeam:_=>new(),saveSchedule:data=>AtomicJson.Write(disk,JsonSerializer.Serialize(data)),saveTeam:(_,_)=>{});var fetched=await storage.Schedule(true,default);AtomicJson.BeforeReplace=null;checks["atomic_cache_failure_preserves_old_file_and_new_memory"]=!fetched.CacheSaved&&File.ReadAllText(disk)==before&&storage.CachedSchedule().Matches.Single().Id=="fetched";
        }
    }
}
sealed partial class PetForm
{
    internal async Task<Dictionary<string,bool>> VerifyFootballRegistry()
    {
        var result=await commands.ExecuteDetailedAsync(new("football_facts","view:今晚比赛","fixture"));
        var opened=await commands.ExecuteDetailedAsync(new("football_open","","fixture"));
        return new(){["football_registered_in_real_host"]=result.Status=="succeeded"&&result.ModuleId=="football-data",["preview_preserves_offline_empty_shape"]=result.Data!.Value.GetProperty("cache").GetProperty("Matches").GetArrayLength()==0,["football_window_uses_host_client"]=opened.Status=="succeeded"&&launcher.SharedFootballClient==footballClient,["brain_has_single_football_query"]=new BrainCatalog(commands.Registry).Read().Count(x=>x.Action.Id=="football_facts")==1};
    }
}
