using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
namespace LeoLauncher;
sealed record ComputerProcess(int Id,long Started,string Name,double? Cpu,long? Memory);
sealed record ComputerSnapshot(DateTimeOffset Time,double? Cpu,long? TotalMemory,long? AvailableMemory,double? Download,double? Upload,long? DiskFree,ComputerProcess[] Processes,string Note="");
interface IComputerSampler { ComputerSnapshot Read(bool processes,CancellationToken token); }
sealed class WindowsComputerSampler:IComputerSampler
{
    [StructLayout(LayoutKind.Sequential)]struct Memory{public uint Length,Load;public ulong Total,Available,PageTotal,PageAvailable,VirtualTotal,VirtualAvailable,Extended;}
    [DllImport("kernel32.dll")]static extern bool GlobalMemoryStatusEx(ref Memory memory);
    [DllImport("kernel32.dll")]static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
    readonly object gate=new();long lastTime,idle,total,received,sent;bool cpuValid,netValid;Dictionary<(int,long),(TimeSpan Cpu,long At)> previous=new();ComputerProcess[] processes=[];long processAt,driveAt;long? disk;
    internal static double? CpuPercent(long deltaTotal,long deltaIdle)=>deltaTotal>0&&deltaIdle>=0&&deltaIdle<=deltaTotal?100d*(deltaTotal-deltaIdle)/deltaTotal:null;
    internal static double? Rate(long value,long old,double seconds)=>seconds>0&&seconds<=60&&value>=old?(value-old)/seconds:null;
    public ComputerSnapshot Read(bool detail,CancellationToken token)
    {
        lock(gate){token.ThrowIfCancellationRequested();long at=Stopwatch.GetTimestamp();double elapsed=lastTime==0?0:(at-lastTime)/(double)Stopwatch.Frequency;double? cpu=null,down=null,up=null;long? memory=null,available=null;var notes=new List<string>();
            if(GetSystemTimes(out long i,out long k,out long u)){long t=k+u;if(cpuValid&&elapsed>0&&elapsed<=60)cpu=CpuPercent(t-total,i-idle);idle=i;total=t;cpuValid=true;}else{cpuValid=false;notes.Add("CPU暂不可读取");}
            var m=new Memory{Length=(uint)Marshal.SizeOf<Memory>()};if(GlobalMemoryStatusEx(ref m)){memory=(long)m.Total;available=(long)m.Available;}else notes.Add("内存暂不可读取");
            try{long rx=0,tx=0;foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces()){token.ThrowIfCancellationRequested();if(adapter.OperationalStatus!=OperationalStatus.Up||adapter.NetworkInterfaceType==NetworkInterfaceType.Loopback)continue;var stats=adapter.GetIPStatistics();rx+=stats.BytesReceived;tx+=stats.BytesSent;}if(netValid){down=Rate(rx,received,elapsed);up=Rate(tx,sent,elapsed);}received=rx;sent=tx;netValid=true;}catch(Exception e)when(FeatureFaults.Recoverable(e)&&e is not OperationCanceledException){netValid=false;notes.Add("网络暂不可读取");}
            if(driveAt==0||(at-driveAt)/(double)Stopwatch.Frequency>=30){driveAt=at;try{disk=new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!).AvailableFreeSpace;}catch(Exception e)when(FeatureFaults.Recoverable(e)){disk=null;}}
            if(detail&&(processAt==0||(at-processAt)/(double)Stopwatch.Frequency>=4)){
                processAt=at;var rows=new List<ComputerProcess>();var next=new Dictionary<(int,long),(TimeSpan,long)>();
                try{
                    var all=Process.GetProcesses();
                    try{foreach(var p in all){token.ThrowIfCancellationRequested();try{int id=p.Id;string name=p.ProcessName;long started=p.StartTime.ToUniversalTime().Ticks;var time=p.TotalProcessorTime;long used=p.WorkingSet64;double? load=null;if(previous.TryGetValue((id,started),out var old)){double seconds=(at-old.At)/(double)Stopwatch.Frequency;if(seconds>0&&seconds<=60&&time>=old.Cpu)load=Math.Clamp((time-old.Cpu).TotalSeconds/seconds/Environment.ProcessorCount*100,0,100);}next[(id,started)]=(time,at);rows.Add(new(id,started,name,load,used));}catch(Exception e)when(FeatureFaults.Recoverable(e)&&e is not OperationCanceledException){}}}
                    finally{foreach(var p in all)p.Dispose();}
                    previous=next;processes=rows.OrderByDescending(p=>p.Cpu??0).ThenByDescending(p=>p.Memory??0).Take(12).Concat(rows.OrderByDescending(p=>p.Memory??0).Take(12)).DistinctBy(p=>(p.Id,p.Started)).ToArray();
                }catch(Exception e)when(FeatureFaults.Recoverable(e)&&e is not OperationCanceledException){notes.Add("部分进程不可读取");}
            }
            if(!detail){previous.Clear();processes=[];processAt=0;}lastTime=at;token.ThrowIfCancellationRequested();return new(DateTimeOffset.Now,cpu,memory,available,down,up,disk,processes.ToArray(),string.Join(" · ",notes));
        }
    }
}
sealed class SustainedResourceAlert
{
    DateTimeOffset? cpuSince,memorySince,last;DateTimeOffset cpuNotice=DateTimeOffset.MinValue,memoryNotice=DateTimeOffset.MinValue;
    public void Reset(){cpuSince=memorySince=last=null;}
    public string? Check(ComputerSnapshot s)
    {
        if(last is{} prior&&(s.Time-prior>TimeSpan.FromSeconds(15)||s.Time<prior))Reset();last=s.Time;
        string? CheckOne(bool high,ref DateTimeOffset? since,ref DateTimeOffset notified,string message){if(!high){since=null;return null;}since??=s.Time;if(s.Time<since){since=s.Time;return null;}if(s.Time-since>=TimeSpan.FromSeconds(30)&&s.Time-notified>=TimeSpan.FromMinutes(15)){notified=s.Time;return message;}return null;}
        string? first=CheckOne(s.Cpu>=90,ref cpuSince,ref cpuNotice,"CPU持续较高，打开电脑状态可查看占用；这不表示存在病毒。");string? second=CheckOne(s.TotalMemory>0&&s.AvailableMemory>=0&&100d*(s.TotalMemory-s.AvailableMemory)/s.TotalMemory>=92,ref memorySince,ref memoryNotice,"内存持续紧张，打开电脑状态查看占用。");return first??second;
    }
}
sealed class ComputerMonitor:IDisposable
{
    readonly IComputerSampler sampler;readonly Func<bool> enabled,alerts,visible;readonly Action<string> notify;readonly System.Windows.Forms.Timer timer=new(){Interval=2000};readonly OwnedActivityTimer activity;readonly SustainedResourceAlert threshold=new();bool closed;long generation;
    public ComputerSnapshot? Current{get;private set;}public event Action? Updated;
    public ComputerMonitor(IComputerSampler sampler,Func<bool> enabled,Func<bool> alerts,Func<bool> visible,Action<string> notify){this.sampler=sampler;this.enabled=enabled;this.alerts=alerts;this.visible=visible;this.notify=notify;activity=new(timer,"system-monitor","电脑状态采样","sample",Poll);}
    public void Configure(){if(closed)return;generation++;timer.Interval=visible()?2000:5000;if(!enabled()||!alerts())threshold.Reset();activity.Configure(enabled()&&(visible()||alerts()));if(enabled()&&(visible()||alerts()))activity.Retry();}
    async Task Poll(CancellationToken token){long version=generation;bool detail=visible();var snapshot=await Task.Run(()=>sampler.Read(detail,token),token);token.ThrowIfCancellationRequested();if(closed||version!=generation||!enabled())return;Current=snapshot;if(alerts()&&threshold.Check(snapshot) is{} message)notify(message);Updated?.Invoke();}
    internal bool Active=>activity.Enabled;internal Task Tick()=>activity.RunNowAsync();
    public void Dispose(){if(closed)return;closed=true;generation++;activity.Dispose();Updated=null;}
}
