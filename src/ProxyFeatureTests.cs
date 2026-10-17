using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Text.RegularExpressions;
namespace LeoLauncher;

static class ProxyFeatureTests
{
    public static Dictionary<string,bool> Check(string output)
    {
        Directory.CreateDirectory(output);var checks=new Dictionary<string,bool>();
        checks["spaced_home_labels_are_recognized"]=ClashProxy.IsHomeLabel("首 页")&&ClashProxy.IsHomeLabel("主 页")&&ClashProxy.IsHomeLabel("Home")&&!ClashProxy.IsHomeLabel("代理");
        using var toast=new LeoToast(()=>new Rectangle(600,400,136,148));
        toast.Present(new("系统代理已开启"));Application.DoEvents();int shortHeight=toast.Height;
        checks["short_notice_stays_compact"]=shortHeight<=58&&toast.PreviewTextFits;
        toast.Present(new("未找到 Clash 的系统代理开关。请确认客户端已就绪，再点击重试。如果仍无法操作，请在 Clash 首页确认系统代理状态后重试。",true,()=>Task.CompletedTask));Application.DoEvents();
        checks["long_proxy_error_grows_without_clipping"]=toast.Height>shortHeight&&toast.PreviewTextFits&&toast.PreviewHasRetry;
        using(var image=new Bitmap(toast.Width,toast.Height)){toast.DrawToBitmap(image,toast.ClientRectangle);image.Save(Path.Combine(output,"proxy-error-notice.png"));}
        toast.Present(new("系统代理已关闭"));Application.DoEvents();checks["next_short_notice_returns_to_compact_layout"]=toast.Height==shortHeight&&toast.PreviewTextFits&&!toast.PreviewHasRetry;
        return checks;
    }
    public static Dictionary<string,bool> Silent()
    {
        const string other="enable_system_proxy: true\r\nproxy_host: 127.0.0.1\r\n";
        string changed=ClashProxy.SilentStartConfig(other+"enable_silent_start: false\r\n");
        return new(){["native_silent_start_enabled"]=changed==other+"enable_silent_start: true\r\n",["silent_start_patch_is_idempotent"]=ClashProxy.SilentStartConfig(changed)==changed,["missing_setting_is_added_without_changing_other_settings"]=ClashProxy.SilentStartConfig(other)==changed,["empty_configuration_is_valid"]=ClashProxy.SilentStartConfig("")=="enable_silent_start: true\n"};
    }
    sealed class WindowWatch:IDisposable
    {
        [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")]static extern bool IsIconic(IntPtr h);
        readonly List<(IntPtr handle,bool shown)> windows=new();readonly Thread monitor;
        volatile bool stop;public volatile bool Appeared;
        static bool Shown(IntPtr h)=>IsWindowVisible(h)&&!IsIconic(h);
        public bool Restored=>windows.All(w=>Shown(w.handle)==w.shown);
        public WindowWatch()
        {
            foreach(var process in Process.GetProcessesByName("clash-verge"))using(process)
                foreach(AutomationElement window in AutomationElement.RootElement.FindAll(TreeScope.Children,new PropertyCondition(AutomationElement.ProcessIdProperty,process.Id)))
                    if(window.FindAll(TreeScope.Descendants,Condition.TrueCondition).Count>0){var h=(IntPtr)window.Current.NativeWindowHandle;windows.Add((h,Shown(h)));}
            monitor=new Thread(()=>{while(!stop){foreach(var w in windows)if(!w.shown&&Shown(w.handle))Appeared=true;Thread.Sleep(5);}}){IsBackground=true};monitor.Start();
        }
        public void Dispose(){stop=true;monitor.Join(1000);}
    }
    static void SelectOtherPage()
    {
        foreach(var process in Process.GetProcessesByName("clash-verge"))using(process)
            foreach(AutomationElement window in AutomationElement.RootElement.FindAll(TreeScope.Children,new PropertyCondition(AutomationElement.ProcessIdProperty,process.Id))){
                var page=window.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem)).Cast<AutomationElement>().FirstOrDefault(e=>Regex.Replace(e.Current.Name,@"\s","")=="代理");
                if(page==null)continue;
                var button=page.TryGetCurrentPattern(InvokePattern.Pattern,out _)?page:page.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.IsInvokePatternAvailableProperty,true));
                if(button?.TryGetCurrentPattern(InvokePattern.Pattern,out var pattern)==true){((InvokePattern)pattern).Invoke();Thread.Sleep(150);}return;
            }
    }
    public static object Live()
    {
        var before=ClashProxy.ReadStatus();bool changed=false,restored=false;string? error=null;
        if(!before.Running||before.Enabled==null)return new{ok=false,error="Clash 未运行或代理状态不明确，未进行切换"};
        using var windows=new WindowWatch();
        try{SelectOtherPage();changed=ClashProxy.SetEnabled(!before.Enabled.Value).Enabled==!before.Enabled.Value;}
        catch(Exception e){error=e.Message;}
        finally{try{restored=ClashProxy.SetEnabled(before.Enabled.Value).Enabled==before.Enabled.Value;}catch(Exception e){error=(error??"")+"；恢复状态失败："+e.Message;}}
        return new{ok=changed&&restored&&!windows.Appeared&&windows.Restored,changed,restored,clash_window_flashed=windows.Appeared,window_state_restored=windows.Restored,error};
    }
}
