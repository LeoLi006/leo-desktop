using System.Reflection;
using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;
static class ThemeCleanupTests
{
    sealed class FaultLabel:Label
    {
        public bool Fail;
        protected override void OnBackColorChanged(EventArgs e){if(Fail){Fail=false;throw new IOException("PRIVATE_THEME_CONTENT");}base.OnBackColorChanged(e);}
    }
    static void Queue(ModuleThemeBinding binding,ModuleThemeSnapshot snapshot)=>Task.Run(()=>typeof(ModuleThemeBinding).GetMethod("Changed",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(binding,[snapshot])).GetAwaiter().GetResult();
    static Form Window()=>new(){Text="PRIVATE_WINDOW_NAME",ShowInTaskbar=false,Opacity=0,Location=new(-20000,-20000),StartPosition=FormStartPosition.Manual};
    public static void Run(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var host=Window();host.Shown+=async(_,_)=>{
            var c=new Dictionary<string,bool>();var original=UiTheme.Snapshot();var options=UiTheme.Options;var observer=FeatureFaults.Observer;
            try{
                var baseline=ModuleThemeBinding.Diagnostics();using var first=Window();using var second=Window();
                var label=new FaultLabel{BackColor=BarcaUi.Surface};first.Controls.Add(label);
                using var a=new ModuleThemeBinding(first);using var b=new ModuleThemeBinding(second);
                c["hidden_bindings_counted"]=ModuleThemeBinding.Diagnostics()==baseline with{Bindings=baseline.Bindings+2};
                first.Show();second.Show();c["visible_subscriptions_counted"]=ModuleThemeBinding.Diagnostics() is var active&&active.Bindings==baseline.Bindings+2&&active.Subscriptions==baseline.Subscriptions+2;
                Queue(a,UiTheme.Snapshot());c["queued_delivery_counted"]=ModuleThemeBinding.Diagnostics().Pending==baseline.Pending+1;
                first.Hide();await Task.Delay(15);c["hidden_pending_drains"]=ModuleThemeBinding.Diagnostics() is var hidden&&hidden.Pending==baseline.Pending&&hidden.Subscriptions==baseline.Subscriptions+1;
                UiTheme.Configure("382","Leo");int faults=0;FeatureFaults.Observer=f=>{if(f.Feature=="窗口主题")faults++;};label.Fail=true;
                first.Show();c["reopen_failure_isolated"]=first.Visible&&faults==1&&a.Current.Revision!=UiTheme.Snapshot().Revision;
                UiTheme.Configure("164","Leo");c["failed_window_recovers_next_theme"]=a.Current==UiTheme.Snapshot()&&b.Current==UiTheme.Snapshot();
                var summary=HostServiceDiagnostics.Summary(new Preferences());c["summary_counts_only"]=summary.Contains("主题（进程）：绑定 "+(baseline.Bindings+2))&&summary.Contains("活跃订阅")&&summary.Contains("在途通知")&&!summary.Contains("PRIVATE_");
                using(var health=new HealthChecksView(new Preferences(),true,null)){var result=(Label)typeof(HealthChecksView).GetField("result",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(health)!;c["existing_health_view_shows_theme_summary"]=result.AutoSize&&result.Text.Contains("主题（进程）");}
                int unexpected=0;FeatureFaults.Observer=_=>unexpected++;
                Queue(a,UiTheme.Snapshot());Queue(b,UiTheme.Snapshot());
                c["two_queued_deliveries_counted"]=ModuleThemeBinding.Diagnostics().Pending==baseline.Pending+2;
                first.Dispose();second.Dispose();a.Dispose();b.Dispose();await Task.Delay(20);
                c["dispose_drains_and_unregisters"]=ModuleThemeBinding.Diagnostics()==baseline;
                c["normal_disposal_not_reported_as_fault"]=unexpected==0;
                var process=Process.GetCurrentProcess();process.Refresh();long memory=process.WorkingSet64;int handles=process.HandleCount;var watch=Stopwatch.StartNew();bool drained=true;
                for(int i=0;i<30;i++){using var w=Window();using var binding=new ModuleThemeBinding(w);w.Show();Queue(binding,binding.Current);w.Hide();w.Show();w.Dispose();}
                await Task.Delay(30);drained=ModuleThemeBinding.Diagnostics()==baseline;process.Refresh();watch.Stop();c["thirty_lifecycles_return_to_baseline"]=drained;
                File.WriteAllText(path,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,resources=new{rounds=30,elapsed_ms=watch.ElapsedMilliseconds,handle_delta=process.HandleCount-handles,working_set_delta=process.WorkingSet64-memory},real_api_requests=0,external_process_starts=0}));
            }catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}
            finally{FeatureFaults.Observer=observer;UiTheme.Configure(original.Skin,original.Name,options);host.Close();}
        };Application.Run(host);
    }
}
