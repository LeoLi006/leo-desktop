using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace LeoLauncher;

sealed record WorkflowStep(string Id,ActionRequest Request,string Label,int TimeoutMs=30000,string? FromStep=null,string? ResultProperty=null);
sealed record WorkflowStepState(string Id,string Action,string State="pending",string? Evidence=null,string? ErrorCode=null);
sealed record WorkflowRecord(string ComboId,string RunId,string Fingerprint,DateTimeOffset Updated,WorkflowStepState[] Steps);
enum WorkflowVerification{Confirmed,NotApplied,Unknown}
sealed class WorkflowRecoveryRequired(string message):InvalidOperationException(message);

sealed class WorkflowJournal
{
    readonly ModuleJsonData<List<WorkflowRecord>> data;
    public WorkflowJournal(string? path)
    {
        data=new(path,[],Validate,items=>items.ToList());
        if(path!=null&&AtomicJson.Recovered.Contains(Path.GetFileName(path)))data.Change(items=>{for(int i=0;i<items.Count;i++)items[i]=items[i] with{Steps=items[i].Steps.Select(s=>s.State=="pending"?s with{State="unknown"}:s).ToArray()};return true;});
    }
    static List<WorkflowRecord> Validate(string text)
    {
        var items=JsonSerializer.Deserialize<List<WorkflowRecord>>(text)??throw new IOException("组合执行记录为空");
        if(items.Count>32||items.Any(x=>x==null||string.IsNullOrWhiteSpace(x.ComboId)||x.Fingerprint?.Length!=64||!Guid.TryParseExact(x.RunId,"N",out _)||x.Steps==null||x.Steps.Length is <1 or >12||x.Steps.Any(s=>s==null||string.IsNullOrWhiteSpace(s.Id)||string.IsNullOrWhiteSpace(s.Action)||s.State is not("pending" or "running" or "succeeded" or "unknown"))||x.Steps.Select(s=>s.Id).Distinct().Count()!=x.Steps.Length)||items.Select(x=>x.ComboId).Distinct().Count()!=items.Count)throw new IOException("组合执行记录损坏，请保留原文件后检查备份");return items;
    }
    public WorkflowRecord? Find(string id)=>data.Read(items=>items.FirstOrDefault(x=>x.ComboId==id) is {} item?item with{Steps=item.Steps.ToArray()}:null);
    public void Save(WorkflowRecord record)=>data.Change(items=>{items.RemoveAll(x=>x.ComboId==record.ComboId);items.Insert(0,record with{Steps=record.Steps.ToArray()});if(items.Count>32)items.RemoveRange(32,items.Count-32);return true;});
}

sealed class ReliableWorkflow(WorkflowJournal journal,Func<ActionRequest,CancellationToken,Task<ActionResult>> execute,Func<ActionRequest,CancellationToken,Task<WorkflowVerification>> verify)
{
    int busy;
    public async Task<WorkflowRecord> Run(string id,WorkflowStep[] steps,Action<int,WorkflowStep> progress,CancellationToken token=default,bool confirmUnknown=false)
    {
        if(Interlocked.CompareExchange(ref busy,1,0)!=0)throw new InvalidOperationException("已有组合正在执行，请稍候");
        try{
            if(steps.Length is <1 or >12||steps.Select(s=>s.Id).Distinct().Count()!=steps.Length||steps.Any(s=>s.TimeoutMs is <1 or >120000))throw new ArgumentException("组合步骤无效");
            var preceding=new HashSet<string>();foreach(var step in steps){if(string.IsNullOrWhiteSpace(step.Id)||step.FromStep!=null&&!preceding.Contains(step.FromStep))throw new ArgumentException("步骤只能引用前面步骤的结果");preceding.Add(step.Id);}
            var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(steps.Select(s=>new{s.Id,s.Request.Action,s.Request.Value,s.TimeoutMs,s.FromStep,s.ResultProperty})))));
            var old=journal.Find(id);bool unfinished=old!=null&&old.Steps.Any(s=>s.State!="succeeded");
            if(unfinished&&old!.Fingerprint!=fingerprint&&!confirmUnknown)throw new WorkflowRecoveryRequired("组合已修改，上次未完成进度无法复用；确认后将从头执行，可能重复已完成的操作。");
            var run=unfinished&&old!.Fingerprint==fingerprint?old:new WorkflowRecord(id,Guid.NewGuid().ToString("N"),fingerprint,DateTimeOffset.UtcNow,steps.Select(s=>new WorkflowStepState(s.Id,s.Request.Action)).ToArray());
            if(run.Steps.Length!=steps.Length||run.Steps.Where((state,index)=>state.Id!=steps[index].Id||state.Action!=steps[index].Request.Action).Any())throw new IOException("执行记录与当前步骤不一致，未执行操作");
            var outputs=new Dictionary<string,JsonElement>();
            for(int i=0;i<steps.Length;i++){
                token.ThrowIfCancellationRequested();var step=steps[i];var state=run.Steps[i];
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(step.TimeoutMs);
                if(state.State=="succeeded"){
                    // Re-read explicit query outputs on recovery; never replay a completed write for its result.
                    if(steps.Any(s=>s.FromStep==step.Id)){
                        if(state.Evidence is not("cached" or "snapshot"))throw new WorkflowRecoveryRequired("恢复时缺少前一步结果，不能自动重放已完成的写操作。");
                        var query=await execute(step.Request,deadline.Token).WaitAsync(deadline.Token);query.ToLegacy(token);if(query.Data is {} data)outputs[step.Id]=data;
                    }continue;
                }
                var request=step.Request;
                if(step.FromStep is {} from){if(!outputs.TryGetValue(from,out var output))throw new ArgumentException("上一步结果不可用");request=request with{Value=(step.ResultProperty==null?output:output.GetProperty(step.ResultProperty)).GetRawText()};}
                if(state.State is "running" or "unknown"){
                    var status=await verify(request,deadline.Token).WaitAsync(deadline.Token);
                    if(status==WorkflowVerification.Confirmed){Set(i,"succeeded","verified");continue;}
                    if(status==WorkflowVerification.Unknown&&!confirmUnknown)throw new WorkflowRecoveryRequired("第 "+(i+1)+" 步上次可能已生效，无法自动核验；请确认是否重新执行此步，可能重复打开或操作。");
                }
                if(state.State=="pending"&&await verify(request,deadline.Token).WaitAsync(deadline.Token)==WorkflowVerification.Confirmed){Set(i,"succeeded","verified");continue;}
                token.ThrowIfCancellationRequested();progress(i,step);Set(i,"running");
                try{
                    var operation=execute(request with{RequestId=run.RunId+":"+step.Id,TraceId=run.RunId,Deadline=DateTimeOffset.UtcNow.AddMilliseconds(step.TimeoutMs)},deadline.Token);
                    _=operation.ContinueWith(t=>{_ =t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
                    var result=await operation.WaitAsync(deadline.Token);
                    result.ToLegacy(deadline.Token);if(result.Data is {} data)outputs[step.Id]=data;Set(i,"succeeded",result.Evidence);
                }catch(Exception e)when(FeatureFaults.Recoverable(e)){bool timeout=e is TimeoutException||e is OperationCanceledException&&!token.IsCancellationRequested&&deadline.IsCancellationRequested;Set(i,"unknown",error:timeout?"timeout":e is OperationCanceledException?"cancelled":"step_failed");if(timeout&&e is not TimeoutException)throw new TimeoutException("第 "+(i+1)+" 步等待超时，已停止后续步骤；重试前需确认状态。",e);throw;}
                token.ThrowIfCancellationRequested();
            }
            return run;
            void Set(int index,string state,string? evidence=null,string? error=null){var states=run.Steps.ToArray();states[index]=states[index] with{State=state,Evidence=evidence,ErrorCode=error};var next=run with{Updated=DateTimeOffset.UtcNow,Steps=states};journal.Save(next);run=next;}
        }finally{Volatile.Write(ref busy,0);}
    }
}

sealed class WorkflowCapability(Func<ActionRequest,CancellationToken,Task<ActionPayload>> run):IStructuredLeoModule,IBrainContributor,IModuleContractSource
{
    public ModuleContract DescribeModule()=>ModuleContracts.Workflow(this);
    public DependencyState[] ReadDependencies()=>[new("selected-actions","unknown"),new("workflow-journal","unknown")];

    public IReadOnlyList<BrainAction> BrainActions=>[new("combo_name","执行已保存组合","combo-name",Hint:"先用leo_catalog查询名称；组合只能作为单独的一步计划，内部仍受原组合限制")];
    public string Id=>"workflows";public string Name=>"自定义组合";public string Version=>"1";public bool Enabled=>true;public bool ConfigurationIsCurrent=>true;
    public IReadOnlyList<string> Actions=>["combo_name","scene_watch","combo_resume"];
    public CapabilityDefinition Describe(string action)=>new(action,Id,"in-process","legacy-combo-name","external","coalesce-inflight",120000);
    public string FailureCode(Exception error)=>error is WorkflowRecoveryRequired?"recovery_required":error is IOException or UnauthorizedAccessException?"journal_failed":"workflow_failed";
    public Task<ActionPayload> ExecuteStructuredAsync(ActionRequest request,CancellationToken token)=>run(request,token);
    public async Task<string> ExecuteAsync(ActionRequest request,CancellationToken token)=>(await run(request,token)).Message;
}
