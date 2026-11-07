namespace LeoLauncher;
sealed class DropApplicationPicker:BarcaWindow
{
    public string ModuleId{get;private set;}="";public string Executable{get;private set;}="";public bool SystemChoice{get;private set;}
    public DropApplicationPicker(Preferences prefs,bool systemAvailable=true)
    {
        AutoScaleMode=AutoScaleMode.None;Text="选择打开方式";ClientSize=new(590,480);
        Controls.Add(BarcaUi.Chrome(this,"用哪个应用打开？","系统推荐由 Windows 提供；下方应用由你手动指定，不代表文件类型兼容。"));
        var system=new BarcaButton{Text="Windows 打开方式…",Bounds=new(24,105,542,36),Enabled=systemAvailable};
        system.Click+=(_,_)=>{SystemChoice=true;DialogResult=DialogResult.OK;};
        var label=new Label{Text="已配置应用（手动指定）",Bounds=new(24,158,542,26),ForeColor=BarcaUi.Gold,Font=BarcaUi.Font(10,true)};
        var apps=new ListBox{Bounds=new(24,189,542,160),BackColor=BarcaUi.Surface,ForeColor=BarcaUi.Text,BorderStyle=BorderStyle.None};
        foreach(var app in prefs.ApplicationModules.Where(Available))apps.Items.Add(new ComboChoice(app.Name+(app.Administrator?UiLanguage.T(" · 管理员"):""),app.Id));
        if(apps.Items.Count>0)apps.SelectedIndex=0;
        var help=new Label{Text="仅支持一项。管理员应用可能弹出 Windows 授权；应用是否支持该文件由应用决定。",Bounds=new(24,359,542,46),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)};
        var open=new BarcaButton{Text="使用所选应用",Bounds=new(386,423,180,34),Enabled=apps.Items.Count>0};
        open.Click+=(_,_)=>{if(apps.SelectedItem is ComboChoice{Value.Length:>0} choice){ModuleId=choice.Value;DialogResult=DialogResult.OK;}};
        var browse=new BarcaButton{Text="浏览其他程序…",Bounds=new(24,423,180,34)};
        browse.Click+=(_,_)=>{using var picker=new OpenFileDialog{Filter="桌面应用|*.exe",CheckFileExists=true};if(picker.ShowDialog(this)==DialogResult.OK){Executable=picker.FileName;DialogResult=DialogResult.OK;}};
        SelectionKeys.Wire(apps,()=>open.PerformClick());Controls.AddRange([system,label,apps,help,open,browse]);MinimumSize=MaximumSize=Size;
    }
    static bool Available(LocalApplication app){if(!app.Enabled)return false;try{LocalApplications.Validate(app);return true;}catch(Exception error)when(FeatureFaults.Recoverable(error)){return false;}}
    protected override bool ProcessCmdKey(ref Message message,Keys key){if(key==Keys.Escape){DialogResult=DialogResult.Cancel;return true;}if(key==Keys.Enter&&ActiveControl is Button button){button.PerformClick();return true;}return base.ProcessCmdKey(ref message,key);}
}
