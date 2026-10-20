namespace LeoLauncher;

sealed record ClientHostConfiguration(string Module,bool Enabled,string Generation);
interface IClientHostServices
{
    ClientHostConfiguration ReadConfiguration();
    DependencyState[] ReadDependencies();
    bool ClientAvailable();
    void RecordFault(ApplicationHostPhase phase,Type errorType);
}
// Same three-field, bounded, memory-only fault format as the application pilot.
sealed class ClientHostServices(string module,Func<bool> enabled,Func<string> generation,
    Func<DependencyState[]> dependencies,Func<bool>? available=null,ApplicationHostFaults? faults=null):IClientHostServices
{
    public const string Protocol="leo-host-client/1";
    public ClientHostConfiguration ReadConfiguration()=>new(module,enabled(),generation());
    public DependencyState[] ReadDependencies()=>dependencies().ToArray();
    public bool ClientAvailable()=>available?.Invoke()??true;
    public void RecordFault(ApplicationHostPhase phase,Type errorType)
    {
        if(module is not("clash" or "music" or "media")||!Enum.IsDefined(phase))return;
        faults?.Add(new(module,phase.ToString(),errorType.Name));
    }
}
// Validates the service's scope and reports faults without changing execution semantics.
sealed class ClientHostContext
{
    readonly string module,generation;readonly IClientHostServices services;
    public ClientHostContext(string module,IClientHostServices services){this.module=module;this.services=services;generation=Read().Generation;}
    ClientHostConfiguration Read()
    {
        try{var result=services.ReadConfiguration();if(result.Module!=module)throw new InvalidOperationException("客户端宿主服务归属不一致。");return result;}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){Fault(ApplicationHostPhase.Configuration,e);throw;}
    }
    void Fault(ApplicationHostPhase phase,Exception error)
    {
        if(error is OperationCanceledException)return;
        try{services.RecordFault(phase,error.GetType());}catch(Exception e)when(FeatureFaults.Recoverable(e)){/* Logging must not replace the original result. */}
    }
    public bool Enabled=>Read().Enabled;
    public string Stamp=>Read().Generation;
    public bool ConfigurationIsCurrent=>generation==Stamp;
    public DependencyState[] ReadDependencies()
    {
        try{return services.ReadDependencies().ToArray();}catch(Exception e)when(FeatureFaults.Recoverable(e)){Fault(ApplicationHostPhase.Dependencies,e);throw;}
    }
    public bool ClientAvailable()
    {
        try{return services.ClientAvailable();}catch(Exception e)when(FeatureFaults.Recoverable(e)){Fault(ApplicationHostPhase.Dependencies,e);throw;}
    }
    public async Task<ActionPayload> Execute(Func<Task<ActionPayload>> work)
    {
        try{return await work();}catch(Exception e)when(FeatureFaults.Recoverable(e)){Fault(ApplicationHostPhase.Execution,e);throw;}
    }
}
