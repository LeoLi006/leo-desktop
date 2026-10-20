namespace LeoLauncher;

// Runtime-only accounting. Diagnostics expose types/counts, never business payloads.
sealed record ActivityCount(string Module,string Kind,int Active,int Cancelling);
static class ModuleActivities
{
    static readonly object gate=new();static readonly Dictionary<ActivityLease,(string Module,string Kind)> live=new();
    internal static void Add(ActivityLease activity,string module,string kind){lock(gate)live.Add(activity,(module,kind));}
    internal static void Remove(ActivityLease activity){lock(gate)live.Remove(activity);}
    public static ActivityCount[] Snapshot(){lock(gate)return live.GroupBy(x=>x.Value).Select(g=>new ActivityCount(g.Key.Module,g.Key.Kind,g.Count(),g.Count(x=>x.Key.Token.IsCancellationRequested))).OrderBy(x=>x.Module).ThenBy(x=>x.Kind).ToArray();}
    static string ModuleName(string id)=>id switch{"football"=>"足球","music-window"=>"音乐窗口","media-window"=>"媒体窗口","reminder-scheduler"=>"清单提醒","match-sync"=>"比赛后台校时","clipboard-monitor"=>"剪贴板监听",_=>id};
    static string KindName(string id)=>id switch{"schedule-refresh"=>"赛程刷新","team-refresh"=>"球队刷新","event-refresh"=>"比赛校时","search"=>"搜索请求","control"=>"控制请求","refresh"=>"刷新请求","polling"=>"轮询","reminder-check"=>"提醒检查","sync-request"=>"校时请求","capture"=>"剪贴板检查",_=>id};
    public static string Summary()
    {
        var rows=Snapshot();return "后台活动（已登记任务）"+(rows.Length==0?"：0":"：\n"+string.Join("\n",rows.Select(r=>ModuleName(r.Module)+" · "+KindName(r.Kind)+"："+r.Active+(r.Cancelling==0?"":"（取消收尾 "+r.Cancelling+"）"))));
    }

}
sealed class ActivityOwner(string module):IDisposable
{
    readonly object gate=new();CancellationTokenSource life=new();long generation;bool disposed;
    public ActivityLease Begin(string kind,CancellationToken token=default){lock(gate){ObjectDisposedException.ThrowIf(disposed,this);return new(this,module,kind,generation,CancellationTokenSource.CreateLinkedTokenSource(life.Token,token));}}
    internal bool Current(long version){lock(gate)return !disposed&&generation==version;}
    public void Reset()=>Stop(false);
    public void Dispose()=>Stop(true);
    void Stop(bool closing)
    {
        CancellationTokenSource old;
        lock(gate){if(disposed)return;generation++;disposed=closing;old=life;if(!closing)life=new();}
        // Invoke callbacks outside the owner lock and continue cleanup after a bad callback.
        try{old.Cancel();}catch(Exception e)when(FeatureFaults.Recoverable(e)){System.Diagnostics.Trace.WriteLine("Leo activity cancellation: "+e.GetType().Name);}finally{old.Dispose();}
    }
}
sealed class ActivityLease:IDisposable
{
    readonly ActivityOwner owner;readonly long generation;readonly CancellationTokenSource source;int released;
    public CancellationToken Token{get;}
    internal ActivityLease(ActivityOwner owner,string module,string kind,long generation,CancellationTokenSource source){this.owner=owner;this.generation=generation;this.source=source;Token=source.Token;ModuleActivities.Add(this,module,kind);}
    public bool Current=>Volatile.Read(ref released)==0&&!Token.IsCancellationRequested&&owner.Current(generation);
    public void ThrowIfNotCurrent(){if(!Current)throw new OperationCanceledException(Token);}
    public void Dispose(){if(Interlocked.Exchange(ref released,1)!=0)return;ModuleActivities.Remove(this);source.Dispose();}
}
