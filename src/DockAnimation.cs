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
        if(last<0){Reset(now);return;}float delta=Math.Clamp(now-last,0,80)/760f;last=now;
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
    readonly Bitmap sheet;readonly Rectangle[] cells=new Rectangle[4];
    public DockSprites()
    {
        sheet=new Bitmap(Path.Combine(Store.Root,"assets","edge-directions-v3.png"));
        var locked=sheet.LockBits(new Rectangle(0,0,sheet.Width,sheet.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        var data=new byte[Math.Abs(locked.Stride)*sheet.Height];Marshal.Copy(locked.Scan0,data,0,data.Length);int stride=locked.Stride;sheet.UnlockBits(locked);
        var visited=new bool[sheet.Width*sheet.Height];
        for(int i=0;i<4;i++){
            int left=(i%2)*sheet.Width/2,right=(i%2+1)*sheet.Width/2,top=(i/2)*sheet.Height/2,bottom=(i/2+1)*sheet.Height/2,best=0;
            for(int y=top;y<bottom;y++)for(int x=left;x<right;x++){
                int pixel=y*sheet.Width+x;if(visited[pixel]||data[y*stride+x*4+3]<=32)continue;
                var queue=new List<int>{pixel};visited[pixel]=true;int minX=x,maxX=x,minY=y,maxY=y;
                for(int n=0;n<queue.Count;n++){
                    int current=queue[n],cx=current%sheet.Width,cy=current/sheet.Width;
                    minX=Math.Min(minX,cx);maxX=Math.Max(maxX,cx);minY=Math.Min(minY,cy);maxY=Math.Max(maxY,cy);
                    for(int direction=0;direction<4;direction++){
                        int nx=cx+(direction==0?-1:direction==1?1:0),ny=cy+(direction==2?-1:direction==3?1:0);
                        if(nx<left||nx>=right||ny<top||ny>=bottom)continue;
                        int next=ny*sheet.Width+nx;if(visited[next]||data[ny*stride+nx*4+3]<=32)continue;
                        visited[next]=true;queue.Add(next);
                    }
                }
                if(queue.Count>best){best=queue.Count;cells[i]=Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1);}
            }
        }
    }
    public Rectangle Source(float progress,long now,DockSide side=DockSide.Bottom)=>cells[Math.Clamp((int)side-1,0,3)];
    public RectangleF Destination(Size size,float progress,DockSide side,Rectangle source)
    {
        float inset=Math.Max(2,size.Width*.025f);
        float w=size.Width*.72f,h=w*source.Height/source.Width;
        if(h>size.Height-inset*2){h=size.Height-inset*2;w=h*source.Width/source.Height;}
        float x=(size.Width-w)/2,y=(size.Height-h)/2;
        float edgeX=side==DockSide.Left?inset:side==DockSide.Right?size.Width-w-inset:x;
        float edgeY=side==DockSide.Top?inset:side==DockSide.Bottom?size.Height-h-inset:y;
        float t=progress*progress*(3-2*progress);return new(x+(edgeX-x)*t,y+(edgeY-y)*t,w,h);
    }
    public static float HeadOpacity(float progress){float t=Math.Clamp((progress-.22f)/.55f,0,1);return t*t*(3-2*t);}
    public RectangleF Draw(Graphics graphics,Size size,float progress,DockSide side,long now)
    {
        var source=Source(progress,now,side);var target=Destination(size,progress,side,source);
        using var attributes=new ImageAttributes();attributes.SetColorMatrix(new ColorMatrix{Matrix33=HeadOpacity(progress)});
        graphics.DrawImage(sheet,Rectangle.Round(target),source.X,source.Y,source.Width,source.Height,GraphicsUnit.Pixel,attributes);return target;
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
    bool dockAwaitExit,dockClickReveal;
    void BeginDockTuck(){var side=DockMotion.Detect(Bounds,Screen.FromControl(this).WorkingArea,foot.ReservedSpace);if(!prefs.EdgeDocking||side==DockSide.None)return;dockSide=side;dockAwaitExit=true;dockMotion.BeginTuck(clock.ElapsedMilliseconds);}
    void StartDockAnimation(){if(previewMode)return;dockMotion.Reset(clock.ElapsedMilliseconds);dockTimer.Tick+=(_,_)=>UpdateDockAnimation();dockTimer.Start();}
    void RevealDock(bool immediate=false)
    {
        if(resourcesDisposed)return;
        dockMotion.Reveal(clock.ElapsedMilliseconds);
        if(!immediate)return;
        var shift=dockShift;dockShift=Point.Empty;movingDock=true;try{Location=new Point(Left-shift.X,Top-shift.Y);}finally{movingDock=false;}
        dockAwaitExit=false;dockMotion.Reset(clock.ElapsedMilliseconds);SyncDockHit();Invalidate();
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
        if(dockMotion.Progress>.01f){dockSprites??=new();var source=dockSprites.Source(dockMotion.Progress,clock.ElapsedMilliseconds,dockSide);relative=Rectangle.Ceiling(dockSprites.Destination(ClientSize,dockMotion.Progress,dockSide,source));relative=Rectangle.Intersect(ClientRectangle,relative);}
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
        if(dockAwaitExit&&dockMotion.Progress>.995f&&!hit.Bounds.Contains(pointer))dockAwaitExit=false;
        bool engaged=!dockAwaitExit&&hit.Bounds.Contains(pointer)||foot.Visible&&(foot.Bounds.Contains(pointer)||foot.IsSearchOpen)||dragging||launcher.Visible||ContextMenuStrip?.Visible==true;
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
        float t=dockMotion.Progress*dockMotion.Progress*(3-2*dockMotion.Progress)*.28f;
        var normal=new Rectangle(0,0,(int)target.Width,(int)target.Height);
        normal.Offset(dockSide==DockSide.Left?-(int)(target.Width*t):dockSide==DockSide.Right?(int)(target.Width*t):0,dockSide==DockSide.Top?-(int)(target.Height*t):dockSide==DockSide.Bottom?(int)(target.Height*t):0);
        graphics.SetClip(new Rectangle(Point.Empty,normal.Size));
        using(var attributes=new ImageAttributes()){attributes.SetColorMatrix(new ColorMatrix{Matrix33=1-DockSprites.HeadOpacity(dockMotion.Progress)});graphics.DrawImage(atlas,normal,frame*192,row*208,192,208,GraphicsUnit.Pixel,attributes);}
        dockSprites.Draw(graphics,new Size((int)target.Width,(int)target.Height),dockMotion.Progress,dockSide,clock.ElapsedMilliseconds);graphics.Restore(state);return true;
    }
}
