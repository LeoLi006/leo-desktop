using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace LeoLauncher;

sealed class KitTexture
{
    static readonly Dictionary<string,KitTexture> cache=new();
    readonly byte[] pixels;readonly int width,height,stride;
    KitTexture(string name)
    {
        using var image=new Bitmap(Path.Combine(Store.Root,"assets","MatchDay","Kits",name+"-2024.png"));width=image.Width;height=image.Height;
        var bits=image.LockBits(new Rectangle(Point.Empty,image.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try{stride=Math.Abs(bits.Stride);pixels=new byte[stride*height];Marshal.Copy(bits.Scan0,pixels,0,pixels.Length);}finally{image.UnlockBits(bits);}
    }
    public static KitTexture Load(string name){if(!cache.TryGetValue(name,out var value)){value=new(name);cache.Add(name,value);}return value;}
    public Color Sample(float u,float v)
    {
        int x=Math.Clamp((int)(u*(width-1)),0,width-1),y=Math.Clamp((int)(v*(height-1)),0,height-1),at=y*stride+x*4;
        return Color.FromArgb(pixels[at+2],pixels[at+1],pixels[at]);
    }
    internal static bool Fabric(Color p)=>Math.Max(p.R,p.B)>50&&Math.Max(p.R,p.B)>p.G*1.3&&p.B>p.G*.5;
    internal static Color Shade(Color texel,Color original)
    {
        float light=Math.Clamp((Math.Max(original.R,original.B)+100)/275f,.94f,1.04f);
        return Color.FromArgb(original.A,Math.Min(255,(int)(texel.R*light)),Math.Min(255,(int)(texel.G*light)),Math.Min(255,(int)(texel.B*light)));
    }
    internal static Color Shorts(Color original,string name)
    {
        if(name=="spain")return original;
        float light=Math.Clamp(original.B/100f,.7f,1.05f);
        var color=name=="city"?Color.FromArgb(240,241,238):Color.FromArgb(24,26,32);
        return Color.FromArgb(original.A,(int)(color.R*light),(int)(color.G*light),(int)(color.B*light));
    }
}
