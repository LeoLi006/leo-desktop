using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;
static class R02HostTests
{
    internal static int Count(string module,string? kind=null)=>ModuleActivities.Snapshot().Where(x=>x.Module==module&&(kind==null||x.Kind==kind)).Sum(x=>x.Active);
    internal static async Task Until(Func<bool> ready){var until=DateTime.UtcNow.AddSeconds(4);while(!ready()&&DateTime.UtcNow<until)await Task.Delay(10);if(!ready())throw new TimeoutException("host activity fixture deadline");}
    static void Tick(System.Windows.Forms.Timer timer)=>typeof(System.Windows.Forms.Timer).GetMethod("OnTick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(timer,[EventArgs.Empty]);
    static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    static int PrivacySubscribers()=>((Delegate?)typeof(ClipboardPrivacy).GetField("Changed",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null))?.GetInvocationList().Length??0;
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{
            var checks=new Dictionary<string,bool>();try{var probe=await Check(checks);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,short_probe=probe,real_api_requests=0,clipboard_content_reads=0,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));}
            catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks}));}finally{Modules.SetFootball(true);ui.Close();}};Application.Run(ui);
    }
    static async Task<object> Check(Dictionary<string,bool> c)
    {
        using(var timer=new System.Windows.Forms.Timer{Interval=600000}){
            int calls=0;bool fail=false;var held=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);CancellationToken token=default;
            using IPeriodicTask loop=new OwnedActivityTimer(timer,"fixture-host","后台测试","fixture-work",async t=>{calls++;token=t;if(calls==1)await held.Task;if(fail)throw new IOException("injected background fault");});
            loop.Configure(true);loop.Configure(true);c["repeated_enable_has_one_polling_lease"]=Count("fixture-host","polling")==1;
            c["contract_enabled_idle_state"]=loop.Enabled&&!loop.Running;
            var work=loop.RunNowAsync();await loop.RunNowAsync();Tick(timer);c["ticks_do_not_overlap_inflight_work"]=calls==1&&Count("fixture-host","fixture-work")==1;
            c["contract_running_state"]=loop.Enabled&&loop.Running;
            loop.Configure(false);c["disable_stops_timer_and_cancels_work"]=!timer.Enabled&&token.IsCancellationRequested;
            c["cancelled_pending_work_remains_counted"]=Count("fixture-host","fixture-work")==1;
            c["disabled_contract_still_reports_unfinished_work"]=!loop.Enabled&&loop.Running;
            loop.Configure(true);await loop.RunNowAsync();held.SetResult();await work;c["reenable_runs_once_after_old_work_finishes"]=calls==2&&Count("fixture-host","polling")==1&&Count("fixture-host","fixture-work")==0;
            fail=true;await loop.RunNowAsync();c["background_failure_stops_only_its_timer"]=!loop.Enabled&&!timer.Enabled&&Count("fixture-host")==0;
            loop.Configure(true);c["same_settings_do_not_silently_restart_faulted_timer"]=!timer.Enabled;
            loop.Configure(false);loop.Retry();c["stale_retry_cannot_restart_disabled_activity"]=!timer.Enabled;
            fail=false;loop.Configure(true);Tick(timer);c["explicit_reenable_recovers_without_duplicate_tick_handler"]=calls==4;
            loop.Dispose();loop.Configure(true);loop.Retry();Tick(timer);c["disposed_timer_has_no_handler_or_restart"]=calls==4&&Count("fixture-host")==0;
            c["disposed_contract_state"]=!loop.Enabled&&!loop.Running;
        }
        int baseline=PrivacySubscribers(),captures=0;bool captureFails=false,captureSeen=false;var prefs=new Preferences{ClipboardHistory=true};
        using(var clipboard=new ClipboardHistoryService(prefs,true,captureOverride:()=>{captures++;captureSeen=Count("clipboard-monitor","capture")==1;if(captureFails)throw new IOException("fixture capture failure");})){
            var timer=(System.Windows.Forms.Timer)Field(clipboard,"timer");var monitor=(IPeriodicTask)Field(clipboard,"monitor");clipboard.ConfigureActivity();clipboard.ConfigureActivity();
            c["clipboard_declares_common_contract"]=typeof(ClipboardHistoryService).GetField("monitor",BindingFlags.Instance|BindingFlags.NonPublic)!.FieldType==typeof(IPeriodicTask)&&monitor.Enabled&&!monitor.Running&&timer.Interval==450;
            c["clipboard_monitor_has_single_privacy_subscription"]=PrivacySubscribers()==baseline+1;c["clipboard_monitor_enable_is_idempotent"]=Count("clipboard-monitor","polling")==1;
            Tick(timer);c["clipboard_tick_is_registered_and_runs_once"]=captures==1&&captureSeen&&Count("clipboard-monitor","capture")==0;
            prefs.ClipboardHistory=false;clipboard.ConfigureActivity();Tick(timer);clipboard.Capture();c["clipboard_disable_blocks_timer_and_manual_capture"]=captures==1&&!timer.Enabled&&Count("clipboard-monitor")==0;
            prefs.ClipboardHistory=true;clipboard.ConfigureActivity();Tick(timer);c["clipboard_reenable_does_not_duplicate_capture"]=captures==2;
            captureFails=true;clipboard.Capture();c["clipboard_fault_releases_monitor_activity"]=!timer.Enabled&&Count("clipboard-monitor")==0;
            prefs.ClipboardHistory=false;clipboard.ConfigureActivity();monitor.Retry();c["clipboard_stale_retry_respects_disabled_setting"]=!timer.Enabled;
            captureFails=false;prefs.ClipboardHistory=true;clipboard.ConfigureActivity();Tick(timer);c["clipboard_explicit_reenable_recovers"]=captures==4;
            clipboard.Dispose();clipboard.ConfigureActivity();clipboard.RefreshPrivacy();clipboard.Capture();ClipboardPrivacy.Notify();Tick(timer);
            c["disposed_clipboard_unsubscribes_privacy_events"]=PrivacySubscribers()==baseline;c["disposed_clipboard_cannot_capture_or_restart"]=captures==4&&Count("clipboard-monitor")==0;
        }
        using(var pet=new PetForm(new Preferences{ApplicationIndexing=false,FootballModule=true},true,true))foreach(var result in await pet.VerifyBackgroundOwnership())c[result.Key]=result.Value;
        c["host_exit_releases_all_registered_background_activities"]=ModuleActivities.Snapshot().Length==0;
        var process=System.Diagnostics.Process.GetCurrentProcess();process.Refresh();int handles=process.HandleCount;long memory=process.WorkingSet64;var cpu=process.TotalProcessorTime;var clock=System.Diagnostics.Stopwatch.StartNew();int total=0;
        for(int i=0;i<30;i++){
            using var timer=new System.Windows.Forms.Timer{Interval=600000};using var loop=new OwnedActivityTimer(timer,"fixture-host","后台短测","fixture-work",_=>{total++;return Task.CompletedTask;});
            loop.Configure(true);loop.Configure(true);Tick(timer);loop.Configure(false);loop.Configure(true);Tick(timer);loop.Dispose();
        }
        clock.Stop();process.Refresh();c["repeated_start_stop_releases_all_leases"]=ModuleActivities.Snapshot().Length==0;c["repeated_start_stop_never_duplicates_handler"]=total==60;
        return new{cycles=30,elapsed_ms=clock.ElapsedMilliseconds,cpu_ms=(process.TotalProcessorTime-cpu).TotalMilliseconds,handles_before=handles,handles_after=process.HandleCount,working_set_before=memory,working_set_after=process.WorkingSet64};
    }
}
sealed partial class PetForm
{
    internal async Task<Dictionary<string,bool>> VerifyBackgroundOwnership()
    {
        var c=new Dictionary<string,bool>();int remindersChecked=0,requests=0;CancellationToken oldToken=default;
        ReminderTickOverride=()=>remindersChecked++;var held=new TaskCompletionSource<EventSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var match=new FootballMatch("123456",DateTimeOffset.Now.AddDays(2),"fixture","83","Barcelona","86","Real Madrid","pre","Scheduled","VS",true);
        var plan=todoClient.AddMatch(new(new("espn",match.Id,TimeZoneInfo.Local.Id),match,DateTimeOffset.Now,"https://www.espn.com/"));
        var fresh=match with{Kickoff=match.Kickoff.AddDays(2)};var old=match with{Kickoff=match.Kickoff.AddDays(1)};var publications=new List<DateTimeOffset?>();todoStore.Changed+=()=>publications.Add(todoStore.Items.Single().MatchKickoff);
        MatchSyncOverride=(_,t)=>{requests++;oldToken=t;return requests==1?held.Task:Task.FromResult(new EventSnapshot([fresh],DateTimeOffset.Now,false,0));};ConfigureBackgroundActivities();
        c["host_tasks_declare_common_contract"]=typeof(PetForm).GetField("reminderActivity",BindingFlags.Instance|BindingFlags.NonPublic)!.FieldType==typeof(IPeriodicTask)&&typeof(PetForm).GetField("matchSyncActivity",BindingFlags.Instance|BindingFlags.NonPublic)!.FieldType==typeof(IPeriodicTask);
        c["host_intervals_and_initial_state_preserved"]=reminderTimer.Interval==15000&&matchSyncTimer.Interval==600000&&reminderActivity.Enabled&&matchSyncActivity.Enabled&&!reminderActivity.Running&&!matchSyncActivity.Running;
        c["real_host_registers_reminder_and_match_polling"]=R02HostTests.Count("reminder-scheduler","polling")==1&&R02HostTests.Count("match-sync","polling")==1;
        ShowTodos();todoWindow!.Close();await reminderActivity.RunNowAsync();
        c["closing_todo_window_keeps_host_reminder_scheduler"]=reminderTimer.Enabled&&remindersChecked==1&&R02HostTests.Count("reminder-scheduler","polling")==1;
        var pending=SyncMatchesAsync();await R02HostTests.Until(()=>requests==1);var cancelledToken=oldToken;
        c["match_sync_request_has_host_ownership"]=R02HostTests.Count("match-sync","sync-request")==1;
        var next=LocalApplications.Clone(prefs);next.FootballModule=false;ApplyPlatformSettings(next);
        c["football_disable_cancels_host_sync_and_stops_timer"]=cancelledToken.IsCancellationRequested&&!matchSyncTimer.Enabled&&R02HostTests.Count("match-sync","polling")==0;
        c["football_disable_does_not_stop_reminders"]=reminderTimer.Enabled;next=LocalApplications.Clone(prefs);next.FootballModule=true;ApplyPlatformSettings(next);
        await SyncMatchesAsync();c["rapid_reenable_does_not_overlap_old_sync"]=requests==1;
        held.SetResult(new([old],DateTimeOffset.Now,false,0));await pending;
        c["rapid_reenable_discards_old_match_time"]=!publications.Contains(old.Kickoff)&&todoStore.Items.Single().MatchKickoff==fresh.Kickoff&&requests==2;
        c["match_sync_completion_releases_request_lease"]=R02HostTests.Count("match-sync","sync-request")==0&&R02HostTests.Count("match-sync","polling")==1;
        MatchSyncOverride=(_,_)=>throw new IOException("fixture background source failure");await SyncMatchesAsync();
        c["match_sync_failure_does_not_stop_reminder_scheduler"]=!matchSyncTimer.Enabled&&reminderTimer.Enabled;
        next=LocalApplications.Clone(prefs);next.FootballModule=false;ApplyPlatformSettings(next);matchSyncActivity.Retry();c["old_match_retry_cannot_reenable_disabled_football"]=!matchSyncTimer.Enabled;
        var closing=new TaskCompletionSource<EventSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);MatchSyncOverride=(_,t)=>{oldToken=t;return closing.Task;};next=LocalApplications.Clone(prefs);next.FootballModule=true;ApplyPlatformSettings(next);
        pending=SyncMatchesAsync();c["reenabled_host_accepts_fresh_sync"]=R02HostTests.Count("match-sync","sync-request")==1;
        Dispose();c["host_dispose_cancels_sync_before_store_teardown"]=oldToken.IsCancellationRequested&&!reminderTimer.Enabled&&!matchSyncTimer.Enabled;
        closing.SetResult(new([old],DateTimeOffset.Now,false,0));await pending;c["late_sync_after_host_dispose_does_not_write"]=todoStore.Items.Single().MatchKickoff==fresh.Kickoff;
        reminderActivity.Retry();matchSyncActivity.Retry();c["host_dispose_prevents_old_retry_restart"]=R02HostTests.Count("reminder-scheduler")==0&&R02HostTests.Count("match-sync")==0;
        return c;
    }
}
