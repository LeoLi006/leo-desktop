using System.Drawing.Imaging;
namespace LeoLauncher;

// A skin is one atomic set: never mix a new idle costume with the old movement art.
sealed class AnimationSkin:IDisposable
{
    public readonly Bitmap Main,Vertical;
    public readonly string NameOfKit;
    public readonly DockSprites Dock;
    public readonly DockBridge Bridge;
    public static string? Name(string? id)=>id switch{"382"=>"city","20232"=>"miami","202"=>"argentina","164"=>"spain",_=>null};
    public AnimationSkin(string name)
    {
        NameOfKit=name;string root=Path.Combine(Store.Root,"assets");
        Bitmap? main=null,vertical=null;DockSprites? dock=null;DockBridge? bridge=null;
        try{main=new(Path.Combine(root,"leo.png"));vertical=new(Path.Combine(root,"vertical-drag.png"));dock=new(kit:name);bridge=new(Path.Combine(root,"edge-bridge-v1.png"),main,name);Main=main;Vertical=vertical;Dock=dock;Bridge=bridge;}
        catch{main?.Dispose();vertical?.Dispose();dock?.Dispose();bridge?.Dispose();throw;}
    }
    public void Dispose(){Main.Dispose();Vertical.Dispose();Dock.Dispose();Bridge.Dispose();}
}

sealed partial class PetForm
{
    readonly Dictionary<string,AnimationSkin> animationSkins=new();
    readonly HashSet<string> failedSkins=new();
    AnimationSkin? CurrentSkin()
    {
        var name=AnimationSkin.Name(matchDayTeam);
        if(!prefs.FootballModule||!prefs.MatchDayOutfits||name==null||failedSkins.Contains(name))return null;
        if(animationSkins.TryGetValue(name,out var skin))return skin;
        try{skin=new(name);animationSkins.Add(name,skin);return skin;}
        catch(IOException){failedSkins.Add(name);return null;}
        catch(ArgumentException){failedSkins.Add(name);return null;}
    }
    DockSprites CurrentDock()=>CurrentSkin()?.Dock??(dockSprites??=new());
    DockBridge? dockBridge;
    DockBridge CurrentBridge()=>CurrentSkin()?.Bridge??(dockBridge??=new(Path.Combine(Store.Root,"assets","edge-bridge-v1.png"),atlas));
    void DrawAnimationFrame(Graphics graphics,RectangleF target,float opacity=1)
    {
        bool vertical=row is 11 or 12;var skin=CurrentSkin();var image=vertical?skin?.Vertical??verticalAtlas:skin?.Main??atlas;
        int columns=vertical?4:8,rows=vertical?2:11;float w=image.Width/(float)columns,h=image.Height/(float)rows;
        if(skin!=null){KitPalette.Draw(graphics,image,target,new RectangleF(frame*w,(vertical?row-11:row)*h,w,h),skin.NameOfKit,opacity);return;}
        using var alpha=new ImageAttributes();alpha.SetColorMatrix(new ColorMatrix{Matrix33=opacity});
        graphics.DrawImage(image,Rectangle.Round(target),frame*w,(vertical?row-11:row)*h,w,h,GraphicsUnit.Pixel,alpha);
    }
}
