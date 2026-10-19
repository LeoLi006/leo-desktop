using System.Diagnostics;
namespace LeoLauncher;

static class ApplicationDrop
{
    public static bool IsProgram(string path)=>File.Exists(path)&&new[]{".exe",".lnk"}.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase);
    public static LocalApplication Draft(string path)=>new(Guid.NewGuid().ToString("N"),Path.GetFileNameWithoutExtension(path)[..Math.Min(24,Path.GetFileNameWithoutExtension(path).Length)],Path.GetFullPath(path));
}
sealed class ApplicationEditor:BarcaWindow
{
    public LocalApplication? Result{get;private set;}
    public ApplicationEditor(LocalApplication? existing,string? builtin,Preferences? prefs)
    {
        Text=existing==null&&builtin==null?"新建应用模块":"编辑应用模块";ClientSize=new(660,570);StartPosition=FormStartPosition.CenterParent;Controls.Add(BarcaUi.Chrome(this,Text,"支持启动参数、工作目录和管理员启动；修改仅保存在本机。"));
        TextBox Field(string label,int y,string value,int width=398){Controls.Add(new Label{Text=label,Bounds=new(24,y,105,28),ForeColor=BarcaUi.Muted});var field=new TextBox{Text=value,Bounds=new(135,y,width,28),BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text};Controls.Add(field);return field;}
        var name=Field("名称",111,existing?.Name??(builtin!=null?ModulePaths.Entries.First(e=>e.Id==builtin).Name:""));name.MaxLength=24;name.ReadOnly=builtin!=null;
        var path=Field("程序路径",157,existing?.Path??(builtin!=null?prefs!.ModuleExecutables.GetValueOrDefault(builtin)??"":""));path.PlaceholderText=builtin!=null?"留空自动检测":"选择EXE或LNK文件";
        var icon=Field("快捷图标",203,existing?.Icon??(builtin!=null?prefs!.ModuleIcons.GetValueOrDefault(builtin)??"":""));icon.PlaceholderText="留空使用应用图标";
        var args=Field("启动参数",249,existing?.Arguments??"",500);args.PlaceholderText="例如：--fullscreen 或带引号的文件路径";args.Enabled=builtin==null;
        var working=Field("工作目录",295,existing?.WorkingDirectory??"");working.PlaceholderText="留空沿用默认目录";working.Enabled=builtin==null;
        void Browse(TextBox field,int y,string filter){var b=new BarcaButton{Text="选择…",Bounds=new(544,y-2,92,32)};b.Click+=(_,_)=>{using var picker=new OpenFileDialog{Filter=filter,CheckFileExists=true};if(picker.ShowDialog(this)==DialogResult.OK){field.Text=picker.FileName;if(field==path&&name.Text.Length==0)name.Text=Path.GetFileNameWithoutExtension(picker.FileName);}};Controls.Add(b);}Browse(path,157,"应用程序与快捷方式|*.exe;*.lnk");Browse(icon,203,"图标或图片|*.ico;*.png;*.jpg;*.jpeg;*.exe");
        var folder=new BarcaButton{Text="选择…",Bounds=new(544,293,92,32),Enabled=builtin==null};folder.Click+=(_,_)=>{using var picker=new FolderBrowserDialog();if(picker.ShowDialog(this)==DialogResult.OK)working.Text=picker.SelectedPath;};
        var enabled=new CheckBox{Text="启用模块",Checked=existing?.Enabled??(builtin==null||Modules.Enabled(prefs!,builtin)),Bounds=new(135,345,160,28),ForeColor=BarcaUi.Text};var admin=new CheckBox{Text="以管理员身份启动（Windows确认）",Checked=existing?.Administrator??false,Enabled=builtin==null,Bounds=new(135,381,450,28),ForeColor=BarcaUi.Text};
        var error=new Label{Bounds=new(24,466,470,80),ForeColor=Color.LightSalmon};var repair=new BarcaButton{Text="查找迁移后的程序…",Bounds=new(135,425,220,32)};repair.Click+=async(_,_)=>{try{repair.Enabled=false;string oldPath=path.Text;var candidates=await Task.Run(()=>ModuleRepair.Find(oldPath,builtin));if(IsDisposed)return;using var dialog=new RepairPathForm(path.Text,candidates);if(dialog.ShowDialog(this)==DialogResult.OK)path.Text=dialog.SelectedPath;}catch(Exception e){if(!IsDisposed)error.Text=e.Message;}finally{if(!IsDisposed)repair.Enabled=true;}};
        var save=new BarcaButton{Text="确定",Bounds=new(536,512,100,34)};save.Click+=(_,_)=>{try{string target=path.Text.Trim();if(builtin!=null)target=ModulePaths.Validate(builtin,target);var result=new LocalApplication(existing?.Id??Guid.NewGuid().ToString("N"),name.Text.Trim(),target,enabled.Checked,icon.Text.Trim(),args.Text,working.Text.Trim(),admin.Checked);if(builtin==null)LocalApplications.Validate(result);if(result.Icon.Length>0){using var image=LocalApplications.Icon(result.Icon);if(image==null)throw new ArgumentException("图标无法读取，请选择10MB以内的有效图片或ICO文件");}Result=result;if(builtin!=null)LocalApplications.SetEnabled(prefs!,builtin,enabled.Checked);DialogResult=DialogResult.OK;}catch(Exception e){error.Text=e.Message;}};Controls.AddRange([folder,enabled,admin,repair,error,save]);MinimumSize=MaximumSize=Size;
    }
}

static class ModuleRepair
{
    public static Func<IEnumerable<AppEntry>>? Applications;
    internal static string[] Find(string oldPath,string? builtin=null)
    {
        var names=builtin switch{"media"=>new[]{"PotPlayerMini64.exe","PotPlayerMini.exe","PotPlayer64.exe","PotPlayer.exe"},"music"=>new[]{"QQMusic.exe"},"clash"=>new[]{"clash-verge.exe"},"edge"=>new[]{"msedge.exe"},"terminal"=>new[]{"wt.exe","WindowsTerminal.exe","pwsh.exe","powershell.exe","cmd.exe"},_=>new[]{Path.GetFileName(oldPath)}};
        var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Candidate(string path){try{if(File.Exists(path)&&names.Contains(Path.GetFileName(path),StringComparer.OrdinalIgnoreCase))found.Add(Path.GetFullPath(path));}catch{}}
        foreach(var a in Applications?.Invoke()??[]){Candidate(a.ExePath);Candidate(a.Target);}
        foreach(string name in names)foreach(var hive in new[]{Microsoft.Win32.Registry.CurrentUser,Microsoft.Win32.Registry.LocalMachine})try{using var key=hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\"+name);if(key?.GetValue("") is string value)Candidate(value);}catch{}
        var roots=new List<string>();try{var parent=Directory.GetParent(oldPath);if(parent!=null){roots.Add(parent.FullName);if(parent.Parent!=null)roots.Add(parent.Parent.FullName);}}catch{}
        foreach(var root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}){
            foreach(string suffix in builtin switch{"edge"=>new[]{@"Microsoft\Edge\Application"},"music"=>new[]{@"Tencent\QQMusic",@"QQMusic"},"clash"=>new[]{@"Clash Verge",@"Programs\Clash Verge"},"media"=>new[]{@"DAUM\PotPlayer",@"PotPlayer"},_=>Array.Empty<string>()})roots.Add(Path.Combine(root,suffix));
        }
        foreach(string root in roots.Distinct(StringComparer.OrdinalIgnoreCase)){foreach(string name in names)Candidate(Path.Combine(root,name));try{foreach(var child in Directory.EnumerateDirectories(root).Take(120)){if(File.GetAttributes(child).HasFlag(FileAttributes.ReparsePoint))continue;foreach(string name in names)Candidate(Path.Combine(child,name));}}catch(IOException){}catch(UnauthorizedAccessException){}}
        return found.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).Take(30).ToArray();
    }
}
sealed class RepairPathForm:BarcaWindow
{
    public string? SelectedPath{get;private set;}
    public RepairPathForm(string old,string[] candidates)
    {
        Text="确认修复应用路径";ClientSize=new(710,410);StartPosition=FormStartPosition.CenterParent;Controls.Add(BarcaUi.Chrome(this,"程序位置发生变化","只匹配同名程序；确认后更新路径，不会自动启动候选。"));
        var list=new ListBox{Bounds=new(24,151,662,172),BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text,HorizontalScrollbar=true};list.Items.AddRange(candidates);if(list.Items.Count>0)list.SelectedIndex=0;Controls.Add(new Label{Text=candidates.Length>0?"发现以下候选，请确认正确位置：":"未找到候选，可手动选择程序。",Bounds=new(24,111,662,32),ForeColor=BarcaUi.Muted});
        var browse=new BarcaButton{Text="手动选择…",Bounds=new(24,350,140,34)};browse.Click+=(_,_)=>{using var picker=new OpenFileDialog{Filter="应用程序与快捷方式|*.exe;*.lnk",CheckFileExists=true,FileName=Path.GetFileName(old)};if(picker.ShowDialog(this)==DialogResult.OK){list.Items.Add(picker.FileName);list.SelectedItem=picker.FileName;}};
        var accept=new BarcaButton{Text="确认使用",Bounds=new(546,350,140,34)};accept.Click+=(_,_)=>{if(list.SelectedItem is string p){SelectedPath=p;DialogResult=DialogResult.OK;}};var cancel=new BarcaButton{Text="取消",Bounds=new(394,350,140,34),DialogResult=DialogResult.Cancel};AcceptButton=accept;CancelButton=cancel;SelectionKeys.Wire(list,()=>accept.PerformClick());Controls.AddRange([list,browse,cancel,accept]);MinimumSize=MaximumSize=Size;
    }
}
sealed partial class PetForm
{
    string? RepairBuiltinPath(string id,string old){if(InvokeRequired)return (string?)Invoke(()=>RepairBuiltinPath(id,old));string? repaired=ConfirmRepair(old,id);if(repaired!=null)RememberBuiltinRepair(id,repaired);return repaired;}
    void RememberBuiltinRepair(string id,string repaired){ModulePaths.Validate(id,repaired);var settings=LocalApplications.Clone(prefs);settings.ModuleExecutables[id]=repaired;if(!previewMode){if(id is "clash" or "media")ConfigMigration.Write(Store.FilePath,System.Text.Json.JsonSerializer.Serialize(settings));else Store.Save(settings);}prefs.ModuleExecutables[id]=repaired;if(id is "clash" or "media"){RegisterClientCapabilities();commands.CancelDisabled();}}
    string? ConfirmRepair(string old,string? builtin)
    {
        if(IsDisposed||Disposing)return null;if(InvokeRequired)return (string?)Invoke(()=>ConfirmRepair(old,builtin));
        using var form=new RepairPathForm(old,ModuleRepair.Find(old,builtin));WindowPositions.Prepare(form,"repair-path",Bounds,previewMode);return form.ShowDialog()==DialogResult.OK?form.SelectedPath:null;
    }
    LocalApplication RepairLocalApplication(LocalApplication app)
    {
        var path=ConfirmRepair(app.Path,null)??throw new OperationCanceledException("未修改应用路径");var next=LocalApplications.WithRepairedPath(app,path);LocalApplications.Validate(next);int index=prefs.ApplicationModules.FindIndex(a=>a.Id==app.Id);if(index<0)throw new InvalidOperationException("模块已移除");var settings=LocalApplications.Clone(prefs);settings.ApplicationModules[index]=next;if(!previewMode)Store.Save(settings);prefs.ApplicationModules[index]=next;launcher.RefreshModules();return next;
    }
    void AddApplicationModules(string[] paths)
    {
        foreach(string file in paths.Where(ApplicationDrop.IsProgram).Take(10)){
            launcher.Hide();using var form=new ApplicationEditor(ApplicationDrop.Draft(file),null,null);WindowPositions.Prepare(form,"new-application",Bounds,previewMode);if(form.ShowDialog()!=DialogResult.OK)continue;
            if(prefs.ApplicationModules.Any(a=>a.Path.Equals(form.Result!.Path,StringComparison.OrdinalIgnoreCase))){OperationFeedback.Show("此应用已在模块列表中，可到设置编辑。");continue;}var settings=LocalApplications.Clone(prefs);settings.ApplicationModules.Add(form.Result!);if(!previewMode)Store.Save(settings);prefs.ApplicationModules.Add(form.Result!);launcher.RefreshModules();OperationFeedback.Show("已添加 "+form.Result!.Name);
        }
    }
}
