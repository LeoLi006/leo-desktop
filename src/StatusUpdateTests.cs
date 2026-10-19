using System.Text.Json;
namespace LeoLauncher;
static class StatusUpdateTests
{
    public static void Run(string report){try{Directory.CreateDirectory(Path.GetDirectoryName(report)!);var checks=Check(Path.GetDirectoryName(report)!);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}}
    static IEnumerable<Control> Walk(Control root){foreach(Control child in root.Controls){yield return child;foreach(var nested in Walk(child))yield return nested;}}
    static Dictionary<string,bool> Check(string folder)
    {
        var c=new Dictionary<string,bool>();var file=Path.Combine(folder,"fixture.exe");File.WriteAllText(file,"fixture");
        c["disabled_wins_over_invalid_path"]=ModuleStatuses.Resolve("test","Test",false,"missing",()=>false).State==ModuleState.Disabled;
        c["missing_dependency_is_not_installed"]=ModuleStatuses.Resolve("test","Test",true,null,()=>false).State==ModuleState.NotInstalled;
        c["broken_explicit_path_is_invalid"]=ModuleStatuses.Resolve("test","Test",true,"missing",()=>true).State==ModuleState.Invalid;
        c["wrong_program_is_invalid"]=ModuleStatuses.Resolve("test","Test",true,file,()=>true,()=>false).State==ModuleState.Invalid;
        c["valid_dependency_is_ready"]=ModuleStatuses.Resolve("test","Test",true,file,()=>true,()=>true).State==ModuleState.Ready;
        long first=ModuleActivity.Begin("test");c["running_operation_waits"]=ModuleStatuses.Resolve("test","Test",true,null,()=>true).State==ModuleState.Waiting;
        long second=ModuleActivity.Begin("test");ModuleActivity.Complete("test",first,ModuleState.Failed,"old");c["old_result_cannot_replace_newer_request"]=ModuleActivity.Get("test")?.State==ModuleState.Waiting;
        ModuleActivity.Complete("test",second,ModuleState.Failed,"fixture failure");c["failure_retains_actionable_message"]=ModuleStatuses.Resolve("test","Test",true,null,()=>true) is {State:ModuleState.Failed,Detail:"fixture failure",Action:"重新检测"};
        ModuleActivity.Set("test",ModuleState.Ready,"done");c["recheck_clears_transient_failure"]=ModuleStatuses.Resolve("test","Test",true,null,()=>true).State==ModuleState.Ready;
        var pending=new TaskCompletionSource<string>();using(var dispatcher=new ActionDispatcher(new Preferences(),(_,_,_)=>pending.Task,new ExtensionStore(Path.Combine(folder,"no-extensions")))){
            var work=dispatcher.ExecuteAsync(new("music_open"));c["real_dispatcher_records_waiting"]=ModuleActivity.Get("music")?.State==ModuleState.Waiting;pending.SetException(new IOException("test failure"));try{work.GetAwaiter().GetResult();}catch(IOException){}c["real_dispatcher_records_failure"]=ModuleActivity.Get("music")?.State==ModuleState.Failed;
        }
        ModuleActivity.Set("music",ModuleState.Ready,"");
        var current=new Version(1,0,2);LeoRelease Release(string tag)=>new(LeoUpdates.TagVersion(tag)!,tag,"","","",true);
        c["no_release_is_silent"]=!UpdateNotifications.ShouldNotify(null,current,"");c["same_version_is_silent"]=!UpdateNotifications.ShouldNotify(Release("v1.00.2"),current,"");c["older_version_is_silent"]=!UpdateNotifications.ShouldNotify(Release("v1.00.1"),current,"");c["newer_version_notifies"]=UpdateNotifications.ShouldNotify(Release("v1.00.3"),current,"");c["same_release_not_repeated"]=!UpdateNotifications.ShouldNotify(Release("v1.00.3"),current,"v1.0.3");c["later_release_still_notifies"]=UpdateNotifications.ShouldNotify(Release("v1.00.4"),current,"v1.00.3");
        int networkCalls=0;using(var watcher=new UpdateNotifications(_=>{networkCalls++;return Task.FromResult<LeoRelease?>(null);},_=>throw new Exception("should not notify"))){watcher.Configure(false);watcher.Tick().GetAwaiter().GetResult();c["disabled_watcher_does_not_connect"]=networkCalls==0;watcher.Configure(true);watcher.Tick().GetAwaiter().GetResult();c["startup_check_is_delayed"]=networkCalls==0;watcher.Configure(false);}
        var clock=DateTimeOffset.Now;int calls=0,notifications=0;using(var watcher=new UpdateNotifications(_=>{calls++;return Task.FromResult<LeoRelease?>(Release("v1.00.3"));},_=>notifications++,()=>clock)){
            watcher.Configure(true);clock=clock.AddSeconds(31);watcher.Tick().GetAwaiter().GetResult();c["timer_checks_and_delivers_result"]=calls==1&&notifications==1;
            watcher.Configure(true);clock=clock.AddHours(1);watcher.Tick().GetAwaiter().GetResult();c["saving_enabled_setting_does_not_restart_daily_schedule"]=calls==1;
            clock=clock.AddDays(1);watcher.Tick().GetAwaiter().GetResult();c["daily_check_repeats"]=calls==2;
        }
        var response=new TaskCompletionSource<LeoRelease?>();clock=clock.AddDays(1);using(var watcher=new UpdateNotifications(_=>response.Task,_=>notifications++,()=>clock)){
            watcher.Configure(true);clock=clock.AddMinutes(1);var task=watcher.Tick();int before=notifications;watcher.Configure(false);response.SetResult(Release("v1.00.3"));task.GetAwaiter().GetResult();c["disabling_inflight_check_suppresses_notification"]=notifications==before;
        }
        using(var watcher=new UpdateNotifications(_=>Task.FromException<LeoRelease?>(new IOException("offline")),_=>throw new Exception("should not notify"),()=>clock)){watcher.Configure(true);clock=clock.AddMinutes(1);watcher.Tick().GetAwaiter().GetResult();c["offline_check_fails_silently"]=true;}
        c["legacy_preferences_enable_notifications"]=JsonSerializer.Deserialize<Preferences>("{}")!.AutoUpdateNotifications;
        var saved=new Preferences{AutoUpdateNotifications=false,LastNotifiedUpdate="v1.00.3"};var restored=LocalApplications.Clone(saved);c["update_preferences_roundtrip"]=!restored.AutoUpdateNotifications&&restored.LastNotifiedUpdate==saved.LastNotifiedUpdate;
        Preferences? applied=null;using(var settings=new SettingsForm(new Preferences(),(value,_)=>applied=value,()=>{},true)){
            settings.StartPosition=FormStartPosition.Manual;settings.Location=new(-19000,-19000);settings.Show();Application.DoEvents();var checkbox=Walk(settings).OfType<CheckBox>().Single(x=>x.Text.StartsWith("自动检查更新"));checkbox.Checked=false;Walk(settings).OfType<Button>().Single(x=>x.Text=="保存设置").PerformClick();c["settings_saves_notification_toggle"]=applied?.AutoUpdateNotifications==false;settings.Close();
        }
        using(var form=new ModuleStatusForm(new Preferences(),_=>{})){form.StartPosition=FormStartPosition.Manual;form.Location=new(-19000,-19000);form.Show();Application.DoEvents();using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,"module-status.png"));c["status_page_renders"]=form.Controls.OfType<FlowLayoutPanel>().Single().Controls.Count>=7;form.Close();}
        using(var pet=new PetForm(new Preferences(),true)){pet.Show();Application.DoEvents();foreach(var pair in pet.VerifyHorizontalDock())c[pair.Key]=pair.Value;}
        File.Delete(file);return c;
    }
}
sealed partial class PetForm
{
    internal Dictionary<string,bool> VerifyHorizontalDock(){var c=new Dictionary<string,bool>();var area=Screen.FromControl(this).WorkingArea;var far=new Point(area.Left+area.Width/2,area.Top+5);foreach(int size in new[]{80,130,230}){RevealDock(true);ClientSize=new(size,size*208/192);foreach(var side in new[]{DockSide.Left,DockSide.Right})foreach(int gap in new[]{0,1,3}){RevealDock(true);dockSide=side;Location=new(side==DockSide.Left?area.Left+gap:area.Right-Width-gap,area.Top+area.Height/2);dockMotion.Reset(0);for(int t=0;t<=2400;t+=32)AdvanceDock(t,far);c[$"{side}_{size}_gap{gap}_window_snaps_flush"]=side==DockSide.Left?Left==area.Left:Right==area.Right;c[$"{side}_{size}_gap{gap}_head_hits_edge"]=dockMotion.Progress>.999f&&(side==DockSide.Left?hit.Left==area.Left:hit.Right==area.Right)&&!foot.Visible;}}RevealDock(true);return c;}
}
