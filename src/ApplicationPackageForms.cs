namespace LeoLauncher;

sealed partial class LocalApplicationsForm
{
    void AddPackageControls()
    {
        var import=new BarcaButton{Text="导入 / 更新模块包…",Bounds=new(24,454,224,34)};
        var export=new BarcaButton{Text="导出所选模块包…",Bounds=new(260,454,224,34)};
        import.Click+=(_,_)=>{try{ImportPackage();}catch(Exception e){status.Text="导入未完成："+e.Message;}};
        export.Click+=(_,_)=>{try{ExportPackage();}catch(Exception e){status.Text="导出未完成："+e.Message;}};
        Controls.AddRange([import,export]);
    }
    void DescribePackage()
    {
        var id=(list.SelectedItem as ComboChoice)?.Value??"";
        status.Text=draft.ApplicationModules.Any(x=>x.Id==id)?ApplicationPackages.Summary(draft,id)+"\n更改暂存；确定后还需保存设置。":"内置适配器 · 不能导出为应用模块包；本机软件路径由当前设置管理。";
    }
    void ImportPackage()
    {
        using var picker=new OpenFileDialog{Filter="Leo 应用模块包|*.leoapp.json|JSON|*.json",CheckFileExists=true};
        if(picker.ShowDialog(this)!=DialogResult.OK)return;
        var package=ApplicationPackages.Read(picker.FileName);ApplicationPackages.CheckUpdate(draft,package);
        var old=draft.ApplicationPackages.SingleOrDefault(x=>x.Value.Manifest.Id==package.Id);
        var local=old.Value==null?null:draft.ApplicationModules.FirstOrDefault(x=>x.Id==old.Key)??old.Value.Local;
        using var preview=new ApplicationPackagePreview(package,local);
        if(preview.ShowDialog(this)!=DialogResult.OK)return;
        // Even updates retain and show this computer's configuration. Nothing in a package can set it.
        using var editor=new ApplicationEditor(local??new(Guid.NewGuid().ToString("N"),package.Name,""),null,null);
        if(editor.ShowDialog(this)!=DialogResult.OK)return;
        draft=ApplicationPackages.Install(draft,package,editor.Result!);Reload(editor.Result!.Id);DescribePackage();
    }
    void ExportPackage()
    {
        var id=(list.SelectedItem as ComboChoice)?.Value??"";
        var app=draft.ApplicationModules.FirstOrDefault(x=>x.Id==id)??throw new ArgumentException("请选择自定义应用，内置适配器不能导出。");
        var manifest=draft.ApplicationPackages.TryGetValue(id,out var old)?old.Manifest:ApplicationPackages.Create(app);
        using var preview=new ApplicationPackagePreview(manifest,null,true);
        if(preview.ShowDialog(this)!=DialogResult.OK)return;
        manifest=preview.Result;
        if(old!=null&&Version.Parse(manifest.Version)<Version.Parse(old.Manifest.Version))throw new ArgumentException("导出版本不能低于本机记录的模块版本。");
        using var picker=new SaveFileDialog{Filter="Leo 应用模块包|*.leoapp.json",FileName="application.leoapp.json",OverwritePrompt=true};
        if(picker.ShowDialog(this)!=DialogResult.OK)return;
        AtomicJson.Write(picker.FileName,ApplicationPackages.Export(manifest));
        draft.ApplicationPackages[id]=new(manifest,app);Reload(id);status.Text="已导出纯描述包；路径、图标、参数、管理员选项和个人数据均未包含。请保存设置以保留模块包身份。";
    }
}

sealed class ApplicationPackagePreview:BarcaWindow
{
    public ApplicationPackage Result{get;private set;}
    public ApplicationPackagePreview(ApplicationPackage package,LocalApplication? local,bool export=false)
    {
        Result=package;Text=export?"分享前预览":"模块包预览";ClientSize=new(650,468);StartPosition=FormStartPosition.CenterParent;
        Controls.Add(BarcaUi.Chrome(this,Text,export?"只导出公开描述；请检查名称中是否含个人信息。":"只接受数据型应用包；不会下载依赖或启动程序。"));
        Controls.Add(new Label{Text="公开名称",Bounds=new(24,108,100,26),ForeColor=BarcaUi.Muted});
        var name=new TextBox{Text=package.Name,Bounds=new(132,106,486,29),MaxLength=24,ReadOnly=!export,BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text};
        Controls.Add(new Label{Text="模块版本",Bounds=new(24,151,100,26),ForeColor=BarcaUi.Muted});
        var version=new TextBox{Text=package.Version,Bounds=new(132,149,486,29),MaxLength=32,ReadOnly=!export,BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text};
        var text=new TextBox{Bounds=new(24,193,594,153),Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=BarcaUi.Surface,ForeColor=BarcaUi.Text,BorderStyle=BorderStyle.None,Text=$"身份：{package.Id}\r\n宿主：{package.HostMin} ～ {package.HostMax} · 协议{package.Protocol}\r\n依赖：Windows、应用启动适配器 v1\r\n"+(local==null?"程序路径需在本机填写；包不包含启动参数或管理员授权。":$"更新/重装沿用本机身份及配置，下一步可核对：\r\n{local.Path}\r\n参数：{local.Arguments}\r\n工作目录：{local.WorkingDirectory}\r\n管理员：{local.Administrator}")};
        var error=new Label{Bounds=new(24,354,594,40),ForeColor=Color.LightSalmon};
        var accept=new BarcaButton{Text=export?"选择导出位置…":"核对本机配置…",Bounds=new(422,410,196,34)};
        accept.Click+=(_,_)=>{try{Result=package with{Name=name.Text.Trim(),Version=version.Text.Trim()};ApplicationPackages.Validate(Result);DialogResult=DialogResult.OK;}catch(Exception e){error.Text=e.Message;}};
        var cancel=new BarcaButton{Text="取消",Bounds=new(304,410,104,34),DialogResult=DialogResult.Cancel};
        Controls.AddRange([name,version,text,error,accept,cancel]);BarcaScrollTheme.Attach(text);AcceptButton=accept;CancelButton=cancel;MinimumSize=MaximumSize=Size;
    }
}
