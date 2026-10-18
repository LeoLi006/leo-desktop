using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace LeoLauncher;

static class LayeredPet
{
    internal const int Style=0x80000;
    [StructLayout(LayoutKind.Sequential)] struct Info {public uint Size;public int Width,Height;public ushort Planes,Bits;public uint Compression,ImageSize;public int X,Y;public uint Used,Important;}
    [StructLayout(LayoutKind.Sequential,Pack=1)] struct Blend {public byte Operation,Flags,Opacity,Format;}
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll",SetLastError=true)] static extern IntPtr CreateDIBSection(IntPtr dc,ref Info info,uint usage,out IntPtr pixels,IntPtr section,uint offset);
    [DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr window,IntPtr screen,ref Point position,ref Size size,IntPtr memory,ref Point origin,uint key,ref Blend blend,uint flags);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int GetStyle(IntPtr window,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")] static extern int SetStyle(IntPtr window,int index,int style);
    public static void Present(IntPtr window,Point position,Bitmap frame)
    {
        IntPtr screen=GetDC(IntPtr.Zero),memory=IntPtr.Zero,bitmap=IntPtr.Zero,previous=IntPtr.Zero;
        try{
            memory=CreateCompatibleDC(screen);var info=new Info{Size=40,Width=frame.Width,Height=-frame.Height,Planes=1,Bits=32};
            bitmap=CreateDIBSection(memory,ref info,0,out var target,IntPtr.Zero,0);if(bitmap==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            var bits=frame.LockBits(new Rectangle(Point.Empty,frame.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{
                int stride=Math.Abs(bits.Stride);var source=new byte[stride*frame.Height];Marshal.Copy(bits.Scan0,source,0,source.Length);var data=new byte[frame.Width*frame.Height*4];
                for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++){
                    int s=y*stride+x*4,d=(y*frame.Width+x)*4,a=source[s+3];
                    for(int c=0;c<3;c++)data[d+c]=(byte)((source[s+c]*a+127)/255);data[d+3]=(byte)a;
                }
                Marshal.Copy(data,0,target,data.Length);
            }finally{frame.UnlockBits(bits);}
            previous=SelectObject(memory,bitmap);var origin=Point.Empty;var size=frame.Size;var blend=new Blend{Opacity=255,Format=1};
            int style=GetStyle(window,-20);if((style&Style)==0)SetStyle(window,-20,style|Style);
            if(!UpdateLayeredWindow(window,screen,ref position,ref size,memory,ref origin,0,ref blend,2)){
                // Recover if Windows Forms previously set a uniform opacity on this HWND.
                SetStyle(window,-20,style&~Style);SetStyle(window,-20,style|Style);
                if(!UpdateLayeredWindow(window,screen,ref position,ref size,memory,ref origin,0,ref blend,2))throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }finally{if(previous!=IntPtr.Zero)SelectObject(memory,previous);if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(memory!=IntPtr.Zero)DeleteDC(memory);if(screen!=IntPtr.Zero)ReleaseDC(IntPtr.Zero,screen);}
    }
}
