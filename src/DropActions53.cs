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
        if(data.GetData(DataFormats.FileDrop) is string[] files){var paths=FileDropActions.ValidPaths(files);if(paths.Length==0)throw new IOException("拖入的文件已不存在");return new(){Paths=paths};}
        string text=(data.GetData(DataFormats.UnicodeText)??data.GetData(DataFormats.Text)) as string??"";if(text.Trim().Length>0){if(text.Length>20000)throw new ArgumentException("拖入文字最多20000字符");return new(){Text=text.Trim()};}
        if(data.GetData(DataFormats.Bitmap) is Bitmap image&&image.Width<=8192&&image.Height<=8192&&(long)image.Width*image.Height<=32_000_000)return new(){Image=new Bitmap(image)};throw new ArgumentException("暂不支持此拖放内容");
    }
    public void Dispose()=>Image?.Dispose();
    public bool IsImageFile=>Paths.Length==1&&File.Exists(Paths[0])&&new[]{".png",".jpg",".jpeg",".bmp",".gif",".webp"}.Contains(Path.GetExtension(Paths[0]),StringComparer.OrdinalIgnoreCase);
    public bool HasMedia=>Paths.Any(p=>new[]{".mp4",".mkv",".avi",".mov",".mp3",".flac",".wav",".webm",".m4a",".wmv",".ts"}.Contains(Path.GetExtension(p),StringComparer.OrdinalIgnoreCase));
    public string[] ActionIds(Preferences p)=>Paths.Length>0?new[]{"copy_paths","reveal","note","todo"}.Concat(Paths.Any(ApplicationDrop.IsProgram)?new[]{"add_module"}:[]).Concat(HasMedia&&p.MediaModule?new[]{"potplayer"}:[]).Concat(IsImageFile?new[]{"copy_image_file"}:[]).Concat(new[]{"choose_app"}).ToArray():Image!=null?new[]{"copy_image","save_image"}:new[]{"copy_text","note","todo"}.Concat(IsUrl&&p.EdgeModule?new[]{"open_url"}:[]).ToArray();
}
sealed class DropActionForm:BarcaWindow
{
    public DropActionForm(DropContent content,Preferences p,Func<string,string,Task<string>> execute,Action<string[]> addModule,Action<string> note,Action<string> todo,IEnumerable<AppEntry> applications)
    {
        Text="Leo · 拖放动作";ClientSize=new(590,390);StartPosition=FormStartPosition.CenterParent;Controls.Add(BarcaUi.Chrome(this,"选择一个动作",content.Paths.Length>0?$"已接收 {content.Paths.Length} 个文件或文件夹":content.IsUrl?"已接收网址":content.Image!=null?"已接收图片":"已接收文字"));
        var summary=new TextBox{Text=content.Summary,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Bounds=new(24,108,542,92),BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text,BorderStyle=BorderStyle.None};var actions=new FlowLayoutPanel{Bounds=new(20,216,552,110),WrapContents=true};var status=new Label{Bounds=new(24,342,542,32),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)};
        var titles=new Dictionary<string,string>{{"copy_image_file","复制图片"},{"copy_paths","复制路径"},{"reveal","打开所在文件夹"},{"note","存为便签"},{"todo","添加待办"},{"add_module","添加应用模块"},{"potplayer","用PotPlayer播放"},{"choose_app","选择应用打开…"},{"copy_image","复制图片"},{"save_image","保存图片…"},{"copy_text","复制文字"},{"open_url","打开网址"}};
        foreach(string id in content.ActionIds(p)){var button=new BarcaButton{Text=titles[id],Size=new(174,40),Margin=new Padding(5)};button.Click+=async(_,_)=>{button.Enabled=false;try{switch(id){case "copy_paths":Clipboard.SetText(string.Join(Environment.NewLine,content.Paths));status.Text="已复制路径";break;case "copy_text":Clipboard.SetText(content.Text);status.Text="已复制文字";break;case "copy_image_file":{if(new FileInfo(content.Paths[0]).Length>20_000_000)throw new ArgumentException("图片过大，请用图片软件打开");using var image=System.Drawing.Image.FromFile(content.Paths[0]);if((long)image.Width*image.Height>32_000_000)throw new ArgumentException("图片尺寸过大");Clipboard.SetImage(image);status.Text="已复制图片";break;}case "copy_image":Clipboard.SetImage(content.Image!);status.Text="已复制图片";break;case "save_image":using(var picker=new SaveFileDialog{Filter="PNG图片|*.png",FileName="Leo-image.png"})if(picker.ShowDialog(this)==DialogResult.OK){content.Image!.Save(picker.FileName,System.Drawing.Imaging.ImageFormat.Png);status.Text="图片已保存";}break;case "reveal":status.Text=await execute("path_reveal",content.Paths[0]);break;case "open_url":status.Text=await execute("web_open",content.Text);break;case "potplayer":status.Text=await execute("media_file",content.Paths.First(p=>new DropContent{Paths=[p]}.HasMedia));break;case "add_module":addModule(content.Paths);Close();break;case "note":note(content.Summary);Close();break;case "todo":todo(content.Paths.Length>0?Path.GetFileName(content.Paths[0]):content.Text);Close();break;case "choose_app":using(var picker=new DropApplicationPicker(p)){if(picker.ShowDialog(this)==DialogResult.OK)status.Text=await execute("files_with_app",System.Text.Json.JsonSerializer.Serialize(new{application=picker.Executable,module=picker.ModuleId,paths=content.Paths}));}break;}}catch(OperationCanceledException){}catch(Exception e){if(!IsDisposed)status.Text=e.Message;}finally{if(!button.IsDisposed)button.Enabled=true;}};actions.Controls.Add(button);}
        int extra=Math.Max(0,(content.ActionIds(p).Length+2)/3-2)*50;if(extra>0){ClientSize=new(590,390+extra);actions.Height+=extra;status.Top+=extra;}
        Controls.AddRange([summary,actions,status]);MinimumSize=MaximumSize=Size;Disposed+=(_,_)=>content.Dispose();
    }
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
        DropContent? content=null;try{content=DropContent.Read(data);launcher.Hide();RevealDock(true);dropWindow?.Close();dropWindow=new DropActionForm(content,prefs,(a,v)=>commands.ExecuteAsync(new(a,v,"drop")),AddApplicationModules,ShowNotes,text=>{using var confirm=new QuickTextForm("添加待办",text);WindowPositions.Prepare(confirm,"drop-todo",Bounds,previewMode);if(confirm.ShowDialog()==DialogResult.OK){todoClient.From("drop-ui").Add(confirm.Value,DateOnly.FromDateTime(DateTime.Today),null);todoWindow?.View.Render();OperationFeedback.Show("已添加到今天的清单");}},launcher.FileApplications);WindowPositions.Prepare(dropWindow,"drop-actions",Bounds,previewMode);dropWindow.Show();dropWindow.Activate();}catch(Exception e){content?.Dispose();OperationFeedback.Error(e.Message);}
    }
}
