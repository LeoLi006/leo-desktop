using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace LeoLauncher;

// An opt-in fixture, never part of the normal application lifecycle.
static class BaselineProbe
{
    [DllImport("user32.dll")]static extern int GetGuiResources(IntPtr process,int kind);
    static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    static void Pump(int milliseconds,List<double>? gaps=null)
    {
        var clock=Stopwatch.StartNew();double previous=clock.Elapsed.TotalMilliseconds;
        while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(10);double now=clock.Elapsed.TotalMilliseconds;gaps?.Add(now-previous);previous=now;}
    }
    public static void Run(string report,bool runtime=false,bool companion=false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
        try{
            using var process=Process.GetCurrentProcess();var startup=Stopwatch.StartNew();
            var fixtureDirectory=new DirectoryInfo(Store.Root);
            if(runtime&&(!fixtureDirectory.Name.Equals("Release",StringComparison.OrdinalIgnoreCase)||fixtureDirectory.Parent?.Name!=".build"))throw new InvalidOperationException("Runtime baseline only supports .build/Release");
            if(runtime&&Directory.Exists(Path.Combine(Store.Root,"models")))throw new InvalidOperationException("Use clean build fixtures, never installed app data");
            if(runtime&&new[]{"leo-settings.json","leo-ai.json","leo-history.json","leo-todos.json","leo-notes.json"}.Any(name=>File.Exists(Path.Combine(Store.Root,name))))throw new InvalidOperationException("Runtime baseline requires clean fixture data");
            var prefs=new Preferences{SkinId="83",ApplicationIndexing=false,ContextAware=false,ClipboardHistory=false,AutoUpdateNotifications=false,HideInFullscreen=false,TerminalModule=!companion,ClashModule=!companion,MusicModule=!companion,FootballModule=!companion,EdgeModule=!companion,MediaModule=!companion,SearchHotkey=Keys.None,VoiceHotkey=Keys.None,LyricsHotkey=Keys.None,QuickNoteHotkey=Keys.None};
            var stages=new Dictionary<string,double>();using var pet=new PetForm(prefs,!runtime,constructionProbe:stage=>stages[stage]=startup.Elapsed.TotalMilliseconds);stages["constructed"]=startup.Elapsed.TotalMilliseconds;pet.Show();Application.DoEvents();double firstShow=startup.Elapsed.TotalMilliseconds;File.WriteAllText(report+".startup.json",JsonSerializer.Serialize(stages,Json));
            var samples=new List<object>();
            foreach(var scene in new[]{"idle","docked","hidden","windows"}){
                pet.PrepareBaselineScene(scene,runtime);Pump(1200);process.Refresh();
                var cpu=process.TotalProcessorTime;long allocated=GC.GetTotalAllocatedBytes(false);int frames=pet.LayeredFrames;
                int handles=process.HandleCount,gdi=GetGuiResources(process.Handle,0),user=GetGuiResources(process.Handle,1);long memory=process.PrivateMemorySize64;
                var gaps=new List<double>();var timer=Stopwatch.StartNew();Pump(8000,gaps);double elapsed=timer.Elapsed.TotalSeconds;
                process.Refresh();gaps.Sort();double cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;
                samples.Add(new{scene,seconds=elapsed,cpu_ms=cpuMs,cpu_one_core_percent=cpuMs/(elapsed*10),cpu_machine_percent=cpuMs/(elapsed*10*Environment.ProcessorCount),allocated_bytes=GC.GetTotalAllocatedBytes(false)-allocated,private_bytes_before=memory,private_bytes_after=process.PrivateMemorySize64,working_set=process.WorkingSet64,handles_before=handles,handles_after=process.HandleCount,gdi_before=gdi,gdi_after=GetGuiResources(process.Handle,0),user_before=user,user_after=GetGuiResources(process.Handle,1),frames=pet.LayeredFrames-frames,pump_gap_p95_ms=gaps[(int)((gaps.Count-1)*.95)],pump_gap_max_ms=gaps[^1]});
            }
            pet.PreparePerformanceScene("idle");var opens=new List<double>();
            for(int i=0;i<10;i++){var clock=Stopwatch.StartNew();using var note=new QuickNoteForm(new NoteStore(null));note.Show();Application.DoEvents();opens.Add(clock.Elapsed.TotalMilliseconds);note.Close();}
            // Lifecycle cost uses the real dispatcher with a local cancellable executor.
            var enabled=new Preferences{MusicModule=true};int executions=0;
            using var dispatcher=new ActionDispatcher(enabled,async(_,_,token)=>{Interlocked.Increment(ref executions);await Task.Delay(Timeout.Infinite,token);return "unreachable";},new ExtensionStore(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!,"empty-modules")));
            var transitions=new List<double>();bool cancelled=true;
            for(int i=0;i<10;i++){
                enabled.MusicModule=true;var task=dispatcher.ExecuteAsync(new("music_search","fixture-"+i,"baseline"));enabled.MusicModule=false;
                var clock=Stopwatch.StartNew();dispatcher.CancelDisabled();while(!task.IsCompleted&&clock.ElapsedMilliseconds<2000)Pump(10);
                if(!task.IsCompleted)throw new TimeoutException("Fixture cancellation deadline exceeded");
                transitions.Add(clock.Elapsed.TotalMilliseconds);try{task.GetAwaiter().GetResult();cancelled=false;}catch(OperationCanceledException){}cancelled&=task.IsCanceled;
            }
            pet.Close();
            File.WriteAllText(report,JsonSerializer.Serialize(new{ok=cancelled&&executions==10,format="leo-q1-baseline-1",mode=runtime?(companion?"isolated-runtime-companion":"isolated-runtime-common"):"isolated-preview",runtime=Environment.Version.ToString(),logical_processors=Environment.ProcessorCount,os_build=Environment.OSVersion.Version.ToString(),pet_construction_to_first_show_ms=firstShow,process_uptime_to_probe_ms=(DateTime.Now-process.StartTime).TotalMilliseconds,quick_note_show_ms=opens,module_disable_cancel_ms=transitions,module_fixture_executions=executions,module_fixture_all_cancelled=cancelled,samples,limitations=new[]{"All modes disable indexing, clipboard, foreground history, auto-updates and global hotkeys. Runtime mode enables real host timers; preview does not. Common mode enables the six optional modules but does not launch external apps, record audio or call model APIs. Empty fixtures supply no matches to sync.","Pump gaps include the deliberate 10 ms sleep; they are scheduling responsiveness, not input latency.","Startup measures construction to Show, not cold boot or first painted frame. Process uptime is recorded at report completion.","No forced GC; allocations are process-wide managed allocations, private bytes include native memory. Request traffic is not instrumented; do not infer zero network traffic from this report."}},Json));
        }catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error_type=e.GetType().Name},Json));Environment.ExitCode=1;}
    }
}

sealed partial class PetForm
{
    internal void PrepareBaselineScene(string scene,bool runtime)
    {
        PreparePerformanceScene(scene);
        // Simulate intentional fullscreen hiding without opening a fullscreen app.
        // The real monitor must not immediately restore this synthetic scene.
        if(runtime){if(scene=="hidden")fullscreenTimer.Stop();else fullscreenTimer.Start();}
    }
}
