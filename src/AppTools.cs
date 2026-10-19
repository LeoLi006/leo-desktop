using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace LeoLauncher;

sealed class AppTools
{
    readonly Func<IEnumerable<AppEntry>> catalog;
    readonly Action<AppEntry> launch;
    readonly Func<string,string,CancellationToken,Task<string>>? leo;
    public Func<string,bool>? RecognizesExtension;
    public string Source="chat";
    public BrainSession? Brain;
    public Func<string,string,string,CancellationToken,Task<string>>? OriginDispatcher;
    public bool HasLeo=>leo!=null||OriginDispatcher!=null;
    readonly Func<Dictionary<string, string[]>> aliases;
    public AppTools(Func<IEnumerable<AppEntry>> apps, Action<AppEntry> open, Func<Dictionary<string, string[]>>? names = null,Func<string,string,CancellationToken,Task<string>>? leoActions=null)
    { catalog = apps; launch = open;leo=leoActions; aliases = names ?? (() => new()); }
    public static string Id(AppEntry app) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(app.Target)))[..24];
    internal static bool Launchable(AppEntry app) => !app.Target.StartsWith("leo-tool:", StringComparison.OrdinalIgnoreCase)
        && !new[] { "卸载", "uninstall", "uninstaller" }.Any(x => app.Name.Contains(x, StringComparison.OrdinalIgnoreCase));
    public AppEntry[] FindLocal(string query)=>catalog().Where(Launchable).Select(app=>(App:app,Rank:AppSearch.Rank(app,query,aliases()))).Where(x=>x.Rank<99).OrderBy(x=>x.Rank).ThenBy(x=>x.App.Name.Length).Take(8).Select(x=>x.App).ToArray();
    public async Task<string> OpenLocalAsync(AppEntry app,CancellationToken token=default){token.ThrowIfCancellationRequested();if(!catalog().Where(Launchable).Any(x=>x.Target==app.Target))throw new InvalidOperationException("应用已不在当前索引，请重新搜索");if(HasLeo)return await ExecuteLeoAsync("app_open",Id(app),token);launch(app);return "已打开 "+app.Name;}
    public async void OpenLocal(AppEntry app){try{await OpenLocalAsync(app);}catch(Exception e){OperationFeedback.Error(e.Message);}}
    static object Definition(string name, string description, string argument, string explanation, bool responses)
    {
        var parameters = new { type = "object", properties = new Dictionary<string, object> { [argument] = new { type = "string", description = explanation } }, required = new[] { argument }, additionalProperties = false };
        return responses ? new { type = "function", name, description, parameters, strict = true } : new { type = "function", function = new { name, description, parameters, strict = true } };
    }
    public static object[] Definitions(bool responses) => new[] {
        Definition("search_apps", "按名称、拼音或简称查找本机已安装的应用，返回应用 ID 和名称。不打开应用。", "query", "用户要找的软件名称，优先使用用户输入的中文显示名称", responses),
        Definition("open_app", "打开 search_apps 返回的精确应用 ID，不接受路径、网址、命令或额外启动参数。仅在用户要求打开该应用时调用。", "app_id", "search_apps 返回的 app_id，禁止猜测或自行构造", responses)
    };
    public Task<string> ExecuteLeoAsync(string action,string value,CancellationToken token){token.ThrowIfCancellationRequested();return OriginDispatcher?.Invoke(action,value,Source,token)??leo?.Invoke(action,value,token)??Task.FromResult("Leo 功能入口不可用");}
    public async Task<string> ExecuteAsync(ToolCall call,HashSet<string> opened,CancellationToken token){if(Brain!=null)return await Brain.Tool(call,token);if(call.Name!="leo_action"){if(!HasLeo||call.Name is not("search_apps" or "open_app"))return Execute(call,opened,token);try{using var arguments=JsonDocument.Parse(call.Arguments);string field=call.Name=="search_apps"?"query":"app_id";var root=arguments.RootElement;if(root.ValueKind!=JsonValueKind.Object||root.EnumerateObject().Any(p=>p.Name!=field)||!root.TryGetProperty(field,out var arg)||arg.ValueKind!=JsonValueKind.String)return JsonSerializer.Serialize(new{error="invalid_arguments"});string value=arg.GetString()?.Trim()??"";if(value.Length is <1 or >120)return JsonSerializer.Serialize(new{error="invalid_arguments"});if(call.Name=="open_app"&&opened.Contains(value))return "本轮已打开过这个应用。";string result=await ExecuteLeoAsync(call.Name=="search_apps"?"app_search":"app_open",value,token);if(call.Name=="open_app")opened.Add(value);return result;}catch(OperationCanceledException){throw;}catch(Exception e){return JsonSerializer.Serialize(new{error=e.Message});}}token.ThrowIfCancellationRequested();try{using var doc=JsonDocument.Parse(call.Arguments);var root=doc.RootElement;if(root.ValueKind!=JsonValueKind.Object||root.EnumerateObject().Any(p=>p.Name is not("action" or "value"))||!root.TryGetProperty("action",out var a)||a.ValueKind!=JsonValueKind.String||!root.TryGetProperty("value",out var v)||v.ValueKind!=JsonValueKind.String)return "{\"error\":\"invalid_arguments\"}";string action=a.GetString()!,value=v.GetString()!;if(!LeoActions.Names.Contains(action)||value.Length>4000)return "{\"error\":\"invalid_arguments\"}";string id="leo:"+call.Arguments;if(!LeoActions.ReadOnly.Contains(action)&&opened.Contains(id))return "本轮已执行过此操作，请勿重复。";string result=await ExecuteLeoAsync(action,value,token);if(!LeoActions.ReadOnly.Contains(action))opened.Add(id);return result;}catch(OperationCanceledException){throw;}catch(Exception e){return "操作失败："+e.Message;}}
    public static object LeoDefinition(bool responses){var parameters=new{type="object",properties=new{action=new{type="string",@enum=LeoActions.Names},value=new{type="string",description=LeoActions.Description}},required=new[]{"action","value"},additionalProperties=false};return responses?new{type="function",name="leo_action",description="调用Leo已有功能。仅实际成功后才能报告完成。",parameters,strict=true}:new{type="function",function=new{name="leo_action",description="调用Leo已有功能。仅实际成功后才能报告完成。",parameters,strict=true}};}
    public string Execute(ToolCall call, HashSet<string> opened, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (call.Name is not ("search_apps" or "open_app")) return JsonSerializer.Serialize(new { error = "unsupported_tool" });
        try
        {
            using var json = JsonDocument.Parse(call.Arguments);
            string argument = call.Name == "search_apps" ? "query" : "app_id";
            if (json.RootElement.ValueKind != JsonValueKind.Object || json.RootElement.EnumerateObject().Any(x => x.Name != argument)
                || !json.RootElement.TryGetProperty(argument, out var value) || value.ValueKind != JsonValueKind.String)
                return JsonSerializer.Serialize(new { error = "invalid_arguments" });
            string text = value.GetString()?.Trim() ?? "";
            if (text.Length == 0 || text.Length > 120) return JsonSerializer.Serialize(new { error = "invalid_arguments" });
            var apps = catalog().Where(Launchable).ToArray();
            if (call.Name == "search_apps")
            {
                var names = aliases();
                var matches = apps.Select(x => (App: x, Rank: AppSearch.Rank(x, text, names)))
                    .Where(x => x.Rank < 99 || x.App.ExePath.Length > 0 && Path.GetFileNameWithoutExtension(x.App.ExePath).Contains(text, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.Rank).ThenBy(x => x.App.Name.Length).Take(8).Select(x => new { app_id = Id(x.App), name = x.App.Name }).ToArray();
                return JsonSerializer.Serialize(new { apps = matches, note = matches.Length == 0 ? "未找到，应用可能仍在索引或没有开始菜单入口" : "如匹配有歧义，先询问用户，勿随机打开" });
            }
            var app = apps.FirstOrDefault(x => Id(x).Equals(text, StringComparison.OrdinalIgnoreCase));
            if (app == null) return JsonSerializer.Serialize(new { error = "unknown_app_id", note = "请先 search_apps 获取当前有效 ID" });
            if (opened.Contains(text)) return JsonSerializer.Serialize(new { success = true, name = app.Name, already_opened_in_this_turn = true });
            token.ThrowIfCancellationRequested(); launch(app); opened.Add(text);
            return JsonSerializer.Serialize(new { success = true, name = app.Name });
        }
        catch (OperationCanceledException) { throw; }
        catch (JsonException) { return JsonSerializer.Serialize(new { error = "invalid_json" }); }
        catch { return JsonSerializer.Serialize(new { error = "launch_failed", note = "无法启动该应用，请用户检查安装状态" }); }
    }
}

static class ChatAgent
{
    public static async Task<ChatReply> RunAsync(ChatApi api, ChatSettings settings, string key, List<ApiMessage> context,
        Action<string> onText, AppTools? tools, Action<string> onTool, CancellationToken token)
    {
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(token);if(tools?.Brain!=null){budget.CancelAfter(TimeSpan.FromSeconds(90));tools.Brain.Origin=tools.Source;tools.Brain.BeginTurn(context.LastOrDefault(m=>m.role=="user")?.content??"");}token=budget.Token;
        var opened = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int actions = 0;
        for (int round = 0; round < (tools?.Brain!=null?3:6); round++)
        {
            token.ThrowIfCancellationRequested();
            if(tools?.Brain!=null&&context.Sum(m=>m.content.Length+(m.Calls?.Sum(c=>c.Arguments.Length)??0))>32000)throw new InvalidOperationException("本轮上下文已达上限，请缩小请求或清空对话后重试");
            var reply = await api.StreamAsync(settings, key, context, onText, token, tools?.Brain?.Definitions(settings.Protocol=="responses")??(tools == null ? null : tools.HasLeo?AppTools.Definitions(settings.Protocol == "responses").Append(AppTools.LeoDefinition(settings.Protocol=="responses")).ToArray():AppTools.Definitions(settings.Protocol == "responses")));
            if (reply.Calls?.Length is not > 0) return reply;
            if (tools == null) throw new InvalidOperationException("当前未启用应用工具");
            context.Add(new("assistant", reply.Text, reply.Calls, ResponseItems: reply.ResponseItems));
            foreach (var call in reply.Calls)
            {
                token.ThrowIfCancellationRequested();
                if (++actions > (tools.Brain!=null?8:12)) throw new InvalidOperationException("本轮工具次数已达上限，请缩小任务后重试");
                onTool(call.Name == "search_apps" ? "正在查找应用…" : call.Name == "open_app" ? "正在打开应用…" : call.Name=="leo_action"?"Leo 正在处理…":"收到不支持的工具请求");
                string result = await tools.ExecuteAsync(call, opened, token);
                context.Add(new("tool", result, CallId: call.Id));
                if(tools.Brain?.Pending!=null)return new("已整理成待确认计划，尚未执行。请在预览中确认或取消。",false,false);
            }
        }
        throw new InvalidOperationException("模型连续调用工具过多，本轮已停止");
    }
}
