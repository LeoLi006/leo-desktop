using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;
static class BrainChainTests
{
    static ToolCall Tool(string name,object value)=>new(Guid.NewGuid().ToString("N"),name,JsonSerializer.Serialize(value));
    static object Step(string action,string value="",string from_step="",string result_property="")=>new{action,value,from_step,result_property};
    static async Task<bool> Rejected(Func<Task> action){try{await action();return false;}catch(Exception e)when(e is ArgumentException or InvalidOperationException or OperationCanceledException or IOException){return true;}}
    static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control child in root.Controls)foreach(var item in Walk(child))yield return item;}
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{
            var c=new Dictionary<string,bool>();try{await Check(Path.GetDirectoryName(report)!,ui,c);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,real_api_requests=0,personal_data_reads=0,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{AtomicJson.BeforeReplace=null;ui.Close();}
        };Application.Run(ui);
    }
    static async Task Check(string directory,Form ui,Dictionary<string,bool> c)
    {
        string root=Path.Combine(directory,"fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var now=DateTimeOffset.UtcNow;var day=DateOnly.FromDateTime(DateTime.Today);var prefs=new Preferences{ApplicationIndexing=false};
        string tasksFile=Path.Combine(root,"todos.json"),notesFile=Path.Combine(root,"notes.json");var todos=new TodoStore(tasksFile);var notes=new NoteStore(notesFile);var match=new FootballMatch("123456",now.AddHours(5),"fixture","83","Barcelona","86","Real Madrid","pre","Scheduled","VS",true);var feed=new List<FootballMatch>{match};DateTimeOffset observed=now;
        using var bus=new ActionDispatcher(prefs,(_,_,_)=>throw new Exception("no legacy"),new ExtensionStore(Path.Combine(root,"modules")));
        using var queries=new FootballQueries(()=>prefs.FootballModule,()=>"fixture",loadSchedule:()=>new(){Updated=now,SchemaVersion=2,Matches=feed.ToList()},loadTeam:_=>new(),fetchSchedule:(_,_)=>Task.FromResult(new FootballCache{Updated=now,SchemaVersion=2,Matches=feed.ToList()}),saveSchedule:_=>{},saveTeam:(_,_)=>{});
        bus.Registry.Register(new FootballCapability(()=>prefs.FootballModule,queries,bus.ExecuteDetailedAsync,_=>Task.CompletedTask,()=>["巴塞罗那"]));
        bus.Registry.Register(new TodoCapability(todos,(work,t)=>{t.ThrowIfCancellationRequested();work();return Task.CompletedTask;},_=>{},_=>null));var todoClient=new TodoClient(todos,bus);todos.SharedClient=todoClient;
        bus.Registry.Register(new NotesCapability(()=>notes,(_,_,_)=>Task.CompletedTask));
        bus.Registry.Register(new MatchPlanningCapability(()=>prefs.FootballModule,new MatchPlanningHost(()=>feed.Select(m=>(m,observed))),todos,new ModuleWindowService(ui)));
        var catalog=new BrainCatalog(bus.Registry);BrainSession New()=>new(catalog,bus.ExecuteDetailedAsync,(_,_)=>Task.FromResult(WorkflowVerification.Unknown),()=>new Dictionary<string,string>(),()=>now);
        async Task<string> Plan(BrainSession brain){await queries.Schedule(true,default);brain.BeginTurn("查询下一场巴萨比赛并加入清单");await brain.Tool(Tool("leo_action",new{action="football_facts",value="今晚比赛"}),default);await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("match_lookup",match.Id),Step("match_add","","step-1","Reference")}}),default);return brain.Pending?.Id??throw new Exception("missing plan");}
        using(var brain=New()){
            string id=await Plan(brain);c["match_plan_previews_real_match_without_writing"]=todos.Items.Count==0&&brain.Pending!.Preview[0].Contains("巴塞罗那");
            feed[0]=match with{Kickoff=match.Kickoff.AddHours(1)};observed=now.AddMinutes(1);bool denied=await Rejected(()=>brain.Confirm(id,(_,_)=>{},default));c["changed_match_time_requires_new_preview"]=denied&&todos.Items.Count==0;
            string refreshed=await Plan(brain);await brain.Confirm(refreshed,(_,_)=>{},default);c["refreshed_preview_accepts_new_actual_kickoff"]=todos.Items.Single().MatchKickoff==feed[0].Kickoff;
            // Keep later assertions independent of the initial reproduction failure.
            foreach(var item in todos.Items)todoClient.Delete(item.Id);feed[0]=match;observed=now;
        }
        foreach(var change in new[]{"status","opponent","missing"})using(var brain=New()){
            string id=await Plan(brain);if(change=="status")feed[0]=match with{Status="Postponed"};else if(change=="opponent")feed[0]=match with{AwayId="different"};else feed.Clear();
            c["changed_match_"+change+"_blocks_old_preview"]=await Rejected(()=>brain.Confirm(id,(_,_)=>{},default))&&todos.Items.Count==0;feed.Clear();feed.Add(match);
        }
        using(var brain=New()){string id=await Plan(brain);prefs.FootballModule=false;c["module_disable_after_preview_blocks_write"]=await Rejected(()=>brain.Confirm(id,(_,_)=>{},default))&&todos.Items.Count==0;prefs.FootballModule=true;}
        using(var brain=New()){string id=await Plan(brain);using var stop=new CancellationTokenSource();stop.Cancel();c["precancelled_match_confirmation_does_not_add"]=await Rejected(()=>brain.Confirm(id,(_,_)=>{},stop.Token))&&todos.Items.Count==0;}
        using(var brain=New()){
            string id=await Plan(brain);AtomicJson.BeforeReplace=file=>{if(file==tasksFile)throw new IOException("fixture save failure");};bool failed=await Rejected(()=>brain.Confirm(id,(_,_)=>{},default));AtomicJson.BeforeReplace=null;
            c["failed_match_write_does_not_report_success"]=failed&&todos.Items.Count==0&&brain.Pending?.Id==id;
            c["unknown_failed_step_requires_explicit_retry"]=await Rejected(()=>brain.Confirm(id,(_,_)=>{},default))&&todos.Items.Count==0;
            var result=await brain.Confirm(id,(_,_)=>{},default,true);c["explicit_retry_rereads_reference_and_commits_once"]=result.Steps.All(s=>s.State=="succeeded")&&todos.Items.Single().MatchId=="espn:"+match.Id;
            c["confirmed_match_uses_real_kickoff_and_reminder"]=todos.Items.Single().Reminder==match.Kickoff.AddMinutes(-10)&&todos.Items.Single().MatchSourceUpdated==observed;
            c["completed_plan_cannot_replay"]=await Rejected(()=>brain.Confirm(id,(_,_)=>{},default))&&todos.Items.Count==1;
        }
        var journal=new WorkflowJournal(null);var flow=new ReliableWorkflow(journal,bus.ExecuteDetailedAsync,(_,_)=>Task.FromResult(WorkflowVerification.NotApplied));var steps=new[]{new WorkflowStep("lookup",new("match_lookup",match.Id,"saved-chain"),"查比赛"),new WorkflowStep("add",new("match_add","","saved-chain"),"加清单",FromStep:"lookup",ResultProperty:"Reference")};
        var saved=await flow.Run("fixture-saved-match",steps,(_,_)=>{});c["saved_and_brain_flows_share_match_identity"]=saved.Steps.All(s=>s.State=="succeeded")&&todos.Items.Count==1&&bus.RecentResults.Any(r=>r.Source=="saved-chain"&&r.ActionId=="match_add");
        using(var brain=New()){brain.BeginTurn("安排比赛");c["invented_match_reference_is_rejected"]=(await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("match_add","invented")}}),default)).Contains("error")&&brain.Pending==null;}
        var noteClient=new NotesClient(notes,bus);var note=noteClient.Save(null,"fixture note conversion");int conversions=0;bool cancel=false,failLink=true;
        var pending=new Dictionary<string,string>();using(var form=new NotesForm(new NotesClient(notes,bus),(n,date)=>{conversions++;if(cancel)throw new OperationCanceledException();return todoClient.From("note-conversion").Add(n.Text,date,null,n.Id).Id;},pending)){
            form.Show();form.Location=new(-19000,-19000);var list=Walk(form).OfType<ListBox>().Single();list.SelectedIndex=0;var turn=Walk(form).OfType<Button>().Single(b=>b.Text=="转为待办");
            cancel=true;turn.PerformClick();c["note_conversion_cancel_preserves_original_and_tasks"]=notes.Items.Single().TaskId==null&&todos.Items.Count==1;cancel=false;
            AtomicJson.BeforeReplace=file=>{if(file==notesFile&&failLink&&todos.Items.Count==2)throw new IOException("fixture note link failure");};turn.PerformClick();AtomicJson.BeforeReplace=null;
            c["note_link_failure_keeps_partial_state_without_false_completion"]=todos.Items.Count==2&&notes.Items.Single().TaskId==null&&pending.ContainsKey(note.Id)&&turn.Enabled;
            int before=conversions;failLink=false;turn.PerformClick();c["note_retry_reuses_committed_task"]=conversions==before&&todos.Items.Count==2&&notes.Items.Single().TaskId!=null&&pending.Count==0;
            c["note_ui_disables_completed_conversion"]=!turn.Enabled;form.Close();
        }
        var linked=notes.Items.Single();var replay=todoClient.From("note-conversion").Add(note.Text,day,null,note.Id);c["note_conversion_identity_survives_new_store"]=new TodoStore(tasksFile).Items.Single(t=>t.Id==replay.Id).Id==linked.TaskId&&todos.Items.Count==2;
        using(var brain=New()){
            brain.BeginTurn("读取便签并转为待办");var answer=await brain.Tool(Tool("leo_action",new{action="note_query",value="{\"scope\":\"all\"}"}),default);c["explicit_brain_note_read_uses_real_note"]=answer.Contains(note.Id);
            c["brain_has_no_note_link_or_origin_write_permission"]=!catalog.Read().Any(e=>e.Action.Id=="note_link")&&(await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("todo_add",JsonSerializer.Serialize(new{title="forged",day,origin=note.Id}))}}),default)).Contains("error")&&brain.Pending==null;
        }
        c["chain_receipts_do_not_retain_private_results"]=bus.RecentResults.All(r=>r.Data==null&&!r.Message.Contains(note.Text));
    }
}
