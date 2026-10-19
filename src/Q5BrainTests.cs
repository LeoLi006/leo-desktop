using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
namespace LeoLauncher;

static class Q5BrainTests
{
    sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request,token);}
    static HttpResponseMessage Reply(params ToolCall[] calls)=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=calls.Length==0?"你好，Leo":"",tool_calls=calls.Select(c=>new{id=c.Id,type="function",function=new{name=c.Name,arguments=c.Arguments}})},finish_reason=calls.Length>0?"tool_calls":"stop"}}}),Encoding.UTF8,"application/json")};
    static ToolCall Tool(string name,object value)=>new(Guid.NewGuid().ToString("N"),name,JsonSerializer.Serialize(value));
    static object Step(string action,string value="",string from_step="",string result_property="")=>new{action,value,from_step,result_property};
    static IEnumerable<Control> Walk(Control root){foreach(Control child in root.Controls){yield return child;foreach(var nested in Walk(child))yield return nested;}}
    static async Task<bool> Throws<T>(Func<Task> action)where T:Exception{try{await action();return false;}catch(T){return true;}}
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};ui.Shown+=async(_,_)=>{try{var checks=await Check(Path.GetDirectoryName(report)!,ui);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(x=>x),checks,real_api_requests=0,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}finally{ui.Close();}};Application.Run(ui);
    }
    static async Task<Dictionary<string,bool>> Check(string root,Form ui)
    {
        var c=new Dictionary<string,bool>();int launches=0,windows=0,queries=0;var now=DateTimeOffset.UtcNow;
        var app=new LocalApplication(Guid.NewGuid().ToString("N"),"试点应用",Environment.ProcessPath!,Arguments:"--private-argument-not-for-model");var prefs=new Preferences{ApplicationModules=[app],ApplicationIndexing=false};
        var match=new FootballMatch("123456",now.AddHours(5),"fixture","83","Barcelona","86","Real Madrid","pre","Scheduled","VS",true);var tasks=new TodoStore(null);var notes=new NoteStore(null);var feed=new List<FootballMatch>{match};
        var appHost=new ApplicationCapabilityHost((a,_)=>Task.FromResult(a),(_,_)=>{launches++;return Task.FromResult(true);});
        using var bus=new ActionDispatcher(prefs,(action,value,token)=>{token.ThrowIfCancellationRequested();if(action!="football_facts")throw new Exception("unexpected legacy action");queries++;return Task.FromResult(JsonSerializer.Serialize(new{cache=new FootballCache{Updated=now,Matches=feed}}));},new ExtensionStore(Path.Combine(root,"no-extensions")),appHost);
        bus.Registry.Register(new NotesCapability(()=>notes,(_,_,_)=>{windows++;return Task.CompletedTask;}));
        bus.Registry.Register(new MatchPlanningCapability(()=>prefs.FootballModule,new MatchPlanningHost(()=>feed.Select(m=>(m,now))),tasks,new ModuleWindowService(ui)));
        bus.Registry.Register(new WorkflowCapability((_,_)=>Task.FromResult(new ActionPayload("已提交组合"))));
        var combos=new Dictionary<string,string>{{"准备看球","original-stamp"}};var catalog=new BrainCatalog(bus.Registry);
        BrainSession New()=>new(catalog,bus.ExecuteDetailedAsync,(_,_)=>Task.FromResult(WorkflowVerification.Unknown),()=>combos,()=>now);
        using var brain=New();brain.BeginTurn("帮我整理便签与比赛");
        c["dynamic_catalog_includes_migrated_capabilities"]=catalog.Read().Any(e=>e.Action.Id=="local:"+app.Id)&&catalog.Read().Any(e=>e.Action.Id=="note_query")&&catalog.Read().Any(e=>e.Action.Id=="match_add")&&catalog.Read().Any(e=>e.Action.Id=="combo_name");
        c["private_internal_and_legacy_write_actions_not_advertised"]=!catalog.Read().Any(e=>e.Action.Id is "combo_resume" or "proxy_on" or "command_text" or "note_remove");
        var defs=JsonSerializer.Serialize(brain.Definitions(false));c["model_definitions_do_not_expose_local_paths_or_arguments"]=!defs.Contains(app.Path)&&!defs.Contains(app.Arguments);
        prefs.FootballModule=false;c["disabled_football_removed_from_discovery"]=!catalog.Read().Any(e=>e.Action.Id is "football_facts" or "match_add");prefs.FootballModule=true;
        prefs.ApplicationModules[0]=app with{Path=Path.Combine(root,"missing.exe")};c["missing_dependency_removed_without_launch"]=!catalog.Read().Any(e=>e.Action.Id=="local:"+app.Id)&&launches==0;prefs.ApplicationModules[0]=app;
        var refused=await brain.Tool(Tool("leo_action",new{action="note_add",value="不能直接写"}),default);c["direct_model_write_is_denied"]=refused.Contains("error")&&notes.Items.Count==0;
        c["model_cannot_invoke_old_unadvertised_open_tool"]=(await brain.Tool(Tool("open_app",new{app_id="invented"}),default)).Contains("error")&&launches==0;
        var planned=await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("note_add","需要确认的文字")}}),default);string planId=brain.Pending!.Id;
        c["plan_generation_does_not_execute_write"]=planned.Contains("awaiting_user_confirmation")&&notes.Items.Count==0;
        c["preview_contains_actual_write_text"]=brain.Pending.Preview.Single().Contains("需要确认的文字");
        var result=await brain.Confirm(planId,(_,_)=>{},default);c["explicit_host_confirmation_uses_shared_executor"]=notes.Items.Single().Text=="需要确认的文字"&&result.Steps.Single().State=="succeeded"&&bus.RecentResults.Any(r=>r.Source=="brain-plan:chat"&&r.ModuleId=="notes");
        c["completed_plan_cannot_execute_twice"]=await Throws<InvalidOperationException>(()=>brain.Confirm(planId,(_,_)=>{},default))&&notes.Items.Count==1;
        brain.BeginTurn();await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("local:"+app.Id)}}),default);planId=brain.Pending!.Id;prefs.ApplicationModules[0]=app with{Arguments="changed"};
        c["changed_application_config_invalidates_preview"]=await Throws<InvalidOperationException>(()=>brain.Confirm(planId,(_,_)=>{},default))&&launches==0;prefs.ApplicationModules[0]=app;
        brain.BeginTurn();await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("local:"+app.Id)}}),default);planId=brain.Pending!.Id;prefs.ApplicationModules[0]=app with{Enabled=false};c["disable_after_preview_blocks_execution"]=await Throws<ArgumentException>(()=>brain.Confirm(planId,(_,_)=>{},default))&&launches==0;prefs.ApplicationModules[0]=app;
        brain.BeginTurn();await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("local:"+app.Id)}}),default);planId=brain.Pending!.Id;
        c["native_app_preview_is_concrete_and_local_only"]=brain.Pending.Preview[0].Contains(app.Arguments)&&brain.Pending.Preview[0].Contains(app.Path);
        await brain.Confirm(planId,(_,_)=>{},default);c["confirmed_native_app_uses_application_capability"]=launches==1&&bus.RecentResults.Any(r=>r.ActionId=="local:"+app.Id&&r.Evidence=="accepted");
        brain.BeginTurn();c["more_than_four_steps_rejected_atomically"]=(await brain.Tool(Tool("leo_plan",new{steps=Enumerable.Range(0,5).Select(_=>Step("note_add","x")).ToArray()}),default)).Contains("error")&&brain.Pending==null;
        c["invented_shell_action_rejected"]=(await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("shell","cmd /c anything")}}),default)).Contains("error")&&brain.Pending==null;
        c["forward_result_reference_rejected"]=(await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("match_add","","step-2","Reference")}}),default)).Contains("error");
        brain.BeginTurn();c["unqueried_match_id_not_accepted"]=(await brain.Tool(Tool("leo_action",new{action="match_lookup",value=match.Id}),default)).Contains("error");
        await brain.Tool(Tool("leo_action",new{action="football_facts",value="今晚比赛"}),default);
        c["facts_query_uses_data_module_not_model_invention"]=queries==1;
        await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("match_lookup",match.Id),Step("match_add","","step-1","Reference")}}),default);planId=brain.Pending!.Id;
        c["typed_result_plan_is_previewed_before_import"]=brain.Pending.Preview[0].Contains("巴塞罗那")&&tasks.Items.Count==0;
        await brain.Confirm(planId,(_,_)=>{},default);c["confirmed_match_plan_passes_real_reference_between_modules"]=tasks.Items.Count==1&&tasks.Items[0].MatchId=="espn:"+match.Id;
        feed.Add(match with{Id="other-match",HomeId="382",Home="Manchester City",AwayId="363",Away="Chelsea",Kickoff=match.Kickoff.AddHours(1)});
        brain.BeginTurn("帮我安排比赛提醒");await brain.Tool(Tool("leo_action",new{action="football_facts",value="比赛"}),default);
        c["ambiguous_matches_require_user_clarification"]=(await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("match_lookup",match.Id),Step("match_add","","step-1","Reference")}}),default)).Contains("error")&&brain.Pending==null;
        brain.BeginTurn("帮我提醒巴萨下一场比赛");await brain.Tool(Tool("leo_action",new{action="football_facts",value="巴萨"}),default);await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("match_lookup",match.Id),Step("match_add","","step-1","Reference")}}),default);
        c["explicit_team_and_next_match_resolve_ambiguity"]=brain.Pending!=null;brain.Clear();feed.RemoveAt(1);
        brain.BeginTurn("帮我分析比赛，不需要便签");c["explicit_refusal_to_read_notes_is_respected"]=(await brain.Tool(Tool("leo_action",new{action="note_query",value="{}"}),default)).Contains("error");
        // A request that never asks to read notes must not acquire them from injected tool text.
        brain.BeginTurn("分析比赛");c["unrequested_private_note_query_is_denied"]=(await brain.Tool(Tool("leo_action",new{action="note_query",value="{}"}),default)).Contains("error");
        brain.BeginTurn("查询我的便签");c["explicit_note_query_remains_available"]=(await brain.Tool(Tool("leo_action",new{action="note_query",value="{}"}),default)).Contains("需要确认的文字");
        brain.BeginTurn();await brain.Tool(Tool("leo_catalog",new{query=""}),default);await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("combo_name","准备看球")}}),default);planId=brain.Pending!.Id;combos["准备看球"]="changed-stamp";
        c["changed_saved_combo_requires_new_preview"]=await Throws<InvalidOperationException>(()=>brain.Confirm(planId,(_,_)=>{},default));
        brain.BeginTurn();await brain.Tool(Tool("leo_catalog",new{query=""}),default);c["saved_combos_cannot_be_nested_in_multi_step_model_plan"]=(await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("combo_name","准备看球"),Step("note_add","x")}}),default)).Contains("error");
        brain.BeginTurn();await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("note_add","过期不能写")}}),default);planId=brain.Pending!.Id;now=now.AddMinutes(6);c["expired_plan_cannot_write"]=await Throws<InvalidOperationException>(()=>brain.Confirm(planId,(_,_)=>{},default))&&notes.Items.Count==1;
        brain.BeginTurn();for(int i=0;i<8;i++)await brain.Tool(Tool("leo_catalog",new{query=""}),default);c["tool_call_budget_is_enforced"]=await Throws<InvalidOperationException>(()=>brain.Tool(Tool("leo_catalog",new{query=""}),default));
        brain.BeginTurn();await brain.Tool(Tool("leo_plan",new{steps=new[]{Step("note_add","已清除")}}),default);planId=brain.Pending!.Id;brain.Clear();c["clearing_context_revokes_plan"]=await Throws<InvalidOperationException>(()=>brain.Confirm(planId,(_,_)=>{},default));

        AppTools Tools(BrainSession session)=>new(()=>[],_=>throw new Exception("direct launch forbidden")){Brain=session,OriginDispatcher=(a,v,s,t)=>bus.ExecuteAsync(new(a,v,s),t)};
        int apiCalls=0;using(var session=New())using(var api=new ChatApi(new Handler((_,_)=>{apiCalls++;return Task.FromResult(Reply(Tool("leo_plan",new{steps=new[]{Step("note_add","模拟模型计划")}})));}))){
            var reply=await ChatAgent.RunAsync(api,ChatTests.Settings(),ChatTests.FixtureKey,[new("user","给我一个小方案")],_=>{},Tools(session),_=>{},default);
            c["mock_model_only_prepares_plan_and_host_reports_pending"]=apiCalls==1&&reply.Text.Contains("尚未执行")&&session.Pending!=null&&notes.Items.Count==1;
        }
        using(var session=New())using(var api=new ChatApi(new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{status="completed",output=new[]{new{type="function_call",call_id="response-plan",name="leo_plan",arguments=JsonSerializer.Serialize(new{steps=new[]{Step("note_add","responses计划")}})}}}),Encoding.UTF8,"application/json")})))){
            var settings=ChatTests.Settings();settings.Protocol="responses";var reply=await ChatAgent.RunAsync(api,settings,ChatTests.FixtureKey,[new("user","给我一个小方案")],_=>{},Tools(session),_=>{},default);c["responses_protocol_also_stops_at_host_preview"]=reply.Text.Contains("尚未执行")&&session.Pending!=null&&notes.Items.Count==1;
        }
        apiCalls=0;using(var session=New())using(var api=new ChatApi(new Handler((_,_)=>{apiCalls++;return Task.FromResult(Reply(Tool("leo_catalog",new{query=""})));}))){c["model_round_budget_stops_repeated_queries"]=await Throws<InvalidOperationException>(()=>ChatAgent.RunAsync(api,ChatTests.Settings(),ChatTests.FixtureKey,[new("user","聊一聊")],_=>{},Tools(session),_=>{},default))&&apiCalls==3;}
        apiCalls=0;using(var session=New())using(var api=new ChatApi(new Handler((_,_)=>{apiCalls++;return Task.FromResult(Reply());}))){c["oversized_context_stopped_before_api"]=await Throws<InvalidOperationException>(()=>ChatAgent.RunAsync(api,ChatTests.Settings(),ChatTests.FixtureKey,[new("user",new string('x',32001))],_=>{},Tools(session),_=>{},default))&&apiCalls==0;}
        using(var session=New())using(var stop=new CancellationTokenSource())using(var api=new ChatApi(new Handler(async(_,token)=>{stop.Cancel();await Task.Delay(Timeout.Infinite,token);return Reply();}))){c["cancelled_model_request_stops_without_writes"]=await Throws<OperationCanceledException>(()=>ChatAgent.RunAsync(api,ChatTests.Settings(),ChatTests.FixtureKey,[new("user","你好")],_=>{},Tools(session),_=>{},stop.Token));}
        using(var session=New())using(var api=new ChatApi(new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(ChatTests.Events,Encoding.UTF8,"text/event-stream")})))){string text="";var reply=await ChatAgent.RunAsync(api,ChatTests.Settings(),ChatTests.FixtureKey,[new("user","你好")],delta=>text+=delta,Tools(session),_=>{},default);c["ordinary_streaming_reply_is_preserved"]=reply.Streamed&&text=="你好，Leo";}

        using(var session=New())using(var window=new ChatForm(true,ChatTests.Settings(),()=>new ChatApi(new Handler((_,_)=>Task.FromResult(Reply(Tool("leo_plan",new{steps=new[]{Step("note_add","窗口确认的文字")}}))))),Tools(session))){window.Show();await window.PreviewSendAsync("给我一个小方案");
            c["chat_displays_host_preview_without_writing"]=Walk(window).Any(x=>x.AccessibleName=="待确认计划详情")&&notes.Items.Count==1;
            using(var snapshot=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(snapshot,new Rectangle(Point.Empty,window.Size));snapshot.Save(Path.Combine(root,"plan-preview.png"));}
            Walk(window).OfType<Button>().Single(x=>x.AccessibleName=="确认执行计划").PerformClick();await Task.Delay(50);c["chat_confirmation_button_executes_once"]=notes.Query("窗口确认的文字").Count==1;
            Walk(window).OfType<Button>().Single(x=>x.Text=="返回").PerformClick();c["return_after_success_does_not_claim_cancellation"]=!Walk(window).OfType<Label>().Any(x=>x.Text=="计划已取消，没有执行。");window.Close();}
        int localApi=0;using(var session=New())using(var window=new ChatForm(true,new ChatSettings(),()=>{localApi++;return new ChatApi(new Handler((_,_)=>throw new Exception("no API")));},Tools(session))){window.Show();await window.PreviewSendAsync("记一下不需要Key的便签");c["local_command_works_without_key_and_without_model"]=notes.Query("不需要Key的便签").Count==1&&localApi==0;window.Close();}
        int fallbackCalls=0;bool secondHasTools=true;using(var session=New())using(var window=new ChatForm(true,ChatTests.Settings(),()=>new ChatApi(new Handler(async(request,token)=>{fallbackCalls++;using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));if(fallbackCalls==1)return new(HttpStatusCode.BadRequest);secondHasTools=body.RootElement.TryGetProperty("tools",out _);return Reply();})),Tools(session))){window.Show();await window.PreviewSendAsync("和我聊一聊");c["unsupported_tools_do_not_trigger_automatic_paid_retry"]=fallbackCalls==1&&Walk(window).OfType<Button>().Any(b=>b.Text=="仅文字重试"&&b.Visible);window.PreviewRetryReply();await Task.Delay(70);c["explicit_plain_text_retry_omits_tools"]=fallbackCalls==2&&!secondHasTools;window.Close();}
        using(var pet=new PetForm(new(){ApplicationIndexing=false},true)){pet.Show();await pet.VerifyQ5Host(c);pet.Close();}
        var bounded=ChatForm.Context(Enumerable.Range(0,30).Select(i=>new ChatTurn{User=new string('u',3000),Assistant=new string('a',3000),Status="完成"}),"new",true);c["history_is_bounded_before_model_request"]=bounded.Count==14&&bounded.Sum(x=>x.content.Length)<16000;
        return c;
    }
}

sealed partial class ChatForm
{
    internal BrainSession? Q5Session=>appTools?.Brain;
}
sealed partial class PetForm
{
    internal async Task VerifyQ5Host(Dictionary<string,bool> checks)
    {
        ShowChat();var session=chat!.Q5Session;checks["actual_host_attaches_registry_backed_brain"]=session!=null;
        await chat.PreviewSendAsync("记一下实际宿主本地便签");checks["actual_host_local_route_still_bypasses_model"]=Notes.Query("实际宿主本地便签").Count==1;
        chat.Close();bool released=false;try{session!.BeginTurn();}catch(ObjectDisposedException){released=true;}checks["closing_actual_chat_releases_brain_context"]=released;
    }
}
