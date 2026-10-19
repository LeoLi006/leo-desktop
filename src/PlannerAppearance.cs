using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace LeoLauncher;
static class PlannerPhrases
{
    static string[]? custom;
    public static string[] Current=>custom??QuoteDeck.Lines;
    public static int Revision{get;private set;}
    public static event Action? Changed;
    public static void Validate(string[]? lines){if(lines!=null&&(lines.Length>200||lines.Any(s=>s==null||s.Length is <1 or >120||s.Any(char.IsControl))))throw new ArgumentException("最多200句话，每句1～120字，不含控制字符。");}
    public static void Refresh(){Revision++;if(Changed is {} changed)foreach(Action update in changed.GetInvocationList())FeatureFaults.Run("清单话语刷新",update);}
    public static void Configure(string[]? lines){Validate(lines);if((custom==null&&lines==null)||(custom!=null&&lines!=null&&custom.SequenceEqual(lines)))return;custom=lines?.ToArray();Refresh();}
}
static class PlannerBadge
{
    static readonly Dictionary<string,Bitmap> cache=new();
    public static string? PathFor(string id){var skin=SkinLibrary.Find(id);return skin?.BadgeFile is {} file&&Regex.IsMatch(file,@"^badge-[a-f0-9]{32}\.png$")&&File.Exists(Path.Combine(SkinLibrary.Folder(id),file))?Path.Combine(SkinLibrary.Folder(id),file):null;}
    public static CustomSkin Set(string id,string? source)
    {
        var skin=SkinLibrary.Find(id)??throw new ArgumentException("请先新建或选择一套自定义皮肤。");
        if(source==null)return SkinLibrary.Update(skin with{BadgeFile=null});
        if(new FileInfo(source).Length>4_000_000)throw new ArgumentException("标识图片最多4MB。");
        using var stream=File.OpenRead(source);using var image=Image.FromStream(stream);
        if(image.Width is <16 or >2048||image.Height is <16 or >2048)throw new ArgumentException("标识图片宽高须为16～2048像素。");
        string name="badge-"+Guid.NewGuid().ToString("N")+".png",path=Path.Combine(SkinLibrary.Folder(id),name);
        using var clean=new Bitmap(256,256,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(clean)){g.Clear(Color.Transparent);g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;float scale=Math.Min(248f/image.Width,248f/image.Height);g.DrawImage(image,(256-image.Width*scale)/2,(256-image.Height*scale)/2,image.Width*scale,image.Height*scale);}
        clean.Save(path,ImageFormat.Png);try{return SkinLibrary.Update(skin with{BadgeFile=name});}catch{File.Delete(path);throw;}
    }
    public static void Draw(Graphics g,string id,Rectangle bounds){var path=PathFor(id);if(path!=null){try{if(!cache.TryGetValue(path,out var image)){if(cache.Count>=8){foreach(var old in cache.Values)old.Dispose();cache.Clear();}using var source=Image.FromFile(path);image=new Bitmap(source);cache[path]=image;}g.DrawImage(image,bounds);return;}catch(IOException){}catch(ArgumentException){}}BarcaUi.DrawBadge(g,SkinLibrary.Find(id)?.BaseTheme??id,bounds);}
}
sealed class PlannerAppearanceForm:BarcaWindow
{
    public PlannerAppearanceForm(string skin,Preferences prefs,Action<Preferences> apply,bool preview=false)
    {
        Text="Leo · 清单标识与话语";ClientSize=new(690,620);Controls.Add(BarcaUi.Chrome(this,"清单标识与话语","队徽随皮肤保存；每行一句，清空可停止滚动话语。"));
        string? badge=null;bool badgeChanged=false;string[]? lines=prefs.PlannerQuotes?.ToArray();
        var picture=new PictureBox{Bounds=new(25,107,64,64),SizeMode=PictureBoxSizeMode.Zoom};var pick=new BarcaButton{Text="上传队徽 / 国旗…",Bounds=new(110,108,190,32),Enabled=SkinLibrary.Find(skin)!=null};var clear=new BarcaButton{Text="使用默认标识",Bounds=new(316,108,160,32),Enabled=pick.Enabled};
        using(var bitmap=new Bitmap(64,64)){using(var g=Graphics.FromImage(bitmap))PlannerBadge.Draw(g,skin,new Rectangle(0,0,64,64));picture.Image=new Bitmap(bitmap);}
        var explanation=new Label{Text=pick.Enabled?"PNG / JPG，16～2048像素，最多4MB；透明PNG效果更好。":"内置皮肤保留原队徽；请先复制或新建一套自定义皮肤再上传。",Bounds=new(110,148,550,40),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)};
        var input=new TextBox{AccessibleName="清单自定义话语",Multiline=true,ScrollBars=ScrollBars.Vertical,AcceptsReturn=true,Bounds=new(25,225,640,265),BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text,Font=BarcaUi.Font(10),Text=string.Join(Environment.NewLine,lines??QuoteDeck.Lines)};
        Controls.Add(new Label{Text="滚动话语 · 可直接添加或删除行",Bounds=new(25,195,600,26),ForeColor=BarcaUi.Gold});
        var defaults=new BarcaButton{Text="恢复默认话语",Bounds=new(25,505,160,32)};defaults.Click+=(_,_)=>input.Text=string.Join(Environment.NewLine,QuoteDeck.Lines);
        var state=new Label{Bounds=new(25,548,420,55),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)};
        pick.Click+=(_,_)=>{using var dialog=new OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{if(new FileInfo(dialog.FileName).Length>4_000_000)throw new ArgumentException("图片最多4MB");using var source=Image.FromFile(dialog.FileName);if(source.Width is <16 or >2048||source.Height is <16 or >2048)throw new ArgumentException("宽高须为16～2048像素");var old=picture.Image;picture.Image=new Bitmap(source);old?.Dispose();badge=dialog.FileName;badgeChanged=true;}catch(Exception e){state.Text=e.Message;}};
        clear.Click+=(_,_)=>{badge=null;badgeChanged=true;var old=picture.Image;picture.Image=null;old?.Dispose();};
        var save=new BarcaButton{Text="保存",Bounds=new(545,564,120,34)};save.Click+=(_,_)=>{try{var next=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(prefs))!;next.PlannerQuotes=input.Lines.Select(s=>s.Trim()).Where(s=>s.Length>0).Distinct().ToArray();PlannerPhrases.Validate(next.PlannerQuotes);if(preview){state.Text="示例模式不写入配置";return;}var original=SkinLibrary.Find(skin);try{if(badgeChanged)PlannerBadge.Set(skin,badge);apply(next);if(badgeChanged)PlannerPhrases.Refresh();}catch{if(original!=null&&badgeChanged)SkinLibrary.Update(original);throw;}DialogResult=DialogResult.OK;}catch(Exception e){state.Text=e.Message;}};
        var cancel=new BarcaButton{Text="取消",Bounds=new(445,564,90,34),DialogResult=DialogResult.Cancel};CancelButton=cancel;Controls.AddRange([picture,pick,clear,explanation,input,defaults,state,cancel,save]);Disposed+=(_,_)=>picture.Image?.Dispose();MinimumSize=MaximumSize=Size;
    }
}
