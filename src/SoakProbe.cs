using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace LeoLauncher;
static class SoakProbe
{
    [DllImport("user32.dll")]static extern int GetGuiResources(IntPtr process,int kind);
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);var samples=new List<object>();
        try{
            using var process=Process.GetCurrentProcess();using var pet=new PetForm(new Preferences{ApplicationIndexing=false,HideInFullscreen=false},true);pet.Show();
            var notes=new NoteStore(null);for(int i=0;i<80;i++)notes.Save(null,"持续运行便签 "+i);var todos=new TodoStore(null);for(int i=0;i<12;i++)todos.Add("测试事项 "+i,DateOnly.FromDateTime(DateTime.Today));
            var watch=Stopwatch.StartNew();int cycles=0;int firstGdi=0,lastGdi=0;long firstHeap=0,lastHeap=0;
            for(int batch=0;batch<6;batch++){
                long until=(batch+1)*30000;
                while(watch.ElapsedMilliseconds<until){
                    using(var quick=new QuickNoteForm(notes)){quick.Show();Application.DoEvents();quick.Close();}
                    if(cycles%6==0){using var full=new NotesForm(notes,(_,_)=>"fixture");full.Show();Application.DoEvents();full.Close();}
                    if(cycles%10==0){using var planner=new TodoForm(true,todos);planner.Show();Application.DoEvents();planner.Close();using var chat=new ChatForm(true);chat.Show();Application.DoEvents();chat.Dispose();}
                    pet.SoakCycle(cycles);Application.DoEvents();Thread.Sleep(100);cycles++;
                }
                GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();Application.DoEvents();process.Refresh();lastGdi=GetGuiResources(process.Handle,0);lastHeap=GC.GetTotalMemory(false);
                if(batch==0){firstGdi=lastGdi;firstHeap=lastHeap;}
                samples.Add(new{seconds=watch.Elapsed.TotalSeconds,cycles,gdi=lastGdi,user_handles=GetGuiResources(process.Handle,1),managed_heap=lastHeap,private_bytes=process.PrivateMemorySize64});
                File.WriteAllText(report+".progress",JsonSerializer.Serialize(new{samples}));
            }
            pet.Close();File.WriteAllText(report,JsonSerializer.Serialize(new{ok=lastGdi-firstGdi<=12&&lastHeap-firstHeap<16_000_000,duration_seconds=watch.Elapsed.TotalSeconds,cycles,checks=new{gdi_growth_within_12=lastGdi-firstGdi<=12,managed_heap_growth_under_16mb=lastHeap-firstHeap<16_000_000},samples,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));
        }catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),samples}));}
    }
}
sealed partial class PetForm
{
    internal void SoakCycle(int cycle)
    {
        if(cycle%10==0){prefs.PetSize=cycle%20==0?110:180;ResizePet();}
        if(cycle%30==0){UiTheme.Configure(cycle%60==0?"382":"83","Leo");RevealDock(true);}
        if(cycle%12==0){SetFullscreenHidden(true);SetFullscreenHidden(false);}
    }
}
