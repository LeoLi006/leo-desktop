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
    public static object Live()
    {
        var before=ClashProxy.ReadStatus();bool changed=false,restored=false;string? error=null;
        if(!before.Running||before.Enabled==null)return new{ok=false,error="Clash 未运行或代理状态不明确，未进行切换"};
        try{changed=ClashProxy.SetEnabled(!before.Enabled.Value).Enabled==!before.Enabled.Value;}
        catch(Exception e){error=e.Message;}
        finally{try{restored=ClashProxy.SetEnabled(before.Enabled.Value).Enabled==before.Enabled.Value;}catch(Exception e){error=(error??"")+"；恢复状态失败："+e.Message;}}
        return new{ok=changed&&restored,changed,restored,error};
    }
}
