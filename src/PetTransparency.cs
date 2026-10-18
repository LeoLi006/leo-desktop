using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace LeoLauncher;

static class PetTransparency
{
    // A color-key window can remove only the exact key color. Composite animation
    // layers on transparent pixels first, then resolve coverage at the final size.
    // Otherwise even alpha=1 contaminates the magenta key and becomes visible.
    public static void ResolveCoverage(Bitmap frame)
    {
        var bits=frame.LockBits(new Rectangle(Point.Empty,frame.Size),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
        try{
            int stride=Math.Abs(bits.Stride);var data=new byte[stride*frame.Height];Marshal.Copy(bits.Scan0,data,0,data.Length);
            for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++){
                int at=y*stride+x*4;
                if(data[at+3]<128){data[at]=data[at+1]=data[at+2]=data[at+3]=0;}
                else data[at+3]=255;
            }
            Marshal.Copy(data,0,bits.Scan0,data.Length);
        }finally{frame.UnlockBits(bits);}
    }
}
