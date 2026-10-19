using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace LeoLauncher;
sealed record ThemeOptions(string? Accent=null,int Brightness=100,int CornerRadius=14,int Opacity=100)
{
    public void Validate(){if(Accent!=null&&!Regex.IsMatch(Accent,@"^#[0-9a-fA-F]{6}$")||Brightness is <70 or >130||CornerRadius is <0 or >24||Opacity is <70 or >100)throw new ArgumentException("主题参数无效：明暗70～130、圆角0～24、透明度70～100。");}
}
sealed record CustomSkin(string Id,string Name,string BaseTheme,string Format="LeoJersey/1",bool OriginalKit=false);
static class SkinLibrary
{
    internal static string Root{get;set;}=Path.Combine(Store.Root,"skins");
    public static string Folder(string id){if(!Regex.IsMatch(id,@"^custom:[a-f0-9]{32}$"))throw new ArgumentException("皮肤标识无效");return Path.Combine(Root,id[7..]);}
    static string cacheKey="";static CustomSkin[] cached=[];
    public static CustomSkin[] Load(){string key=Root+"|"+(Directory.Exists(Root)?Directory.GetLastWriteTimeUtc(Root).Ticks:0);if(key==cacheKey)return cached;cacheKey=key;cached=[];if(!Directory.Exists(Root))return [];var skins=new List<CustomSkin>();foreach(var path in Directory.GetDirectories(Root))try{var skin=JsonSerializer.Deserialize<CustomSkin>(File.ReadAllText(Path.Combine(path,"skin.json")));if(skin!=null&&skin.Format=="LeoJersey/1"&&skin.Name.Trim().Length is >0 and <=24&&SkinSelection.Options.Any(o=>o.Id==skin.BaseTheme)&&Path.GetFullPath(Folder(skin.Id)).Equals(Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase)&&File.Exists(Path.Combine(path,"jersey.png")))skins.Add(skin);}catch(Exception e)when(e is IOException or ArgumentException or JsonException or UnauthorizedAccessException){}return cached=skins.OrderBy(s=>s.Name).ToArray();}
    public static CustomSkin? Find(string? id)=>id!=null&&id.StartsWith("custom:")?Load().FirstOrDefault(s=>s.Id==id):null;
    public static string TexturePath(string id)=>Path.Combine(Folder(id),"jersey.png");
    public static CustomSkin Import(string source,string name,string baseTheme)
    {
        name=UiTheme.ValidateName(name);if(!SkinSelection.Options.Any(o=>o.Id==baseTheme))throw new ArgumentException("请选择内置球队作为界面基础主题");if(new FileInfo(source).Length>10_000_000)throw new ArgumentException("球衣PNG最多10MB");
        using var stream=File.OpenRead(source);using var image=Image.FromStream(stream);if(image.RawFormat.Guid!=ImageFormat.Png.Guid||image.Width!=1024||image.Height!=1024)throw new ArgumentException("请使用1024×1024像素的PNG球衣纹样，其他尺寸请先按模板制作。");
        using var clean=new Bitmap(1024,1024,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(clean)){g.Clear(Color.White);g.DrawImageUnscaled(image,0,0);}var skin=new CustomSkin("custom:"+Guid.NewGuid().ToString("N"),name,baseTheme);string folder=Folder(skin.Id);Directory.CreateDirectory(folder);cacheKey="";try{clean.Save(Path.Combine(folder,"jersey.png"),ImageFormat.Png);ConfigMigration.Write(Path.Combine(folder,"skin.json"),JsonSerializer.Serialize(skin));return skin;}catch{Directory.Delete(folder,true);throw;}
    }
    public static CustomSkin Duplicate(string id,string name)
    {
        var custom=Find(id);string basis=custom?.BaseTheme??id;string? source=custom!=null?TexturePath(id):KitTexture.SourcePath(basis);
        using var image=source!=null&&File.Exists(source)?Image.FromFile(source):null;using var scaled=new Bitmap(1024,1024);using(var g=Graphics.FromImage(scaled)){if(image!=null)g.DrawImage(image,0,0,1024,1024);else{g.Clear(Color.FromArgb(24,48,113));using var red=new SolidBrush(Color.FromArgb(160,28,54));for(int x=0;x<1024;x+=256)g.FillRectangle(red,x,0,128,1024);}}string temp=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".png");try{scaled.Save(temp,ImageFormat.Png);var added=Import(temp,name,basis);if(id=="83"||custom?.OriginalKit==true){added=added with{OriginalKit=true};ConfigMigration.Write(Path.Combine(Folder(added.Id),"skin.json"),JsonSerializer.Serialize(added));cacheKey="";}return added;}finally{File.Delete(temp);}
    }
    public static void Remove(string id){string folder=Folder(id);if(Directory.Exists(folder)){if(File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint))throw new IOException("不支持链接目录");Directory.Delete(folder,true);cacheKey="";}}
    public static void ExportTemplate(string path)
    {
        using var image=new Bitmap(1024,1024);using(var g=Graphics.FromImage(image)){g.Clear(Color.FromArgb(100,170,215));using var sleeve=new SolidBrush(Color.FromArgb(30,70,115));g.FillRectangle(sleeve,0,0,85,1024);g.FillRectangle(sleeve,939,0,85,1024);g.FillRectangle(sleeve,385,0,254,42);using var chest=new SolidBrush(Color.White);g.FillEllipse(chest,190,160,60,60);using var sponsor=BarcaUi.Font(36,true);TextRenderer.DrawText(g,"YOUR TEAM",sponsor,new Rectangle(200,285,624,100),Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
        image.Save(path,ImageFormat.Png);
    }
}
