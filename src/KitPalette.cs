using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
namespace LeoLauncher;

// Real jersey texture projection, preserving the default character geometry and alpha.
static class KitPalette
{
    static bool Red(Color p)=>p.R>95&&p.G<90&&p.R>p.G*2.5&&p.R>p.B*1.5&&p.B>p.G*.7;
    public static void Draw(Graphics g,Bitmap image,RectangleF target,RectangleF source,string name,float opacity=1)
    {
        var bounds=Rectangle.Round(target);if(bounds.Width<=0||bounds.Height<=0)return;var texture=KitTexture.Load(name);
        // Project on the native animation cell before the only final-size resample.
        using var surface=new Bitmap(Math.Max(1,(int)Math.Round(source.Width)),Math.Max(1,(int)Math.Round(source.Height)),PixelFormat.Format32bppArgb);
        using(var drawing=Graphics.FromImage(surface)){drawing.InterpolationMode=InterpolationMode.HighQualityBicubic;drawing.DrawImage(image,new Rectangle(Point.Empty,surface.Size),source,GraphicsUnit.Pixel);}
        var bits=surface.LockBits(new Rectangle(Point.Empty,surface.Size),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
        try{
            int stride=Math.Abs(bits.Stride);var pixels=new byte[stride*surface.Height];Marshal.Copy(bits.Scan0,pixels,0,pixels.Length);
            var rowCounts=new int[surface.Height];
            for(int y=0;y<surface.Height;y++)for(int x=0;x<surface.Width;x++){int n=y*stride+x*4;if(pixels[n+3]>100&&Red(Color.FromArgb(pixels[n+3],pixels[n+2],pixels[n+1],pixels[n])))rowCounts[y]++;}
            // Mouths may share a burgundy pixel with the shirt. Only the dominant
            // contiguous fabric band defines the jersey, never isolated facial details.
            int best=0,top=0,bottom=-1,start=0,last=-10,total=0;
            for(int y=0;y<surface.Height;y++)if(rowCounts[y]>0){
                if(y-last>3){start=y;total=0;}total+=rowCounts[y];last=y;
                if(total>best){best=total;top=start;bottom=y;}
            }
            bool hasJersey=best>=Math.Max(6,surface.Width*surface.Height*.0008f);
            int left=surface.Width,right=-1;
            if(hasJersey)for(int y=top;y<=bottom;y++)for(int x=0;x<surface.Width;x++){int n=y*stride+x*4;if(pixels[n+3]>100&&Red(Color.FromArgb(pixels[n+3],pixels[n+2],pixels[n+1],pixels[n]))){left=Math.Min(left,x);right=Math.Max(right,x);}}
            long woolY=0,woolCount=0;
            for(int y=0;y<surface.Height;y++)for(int x=0;x<surface.Width;x++){int n=y*stride+x*4;if(pixels[n+3]>128&&pixels[n+2]>190&&pixels[n+1]>170&&pixels[n]>120){woolY+=y;woolCount++;}}
            bool upside=woolCount>0&&woolY/(float)woolCount>(top+bottom)/2f;
            int jerseyHeight=Math.Max(1,bottom-top+1),jerseyWidth=Math.Max(1,right-left+1);
            int pad=Math.Max(1,jerseyWidth/12);var detail=texture.Detail(jerseyWidth+pad*2,jerseyHeight);
            for(int y=0;y<surface.Height;y++)for(int x=0;x<surface.Width;x++){
                int n=y*stride+x*4;if(pixels[n+3]==0)continue;var original=Color.FromArgb(pixels[n+3],pixels[n+2],pixels[n+1],pixels[n]);var p=original;
                bool fabric=hasJersey&&KitTexture.Fabric(original),horizontal=x>=left-pad&&x<=right+pad;
                if(fabric&&horizontal&&y>=top&&y<=bottom){
                    float u=(x-left+pad)/(float)(jerseyWidth+pad*2),v=(y-top)/(float)jerseyHeight;if(upside){u=1-u;v=1-v;}
                    p=KitTexture.Shade(detail.Sample(u,v),original);
                }else if(fabric&&horizontal&&(upside?y<top&&y>=top-jerseyHeight*.38f:y>bottom&&y<=bottom+jerseyHeight*.38f))p=KitTexture.Shorts(original,name);
                pixels[n]=p.B;pixels[n+1]=p.G;pixels[n+2]=p.R;pixels[n+3]=(byte)(p.A*opacity);
            }
            Marshal.Copy(pixels,0,bits.Scan0,pixels.Length);
        }finally{surface.UnlockBits(bits);}
        var saved=g.Save();g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        g.DrawImage(surface,bounds,0,0,surface.Width,surface.Height,GraphicsUnit.Pixel);g.Restore(saved);
    }
}
