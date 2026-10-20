using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace LeoLauncher;

sealed record BrainAction(string Id,string Label,string InputKind,bool ReadOnly=false,string Hint="",string? ResultProperty=null,string? ResultKind=null,Action<string>? Check=null,Func<string,string>? Preview=null,int TimeoutMs=30000);
interface IBrainContributor
{
    IReadOnlyList<BrainAction> BrainActions{get;}
    bool BrainAvailable=>true;
    string BrainStamp=>"";
}
sealed record BrainEntry(BrainAction Action,string Module,string Stamp);
sealed class BrainCatalog(CapabilityRegistry registry)
{
    public BrainEntry[] Read()
    {
        var result=registry.Read().Where(m=>m.Enabled&&m is IBrainContributor{BrainAvailable:true}).SelectMany(m=>((IBrainContributor)m).BrainActions.Select(a=>new BrainEntry(a,m.Id,((IBrainContributor)m).BrainStamp))).Take(64).ToList();
        var facts=registry.Resolve("football_facts");if(facts.Enabled&&!result.Any(e=>e.Action.Id=="football_facts"))result.Add(new(new("football_facts","查询足球赛程","text",true,"球队名、今晚比赛；只返回实际数据"),facts.Id,""));return result.ToArray();
    }
    public BrainEntry Require(string action)=>Read().SingleOrDefault(x=>x.Action.Id==action)??throw new ArgumentException("能力已停用、依赖不可用或尚未接入模型，请改用本地入口");
    public static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
sealed record BrainPlan(string Id,DateTimeOffset Expires,WorkflowStep[] Steps,Dictionary<string,string> Stamps,string[] Preview);
sealed class BrainSession(BrainCatalog catalog,Func<ActionRequest,CancellationToken,Task<ActionResult>> execute,Func<ActionRequest,CancellationToken,Task<WorkflowVerification>> verify,Func<IReadOnlyDictionary<string,string>> combos,Func<DateTimeOffset>? clock=null,Func<string,string>? comboPreview=null):IDisposable
{
    static readonly JsonSerializerOptions ToolJson=new(){Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    public string Origin="chat";
    readonly Dictionary<string,string> matchLabels=new();
    readonly Dictionary<string,(string Home,string Away,DateTimeOffset Kickoff)> matchTargets=new();string userPrompt="";
    readonly HashSet<string> matches=new(StringComparer.Ordinal);readonly Dictionary<string,string> knownCombos=new();
    readonly ReliableWorkflow workflow=new(new WorkflowJournal(null),execute,verify);
    readonly Dictionary<string,string> mediaTargets=new();readonly Dictionary<string,(string Name,string Artist)> musicTargets=new();
    readonly Dictionary<string,DailyTask> todoTargets=new();bool todosRequested;
    bool disposed,notesRequested;int calls;DateTimeOffset turnStarted;public BrainPlan? Pending{get;private set;}
    DateTimeOffset Now=>(clock??(()=>DateTimeOffset.UtcNow))();
    public void BeginTurn(string prompt=""){userPrompt=prompt;todosRequested=System.Text.RegularExpressions.Regex.IsMatch(prompt,"清单|待办|事项|我的.{0,8}(计划|任务)|(今天|明天|本周).{0,8}(计划|任务)")&&!System.Text.RegularExpressions.Regex.IsMatch(prompt,"(不要|不用|不许|禁止|无需|别).{0,12}(读取|查看|查询|清单|待办)|(清单|待办).{0,8}(不要|不用|别)");notesRequested=System.Text.RegularExpressions.Regex.IsMatch(prompt,"便签|笔记|随手记|记事|记录的|我的想法")&&!System.Text.RegularExpressions.Regex.IsMatch(prompt,"(不要|不需要|不用|别|不许|禁止|无需).{0,12}(便签|笔记|随手记|记录)|(便签|笔记).{0,8}(不要|不需要|不用|别|无需)");if(disposed)throw new ObjectDisposedException(nameof(BrainSession));Pending=null;todoTargets.Clear();musicTargets.Clear();mediaTargets.Clear();matches.Clear();matchLabels.Clear();matchTargets.Clear();knownCombos.Clear();calls=0;turnStarted=Now;}
    public void Clear(){userPrompt="";notesRequested=todosRequested=false;turnStarted=DateTimeOffset.MinValue;Pending=null;todoTargets.Clear();musicTargets.Clear();mediaTargets.Clear();matches.Clear();matchLabels.Clear();matchTargets.Clear();knownCombos.Clear();}
    public void Dispose(){disposed=true;Clear();}
    public object[] Definitions(bool responses)
    {
        var entries=catalog.Read();
        object Definition(string name,string description,object parameters)=>responses?new{type="function",name,description,parameters,strict=true}:new{type="function",function=new{name,description,parameters,strict=true}};
        var actionNames=entries.Select(x=>x.Action.Id).ToArray();
        var step=new{type="object",properties=new{action=new{type="string",@enum=actionNames},value=new{type="string"},from_step=new{type="string",description="空字符串或之前步骤的step-1等编号"},result_property=new{type="string",description="无引用时为空；比赛查询可引用Reference"}},required=new[]{"action","value","from_step","result_property"},additionalProperties=false};
        return [Definition("leo_catalog","查询可用能力与已保存组合；不执行操作。",new{type="object",properties=new{query=new{type="string"}},required=new[]{"query"},additionalProperties=false}),
            Definition("leo_action","仅立即执行只读查询。写操作必须通过leo_plan等待用户确认。",new{type="object",properties=new{action=new{type="string",@enum=entries.Where(x=>x.Action.ReadOnly).Select(x=>x.Action.Id).ToArray()},value=new{type="string"}},required=new[]{"action","value"},additionalProperties=false}),
            Definition("leo_plan","整理最多4步的待确认计划，绝不会直接执行；步骤按序编号step-1等。",new{type="object",properties=new{steps=new{type="array",items=step,minItems=1,maxItems=4}},required=new[]{"steps"},additionalProperties=false})];
    }
    static void Fields(JsonElement value,params string[] names){if(value.ValueKind!=JsonValueKind.Object||value.EnumerateObject().Count()!=names.Length||value.EnumerateObject().Select(x=>x.Name).Distinct().Count()!=names.Length||names.Any(n=>!value.TryGetProperty(n,out _)))throw new ArgumentException("工具参数缺失、重复或包含多余字段");}
    static string Text(JsonElement value,string name){var p=value.GetProperty(name);return p.ValueKind==JsonValueKind.String?p.GetString()!:throw new ArgumentException("参数必须是文字");}
    void Check(BrainAction action,string value)
    {
        if(action.InputKind=="notes-query"&&!notesRequested)throw new ArgumentException("读取私人便签需要用户本轮明确提出，请先询问用户");
        if(action.InputKind=="todos-query"&&!todosRequested)throw new ArgumentException("读取清单需要用户本轮明确要求，请先询问用户");
        if(action.InputKind is "todo-id" or "todo-target"){
            string id=value;if(action.InputKind=="todo-target"){using var d=JsonDocument.Parse(value);id=d.RootElement.GetProperty("id").GetString()??"";}
            if(!todoTargets.TryGetValue(id,out var target))throw new ArgumentException("请先查询清单，使用本轮实际返回的事项编号");
            var candidates=todoTargets.Values.Where(t=>userPrompt.Contains(t.Title,StringComparison.OrdinalIgnoreCase)).ToArray();
            if(todoTargets.Count>1&&!userPrompt.Contains(id,StringComparison.Ordinal)&&(candidates.Length!=1||candidates[0].Id!=id))throw new ArgumentException("有多个事项，请先询问具体标题或日期");
            if(action.Id=="todo_skip_repeat"&&target.SeriesId==null)throw new ArgumentException("该事项不是重复事项");
        }
        if(value.Length>4000)throw new ArgumentException("动作参数过长");action.Check?.Invoke(value);
        if(action.InputKind.StartsWith("match-")&&!matches.Contains(value))throw new ArgumentException("请先查询足球赛程，再使用实际返回的比赛编号");
        if(action.InputKind.StartsWith("match-")&&matches.Count>1&&!userPrompt.Contains(value,StringComparison.Ordinal)){
            var team=ClubCatalog.Find(QuickRouter.Team(userPrompt));var candidates=team==null?[]:matchTargets.Where(x=>x.Value.Home==team.Id||x.Value.Away==team.Id).OrderBy(x=>x.Value.Kickoff).Select(x=>x.Key).ToArray();
            bool next=System.Text.RegularExpressions.Regex.IsMatch(userPrompt,"下一场|最近一场");
            if(next?(candidates.Length==0||candidates[0]!=value):(candidates.Length!=1||candidates[0]!=value))throw new ArgumentException("找到多场比赛，请先询问用户具体球队及场次，不能任意选择");
        }
        if(action.InputKind=="music-song"){
            if(!musicTargets.ContainsKey(value))throw new ArgumentException("请先搜索歌曲，再使用实际返回的编号");
            if(musicTargets.Count>1&&!userPrompt.Contains(value,StringComparison.Ordinal)){
                var names=musicTargets.Where(x=>userPrompt.Contains(x.Value.Name,StringComparison.OrdinalIgnoreCase)).ToArray();var artists=names.Where(x=>x.Value.Artist.Split(" / ").Any(a=>a.Length>0&&userPrompt.Contains(a,StringComparison.OrdinalIgnoreCase))).ToArray();var matches=artists.Length>0?artists:names;
                if(matches.Length!=1||matches[0].Key!=value)throw new ArgumentException("找到多首候选，请先询问用户明确歌名或歌手");
            }
        }
        if(action.InputKind=="media-session"){if(!mediaTargets.ContainsKey(value))throw new ArgumentException("请先查询播放器，再使用实际返回的身份");if(mediaTargets.Count>1&&!userPrompt.Contains(value,StringComparison.Ordinal)){var candidates=mediaTargets.Where(x=>userPrompt.Contains(x.Value,StringComparison.OrdinalIgnoreCase)).Select(x=>x.Key).ToArray();if(candidates.Length!=1||candidates[0]!=value)throw new ArgumentException("有多个播放器，请先询问用户选择哪个");}}
        if(action.InputKind=="combo-name"&&!knownCombos.ContainsKey(value))throw new ArgumentException("请先查询组合清单，再选用实际返回的名称");
    }
    public async Task<string> Tool(ToolCall call,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(disposed||Now-turnStarted>TimeSpan.FromMinutes(5))throw new InvalidOperationException("本轮上下文已失效，请重新发起请求");
        if(++calls>8)throw new InvalidOperationException("本轮工具调用已达上限");if(Pending!=null)throw new InvalidOperationException("已有计划等待用户确认，不能继续追加动作");
        try{
            if(call.Arguments.Length>20000)throw new ArgumentException("工具请求过长");using var doc=JsonDocument.Parse(call.Arguments);var p=doc.RootElement;
            if(call.Name=="leo_catalog"){
                Fields(p,"query");var query=Text(p,"query").Trim();if(query.Length>100)throw new ArgumentException("查询词过长");
                knownCombos.Clear();foreach(var pair in combos().Take(24))knownCombos[pair.Key]=pair.Value;
                return JsonSerializer.Serialize(new{actions=catalog.Read().Where(x=>query.Length==0||x.Action.Label.Contains(query,StringComparison.OrdinalIgnoreCase)||x.Action.Id.Contains(query,StringComparison.OrdinalIgnoreCase)).Select(x=>new{id=x.Action.Id,name=x.Action.Label,x.Action.ReadOnly,x.Action.InputKind,x.Action.Hint}),combos=knownCombos.Keys},ToolJson);
            }
            if(call.Name=="leo_plan"){
                Fields(p,"steps");var array=p.GetProperty("steps");if(array.ValueKind!=JsonValueKind.Array||array.GetArrayLength() is <1 or >4)throw new ArgumentException("计划必须为1～4步");
                var steps=new List<WorkflowStep>();var stamps=new Dictionary<string,string>();var preview=new List<string>();
                foreach(var item in array.EnumerateArray()){
                    Fields(item,"action","value","from_step","result_property");string id=Text(item,"action"),value=Text(item,"value"),from=Text(item,"from_step"),property=Text(item,"result_property");var entry=catalog.Require(id);var action=entry.Action;
                    if(from.Length>0){var previous=steps.SingleOrDefault(s=>s.Id==from)??throw new ArgumentException("只能引用前面步骤的结果");var source=catalog.Require(previous.Request.Action).Action;if(value.Length>0||source.ResultProperty!=property||source.ResultKind!=action.InputKind)throw new ArgumentException("结果引用与目标参数不匹配");}
                    else{if(property.Length>0)throw new ArgumentException("没有来源步骤，不能填写结果字段");Check(action,value);}
                    if(action.InputKind=="combo-name"){if(array.GetArrayLength()!=1)throw new ArgumentException("已保存组合只能作为单独计划执行，不能嵌套叠加");stamps["combo:"+value]=knownCombos[value];}
                    if(catalog.Read().Count(e=>e.Action.Label==action.Label)>1)throw new ArgumentException("存在同名目标，请先在本地选择或区分名称");
                    stamps[id]=entry.Stamp;steps.Add(new("step-"+(steps.Count+1),new(id,value,"brain-plan:"+Origin),action.Label,TimeoutMs:action.TimeoutMs,FromStep:from.Length>0?from:null,ResultProperty:property.Length>0?property:null));
                    preview.Add(action.InputKind=="combo-name"&&comboPreview!=null?comboPreview(value):action.Preview!=null?action.Preview(value):action.Label+(from.Length>0?"：使用第 "+from.Replace("step-","")+" 步的实际结果":value.Length>0?"："+matchLabels.GetValueOrDefault(value,value):""));
                }
                Pending=new(Guid.NewGuid().ToString("N"),Now.AddMinutes(5),steps.ToArray(),stamps,preview.ToArray());return JsonSerializer.Serialize(new{status="awaiting_user_confirmation",plan_id=Pending.Id,note="尚未执行任何计划步骤"});
            }
            if(call.Name!="leo_action")throw new ArgumentException("模型请求了未提供的工具");Fields(p,"action","value");var requested=catalog.Require(Text(p,"action")).Action;string argument=Text(p,"value");if(!requested.ReadOnly)throw new ArgumentException("写操作必须生成计划，并由用户确认后执行");Check(requested,argument);
            var result=await execute(new(requested.Id,argument,"brain-query:"+Origin),token);result.ToLegacy(token);
            string raw=result.Data?.GetRawText()??result.Message;var node=JsonNode.Parse(raw);Shape(node,0);string answer=node?.ToJsonString(ToolJson)??"null";
            if(answer.Length>10000)throw new InvalidOperationException("查询结果过多，请缩小范围");if(requested.Id=="todo_list"){todoTargets.Clear();if(node is JsonArray tasks)foreach(var row in tasks)if(row!=null){var task=row.Deserialize<DailyTask>();if(task!=null)todoTargets[task.Id]=task;}}if(requested.Id=="football_facts")CollectMatches(node);if(requested.Id=="media_status"){mediaTargets.Clear();if(node is JsonArray rows)foreach(var row in rows)if(row is JsonObject obj&&obj["Id"]?.ToString() is string id&&obj["Provider"]?.ToString() is string provider)mediaTargets[id]=provider;}if(requested.Id=="music_search"){musicTargets.Clear();if(node is JsonArray songRows)foreach(var song in songRows)if(song is JsonObject obj&&obj["Id"]?.ToString() is string id&&obj["Name"]?.ToString() is string name)musicTargets[id]=(name,obj["Artist"]?.ToString()??"");}return answer;
        }catch(Exception e)when(e is ArgumentException or JsonException or InvalidOperationException or KeyNotFoundException){return JsonSerializer.Serialize(new{error=e.Message,note="没有执行本地写操作；请补充参数或重新选择目标"});}
    }
    void CollectMatches(JsonNode? node)
    {
        if(node is JsonObject obj){
            if(obj.ContainsKey("Kickoff")&&obj.ContainsKey("Home")&&obj["Id"] is JsonValue value&&value.TryGetValue<string>(out var id)&&matches.Count<64&&DateTimeOffset.TryParse(obj["Kickoff"]?.ToString(),out var kickoff)&&kickoff>Now&&obj["State"]?.ToString()=="pre"&&obj["TimeKnown"]?.ToString().Equals("true",StringComparison.OrdinalIgnoreCase)==true){
                string status=obj["Status"]?.ToString()??"";if(!status.Contains("postpon",StringComparison.OrdinalIgnoreCase)&&!status.Contains("cancel",StringComparison.OrdinalIgnoreCase)){
                    matches.Add(id);matchLabels[id]=FootballFeed.Name(obj["Home"]?.ToString()??"")+" 对 "+FootballFeed.Name(obj["Away"]?.ToString()??"")+" · "+kickoff.ToLocalTime().ToString("MM-dd HH:mm");matchTargets[id]=(obj["HomeId"]?.ToString()??"",obj["AwayId"]?.ToString()??"",kickoff);
                }
            }foreach(var pair in obj)CollectMatches(pair.Value);
        }else if(node is JsonArray array)foreach(var child in array)CollectMatches(child);
    }
    static void Shape(JsonNode? node,int depth){if(node is JsonArray array){while(array.Count>8)array.RemoveAt(array.Count-1);foreach(var child in array)Shape(child,depth+1);}else if(node is JsonObject obj){foreach(var key in obj.Select(p=>p.Key).ToArray()){if(depth>=8){obj.Remove(key);continue;}if(obj[key] is JsonValue value&&value.TryGetValue<string>(out var text)&&text.Length>700)obj[key]=text[..700]+"…";else Shape(obj[key],depth+1);}}}
    public async Task<WorkflowRecord> Confirm(string planId,Action<int,WorkflowStep> progress,CancellationToken token,bool confirmUnknown=false)
    {
        var plan=Pending;if(disposed||plan==null||plan.Id!=planId||plan.Expires<Now)throw new InvalidOperationException("计划已失效，请重新生成预览");
        foreach(var step in plan.Steps){var current=catalog.Require(step.Request.Action);if(plan.Stamps[step.Request.Action]!=current.Stamp)throw new InvalidOperationException("目标配置已改变，请重新生成预览");}
        var latestCombos=combos();foreach(var pair in plan.Stamps.Where(x=>x.Key.StartsWith("combo:")))if(!latestCombos.TryGetValue(pair.Key[6..],out var stamp)||stamp!=pair.Value)throw new InvalidOperationException("组合配置已改变，请重新生成预览");
        var result=await workflow.Run("brain-"+plan.Id,plan.Steps,progress,token,confirmUnknown);Pending=null;return result;
    }
}
