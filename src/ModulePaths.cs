namespace LeoLauncher;

static class ModulePaths
{
    public static readonly (string Id,string Name)[] Entries=[("terminal","管理员终端"),("clash","Clash Verge"),("music","QQ 音乐"),("edge","Edge")];
    static Dictionary<string,string> current=new();
    internal static Dictionary<string,string> Snapshot()=>new(current);
    public static void Configure(Dictionary<string,string> paths)=>current=new(paths);
    public static string Validate(string id,string path)
    {
        path=path.Trim();if(path.Length==0)return "";
        if(!Path.IsPathFullyQualified(path)||!File.Exists(path))throw new ArgumentException("请选择已存在的应用程序文件。");
        var allowed=id switch{"music"=>new[]{"QQMusic.exe"},"clash"=>new[]{"clash-verge.exe"},"edge"=>new[]{"msedge.exe"},"terminal"=>new[]{"wt.exe","WindowsTerminal.exe","powershell.exe","pwsh.exe","cmd.exe"},_=>throw new ArgumentException("不支持的模块")};
        if(!allowed.Contains(Path.GetFileName(path),StringComparer.OrdinalIgnoreCase))throw new ArgumentException("所选程序与模块不匹配，请重新选择。");
        return Path.GetFullPath(path);
    }
    public static string? Selected(string id)
    {
        if(!current.TryGetValue(id,out var path)||string.IsNullOrWhiteSpace(path))return null;
        try{return Validate(id,path);}catch{throw new InvalidOperationException("手动指定的软件路径已失效，请在设置 → 功能 → 软件路径中重新选择。");}
    }
}

sealed class ModulePathsForm:BarcaWindow
{
    readonly Dictionary<string,TextBox> fields=new();
    public Dictionary<string,string> Paths{get;private set;}=new();
    public ModulePathsForm(Dictionary<string,string> paths)
    {
        Text="Leo · 软件路径";ClientSize=new Size(620,490);StartPosition=FormStartPosition.CenterParent;
        Controls.Add(BarcaUi.Chrome(this,"软件路径","留空自动检测；也可选择便携版或其他安装位置。"));
        int y=103;
        foreach(var (id,name) in ModulePaths.Entries){
            Controls.Add(new Label{Text=name,Bounds=new Rectangle(23,y,550,23),ForeColor=BarcaUi.Gold,Font=BarcaUi.Font(10,true)});
            var field=new TextBox{Text=paths.GetValueOrDefault(id)??"",PlaceholderText="自动检测（留空）",Bounds=new Rectangle(23,y+28,410,28),BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text,BorderStyle=BorderStyle.FixedSingle};fields[id]=field;
            var choose=new BarcaButton{Text="选择…",Bounds=new Rectangle(442,y+26,72,31)};choose.Click+=(_,_)=>{using var picker=new OpenFileDialog{Title="选择 "+name,Filter="应用程序 (*.exe)|*.exe",CheckFileExists=true};if(picker.ShowDialog(this)==DialogResult.OK)field.Text=picker.FileName;};
            var auto=new BarcaButton{Text="自动",Bounds=new Rectangle(522,y+26,72,31)};auto.Click+=(_,_)=>field.Clear();Controls.AddRange([field,choose,auto]);y+=76;
        }
        var error=new Label{Bounds=new Rectangle(23,416,390,51),ForeColor=Color.LightSalmon,Font=BarcaUi.Font(8.5f)};var save=new BarcaButton{Text="确定",Bounds=new Rectangle(499,431,95,34)};
        save.Click+=(_,_)=>{try{Paths=fields.Select(p=>(p.Key,Path:ModulePaths.Validate(p.Key,p.Value.Text))).Where(p=>p.Path.Length>0).ToDictionary(p=>p.Key,p=>p.Path);DialogResult=DialogResult.OK;}catch(Exception e){error.Text=e.Message;}};
        Controls.AddRange([error,save]);AcceptButton=save;
    }
}
