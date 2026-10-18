using System.Drawing.Imaging;
namespace LeoLauncher;
sealed partial class PetForm
{
    Size lastDragMovement;Bitmap? dragPickup;long pickupStarted;
    void DrawNormalPet(Graphics graphics,RectangleF target,float opacity)
    {
        DrawAnimationFrame(graphics,target,opacity);
    }
    void BeginDragPickup()
    {
        lastDragMovement=Size.Empty;if(dockMotion.Progress<=.001f)return;
        var previous=dragPickup;dragPickup=null;previous?.Dispose();var snapshot=new Bitmap(Width,Height);using(var g=Graphics.FromImage(snapshot))DrawPet(g,PetDrawingBounds(ClientSize));dragPickup=snapshot;pickupStarted=clock.ElapsedMilliseconds;
    }
    bool DrawDragPickup(Graphics graphics,RectangleF target)
    {
        if(dragPickup==null)return false;float t=Math.Clamp((clock.ElapsedMilliseconds-pickupStarted)/160f,0,1);
        if(t>=1){var expired=dragPickup;dragPickup=null;expired.Dispose();return false;}
        DrawNormalPet(graphics,target,t);using var attributes=new ImageAttributes();attributes.SetColorMatrix(new ColorMatrix{Matrix33=1-t});
        graphics.DrawImage(dragPickup,Rectangle.Round(target),0,0,dragPickup.Width,dragPickup.Height,GraphicsUnit.Pixel,attributes);return true;
    }
    internal Dictionary<string,bool> VerifyDockPolish()
    {
        var checks=new Dictionary<string,bool>();RevealDock(true);dockSide=DockSide.Left;dockMotion.BeginTuck(0);for(int t=32;t<=650;t+=32)dockMotion.Tick(t,true,false);
        dockMotion.Reveal(650);dockMotion.Tick(682,true,true);float partial=dockMotion.Progress;BeginDragPickup();bool captured=dragPickup!=null;RevealDock(true);
        using var image=new Bitmap(Width,Height);using var g=Graphics.FromImage(image);DrawPet(g,PetDrawingBounds(ClientSize));checks["unfolding_drag_captures_and_blends_current_pose"]=partial>0&&partial<1&&captured&&dragPickup!=null&&dockMotion.Progress==0;
        pickupStarted=clock.ElapsedMilliseconds-170;DrawPet(g,PetDrawingBounds(ClientSize));checks["drag_pickup_expires_without_loop_or_disposed_image"]=dragPickup==null;
        var area=Screen.FromControl(this).WorkingArea;RevealDock(true);Location=new Point(area.Left,area.Top+Height);dockMotion.Reset(0);feedback=new LeoToast(()=>Bounds);feedback.Present(new("正在等待测试客户端…",Busy:true));
        for(int t=0;t<=2400;t+=32)AdvanceDock(t,new Point(area.Right-3,area.Bottom-3));checks["busy_operation_keeps_feedback_and_cancel_accessible"]=dockMotion.Progress==0&&feedback.Visible;
        feedback.Dispose();feedback=null;return checks;
    }
}
