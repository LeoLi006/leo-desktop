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
    internal sealed class Surface:IDisposable
    {
        IntPtr memory,bitmap,previous,pixels;Size size;byte[] data=[];bool disposed;
        void Ensure(Size wanted)
        {
            if(disposed)throw new ObjectDisposedException(nameof(Surface));
            if(size==wanted&&bitmap!=IntPtr.Zero)return;
            Release();memory=CreateCompatibleDC(IntPtr.Zero);
            if(memory==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            var info=new Info{Size=40,Width=wanted.Width,Height=-wanted.Height,Planes=1,Bits=32};
            bitmap=CreateDIBSection(memory,ref info,0,out pixels,IntPtr.Zero,0);
            if(bitmap==IntPtr.Zero){Release();throw new Win32Exception(Marshal.GetLastWin32Error());}
            previous=SelectObject(memory,bitmap);size=wanted;data=new byte[wanted.Width*wanted.Height*4];
        }
        public void Present(IntPtr window,Point position,Bitmap frame)
        {
            Ensure(frame.Size);
            var bits=frame.LockBits(new Rectangle(Point.Empty,frame.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{
                if(bits.Stride==frame.Width*4)Marshal.Copy(bits.Scan0,data,0,data.Length);
                else for(int y=0;y<frame.Height;y++)Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),data,y*frame.Width*4,frame.Width*4);
                for(int at=0;at<data.Length;at+=4){int a=data[at+3];data[at]=(byte)((data[at]*a+127)/255);data[at+1]=(byte)((data[at+1]*a+127)/255);data[at+2]=(byte)((data[at+2]*a+127)/255);}
                Marshal.Copy(data,0,pixels,data.Length);
            }finally{frame.UnlockBits(bits);}
            var origin=Point.Empty;var blend=new Blend{Opacity=255,Format=1};
            int style=GetStyle(window,-20);if((style&Style)==0)SetStyle(window,-20,style|Style);
            if(!UpdateLayeredWindow(window,IntPtr.Zero,ref position,ref size,memory,ref origin,0,ref blend,2)){
                SetStyle(window,-20,style&~Style);SetStyle(window,-20,style|Style);
                if(!UpdateLayeredWindow(window,IntPtr.Zero,ref position,ref size,memory,ref origin,0,ref blend,2))throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        void Release(){if(previous!=IntPtr.Zero&&memory!=IntPtr.Zero)SelectObject(memory,previous);if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(memory!=IntPtr.Zero)DeleteDC(memory);previous=bitmap=memory=pixels=IntPtr.Zero;size=Size.Empty;data=[];}
        public void Dispose(){if(disposed)return;disposed=true;Release();}
    }
}
