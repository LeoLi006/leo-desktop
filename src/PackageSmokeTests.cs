using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;
static class PackageSmokeTests
{
    public static Dictionary<string,bool> Check(string output)
    {
        Directory.CreateDirectory(output);string target=Path.Combine(output,"package-install-"+Guid.NewGuid().ToString("N"));var checks=new Dictionary<string,bool>();
        var source=LeoInstaller.Verify(AppContext.BaseDirectory);LeoInstaller.Install(AppContext.BaseDirectory,target,stop:false);
        checks["clean_install_has_no_personal_config"]=!Directory.EnumerateFiles(target,"leo-*.json").Any()&&LeoInstaller.Verify(target).Version==source.Version;
        string report=Path.Combine(output,"installed-runtime.json");var start=new ProcessStartInfo(Path.Combine(target,"LeoLauncher.exe")){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};start.ArgumentList.Add("--package-verify");start.ArgumentList.Add(report);using(var child=Process.Start(start)??throw new IOException("安装后的 Leo 未能启动")){if(!child.WaitForExit(30000))throw new TimeoutException("安装后的 Leo 启动超时");checks["installed_app_starts_with_bundled_runtime"]=child.ExitCode==0&&JsonDocument.Parse(File.ReadAllText(report)).RootElement.GetProperty("self_contained").GetBoolean();}
        File.WriteAllText(Path.Combine(target,"leo-settings.json"),"{\"PetSize\":130}");string backup=LeoInstaller.Install(AppContext.BaseDirectory,target,stop:false);File.WriteAllText(Path.Combine(target,"LeoLauncher.dll"),"simulated corrupt update");LeoInstaller.Restore(target,backup,stop:false);
        checks["real_payload_update_and_rollback_keep_settings"]=LeoInstaller.Verify(target).Version==source.Version&&File.ReadAllText(Path.Combine(target,"leo-settings.json"))=="{\"PetSize\":130}";
        using var firstRun=new FirstRunForm(new Preferences());firstRun.Show();Application.DoEvents();using var bitmap=new Bitmap(firstRun.Width,firstRun.Height);firstRun.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,"bundled-first-run.png"));checks["bundled_first_run_ui_opens"]=firstRun.Visible;
        File.WriteAllText(Path.Combine(output,"smoke-target.txt"),target);return checks;
    }
}
