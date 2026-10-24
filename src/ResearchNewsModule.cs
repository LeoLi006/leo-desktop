using System.Text.Json;
namespace LeoLauncher;

sealed class ResearchNewsModule(Func<bool> enabled,Func<CancellationToken,Task> show):IStructuredLeoModule,IActionRequestAdapter,IModuleContractSource,IModuleEntryContributor
{
    public string Id=>"research-news";public string Name=>"AI 动态";public string Version=>"1.0";public bool Enabled=>enabled();
    public bool ConfigurationIsCurrent=>Enabled;public int MaximumInputLength=>0;public IReadOnlyList<string> Actions=>["research_open"];
    public CapabilityDefinition Describe(string action)=>new(action,Id,"in-process","empty-object","window","coalesce-inflight",10000);
    public ModuleContract DescribeModule()=>ModuleContracts.Build(this,"research-news",Name,"optional","host.public-research",[],_ =>("object","只接受空对象；打开公开研究动态窗口，不授权模型调用或下载。","research-window","仅确认窗口已显示，不代表所有信息源可用。",true,["window-shown"]));
    public DependencyState[] ReadDependencies()=>[];
    public EntryContribution[] DescribeEntries()=>[new(Id+"/open",Id,"research_open",UiLanguage.T(Name),"",Enabled,EntryDisplay.Shortcut,ModuleEntryContributions.TargetPrefix+Id+"/open",["AI新闻","论文","Agent","全双工","研究动态","arxiv"],"module-action"){
        Parameters="{}",Glyph="news",Hint="Agent 与实时语音论文、开源项目",MenuActions=[new("AI 动态…","research_open","{}")]}];
    public ActionRequest Adapt(StructuredActionRequest r)
    {
        if(r.Protocol!="leo-action/1"||r.ActionId!="research_open"||r.Parameters.ValueKind!=JsonValueKind.Object||r.Parameters.EnumerateObject().Any())throw new ArgumentException("只接受空参数");
        return new(r.ActionId,"",r.Source,r.RequestId,r.TraceId,r.Deadline);
    }
    public async Task<ActionPayload> ExecuteStructuredAsync(ActionRequest r,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(!Enabled)throw new InvalidOperationException("AI 动态已停用");if(r.Action!="research_open"||r.Value.Length>0)throw new ArgumentException("动作或参数无效");
        await show(token);return new("AI 动态窗口已打开。",Evidence:"window-shown");
    }
    public async Task<string> ExecuteAsync(ActionRequest r,CancellationToken token)=>(await ExecuteStructuredAsync(r,token)).Message;
    public string FailureCode(Exception e)=>e is ArgumentException?"invalid_request":"research_window_failed";
}

sealed partial class PetForm
{
    ResearchNewsFeed? researchFeed;ResearchNewsForm? researchWindow;
    void RegisterResearchNews()
    {
        var ui=new ModuleWindowService(this);
        commands.Registry.Register(new ResearchNewsModule(()=>prefs.ResearchNewsModule,token=>ui.ShowAsync(()=>{
            if(researchWindow==null||researchWindow.IsDisposed){researchFeed??=new ResearchNewsFeed(previewMode?null:Path.Combine(Store.Root,"leo-research-cache.json"));researchWindow=new(researchFeed,previewMode);WindowPositions.Prepare(researchWindow,"research-news",Bounds,previewMode);}
            launcher.Hide();WindowInteraction.Show(researchWindow);
        },token,()=>!resourcesDisposed&&prefs.ResearchNewsModule)));
    }
    void ConfigureResearchNews(){if(!prefs.ResearchNewsModule){researchWindow?.Close();researchWindow=null;}}
    void ReleaseResearchNews(){researchWindow?.Dispose();researchFeed?.Dispose();}
}
