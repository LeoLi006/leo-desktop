using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;
static class M06StabilityTests
{
    static object Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};
        ui.Shown+=async(_,_)=>{var checks=new Dictionary<string,bool>();try{
            using(var pet=new PetForm(new Preferences{ApplicationIndexing=false},true,true)){
                var bus=(ActionDispatcher)Field(pet,"commands");var prefs=(Preferences)Field(pet,"prefs");var state=(MediaCapabilityState)Field(pet,"mediaCapabilityState");
                state.Update([new("fixture:one","Fixture","title","","Paused")]);var media=bus.Registry.Resolve("media_status");var proxy=bus.Registry.Resolve("proxy_status");
                prefs.ClashModule=!prefs.ClashModule;typeof(PetForm).GetMethod("RegisterClientCapabilities",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pet,null);
                checks["proxy_change_preserves_media_instance"]=ReferenceEquals(media,bus.Registry.Resolve("media_status"));checks["proxy_change_preserves_discovered_targets"]=state.Read().Length==1;
                checks["proxy_change_replaces_proxy"]=!ReferenceEquals(proxy,bus.Registry.Resolve("proxy_status"));proxy=bus.Registry.Resolve("proxy_status");
                prefs.MediaModule=!prefs.MediaModule;typeof(PetForm).GetMethod("RegisterClientCapabilities",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pet,null);
                checks["media_change_preserves_proxy"]=ReferenceEquals(proxy,bus.Registry.Resolve("proxy_status"));checks["media_change_invalidates_own_targets"]=state.Read().Length==0;
            }
            for(int i=0;i<2;i++){
                var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);bool lockHeld=true;CancellationToken captured=default;
                using var query=new FootballQueries(()=>true,()=>"fixture",fetchSchedule:async(_,t)=>{captured=t;entered.SetResult();await Task.Delay(Timeout.Infinite,t);return new();},loadSchedule:()=>new(),saveSchedule:_=>{});
                var work=query.Schedule(true,default);await entered.Task;
                using var registration=captured.Register(()=>lockHeld=Monitor.IsEntered(Field(query,"gate")));
                if(i==0)query.Suspend();else query.Dispose();try{await work;}catch(OperationCanceledException){}
                checks[i==0?"suspend_cancels_outside_lock":"dispose_cancels_outside_lock"]=!lockHeld;
            }
            using(var launcher=new LauncherForm(new Preferences{ApplicationIndexing=false},()=>{},true,false)){
                int calls=0;CancellationToken captured=default;var held=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                launcher.CommandRequested=(_,t)=>{calls++;captured=t;return held.Task;};var first=launcher.PlatformSearch(":fixture");var second=launcher.PlatformSearch(":fixture");await second;
                checks["launcher_repeated_command_is_merged"]=calls==1;using var badCancel=captured.Register(()=>throw new IOException("injected cancellation failure"));launcher.Dispose();checks["launcher_dispose_cancels_command"]=captured.IsCancellationRequested;held.SetResult("late fixture");await first;checks["late_completion_does_not_reopen_disposed_launcher"]=launcher.IsDisposed;
            }
            using(var pet=new PetForm(new Preferences{ApplicationIndexing=false},true,true)){
                var query=(FootballQueries)Field(pet,"footballQueries");var life=(CancellationTokenSource)Field(Field(query,"activities"),"life");using var bad=life.Token.Register(()=>throw new IOException("injected cleanup failure"));var launcher=(LauncherForm)Field(pet,"launcher");
                var failingMedia=new MediaForm((_,_,_)=>Task.FromResult("[]"),true);failingMedia.Disposed+=(_,_)=>throw new IOException("injected window disposal failure");
                typeof(PetForm).GetField("mediaWindow",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(pet,failingMedia);
                pet.Dispose();checks["module_cleanup_failure_does_not_skip_launcher_release"]=launcher.IsDisposed;checks["host_cleanup_is_repeatable"]=pet.IsDisposed;pet.Dispose();
            }
            var clock=System.Diagnostics.Stopwatch.StartNew();var process=System.Diagnostics.Process.GetCurrentProcess();process.Refresh();int handlesBefore=process.HandleCount;long memoryBefore=process.WorkingSet64;
            for(int i=0;i<3;i++){using var pet=new PetForm(new Preferences{ApplicationIndexing=false},true,true);pet.Dispose();checks["short_cycle_"+i]=pet.IsDisposed;}
            clock.Stop();process.Refresh();
            File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,short_probe=new{cycles=3,elapsed_ms=clock.ElapsedMilliseconds,handles_before=handlesBefore,handles_after=process.HandleCount,working_set_before=memoryBefore,working_set_after=process.WorkingSet64},real_api_requests=0,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));
        }catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks}));}finally{ui.Close();}};Application.Run(ui);
    }
}
