using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace LeoLauncher;

enum DockSide{None,Left,Right,Top,Bottom}
sealed class DockMotion
{
    public float Progress{get;private set;}
    long last=-1,lastInteraction,revealUntil;
    public void Reset(long now){Progress=0;last=now;lastInteraction=now;revealUntil=now+1000;}
    public void Reveal(long now){lastInteraction=now;revealUntil=now+1000;}
    public void Tick(long now,bool docked,bool engaged)
    {
        if(last<0){Reset(now);return;}float delta=Math.Clamp(now-last,0,80)/480f;last=now;
        if(engaged)Reveal(now);
        bool tuck=docked&&!engaged&&now>revealUntil&&now-lastInteraction>=1400;
        Progress=Math.Clamp(Progress+(tuck?delta:-delta),0,1);
    }
    public static DockSide Detect(Rectangle pet,Rectangle area,int footer)
    {
        if(Math.Abs(pet.Left-area.Left)<=3)return DockSide.Left;
        if(Math.Abs(pet.Right-area.Right)<=3)return DockSide.Right;
        if(Math.Abs(pet.Top-area.Top)<=3)return DockSide.Top;
        return Math.Abs(pet.Bottom+footer-area.Bottom)<=3?DockSide.Bottom:DockSide.None;
    }
}

sealed class DockSprites:IDisposable
{
    readonly Bitmap sheet;readonly Rectangle[] cells=new Rectangle[6];readonly Rectangle[] heads=new Rectangle[6];
    public DockSprites()
    {
        sheet=new Bitmap(Path.Combine(Store.Root,"assets","edge-hide-v2.png"));
        var locked=sheet.LockBits(new Rectangle(0,0,sheet.Width,sheet.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        var data=new byte[Math.Abs(locked.Stride)*sheet.Height];Marshal.Copy(locked.Scan0,data,0,data.Length);int stride=locked.Stride;sheet.UnlockBits(locked);
        for(int i=0;i<6;i++){
            int left=i*sheet.Width/6,right=(i+1)*sheet.Width/6,minX=right,minY=sheet.Height,maxX=left,maxY=0;
            for(int y=0;y<sheet.Height;y++)for(int x=left;x<right;x++)if(data[y*stride+x*4+3]>32){minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
            cells[i]=Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1);heads[i]=cells[i];

        }
    }
    public Rectangle Source(float progress,long now)=>progress>=.995f?heads[now%4000>3650?5:4]:cells[Math.Clamp((int)(progress*5),0,4)];
    public RectangleF Destination(Size size,float progress,DockSide side,Rectangle source)
    {
        float scale=size.Width*.80f/cells.Max(r=>r.Width),w=source.Width*scale,h=source.Height*scale;
        float x=(size.Width-w)/2,y=size.Height-h-size.Height*.035f;
        float edgeX=side==DockSide.Left?0:side==DockSide.Right?size.Width-w:x;
        float edgeY=side==DockSide.Top?0:side==DockSide.Bottom?size.Height-h:(size.Height-h)/2;
        float t=progress*progress*(3-2*progress);return new(x+(edgeX-x)*t,y+(edgeY-y)*t,w,h);
    }
    public RectangleF Draw(Graphics graphics,Size size,float progress,DockSide side,long now)
    {
        var source=Source(progress,now);var target=Destination(size,progress,side,source);graphics.DrawImage(sheet,target,source,GraphicsUnit.Pixel);return target;
    }
    public void Dispose()=>sheet.Dispose();
}

sealed partial class PetForm
{
    readonly DockMotion dockMotion=new();
    readonly System.Windows.Forms.Timer dockTimer=new(){Interval=32};
    DockSprites? dockSprites;
    DockSide dockSide;
    Point dockShift;
    bool movingDock;
    long dockFootShownAt;
    void StartDockAnimation(){if(previewMode)return;dockMotion.Reset(clock.ElapsedMilliseconds);dockTimer.Tick+=(_,_)=>UpdateDockAnimation();dockTimer.Start();}
    void RevealDock(bool immediate=false)
    {
        if(resourcesDisposed)return;
        dockMotion.Reveal(clock.ElapsedMilliseconds);
        if(!immediate)return;
        var shift=dockShift;dockShift=Point.Empty;movingDock=true;try{Location=new Point(Left-shift.X,Top-shift.Y);}finally{movingDock=false;}
        dockMotion.Reset(clock.ElapsedMilliseconds);SyncDockHit();Invalidate();
        if(!hiddenForFullscreen&&IsHandleCreated){PositionFoot();foot.Opacity=1;if(!foot.Visible)foot.Show(this);}
    }
    void OnDockLocationChanged()
    {
        if(!movingDock){dockShift=Point.Empty;dockMotion.Reset(clock.ElapsedMilliseconds);}
        SyncDockHit();PositionFoot();feedback?.Follow();hoverCard?.Hide();
    }
    void SyncDockHit()
    {
        if(hit==null||hit.IsDisposed)return;
        Rectangle relative=ClientRectangle;
        if(dockMotion.Progress>.01f){dockSprites??=new();var source=dockSprites.Source(dockMotion.Progress,clock.ElapsedMilliseconds);relative=Rectangle.Ceiling(dockSprites.Destination(ClientSize,dockMotion.Progress,dockSide,source));relative=Rectangle.Intersect(ClientRectangle,relative);}
        hit.Bounds=new Rectangle(new Point(Left+relative.Left,Top+relative.Top),relative.Size);
    }
    void UpdateDockAnimation()
    {
        if(resourcesDisposed||IsDisposed||Disposing||!IsHandleCreated||previewMode||hiddenForFullscreen)return;
        AdvanceDock(clock.ElapsedMilliseconds,Cursor.Position);
    }
    void AdvanceDock(long now,Point pointer)
    {
        var anchor=new Rectangle(new Point(Left-dockShift.X,Top-dockShift.Y),Size);var area=Screen.FromRectangle(anchor).WorkingArea;
        var side=prefs.EdgeDocking?DockMotion.Detect(anchor,area,foot.ReservedSpace):DockSide.None;
        bool engaged=hit.Bounds.Contains(pointer)||foot.Visible&&(foot.Bounds.Contains(pointer)||foot.IsSearchOpen)||dragging||launcher.Visible||ContextMenuStrip?.Visible==true;
        if(side!=DockSide.None)dockSide=side;
        float before=dockMotion.Progress;dockMotion.Tick(now,side!=DockSide.None,engaged);
        int shiftY=dockSide==DockSide.Bottom?(int)Math.Round(dockMotion.Progress*foot.ReservedSpace):0;
        if(shiftY!=dockShift.Y){movingDock=true;try{Location=new Point(Left,Top+shiftY-dockShift.Y);dockShift=new Point(0,shiftY);}finally{movingDock=false;}}
        if(dockMotion.Progress>.001f){if(foot.Visible){foot.CollapseSearch();foot.Hide();}hoverCard?.Hide();feedback?.Hide();dockFootShownAt=0;}
        else if(!foot.Visible){foot.Opacity=0;PositionFoot();foot.Show(this);dockFootShownAt=now;}
        if(foot.Visible){foot.Opacity=dockFootShownAt==0?1:Math.Clamp((now-dockFootShownAt)/180d,0,1);}
        SyncDockHit();if(before!=dockMotion.Progress||dockMotion.Progress>.995f)Invalidate();
    }
    bool DrawDockPet(Graphics graphics,RectangleF target)
    {
        if(dockMotion.Progress<=.001f)return false;
        dockSprites??=new();var state=graphics.Save();graphics.TranslateTransform(target.Left,target.Top);
        dockSprites.Draw(graphics,new Size((int)target.Width,(int)target.Height),dockMotion.Progress,dockSide,clock.ElapsedMilliseconds);graphics.Restore(state);return true;
    }
}
