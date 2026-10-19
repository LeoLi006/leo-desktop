using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace LeoLauncher;

static class PetTransparency
{
    // Keep antialiasing coverage for the per-pixel alpha window. Remove only nearly
    // invisible background noise, never threshold the character edge to opaque.
    public static void ResolveCoverage(Bitmap frame)
    {
        var bits=frame.LockBits(new Rectangle(Point.Empty,frame.Size),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
        try{
            int stride=Math.Abs(bits.Stride),length=stride*frame.Height;var data=System.Buffers.ArrayPool<byte>.Shared.Rent(length);
            try{Marshal.Copy(bits.Scan0,data,0,length);
            for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++){
                int at=y*stride+x*4;
                if(data[at+3]<8){data[at]=data[at+1]=data[at+2]=data[at+3]=0;}
            }
            Marshal.Copy(data,0,bits.Scan0,length);
            }finally{System.Buffers.ArrayPool<byte>.Shared.Return(data);}
        }finally{frame.UnlockBits(bits);}
    }
}
