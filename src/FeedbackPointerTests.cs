using System.Runtime.InteropServices;
using System.Text.Json;
namespace LeoLauncher;

static class FeedbackPointerTests
{
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
    internal static void Mouse(HitSurface surface,bool down,Point point)=>SendMessage(surface.Handle,down?0x201:0x202,down?(IntPtr)1:IntPtr.Zero,(IntPtr)((point.Y<<16)|(point.X&0xffff)));
    public static void Run(string report)
    {
        try{
            string folder=Path.GetDirectoryName(report)!;Directory.CreateDirectory(folder);var checks=new Dictionary<string,bool>();
            using(var toast=new LeoToast(()=>new Rectangle(600,400,136,148))){
                string[] texts=["系统代理已开启","Clash Verge 已启动，系统代理已成功开启。","系统代理已关闭","正在等待 Clash Verge 启动并确认系统代理状态…",new string('测',160),"系统代理已开启"];
                bool fits=true,regions=true;int compact=0;
                for(int round=0;round<3;round++)for(int i=0;i<texts.Length;i++){
                    toast.Present(new(texts[i],Error:i==4,Retry:i==4?()=>Task.CompletedTask:null,Busy:i==3));Application.DoEvents();UiTheme.Attach(toast);Application.DoEvents();
                    fits&=toast.PreviewTextFits;regions&=toast.Region!.IsVisible(new Point(toast.Width-14,toast.Height-14));if(i==0)compact=toast.Height;
                    if(round==0&&i is 1 or 4){using var image=new Bitmap(toast.Width,toast.Height);toast.DrawToBitmap(image,toast.ClientRectangle);image.Save(Path.Combine(folder,i==1?"clash-success.png":"clash-long-error.png"));}
                }
                checks["short_long_busy_and_retry_text_fit_after_repeated_resize"]=fits;
                checks["rounded_window_region_tracks_every_size_change"]=regions;
                checks["short_success_returns_to_compact_size"]=toast.Height==compact&&compact<=58;
                foreach(string skin in new[]{"202","164","20232","382","83"}){UiTheme.Configure(skin,"Leo");toast.Present(new("Clash 系统代理已成功开启，现可继续使用。"));Application.DoEvents();fits&=toast.PreviewTextFits;}
                checks["feedback_layout_survives_team_theme_changes"]=fits;toast.Close();
            }
            using(var pet=new PetForm(new Preferences{ApplicationIndexing=false,HideInFullscreen=false},true)){
                pet.Show();Application.DoEvents();foreach(var pair in pet.VerifyNoticePointer(folder))checks[pair.Key]=pair.Value;pet.Close();
            }
            File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));
        }catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}
    }
}

sealed partial class PetForm
{
    internal Dictionary<string,bool> VerifyNoticePointer(string folder)
    {
        var checks=new Dictionary<string,bool>();notices.Add("系统代理已成功开启");
        foreach(int size in new[]{80,136,230}){
            prefs.PetSize=size;ResizePet();RevealDock(true);var before=Location;int opens=launcherOpens;int oldRow=row,oldFrame=frame;var badge=new Point(Width-18,18);
            FeedbackPointerTests.Mouse(hit,true,badge);
            checks[size+"_badge_press_does_not_open_window_or_start_drag"]=noticePress&&pointerPressed&&hit.Capture&&noticeWindow?.Visible!=true&&!dragging&&row==oldRow&&frame==oldFrame;
            // Native mouse-up tests the actual WinForms capture-release ordering.
            FeedbackPointerTests.Mouse(hit,false,badge);Application.DoEvents();
            checks[size+"_badge_release_opens_only_inbox_without_moving_pet"]=noticeWindow?.Visible==true&&launcherOpens==opens&&Location==before&&!pointerPressed&&!noticePress&&!hit.Capture&&!dragging&&row==oldRow;
            checks[size+"_native_release_opens_inbox"]=noticeWindow?.Visible==true;
            checks[size+"_native_release_keeps_pet_position"]=Location==before;
            checks[size+"_native_release_clears_capture"]=!hit.Capture&&!pointerPressed&&!noticePress;
            using(var image=RenderPetSurface())image.Save(Path.Combine(folder,"notification-button-"+size+".png"));noticeWindow?.Close();
            // Press near the outer edge: the target is larger than the painted icon.
            hit.SimulateClick(new Point(Width-35,32));checks[size+"_badge_padding_is_clickable"]=noticeWindow?.Visible==true&&launcherOpens==opens&&Location==before;noticeWindow?.Close();
        }
        var position=Location;int count=launcherOpens;var local=new Point(Width-18,18);
        FeedbackPointerTests.Mouse(hit,true,local);hit.Capture=false;FeedbackPointerTests.Mouse(hit,false,local);
        checks["lost_capture_cancels_badge_without_late_launcher_click"]=!pointerPressed&&!noticePress&&!dragging&&noticeWindow?.Visible!=true&&launcherOpens==count&&Location==position;
        FeedbackPointerTests.Mouse(hit,true,local);FeedbackPointerTests.Mouse(hit,false,new Point(Width/2,Height/2));
        checks["releasing_outside_badge_cancels_without_toggling_launcher"]=noticeWindow?.Visible!=true&&launcherOpens==count&&Location==position&&!hit.Capture;
        hit.SimulateClick(new Point(Width/2,Height/2));checks["normal_pet_click_still_opens_search"]=launcher.Visible&&launcherOpens==count+1;
        hit.SimulateClick(new Point(Width/2,Height/2));checks["normal_pet_click_still_closes_search"]=!launcher.Visible&&launcherOpens==count+1;
        row=frame=0;position=Location;BeginPetPress(hit.PointToScreen(new Point(Width/2,Height/2)));ApplyDrag(new Size(-20,0));hit.Capture=false;
        checks["interrupted_body_drag_resets_motion_and_pending_press"]=!pointerPressed&&!dragging&&row==0&&frame==0&&dragPickup==null&&!hit.Capture;
        Location=position;RevealDock(true);row=frame=0;notices.ReadAll();checks["empty_badge_target_disappears_after_read_all"]=NoticeHitBounds.IsEmpty;
        return checks;
    }
}
