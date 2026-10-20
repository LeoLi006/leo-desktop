using System.Globalization;
using System.Text.Json;
namespace LeoLauncher;

sealed class TodoCapability(TodoStore store,Func<Action,CancellationToken,Task> ui,Action<string> show,Func<NaturalTodoDraft,NaturalTodoDraft?> confirm):IStructuredLeoModule,IActionRequestAdapter,IBrainContributor,ICommittedActionModule
{
    readonly Dictionary<string,(string Action,string Value,ActionPayload Payload)> receipts=new();readonly object gate=new();
    public string Id=>"todos";public string Name=>"清单与提醒";public string Version=>"1";public bool Enabled=>true;public bool ConfigurationIsCurrent=>true;
    public IReadOnlyList<string> Actions=>["todos_open","todo_list","todo_add","todo_repeat","todo_complete","todo_complete_title","todo_edit","todo_move","todo_remind","todo_snooze","todo_postpone","todo_stop_repeat","todo_skip_repeat","todo_undo","todo_natural","todo_snapshot","todo_ensure","todo_completed","todo_important","todo_delete","todo_restore","todo_postpone_ids","todo_follow_match","todo_match_add","todo_match_sync","todo_match_hydrate","todo_due","todo_ack","todo_backup","todo_restore_backup","todo_quiet","todo_quiet_save"];
    public string BrainStamp=>store.Generation.ToString(CultureInfo.InvariantCulture);
    public int MaximumInputLength=>125000;
    public CapabilityDefinition Describe(string a)=>new(a,Id,"in-process","todo-local/v1",a is "todo_list" or "todo_snapshot" or "todo_due" or "todo_quiet"?"read":a=="todos_open"?"window":"write","serialized-store",15000);
    public string FailureCode(Exception e)=>e is ArgumentException or JsonException or KeyNotFoundException or FormatException or InvalidOperationException?"invalid_request":e is IOException or UnauthorizedAccessException?"storage_failed":"todo_failed";
    public IReadOnlyList<BrainAction> BrainActions=>[
        new("todo_list","查询清单","todos-query",true,"YYYY-MM-DD或week:YYYY-MM-DD；只有用户明确要求才能读取",Check:v=>ListDay(v)),
        new("todos_open","打开清单","text",Check:v=>{if(v is not("" or "week"))throw new ArgumentException("只支持每日或week视图");}),
        new("todo_add","新建事项","todo-create",Hint:"JSON {title,day,reminder?}，日期yyyy-MM-dd，提醒必须带时区",Check:v=>ValidateModel("todo_add",v),Preview:v=>Preview("todo_add",v)),
        new("todo_repeat","新建重复事项","todo-create",Hint:"JSON {title,day,frequency,reminder?}，frequency为daily或weekly",Check:v=>Validate("todo_repeat",v),Preview:v=>Preview("todo_repeat",v)),
        new("todo_complete","完成事项","todo-id",Hint:"使用本轮清单返回的真实Id",Preview:v=>Preview("todo_complete",v)),
        new("todo_edit","修改事项标题","todo-target",Hint:"JSON {id,title}，使用本轮真实Id",Check:v=>Validate("todo_edit",v),Preview:v=>Preview("todo_edit",v)),
        new("todo_move","事项改期","todo-target",Hint:"JSON {id,day}，使用本轮真实Id",Check:v=>Validate("todo_move",v),Preview:v=>Preview("todo_move",v)),
        new("todo_remind","设置或取消提醒","todo-target",Hint:"JSON {id,time}，时间带时区，空字符串取消",Check:v=>Validate("todo_remind",v),Preview:v=>Preview("todo_remind",v)),
        new("todo_skip_repeat","仅跳过这次重复","todo-id",Hint:"使用本轮重复事项Id",Preview:v=>Preview("todo_skip_repeat",v))];
    static void ValidateModel(string action,string value){Validate(action,value);using var d=JsonDocument.Parse(value);if(d.RootElement.TryGetProperty("origin",out _))throw new ArgumentException("模型不能指定便签来源");}
    string Preview(string action,string value)
    {
        using var d=value.StartsWith('{')?JsonDocument.Parse(value):null;var p=d?.RootElement??default;
        if(action is "todo_add" or "todo_repeat")return "新增："+p.GetProperty("title").GetString()+" · "+p.GetProperty("day").GetString()+(action=="todo_repeat"?(p.GetProperty("frequency").GetString()=="daily"?" · 每天重复":" · 每周重复"):"")+(p.TryGetProperty("reminder",out var at)&&at.ValueKind==JsonValueKind.String?" · 提醒 "+at.GetString():"");
        string id=d==null?value:p.GetProperty("id").GetString()!;var task=store.Items.SingleOrDefault(t=>t.Id==id)??throw new ArgumentException("事项已失效，请重新查询");
        return action switch{"todo_complete"=>"完成："+task.Title,"todo_skip_repeat"=>"仅跳过："+task.Title+" · "+task.Day,"todo_edit"=>"修改标题："+task.Title+" → "+p.GetProperty("title").GetString()+(p.TryGetProperty("future",out var future)&&future.GetBoolean()?"（含后续重复）":""),"todo_move"=>"改期："+task.Title+" → "+p.GetProperty("day").GetString(),_=>"提醒："+task.Title+" → "+(p.GetProperty("time").GetString() is {Length:>0} time?time:"取消")};
    }
    internal static DateOnly Day(string value)=>DateOnly.ParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture);
    internal static DateOnly ListDay(string v)=>Day(v.StartsWith("week:")?v[5..]:v.Length==0?DateTime.Today.ToString("yyyy-MM-dd"):v);
    static string[] Fields(string a)=>a switch{
        "todo_add"=>["title","day","reminder","origin"],"todo_repeat"=>["title","day","frequency","reminder"],"todo_edit"=>["id","title","future"],"todo_move"=>["id","day"],"todo_remind"=>["id","time","series"],"todo_snooze"=>["id","minutes"],"todo_stop_repeat"=>["series_id","after"],"todo_completed" or "todo_important"=>["id","value"],"todo_match_sync" or "todo_match_hydrate"=>["matches","observed"],_=>[]};
    internal static void Validate(string a,string value)
    {
        using var d=JsonDocument.Parse(value);var p=d.RootElement;var allowed=Fields(a);if(p.ValueKind!=JsonValueKind.Object||p.EnumerateObject().Any(x=>!allowed.Contains(x.Name))||p.EnumerateObject().Select(x=>x.Name).Distinct().Count()!=p.EnumerateObject().Count())throw new ArgumentException("清单字段多余或重复");
        string S(string n)=>p.GetProperty(n).GetString()??throw new ArgumentException("缺少 "+n);
        if(a is "todo_add" or "todo_repeat" or "todo_edit"){string title=S("title").Trim();if(title.Length is <1 or >200)throw new ArgumentException("事项请填写1到200个字");}
        if(a is "todo_add" or "todo_repeat" or "todo_move")Day(S("day"));
        if(a=="todo_repeat"&&S("frequency") is not("daily" or "weekly"))throw new ArgumentException("重复方式无效");
        if(a=="todo_remind")Time(S("time"));
        if(p.TryGetProperty("reminder",out var at)&&at.ValueKind!=JsonValueKind.Null)Time(at.GetString()??"");
        if(a=="todo_stop_repeat")Day(S("after"));
    }
    internal static DateTimeOffset? Time(string value)
    {
        if(value.Length==0)return null;
        if(!System.Text.RegularExpressions.Regex.IsMatch(value,@"T\d{2}:\d{2}.*(Z|[+-]\d{2}:\d{2})$")||!DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out var time)||time<=DateTimeOffset.Now)throw new ArgumentException("提醒须为未来时间，使用带时区的ISO时间");return time;
    }
    public ActionRequest Adapt(StructuredActionRequest r)
    {
        if(r.Protocol!="leo-action/1"||!Actions.Contains(r.ActionId)||r.Parameters.ValueKind!=JsonValueKind.Object)throw new ArgumentException("清单请求协议无效");
        string value;
        if(Fields(r.ActionId).Length>0){value=r.Parameters.GetRawText();Validate(r.ActionId,value);}
        else{var fields=r.Parameters.EnumerateObject().ToArray();if(fields.Length>1||fields.Any(x=>x.Name!="value"||x.Value.ValueKind!=JsonValueKind.String))throw new ArgumentException("此动作只接受value文字参数");value=fields.Length==0?"":fields[0].Value.GetString()!;}
        return new(r.ActionId,value,r.Source,r.RequestId,r.TraceId,r.Deadline);
    }
    public bool TryCommitted(ActionRequest r,out ActionPayload payload){lock(gate){if(r.RequestId is {} id&&receipts.TryGetValue(id,out var saved)&&saved.Action==r.Action&&saved.Value==r.Value){payload=saved.Payload;return true;}}payload=null!;return false;}
    public async Task<string> ExecuteAsync(ActionRequest r,CancellationToken t)=>(await ExecuteStructuredAsync(r,t)).Message;
    public async Task<ActionPayload> ExecuteStructuredAsync(ActionRequest r,CancellationToken token)
    {
        ActionPayload? result=null;
        await ui(()=>{
            token.ThrowIfCancellationRequested();
            lock(gate){if(r.RequestId is {} id&&receipts.TryGetValue(id,out var old)){if(old.Action!=r.Action||old.Value!=r.Value)throw new ArgumentException("同一请求编号不能用于不同操作");result=old.Payload;return;}}
            if(r.Source.StartsWith("brain-",StringComparison.Ordinal)&&!BrainActions.Any(a=>a.Id==r.Action))throw new ArgumentException("此清单内部动作不提供给模型");
            var previous=store.OperationToken;store.OperationToken=token;store.DeferNotifications=true;
            try{result=Execute(r,token);if(result.Evidence=="committed"&&r.RequestId is {} key)lock(gate){receipts[key]=(r.Action,r.Value,result);while(receipts.Count>256)receipts.Remove(receipts.Keys.First());}}
            finally{store.OperationToken=previous;store.DeferNotifications=false;if(store.PendingNotification){store.PendingNotification=false;store.PublishChanges();}}
        },token);
        return result!;
    }
    ActionPayload Execute(ActionRequest r,CancellationToken token)
    {
        string v=r.Value;using var doc=Fields(r.Action).Length>0?JsonDocument.Parse(v):null;var p=doc?.RootElement??default;
        if(doc!=null)Validate(r.Action,v);
        string S(string n)=>p.GetProperty(n).GetString()??throw new ArgumentException("缺少 "+n);
        bool B(string n)=>p.TryGetProperty(n,out var x)&&x.GetBoolean();
        DailyTask Find(string id)=>store.Items.SingleOrDefault(x=>x.Id==id)??throw new ArgumentException("事项不存在，请重新查询");
        ActionPayload Data(object? data,string evidence="committed")=>new(evidence=="snapshot"?JsonSerializer.Serialize(data):"清单操作已完成。",JsonSerializer.SerializeToElement(data),evidence,true,"清单"+(evidence=="snapshot"?"查询":"操作")+"已完成。");
        void Empty(){if(v.Length!=0)throw new ArgumentException("此动作不接受额外参数");}
        switch(r.Action){
            case "todos_open":if(v is not("" or "week"))throw new ArgumentException("清单视图无效");show(v);return new("已打开清单。",Evidence:"window-shown");
            case "todo_snapshot":Empty();return Data(store.Items,"snapshot");
            case "todo_list":var date=ListDay(v);store.EnsureThrough(date.AddDays(7));return Data(v.StartsWith("week:")?store.ForWeek(date):store.ForDay(date),"snapshot");
            case "todo_add":case "todo_repeat":
                var day=Day(S("day"));var at=p.TryGetProperty("reminder",out var reminder)&&reminder.ValueKind!=JsonValueKind.Null?Time(reminder.GetString()??""):null;
                string identity=r.Action=="todo_add"&&p.TryGetProperty("origin",out var origin)&&origin.ValueKind==JsonValueKind.String?"note:"+(origin.GetString()??throw new ArgumentException("便签编号为空")):"request:"+r.RequestId;
                if(identity.StartsWith("note:")&&(identity.Length is <6 or >160||r.Source.StartsWith("brain-")))throw new ArgumentException("便签来源编号无效");
                var created=store.AddIdentified(S("title"),day,r.Action=="todo_repeat"?S("frequency"):"",at,identity);return Data(new{success=true,task=created});
            case "todo_complete":var task=Find(v);if(task.Completed==null)store.Toggle(v);return Data(new{task=Find(v)});
            case "todo_complete_title":var matches=store.ForDay(DateOnly.FromDateTime(DateTime.Today)).Where(t=>t.Completed==null&&t.Title.Equals(v,StringComparison.OrdinalIgnoreCase)).ToArray();if(matches.Length!=1)throw new ArgumentException("没有唯一匹配的事项，请在清单中选择");store.Toggle(matches[0].Id);return Data(new{task=Find(matches[0].Id)});
            case "todo_completed":var target=Find(S("id"));if((target.Completed!=null)!=B("value"))store.Toggle(target.Id);break;
            case "todo_edit":store.Rename(S("id"),S("title"),B("future"));break;
            case "todo_move":store.MoveTo(S("id"),Day(S("day")));break;
            case "todo_remind":store.SetReminder(S("id"),Time(S("time")),B("series"));break;
            case "todo_snooze":store.Snooze(S("id"),p.GetProperty("minutes").GetInt32(),DateTimeOffset.Now);break;
            case "todo_important":store.SetImportant(S("id"),B("value"));break;
            case "todo_skip_repeat":store.SkipOccurrence(v);break;
            case "todo_stop_repeat":store.StopRepeating(S("series_id"),Day(S("after")));break;
            case "todo_postpone":return Data(store.Postpone(v.StartsWith("day:")?store.ForDay(Day(v[4..])).Where(x=>x.Completed==null).Select(x=>x.Id):[Find(v).Id]));
            case "todo_postpone_ids":var ids=JsonSerializer.Deserialize<string[]>(v)??throw new ArgumentException("事项编号无效");foreach(var id in ids)Find(id);return Data(store.Postpone(ids));
            case "todo_delete":return Data(store.Delete(Find(v).Id));
            case "todo_restore":store.Restore(JsonSerializer.Deserialize<DailyTask>(v)??throw new ArgumentException("事项无效"));break;
            case "todo_undo":Empty();return Data(store.Undo());
            case "todo_ensure":store.EnsureThrough(Day(v));return Data(null,"snapshot");
            case "todo_follow_match":store.FollowMatch(v);break;
            case "todo_match_add":return Data(store.AddMatch(JsonSerializer.Deserialize<MatchEnvelope>(v)??throw new ArgumentException("比赛引用无效"),DateTimeOffset.Now,token));
            case "todo_match_sync":return Data(store.SyncMatches(p.GetProperty("matches").Deserialize<FootballMatch[]>()!,p.GetProperty("observed").GetDateTimeOffset()));
            case "todo_match_hydrate":store.HydrateMatchMetadata(p.GetProperty("matches").Deserialize<FootballMatch[]>()!,p.GetProperty("observed").GetDateTimeOffset());break;
            case "todo_due":return Data(store.Due(DateTimeOffset.Parse(v,CultureInfo.InvariantCulture)),"snapshot");
            case "todo_ack":store.Acknowledge(JsonSerializer.Deserialize<ReminderTicket[]>(v)??throw new ArgumentException("提醒票据无效"),DateTimeOffset.Now);break;
            case "todo_backup":Empty();store.BackupNow();break;
            case "todo_restore_backup":store.RestoreBackup(v);break;
            case "todo_quiet":Empty();return Data(store.ReadQuiet(),"snapshot");
            case "todo_quiet_save":store.SaveQuiet(JsonSerializer.Deserialize<QuietHours>(v)??throw new ArgumentException("时段无效"));break;
            case "todo_natural":var draft=confirm(JsonSerializer.Deserialize<NaturalTodoDraft>(v)??throw new ArgumentException("安排无效"));if(draft==null)throw new OperationCanceledException(token);token.ThrowIfCancellationRequested();return Data(new{task=store.AddIdentified(draft.Title,draft.Day,draft.Frequency,draft.Time is {} time?new DateTimeOffset(draft.Day.ToDateTime(time)):null,"request:"+r.RequestId)});
            default:throw new ArgumentException("未知清单动作");
        }
        return Data(null);
    }
}

sealed record ReminderTicket(string Id,DateTimeOffset? Reminder,DateTimeOffset? SnoozedUntil);
sealed partial class TodoStore
{
    internal CancellationToken OperationToken;
    internal long Generation;internal bool DeferNotifications,PendingNotification;
    internal void PublishChanges()=>NotifyChanged();
    internal TodoClient? SharedClient;
    internal TodoClient Client=>SharedClient??=TodoClient.Standalone(this);
    internal DailyTask AddIdentified(string title,DateOnly day,string frequency,DateTimeOffset? reminder,string identity)
    {
        string id=BrainCatalog.Hash("leo-todo/v1:"+identity)[..32].ToLowerInvariant();var existing=records.FirstOrDefault(t=>t.Id==id);
        if(existing!=null)return existing.Repeat==null?existing:Items.First(t=>t.SeriesId==id);
        title=Title(title);if(frequency is not("" or "daily" or "weekly"))throw new ArgumentException("重复方式无效");
        var task=new DailyTask(id,title,day,DateTimeOffset.Now,Reminder:reminder,Repeat:frequency.Length==0?null:new(frequency));
        Commit(frequency.Length==0?records.Append(task).ToList():Generate(records.Append(task).ToList(),day>DateOnly.FromDateTime(DateTime.Today).AddDays(35)?day:DateOnly.FromDateTime(DateTime.Today).AddDays(35)));
        return frequency.Length==0?task:Items.First(t=>t.SeriesId==id&&t.OccurrenceDay==day);
    }
    internal void Acknowledge(ReminderTicket[] tickets,DateTimeOffset now)
    {
        var valid=tickets.Where(x=>Items.Any(t=>t.Id==x.Id&&t.Reminder==x.Reminder&&t.SnoozedUntil==x.SnoozedUntil&&t.Notified==null&&t.Completed==null&&t.Reminder<=now)).Select(x=>x.Id).ToHashSet();if(valid.Count>0)MarkNotified(valid,now);
    }
    QuietHours? previewQuiet;
    internal QuietHours ReadQuiet(){if(path==null)return previewQuiet??new();string file=Path.Combine(Path.GetDirectoryName(path)!,"leo-reminder-settings.json");try{return AtomicJson.Read(file,json=>{var q=JsonSerializer.Deserialize<QuietHours>(json)??throw new ArgumentException("提醒配置为空");if(q.Start is <0 or >=1440||q.End is <0 or >=1440||q.Start==q.End)throw new ArgumentException("时段无效");return q;});}catch{return new();}}
    internal void SaveQuiet(QuietHours value){if(value.Start is <0 or >=1440||value.End is <0 or >=1440||value.Start==value.End)throw new ArgumentException("免打扰时间无效");OperationToken.ThrowIfCancellationRequested();if(path==null)previewQuiet=value;else AtomicJson.Write(Path.Combine(Path.GetDirectoryName(path)!,"leo-reminder-settings.json"),JsonSerializer.Serialize(value));}
}
