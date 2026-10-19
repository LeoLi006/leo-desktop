using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace LeoLauncher;

enum DockSide{None,Left,Right,Top,Bottom}
sealed class DockMotion
{
    public float Progress{get;private set;}
    long last=-1,lastInteraction,revealUntil;
    public void Reset(long now){Progress=0;last=now;lastInteraction=now;revealUntil=now+1000;}
    public void BeginTuck(long now){last=now;lastInteraction=now-2000;revealUntil=0;}
    public void Reveal(long now){lastInteraction=now;revealUntil=now+1000;}
    public void Tick(long now,bool docked,bool engaged)
    {
        if(last<0){Reset(now);return;}float delta=Math.Clamp(now-last,0,80)/900f;last=now;
        if(engaged)Reveal(now);
        bool tuck=docked&&!engaged&&now>revealUntil&&now-lastInteraction>=1400;
        Progress=Math.Clamp(Progress+(tuck?delta:-delta),0,1);
    }
    public static DockSide Detect(Rectangle pet,Rectangle area,int footer,DockSide preferred=DockSide.None)
    {
        if(preferred!=DockSide.None&&EdgeDock.ChooseSide(pet,area,footer,preferred is DockSide.Top or DockSide.Bottom?new Size(0,1):new Size(1,0),3)==preferred)return preferred;
        if(Math.Abs(pet.Left-area.Left)<=3)return DockSide.Left;
        if(Math.Abs(pet.Right-area.Right)<=3)return DockSide.Right;
        if(Math.Abs(pet.Top-area.Top)<=3)return DockSide.Top;
        return Math.Abs(pet.Bottom+footer-area.Bottom)<=3?DockSide.Bottom:DockSide.None;
    }
}

sealed class DockSprites:IDisposable
{
    readonly Bitmap[] frames;readonly Rectangle[] sources;readonly string? kit;
    public const int FramesPerDirection=6;
    public DockSprites(string? path=null,string? kit=null)
    {
        this.kit=kit;using var sheet=new Bitmap(path??Path.Combine(Store.Root,"assets","edge-motion-v1.png"));
        var locked=sheet.LockBits(new Rectangle(0,0,sheet.Width,sheet.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        var data=new byte[Math.Abs(locked.Stride)*sheet.Height];Marshal.Copy(locked.Scan0,data,0,data.Length);int stride=locked.Stride;sheet.UnlockBits(locked);
        var visited=new bool[sheet.Width*sheet.Height];var components=new List<(Rectangle box,List<int> pixels)>();
        for(int y=0;y<sheet.Height;y++)for(int x=0;x<sheet.Width;x++){
            int pixel=y*sheet.Width+x;if(visited[pixel]||data[y*stride+x*4+3]<=100)continue;
            var queue=new List<int>{pixel};visited[pixel]=true;int minX=x,maxX=x,minY=y,maxY=y;
            for(int n=0;n<queue.Count;n++){
                int current=queue[n],cx=current%sheet.Width,cy=current/sheet.Width;
                minX=Math.Min(minX,cx);maxX=Math.Max(maxX,cx);minY=Math.Min(minY,cy);maxY=Math.Max(maxY,cy);
                for(int direction=0;direction<4;direction++){
                    int nx=cx+(direction==0?-1:direction==1?1:0),ny=cy+(direction==2?-1:direction==3?1:0);
                    if(nx<0||nx>=sheet.Width||ny<0||ny>=sheet.Height)continue;
                    int next=ny*sheet.Width+nx;if(visited[next]||data[ny*stride+nx*4+3]<=100)continue;
                    visited[next]=true;queue.Add(next);
                }
            }
            if(queue.Count>1500)components.Add((Rectangle.FromLTRB(Math.Max(0,minX-1),Math.Max(0,minY-1),Math.Min(sheet.Width,maxX+2),Math.Min(sheet.Height,maxY+2)),queue));
        }
        if(components.Count!=24)throw new IOException("吸附动画必须包含四个方向各六帧");
        var ordered=components.OrderBy(c=>(c.box.Top+c.box.Height/2)/(sheet.Height/4)).ThenBy(c=>c.box.Left).ToArray();
        sources=ordered.Select(c=>c.box).ToArray();frames=new Bitmap[24];
        for(int i=0;i<24;i++){
            var (box,pixels)=ordered[i];var buffer=new byte[box.Width*box.Height*4];
            foreach(int pixel in pixels){int cx=pixel%sheet.Width,cy=pixel/sheet.Width;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){
                    int nx=cx+dx,ny=cy+dy;if(!box.Contains(nx,ny))continue;
                    Buffer.BlockCopy(data,ny*stride+nx*4,buffer,((ny-box.Top)*box.Width+nx-box.Left)*4,4);
                }
            }
            var image=new Bitmap(box.Width,box.Height,PixelFormat.Format32bppArgb);var target=image.LockBits(new Rectangle(Point.Empty,image.Size),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            Marshal.Copy(buffer,0,target.Scan0,buffer.Length);image.UnlockBits(target);frames[i]=image;
        }
        // Use one consistent inward-looking sequence for both horizontal edges.
        // Mirror the character before kit projection so sponsor lettering stays readable.
        for(int i=0;i<FramesPerDirection;i++){
            var mirrored=(Bitmap)frames[i].Clone();mirrored.RotateFlip(RotateFlipType.RotateNoneFlipX);
            frames[FramesPerDirection+i].Dispose();frames[FramesPerDirection+i]=mirrored;
            sources[FramesPerDirection+i]=new Rectangle(sources[FramesPerDirection+i].Location,mirrored.Size);
        }
    }
    internal bool RightGazeMatchesLeftMirror()
    {
        for(int i=0;i<FramesPerDirection;i++){using var expected=(Bitmap)frames[i].Clone();expected.RotateFlip(RotateFlipType.RotateNoneFlipX);if(SkinAnimationTests.Hash(expected)!=SkinAnimationTests.Hash(frames[FramesPerDirection+i]))return false;}return true;
    }
    public static int FrameIndex(float progress)=>Math.Clamp((int)(Math.Clamp(progress,0,1)*FramesPerDirection),0,FramesPerDirection-1);
    int Index(float progress,DockSide side)=>Math.Clamp((int)side-1,0,3)*FramesPerDirection+FrameIndex(progress);
    public Rectangle Source(float progress,long now,DockSide side=DockSide.Bottom)=>sources[Index(progress,side)];
    public RectangleF Destination(Size size,float progress,DockSide side,Rectangle source)
    {
        float inset=Math.Max(2,size.Width*.025f),scale=Math.Min((size.Width-inset*2)/sources.Max(r=>r.Width),(size.Height-inset*2)/sources.Max(r=>r.Height));
        scale=Math.Min(scale,size.Width*(138f/192)/sources.Max(r=>r.Width));
        float w=source.Width*scale,h=source.Height*scale,x=(size.Width-w)/2,y=size.Height-h-inset;
        float edgeX=side==DockSide.Left?0:side==DockSide.Right?size.Width-w:x;
        float edgeY=side==DockSide.Top?inset:side==DockSide.Bottom?size.Height-h-inset:(size.Height-h)/2;
        float t=progress*progress*(3-2*progress);return new(x+(edgeX-x)*t,y+(edgeY-y)*t,w,h);
    }
    public RectangleF Draw(Graphics graphics,Size size,float progress,DockSide side,long now,float opacity=1,float? positionProgress=null)
    {
        int index=Index(progress,side);var target=Destination(size,positionProgress??progress,side,sources[index]);if(kit!=null){KitPalette.Draw(graphics,frames[index],target,new RectangleF(Point.Empty,frames[index].Size),kit,opacity);return target;}
        using var alpha=new ImageAttributes();alpha.SetColorMatrix(new ColorMatrix{Matrix33=opacity});graphics.DrawImage(frames[index],Rectangle.Round(target),0,0,frames[index].Width,frames[index].Height,GraphicsUnit.Pixel,alpha);return target;
    }
    public void Dispose(){foreach(var frame in frames)frame.Dispose();}
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
    bool dockAwaitExit,dockClickReveal;
    void BeginDockTuck(){var side=DesktopDock.Detect(Bounds,foot.ReservedSpace,dockSide);if(!prefs.EdgeDocking||side==DockSide.None)return;movingDock=true;try{Location=DesktopDock.Snap(Bounds,DesktopDock.Area(Bounds,DesktopDock.Displays()),foot.ReservedSpace,side);}finally{movingDock=false;}dockSide=side;dockAwaitExit=true;dockMotion.BeginTuck(clock.ElapsedMilliseconds);}
    void StartDockAnimation(){if(previewMode)return;dockMotion.Reset(clock.ElapsedMilliseconds);dockTimer.Tick+=(_,_)=>UpdateDockAnimation();dockTimer.Start();}
    void RevealDock(bool immediate=false)
    {
        if(resourcesDisposed)return;
        dockMotion.Reveal(clock.ElapsedMilliseconds);
        if(!immediate)return;
        var shift=dockShift;dockShift=Point.Empty;movingDock=true;try{Location=new Point(Left-shift.X,Top-shift.Y);}finally{movingDock=false;}
        dockAwaitExit=false;dockMotion.Reset(clock.ElapsedMilliseconds);SyncDockHit();Invalidate();
        if(!hiddenForFullscreen&&IsHandleCreated){PositionFoot();foot.Opacity=UiTheme.WindowOpacity;if(!foot.Visible)foot.Show(this);}
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
        if(dockMotion.Progress>=DockBridge.End){var sprites=CurrentDock();var source=sprites.Source(DockProgress(dockMotion.Progress,dockSide),clock.ElapsedMilliseconds,dockSide);relative=Rectangle.Ceiling(sprites.Destination(ClientSize,dockMotion.Progress,dockSide,source));relative=Rectangle.Intersect(ClientRectangle,relative);}
        hit.Bounds=new Rectangle(new Point(Left+relative.Left,Top+relative.Top),relative.Size);
    }
    void UpdateDockAnimation()
    {
        if(resourcesDisposed||IsDisposed||Disposing||!IsHandleCreated||previewMode||hiddenForFullscreen)return;
        AdvanceDock(clock.ElapsedMilliseconds,Cursor.Position);
    }
    void AdvanceDock(long now,Point pointer)
    {
        var anchor=new Rectangle(new Point(Left-dockShift.X,Top-dockShift.Y),Size);var area=DesktopDock.Area(anchor,DesktopDock.Displays());
        var side=prefs.EdgeDocking?DesktopDock.Detect(anchor,foot.ReservedSpace,dockSide):DockSide.None;
        if(dockAwaitExit&&dockMotion.Progress>.995f&&!hit.Bounds.Contains(pointer))dockAwaitExit=false;
        bool engaged=!dockAwaitExit&&hit.Bounds.Contains(pointer)||foot.Visible&&(foot.Bounds.Contains(pointer)||foot.IsSearchOpen)||dragging||launcher.Visible||feedback?.IsBusy==true||ContextMenuStrip?.Visible==true;
        if(side!=DockSide.None){dockSide=side;var snapped=DesktopDock.Snap(anchor,area,foot.ReservedSpace,side);if(snapped!=anchor.Location){movingDock=true;try{Location=new Point(snapped.X+dockShift.X,snapped.Y+dockShift.Y);}finally{movingDock=false;}}}
        float before=dockMotion.Progress;dockMotion.Tick(now,side!=DockSide.None,engaged);
        int shiftY=dockSide==DockSide.Bottom?(int)Math.Round(dockMotion.Progress*foot.ReservedSpace):0;
        if(shiftY!=dockShift.Y){movingDock=true;try{Location=new Point(Left,Top+shiftY-dockShift.Y);dockShift=new Point(0,shiftY);}finally{movingDock=false;}}
        if(dockMotion.Progress>.001f){if(foot.Visible){foot.CollapseSearch();foot.Hide();}hoverCard?.Hide();feedback?.Hide();dockFootShownAt=0;}
        else if(!foot.Visible){foot.Opacity=0;PositionFoot();foot.Show(this);dockFootShownAt=now;}
        if(foot.Visible){foot.Opacity=UiTheme.WindowOpacity*(dockFootShownAt==0?1:Math.Clamp((now-dockFootShownAt)/180d,0,1));}
        SyncDockHit();if(dragPickup!=null)Invalidate();if(before!=dockMotion.Progress||dockMotion.Progress>.995f)Invalidate();
    }
    internal static float DockProgress(float progress,DockSide side)
    {
        float first=side is DockSide.Top or DockSide.Bottom?1f/6+.0001f:0;
        return first+(1-first)*Math.Clamp((progress-DockBridge.End)/(1-DockBridge.End),0,1);
    }
    bool DrawDockPet(Graphics graphics,RectangleF target)
    {
        if(dockMotion.Progress<=.001f)return false;
        target=new RectangleF(Point.Empty,ClientSize);var sprites=CurrentDock();var state=graphics.Save();graphics.TranslateTransform(target.Left,target.Top);
        graphics.SetClip(new Rectangle(Point.Empty,new Size((int)target.Width,(int)target.Height)));
        var size=new Size((int)target.Width,(int)target.Height);
        if(dockMotion.Progress<DockBridge.End)CurrentBridge().Draw(graphics,size,dockMotion.Progress,dockSide,sprites);
        else sprites.Draw(graphics,size,DockProgress(dockMotion.Progress,dockSide),dockSide,clock.ElapsedMilliseconds,positionProgress:dockMotion.Progress);
        graphics.Restore(state);return true;
    }
}
