using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
namespace LeoLauncher;

// Runtime jersey shader: every team uses the default artwork's exact poses and alpha.
static class KitPalette
{
    static bool Red(Color p)=>p.R>95&&p.G<90&&p.R>p.G*2.5&&p.R>p.B*1.5&&p.B>p.G*.7;
    internal static Color Replace(Color p,string name)
    {
        bool red=Red(p);
        bool blue=p.B>80&&p.B>p.R*1.5&&p.B>p.G*1.35;
        if(!red&&!blue)return p;
        var color=(name,red) switch{
            ("city",true)=>Color.FromArgb(105,183,222),("city",false)=>Color.FromArgb(125,197,230),
            ("miami",true)=>Color.FromArgb(246,162,191),("miami",false)=>Color.FromArgb(225,130,163),
            ("argentina",true)=>Color.FromArgb(242,242,238),("argentina",false)=>Color.FromArgb(107,187,226),
            ("spain",true)=>Color.FromArgb(208,35,47),("spain",false)=>Color.FromArgb(175,29,42),_=>p};
        // Retain the source fabric's highlights and folds, leaving wool and outlines alone.
        float light=Math.Clamp((red?p.R/195f:p.B/150f),.35f,1.18f);
        return Color.FromArgb(p.A,Math.Min(255,(int)(color.R*light)),Math.Min(255,(int)(color.G*light)),Math.Min(255,(int)(color.B*light)));
    }
    public static void Draw(Graphics g,Bitmap image,RectangleF target,RectangleF source,string name,float opacity=1)
    {
        var bounds=Rectangle.Round(target);if(bounds.Width<=0||bounds.Height<=0)return;
        using var surface=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb);
        using(var drawing=Graphics.FromImage(surface)){drawing.InterpolationMode=InterpolationMode.HighQualityBicubic;drawing.DrawImage(image,new Rectangle(Point.Empty,surface.Size),source,GraphicsUnit.Pixel);}
        var bits=surface.LockBits(new Rectangle(Point.Empty,surface.Size),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
        try{
            int stride=Math.Abs(bits.Stride);var pixels=new byte[stride*surface.Height];Marshal.Copy(bits.Scan0,pixels,0,pixels.Length);
            int left=surface.Width,right=-1,top=surface.Height,bottom=-1;
            for(int y=0;y<surface.Height;y++)for(int x=0;x<surface.Width;x++){
                int n=y*stride+x*4;if(pixels[n+3]>100&&Red(Color.FromArgb(pixels[n+3],pixels[n+2],pixels[n+1],pixels[n]))){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            }
            for(int y=0;y<surface.Height;y++)for(int x=0;x<surface.Width;x++){
                int n=y*stride+x*4;if(pixels[n+3]==0)continue;var original=Color.FromArgb(pixels[n+3],pixels[n+2],pixels[n+1],pixels[n]);
                // Blue outside the jersey belongs to gloves, shorts or boots.
                var p=Red(original)||x>=left&&x<=right&&y>=top&&y<=bottom?Replace(original,name):original;
                pixels[n]=p.B;pixels[n+1]=p.G;pixels[n+2]=p.R;pixels[n+3]=(byte)(p.A*opacity);
            }
            Marshal.Copy(pixels,0,bits.Scan0,pixels.Length);
        }finally{surface.UnlockBits(bits);}
        g.DrawImageUnscaled(surface,bounds.Location);
    }
}
