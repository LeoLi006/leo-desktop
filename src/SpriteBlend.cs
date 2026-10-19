using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace LeoLauncher;
static class SpriteBlend
{
    // Interpolate premultiplied pixels so neither a translucent dip nor a leftover silhouette appears.
    public static void Mix(Bitmap first,Bitmap second,float weight){var region=new Rectangle(Point.Empty,first.Size);var a=first.LockBits(region,ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);try{var b=second.LockBits(region,ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);try{int length=Math.Abs(a.Stride)*first.Height;var left=System.Buffers.ArrayPool<byte>.Shared.Rent(length);var right=System.Buffers.ArrayPool<byte>.Shared.Rent(length);try{Marshal.Copy(a.Scan0,left,0,length);Marshal.Copy(b.Scan0,right,0,length);for(int i=0;i<length;i++)left[i]=(byte)Math.Clamp((int)Math.Round(left[i]*(1-weight)+right[i]*weight),0,255);Marshal.Copy(left,0,a.Scan0,length);}finally{System.Buffers.ArrayPool<byte>.Shared.Return(left);System.Buffers.ArrayPool<byte>.Shared.Return(right);}}finally{second.UnlockBits(b);}}finally{first.UnlockBits(a);}}
}
