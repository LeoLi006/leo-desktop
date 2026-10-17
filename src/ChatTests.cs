using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
namespace LeoLauncher;

static class ChatTests
{
    public const string FixtureKey = "fixture-key-not-a-real-credential";
    public const string Events = ": ping\r\n\r\ndata: {\"choices\":[{\"delta\":{\"role\":\"assistant\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"你好，\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"Leo\"}}]}\n\ndata: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: {\"choices\":[],\"usage\":{\"total_tokens\":9}}\n\ndata: [DONE]\n\n";
    public static ChatSettings Settings()
    {
        var settings = new ChatSettings { Model = "fixture-model" }; settings.SetKey(FixtureKey); return settings;
    }
    sealed class FakeHandler : HttpMessageHandler
    {
        readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond;
        public FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) => respond = response;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
    }
    sealed class Fragments : Stream
    {
        readonly byte[] bytes;
        readonly int delay;
        int position;
        public int Consumed => position;
        public Fragments(string text, int delayMs = 0) { bytes = Encoding.UTF8.GetBytes(text); delay = delayMs; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => bytes.Length; public override long Position { get => position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { int n = Math.Min(3, Math.Min(count, bytes.Length - position)); Array.Copy(bytes, position, buffer, offset, n); position += n; return n; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (delay > 0) await Task.Delay(delay, cancellationToken); cancellationToken.ThrowIfCancellationRequested();
            int n = Math.Min(3, Math.Min(buffer.Length, bytes.Length - position)); bytes.AsMemory(position, n).CopyTo(buffer); position += n; return n;
        }
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    static HttpResponseMessage Sse(string data, int delay = 0)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new Fragments(data, delay)) };
        response.Content.Headers.ContentType = new("text/event-stream"); return response;
    }
    public static ChatApi FixtureApi(bool slow = false) => new(new FakeHandler((_, _) => Task.FromResult(Sse(Events, slow ? 15 : 1))));
    public static async Task<Dictionary<string, bool>> CheckAsync()
    {
        var checks = new Dictionary<string, bool>();
        var settings = Settings();
        checks["key_encryption_roundtrip"] = settings.ReadKey() == FixtureKey;
        checks["settings_do_not_contain_plaintext_key"] = !JsonSerializer.Serialize(settings).Contains(FixtureKey);
        checks["root_url_normalized_to_v1_once"] = ChatSettings.NormalizeBase("https://api.example.com/") == "https://api.example.com/v1"
            && ChatSettings.NormalizeBase("https://api.example.com/v1/chat/completions") == "https://api.example.com/v1";
        bool credentialsRejected = false;
        try { ChatSettings.NormalizeBase("https://user:key@example.com/v1?key=secret"); } catch (ArgumentException) { credentialsRejected = true; }
        checks["credentials_in_base_url_rejected"] = credentialsRejected;
        var pieces = new List<string>();
        using (var stream = new Fragments(Events))
        {
            bool incremental = false;
            var result = await ChatApi.ReadSseAsync(stream, false, delta => { pieces.Add(delta); incremental |= stream.Consumed < stream.Length; }, CancellationToken.None);
            checks["utf8_fragmented_sse_is_incremental"] = incremental && result.Text == "你好，Leo" && pieces.SequenceEqual(new[] { "你好，", "Leo" });
        }
        string responses = "event: response.output_text.delta\ndata: {\"type\":\"response.output_text.delta\",\"delta\":\"你好\"}\n\ndata: {\"type\":\"response.completed\"}\n\n";
        using (var stream = new Fragments(responses)) checks["responses_protocol_stream_supported"] = (await ChatApi.ReadSseAsync(stream, true, _ => { }, CancellationToken.None)).Text == "你好";
        using (var api = new ChatApi(new FakeHandler(async (request, token) => {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            checks["http_chat_route_auth_model_stream_payload"] = request.RequestUri!.AbsoluteUri == "https://api.example.com/v1/chat/completions"
                && request.Headers.Authorization?.Parameter == FixtureKey && body.RootElement.GetProperty("stream").GetBoolean()
                && body.RootElement.GetProperty("model").GetString() == "fixture-model" && body.RootElement.GetProperty("messages").GetArrayLength() == 1;
            return Sse(Events);
        }))) await api.StreamAsync(settings, FixtureKey, new[] { new ApiMessage("user", "hello") }, _ => { }, CancellationToken.None);
        using (var api = new ChatApi(new FakeHandler((request, _) => {
            checks["model_discovery_route_and_auth"] = request.RequestUri!.AbsolutePath == "/v1/models" && request.Headers.Authorization?.Parameter == FixtureKey;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[{\"id\":\"b\"},{\"id\":\"a\"},{\"id\":\"b\"}]}") });
        }))) checks["model_list_sorted_and_deduplicated"] = (await api.ModelsAsync(settings, FixtureKey, CancellationToken.None)).SequenceEqual(new[] { "a", "b" });
        using (var api = new ChatApi(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"兼容结果\"}}]}", Encoding.UTF8, "application/json") }))))
            checks["nonstreaming_gateway_fallback"] = !(await api.StreamAsync(settings, FixtureKey, new[] { new ApiMessage("user", "hello") }, _ => { }, CancellationToken.None)).Streamed;
        using (var api = new ChatApi(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent(FixtureKey) }))))
        {
            try { await api.StreamAsync(settings, FixtureKey, new[] { new ApiMessage("user", "hello") }, _ => { }, CancellationToken.None); checks["auth_error_does_not_echo_key"] = false; }
            catch (InvalidOperationException e) { checks["auth_error_does_not_echo_key"] = e.Message.Contains("401") && !e.Message.Contains(FixtureKey); }
        }
        using (var api = FixtureApi(true))
        using (var cancellation = new CancellationTokenSource(40))
        {
            try { await api.StreamAsync(settings, FixtureKey, new[] { new ApiMessage("user", "hello") }, _ => { }, cancellation.Token); checks["request_cancellation_works"] = false; }
            catch (OperationCanceledException) { checks["request_cancellation_works"] = true; }
        }
        using (var stream = new Fragments("data: {\"choices\":[{\"delta\":{\"content\":\"一半\"}}]}\n\n"))
        {
            try { await ChatApi.ReadSseAsync(stream, false, _ => { }, CancellationToken.None); checks["truncated_connection_is_not_success"] = false; }
            catch (IOException) { checks["truncated_connection_is_not_success"] = true; }
        }
        var context = ChatForm.Context(new[] { new ChatTurn { User = "failed", Assistant = "partial", Status = "未完成" }, new ChatTurn { User = "old", Assistant = "done", Status = "完成" } }, "new");
        checks["failed_turns_not_sent_as_complete_context"] = context.Count == 4 && context[1].content == "old" && context[^1].content == "new";
        var fixtureApp = new AppEntry("记事本", "fixture-notepad.lnk");
        int launches = 0, requests = 0;
        var appTools = new AppTools(() => new[] { fixtureApp }, _ => launches++);
        using (var api = new ChatApi(new FakeHandler(async (request, token) => {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            int step = requests++;
            if (step > 0) {
                var messages = body.RootElement.GetProperty("messages");
                checks["tool_result_id_round_" + step] = messages[messages.GetArrayLength()-1].GetProperty("tool_call_id").GetString() == "call" + (step-1);
            }
            if (step == 2) return Sse(Events);
            string name = step == 0 ? "search_apps" : "open_app";
            string arguments = step == 0 ? "{\"query\":\"记事本\"}" : JsonSerializer.Serialize(new { app_id = AppTools.Id(fixtureApp) });
            int midpoint = arguments.Length / 2;
            string Event(object delta) => "data: " + JsonSerializer.Serialize(new { choices = new[] { new { delta } } }) + "\n\n";
            return Sse(Event(new { tool_calls = new[] { new { index = 0, id = "call" + step, function = new { name, arguments = arguments[..midpoint] } } } })
                + Event(new { tool_calls = new[] { new { index = 0, function = new { arguments = arguments[midpoint..] } } } })
                + "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"tool_calls\"}]}\n\ndata: [DONE]\n\n");
        }))) {
            var reply = await ChatAgent.RunAsync(api, settings, FixtureKey, new() { new("user", "打开记事本") }, _ => { }, appTools, _ => { }, CancellationToken.None);
            checks["streamed_search_open_reply_roundtrip"] = requests == 3 && launches == 1 && reply.Text == "你好，Leo";
        }
        var opened = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var openCall = new ToolCall("x", "open_app", JsonSerializer.Serialize(new { app_id = AppTools.Id(fixtureApp) }));
        appTools.Execute(openCall, opened, CancellationToken.None); appTools.Execute(openCall, opened, CancellationToken.None);
        checks["duplicate_open_is_idempotent"] = launches == 2;
        appTools.Execute(new("x", "open_app", "{\"app_id\":\"unknown\"}"), opened, CancellationToken.None);
        appTools.Execute(new("x", "open_app", "{\"app_id\":\"unknown\",\"command\":\"cmd\"}"), opened, CancellationToken.None);
        appTools.Execute(new("x", "shell", "{}"), opened, CancellationToken.None);
        checks["invalid_tools_and_arguments_never_launch"] = launches == 2;
        using (var canceled = new CancellationTokenSource()) {
            canceled.Cancel();
            try { appTools.Execute(openCall, opened, canceled.Token); checks["canceled_tools_never_launch"] = false; }
            catch (OperationCanceledException) { checks["canceled_tools_never_launch"] = launches == 2; }
        }
        checks["speech_strips_code_and_markdown"] = SpeechIO.PlainSpeech("**你好** ```secret code``` [链接](https://example.com)") == "你好  代码片段略。  链接";
        return checks;
    }
}
