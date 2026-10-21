namespace LeoLauncher;
enum ModuleState { NotInstalled,NotConfigured,Invalid,Disabled,Waiting,Ready,Failed,Unknown }
sealed record ModuleStatus(string Id,string Name,ModuleState State,string Detail,string Action)
{
    public string Source{get;init;}="内置功能";
    public string Label=>State switch{ModuleState.NotInstalled=>"未安装",ModuleState.NotConfigured=>"未配置",ModuleState.Invalid=>"失效",ModuleState.Disabled=>"已停用",ModuleState.Waiting=>"等待就绪",ModuleState.Ready=>"检查通过",ModuleState.Unknown=>"未检测",_=>"失败"};
}
static class ModuleActivity
{
    static readonly Dictionary<string,long> tickets=new();static long sequence;
    public static long Begin(string id){lock(gate){long ticket=++sequence;tickets[id]=ticket;states[id]=(ModuleState.Waiting,"正在执行操作，等待软件或接口就绪…");return ticket;}}
    public static void Complete(string id,long ticket,ModuleState state,string detail){lock(gate)if(tickets.GetValueOrDefault(id)==ticket)states[id]=(state,detail);}
    static readonly object gate=new();static readonly Dictionary<string,(ModuleState State,string Detail)> states=new();
    public static void Set(string id,ModuleState state,string detail){lock(gate)states[id]=(state,detail);}
    public static (ModuleState State,string Detail)? Get(string id){lock(gate)return states.TryGetValue(id,out var value)?value:null;}
}
static partial class ModuleStatuses
{
    internal static ModuleStatus Resolve(string id,string name,bool enabled,string? configured,Func<bool> installed,Func<bool>? valid=null)
    {
        if(!enabled)return new(id,name,ModuleState.Disabled,"入口隐藏与停用是两回事；当前能力已停用。","启用模块");
        if(!string.IsNullOrWhiteSpace(configured)){try{if(valid?.Invoke()==false||!File.Exists(configured))return new(id,name,ModuleState.Invalid,"指定的软件路径已失效或不匹配。","修复路径");}catch{return new(id,name,ModuleState.Invalid,"指定的软件路径无法使用。","修复路径");}}
        bool found;try{found=installed();}catch{return new(id,name,ModuleState.Invalid,"软件路径检查失败。","修复路径");}
        if(!found)return new(id,name,ModuleState.NotInstalled,"未找到本地软件；安装后重新检查，或指定便携版。","选择路径");
        var active=ModuleActivity.Get(id);if(active is {} a&&a.State is ModuleState.Waiting or ModuleState.Failed)return new(id,name,a.State,a.Detail,a.State==ModuleState.Failed?"重新检测":"刷新状态");
        return new(id,name,ModuleState.Ready,"依赖检查通过；不代表外部软件已启动。","查看配置");
    }
    public static ModuleStatus[] Read(Preferences p)
    {
        using var passive=ModulePaths.PassiveRead();var list=new List<ModuleStatus>();
        foreach(var entry in EntryContributions.ReadBuiltins(p)){
            string? configured=p.ModuleExecutables.GetValueOrDefault(entry.Id);
            bool Installed()=>entry.Id switch{"football"=>true,"terminal"=>File.Exists(FootTools.TerminalStart().FileName),"clash"=>ClashProxy.ClientPath()!=null,"music"=>QQMusic.ClientPath()!=null,"edge"=>File.Exists(FootTools.EdgeUrlStart("https://www.bing.com").FileName),"media"=>MediaControl.PotPlayerPath()!=null,_=>false};
            list.Add(Resolve(entry.Id,entry.Name,entry.Enabled,configured,Installed,()=>ModulePaths.Validate(entry.Id,configured??"").Length>0));
        }
        list.AddRange(ReadConfigured(p,new ExtensionStore(Path.Combine(Store.Root,"modules")){Preferences=p},(action,expected)=>EntryContributions.ReadStatusContract(p,action,expected)));
        var config=ChatSettings.Load();bool api=config.ReadKey().Length>0&&config.Model.Length>0;
        list.Add(new("api","模型 API",api?ModuleState.Ready:ModuleState.NotConfigured,api?"配置已填写，连接需手动检测。":"请填写接口、模型及 Key；本地功能不依赖它。","API 配置"));
        return list.ToArray();
    }
}
sealed class ModuleStatusForm:BarcaWindow
{
    readonly Preferences prefs;readonly Action<string> handle;readonly Func<ModuleStatus[]> read;readonly ToolTip details=new();readonly FlowLayoutPanel rows=new(){Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new(18,8,12,8)};
    readonly System.Windows.Forms.Timer timer=new(){Interval=1500};string signature="";
    public ModuleStatusForm(Preferences p,Action<string> action,Func<ModuleStatus[]>? statusReader=null){prefs=p;handle=action;read=statusReader??(()=>ModuleStatuses.Read(prefs));Text="Leo · 模块状态";ClientSize=new(720,610);var header=BarcaUi.Chrome(this,"模块状态","只读检测不会启动软件；操作失败不会误报为未安装。");Controls.Add(rows);Controls.Add(header);FeatureFaults.Timer(timer,"模块状态检查",Render);Shown+=(_,_)=>{if(FeatureFaults.Run("模块状态检查",Render))timer.Start();};MinimumSize=MaximumSize=Size;}
    void Render(){var data=read();string next=string.Join("|",data.Select(s=>s.ToString()));if(next==signature)return;signature=next;var position=rows.AutoScrollPosition;rows.SuspendLayout();details.RemoveAll();foreach(Control c in rows.Controls.Cast<Control>().ToArray())c.Dispose();foreach(var s in data){var row=new RoundedSurface{Tag=s.Id,Size=new(660,108),Margin=new(0,0,0,9),BackColor=BarcaUi.Surface};row.Controls.Add(new Label{Text=s.Name,Bounds=new(14,12,300,25),ForeColor=BarcaUi.Text,Font=BarcaUi.Font(10,true)});row.Controls.Add(new Label{Text=s.Label,Bounds=new(330,13,140,24),ForeColor=s.State is ModuleState.Failed or ModuleState.Invalid?Color.LightSalmon:BarcaUi.Gold});var origin=new Label{Text=s.Source,Bounds=new(14,38,620,20),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f),AutoEllipsis=true};var note=new Label{Text=s.Detail,Bounds=new(14,60,475,43),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9),AutoEllipsis=true};details.SetToolTip(origin,s.Source);details.SetToolTip(note,s.Detail);row.Controls.Add(origin);row.Controls.Add(note);var fix=new BarcaButton{Text=s.Action,Bounds=new(500,56,145,32)};fix.Click+=(_,_)=>{if(s.Action!="管理扩展"&&s.State is ModuleState.Waiting or ModuleState.Failed){if(s.State==ModuleState.Failed)ModuleActivity.Set(s.Id,ModuleState.Ready,"已重新检测；可从原入口重试操作。");signature="";Render();return;}handle(s.Id);signature="";Render();};row.Controls.Add(fix);rows.Controls.Add(row);}rows.AutoScrollPosition=new(-position.X,-position.Y);rows.ResumeLayout();}
    protected override void Dispose(bool disposing){if(disposing){timer.Dispose();details.Dispose();}base.Dispose(disposing);}
}
