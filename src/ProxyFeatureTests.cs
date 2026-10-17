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
    public static Dictionary<string,bool> Startup()
    {
        var checks=new Dictionary<string,bool>();int stage=0,attempts=0,issued=0;
        var result=ProxyRequest.Complete(true,TimeSpan.FromSeconds(2),()=>new(stage>=2,stage<2?null:stage>=7,"fixture"),()=>stage>=9,(wanted,before)=>{attempts++;if(stage<5)return false;issued++;return true;},()=>stage++);
        checks["waits_for_process_controls_and_live_proxy_port"]=result.Enabled==true&&stage==9&&attempts>1&&issued==1;
        stage=0;issued=0;
        ProxyRequest.Complete(true,TimeSpan.FromSeconds(2),()=>new(true,true,"fixture"),()=>stage>=3,(_,_)=>{issued++;return true;},()=>stage++);
        checks["already_enabled_during_startup_waits_for_port_without_toggling"]=stage==3&&issued==0;
        issued=0;var off=ProxyRequest.Complete(false,TimeSpan.FromSeconds(2),()=>new(true,false,"fixture"),()=>false,(_,_)=>{issued++;return true;},()=>{});
        checks["disabling_does_not_require_live_proxy_listener"]=off.Enabled==false&&issued==0;
        bool timeout=false;try{ProxyRequest.Complete(true,TimeSpan.FromMilliseconds(5),()=>new(false,null,"fixture"),()=>false,(_,_)=>false,()=>Thread.Sleep(1));}catch(InvalidOperationException){timeout=true;}
        checks["failed_startup_is_bounded_and_never_reports_success"]=timeout;
        string config="enable_silent_start: true\nhotkeys:\n- other_action,Ctrl+F8\nproxy_host: 127.0.0.1\n";
        string patched=ClashShortcut.Prepare(config,"Ctrl+Alt+Shift+F11");
        checks["cold_start_shortcut_preserves_existing_actions"]=patched.Contains("- other_action,Ctrl+F8")&&patched.Contains("proxy_host: 127.0.0.1")&&ClashShortcut.Read(patched)=="Ctrl+Alt+Shift+F11";
        checks["existing_proxy_shortcut_is_not_replaced"]=ClashShortcut.Prepare(patched,"Ctrl+F9")==patched;
        checks["shortcut_parser_validates_chords"]=ClashShortcut.Parse("Ctrl+Alt+Shift+F11")==((uint)7,(byte)0x7A)&&ClashShortcut.Parse("F11")==null;

        return checks;
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
    public static object ColdLive()
    {
        var before=ClashProxy.ReadStatus();bool closed=false,enabled=false,recovered=false;string? error=null;double elapsed=0;
        try{
            string client=ClashProxy.ClientPath()??throw new IOException("找不到 Clash 客户端");
            if(before.Running){
                ClashProxy.SetEnabled(false);Thread.Sleep(400);
                foreach(var process in Process.GetProcessesByName("clash-verge"))using(process){
                    if(!string.Equals(process.MainModule?.FileName,client,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("客户端路径不一致，取消重启验证");
                    process.Kill();process.WaitForExit(5000);
                }
            }
            closed=!ClashProxy.ReadStatus().Running;var watch=Stopwatch.StartNew();
            enabled=ClashProxy.SetEnabled(true).Enabled==true;elapsed=watch.Elapsed.TotalSeconds;
        }catch(Exception e){error=e.Message;}
        finally{if(!enabled)try{recovered=ClashProxy.SetEnabled(before.Enabled??false).Enabled==(before.Enabled??false);}catch(Exception e){error=(error??"")+"；恢复失败："+e.Message;}}
        return new{ok=closed&&enabled,started_from_stopped=closed,enabled,elapsed_seconds=elapsed,recovered,error,ui=ClashProxy.InspectDetails()};
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
