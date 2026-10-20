using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;
static class R02ActivityTests
{
    static object? shortProbe;
    static object Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;
    static int Count(string module,string? kind=null)=>ModuleActivities.Snapshot().Where(x=>x.Module==module&&(kind==null||x.Kind==kind)).Sum(x=>x.Active);
    static int Cancelling(string module)=>ModuleActivities.Snapshot().Where(x=>x.Module==module).Sum(x=>x.Cancelling);
    static async Task Until(Func<bool> ready){var until=DateTime.UtcNow.AddSeconds(4);while(!ready()&&DateTime.UtcNow<until)await Task.Delay(10);if(!ready())throw new TimeoutException("activity fixture deadline");}
    static void Show(Form form){form.StartPosition=FormStartPosition.Manual;form.Location=new(-19000,-19000);form.Show();}
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{
            var checks=new Dictionary<string,bool>();try{await Check(Path.GetDirectoryName(report)!,checks);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,short_probe=shortProbe,real_api_requests=0,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));}
            catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks}));}finally{Modules.SetFootball(true);ui.Close();}};Application.Run(ui);
    }
    static async Task Check(string root,Dictionary<string,bool> c)
    {
        using(var owner=new ActivityOwner("fixture")){
            using var first=owner.Begin("request");using var second=owner.Begin("request");var gate=Field(owner,"gate");bool inside=true;
            using var bad=first.Token.Register(()=>{inside=Monitor.IsEntered(gate);throw new IOException("injected cancellation callback");});owner.Reset();
            c["callback_runs_outside_owner_lock"]=!inside;c["bad_callback_does_not_skip_other_cancellation"]=second.Token.IsCancellationRequested;
            c["cancelled_work_remains_visible_until_completion"]=Count("fixture")==2&&Cancelling("fixture")==2;
            using var next=owner.Begin("request");c["reenable_has_new_generation"]=next.Current&&!first.Current;
            using var caller=new CancellationTokenSource();using var isolated=owner.Begin("request",caller.Token);caller.Cancel();c["subscriber_cancel_does_not_cancel_owner"]=!isolated.Current&&next.Current;
            owner.Dispose();bool rejected=false;try{owner.Begin("request");}catch(ObjectDisposedException){rejected=true;}c["disposed_owner_rejects_new_activity"]=rejected;
        }
        c["completed_leases_are_removed"]=Count("fixture")==0;
        bool enabled=true;int calls=0,writes=0;CancellationToken oldToken=default;var held=new TaskCompletionSource<FootballCache>(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var queries=new FootballQueries(()=>enabled,()=>"fixture",fetchSchedule:(_,t)=>{calls++;if(calls==1){oldToken=t;return held.Task;}return Task.FromResult(new FootballCache{Updated=DateTimeOffset.UtcNow,SchemaVersion=2});},loadSchedule:()=>new(),saveSchedule:_=>writes++)){
            var old=queries.Schedule(true,default);c["football_provider_request_is_registered"]=Count("football","schedule-refresh")==1;
            enabled=false;queries.Suspend();c["football_suspend_cancels_provider_and_retains_pending_count"]=oldToken.IsCancellationRequested&&Cancelling("football")==1;
            bool stopped=false;try{await queries.Schedule(true,default);}catch(OperationCanceledException){stopped=true;}c["football_disabled_cannot_start_new_work"]=stopped&&calls==1;
            enabled=true;var next=queries.Schedule(true,default);c["football_reenable_does_not_overlap_same_resource_provider"]=Count("football")==2&&calls==1;
            held.SetResult(new(){Updated=DateTimeOffset.UtcNow,SchemaVersion=2});stopped=false;try{await old;}catch(OperationCanceledException){stopped=true;}await next;
            c["old_football_generation_cannot_publish"]=stopped&&writes==1&&calls==2;c["football_tasks_release_counts"]=Count("football")==0;
        }
        bool teamSeen=false,eventSeen=false;
        using(var queries=new FootballQueries(()=>true,()=>"fixture",fetchTeam:(_,_,_)=>{teamSeen=Count("football","team-refresh")==1;return Task.FromResult((new TeamData(),false,false));},fetchEvents:(_,_)=>{eventSeen=Count("football","event-refresh")==1;return Task.FromResult<FootballMatch[]>([]);},loadTeam:_=>new(),saveTeam:(_,_)=>{})){
            await queries.Team(Club.For("巴塞罗那"),true,default);await queries.Events(["123"],default);c["team_and_event_queries_share_lifecycle_registration"]=teamSeen&&eventSeen;
        }
        var windowHeld=new TaskCompletionSource<FootballCache>(TaskCreationOptions.RunContinuationsAsynchronously);CancellationToken sharedToken=default;
        using(var queries=new FootballQueries(()=>true,()=>"fixture",fetchSchedule:(_,t)=>{sharedToken=t;return windowHeld.Task;},loadSchedule:()=>new(),saveSchedule:_=>{}))
        using(var bus=new ActionDispatcher(new Preferences(),(_,_,_)=>throw new Exception("legacy bypass"),new ExtensionStore(Path.Combine(root,"empty-football-extensions")))){
            bus.Registry.Register(new FootballCapability(()=>true,queries,bus.ExecuteDetailedAsync,_=>Task.CompletedTask,()=>[]));var client=new FootballClient(bus,queries);
            using(var football=new FootballForm(false,new TodoStore(null),new FootballFollowing(null),client)){
                Show(football);await Until(()=>Count("football")==1);var other=client.Schedule(false,default);football.Close();
                c["football_window_close_preserves_other_query_subscriber"]=football.IsDisposed&&!sharedToken.IsCancellationRequested&&Count("football")==1;
                windowHeld.SetResult(new(){Updated=DateTimeOffset.UtcNow,SchemaVersion=2});await other;c["football_shared_query_finishes_after_window_close"]=Count("football")==0;
                await client.Schedule(false,default);c["football_window_close_does_not_dispose_shared_service"]=Count("football")==0;
            }
        }
        int mediaCalls=0;var mediaOld=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);CancellationToken mediaToken=default;
        using(var media=new MediaForm((_,_,t)=>{mediaCalls++;if(mediaCalls==1){mediaToken=t;return mediaOld.Task;}return Task.FromResult(JsonSerializer.Serialize(new[]{new MediaSession("fixture:new","Fixture","new generation","","Paused")}));})){
            Show(media);await Until(()=>mediaCalls==1);c["visible_media_polling_and_refresh_are_registered"]=Count("media-window","polling")==1&&Count("media-window","refresh")==1;
            using(var health=new HealthChecksView(new Preferences(),true,null)){var label=(Label)Field(health,"result");c["health_check_reports_activity_types_and_counts"]=label.Text.Contains("媒体窗口")&&label.Text.Contains("轮询");c["health_check_does_not_include_media_titles"]=!label.Text.Contains("new generation");}
            media.Hide();c["hidden_media_stops_timer"]=!((System.Windows.Forms.Timer)Field(media,"timer")).Enabled&&Count("media-window","polling")==0;
            c["hidden_media_cancels_pending_request"]=mediaToken.IsCancellationRequested&&Cancelling("media-window")==1;
            Show(media);await Until(()=>mediaCalls==2);c["media_reopen_starts_fresh_refresh"]=Count("media-window","polling")==1&&((Label)Field(media,"title")).Text=="new generation";
            mediaOld.SetException(new IOException("old generation failure"));await Until(()=>Count("media-window","refresh")==0);
            c["old_media_error_cannot_overwrite_reopened_view"]=!((Label)Field(media,"status")).Text.Contains("old generation");c["old_refresh_does_not_clear_new_generation_state"]=!(bool)Field(media,"refreshing");
            media.Close();c["closed_media_releases_polling"]=media.IsDisposed&&Count("media-window")==0;
        }
        var prefs=new Preferences();var session=new MusicSession(new(null));var oldSearch=new TaskCompletionSource<MusicSong[]>(TaskCreationOptions.RunContinuationsAsynchronously);var pause=new TaskCompletionSource<MusicControlReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);int searches=0,controls=0;CancellationToken searchToken=default,controlToken=default;
        MusicSong[] rows=[new("123","fixture","fixture title","fixture artist","")];
        using(var bus=new ActionDispatcher(prefs,(_,_,_)=>throw new Exception("legacy bypass"),new ExtensionStore(Path.Combine(root,"empty-extensions")))){
            bus.Registry.ReplaceBuiltin(new MusicCapability(()=>prefs.MusicModule,()=>"fixture",()=>true,session,(q,t)=>{searches++;if(q=="held"){searchToken=t;return oldSearch.Task;}return Task.FromResult(rows);},(_,_,_)=>Task.CompletedTask,(t,_)=>{controls++;controlToken=t;return pause.Task;},(_,_)=>Task.FromResult(new MusicControlReceipt("fixture")),(_,_)=>Task.CompletedTask,()=>new(true,true),_=>Task.CompletedTask,new()));
            using var music=new MusicForm(true,shared:new MusicClient(session,bus));Show(music);var search=music.SearchAsync("held");await Until(()=>searches==1);
            c["music_search_is_registered"]=Count("music-window","search")==1;music.Close();await search;
            c["music_user_close_hides_and_cancels_search"]=!music.IsDisposed&&!music.Visible&&searchToken.IsCancellationRequested&&Count("music-window","search")==0;
            oldSearch.SetResult(rows);await Task.Delay(30);c["late_hidden_music_search_does_not_repopulate_view"]=music.Candidates.Length==0&&!music.Visible;
            Show(music);await music.SearchAsync("new");c["music_reopen_accepts_new_search"]=music.Candidates.Length==1;
            Task.Run(()=>typeof(MusicForm).GetMethod("Published",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(music,["queued old",new MusicSong[]{new("456","fixture","old queued result","","")}])).GetAwaiter().GetResult();
            music.Hide();Show(music);await Task.Delay(30);c["queued_music_publication_cannot_cross_visibility_generation"]=music.Candidates.Single().Id=="123";
            var control=(Task<string>)typeof(MusicForm).GetMethod("RunControl",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(music,["music_pause",""])!;await Until(()=>controls==1);music.Close();
            c["music_hide_keeps_submitted_control_running"]=!controlToken.IsCancellationRequested&&Count("music-window","control")==1;
            pause.SetResult(new("fixture accepted"));await control;c["music_control_completion_releases_activity"]=Count("music-window")==0;
            pause=new(TaskCreationOptions.RunContinuationsAsynchronously);control=(Task<string>)typeof(MusicForm).GetMethod("RunControl",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(music,["music_pause",""])!;await Until(()=>controls==2);music.Dispose();bool cancelled=false;try{await control;}catch(OperationCanceledException){cancelled=true;}
            c["music_dispose_cancels_window_control"]=controlToken.IsCancellationRequested&&cancelled;pause.TrySetResult(new("late"));await Task.Delay(30);c["disposed_music_does_not_reappear"]=music.IsDisposed&&Count("music-window")==0;
        }
        using(var pet=new PetForm(new Preferences{ApplicationIndexing=false,MusicModule=true,MediaModule=true,FootballModule=true},true,true)){
            var launcher=(LauncherForm)Field(pet,"launcher");var music=launcher.MusicWithoutOpening();Show(music);typeof(PetForm).GetMethod("ShowMediaCore",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pet,null);var media=(MediaForm)Field(pet,"mediaWindow");
            var next=LocalApplications.Clone((Preferences)Field(pet,"prefs"));next.EntryDisplayModes["music"]=EntryDisplay.Search;next.EntryDisplayModes["media"]=EntryDisplay.Search;pet.ApplyPlatformSettings(next);
            c["entry_hiding_keeps_window_activities_enabled"]=!media.IsDisposed&&!music.IsDisposed&&Count("media-window","polling")==1;
            next=LocalApplications.Clone(next);next.MusicModule=next.MediaModule=next.FootballModule=false;pet.ApplyPlatformSettings(next);
            c["actual_settings_disable_disposes_media_and_music_windows"]=media.IsDisposed&&music.IsDisposed;c["disabled_windows_leave_no_polling"]=Count("media-window")==0&&Count("music-window")==0;
            next=LocalApplications.Clone(next);next.MusicModule=next.MediaModule=next.FootballModule=true;pet.ApplyPlatformSettings(next);var replacement=launcher.MusicWithoutOpening();typeof(PetForm).GetMethod("ShowMediaCore",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pet,null);
            c["actual_settings_reenable_creates_fresh_windows"]=!replacement.IsDisposed&&!ReferenceEquals(music,replacement)&&!ReferenceEquals(media,Field(pet,"mediaWindow"));
        }
        c["all_scoped_activities_released_after_host_exit"]=ModuleActivities.Snapshot().Length==0;
        var process=System.Diagnostics.Process.GetCurrentProcess();process.Refresh();int handlesBefore=process.HandleCount;long memoryBefore=process.WorkingSet64;var cpuBefore=process.TotalProcessorTime;
        var watch=System.Diagnostics.Stopwatch.StartNew();bool disposedAll=true;for(int i=0;i<8;i++){using var media=new MediaForm((_,_,_)=>Task.FromResult("[]"),true);Show(media);media.Hide();Show(media);media.Close();disposedAll&=media.IsDisposed&&!((System.Windows.Forms.Timer)Field(media,"timer")).Enabled;}watch.Stop();process.Refresh();
        shortProbe=new{cycles=8,elapsed_ms=watch.ElapsedMilliseconds,cpu_ms=(process.TotalProcessorTime-cpuBefore).TotalMilliseconds,handles_before=handlesBefore,handles_after=process.HandleCount,working_set_before=memoryBefore,working_set_after=process.WorkingSet64};
        c["repeated_media_visibility_cycles_leave_no_activity"]=Count("media-window")==0;c["short_probe_windows_and_timers_are_released"]=disposedAll;
    }
}
