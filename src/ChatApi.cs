using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace LeoLauncher;

sealed class ChatSettings
{
    public string BaseUrl { get; set; } = "https://api.example.com/v1";
    public string Model { get; set; } = "";
    public string Protocol { get; set; } = "chat";
    public string EncryptedKey { get; set; } = "";
    public bool RememberConversation { get; set; }
    public bool EnableAppTools { get; set; } = true;
    public bool VoiceAutoSpeak { get; set; }
    public bool VoiceAutoSend { get; set; }
    public static string SettingsPath => Path.Combine(Store.Root, "leo-ai.json");
    public string ReadKey()
    {
        if (EncryptedKey.Length == 0) return "";
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(EncryptedKey), null, DataProtectionScope.CurrentUser)); }
        catch { return ""; }
    }
    public void SetKey(string key) => EncryptedKey = key.Length == 0 ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), null, DataProtectionScope.CurrentUser));
    public void Save() { File.WriteAllText(SettingsPath + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); File.Move(SettingsPath + ".tmp", SettingsPath, true); }
    public static ChatSettings Load()
    {
        try { return JsonSerializer.Deserialize<ChatSettings>(File.ReadAllText(SettingsPath)) ?? new(); }
        catch { return new(); }
    }
    public static string NormalizeBase(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new ArgumentException("请填写完整的 Base URL，不要包含 Key、查询参数或片段");
        if (uri.Scheme == "http" && !uri.IsLoopback) throw new ArgumentException("远程中转站请使用 HTTPS；HTTP 只支持本机服务");
        string path = uri.AbsolutePath.TrimEnd('/');
        foreach (string suffix in new[] { "/chat/completions", "/responses", "/models" })
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { path = path[..^suffix.Length]; break; }
        if (path.Length == 0) path = "/v1";
        return uri.GetLeftPart(UriPartial.Authority) + path;
    }
    public Uri Endpoint(string name) => new(NormalizeBase(BaseUrl) + "/" + name);
}

sealed record ToolCall(string Id, string Name, string Arguments);
sealed record ApiMessage(string role, string content, ToolCall[]? Calls = null, string? CallId = null, JsonElement[]? ResponseItems = null);
sealed record ChatReply(string Text, bool Streamed, bool Truncated, ToolCall[]? Calls = null, JsonElement[]? ResponseItems = null);

sealed class ChatApi : IDisposable
{
    readonly HttpClient http;
    readonly ApiUsageLedger? ledger;
    public ChatApi(HttpMessageHandler? handler = null,ApiUsageLedger? usage=null)
    {
        ledger=usage??(handler==null?ApiUsageLedger.Shared:null);
        http = handler == null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) : new HttpClient(handler);
        http.Timeout = Timeout.InfiniteTimeSpan;
    }
    static HttpRequestMessage Request(ChatSettings settings, string key, HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Contains('\r') || key.Contains('\n')) throw new ArgumentException("请先在 API 设置中填写有效 Key");
        var request = new HttpRequestMessage(method, settings.Endpoint(path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
        return request;
    }
    static void CheckStatus(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        string hint = (int)response.StatusCode switch {
            401 => "API Key 缺失或无效，请检查中转站密钥",
            403 => "中转站拒绝访问，请检查模型权限或额度",
            404 => "接口或模型不存在，请检查 Base URL、接口类型和模型名称",
            429 => "请求过于频繁或额度不足，请稍后重试",
            >= 500 => "中转站或上游服务暂时不可用，请稍后重试",
            >= 300 and < 400 => "接口返回重定向，请直接填写最终 API 地址",
            _ => "请求未被接受，请检查接口类型、模型名称与中转站配置"
        };
        throw new InvalidOperationException($"HTTP {(int)response.StatusCode} · {hint}");
    }
    public async Task<string[]> ModelsAsync(ChatSettings settings, string key, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(25));
        using var request = Request(settings, key, HttpMethod.Get, "models");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        CheckStatus(response);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("中转站未返回兼容的模型列表，可手动填写模型 ID");
        return data.EnumerateArray().Where(x => x.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            .Select(x => x.GetProperty("id").GetString()!).Where(x => x.Length > 0).Distinct().OrderBy(x => x).Take(5000).ToArray();
    }
    public async Task<ChatReply> StreamAsync(ChatSettings settings, string key, IReadOnlyList<ApiMessage> messages, Action<string> onText, CancellationToken token, object[]? tools = null)
    {
        if (string.IsNullOrWhiteSpace(settings.Model)) throw new ArgumentException("请先选择或填写模型名称");
        if (settings.Protocol is not ("chat" or "responses")) throw new ArgumentException("不支持的接口类型");
        var budget=ledger==null?new ApiUsageConfig():ApiUsageConfig.Load();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            bool responses = settings.Protocol == "responses";
            using var request = Request(settings, key, HttpMethod.Post, responses ? "responses" : "chat/completions");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            var payload = new Dictionary<string, object> { ["model"] = settings.Model.Trim(), ["stream"] = true };
            if (responses) payload["input"] = ResponsesInput(messages);
            else {payload["messages"] = messages.Select(ChatMessage).ToArray();if(budget.RequestTokens)payload["stream_options"]=new{include_usage=true};}
            if (tools?.Length > 0) payload["tools"] = tools;
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            timeout.Token.ThrowIfCancellationRequested();
            using var usage=ledger?.Begin(budget.DailyLimit);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            CheckStatus(response);
            if (response.Content.Headers.ContentType?.MediaType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                usage?.Usage(json.RootElement);
                string text = NonStreamingText(json.RootElement, responses);
                var calls = ParseCalls(json.RootElement, responses);
                if (calls.Length > 0 && tools?.Length is not > 0) throw new InvalidOperationException("请先开启应用工具");
                if (text.Length == 0 && calls.Length == 0) throw new InvalidOperationException("中转站没有返回可显示的文字");
                bool limited = responses
                    ? json.RootElement.TryGetProperty("status", out var status) && status.GetString() == "incomplete"
                    : json.RootElement.GetProperty("choices")[0].TryGetProperty("finish_reason", out var reason) && reason.ValueKind == JsonValueKind.String && reason.GetString() is "length" or "content_filter";
                onText(text);if(usage!=null)usage.Completed=!limited; return new(text, false, limited, calls, responses ? OutputItems(json.RootElement) : null);
            }
            using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var reply=await ReadSseAsync(stream, responses, onText, timeout.Token, tools?.Length > 0,usage==null?null:usage.Usage);if(usage!=null)usage.Completed=!reply.Truncated;return reply;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("等待中转站超过 5 分钟，已停止本次请求"); }
    }
    static string ContentText(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? "";
        if (value.ValueKind != JsonValueKind.Array) return "";
        return string.Concat(value.EnumerateArray().Where(x => x.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String).Select(x => x.GetProperty("text").GetString()));
    }
    static string NonStreamingText(JsonElement root, bool responses)
    {
        if (responses && root.TryGetProperty("output", out var output))
            return string.Concat(output.EnumerateArray().Where(x => x.TryGetProperty("content", out _)).Select(x => ContentText(x.GetProperty("content"))));
        if (!responses && root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0 && choices[0].TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content)) return ContentText(content);
        return "";
    }
    public static async Task<ChatReply> ReadSseAsync(Stream stream, bool responses, Action<string> onText, CancellationToken token, bool allowTools = false,Action<JsonElement>? onUsage=null)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: true);
        var text = new StringBuilder(); var data = new StringBuilder(); bool complete = false, truncated = false;
        var streamedCalls = new SortedDictionary<int, (string Id, string Name, string Args)>();
        var responseItems = new Dictionary<string, JsonElement>();
        ToolCall[] responseCalls = Array.Empty<ToolCall>();
        bool ProcessEvent()
        {
            string raw = data.ToString().Trim(); data.Clear();
            if (raw.Length == 0) return false;
            if (raw == "[DONE]") { complete = true; return true; }
            using var json = JsonDocument.Parse(raw); var root = json.RootElement;onUsage?.Invoke(root);
            if (root.TryGetProperty("error", out _)) throw new InvalidOperationException("中转站在生成过程中返回错误，请检查额度或模型状态");
            string delta = "";
            if (responses)
            {
                string type = root.TryGetProperty("type", out var kind) ? kind.GetString() ?? "" : "";
                if (type == "response.output_text.delta") delta = root.GetProperty("delta").GetString() ?? "";
                else if (type == "response.output_item.done" && root.TryGetProperty("item", out var item))
                {
                    string id = item.TryGetProperty("id", out var iid) ? iid.GetString() ?? "" : "";
                    if (id.Length > 0) responseItems[id] = item.Clone();
                }
                else if (type == "response.completed") {
                    if (root.TryGetProperty("response", out var response)) {
                        responseCalls = ParseCalls(response, true);
                        foreach (var output in OutputItems(response)) if (output.TryGetProperty("id", out var oid)) responseItems[oid.GetString() ?? ""] = output;
                        if (text.Length == 0) { string fallback = NonStreamingText(response, true); if (fallback.Length > 0) { text.Append(fallback); onText(fallback); } }
                    }
                    complete = true; return true;
                }
                else if (type is "error" or "response.failed" or "response.incomplete") throw new InvalidOperationException("模型回复未完成，中转站报告生成失败或中断");
            }
            else if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var part)) {
                    if (part.TryGetProperty("content", out var content)) delta = ContentText(content);
                    if (part.TryGetProperty("tool_calls", out var toolCalls)) foreach (var call in toolCalls.EnumerateArray()) {
                        int index = call.GetProperty("index").GetInt32();
                        if (index < 0 || index >= 8) throw new InvalidOperationException("单轮工具请求过多");
                        (string Id, string Name, string Args) current = streamedCalls.GetValueOrDefault(index, ("", "", ""));
                        if (call.TryGetProperty("id", out var id)) current.Id += id.GetString();
                        if (call.TryGetProperty("function", out var function)) {
                            if (function.TryGetProperty("name", out var name)) current.Name += name.GetString();
                            if (function.TryGetProperty("arguments", out var args)) current.Args += args.GetString();
                        }
                        if (current.Args.Length > 64000) throw new InvalidOperationException("工具参数过长");
                        streamedCalls[index] = current;
                    }
                }
                if (choice.TryGetProperty("finish_reason", out var reason) && reason.ValueKind == JsonValueKind.String)
                {
                    string finish = reason.GetString()!;
                    if (finish is "tool_calls" or "function_call" && !allowTools) throw new InvalidOperationException("当前未开启应用工具");
                    complete = true; truncated = finish is "length" or "content_filter";
                }
            }
            if (delta.Length > 0) { text.Append(delta); onText(delta); }
            return false;
        }
        while (true)
        {
            string? line = await reader.ReadLineAsync(token);
            if (line == null) { if (data.Length > 0) ProcessEvent(); break; }
            if (line.Length == 0) { if (ProcessEvent()) break; }
            else if (line.StartsWith("data:", StringComparison.Ordinal)) { if (data.Length > 0) data.Append('\n'); data.Append(line[5..].TrimStart(' ')); }
        }
        if (!complete) throw new IOException("回复连接提前断开，已收到的文字保留，但本轮未完整完成");
        var calls = responses ? responseCalls : streamedCalls.Values.Select(x => new ToolCall(x.Id, x.Name, x.Args)).ToArray();
        if (responses && calls.Length == 0) calls = responseItems.Values.Where(x => x.TryGetProperty("type", out var t) && t.GetString() == "function_call")
            .Select(ParseResponseCall).ToArray();
        if (calls.Length > 0 && !allowTools) throw new InvalidOperationException("当前未开启应用工具");
        if (calls.Any(x => x.Id.Length == 0 || x.Name.Length == 0 || x.Arguments.Length > 64000) || calls.Length > 8) throw new InvalidOperationException("中转站返回了无效工具调用");
        if (text.Length == 0 && calls.Length == 0) throw new InvalidOperationException("模型未返回可显示的文字；请确认该模型支持文字聊天");
        return new(text.ToString(), true, truncated, calls, responseItems.Values.ToArray());
    }
    static object ChatMessage(ApiMessage message)
    {
        var result = new Dictionary<string, object> { ["role"] = message.role, ["content"] = message.content };
        if (message.CallId != null) result["tool_call_id"] = message.CallId;
        if (message.Calls?.Length > 0) result["tool_calls"] = message.Calls.Select(x => new { id = x.Id, type = "function", function = new { name = x.Name, arguments = x.Arguments } }).ToArray();
        return result;
    }
    static object[] ResponsesInput(IEnumerable<ApiMessage> messages)
    {
        var items = new List<object>();
        foreach (var message in messages) {
            if (message.role == "tool") { items.Add(new { type = "function_call_output", call_id = message.CallId, output = message.content }); continue; }
            if (message.ResponseItems?.Length > 0) { items.AddRange(message.ResponseItems.Cast<object>()); continue; }
            if (message.content.Length > 0) items.Add(new { role = message.role, content = message.content });
            if (message.Calls != null) foreach (var call in message.Calls) items.Add(new { type = "function_call", call_id = call.Id, name = call.Name, arguments = call.Arguments });
        }
        return items.ToArray();
    }
    static JsonElement[] OutputItems(JsonElement root) => root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array ? output.EnumerateArray().Select(x => x.Clone()).ToArray() : Array.Empty<JsonElement>();
    static ToolCall ParseResponseCall(JsonElement call) => new(call.GetProperty("call_id").GetString() ?? "", call.GetProperty("name").GetString() ?? "", call.GetProperty("arguments").GetString() ?? "{}");
    static ToolCall[] ParseCalls(JsonElement root, bool responses)
    {
        if (responses) return OutputItems(root).Where(x => x.TryGetProperty("type", out var t) && t.GetString() == "function_call").Select(ParseResponseCall).ToArray();
        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0 || !choices[0].TryGetProperty("message", out var message) || !message.TryGetProperty("tool_calls", out var calls)) return Array.Empty<ToolCall>();
        return calls.EnumerateArray().Select(x => new ToolCall(x.GetProperty("id").GetString() ?? "", x.GetProperty("function").GetProperty("name").GetString() ?? "", x.GetProperty("function").GetProperty("arguments").GetString() ?? "{}")).ToArray();
    }
    public void Dispose() => http.Dispose();
}
