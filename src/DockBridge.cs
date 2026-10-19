using System.Drawing.Imaging;
namespace LeoLauncher;

// Real inbetween poses, drawn individually at full opacity rather than ghosted together.
sealed class DockBridge:IDisposable
{
    public const float End=.42f;
    public const int Count=9;
    readonly Bitmap sheet,idle,mirroredSheet;readonly string? kit;
    readonly Rectangle[] sources=new Rectangle[32];
    readonly Rectangle idleSource;readonly Size referenceHead;readonly Size[] heads=new Size[32];
    public DockBridge(string path,Bitmap main,string? kit=null)
    {
        this.kit=kit;sheet=new(path);idle=new Bitmap(main.Width/8,main.Height/11);
        using(var g=Graphics.FromImage(idle))g.DrawImage(main,new Rectangle(Point.Empty,idle.Size),new Rectangle(Point.Empty,idle.Size),GraphicsUnit.Pixel);
        idleSource=Visible(idle,new Rectangle(Point.Empty,idle.Size));
        var parts=Parts(sheet);if(parts.Length!=32)throw new IOException("起步动画必须包含四个方向各八帧");
        Array.Copy(parts,sources,32);referenceHead=HeadGeometry.Measure(idle,new Rectangle(Point.Empty,idle.Size));for(int i=0;i<32;i++)heads[i]=HeadGeometry.Measure(sheet,sources[i]);
        mirroredSheet=(Bitmap)sheet.Clone();mirroredSheet.RotateFlip(RotateFlipType.RotateNoneFlipX);
    }
    static Rectangle[] Parts(Bitmap image)
    {
        var bits=image.LockBits(new Rectangle(Point.Empty,image.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        var data=new byte[Math.Abs(bits.Stride)*image.Height];int stride=Math.Abs(bits.Stride);System.Runtime.InteropServices.Marshal.Copy(bits.Scan0,data,0,data.Length);image.UnlockBits(bits);
        var seen=new bool[image.Width*image.Height];var parts=new List<Rectangle>();
        for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){
            int first=y*image.Width+x;if(seen[first]||data[y*stride+x*4+3]<=100)continue;
            var queue=new List<int>{first};seen[first]=true;int left=x,right=x,top=y,bottom=y;
            for(int n=0;n<queue.Count;n++){
                int at=queue[n],cx=at%image.Width,cy=at/image.Width;left=Math.Min(left,cx);right=Math.Max(right,cx);top=Math.Min(top,cy);bottom=Math.Max(bottom,cy);
                foreach(var (dx,dy) in new[]{(-1,0),(1,0),(0,-1),(0,1)}){int nx=cx+dx,ny=cy+dy;if(nx<0||ny<0||nx>=image.Width||ny>=image.Height)continue;int next=ny*image.Width+nx;if(seen[next]||data[ny*stride+nx*4+3]<=100)continue;seen[next]=true;queue.Add(next);}
            }
            if(queue.Count>1500)parts.Add(Rectangle.FromLTRB(left,top,right+1,bottom+1));
        }
        return parts.OrderBy(r=>(r.Top+r.Height/2)/(image.Height/4)).ThenBy(r=>r.Left).ToArray();
    }
    internal static Rectangle Visible(Bitmap image,Rectangle cell)
    {
        int left=cell.Right,top=cell.Bottom,right=cell.Left,bottom=cell.Top;
        for(int y=cell.Top;y<cell.Bottom;y++)for(int x=cell.Left;x<cell.Right;x++)if(image.GetPixel(x,y).A>100){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
        if(left>right||top>bottom)throw new IOException("动画格缺少可见内容");
        return Rectangle.FromLTRB(left,top,right+1,bottom+1);
    }
    public static int Frame(float progress)=>Math.Clamp((int)(progress/End*Count),0,Count-1);
    (Bitmap Image,Rectangle Source) Art(int frame,DockSide side)
    {
        if(frame==0)return(idle,idleSource);
        int direction=side==DockSide.Right?0:Math.Clamp((int)side,1,4)-1;var source=sources[direction*8+frame-1];
        return side==DockSide.Right?(mirroredSheet,new Rectangle(sheet.Width-source.Right,source.Top,source.Width,source.Height)):(sheet,source);
    }
    internal bool RightGazeMatchesLeftMirror()
    {
        for(int frame=1;frame<Count;frame++){
            var left=Art(frame,DockSide.Left);var right=Art(frame,DockSide.Right);
            using var expected=left.Image.Clone(left.Source,PixelFormat.Format32bppArgb);expected.RotateFlip(RotateFlipType.RotateNoneFlipX);
            using var actual=right.Image.Clone(right.Source,PixelFormat.Format32bppArgb);if(SkinAnimationTests.Hash(expected)!=SkinAnimationTests.Hash(actual))return false;
        }return true;
    }
    public RectangleF Draw(Graphics g,Size size,float progress,DockSide side,DockSprites dock)
    {
        int frame=Frame(progress);float t=Math.Clamp(progress/End,0,1);t=t*t*(3-2*t);
        var art=Art(frame,side);var source=art.Source;int direction=side==DockSide.Right?0:Math.Clamp((int)side,1,4)-1;
        var first=sources[direction*8];
        var last=sources[direction*8+7];
        var end=dock.Destination(size,End,side,dock.Source(PetForm.DockProgress(End,side),0,side));
        // Match original standing scale at entry, then arrive at the first tuck pose scale.
        float startScale=Math.Min(size.Width/(float)idle.Width,size.Height/(float)idle.Height);
        var start=new RectangleF(idleSource.X*startScale,(size.Height-idle.Height*startScale)+idleSource.Y*startScale,idleSource.Width*startScale,idleSource.Height*startScale);
        float sx=start.Width/first.Width+(end.Width/last.Width-start.Width/first.Width)*t;
        float sy=start.Height/first.Height+(end.Height/last.Height-start.Height/first.Height)*t;
        float uniform=frame==0?startScale:Math.Min(Math.Min(sx,sy),startScale*HeadGeometry.Fit(referenceHead,heads[direction*8+frame-1]));
        float w=source.Width*uniform,h=source.Height*uniform;
        float fit=Math.Min(1,Math.Min(size.Width*(138f/192)/w,size.Height/h));w*=fit;h*=fit;
        float cx=start.Left+start.Width/2+(end.Left+end.Width/2-start.Left-start.Width/2)*t;
        float bottom=start.Bottom+(end.Bottom-start.Bottom)*t;
        var target=new RectangleF(cx-w/2,bottom-h,w,h);
        // Keep ears inside the window, including the top-edge bending poses.
        target.X=Math.Clamp(target.X,0,Math.Max(0,size.Width-target.Width));target.Y=Math.Clamp(target.Y,0,Math.Max(0,size.Height-target.Height));
        if(kit!=null)KitPalette.Draw(g,art.Image,target,source,kit);
        else g.DrawImage(art.Image,target,source,GraphicsUnit.Pixel);return target;
    }
    public void Dispose(){sheet.Dispose();idle.Dispose();mirroredSheet.Dispose();}
}
