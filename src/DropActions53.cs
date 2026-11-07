using System.Diagnostics;
namespace LeoLauncher;
sealed class DropContent:IDisposable
{
    public string[] Paths{get;init;}=[];public string Text{get;init;}="";public Bitmap? Image{get;init;}
    public bool IsUrl=>Uri.TryCreate(Text,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https"&&uri.UserInfo.Length==0;
    public string Summary=>Paths.Length>0?string.Join(Environment.NewLine,Paths):Image!=null?"图片 · "+Image.Width+" × "+Image.Height:Text;
    public static bool Accepts(IDataObject? data)=>data!=null&&(data.GetDataPresent(DataFormats.FileDrop)||data.GetDataPresent(DataFormats.UnicodeText)||data.GetDataPresent(DataFormats.Text)||data.GetDataPresent(DataFormats.Bitmap));
    public static DropContent Read(IDataObject data)
    {
        if(data.GetData(DataFormats.FileDrop) is string[] files){var paths=FileDropActions.RequirePaths(files);if(paths.Length==0)throw new IOException("拖入的文件已不存在");return new(){Paths=paths};}
        string text=(data.GetData(DataFormats.UnicodeText)??data.GetData(DataFormats.Text)) as string??"";if(text.Trim().Length>0){if(text.Length>20000)throw new ArgumentException("拖入文字最多20000字符");return new(){Text=text.Trim()};}
        if(data.GetData(DataFormats.Bitmap) is Bitmap image&&image.Width<=8192&&image.Height<=8192&&(long)image.Width*image.Height<=32_000_000)return new(){Image=new Bitmap(image)};throw new ArgumentException("暂不支持此拖放内容");
    }
    public void Dispose()=>Image?.Dispose();
    public bool IsImageFile=>Paths.Length==1&&File.Exists(Paths[0])&&new[]{".png",".jpg",".jpeg",".bmp",".gif",".webp"}.Contains(Path.GetExtension(Paths[0]),StringComparer.OrdinalIgnoreCase);
    public bool HasMedia=>Paths.Any(p=>new[]{".mp4",".mkv",".avi",".mov",".mp3",".flac",".wav",".webm",".m4a",".wmv",".ts"}.Contains(Path.GetExtension(p),StringComparer.OrdinalIgnoreCase));
    public string[] ActionIds(Preferences p,IEnumerable<AppEntry>? applications=null)=>Paths.Length>0?
        (Paths.Any(DropOpenExecution.MayRun)?Array.Empty<string>():new[]{"default"})
        .Concat(LauncherFileSearch.FindCode(applications??[])!=null?new[]{"code"}:[])
        .Concat(Paths.Length==1&&HasMedia&&DropOpenExecution.MediaAvailable(p)?new[]{"potplayer"}:[])
        .Concat(new[]{"choose_app","copy_paths","reveal","note","todo"})
        .Concat(Paths.Any(ApplicationDrop.IsProgram)?new[]{"add_module"}:[])
        .Concat(IsImageFile?new[]{"copy_image_file"}:[]).ToArray():Image!=null?new[]{"copy_image","save_image"}:
        new[]{"copy_text","note","todo"}.Concat(IsUrl&&p.EdgeModule?new[]{"open_url"}:[]).ToArray();

}
sealed class QuickTextForm:BarcaWindow
{
    public string Value=>input.Text.Trim();readonly TextBox input=new();
    public QuickTextForm(string title,string initial,string subtitle="确认内容后加入今天的清单；可在清单中改期。"){Text=title;ClientSize=new(540,290);StartPosition=FormStartPosition.CenterParent;Controls.Add(BarcaUi.Chrome(this,title,subtitle));input.Bounds=new(24,109,492,96);input.Text=initial[..Math.Min(200,initial.Length)];input.MaxLength=200;input.Multiline=true;input.BackColor=BarcaUi.Raised;input.ForeColor=BarcaUi.Text;var add=new BarcaButton{Text="确认添加",Bounds=new(376,233,140,34)};add.Click+=(_,_)=>{if(Value.Length>0)DialogResult=DialogResult.OK;};Controls.AddRange([input,add]);MinimumSize=MaximumSize=Size;}
}
sealed partial class PetForm
{
    DropActionForm? dropWindow;
    void AcceptDrop(IDataObject data)
    {
        DropContent? content=null;try{content=DropContent.Read(data);launcher.Hide();RevealDock(true);dropWindow?.Close();dropWindow=new DropActionForm(content,prefs,(a,v)=>commands.ExecuteAsync(new(a,v,"drop")),AddApplicationModules,ShowNotes,text=>{using var confirm=new QuickTextForm("添加待办",text);WindowPositions.Prepare(confirm,"drop-todo",Bounds,previewMode);if(confirm.ShowDialog()==DialogResult.OK){todoClient.From("drop-ui").Add(confirm.Value,DateOnly.FromDateTime(DateTime.Today),null);todoWindow?.View.Render();OperationFeedback.Show("已添加到今天的清单");}},launcher.FileApplications,(a,v,t)=>commands.ExecuteAsync(new(a,v,"drop"),t));WindowPositions.Prepare(dropWindow,"drop-actions",Bounds,true);dropWindow.Show();dropWindow.Activate();}catch(Exception e){content?.Dispose();OperationFeedback.Error(e.Message);}
    }
}
