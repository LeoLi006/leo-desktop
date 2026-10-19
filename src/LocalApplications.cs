using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;

sealed record LocalApplication(string Id,string Name,string Path,bool Enabled=true,string Icon="");
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
    public static void Validate(LocalApplication a){if(a.Id.Length!=32||!Guid.TryParseExact(a.Id,"N",out _)||string.IsNullOrWhiteSpace(a.Name)||a.Name.Length>24||a.Name.Any(char.IsControl))throw new ArgumentException("应用名称须为1～24个字符");if(!System.IO.Path.IsPathFullyQualified(a.Path)||!File.Exists(a.Path)||!new[]{".exe",".lnk"}.Contains(System.IO.Path.GetExtension(a.Path),StringComparer.OrdinalIgnoreCase))throw new ArgumentException("请选择已存在的 EXE 程序或 LNK 快捷方式。");}
    public static Bitmap? Icon(string? path){if(string.IsNullOrWhiteSpace(path)||!File.Exists(path))return null;try{if(new FileInfo(path).Length>10_000_000)return null;if(new[]{".exe",".lnk"}.Contains(System.IO.Path.GetExtension(path),StringComparer.OrdinalIgnoreCase)){using var icon=System.Drawing.Icon.ExtractAssociatedIcon(path);return icon?.ToBitmap();}using var image=Image.FromFile(path);return new Bitmap(image,new Size(32,32));}catch{return null;}}
    public static void SetEnabled(Preferences p,string id,bool value){switch(id){case "terminal":p.TerminalModule=value;break;case "clash":p.ClashModule=value;break;case "music":p.MusicModule=value;break;case "edge":p.EdgeModule=value;break;case "media":p.MediaModule=value;break;}}
}
sealed class LocalApplicationsForm:BarcaWindow
{
    readonly Preferences draft;readonly ListBox list=new();readonly Label status=new();
    public LocalApplicationsForm(Preferences output)
    {
        draft=LocalApplications.Clone(output);Text="应用模块";ClientSize=new(680,540);StartPosition=FormStartPosition.CenterParent;Controls.Add(BarcaUi.Chrome(this,"应用模块","统一管理开关、程序路径与图标；自定义应用只负责启动。"));
        list.Bounds=new(24,105,632,292);list.BackColor=BarcaUi.Raised;list.ForeColor=BarcaUi.Text;list.Font=BarcaUi.Font(11);list.BorderStyle=BorderStyle.None;list.ItemHeight=34;Controls.Add(list);
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
sealed class ApplicationEditor:BarcaWindow
{
    public LocalApplication? Result{get;private set;}
    public ApplicationEditor(LocalApplication? existing,string? builtin,Preferences? prefs)
    {
        Text=existing==null&&builtin==null?"新建应用模块":"编辑应用模块";ClientSize=new(620,405);StartPosition=FormStartPosition.CenterParent;Controls.Add(BarcaUi.Chrome(this,Text,"路径和图标保存在本机；不会复制或上传你的应用文件。"));
        TextBox Field(string label,int y,string value){Controls.Add(new Label{Text=label,Bounds=new(24,y,90,28),ForeColor=BarcaUi.Muted});var field=new TextBox{Text=value,Bounds=new(115,y,365,28),BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text};Controls.Add(field);return field;}
        var name=Field("名称",111,existing?.Name??(builtin!=null?ModulePaths.Entries.First(e=>e.Id==builtin).Name:""));name.MaxLength=24;name.ReadOnly=builtin!=null;
        var path=Field("程序路径",163,existing?.Path??(builtin!=null?prefs!.ModuleExecutables.GetValueOrDefault(builtin)??"":""));path.PlaceholderText=builtin!=null?"留空自动检测":"选择 EXE 或 LNK 文件";
        var icon=Field("快捷图标",215,existing?.Icon??(builtin!=null?prefs!.ModuleIcons.GetValueOrDefault(builtin)??"":""));icon.PlaceholderText="留空使用默认图标";
        void Browse(TextBox field,int y,string filter){var b=new BarcaButton{Text="选择…",Bounds=new(493,y-2,103,32)};b.Click+=(_,_)=>{using var picker=new OpenFileDialog{Filter=filter,CheckFileExists=true};if(picker.ShowDialog(this)==DialogResult.OK)field.Text=picker.FileName;};Controls.Add(b);}Browse(path,163,"应用程序与快捷方式|*.exe;*.lnk");Browse(icon,215,"图标或图片|*.ico;*.png;*.jpg;*.jpeg;*.exe");
        var enabled=new CheckBox{Text="启用该应用模块",Checked=existing?.Enabled??(builtin==null||Modules.Enabled(prefs!,builtin)),Bounds=new(115,266,350,28),ForeColor=BarcaUi.Text};Controls.Add(enabled);var error=new Label{Bounds=new(24,309,430,75),ForeColor=Color.LightSalmon};var save=new BarcaButton{Text="确定",Bounds=new(493,344,103,34)};save.Click+=(_,_)=>{try{string target=path.Text.Trim();if(builtin!=null)target=ModulePaths.Validate(builtin,target);var result=new LocalApplication(existing?.Id??Guid.NewGuid().ToString("N"),name.Text.Trim(),target,enabled.Checked,icon.Text.Trim());if(builtin==null)LocalApplications.Validate(result);if(result.Icon.Length>0){using var image=LocalApplications.Icon(result.Icon);if(image==null)throw new ArgumentException("图标无法读取，请选择10MB以内的有效图片或ICO文件");}Result=result;if(builtin!=null)LocalApplications.SetEnabled(prefs!,builtin,enabled.Checked);DialogResult=DialogResult.OK;}catch(Exception e){error.Text=e.Message;}};Controls.AddRange([error,save]);MinimumSize=MaximumSize=Size;
    }
}
sealed partial class LauncherForm
{
    readonly ToolTip shortcutTips=new();
    void RefreshShortcuts()
    {
        if(shortcutRow==null)return;foreach(Control old in shortcutRow.Controls.Cast<Control>().ToArray()){shortcutRow.Controls.Remove(old);if(old!=footballShortcut){old.Dispose();}}
        footballShortcut.Visible=prefs.FootballModule;footballShortcut.Margin=new Padding(2,3,2,0);if(prefs.FootballModule)shortcutRow.Controls.Add(footballShortcut);
        void Add(string id,string name,string glyph,bool enabled,Action action,string? iconPath=null){if(!enabled)return;var button=new GlyphButton(glyph){Width=28,Height=24,Margin=new Padding(2,3,2,0),AccessibleName=name,FlatStyle=FlatStyle.Flat};button.FlatAppearance.BorderSize=0;var image=LocalApplications.Icon(iconPath??prefs.ModuleIcons.GetValueOrDefault(id));if(image!=null){button.Image=image;button.ImageAlign=ContentAlignment.MiddleCenter;}button.Disposed+=(_,_)=>{button.Image?.Dispose();};button.Click+=(_,_)=>{try{action();}catch(Exception e){OperationFeedback.Error(e.Message,()=>{action();return Task.CompletedTask;});}};shortcutTips.SetToolTip(button,name+" · 图标较多时在此处滚动鼠标滚轮");shortcutRow.Controls.Add(button);}
        Add("music","QQ音乐点歌","music",prefs.MusicModule,()=>OpenMusicFromAssistant());
        Add("media","PotPlayer · 媒体控制","play",prefs.MediaModule,()=>MediaRequested?.Invoke());
        foreach(var app in prefs.ApplicationModules??[])Add(app.Id,app.Name,"terminal",app.Enabled,async()=>{try{if(ActionRequested!=null)await ActionRequested("local:"+app.Id,"",CancellationToken.None);else if(!preview){LocalApplications.Validate(app);Process.Start(new ProcessStartInfo(app.Path){UseShellExecute=true});}}catch(Exception e){OperationFeedback.Show(e.Message);}},app.Icon.Length>0?app.Icon:app.Path);
    }
}
