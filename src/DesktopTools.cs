using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;
sealed class DesktopToolModule(string id,string name,Func<bool> enabled,string[] actions,Func<ActionRequest,CancellationToken,Task<ActionPayload>> run):IStructuredLeoModule,IActionRequestAdapter,IModuleContractSource,IModuleEntryContributor
{
    public string Id=>id;public string Name=>name;public string Version=>"1.0";public bool Enabled=>enabled();public bool ConfigurationIsCurrent=>Enabled;public int MaximumInputLength=>0;public IReadOnlyList<string> Actions=>actions;
    public CapabilityDefinition Describe(string action)=>new(action,Id,"in-process","empty-object",action=="system_snapshot"?"read":action is "system_open" or "mail_client_settings"?"window":"external","coalesce-inflight",10000);
    public ModuleContract DescribeModule()=>ModuleContracts.Build(this,"desktop-tools",Name,"optional","host.desktop-tools",[],_ =>("object","只接受空对象，不接受路径、参数或权限覆盖。","desktop-tools-result","状态为带采样时间的只读快照；外部启动仅表示请求已接收。",true,["snapshot","window-shown","accepted","unknown"]));
    public DependencyState[] ReadDependencies()=>[];
    public EntryContribution[] DescribeEntries()=>[new(Id+"/open",Id,actions[0],Name,"",Enabled,EntryDisplay.Shortcut,ModuleEntryContributions.TargetPrefix+Id+"/open",Id=="system-monitor"?["电脑状态","系统监测","CPU","内存"]:["邮箱","邮件客户端"],"module-action"){Parameters="{}",Glyph=Id=="system-monitor"?"media":"notes",Hint=Id=="system-monitor"?"资源速览与任务管理器":"使用现有客户端收发邮件",MenuActions=Id=="mail-client"?[new("打开邮箱客户端",actions[0],"{}"),new("设置邮箱客户端","mail_client_settings","{}")]:[new(Name,actions[0],"{}")]}];
    public ActionRequest Adapt(StructuredActionRequest r){if(r.Protocol!="leo-action/1"||!Actions.Contains(r.ActionId)||r.Parameters.ValueKind!=JsonValueKind.Object||r.Parameters.EnumerateObject().Any())throw new ArgumentException("只接受空参数");return new(r.ActionId,"",r.Source,r.RequestId,r.TraceId,r.Deadline);}
    public Task<ActionPayload> ExecuteStructuredAsync(ActionRequest r,CancellationToken token){token.ThrowIfCancellationRequested();if(!Enabled)throw new InvalidOperationException(Name+"已停用");if(!Actions.Contains(r.Action)||r.Value.Length!=0)throw new ArgumentException("动作或参数无效");return run(r,token);}
    public async Task<string> ExecuteAsync(ActionRequest r,CancellationToken t)=>(await ExecuteStructuredAsync(r,t)).Message;
    public string FailureCode(Exception e)=>e is ArgumentException?"invalid_request":"desktop_tool_failed";
}
static class DesktopClients
{
    internal static string[] Candidates(bool mail)
    {
        string[] roots=[Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs")];
        string[] names=mail?["Foxmail/Foxmail.exe","Mozilla Thunderbird/thunderbird.exe","Microsoft Office/root/Office16/OUTLOOK.EXE"]:["Huorong/Sysdiag/bin/HipsMain.exe"];
        return roots.Where(r=>r.Length>0).SelectMany(r=>names.Select(n=>Path.Combine(r,n))).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static string? Resolve(string configured,bool mail){if(!string.IsNullOrWhiteSpace(configured))return File.Exists(configured)?configured:null;var candidates=Candidates(mail);return candidates.Length==1?candidates[0]:null;}
    public static Task<bool> Open(string path,CancellationToken token)=>ApplicationCapabilityHost.StartProcess(LocalApplications.Start(new("0123456789abcdef0123456789abcdef","桌面工具",path)),token);
}
sealed partial class PetForm
{
    ComputerMonitor? computerMonitor;ComputerStatusForm? computerWindow;MailClientForm? desktopMailWindow;
    void RegisterDesktopTools()
    {
        computerMonitor=new(new WindowsComputerSampler(),()=>!previewMode&&prefs.SystemMonitorModule,()=>prefs.SystemMonitorAlerts,()=>computerWindow?.Visible==true,message=>{if(!previewMode)OperationFeedback.Sink?.Invoke(new LeoNotice(message,Retry:()=>RunDesktopAction("system_open"),ActionLabel:"查看状态",Module:"system-monitor",Attention:true));});
        var ui=new ModuleWindowService(this);
        async Task<ActionPayload> Run(ActionRequest r,CancellationToken token){
            if(r.Action=="system_snapshot")return computerMonitor.Current is{} snapshot?new("电脑状态快照",JsonSerializer.SerializeToElement(snapshot),"snapshot",true,"已读取电脑状态快照。"):new("尚未采样，请先打开电脑状态。",Evidence:"unknown",PrivateData:true);
            if(r.Action=="mail_client_open"&&DesktopClients.Resolve(prefs.MailClientPath,true) is{} mailPath){token.ThrowIfCancellationRequested();if(!previewMode)await DesktopClients.Open(mailPath,token);return new("邮件客户端打开请求已提交。",Evidence:"accepted");}
            if(r.Action is "system_open" or "mail_client_open" or "mail_client_settings"){
                await ui.ShowAsync(()=>{if(r.Action=="system_open")ShowComputer();else ShowDesktopMail();},token,()=>!resourcesDisposed&&(r.Action=="system_open"?prefs.SystemMonitorModule:prefs.DesktopMailModule));return new("窗口已打开",Evidence:"window-shown");
            }
            string path=r.Action=="system_taskmanager"?Path.Combine(Environment.SystemDirectory,"Taskmgr.exe"):DesktopClients.Resolve(prefs.SecurityClientPath,false)??throw new IOException("未找到火绒，请在电脑状态窗口选择程序路径。");token.ThrowIfCancellationRequested();if(!previewMode)await DesktopClients.Open(path,token);return new(previewMode?"示例模式，不启动软件。":"打开请求已提交。",Evidence:"accepted");
        }
        commands.Registry.Register(new DesktopToolModule("system-monitor","电脑状态",()=>prefs.SystemMonitorModule,["system_open","system_snapshot","system_taskmanager","system_securityclient"],Run));
        commands.Registry.Register(new DesktopToolModule("mail-client","邮箱客户端",()=>prefs.DesktopMailModule,["mail_client_open","mail_client_settings"],Run));computerMonitor.Configure();
    }
    void SaveDesktopOptions(Action<Preferences> edit){var next=LocalApplications.Clone(prefs);edit(next);if(!previewMode)Store.Save(next);prefs.SystemMonitorAlerts=next.SystemMonitorAlerts;prefs.MailClientPath=next.MailClientPath;prefs.SecurityClientPath=next.SecurityClientPath;computerMonitor?.Configure();}
    async Task RunDesktopAction(string action){try{var result=await commands.ExecuteDetailedAsync(new(action,"","desktop-tools"));if(result.Status!="succeeded")OperationFeedback.Error(result.Message);}catch(Exception e)when(FeatureFaults.Recoverable(e)){OperationFeedback.Error(e.Message);}}
    void ShowComputer(){if(computerWindow==null||computerWindow.IsDisposed){computerWindow=new ComputerStatusForm(computerMonitor!,prefs,SaveDesktopOptions,RunDesktopAction);WindowPositions.Prepare(computerWindow,"computer-status",Bounds,previewMode);computerWindow.VisibleChanged+=(_,_)=>computerMonitor?.Configure();computerWindow.FormClosed+=(_,_)=>computerMonitor?.Configure();}computerWindow.Show();computerWindow.Activate();computerMonitor?.Configure();_=computerMonitor?.Tick();}
    void ShowDesktopMail(){if(desktopMailWindow==null||desktopMailWindow.IsDisposed){desktopMailWindow=new MailClientForm(prefs,SaveDesktopOptions,()=>RunDesktopAction("mail_client_open"),previewMode);WindowPositions.Prepare(desktopMailWindow,"mail-client",Bounds,previewMode);}desktopMailWindow.Show();desktopMailWindow.Activate();}
    void ConfigureDesktopTools(){if(!prefs.SystemMonitorModule)computerWindow?.Close();if(!prefs.DesktopMailModule)desktopMailWindow?.Close();computerMonitor?.Configure();}
    void ReleaseDesktopTools(){computerMonitor?.Dispose();computerWindow?.Dispose();desktopMailWindow?.Dispose();}
}
