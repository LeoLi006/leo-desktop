using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace LeoLauncher;
sealed record SkinPalette(string Primary,string Secondary,string Accent,string Motto="")
{
    public void Validate(){if(new[]{Primary,Secondary,Accent}.Any(s=>s==null||!Regex.IsMatch(s,@"^#[0-9a-fA-F]{6}$"))||Motto==null||Motto.Length>80||Motto.Any(char.IsControl))throw new ArgumentException("主题颜色须为#RRGGBB，标语最多80个字符");}
    public static SkinPalette From(UiTheme.Palette p)=>new(ColorTranslator.ToHtml(p.Primary),ColorTranslator.ToHtml(p.Secondary),ColorTranslator.ToHtml(p.Accent),p.Motto);
}
sealed record SkinBundle(string Format,string Name,SkinPalette Palette,ThemeOptions Options,bool OriginalKit=false,bool HasBadge=false);
static class SkinPackages
{
    public static void Export(string id,ThemeOptions options,string destination)
    {
        options.Validate();var custom=SkinLibrary.Find(id);var palette=SkinPalette.From(UiTheme.PaletteFor(id));
        string? badge=PlannerBadge.PathFor(id);string fallback=Path.Combine(Store.Root,"assets","badges",custom?.BaseTheme??id)+".png";if(badge==null&&File.Exists(fallback))badge=fallback;
        var bundle=new SkinBundle("LeoSkin/1",custom?.Name??SkinSelection.Options.First(s=>s.Id==id).Name,palette,options,id=="83"||custom?.OriginalKit==true,badge!=null);
        string temp=destination+".tmp-"+Guid.NewGuid().ToString("N");
        try{using(var zip=ZipFile.Open(temp,ZipArchiveMode.Create)){
            using(var writer=new StreamWriter(zip.CreateEntry("skin.json").Open()))writer.Write(JsonSerializer.Serialize(bundle,new JsonSerializerOptions{WriteIndented=true}));
            using(var output=zip.CreateEntry("jersey.png").Open())using(var png=SkinLibrary.JerseyImage(id))png.Save(output,System.Drawing.Imaging.ImageFormat.Png);
            if(badge!=null)using(var output=zip.CreateEntry("badge.png").Open())using(var image=Image.FromFile(badge))image.Save(output,System.Drawing.Imaging.ImageFormat.Png);
        }File.Move(temp,destination,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public static CustomSkin Import(string file)
    {
        if(new FileInfo(file).Length>20_000_000)throw new ArgumentException("皮肤包最多20MB");using var zip=ZipFile.OpenRead(file);
        if(zip.Entries.Count is <2 or >3||zip.Entries.Any(e=>e.FullName is not("skin.json" or "jersey.png" or "badge.png")||e.Length>10_000_000||(e.ExternalAttributes>>16&0xF000)==0xA000)||zip.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=zip.Entries.Count||zip.GetEntry("jersey.png")==null||zip.GetEntry("skin.json")==null)throw new ArgumentException("皮肤包只允许skin.json、jersey.png和可选badge.png");
        var manifest=zip.GetEntry("skin.json")!;if(manifest.Length>32000)throw new ArgumentException("皮肤信息过大");SkinBundle bundle;using(var reader=new StreamReader(manifest.Open()))bundle=JsonSerializer.Deserialize<SkinBundle>(reader.ReadToEnd())??throw new ArgumentException("皮肤包内容为空");
        if(bundle.Format!="LeoSkin/1"||bundle.Palette==null||bundle.Options==null||bundle.HasBadge!=(zip.GetEntry("badge.png")!=null))throw new ArgumentException("皮肤包格式或标识声明无效");UiTheme.ValidateName(bundle.Name);bundle.Palette.Validate();bundle.Options.Validate();
        string temp=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".png"),badgeTemp=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".png");CustomSkin? added=null;
        try{using(var input=zip.GetEntry("jersey.png")!.Open())using(var output=File.Create(temp))input.CopyTo(output);added=SkinLibrary.CreateIndependent(bundle.Name,temp,bundle.Palette,bundle.Options);if(bundle.OriginalKit)added=SkinLibrary.Update(added with{OriginalKit=true});if(bundle.HasBadge){using(var input=zip.GetEntry("badge.png")!.Open())using(var output=File.Create(badgeTemp))input.CopyTo(output);added=PlannerBadge.Set(added.Id,badgeTemp);}return added;}
        catch{if(added!=null)SkinLibrary.Remove(added.Id);throw;}
        finally{File.Delete(temp);File.Delete(badgeTemp);}
    }

}
static class SkinCreation
{
    public static void Uniform(Bitmap image,SkinPalette palette){using var g=Graphics.FromImage(image);g.Clear(ColorTranslator.FromHtml(palette.Primary));using var sleeve=new SolidBrush(ColorTranslator.FromHtml(palette.Secondary));g.FillRectangle(sleeve,0,0,85,1024);g.FillRectangle(sleeve,939,0,85,1024);g.FillRectangle(sleeve,385,0,254,42);}
}
sealed class PalettePreview:Control
{
    public SkinPalette Palette=new("#3E657D","#344153","#D6B475");public PalettePreview(){DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;var primary=ColorTranslator.FromHtml(Palette.Primary);var secondary=ColorTranslator.FromHtml(Palette.Secondary);var accent=ColorTranslator.FromHtml(Palette.Accent);e.Graphics.Clear(Color.FromArgb(18,23,30));using var one=new SolidBrush(primary);using var two=new SolidBrush(secondary);using var gold=new SolidBrush(accent);e.Graphics.FillRectangle(one,20,48,Width-40,120);e.Graphics.FillRectangle(two,20,48,25,120);e.Graphics.FillRectangle(two,Width-45,48,25,120);e.Graphics.FillRectangle(gold,Width/2-15,67,30,5);using var font=BarcaUi.Font(10,true);TextRenderer.DrawText(e.Graphics,"独立主题预览",font,new Rectangle(10,10,Width-20,30),accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);e.Graphics.FillRectangle(one,20,199,Width-40,38);e.Graphics.FillRectangle(two,20,250,Width-40,38);TextRenderer.DrawText(e.Graphics,"示例按钮",font,new Rectangle(20,199,Width-40,38),Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);TextRenderer.DrawText(e.Graphics,"卡片与菜单",font,new Rectangle(20,250,Width-40,38),accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
}
