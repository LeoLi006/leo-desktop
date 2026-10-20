using System.Reflection;
using System.Text.Json;
using System.Diagnostics;
namespace LeoLauncher;
static class ThemeServiceTests
{
    static ModuleThemeBinding Binding(object window)=>(ModuleThemeBinding)window.GetType().GetField("theme",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
    static void Queue(ModuleThemeBinding binding,ModuleThemeSnapshot snapshot)=>Task.Run(()=>typeof(ModuleThemeBinding).GetMethod("Changed",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(binding,[snapshot])).GetAwaiter().GetResult();
    public static void Run(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var host=new Form{Opacity=0,ShowInTaskbar=false};
        host.Shown+=async(_,_)=>{
            var checks=new Dictionary<string,bool>();var original=UiTheme.Snapshot();var options=UiTheme.Options;
            try{
                using var music=new MusicForm(true);using var media=new MediaForm((_,_,_)=>throw new Exception("external client forbidden"),true);
                var windows=new Form[]{music,media};var fonts=windows.Select(w=>w.Font).ToArray();var sizes=windows.Select(w=>w.Size).ToArray();
                foreach(var w in windows){w.ShowInTaskbar=false;w.Location=new(-20000,-20000);w.Show();}
                var bindings=windows.Select(Binding).ToArray();
                checks["both_pilots_subscribe"]=bindings.All(b=>b.Subscribed);
                UiTheme.Configure("382","Leo",new ThemeOptions("#ABCDEF",105,12,97));
                var city=UiTheme.Snapshot();checks["immutable_value_tokens"]=city.Accent==ColorTranslator.FromHtml("#ABCDEF")&&city.Radius==12&&city.Opacity==.97&&city.Font.Family=="Microsoft YaHei UI"&&city.LogicalScale==1;
                checks["pilots_use_same_revision"]=bindings.All(b=>b.Current==city);
                checks["appearance_matches_legacy_mapping"]=windows.All(w=>w.BackColor==BarcaUi.Canvas&&w.ForeColor==BarcaUi.Text&&w.Opacity==.97);
                checks["fonts_and_sizes_preserved"]=windows.Select((w,i)=>ReferenceEquals(w.Font,fonts[i])&&w.Size==sizes[i]).All(x=>x);
                var first=bindings[0];Queue(first,city);UiTheme.Configure("164","Leo");var latest=UiTheme.Snapshot();await Task.Delay(30);
                checks["obsolete_queued_revision_discarded"]=first.Current==latest&&music.BackColor==latest.Canvas;
                var oldListener=(Action<ModuleThemeSnapshot>)typeof(ModuleThemeBinding).GetField("listener",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(first)!;
                Queue(first,latest);music.Hide();var hidden=first.Current;UiTheme.Configure("202","Leo");await Task.Delay(30);
                checks["hide_unsubscribes_and_drops_pending"]=!first.Subscribed&&first.Current==hidden;
                music.Show();Task.Run(()=>oldListener(latest)).GetAwaiter().GetResult();await Task.Delay(30);checks["previous_subscription_cannot_reenter"]=first.Current==UiTheme.Snapshot();
                checks["reopen_reads_latest"]=first.Subscribed&&first.Current==UiTheme.Snapshot();
                Queue(first,UiTheme.Snapshot());music.Dispose();await Task.Delay(30);
                checks["dispose_unregisters"]=!first.Subscribed&&!ModuleThemeBinding.Managed(music);
                var second=bindings[1];media.Close();checks["media_close_unregisters"]=!second.Subscribed&&!ModuleThemeBinding.Managed(media);
                var process=Process.GetCurrentProcess();process.Refresh();var handles=process.HandleCount;var memory=process.WorkingSet64;var watch=Stopwatch.StartNew();bool drained=true;
                for(int i=0;i<30;i++){using var w=new Form{ShowInTaskbar=false,Opacity=0};using var b=new ModuleThemeBinding(w);w.Show();Queue(b,UiTheme.Snapshot());w.Hide();b.Dispose();drained&=!b.Subscribed&&!ModuleThemeBinding.Managed(w);}
                await Task.Delay(30);watch.Stop();process.Refresh();checks["thirty_lifecycles_drained"]=drained;
                File.WriteAllText(path,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,resources=new{rounds=30,elapsed_ms=watch.ElapsedMilliseconds,handle_delta=process.HandleCount-handles,working_set_delta=process.WorkingSet64-memory},external_clients=0,real_api_requests=0}));
            }catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks}));}
            finally{UiTheme.Configure(original.Skin,original.Name,options);host.Close();}
        };Application.Run(host);
    }
}
