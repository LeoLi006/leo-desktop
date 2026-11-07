using System.Text.Json;
namespace LeoLauncher;
static class ClashDockShortcutTests
{
    public static void Run(string report)
    {
        var checks = new Dictionary<string,bool>();
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        try
        {
            using var tools = new FootTools(130,true);
            int opened = 0;
            tools.DockRequested = () => opened++;
            var prefs = new Preferences();
            tools.ConfigureModules(prefs);
            tools.StartPosition = FormStartPosition.Manual; tools.Location = new(-19000,-19000);
            tools.Show(); Application.DoEvents(); tools.PreviewUtilities();
            var button = tools.Controls.OfType<Button>().Single(b => b.AccessibleName == "打开 ClashDock");
            button.PerformClick();
            checks["shortcut_opens_dock_once"] = opened == 1;
            checks["shortcut_does_not_poll_proxy"] = typeof(FootTools).GetField("proxyTimer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic) == null;
            checks["shortcut_has_no_proxy_action_delegate"] = typeof(FootTools).GetField("ProxyAction") == null;
            prefs.ClashModule = false; tools.ConfigureModules(prefs); button.PerformClick();
            checks["disabled_shortcut_does_not_launch"] = opened == 1 && !button.Visible;
            prefs.ClashModule = true; tools.ConfigureModules(prefs); tools.PreviewUtilities(); button.PerformClick();
            checks["reenabled_shortcut_launches_again"] = opened == 2;
            string path = Path.Combine(Path.GetDirectoryName(report)!,"ClashDock.exe");
            File.WriteAllText(path, "fixture-not-executable");
            try
            {
                var start = ClashDockShortcut.StartInfo(path);
                checks["launch_preserves_exact_path_and_directory"] = start.FileName == path && start.WorkingDirectory == Path.GetDirectoryName(path) && start.UseShellExecute && start.Arguments.Length == 0;
                bool rejected=false;try{ClashDockShortcut.StartInfo(Environment.ProcessPath!);}catch(IOException){rejected=true;}
                checks["reject_unrelated_executable"] = rejected;
            }
            finally { File.Delete(path); }
            int launches=0,notices=0;
            bool Request(Func<string> read)=>ClashDockShortcut.TryOpen(read,_=>{launches++;return true;},_=>notices++);
            checks["unconfigured_does_not_launch_or_throw"]=!Request(()=>"")&&launches==0;
            checks["deleted_program_does_not_launch_or_throw"]=!Request(()=>path)&&launches==0;
            checks["unreadable_configuration_is_isolated"]=!Request(()=>throw new UnauthorizedAccessException())&&launches==0;
            checks["corrupt_configuration_is_isolated"]=!Request(()=>"invalid\0path")&&launches==0;
            File.WriteAllText(path,"fixture; never execute");
            checks["launch_failure_is_isolated"]=!ClashDockShortcut.TryOpen(()=>path,_=>throw new System.ComponentModel.Win32Exception(2),_=>notices++);
            checks["failed_dependency_does_not_break_later_request"]=Request(()=>path)&&launches==1;
            checks["missing_dependency_has_explicit_feedback"]=notices==5;
            File.Delete(path);
            checks["path_configuration_remains_explicit"]=button.ContextMenuStrip?.Items.OfType<ToolStripMenuItem>().Any(i=>UiLanguage.Source(i)=="设置 ClashDock 路径…")==true;
            tools.Close();
            File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,external_actions=0},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks}));}
    }
}