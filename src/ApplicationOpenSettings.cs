namespace LeoLauncher;

sealed class ApplicationOpenSettings:BarcaWindow
{
    public Preferences? Result{get;private set;}
    readonly Preferences draft;readonly ListBox list=new();readonly TextBox name=new(),types=new();
    readonly ComboBox application=new();readonly CheckBox folders=new(),multiple=new(),enabled=new();readonly Label detail=new();
    string current="";bool loading;
    public ApplicationOpenSettings(Preferences source)
    {
        draft=LocalApplications.Clone(source);draft.ApplicationOpenRules??=[];AutoScaleMode=AutoScaleMode.None;Text="应用打开能力";ClientSize=new(720,570);
        Controls.Add(BarcaUi.Chrome(this,"应用打开能力","文件类型匹配不是兼容保证；程序、图标和启动选项复用应用模块。"));
        list.SetBounds(20,105,180,360);list.BackColor=BarcaUi.Surface;list.ForeColor=BarcaUi.Text;list.BorderStyle=BorderStyle.None;
        void Label(string text,int y){Controls.Add(new Label{Text=text,Bounds=new(220,y,475,23),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)});}
        void Field(Control c,int y,int h=28){c.SetBounds(220,y,475,h);c.BackColor=BarcaUi.Raised;c.ForeColor=BarcaUi.Text;Controls.Add(c);}
        Label("显示名称",104);Field(name,130);name.MaxLength=24;
        Label("文件扩展名（空格或逗号分隔）",164);Field(types,190,52);types.Multiline=true;
        Label("关联应用模块",250);Field(application,276);application.DropDownStyle=ComboBoxStyle.DropDownList;
        folders.Text="支持文件夹";multiple.Text="支持多文件（由应用能力决定）";enabled.Text="启用此打开能力";
        folders.SetBounds(220,316,160,27);multiple.SetBounds(385,316,310,27);enabled.SetBounds(220,350,240,27);
        foreach(var check in new[]{folders,multiple,enabled}){check.ForeColor=BarcaUi.Text;Controls.Add(check);}
        var edit=new BarcaButton{Text="程序、图标与启动选项…",Bounds=new(220,389,475,32)};edit.Click+=(_,_)=>EditApplication();
        detail.SetBounds(220,433,475,72);detail.ForeColor=BarcaUi.Muted;detail.Font=BarcaUi.Font(8.5f);
        var add=new BarcaButton{Text="添加能力…",Bounds=new(20,480,180,30)};add.Click+=(_,_)=>{
            try{Flush();var r=new ApplicationOpenRule("custom-"+Guid.NewGuid().ToString("N")[..12],"新应用",[]);draft.ApplicationOpenRules.Add(r);Reload(r.Id);}catch(Exception e){detail.Text=UiLanguage.T(e.Message);}
        };
        var save=new BarcaButton{Text="保存",Bounds=new(583,520,112,32)};save.Click+=(_,_)=>{try{Flush();ApplicationOpenCapabilities.Validate(draft.ApplicationOpenRules);Result=draft;DialogResult=DialogResult.OK;}catch(Exception e){detail.Text=UiLanguage.T(e.Message);}};
        list.SelectedIndexChanged+=(_,_)=>{if(loading||list.SelectedItem is not ComboChoice choice)return;try{Flush();LoadRule(choice.Value);}catch(Exception e){detail.Text=UiLanguage.T(e.Message);loading=true;list.SelectedItem=list.Items.Cast<ComboChoice>().FirstOrDefault(c=>c.Value==current);loading=false;}};
        Controls.AddRange([list,edit,detail,add,save]);Reload("code");MinimumSize=MaximumSize=Size;
    }
    void Reload(string id)
    {
        loading=true;list.Items.Clear();foreach(var r in ApplicationOpenCapabilities.Rules(draft))list.Items.Add(new ComboChoice(UiLanguage.T(r.Name),r.Id));
        list.SelectedItem=list.Items.Cast<ComboChoice>().FirstOrDefault(c=>c.Value==id);loading=false;LoadRule(id);
    }
    void LoadRule(string id)
    {
        current=id;var r=ApplicationOpenCapabilities.Rules(draft).Single(r=>r.Id==id);name.Text=r.Name;types.Text=string.Join(" ",r.Extensions);folders.Checked=r.Folders;multiple.Checked=r.Multiple;enabled.Checked=r.Enabled;
        folders.Enabled=multiple.Enabled=r.Adapter!="media";
        application.Items.Clear();application.Items.Add(new ComboChoice(UiLanguage.T("自动检测 / 未指定"),""));foreach(var app in draft.ApplicationModules)application.Items.Add(new ComboChoice(app.Name+(app.Enabled?"":UiLanguage.T("（已停用）")),app.Id));
        if(r.ModuleId.Length>0&&!draft.ApplicationModules.Any(a=>a.Id==r.ModuleId))application.Items.Add(new ComboChoice(UiLanguage.T("应用模块已移除"),r.ModuleId));
        application.SelectedItem=application.Items.Cast<ComboChoice>().FirstOrDefault(c=>c.Value==r.ModuleId)??application.Items[0];
        var resolved=ApplicationOpenCapabilities.Resolve(draft).Single(x=>x.Rule.Id==id);
        detail.Text=UiLanguage.T(Status(resolved.State))+"\n"+resolved.Path;UiLanguage.Preserve(detail);
    }
    internal static string Status(string state)=>state switch{"ready"=>"可用 · 仅确认程序路径有效","disabled"=>"模块或打开能力已停用","removed"=>"应用模块已移除",_=>"尚未检测到可用程序，请手动指定"};
    void Flush()
    {
        if(current.Length==0)return;var old=ApplicationOpenCapabilities.Rules(draft).Single(r=>r.Id==current);
        var rule=old with{Name=name.Text.Trim(),Extensions=ApplicationOpenCapabilities.Ext(types.Text),Folders=folders.Checked,Multiple=multiple.Checked,Enabled=enabled.Checked,ModuleId=(application.SelectedItem as ComboChoice)?.Value??""};
        ApplicationOpenCapabilities.Validate([rule]);draft.ApplicationOpenRules.RemoveAll(r=>r.Id==current);draft.ApplicationOpenRules.Add(rule);
        if(rule.ModuleId.Length>0&&draft.ApplicationModules.FirstOrDefault(a=>a.Id==rule.ModuleId) is {} app&&app.Enabled&&rule.Enabled){
            LocalApplications.Validate(app);string actual=ApplicationOpenCapabilities.Identity(app.Path);if(!File.Exists(actual))throw new IOException("应用已停用、移除或程序路径无效。");
            if(rule.Adapter=="code"&&!Path.GetFileName(actual).Equals("Code.exe",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("请选择 VS Code 的 Code.exe");
            if(rule.Adapter=="media")draft.ModuleExecutables["media"]=ModulePaths.Validate("media",actual);
        }
    }
    void EditApplication()
    {
        try{
            Flush();var resolved=ApplicationOpenCapabilities.Resolve(draft).Single(x=>x.Rule.Id==current);
            using var editor=new ApplicationEditor(resolved.Application,null,null);if(editor.ShowDialog(this)!=DialogResult.OK)return;
            var app=editor.Result!;if(resolved.Rule.Adapter=="media")ModulePaths.Validate("media",ApplicationOpenCapabilities.Identity(app.Path));
            var existing=draft.ApplicationModules.FirstOrDefault(a=>a.Id==app.Id)??draft.ApplicationModules.FirstOrDefault(a=>a.Path.Equals(app.Path,StringComparison.OrdinalIgnoreCase));
            if(existing!=null){app=app with{Id=existing.Id};draft.ApplicationModules[draft.ApplicationModules.IndexOf(existing)]=app;}else draft.ApplicationModules.Add(app);
            var rule=resolved.Rule with{ModuleId=app.Id};draft.ApplicationOpenRules.RemoveAll(r=>r.Id==current);draft.ApplicationOpenRules.Add(rule);LoadRule(current);Flush();
        }catch(Exception e){detail.Text=UiLanguage.T(e.Message);}
    }
    protected override bool ProcessCmdKey(ref Message message,Keys key){if(key==Keys.Escape){DialogResult=DialogResult.Cancel;return true;}if(key==Keys.Enter&&ActiveControl is Button button){button.PerformClick();return true;}return base.ProcessCmdKey(ref message,key);}
}
