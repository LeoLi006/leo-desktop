using System.Text.Json;
namespace LeoLauncher;

// Synchronous editors execute on the owning UI thread. Rendering reads immutable task records;
// mutations and business queries always pass through the same registered capability.
sealed class TodoClient(TodoStore store,ActionDispatcher bus,string source="todo-ui")
{
    internal ActionDispatcher Dispatcher=>bus;
    public TodoClient From(string origin)=>new(store,bus,origin);
    public event Action? Changed{add=>store.Changed+=value;remove=>store.Changed-=value;}
    public List<DailyTask> Items=>store.Items;
    public bool CanUndo=>store.CanUndo;
    public string? Error=>store.Error;
    public string? BackupError=>store.BackupError;
    public string[] Backups()=>store.Backups();
    public Recurrence? RuleFor(string? id)=>store.RuleFor(id);
    ActionResult Run(string action,string value="",string? id=null){var r=bus.ExecuteDetailedAsync(new(action,value,source,id)).GetAwaiter().GetResult();r.ToLegacy(default);return r;}
    ActionResult Json(string action,object value,string? id=null)=>Run(action,JsonSerializer.Serialize(value),id);
    string? pendingValue,pendingId;
    public DailyTask Add(string title,DateOnly day,DateTimeOffset? reminder=null,string? origin=null)=>Create("todo_add",new{title,day,reminder,origin});
    public DailyTask AddRepeating(string title,DateOnly day,string frequency,DateTimeOffset? reminder=null)=>Create("todo_repeat",new{title,day,frequency,reminder});
    DailyTask Create(string action,object value)
    {
        // Keep the identity until a confirmed receipt, including after an interrupted wait.
        string json=JsonSerializer.Serialize(value);if(pendingValue!=action+json){pendingValue=action+json;pendingId=Guid.NewGuid().ToString("N");}
        var result=Run(action,json,pendingId);var task=result.Data!.Value.GetProperty("task").Deserialize<DailyTask>()!;pendingValue=pendingId=null;return task;
    }
    public List<DailyTask> ForDay(DateOnly day)=>Run("todo_list",day.ToString("yyyy-MM-dd")).Data!.Value.Deserialize<List<DailyTask>>()!;
    public List<DailyTask> ForWeek(DateOnly day)=>Run("todo_list","week:"+day.ToString("yyyy-MM-dd")).Data!.Value.Deserialize<List<DailyTask>>()!;
    public List<DailyTask> Overdue(DateOnly day)=>Items.Where(t=>t.Day<day&&t.Completed==null).OrderBy(t=>t.Day).ThenBy(t=>t.Created).ToList();
    public void EnsureThrough(DateOnly day)=>Run("todo_ensure",day.ToString("yyyy-MM-dd"));
    public void Toggle(string id){var task=Items.Single(t=>t.Id==id);SetCompleted(id,task.Completed==null);}
    public void SetCompleted(string id,bool value)=>Json("todo_completed",new{id,value});
    public void Rename(string id,string title,bool future=false)=>Json("todo_edit",new{id,title,future});
    public void MoveTo(string id,DateOnly day)=>Json("todo_move",new{id,day});
    public void SetReminder(string id,DateTimeOffset? time,bool applyToSeries=false)=>Json("todo_remind",new{id,time=time?.ToString("O")??"",series=applyToSeries});
    public void Snooze(string id,int minutes,DateTimeOffset now)=>Json("todo_snooze",new{id,minutes});
    public void StopRepeating(string series,DateOnly after)=>Json("todo_stop_repeat",new{series_id=series,after});
    public void SkipOccurrence(string id)=>Run("todo_skip_repeat",id);
    public void SetImportant(string id,bool value)=>Json("todo_important",new{id,value});
    public DateOnly PostponeDay(string id)=>store.PostponeDay(id);
    public int Postpone(IEnumerable<string> ids)=>Json("todo_postpone_ids",ids.ToArray()).Data!.Value.GetInt32();
    public DailyTask? Delete(string id)=>Run("todo_delete",id).Data!.Value.Deserialize<DailyTask>();
    public void Restore(DailyTask task)=>Json("todo_restore",task);
    public bool Undo()=>Run("todo_undo").Data!.Value.GetBoolean();
    public void FollowMatch(string id)=>Run("todo_follow_match",id);
    public MatchPlan AddMatch(MatchEnvelope input)=>Json("todo_match_add",input).Data!.Value.Deserialize<MatchPlan>()!;
    public void HydrateMatchMetadata(IEnumerable<FootballMatch> matches,DateTimeOffset observed)
    {
        // Only legacy tasks without metadata need hydration, not the entire league cache.
        var ids=Items.Where(t=>t.MatchId!=null&&t.MatchKickoff==null).Select(t=>t.MatchId!).ToHashSet(StringComparer.Ordinal);
        if(ids.Count==0)return;
        foreach(var batch in matches.Where(m=>ids.Contains("espn:"+m.Id)).GroupBy(m=>m.Id).Select(g=>g.Last()).Chunk(16))
            Json("todo_match_hydrate",new{matches=batch,observed});
    }
    public void SyncMatches(IEnumerable<FootballMatch> matches,DateTimeOffset observed)=>Json("todo_match_sync",new{matches,observed});
    public DailyTask[] Due(DateTimeOffset now)=>Run("todo_due",now.ToString("O")).Data!.Value.Deserialize<DailyTask[]>()!;
    public void Acknowledge(IEnumerable<DailyTask> tasks)=>Json("todo_ack",tasks.Select(t=>new ReminderTicket(t.Id,t.Reminder,t.SnoozedUntil)).ToArray());
    public void BackupNow()=>Run("todo_backup");
    public void RestoreBackup(string path)=>Run("todo_restore_backup",path);
    public QuietHours Quiet()=>Run("todo_quiet").Data!.Value.Deserialize<QuietHours>()!;
    public void SaveQuiet(QuietHours value)=>Json("todo_quiet_save",value);
    public static TodoClient Standalone(TodoStore store)
    {
        var bus=new ActionDispatcher(new(),(_,_,_)=>throw new InvalidOperationException("清单不可绕回旧入口"),new ExtensionStore(Path.Combine(Path.GetTempPath(),"leo-todo-noextensions")));
        bus.Registry.Register(new TodoCapability(store,(action,t)=>{t.ThrowIfCancellationRequested();action();return Task.CompletedTask;},_=>{},draft=>draft));return new(store,bus);
    }
}

sealed partial class PetForm
{
    TodoClient todoClient=null!;
    void RegisterTodoCapability()
    {
        var ui=new ModuleWindowService(this);
        commands.Registry.Register(new TodoCapability(todoStore,ui.ShowAsync,value=>{ShowTodosCore();todoWindow!.View.SetWeek(value=="week");},draft=>{using var dialog=new NaturalTodoForm(draft);return dialog.ShowDialog(this)==DialogResult.OK?dialog.Draft:null;},(view,token)=>presentation.ShowAsync(new(ModuleWindowTarget.Todos,View:view=="week"?"week":"day"),token)));
        todoClient=new(todoStore,commands);todoStore.SharedClient=todoClient;
    }
}
