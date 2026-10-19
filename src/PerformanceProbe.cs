using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace LeoLauncher;

static class PerformanceProbe
{
    [DllImport("user32.dll")]static extern int GetGuiResources(IntPtr process,int kind);
    static void Pump(int milliseconds,Action? tick=null){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<milliseconds){tick?.Invoke();Application.DoEvents();Thread.Sleep(5);}}
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        try{
            using var process=Process.GetCurrentProcess();var startup=Stopwatch.StartNew();
            using var pet=new PetForm(new Preferences{ApplicationIndexing=false,HideInFullscreen=false},true);pet.Show();Application.DoEvents();double startMs=startup.Elapsed.TotalMilliseconds;pet.PreparePerformanceScene("idle");Pump(1000);
            var samples=new List<object>();
            foreach(string scene in new[]{"idle","docked","hidden","windows"}){
                pet.PreparePerformanceScene(scene);Pump(700);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();process.Refresh();
                var cpu=process.TotalProcessorTime;long allocated=GC.GetTotalAllocatedBytes(true);int frames=pet.LayeredFrames;int gdi=GetGuiResources(process.Handle,0);var watch=Stopwatch.StartNew();
                Pump(6000,()=>pet.PerformanceTick(scene));process.Refresh();
                samples.Add(new{scene,seconds=watch.Elapsed.TotalSeconds,cpu_ms=(process.TotalProcessorTime-cpu).TotalMilliseconds,allocated_bytes=GC.GetTotalAllocatedBytes(true)-allocated,private_bytes=process.PrivateMemorySize64,working_set=process.WorkingSet64,frames=pet.LayeredFrames-frames,gdi_before=gdi,gdi_after=GetGuiResources(process.Handle,0)});
            }
            pet.PreparePerformanceScene("idle");var opens=new List<double>();for(int i=0;i<12;i++){var watch=Stopwatch.StartNew();using var quick=new QuickNoteForm(new NoteStore(null));quick.Show();Application.DoEvents();opens.Add(watch.Elapsed.TotalMilliseconds);quick.Close();}
            pet.Close();File.WriteAllText(report,JsonSerializer.Serialize(new{ok=true,method="Isolated preview process; local fixtures; 6 seconds per scene; CPU is process time, not whole-machine percent; startup is pet construction to first Show, not OS cold start",runtime=Environment.Version.ToString(),pet_first_show_ms=startMs,quick_note_open_ms=opens,samples,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));
        }catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}
    }
}

sealed partial class PetForm
{
    internal void PreparePerformanceScene(string scene)
    {
        noticeWindow?.Close();todoWindow?.Close();RevealDock(true);Show();if(!hit.Visible)hit.Show(this);PositionFoot();if(!foot.Visible)foot.Show(this);
        var area=Screen.FromControl(this).WorkingArea;Location=new Point(area.Left+80,area.Top+100);row=frame=0;
        if(scene=="docked"){Location=new Point(area.Left,area.Top+100);dockSide=DockSide.Left;dockMotion.BeginTuck(0);for(int t=0;t<2400;t+=32)AdvanceDock(t,new Point(area.Right-5,area.Bottom-5));}
        if(scene=="hidden")SetFullscreenHidden(true);else SetFullscreenHidden(false);
        if(scene=="windows"){ShowTodos();ShowNotices();}
    }
    internal void PerformanceTick(string scene){if(scene=="docked")AdvanceDock(clock.ElapsedMilliseconds+5000,new Point(Screen.FromControl(this).WorkingArea.Right-5,Screen.FromControl(this).WorkingArea.Bottom-5));}
}
