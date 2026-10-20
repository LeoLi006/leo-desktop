namespace LeoLauncher;

enum ApplicationHostPhase { Configuration,Dependencies,Execution }
sealed record ApplicationHostFault(string Module,string Phase,string ErrorType);
interface IApplicationHostServices
{
    LocalApplication? ReadConfiguration();
    DependencyState[] ReadDependencies(LocalApplication configuration);
    void RecordFault(ApplicationHostPhase phase,Type errorType);
}
// Memory-only, bounded and deliberately free of exception text, paths and arguments.
sealed class ApplicationHostFaults
{
    readonly object gate=new();readonly Queue<ApplicationHostFault> records=new();
    public void Add(ApplicationHostFault fault){lock(gate){records.Enqueue(fault);while(records.Count>128)records.Dequeue();}}
    public ApplicationHostFault[] Read(){lock(gate)return records.ToArray();}
}
sealed class ApplicationHostServices(string applicationId,Func<LocalApplication?> read,ApplicationHostFaults? faults=null):IApplicationHostServices
{
    public const string Protocol="leo-host-application/1";
    void Own(LocalApplication app){if(app.Id!=applicationId)throw new InvalidOperationException("宿主服务返回了其他模块的配置。");}
    public LocalApplication? ReadConfiguration(){var app=read();if(app!=null)Own(app);return app;}
    public DependencyState[] ReadDependencies(LocalApplication configuration)
    {
        Own(configuration);
        return [new("application-file",File.Exists(configuration.Path)?"ready":"missing"),
            new("working-directory",configuration.WorkingDirectory.Length==0||Directory.Exists(configuration.WorkingDirectory)?"ready":"missing")];
    }
    public void RecordFault(ApplicationHostPhase phase,Type errorType)
    {
        if(!Guid.TryParseExact(applicationId,"N",out _)||!Enum.IsDefined(phase))return;
        faults?.Add(new("local:"+applicationId,phase.ToString(),errorType.Name));
    }
}
