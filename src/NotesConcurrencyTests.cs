using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;
static class NotesConcurrencyTests
{
    static ActionDispatcher Bus(NoteStore store,string root){var bus=new ActionDispatcher(new(),(_,_,_)=>throw new Exception("legacy bypass"),new ExtensionStore(Path.Combine(root,"empty-extensions")));bus.Registry.Register(new NotesCapability(()=>store,(_,_,_)=>Task.CompletedTask));return bus;}
    static object Receipt(string phase,int ordinal,ActionResult result,long elapsed)=>new{phase,ordinal,status=result.Status,error_code=result.ErrorCode,elapsed_ms=elapsed,error_type=result.Failure?.GetType().Name,native_error=result.Failure==null?(int?)null:result.Failure.HResult&65535};
    public static void Run(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{
            var c=new Dictionary<string,bool>();var evidence=new List<object>();
            try{
                string root=Path.Combine(Path.GetDirectoryName(path)!,"isolated-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
                for(int round=0;round<3;round++){
                    string file=Path.Combine(root,"round-"+round+".json");int attempted=0,committed=0;
                    var store=new NoteStore(file,(p,s)=>{Interlocked.Increment(ref attempted);AtomicJson.WriteNotes(p,s);Interlocked.Increment(ref committed);});using var bus=Bus(store,root);
                    var clock=Stopwatch.StartNew();var results=await Task.WhenAll(Enumerable.Range(0,25).Select(async i=>{var request=Stopwatch.StartNew();var result=await bus.ExecuteDetailedAsync(new("note_add","isolated "+i,"fixture"));lock(evidence)evidence.Add(Receipt("round-"+round,i,result,request.ElapsedMilliseconds));return result;}));
                    int disk=new NoteStore(file).Items.Count;int memory=store.Items.Count;
                    c["round_"+round+"_all_25_succeed"]=results.All(r=>r.Status=="succeeded")&&memory==25&&disk==25;
                    c["round_"+round+"_counts_match_actual_commits"]=memory==committed&&disk==committed&&results.Count(r=>r.Status=="succeeded")==committed;
                    evidence.Add(new{phase="round-summary",round,requested=25,attempted,committed,memory,disk,elapsed_ms=clock.ElapsedMilliseconds});
                }
                // Deliberate sharing violation: failure must remain explicit, not a lost success.
                string lockedFile=Path.Combine(root,"locked.json");var locked=new NoteStore(lockedFile);using(var bus=Bus(locked,root)){
                    await bus.ExecuteDetailedAsync(new("note_add","initial","fixture"));string original=File.ReadAllText(lockedFile);ActionResult failed;
                    using(var fileLock=new FileStream(lockedFile,FileMode.Open,FileAccess.Read,FileShare.Read))failed=await bus.ExecuteDetailedAsync(new("note_add","retryable","fixture","locked-request"));
                    evidence.Add(Receipt("controlled-sharing-violation",0,failed,0));
                    c["sharing_violation_is_failure_not_success"]=failed.Status=="failed"&&failed.ErrorCode=="storage_failed";
                    c["sharing_violation_preserves_disk_and_memory"]=locked.Items.Count==1&&File.ReadAllText(lockedFile)==original;
                    var retry=await bus.ExecuteDetailedAsync(new("note_add","retryable","fixture","locked-request"));c["explicit_retry_after_unlock_commits_once"]=retry.Status=="succeeded"&&locked.Items.Count==2&&new NoteStore(lockedFile).Items.Count==2;
                }
                using(var release=new ManualResetEventSlim()){
                    var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int writes=0;
                    var queuedStore=new NoteStore(Path.Combine(root,"queued.json"),(p,s)=>{Interlocked.Increment(ref writes);entered.TrySetResult();if(!release.Wait(3000))throw new TimeoutException("controlled writer gate");ConfigMigration.Write(p,s);});
                    using var bus=Bus(queuedStore,root);var first=bus.ExecuteDetailedAsync(new("note_add","first","fixture"));
                    try{await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));using var cancel=new CancellationTokenSource();var second=bus.ExecuteDetailedAsync(new("note_add","cancelled queued","fixture"),cancel.Token);cancel.Cancel();var result=await second.WaitAsync(TimeSpan.FromSeconds(1));c["queued_cancel_returns_before_writer_release"]=result.Status=="cancelled"&&!first.IsCompleted;evidence.Add(Receipt("queued-cancel",0,result,0));}
                    finally{release.Set();}
                    var done=await first;await Task.Delay(40);c["queued_cancel_never_reaches_commit"]=done.Status=="succeeded"&&writes==1&&queuedStore.Items.Count==1;
                }
                using(var cancel=new CancellationTokenSource()){
                    string file=Path.Combine(root,"commit-cancel.json");var store=new NoteStore(file,(p,s)=>{ConfigMigration.Write(p,s);cancel.Cancel();});using var bus=Bus(store,root);
                    var first=await bus.ExecuteDetailedAsync(new("note_add","committed before cancelled receipt","fixture","commit-request"),cancel.Token);
                    var replay=await bus.ExecuteDetailedAsync(new("note_add","committed before cancelled receipt","fixture","commit-request"));
                    evidence.Add(Receipt("cancel-after-commit",0,first,0));evidence.Add(Receipt("replay-after-commit",0,replay,0));
                    c["post_commit_cancel_does_not_duplicate_on_retry"]=store.Items.Count==1&&new NoteStore(file).Items.Count==1&&replay.Status=="succeeded";
                    c["cancelled_receipt_does_not_claim_rollback"]=first.Status=="succeeded"||first.Status=="cancelled"&&first.Message.Contains("可能已经生效");
                }
                int transient=unchecked((int)0x80070497);
                foreach(string mode in new[]{"recover","persistent","changed-target","changed-temporary","generic"}){
                    string file=Path.Combine(root,"replace-"+mode+".json");var store=new NoteStore(file);store.Save(null,"original");string original=File.ReadAllText(file);int attempts=0,events=0;store.Changed+=()=>events++;
                    var previous=AtomicJson.ReplaceOverride;Exception? failure=null;
                    try{
                        AtomicJson.ReplaceOverride=(temporary,target,backup)=>{
                            if(target!=file){File.Replace(temporary,target,backup);return;}
                            attempts++;
                            if(mode=="recover"&&attempts>1){File.Replace(temporary,target,backup);return;}
                            if(mode=="changed-target")File.AppendAllText(target," ");
                            if(mode=="changed-temporary")File.WriteAllText(temporary,"{}");
                            throw new IOException("injected atomic replacement failure",transient);
                        };
                        try{if(mode=="generic")AtomicJson.Write(file,"[]");else store.Save(null,"new");}catch(IOException error){failure=error;}
                    }finally{AtomicJson.ReplaceOverride=previous;}
                    evidence.Add(new{phase="controlled-replace",mode,attempts,error_type=failure?.GetType().Name,native_error=failure==null?(int?)null:failure.HResult&65535,events});
                    if(mode=="recover")c["transient_1175_retries_only_replace_and_publishes_once"]=failure==null&&attempts==2&&events==1&&store.Items.Count==2&&new NoteStore(file).Items.Count==2;
                    else if(mode=="persistent")c["persistent_1175_is_bounded_and_preserves_original"]=failure!=null&&attempts==4&&events==0&&store.Items.Count==1&&File.ReadAllText(file)==original;
                    else if(mode=="changed-target")c["changed_target_blocks_replay"]=failure!=null&&attempts==1&&events==0&&File.ReadAllText(file)==original+" ";
                    else if(mode=="changed-temporary")c["changed_temporary_blocks_replay"]=failure!=null&&attempts==1&&events==0&&File.ReadAllText(file)==original;
                    else c["generic_json_writes_do_not_gain_retries"]=failure!=null&&attempts==1&&File.ReadAllText(file)==original;
                    c["temporary_cleanup_"+mode]=!Directory.GetFiles(root,Path.GetFileName(file)+".*.tmp").Any();
                }
                File.WriteAllText(path,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,evidence,real_api_requests=0,personal_data_reads=0,external_process_starts=0}));
            }catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{ok=false,error_type=e.GetType().Name,checks=c,evidence}));}finally{ui.Close();}
        };Application.Run(ui);
    }
}
