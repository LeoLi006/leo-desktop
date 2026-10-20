namespace LeoLauncher;

// UI-thread owner for one periodic activity and its in-flight work.
sealed class OwnedActivityTimer:IPeriodicTask
{
    readonly System.Windows.Forms.Timer timer;readonly ActivityOwner owner;readonly string feature,workKind;readonly Func<CancellationToken,Task> work;
    ActivityLease? polling;bool wanted,failed,disposed,running;long generation;
    public OwnedActivityTimer(System.Windows.Forms.Timer timer,string module,string feature,string workKind,Func<CancellationToken,Task> work)
    {this.timer=timer;owner=new(module);this.feature=feature;this.workKind=workKind;this.work=work;timer.Tick+=Tick;}
    public bool Running=>running;
    public bool Enabled=>wanted&&!failed&&!disposed;
    public void Configure(bool enabled)
    {
        if(disposed)return;if(wanted==enabled)return;wanted=enabled;generation++;
        if(enabled){failed=false;Start();}else Stop();
    }
    void Start(){if(!Enabled)return;polling??=owner.Begin("polling");timer.Start();}
    void Stop(){timer.Stop();polling?.Dispose();polling=null;owner.Reset();}
    public void Retry(){if(disposed||!wanted||!failed)return;failed=false;generation++;Start();}
    async void Tick(object? sender,EventArgs e)=>await RunNowAsync();
    public async Task RunNowAsync()
    {
        if(!Enabled||running)return;running=true;
        try{
            long version;
            do{
                version=generation;using var request=owner.Begin(workKind);
                try{await work(request.Token);request.ThrowIfNotCurrent();}
                catch(OperationCanceledException){}
                catch(Exception e)when(FeatureFaults.Recoverable(e)){
                    if(version==generation&&request.Current&&Enabled){failed=true;generation++;Stop();FeatureFaults.Report(feature,e,()=>{Retry();return Task.CompletedTask;});}
                }
            }while(version!=generation&&Enabled);
        }finally{running=false;}
    }

    public void Dispose(){if(disposed)return;disposed=true;wanted=false;generation++;timer.Tick-=Tick;timer.Stop();polling?.Dispose();polling=null;owner.Dispose();timer.Dispose();}
}
