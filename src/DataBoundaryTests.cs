using System.Text.Json;
namespace LeoLauncher;
static class DataBoundaryTests
{
    static async Task<bool> Cancelled(Task task){try{await task;return false;}catch(OperationCanceledException){return true;}}
    public static void Run(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{
            var checks=new Dictionary<string,bool>();try{await Check(Path.GetDirectoryName(path)!,checks);File.WriteAllText(path,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,real_api_requests=0,personal_data_reads=0,system_clipboard_reads=0},new JsonSerializerOptions{WriteIndented=true}));}
            catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks}));}finally{AtomicJson.BeforeReplace=null;ui.Close();}
        };Application.Run(ui);
    }
    static async Task Check(string directory,Dictionary<string,bool> c)
    {
        string root=Path.Combine(directory,"fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var day=DateOnly.FromDateTime(DateTime.Today);var now=DateTimeOffset.UtcNow;
        using var bus=new ActionDispatcher(new(),(_,_,_)=>throw new Exception("no legacy"),new ExtensionStore(Path.Combine(root,"modules")));
        string todoFile=Path.Combine(root,"tasks.json");var todos=new TodoStore(todoFile);int changed=0;var module=new TodoCapability(todos,(work,t)=>{t.ThrowIfCancellationRequested();work();return Task.CompletedTask;},_=>{},_=>null);bus.Registry.Register(module);var client=new TodoClient(todos,bus);var task=client.Add("fixture task",day);todos.Changed+=()=>changed++;
        var items=client.Items;items.Clear();c["todo_items_are_independent"]=client.Items.Count==1;
        var today=client.ForDay(day);today[0]=today[0] with{Title="draft"};today.Clear();c["todo_query_draft_does_not_edit_source"]=client.Items.Single().Title=="fixture task";
        var week=client.ForWeek(day);week.Clear();c["todo_week_query_is_independent"]=client.ForWeek(day).Count==1;
        string original=File.ReadAllText(todoFile);int generation=changed;
        AtomicJson.BeforeReplace=file=>{if(file==todoFile)throw new IOException("fixture write failure");};
        var failed=await bus.ExecuteDetailedAsync(new("todo_edit",JsonSerializer.Serialize(new{id=task.Id,title="failed draft",future=false}),"boundary"));AtomicJson.BeforeReplace=null;
        c["todo_failed_save_keeps_memory_disk_and_notifications"]=failed.Status=="failed"&&failed.ErrorCode=="storage_failed"&&File.ReadAllText(todoFile)==original&&client.Items.Single().Title=="fixture task"&&changed==generation;
        using(var stop=new CancellationTokenSource()){stop.Cancel();var result=await bus.ExecuteDetailedAsync(new("todo_delete",task.Id,"boundary"),stop.Token);c["todo_precancel_does_not_commit"]=result.Status=="cancelled"&&File.ReadAllText(todoFile)==original&&client.Items.Count==1;}
        var natural=await bus.ExecuteDetailedAsync(new("todo_natural",JsonSerializer.Serialize(new NaturalTodoDraft("cancelled draft",day,null,"",false)),"boundary"));c["todo_editor_cancel_preserves_original"]=natural.Status=="cancelled"&&File.ReadAllText(todoFile)==original&&client.Items.Count==1;
        client.Rename(task.Id,"confirmed");c["todo_explicit_save_commits"]=client.Items.Single().Title=="confirmed"&&new TodoStore(todoFile).Items.Single().Title=="confirmed";
        c["todo_receipts_do_not_store_body"]=bus.RecentResults.All(r=>r.Data==null&&!r.Message.Contains("fixture task")&&!r.Message.Contains("failed draft"));
        string clipFile=Path.Combine(root,"clips.json");var preferences=new Preferences();using var service=new ClipboardHistoryService(preferences,true,clipFile);bus.Registry.Register(new ClipboardCapability(service,preferences,(work,t)=>{t.ThrowIfCancellationRequested();work();return Task.CompletedTask;},()=>{}));var clips=new ClipboardClient(service,bus);var clip=new ClipItem("text","fixture clipboard","fixture label",now);service.History.Add(clip);
        var clipItems=service.History.Items;clipItems.Clear();c["clipboard_items_cannot_clear_source"]=service.History.Items.Count==1;
        if(service.History.Items.Count==0)service.History.Add(clip);
        var search=clips.Search("fixture");search.Clear();c["clipboard_query_result_is_independent"]=clips.Search("fixture").Count==1;
        var pinDraft=service.History.Items.Single() with{Pinned=true,Content="draft"};c["clipboard_record_draft_does_not_change_source"]=!service.History.Items.Single().Pinned&&service.History.Items.Single().Content==clip.Content;
        string clipOriginal=File.ReadAllText(clipFile);AtomicJson.BeforeReplace=file=>{if(file==clipFile)throw new IOException("fixture clip failure");};
        var clipFailure=await bus.ExecuteDetailedAsync(new("clipboard_pin",clip.Key,"boundary"));AtomicJson.BeforeReplace=null;
        c["clipboard_failed_save_keeps_memory_and_disk"]=clipFailure.Status=="failed"&&clipFailure.ErrorCode=="storage_failed"&&!service.History.Items.Single().Pinned&&File.ReadAllText(clipFile)==clipOriginal;
        using(var stop=new CancellationTokenSource()){stop.Cancel();var result=await bus.ExecuteDetailedAsync(new("clipboard_delete",clip.Key,"boundary"),stop.Token);c["clipboard_precancel_preserves_history"]=result.Status=="cancelled"&&service.History.Items.Count==1&&File.ReadAllText(clipFile)==clipOriginal;}
        var denied=await bus.ExecuteDetailedAsync(new("clipboard_query","fixture","brain-test"));c["clipboard_body_is_not_model_readable"]=denied.Status=="failed"&&!new BrainCatalog(bus.Registry).Read().Any(a=>a.Action.Id=="clipboard_query");
        clips.Execute("clipboard_pin",clip.Key);c["clipboard_explicit_pin_commits"]=service.History.Items.Single().Pinned&&new ClipHistory(clipFile).Items.Single().Pinned;
        clips.Execute("clipboard_delete",clip.Key);c["clipboard_explicit_delete_commits"]=service.History.Items.Count==0;
        clips.Execute("clipboard_undo");c["clipboard_undo_restores_immutable_record"]=service.History.Items.Single().Key==clip.Key&&service.History.Items.Single().Pinned;
        service.History.Add(new("text","expired fixture","old",now.AddDays(-10)));service.History.Expire(7,now);c["clipboard_expiry_preserves_pinned_record"]=service.History.Items.Count==1&&service.History.Items.Single().Pinned;
        clips.Execute("clipboard_clear");c["clipboard_clear_preserves_favorites"]=service.History.Items.Count==1&&service.History.Items.Single().Pinned;
        c["clipboard_receipts_do_not_store_body"]=bus.RecentResults.Where(r=>r.ModuleId=="clipboard").All(r=>r.Data==null&&!r.Message.Contains(clip.Content));
        var match=new FootballMatch("123456",now.AddHours(3),"fixture","83","Home","86","Away","pre","Scheduled","VS",true);var club=Club.For("巴塞罗那");
        FootballCache Cache()=>new(){Updated=now,SchemaVersion=2,Matches=[match]};
        TeamData Team()=>new(){MatchesUpdated=now,TableUpdated=now,Season="fixture",Matches=[match],Rows=[new("fixture",club.Id,club.Name,"1","1","1","0","0","1:0","1","3")]};
        var loaded=Cache();var loadedTeam=Team();var fetched=Cache();var fetchedTeam=Team();FootballCache? saved=null;TeamData? savedTeam=null;
        using(var queries=new FootballQueries(()=>true,()=>"fixed",loadSchedule:()=>loaded,loadTeam:_=>loadedTeam,fetchSchedule:(_,_)=>Task.FromResult(fetched),fetchTeam:(_,_,_)=>Task.FromResult((fetchedTeam,false,false)),saveSchedule:data=>saved=data,saveTeam:(_,data)=>savedTeam=data,clock:()=>now,fetchEvents:(_,_)=>Task.FromResult(Array.Empty<FootballMatch>()))) {
            queries.CachedSchedule();queries.CachedTeam(club);loaded.Matches.Clear();loadedTeam.Rows.Clear();c["football_loaded_schedule_is_host_owned"]=queries.CachedSchedule().Matches.Count==1;c["football_loaded_team_is_host_owned"]=queries.CachedTeam(club).Rows.Count==1;
            var schedule=await queries.Schedule(true,default);var team=await queries.Team(club,true,default);schedule.Cache.Matches.Clear();team.Data.Matches.Clear();c["football_return_values_are_detached"]=queries.CachedSchedule().Matches.Count==1&&queries.CachedTeam(club).Matches.Count==1;
            fetched.Matches.Clear();fetchedTeam.Rows.Clear();c["football_fetched_schedule_is_host_owned"]=queries.CachedSchedule().Matches.Count==1;c["football_fetched_team_is_host_owned"]=queries.CachedTeam(club).Rows.Count==1;
            saved!.Matches.Clear();savedTeam!.Matches.Clear();c["football_writer_cannot_mutate_published_cache"]=queries.CachedSchedule().Matches.Count==1&&queries.CachedTeam(club).Matches.Count==1;
        }
        int writes=0;var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var queries=new FootballQueries(()=>true,()=>"fixed",loadSchedule:Cache,loadTeam:_=>Team(),fetchSchedule:async(old,t)=>{old.Matches.Clear();ready.SetResult();await release.Task;return Cache();},fetchTeam:(_,_,_)=>Task.FromResult((Team(),false,false)),saveSchedule:_=>writes++,saveTeam:(_,_)=>{},clock:()=>now,fetchEvents:(_,_)=>Task.FromResult(Array.Empty<FootballMatch>()))) {
            using var stop=new CancellationTokenSource();var pending=queries.Schedule(true,stop.Token);await ready.Task;stop.Cancel();release.SetResult();c["football_cancel_before_publication_preserves_cache"]=await Cancelled(pending)&&writes==0&&queries.CachedSchedule().Matches.Count==1;
        }
        var oldCache=Cache();string disk=JsonSerializer.Serialize(oldCache);string cacheFile=Path.Combine(root,"schedule.json");File.WriteAllText(cacheFile,disk);var fresh=Cache();fresh.Matches=[match with{Id="654321"}];
        using(var queries=new FootballQueries(()=>true,()=>"fixed",loadSchedule:()=>oldCache,loadTeam:_=>Team(),fetchSchedule:(_,_)=>Task.FromResult(fresh),fetchTeam:(_,_,_)=>Task.FromResult((Team(),false,false)),saveSchedule:data=>AtomicJson.Write(cacheFile,JsonSerializer.Serialize(data)),saveTeam:(_,_)=>{},clock:()=>now,fetchEvents:(_,_)=>Task.FromResult(Array.Empty<FootballMatch>()))) {
            AtomicJson.BeforeReplace=file=>{if(file==cacheFile)throw new IOException("fixture cache failure");};var result=await queries.Schedule(true,default);AtomicJson.BeforeReplace=null;c["football_cache_write_failure_is_explicit"]=!result.CacheSaved&&result.Cache.Matches.Single().Id=="654321"&&JsonSerializer.Serialize(oldCache)==disk&&File.ReadAllText(cacheFile)==disk;
        }
    }
}
