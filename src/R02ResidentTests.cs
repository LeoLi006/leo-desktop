using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;
static class R02ResidentTests
{
    static object Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;
    static int Count(string module,string? kind=null)=>ModuleActivities.Snapshot().Where(r=>r.Module==module&&(kind==null||r.Kind==kind)).Sum(r=>r.Active);
    static async Task Until(Func<bool> ready){var until=DateTime.UtcNow.AddSeconds(4);while(!ready()&&DateTime.UtcNow<until)await Task.Delay(10);if(!ready())throw new TimeoutException("resident fixture deadline");}
    static void Tick(System.Windows.Forms.Timer timer)=>typeof(System.Windows.Forms.Timer).GetMethod("OnTick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(timer,[EventArgs.Empty]);
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{var c=new Dictionary<string,bool>();try{var probe=await Check(c);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,short_probe=probe,real_api_requests=0,real_update_downloads=0,real_catalog_scans=0,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{ui.Close();}};Application.Run(ui);
    }
    static async Task<object> Check(Dictionary<string,bool> c)
    {
        int scans=0,observations=0;bool observationFails=false;CancellationToken oldToken=default;using var held=new ManualResetEventSlim();
        var prefs=new Preferences{ApplicationIndexing=true,ContextAware=true};
        using(var launcher=new LauncherForm(prefs,()=>{},true,true,scanner:t=>{int call=Interlocked.Increment(ref scans);if(call==1){oldToken=t;held.Wait();}return [new(call==1?"old fixture app":"new fixture app",@"C:\fixture\app.exe","fixture")];},observationOverride:()=>{observations++;if(observationFails)throw new IOException("fixture context failure");})){
            await Until(()=>Volatile.Read(ref scans)==1);launcher.ConfigureActivity();launcher.ConfigureActivity();c["index_scan_is_registered_once"]=Count("application-index","scan")==1&&scans==1;
            c["context_observer_has_one_polling_lease"]=Count("application-context","polling")==1;
            c["context_uses_contract_and_original_interval"]=typeof(LauncherForm).GetField("observationActivity",BindingFlags.Instance|BindingFlags.NonPublic)!.FieldType==typeof(IPeriodicTask)&&((IPeriodicTask)Field(launcher,"observationActivity")).Enabled&&((System.Windows.Forms.Timer)Field(launcher,"observer")).Interval==1500;
            prefs.ApplicationIndexing=false;launcher.ConfigureActivity();c["index_disable_cancels_worker"]=oldToken.IsCancellationRequested;
            c["index_disable_stops_context_observation"]=Count("application-context")==0&&!((System.Windows.Forms.Timer)Field(launcher,"observer")).Enabled;
            c["cancelled_sta_work_remains_counted_until_return"]=Count("application-index","scan")==1;
            prefs.ApplicationIndexing=true;launcher.ConfigureActivity();launcher.ConfigureActivity();c["reenable_does_not_start_parallel_sta_scans"]=scans==1;
            held.Set();await Until(()=>!(bool)Field(launcher,"catalogWorkerRunning")&&scans==2);
            var apps=(List<AppEntry>)Field(launcher,"apps");c["old_index_result_cannot_replace_new_generation"]=apps.Any(a=>a.Name=="new fixture app")&&!apps.Any(a=>a.Name=="old fixture app");
            c["completed_index_scan_releases_registration"]=Count("application-index")==0;
            var observer=(System.Windows.Forms.Timer)Field(launcher,"observer");int before=observations;Tick(observer);c["observer_tick_runs_once_after_repeated_configuration"]=observations==before+1;
            prefs.ContextAware=false;launcher.ConfigureActivity();before=observations;Tick(observer);launcher.Open();c["disabled_context_does_not_observe_from_tick_or_open"]=observations==before&&Count("application-context")==0;
            prefs.ContextAware=true;launcher.ConfigureActivity();before=observations;Tick(observer);c["context_reenable_has_no_duplicate_handler"]=observations==before+1&&Count("application-context","polling")==1;
            observationFails=true;Tick(observer);c["context_failure_stops_its_own_observer"]=!observer.Enabled;
            launcher.ConfigureActivity();c["reapplying_preferences_does_not_restart_failed_observer"]=!observer.Enabled;
            prefs.ContextAware=false;launcher.ConfigureActivity();((IPeriodicTask)Field(launcher,"observationActivity")).Retry();c["stale_context_retry_respects_disabled_option"]=!observer.Enabled;
            observationFails=false;prefs.ContextAware=true;launcher.ConfigureActivity();before=observations;Tick(observer);c["context_explicit_reenable_recovers_once"]=observer.Enabled&&observations==before+1;
            launcher.Dispose();((IPeriodicTask)Field(launcher,"observationActivity")).Retry();launcher.ConfigureActivity();Tick(observer);c["disposed_launcher_cannot_restore_observation"]=Count("application-context")==0;
        }
        int staleErrors=0;var previousObserver=FeatureFaults.Observer;FeatureFaults.Observer=f=>{if(f.Feature=="应用索引")staleErrors++;};
        try{
            using var failureGate=new ManualResetEventSlim();int entered=0;var off=new Preferences{ApplicationIndexing=true,ContextAware=false};
            using var launcher=new LauncherForm(off,()=>{},true,true,scanner:_=>{Interlocked.Increment(ref entered);failureGate.Wait();throw new IOException("old index error");});
            await Until(()=>Volatile.Read(ref entered)==1);off.ApplicationIndexing=false;launcher.ConfigureActivity();failureGate.Set();await Until(()=>!(bool)Field(launcher,"catalogWorkerRunning"));
            c["cancelled_index_error_is_not_reported"]=staleErrors==0;c["cancelled_index_error_does_not_repopulate_catalog"]=((List<AppEntry>)Field(launcher,"apps")).All(a=>a.Target==ClipboardTool.Target);
        }finally{FeatureFaults.Observer=previousObserver;}
        int currentFailures=0,currentScans=0;FeatureFaults.Observer=f=>{if(f.Feature=="应用索引")currentFailures++;};
        try{
            var retryPrefs=new Preferences{ApplicationIndexing=true,ContextAware=false};
            using var launcher=new LauncherForm(retryPrefs,()=>{},true,true,scanner:_=>{if(Interlocked.Increment(ref currentScans)==1)throw new IOException("current index fault");return [new("recovered fixture app",@"C:\fixture\recovered.exe","fixture")];});
            await Until(()=>!(bool)Field(launcher,"catalogWorkerRunning"));c["current_index_failure_is_reported_once"]=currentFailures==1&&Count("application-index")==0;
            launcher.ConfigureActivity();c["index_failure_does_not_start_automatic_rescan"]=currentScans==1;
            retryPrefs.ApplicationIndexing=false;launcher.ConfigureActivity();retryPrefs.ApplicationIndexing=true;launcher.ConfigureActivity();await Until(()=>!(bool)Field(launcher,"catalogWorkerRunning")&&currentScans==2);
            c["index_explicit_reenable_recovers_after_failure"]=((List<AppEntry>)Field(launcher,"apps")).Any(a=>a.Name=="recovered fixture app");
        }finally{FeatureFaults.Observer=previousObserver;}
        int queuedScans=0;
        using(var launcher=new LauncherForm(new Preferences{ApplicationIndexing=true,ContextAware=false},()=>{},true,true,scanner:_=>{Interlocked.Increment(ref queuedScans);return [new("queued old app",@"C:\fixture\old.exe","fixture")];})){
            if(!SpinWait.SpinUntil(()=>Volatile.Read(ref queuedScans)==1&&Count("application-index")==0,3000))throw new TimeoutException("queued index fixture");
            ((Preferences)Field(launcher,"prefs")).ApplicationIndexing=false;launcher.ConfigureActivity();Application.DoEvents();
            c["already_queued_index_completion_respects_disable"]=!((List<AppEntry>)Field(launcher,"apps")).Any(a=>a.Name=="queued old app");
        }
        using(var closeGate=new ManualResetEventSlim()){
            int entered=0;using var launcher=new LauncherForm(new Preferences{ApplicationIndexing=true},()=>{},true,true,scanner:t=>{Interlocked.Increment(ref entered);closeGate.Wait();return [];});
            await Until(()=>Volatile.Read(ref entered)==1);launcher.Dispose();c["launcher_dispose_keeps_pending_worker_accounted"]=Count("application-index")==1;closeGate.Set();await Until(()=>Count("application-index")==0);c["worker_completion_after_launcher_dispose_releases_lease"]=launcher.IsDisposed;
        }
        var clock=DateTimeOffset.Now;int calls=0,notifications=0;var release=new LeoRelease(new(9,0,0),"v9.0.0","","","",true);
        using(var watcher=new UpdateNotifications(_=>{calls++;return Task.FromResult<LeoRelease?>(release);},_=>notifications++,()=>clock)){
            c["updates_declare_common_contract"]=typeof(UpdateNotifications).GetField("activity",BindingFlags.Instance|BindingFlags.NonPublic)!.FieldType==typeof(IPeriodicTask)&&!((IPeriodicTask)Field(watcher,"activity")).Enabled;
            await watcher.Tick();c["disabled_update_check_has_no_activity"]=calls==0&&Count("update-check")==0;
            watcher.Configure(true);watcher.Configure(true);await watcher.Tick();c["update_startup_delay_and_single_schedule_preserved"]=calls==0&&Count("update-check","polling")==1;
            clock=clock.AddSeconds(31);await watcher.Tick();c["due_update_check_notifies_once"]=calls==1&&notifications==1;
            c["updates_preserve_timer_and_idle_state"]=((System.Windows.Forms.Timer)Field(watcher,"timer")).Interval==60000&&((IPeriodicTask)Field(watcher,"activity")).Enabled&&!((IPeriodicTask)Field(watcher,"activity")).Running;
            watcher.Configure(true);clock=clock.AddHours(1);await watcher.Tick();c["reapplying_update_setting_preserves_daily_deadline"]=calls==1;
            clock=clock.AddDays(1);await watcher.Tick();c["daily_update_schedule_is_preserved"]=calls==2&&notifications==2;
            watcher.Dispose();watcher.Configure(true);await watcher.Tick();c["disposed_update_watcher_cannot_restart"]=calls==2&&Count("update-check")==0;
        }
        calls=notifications=0;var response=new TaskCompletionSource<LeoRelease?>(TaskCreationOptions.RunContinuationsAsynchronously);CancellationToken updateToken=default;
        using(var watcher=new UpdateNotifications(t=>{calls++;updateToken=t;return calls==1?response.Task:Task.FromResult<LeoRelease?>(null);},_=>notifications++,()=>clock)){
            watcher.Configure(true);clock=clock.AddMinutes(1);var pending=watcher.Tick();watcher.Configure(false);c["update_disable_cancels_and_keeps_inflight_count"]=updateToken.IsCancellationRequested&&Count("update-check","update-request")==1;
            watcher.Configure(true);clock=clock.AddMinutes(1);await watcher.Tick();c["update_reenable_does_not_overlap_old_check"]=calls==1;
            response.SetResult(release);await pending;c["late_update_release_is_suppressed_after_reenable"]=notifications==0&&calls==2&&Count("update-check","update-request")==0;
        }
        int faultsBefore=FeatureFaults.Snapshot().Sum(f=>f.Count);
        using(var watcher=new UpdateNotifications(_=>throw new IOException("fixture offline"),_=>notifications++,()=>clock)){watcher.Configure(true);clock=clock.AddMinutes(1);await watcher.Tick();c["automatic_update_failure_stays_silent"]=FeatureFaults.Snapshot().Sum(f=>f.Count)==faultsBefore&&Count("update-check","polling")==1;}
        c["same_or_older_release_does_not_notify"]=!UpdateNotifications.ShouldNotify(release,release.Version,"")&&!UpdateNotifications.ShouldNotify(release,new(10,0,0),"")&&!UpdateNotifications.ShouldNotify(release,new(8,0,0),"v9.0.0");
        response=new(TaskCreationOptions.RunContinuationsAsynchronously);notifications=0;
        using(var watcher=new UpdateNotifications(t=>{updateToken=t;return response.Task;},_=>notifications++,()=>clock)){
            watcher.Configure(true);clock=clock.AddMinutes(1);var pending=watcher.Tick();watcher.Dispose();response.SetResult(release);await pending;c["update_dispose_cancels_late_notification"]=updateToken.IsCancellationRequested&&notifications==0&&Count("update-check")==0;
        }
        var focus=new FocusSession(null);focus.Start(25,true,DateTimeOffset.Now);
        using(var form=new FocusForm(focus,()=>{})){
            c["focus_window_declares_common_contract"]=typeof(FocusForm).GetField("displayActivity",BindingFlags.Instance|BindingFlags.NonPublic)!.FieldType==typeof(IPeriodicTask)&&!((IPeriodicTask)Field(form,"displayActivity")).Enabled&&((System.Windows.Forms.Timer)Field(form,"timer")).Interval==1000;
            c["unshown_focus_window_has_no_refresh_timer"]=Count("focus-window")==0;form.StartPosition=FormStartPosition.Manual;form.Location=new(-19000,-19000);form.Show();
            c["visible_focus_window_registers_refresh"]=Count("focus-window","polling")==1;
            c["visible_focus_contract_state"]=((IPeriodicTask)Field(form,"displayActivity")).Enabled&&!((IPeriodicTask)Field(form,"displayActivity")).Running;form.Hide();c["hidden_focus_window_stops_refresh_only"]=Count("focus-window")==0&&focus.Active(DateTimeOffset.Now);
            form.Show();form.Hide();form.Show();c["focus_window_reopen_has_one_refresh_handler"]=Count("focus-window","polling")==1;form.Close();c["focus_window_close_preserves_session"]=form.IsDisposed&&focus.Active(DateTimeOffset.Now)&&Count("focus-window")==0;
        }
        using(var pet=new PetForm(new Preferences{ApplicationIndexing=false,AutoUpdateNotifications=false},true,true))foreach(var row in await pet.VerifyFocusActivities())c[row.Key]=row.Value;
        c["all_resident_activity_leases_released"]=ModuleActivities.Snapshot().Length==0;
        var process=System.Diagnostics.Process.GetCurrentProcess();process.Refresh();int handles=process.HandleCount;long memory=process.WorkingSet64;var cpu=process.TotalProcessorTime;var watch=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<20;i++){using var updater=new UpdateNotifications(_=>Task.FromResult<LeoRelease?>(null),_=>{},()=>clock);updater.Configure(true);updater.Configure(true);clock=clock.AddMinutes(1);await updater.Tick();updater.Configure(false);}
        watch.Stop();process.Refresh();c["repeated_update_start_stop_leaves_no_activity"]=ModuleActivities.Snapshot().Length==0;
        return new{cycles=20,elapsed_ms=watch.ElapsedMilliseconds,cpu_ms=(process.TotalProcessorTime-cpu).TotalMilliseconds,handles_before=handles,handles_after=process.HandleCount,working_set_before=memory,working_set_after=process.WorkingSet64};
    }
}
sealed partial class PetForm
{
    internal async Task<Dictionary<string,bool>> VerifyFocusActivities()
    {
        var c=new Dictionary<string,bool>();int checks=0,completions=0;FocusTickOverride=()=>{checks++;if(focus.Complete(DateTimeOffset.Now))completions++;};companionActivity.Configure(true);
        focus.Start(25,true,DateTimeOffset.Now);focusWindow=new FocusForm(focus,()=>{});focusWindow.StartPosition=FormStartPosition.Manual;focusWindow.Location=new(-19000,-19000);focusWindow.Show();focusWindow.Close();
        c["focus_host_declares_common_contract"]=typeof(PetForm).GetField("companionActivity",BindingFlags.Instance|BindingFlags.NonPublic)!.FieldType==typeof(IPeriodicTask)&&companionActivity.Enabled&&!companionActivity.Running&&companionTimer.Interval==1000;
        c["host_focus_timer_survives_focus_window_close"]=companionTimer.Enabled&&focus.Active(DateTimeOffset.Now)&&ModuleActivities.Snapshot().Any(x=>x.Module=="focus-host"&&x.Kind=="polling");
        focus.Start(1,true,DateTimeOffset.Now.AddMinutes(-2));await companionActivity.RunNowAsync();await companionActivity.RunNowAsync();c["closed_window_session_completion_occurs_once"]=checks==2&&completions==1&&!focus.Active(DateTimeOffset.Now);
        c["health_summary_has_focus_type_but_no_session_content"]=ModuleActivities.Summary().Contains("专注计时")&&!ModuleActivities.Summary().Contains("25 分钟");
        FocusTickOverride=()=>throw new IOException("fixture focus save failure");await companionActivity.RunNowAsync();c["focus_failure_pauses_its_own_timer"]=!companionTimer.Enabled;
        Dispose();companionActivity.Retry();c["focus_retry_after_host_dispose_cannot_restart"]=!companionTimer.Enabled&&!ModuleActivities.Snapshot().Any(x=>x.Module=="focus-host"||x.Module=="focus-window");return c;
    }
}
