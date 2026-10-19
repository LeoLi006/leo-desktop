using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;
static class RobustnessTests
{
    static void Pump(Task task){var watch=Stopwatch.StartNew();while(!task.IsCompleted&&watch.ElapsedMilliseconds<30000){Application.DoEvents();Thread.Sleep(5);}if(!task.IsCompleted)throw new TimeoutException();task.GetAwaiter().GetResult();}
    public static void Run(string report){Directory.CreateDirectory(Path.GetDirectoryName(report)!);try{var c=new Dictionary<string,bool>();string dir=Path.Combine(Path.GetDirectoryName(report)!,"fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);Storage(c,dir);Pump(Lifecycle(c,dir));using(var pet=new PetForm(new Preferences{ApplicationIndexing=false},true)){pet.Show();Application.DoEvents();foreach(var pair in pet.VerifyRenderingResources())c[pair.Key]=pair.Value;pet.Close();}File.WriteAllText(report,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}finally{AtomicJson.BeforeReplace=null;}}
    static void Storage(Dictionary<string,bool> c,string dir)
    {
        string file=Path.Combine(dir,"config.json");AtomicJson.Write(file,"{\"value\":1}");AtomicJson.Write(file,"{\"value\":2}");
        c["atomic_replace_keeps_previous_complete_backup"]=File.ReadAllText(file).Contains(":2")&&File.ReadAllText(file+".bak").Contains(":1");
        AtomicJson.BeforeReplace=_=>throw new IOException("injected before rename");try{AtomicJson.Write(file,"{\"value\":3}");c["interrupted_write_keeps_original"]=false;}catch(IOException){c["interrupted_write_keeps_original"]=File.ReadAllText(file).Contains(":2");}finally{AtomicJson.BeforeReplace=null;}
        c["failed_write_cleans_only_its_temporary_file"]=Directory.GetFiles(dir,"*.tmp").Length==0;
        try{AtomicJson.Write(file,"{truncated");c["invalid_new_json_cannot_replace_original"]=false;}catch(JsonException){c["invalid_new_json_cannot_replace_original"]=File.ReadAllText(file).Contains(":2");}
        using(var locked=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.None)){try{AtomicJson.Write(file,"{\"value\":4}");c["locked_original_is_never_truncated"]=false;}catch(IOException){c["locked_original_is_never_truncated"]=true;}}
        c["locked_write_preserves_bytes_and_removes_temp"]=File.ReadAllText(file).Contains(":2")&&Directory.GetFiles(dir,"*.tmp").Length==0;
        File.WriteAllText(file,"{broken");using(var loaded=AtomicJson.Read(file,text=>JsonDocument.Parse(text))){c["corrupt_primary_recovers_valid_backup"]=loaded.RootElement.GetProperty("value").GetInt32()==1;}
        c["recovery_preserves_corrupt_primary_and_good_backup"]=Directory.GetFiles(dir,"config.json.corrupt-*").Any(f=>File.ReadAllText(f)=="{broken")&&File.ReadAllText(file+".bak").Contains(":1");
        File.Delete(file);using(var loaded=AtomicJson.Read(file,text=>JsonDocument.Parse(text)))c["missing_primary_recovers_backup"]=loaded.RootElement.GetProperty("value").GetInt32()==1;
        Parallel.For(0,20,i=>AtomicJson.Write(file,JsonSerializer.Serialize(new{value=i})));using(var loaded=JsonDocument.Parse(File.ReadAllText(file)))c["concurrent_writes_remain_complete_json"]=loaded.RootElement.TryGetProperty("value",out _);
        string prefs=Path.Combine(dir,"settings.json");AtomicJson.Write(prefs,JsonSerializer.Serialize(new Preferences{PetSize=120}));AtomicJson.Write(prefs,JsonSerializer.Serialize(new Preferences{PetSize=140}));File.WriteAllText(prefs,"{\"PetSize\":999}");c["settings_reject_semantically_invalid_primary"]=AtomicJson.Read(prefs,Store.Validate).PetSize==120;
        string noteFile=Path.Combine(dir,"notes.json");var notes=new NoteStore(noteFile);notes.Save(null,"first");notes.Save(null,"second");File.WriteAllText(noteFile,"{bad");var recovered=new NoteStore(noteFile);c["notes_recover_previous_complete_snapshot"]=recovered.Items.Count==1&&recovered.Items[0].Text=="first";
        AtomicJson.BeforeReplace=_=>throw new IOException();try{recovered.Save(null,"not committed");c["note_failed_write_rolls_back_memory"]=false;}catch(IOException){c["note_failed_write_rolls_back_memory"]=recovered.Items.Count==1&&new NoteStore(noteFile).Items.Count==1;}finally{AtomicJson.BeforeReplace=null;}
        string tasks=Path.Combine(dir,"todos.json");var todos=new TodoStore(tasks);todos.Add("first",DateOnly.FromDateTime(DateTime.Today));todos.AddRepeating("repeat",DateOnly.FromDateTime(DateTime.Today),"weekly");int count=todos.Items.Count;File.WriteAllText(tasks,"{bad");var restored=new TodoStore(tasks);c["todos_recover_latest_snapshot_with_repeat_rules"]=restored.Error==null&&restored.Items.Count==count&&restored.Items.Any(t=>t.SeriesId!=null);
        restored.Changed+=()=>throw new InvalidOperationException();restored.Add("observer failure",DateOnly.FromDateTime(DateTime.Today));c["todo_observer_failure_cannot_undo_successful_save"]=new TodoStore(tasks).Items.Count==count+1;
        AtomicJson.BeforeReplace=p=>{if(p==tasks)throw new IOException();};try{restored.Add("must not exist",DateOnly.FromDateTime(DateTime.Today));c["todo_failed_commit_preserves_memory_and_file"]=false;}catch(IOException){c["todo_failed_commit_preserves_memory_and_file"]=restored.Items.Count==count+1&&new TodoStore(tasks).Items.Count==count+1;}finally{AtomicJson.BeforeReplace=null;}
        string isolated=Path.Combine(dir,"unrecoverable");Directory.CreateDirectory(isolated);string noBackup=Path.Combine(isolated,"leo-todos.json");File.WriteAllText(noBackup,"broken");var broken=new TodoStore(noBackup);bool blocked=false;try{broken.Add("no",DateOnly.FromDateTime(DateTime.Today));}catch(IOException){blocked=true;}c["unrecoverable_todos_are_read_only_and_preserved"]=blocked&&broken.Error!=null&&File.ReadAllText(noBackup)=="broken";
        string source=Path.Combine(dir,"migration-source"),target=Path.Combine(dir,"migration-target");Directory.CreateDirectory(source);Directory.CreateDirectory(target);
        var sourceTasks=new TodoStore(Path.Combine(source,"leo-todos.json"));sourceTasks.Add("imported",DateOnly.FromDateTime(DateTime.Today));string bundle=Path.Combine(dir,"bundle.leo.json");new ConfigMigration(source).Export(bundle,new Preferences{PetSize=160},true);
        File.WriteAllText(Path.Combine(target,"leo-ai.json"),"fixture-private-value");var importedTasks=new TodoStore(Path.Combine(target,"leo-todos.json"));var migration=new ConfigMigration(target);var current=new Preferences{PetSize=120};var plan=migration.Inspect(bundle);migration.Apply(plan,current,p=>current=p,()=>importedTasks.ReloadImported());
        c["atomic_migration_keeps_api_private_and_applies_tasks"]=current.PetSize==160&&importedTasks.Items.Single().Title=="imported"&&File.ReadAllText(Path.Combine(target,"leo-ai.json"))=="fixture-private-value";
        File.WriteAllText(Path.Combine(target,"leo-todos.json"),"broken after import");c["import_refreshes_latest_snapshot_before_future_recovery"]=new TodoStore(Path.Combine(target,"leo-todos.json")).Items.Single().Title=="imported";
        var primaryBefore=Directory.GetFiles(target,"*.json").ToDictionary(p=>p,File.ReadAllText);int attempts=0;bool failed=false;try{migration.Apply(plan,current,p=>{if(++attempts==1)throw new IOException("injected apply");current=p;},()=>importedTasks.ReloadImported());}catch(IOException){failed=true;}c["failed_import_still_rolls_back_primary_files"]=failed&&primaryBefore.All(p=>File.ReadAllText(p.Key)==p.Value);
        using var root=new Panel();root.Controls.Add(new Panel());BarcaScrollTheme.Attach(root);int before=BarcaScrollTheme.AttachmentCount;for(int i=0;i<100;i++)BarcaScrollTheme.Attach(root);c["repeated_scroll_attach_does_not_revisit_children"]=BarcaScrollTheme.AttachmentCount==before;root.Controls.Add(new TextBox());c["new_children_receive_scroll_theme_once"]=BarcaScrollTheme.AttachmentCount==before+1;
    }
    static async Task Lifecycle(Dictionary<string,bool> c,string dir)
    {
        var prefs=new Preferences();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var dispatcher=new ActionDispatcher(prefs,async(a,v,t)=>{entered.TrySetResult();await Task.Delay(60000,t);return "late";},new ExtensionStore(Path.Combine(dir,"modules")))){
            var operation=dispatcher.ExecuteAsync(new("proxy_on"));await entered.Task;prefs.ClashModule=false;dispatcher.CancelDisabled();bool cancelled=false;try{await operation;}catch(OperationCanceledException){cancelled=true;}c["disabling_module_cancels_its_active_action"]=cancelled;
        }
        entered=new(TaskCreationOptions.RunContinuationsAsynchronously);var shutting=new ActionDispatcher(new Preferences(),async(a,v,t)=>{entered.TrySetResult();await Task.Delay(60000,t);return "late";},new ExtensionStore(Path.Combine(dir,"modules")));var wait=shutting.ExecuteAsync(new("proxy_on"));await entered.Task;shutting.Dispose();try{await wait;c["dispatcher_shutdown_cancels_inflight_work"]=false;}catch(OperationCanceledException){c["dispatcher_shutdown_cancels_inflight_work"]=true;}shutting.Dispose();
        int feedback=0;using(var hub=new ClientOperationHub(_=>feedback++)){entered=new(TaskCreationOptions.RunContinuationsAsynchronously);var pending=hub.Run("fixture","wait",async op=>{entered.TrySetResult();await Task.Delay(60000,op.Token);return "late";});await entered.Task;hub.Dispose();int at=feedback;try{await pending.WaitAsync(TimeSpan.FromSeconds(3));c["client_shutdown_cancels_pending_operation"]=false;}catch(OperationCanceledException){c["client_shutdown_cancels_pending_operation"]=true;}c["closed_client_hub_emits_no_late_feedback"]=feedback==at;bool reject=false;try{await hub.Run("fixture","later",_=>Task.FromResult("bad"));}catch(ObjectDisposedException){reject=true;}c["closed_hub_rejects_new_requests"]=reject;}
        bool stopped=false;var tool=new AppTools(()=>[],_=>{},leoActions:async(a,v,t)=>{try{await Task.Delay(60000,t);return "late";}catch(OperationCanceledException){stopped=true;throw;}});
        using(var chat=new ChatForm(true,applicationTools:tool)){chat.Show();var pending=chat.PreviewSendAsync("开启系统代理");chat.Dispose();await pending.WaitAsync(TimeSpan.FromSeconds(3));c["direct_chat_dispose_cancels_local_tool_without_late_ui"]=stopped&&chat.IsDisposed;chat.Dispose();}
        using(var settings=new ApiSettingsForm(new ChatSettings(),true)){settings.Show();settings.Dispose();settings.Dispose();c["embedded_api_settings_support_direct_and_repeated_dispose"]=settings.IsDisposed;}
        using(var health=new HealthChecksView(new Preferences(),true,null)){health.Dispose();health.Dispose();c["health_view_disposal_is_idempotent"]=health.IsDisposed;}
        using(var updates=new UpdateForm(true)){updates.Show();updates.Dispose();updates.Dispose();c["update_window_disposal_is_idempotent"]=updates.IsDisposed;}
    }
}
sealed partial class PetForm
{
    internal Dictionary<string,bool> VerifyRenderingResources()
    {
        var c=new Dictionary<string,bool>();animation.Stop();RevealDock(true);row=frame=0;Invalidate();Update();var original=paintSurface;
        for(int i=0;i<80;i++){Invalidate();Update();}c["steady_render_reuses_bitmap_instance"]=original!=null&&ReferenceEquals(original,paintSurface);
        using(var expected=RenderPetSurface())c["reused_surface_matches_fresh_surface_pixels"]=SkinAnimationTests.Hash(expected)==SkinAnimationTests.Hash(paintSurface!);
        prefs.PetSize=180;ResizePet();Invalidate();Update();c["resize_recreates_matching_buffer"]=paintSurface?.Size==ClientSize&&!ReferenceEquals(original,paintSurface);
        SetFullscreenHidden(true);c["hidden_pet_stops_animation_dock_and_hover_timers"]=!animation.Enabled&&!dockTimer.Enabled&&!hoverTimer.Enabled;SetFullscreenHidden(false);c["restore_restarts_animation"]=animation.Enabled;
        animation.Stop();dockSide=DockSide.Left;dockMotion.BeginTuck(0);for(int t=0;t<=2400;t+=32)dockMotion.Tick(t,true,false);Invalidate();Update();int before=LayeredFrames;
        for(int i=0;i<20;i++)Animate();Update();c["settled_dock_does_not_advance_idle_animation"]=LayeredFrames==before;
        return c;
    }
}
