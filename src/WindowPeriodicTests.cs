using System.Reflection;
using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;
static class WindowPeriodicTests
{
    static int Count(string module,string? kind=null)=>ModuleActivities.Snapshot().Where(r=>r.Module==module&&(kind==null||r.Kind==kind)).Sum(r=>r.Active);
    static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    public static void Run(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var host=new Form{Opacity=0,ShowInTaskbar=false};host.Shown+=async(_,_)=>{
            var c=new Dictionary<string,bool>();var observer=FeatureFaults.Observer;
            try{
                using var window=new Form();var timer=new System.Windows.Forms.Timer{Interval=60000};int calls=0,writes=0;CancellationToken old=default;
                var held=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var schedule=new WindowPeriodicTask(window,timer,"fixture-window-task","fixture周期","refresh",async t=>{calls++;if(calls==1){old=t;await held.Task;}t.ThrowIfCancellationRequested();writes++;});
                schedule.Configure(true);schedule.Configure(true);c["enable_registers_once"]=Count("fixture-window-task","polling")==1&&timer.Enabled;
                c["window_exposes_same_periodic_contract"]=schedule is IPeriodicTask contract&&contract.Enabled&&!contract.Running;
                var running=schedule.RunNowAsync();await schedule.RunNowAsync();c["duplicate_tick_merged"]=calls==1&&Count("fixture-window-task","refresh")==1;
                schedule.Configure(false);c["disable_cancels_and_stops_timer"]=old.IsCancellationRequested&&!timer.Enabled&&Count("fixture-window-task","polling")==0;
                schedule.Configure(true);held.TrySetResult();await running;
                c["reenable_retries_new_generation"]=calls==2&&writes==1&&Count("fixture-window-task","polling")==1;
                schedule.Dispose();schedule.Dispose();await schedule.RunNowAsync();schedule.Retry();schedule.Configure(true);
                c["disposed_service_cannot_restart"]=!timer.Enabled&&calls==2&&Count("fixture-window-task")==0;
                using var owner=new Form();var ownedTimer=new System.Windows.Forms.Timer{Interval=60000};var late=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int lateWrites=0;
                using var owned=new WindowPeriodicTask(owner,ownedTimer,"fixture-owned-task","fixture销毁","refresh",async t=>{await late.Task;t.ThrowIfCancellationRequested();lateWrites++;});
                owned.Configure(true);var outstanding=owned.RunNowAsync();owner.Dispose();
                c["owner_disposal_stops_and_cancels"]=!ownedTimer.Enabled&&ModuleActivities.Snapshot().Any(r=>r.Module=="fixture-owned-task"&&r.Cancelling==1);
                late.TrySetResult();await outstanding;c["late_result_discarded_after_disposal"]=lateWrites==0&&Count("fixture-owned-task")==0;
                using var faultOwner=new Form();var faultyTimer=new System.Windows.Forms.Timer{Interval=60000};int faults=0,attempts=0;FeatureFaults.Observer=f=>{if(f.Feature=="fixture失败")faults++;};
                using var faulty=new WindowPeriodicTask(faultOwner,faultyTimer,"fixture-fault-task","fixture失败","refresh",_=>{if(++attempts==1)throw new IOException("isolated fixture");return Task.CompletedTask;});
                faulty.Configure(true);await faulty.RunNowAsync();c["fault_pauses_only_its_task"]=faults==1&&!faultyTimer.Enabled&&Count("fixture-fault-task")==0;
                faulty.Retry();await faulty.RunNowAsync();c["explicit_retry_recovers"]=attempts==2&&faultyTimer.Enabled;
                faulty.Configure(false);
                var prefs=new Preferences{ClipboardExpireDays=0};int captures=0;string clipPath=Path.Combine(Path.GetDirectoryName(path)!,"isolated-clips.json");File.Delete(clipPath);File.Delete(clipPath+".bak");using var clips=new ClipboardHistoryService(prefs,true,clipPath,()=>captures++);
                using(var clipboard=ClipboardTool.Open(clips)!){
                    await Task.Delay(20);c["clipboard_refresh_registered"]=Count("clipboard-window","polling")==1;
                    clipboard.Hide();c["clipboard_hide_preserves_old_policy"]=Count("clipboard-window","polling")==1;
                    clips.History.Add(new("text","窗口刷新fixture","窗口刷新fixture",DateTimeOffset.Now));
                    var end=DateTime.UtcNow.AddSeconds(3);var list=clipboard.Controls.Cast<Control>().SelectMany(Walk).OfType<ListBox>().Single();
                    while(!list.Items.OfType<ClipItem>().Any(i=>i.Content=="窗口刷新fixture")&&DateTime.UtcNow<end)await Task.Delay(30);
                    c["clipboard_hidden_timer_refreshes_rows"]=list.Items.OfType<ClipItem>().Any(i=>i.Content=="窗口刷新fixture");
                    clipboard.Close();c["clipboard_close_releases_schedule"]=Count("clipboard-window")==0;
                }
                int before=captures;clips.Capture();await Task.Delay(20);c["history_listener_survives_window_close"]=!clips.IsClosed&&captures>before;
                using(var media=new MediaForm((_,_,_)=>throw new Exception("external forbidden"),true)){
                    media.ShowInTaskbar=false;media.Location=new(-20000,-20000);media.Show();var mediaTimer=Field<System.Windows.Forms.Timer>(media,"timer");
                    c["media_interval_and_registration_preserved"]=mediaTimer.Interval==5000&&mediaTimer.Enabled&&Count("media-window","polling")==1;
                    media.Hide();c["media_hide_stops_schedule"]=!mediaTimer.Enabled&&Count("media-window")==0;
                    media.Show();media.Close();c["media_close_releases_schedule"]=!mediaTimer.Enabled&&Count("media-window")==0;
                }
                var process=Process.GetCurrentProcess();process.Refresh();long memory=process.WorkingSet64;int handles=process.HandleCount;var watch=Stopwatch.StartNew();
                for(int i=0;i<30;i++){using var form=new Form();using var s=new WindowPeriodicTask(form,new(){Interval=60000},"fixture-cycle-task","fixture周期","refresh",_=>Task.CompletedTask);s.Configure(true);s.Configure(true);await s.RunNowAsync();form.Dispose();}
                process.Refresh();watch.Stop();c["thirty_cycles_release_activities"]=Count("fixture-cycle-task")==0;
                File.WriteAllText(path,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,resources=new{rounds=30,elapsed_ms=watch.ElapsedMilliseconds,handle_delta=process.HandleCount-handles,working_set_delta=process.WorkingSet64-memory},real_api_requests=0,real_clipboard_reads=0,external_process_starts=0}));
            }catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}
            finally{FeatureFaults.Observer=observer;host.Close();}
        };Application.Run(host);
    }
    static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control child in root.Controls)foreach(var c in Walk(child))yield return c;}
}
