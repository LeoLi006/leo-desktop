using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;

sealed record LocalApplication(string Id,string Name,string Path,bool Enabled=true,string Icon="",string Arguments="",string WorkingDirectory="",bool Administrator=false);
sealed class LocalApplicationModule(LocalApplication app,Func<string,string,CancellationToken,Task<string>> execute):ILeoModule
{public string Id=>"local:"+app.Id;public string Name=>app.Name;public string Version=>"1.0";public bool Enabled=>app.Enabled;public IReadOnlyList<string> Actions=>[Id];public Task<string> ExecuteAsync(ActionRequest request,CancellationToken token){if(!Enabled)throw new InvalidOperationException(Name+"已停用");return execute("local_app_open",app.Id,token);}}
static class LocalPath
{
    public static string Expand(string text)=>Environment.ExpandEnvironmentVariables(text.Trim().Trim('"'));
    public static bool LooksLike(string text){var p=Expand(text);return System.IO.Path.IsPathFullyQualified(p)&&!p.Contains("://");}
    public static string Resolve(string text){string path=Expand(text);if(!LooksLike(path)||!File.Exists(path)&&!Directory.Exists(path))throw new IOException("文件或文件夹不存在，请检查路径。");return System.IO.Path.GetFullPath(path);}
}
static class LocalApplications
{
    public static Preferences Clone(Preferences p)=>JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(p))!;
    public static void Validate(LocalApplication a){if(a.Id.Length!=32||!Guid.TryParseExact(a.Id,"N",out _)||string.IsNullOrWhiteSpace(a.Name)||a.Name.Length>24||a.Name.Any(char.IsControl))throw new ArgumentException("应用名称须为1～24个字符");if(!System.IO.Path.IsPathFullyQualified(a.Path)||!File.Exists(a.Path)||!new[]{".exe",".lnk"}.Contains(System.IO.Path.GetExtension(a.Path),StringComparer.OrdinalIgnoreCase))throw new ArgumentException("请选择已存在的 EXE 程序或 LNK 快捷方式。");if(a.Arguments.Length>8192||a.Arguments.Any(c=>c is '\0' or '\r' or '\n'))throw new ArgumentException("启动参数过长或含无效字符");if(a.WorkingDirectory.Length>0&&(!System.IO.Path.IsPathFullyQualified(a.WorkingDirectory)||!Directory.Exists(a.WorkingDirectory)))throw new ArgumentException("工作目录不存在");}
    public static LocalApplication WithRepairedPath(LocalApplication a,string path)=>a with{Path=path,WorkingDirectory=a.WorkingDirectory.Length>0&&a.WorkingDirectory.Equals(System.IO.Path.GetDirectoryName(a.Path),StringComparison.OrdinalIgnoreCase)?System.IO.Path.GetDirectoryName(path)!:a.WorkingDirectory};
    public static ProcessStartInfo Start(LocalApplication a){Validate(a);return new(a.Path){UseShellExecute=true,Arguments=a.Arguments,WorkingDirectory=a.WorkingDirectory,Verb=a.Administrator?"runas":""};}
    public static Bitmap? Icon(string? path){if(string.IsNullOrWhiteSpace(path)||!File.Exists(path))return null;try{if(new FileInfo(path).Length>10_000_000)return null;if(new[]{".exe",".lnk"}.Contains(System.IO.Path.GetExtension(path),StringComparer.OrdinalIgnoreCase)){using var icon=System.Drawing.Icon.ExtractAssociatedIcon(path);return icon?.ToBitmap();}using var image=Image.FromFile(path);return new Bitmap(image,new Size(32,32));}catch{return null;}}
    public static void SetEnabled(Preferences p,string id,bool value){switch(id){case "terminal":p.TerminalModule=value;break;case "clash":p.ClashModule=value;break;case "music":p.MusicModule=value;break;case "edge":p.EdgeModule=value;break;case "media":p.MediaModule=value;break;}}
}
sealed class LocalApplicationsForm:BarcaWindow
{
    readonly Preferences draft;readonly ListBox list=new();readonly Label status=new();
    public LocalApplicationsForm(Preferences output)
    {
        draft=LocalApplications.Clone(output);Text="应用模块";ClientSize=new(680,540);StartPosition=FormStartPosition.CenterParent;Controls.Add(BarcaUi.Chrome(this,"应用模块","统一管理开关、程序路径与图标；自定义应用只负责启动。"));
        list.Bounds=new(24,105,632,292);list.BackColor=BarcaUi.Raised;list.ForeColor=BarcaUi.Text;list.Font=BarcaUi.Font(11);list.BorderStyle=BorderStyle.None;list.ItemHeight=34;Controls.Add(list);AllowDrop=true;DragEnter+=(_,e)=>e.Effect=e.Data?.GetData(DataFormats.FileDrop) is string[] paths&&paths.Any(ApplicationDrop.IsProgram)?DragDropEffects.Copy:DragDropEffects.None;DragDrop+=(_,e)=>{if(e.Data?.GetData(DataFormats.FileDrop) is not string[] paths)return;foreach(string file in paths.Where(ApplicationDrop.IsProgram).Take(10)){using var editor=new ApplicationEditor(ApplicationDrop.Draft(file),null,null);if(editor.ShowDialog(this)==DialogResult.OK){draft.ApplicationModules.Add(editor.Result!);Reload(editor.Result!.Id);}}};
        var row=new FlowLayoutPanel{Bounds=new(20,408,640,42),WrapContents=false};foreach(var title in new[]{"新建应用","编辑…","启用 / 停用","移除"}){var button=new BarcaButton{Text=title,Width=146,Height=34};button.Click+=(_,_)=>{try{Change(title);}catch(Exception e){status.Text=e.Message;}};row.Controls.Add(button);}Controls.Add(row);
        status.SetBounds(24,456,430,65);status.Font=BarcaUi.Font(9);status.ForeColor=BarcaUi.Muted;status.Text="完成后点击确定，再保存设置；停用不会关闭本机软件。";var save=new BarcaButton{Text="确定",Bounds=new(548,480,108,34)};save.Click+=(_,_)=>{output.ApplicationModules=draft.ApplicationModules.ToList();output.ModuleExecutables=new(draft.ModuleExecutables);output.ModuleIcons=new(draft.ModuleIcons);foreach(var entry in ModulePaths.Entries)LocalApplications.SetEnabled(output,entry.Id,Modules.Enabled(draft,entry.Id));DialogResult=DialogResult.OK;};Controls.AddRange([status,save]);Reload();MinimumSize=MaximumSize=Size;
    }
    void Reload(string? selected=null){list.Items.Clear();foreach(var b in ModulePaths.Entries)list.Items.Add(new ComboChoice((Modules.Enabled(draft,b.Id)?"● ":"○ ")+b.Name+"  · 内置适配器",b.Id));foreach(var a in draft.ApplicationModules)list.Items.Add(new ComboChoice((a.Enabled?"● ":"○ ")+a.Name+"  · 自定义应用",a.Id));list.SelectedItem=list.Items.Cast<ComboChoice>().FirstOrDefault(c=>c.Value==selected)??list.Items[0];}
    void Change(string operation)
    {
        string id=(list.SelectedItem as ComboChoice)?.Value??"";bool builtin=ModulePaths.Entries.Any(e=>e.Id==id);var app=draft.ApplicationModules.FirstOrDefault(a=>a.Id==id);
        if(operation=="新建应用"){using var editor=new ApplicationEditor(null,null,null);if(editor.ShowDialog(this)==DialogResult.OK){draft.ApplicationModules.Add(editor.Result!);Reload(editor.Result!.Id);}return;}
        if(operation=="启用 / 停用"){if(builtin)LocalApplications.SetEnabled(draft,id,!Modules.Enabled(draft,id));else if(app!=null)draft.ApplicationModules[draft.ApplicationModules.IndexOf(app)]=app with{Enabled=!app.Enabled};Reload(id);return;}
        if(operation=="移除"){if(builtin){LocalApplications.SetEnabled(draft,id,false);status.Text="内置适配器已停用；可随时重新开启。";}else draft.ApplicationModules.RemoveAll(a=>a.Id==id);Reload();return;}
        using var edit=new ApplicationEditor(app,builtin?id:null,draft);if(edit.ShowDialog(this)!=DialogResult.OK)return;if(builtin){string path=edit.Result!.Path;if(path.Length==0)draft.ModuleExecutables.Remove(id);else draft.ModuleExecutables[id]=path;draft.ModuleIcons[id]=edit.Result.Icon;}else if(app!=null)draft.ApplicationModules[draft.ApplicationModules.IndexOf(app)]=edit.Result!;Reload(id);
    }
}
