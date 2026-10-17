using System.Text.Json;
namespace LeoLauncher;

sealed class ApiSettingsForm : Form
{
    readonly TextBox url = new(), key = new();
    readonly ComboBox model = new(), protocol = new();
    readonly Label status = new();
    readonly ChatSettings settings;
    readonly CancellationTokenSource lifetime = new();
    public ApiSettingsForm(ChatSettings current, bool preview = false)
    {
        settings = current;
        Text = "Leo · API 设置"; ClientSize = new Size(550, 350); StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        Font = Appearance.Body(); BackColor = Appearance.Background; ForeColor = Appearance.Ink;
        void Label(string text, int top) => Controls.Add(new Label { Text = text, Bounds = new Rectangle(20, top, 115, 25), ForeColor = Appearance.Muted });
        Label("接口类型", 25); protocol.SetBounds(138, 20, 390, 28); protocol.DropDownStyle = ComboBoxStyle.DropDownList;
        protocol.Items.AddRange(new[] { "OpenAI 兼容 · Chat Completions", "OpenAI 兼容 · Responses" }); protocol.SelectedIndex = current.Protocol == "responses" ? 1 : 0;
        Label("Base URL", 70); url.SetBounds(138, 65, 390, 28); url.Text = current.BaseUrl;
        Label("API Key", 115); key.SetBounds(138, 110, 390, 28); key.UseSystemPasswordChar = true; key.Text = current.ReadKey();
        Label("模型名称", 160); model.SetBounds(138, 155, 280, 28); model.DropDownStyle = ComboBoxStyle.DropDown; model.Text = current.Model;
        foreach (var control in new Control[] { protocol, url, key, model }) { control.BackColor = Appearance.Field; control.ForeColor = Appearance.Ink; Controls.Add(control); }
        var fetch = new Button { Text = "读取模型", Bounds = new Rectangle(428, 154, 100, 30) }; Controls.Add(fetch);
        status.SetBounds(20, 205, 508, 62); status.ForeColor = Appearance.Muted;
        status.Text = "Key 仅在本机加密保存；不用发到聊天里。\n读取模型会连接所填中转站；若不支持列表，可手动填写模型 ID。"; Controls.Add(status);
        fetch.Click += async (_, _) => {
            if (preview) { status.Text = "示例预览，不连接中转站"; return; }
            fetch.Enabled = false; status.Text = "正在读取中转站可用模型…";
            try
            {
                var candidate = new ChatSettings { BaseUrl = ChatSettings.NormalizeBase(url.Text) };
                using var api = new ChatApi();
                var names = await api.ModelsAsync(candidate, key.Text, lifetime.Token);
                if (IsDisposed) return;
                string previous = model.Text; model.Items.Clear(); model.Items.AddRange(names);
                if (previous.Length > 0) model.Text = previous; else if (names.Length > 0) model.SelectedIndex = 0;
                status.Text = names.Length > 0 ? $"已读取 {names.Length} 个模型，可从下拉列表选择或手动填写。" : "中转站返回空模型列表，请手动填写可用模型 ID。";
            }
            catch (OperationCanceledException) { if (!IsDisposed) status.Text = "读取已取消或超时，可手动填写模型 ID。"; }
            catch (Exception e) { if (!IsDisposed) status.Text = SafeError(e); }
            finally { if (!IsDisposed) fetch.Enabled = true; }
        };
        var save = new Button { Text = "保存", Bounds = new Rectangle(428, 292, 100, 32) }; Controls.Add(save);
        save.Click += (_, _) => {
            if (preview) { status.Text = "示例预览，不保存配置"; return; }
            try
            {
                string normalized = ChatSettings.NormalizeBase(url.Text);
                if (string.IsNullOrWhiteSpace(model.Text)) throw new ArgumentException("请选择或手动填写模型名称");
                if (string.IsNullOrWhiteSpace(key.Text)) throw new ArgumentException("请填写中转站 API Key");
                var next = new ChatSettings { BaseUrl = normalized, Model = model.Text.Trim(), Protocol = protocol.SelectedIndex == 1 ? "responses" : "chat", RememberConversation = settings.RememberConversation, EnableAppTools = settings.EnableAppTools, VoiceAutoSend = settings.VoiceAutoSend, VoiceAutoSpeak = settings.VoiceAutoSpeak };
                next.SetKey(key.Text); next.Save();
                DialogResult = DialogResult.OK; Close();
            }
            catch (Exception e) { status.Text = SafeError(e); }
        };
        var clear = new Button { Text = "清除已存 Key", Bounds = new Rectangle(20, 292, 140, 32) }; Controls.Add(clear);
        clear.Click += (_, _) => { if (preview) return; try { settings.SetKey(""); settings.Save(); key.Clear(); status.Text = "已清除本地保存的 Key"; } catch { status.Text = "无法清除本地配置，请检查文件写入权限"; } };
        var cancel = new Button { Text = "取消", Bounds = new Rectangle(318, 292, 100, 32), DialogResult = DialogResult.Cancel }; Controls.Add(cancel); CancelButton = cancel;
        foreach (var button in Controls.OfType<Button>()) { button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0; button.BackColor = Appearance.Field; button.ForeColor = Appearance.Ink; }
    }
    string SafeError(Exception error)
    {
        string text = error is System.Net.Http.HttpRequestException ? "连接失败，请检查 Base URL、网络和系统代理。" : error.Message;
        if (key.Text.Length > 0) text = text.Replace(key.Text, "[Key 已隐藏]", StringComparison.Ordinal);
        if (key.Text.Trim().Length > 0) text = text.Replace(key.Text.Trim(), "[Key 已隐藏]", StringComparison.Ordinal);
        return text.Length > 260 ? text[..260] : text;
    }
    protected override void OnFormClosed(FormClosedEventArgs e) { lifetime.Cancel(); lifetime.Dispose(); base.OnFormClosed(e); }
}

sealed class ChatTurn
{
    public string User { get; set; } = "";
    public string Assistant { get; set; } = "";
    public string Status { get; set; } = "完成";
    public List<string> ToolLog { get; set; } = new();
}

sealed partial class ChatForm : BarcaWindow
{
    readonly ConversationView transcript = new();
    readonly TextBox input = new();
    readonly Button send = new(), stop = new(), configure = new(), reset = new();
    readonly Label state = new();
    readonly CheckBox remember = new(), useTools = new(), autoSpeak = new(), autoSend = new();
    readonly Button microphone = new(), readAloud = new();
    readonly Button inputDevice=new BarcaButton{Text="输入设备",Size=new Size(86,31)};
    readonly SpeechIO voice = new();
    readonly AppTools? appTools;
    readonly System.Windows.Forms.Timer renderTimer = new() { Interval = 80 };
    readonly bool preview;
    ChatSettings settings;
    readonly List<ChatTurn> turns = new();
    CancellationTokenSource? pending;
    bool dirty;
    bool directVoice;
    public void ToggleVoiceInput(){if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;Show();Activate();microphone.PerformClick();}
    readonly Func<ChatApi>? testApi;
    public bool IsThinking => pending != null || Visible && input.Focused && input.TextLength > 0;
    static string HistoryPath => Path.Combine(Store.Root, "leo-chat-history.json");
    public ChatForm(bool previewMode = false, ChatSettings? testSettings = null, Func<ChatApi>? testFactory = null, AppTools? applicationTools = null, Action? clipboardAction = null, Action<QuickIntent>? onTodo = null,TodoStore? planner=null)
    {
        preview = previewMode; settings = testSettings ?? (preview ? new ChatSettings() : ChatSettings.Load()); testApi = testFactory; appTools = applicationTools;
        todoAction=onTodo;openClipboard=clipboardAction;planStore=planner;
        TopMost = false; ShowInTaskbar = true; Text = "Leo · AI 对话"; StartPosition = FormStartPosition.Manual; ClientSize = new Size(600, 610); MinimumSize = new Size(500, 430);
        Font=BarcaUi.Font(10);BackColor=BarcaUi.Canvas;ForeColor=BarcaUi.Text;ClientSize=new Size(620,440);MinimumSize=new Size(600,430);Text="问 Leo";
        CanResize=true;FormBorderStyle=FormBorderStyle.Sizable;MaximizeBox=true;MinimizeBox=true;Text="Leo";ClientSize=new Size(570,440);MinimumSize=new Size(490,360);Padding=new Padding(1);
        var chrome=new Panel{Dock=DockStyle.Top,Height=55,Padding=new Padding(18,10,16,8)};
        chrome.Controls.Add(new Label{Text="LEO",Dock=DockStyle.Fill,Font=BarcaUi.Font(15,true),ForeColor=BarcaUi.Gold});
        settingsButton.Text="···";settingsButton.Width=36;settingsButton.Dock=DockStyle.Right;chrome.Controls.Add(settingsButton);
        settingsButton.Click+=(_,_)=>{options.Visible=!options.Visible;topPanel.Height=options.Visible?154:55;};
        configure.Text="连接设置";configure.Size=new Size(92,30);reset.Text="清空对话";reset.Size=new Size(88,30);remember.Text="保存会话";remember.Checked=settings.RememberConversation;remember.AutoSize=true;remember.Padding=new Padding(8,5,0,0);remember.ForeColor=BarcaUi.Muted;
        var header=new FlowLayoutPanel{Dock=DockStyle.Top,Height=43,Padding=new Padding(23,6,8,3),WrapContents=false};header.Controls.AddRange([configure,reset,remember]);
        var voiceBar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=47,Padding=new Padding(23,3,5,4),WrapContents=false};
        useTools.Text="Leo 功能调用";useTools.AutoSize=true;useTools.Checked=true;settings.EnableAppTools=true;useTools.Padding=new Padding(0,5,7,0);readAloud.Text="朗读";readAloud.Size=new Size(65,30);
        autoSpeak.Text="自动朗读";autoSpeak.AutoSize=true;autoSpeak.Checked=settings.VoiceAutoSpeak;autoSpeak.Padding=new Padding(7,5,0,0);autoSend.Text="转写后提交";autoSend.AutoSize=true;autoSend.Checked=settings.VoiceAutoSend;autoSend.Padding=new Padding(7,5,0,0);
        voiceBar.Controls.AddRange([inputDevice,readAloud,autoSpeak]);options.Controls.Add(header);options.Controls.Add(voiceBar);
        topPanel.Dock=DockStyle.Top;topPanel.Height=55;topPanel.Controls.Add(chrome);topPanel.Controls.Add(options);
        var bottom=new Panel{Dock=DockStyle.Bottom,Height=108,Padding=new Padding(16,7,16,15)};var composer=new RoundedSurface{Dock=DockStyle.Fill,BackColor=BarcaUi.Raised,Padding=new Padding(14,12,14,9)};
        input.Multiline=true;input.Dock=DockStyle.Fill;input.ScrollBars=ScrollBars.None;input.BorderStyle=BorderStyle.None;input.Font=BarcaUi.Font(11);input.BackColor=BarcaUi.Raised;input.ForeColor=BarcaUi.Text;input.PlaceholderText="想办什么，直接说或输入…";
        var actions=new Panel{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(0,3,0,0)};send.Text="发送 ↗";send.Dock=DockStyle.Right;send.Width=78;microphone.Text="说话";microphone.Dock=DockStyle.Right;microphone.Width=88;stop.Text="停止";stop.Dock=DockStyle.Right;stop.Width=55;stop.Visible=false;
        state.Dock=DockStyle.Fill;state.ForeColor=BarcaUi.Muted;state.Font=BarcaUi.Font(8.5f);state.AutoEllipsis=true;state.Padding=new Padding(0,8,7,0);actions.Controls.Add(state);actions.Controls.Add(stop);actions.Controls.Add(microphone);actions.Controls.Add(send);composer.Controls.Add(input);composer.Controls.Add(actions);bottom.Controls.Add(composer);
        transcript.Dock=DockStyle.Fill;bodyHost.Controls.Add(transcript);bodyHost.Controls.Add(results);Controls.Add(bodyHost);Controls.Add(bottom);Controls.Add(topPanel);
        foreach(var button in new[]{send,stop,configure,reset,microphone,readAloud}){button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=0;button.BackColor=BarcaUi.Raised;button.ForeColor=BarcaUi.Text;button.Font=BarcaUi.Font(9);Appearance.Round(button,9);}send.BackColor=BarcaUi.Raised;microphone.BackColor=Appearance.Blue;microphone.ForeColor=BarcaUi.Gold;
        var deviceMenu=new ContextMenuStrip();inputDevice.Click+=(_,_)=>{foreach(ToolStripItem item in deviceMenu.Items.Cast<ToolStripItem>().ToArray())item.Dispose();deviceMenu.Items.Clear();void Add(int id,string name){var item=new ToolStripMenuItem(name){Checked=voice.InputDevice==id};item.Click+=(_,_)=>{voice.InputDevice=id;state.Text="录音设备："+name;};deviceMenu.Items.Add(item);}Add(-1,"Windows 默认输入设备");try{foreach(var device in SpeechIO.InputDevices())Add(device.Id,device.Name);}catch{state.Text="无法读取录音设备";}deviceMenu.Show(inputDevice,new Point(0,inputDevice.Height));};FormClosed+=(_,_)=>deviceMenu.Dispose();
        if (!preview && settings.RememberConversation)
        {
            try { turns.AddRange((JsonSerializer.Deserialize<List<ChatTurn>>(File.ReadAllText(HistoryPath)) ?? new()).Where(x => x != null).TakeLast(40)); } catch { }
        }
        configure.Click += (_, _) => {
            using var dialog = new ApiSettingsForm(settings, preview);
            dialog.ShowDialog(this); settings = preview ? settings : ChatSettings.Load(); UpdateState();
        };
        reset.Click += (_, _) => { turns.Clear(); ShowHome(); SaveHistory(); state.Text = "已开始新对话"; input.Focus(); };
        remember.CheckedChanged += (_, _) => {
            settings.RememberConversation = remember.Checked;
            if (!preview) { try { settings.Save(); SaveHistory(); } catch { state.Text = "本地配置保存失败"; } }
        };
        send.Click += async (_, _) => await SendAsync();
        stop.Click += (_, _) => { pending?.Cancel(); voice.Stop(); UpdateVoice(); };
        useTools.CheckedChanged += (_, _) => { settings.EnableAppTools = useTools.Checked; SaveOptions(); };
        autoSpeak.CheckedChanged += (_, _) => { settings.VoiceAutoSpeak = autoSpeak.Checked; SaveOptions(); };
        autoSend.CheckedChanged += (_, _) => { settings.VoiceAutoSend = autoSend.Checked; SaveOptions(); };
        microphone.Click += (_, _) => {
            if (preview) { state.Text = "示例模式，不启用麦克风"; return; }
            try { if (voice.Listening) voice.FinishListening(); else { directVoice=true; voice.Start(); } UpdateVoice(); }
            catch (Exception error) { voice.Stop(); state.Text = "语音输入失败："+error.Message; }
        };
        readAloud.Click += (_, _) => {
            if (preview) return;
            try { if (voice.Speaking) voice.StopSpeaking(); else voice.Speak(lastAnswer.Length>0?lastAnswer:turns.LastOrDefault(x => x.Assistant.Length > 0)?.Assistant ?? "你好，我是 Leo。"); }
            catch { state.Text = "无法朗读，请检查 Windows 语音和音频输出"; }
        };
        voice.Changed += () => OnUi(UpdateVoice);
        voice.Status += message => OnUi(() => state.Text = message);
        voice.Recognized += (text,confident) => OnUi(async()=>await ReceiveVoiceAsync(text,confident));
        input.KeyDown += async (_, e) => { if (!e.Shift && e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SendAsync(); } };
        renderTimer.Tick += (_, _) => { if (dirty) { dirty = false; Render(); } };
        renderTimer.Start(); Render(); ShowHome(); UpdateState();
        bodyHost.SizeChanged+=(_,_)=>{foreach(Control c in results.Controls)c.Width=ResultWidth;};
        Shown += (_, _) => input.Focus();
    }
    async Task ReceiveVoiceAsync(string text,bool confident){if(pending!=null)return;input.AppendText((input.TextLength>0?" ":"")+text);if((directVoice||autoSend.Checked||QuickRouter.IsLocalMusic(text)&&string.Equals(input.Text.Trim(),text.Trim(),StringComparison.Ordinal))&&confident){directVoice=false;voice.StopListening();await SendAsync();}else state.Text=confident?"已识别，可编辑后发送":"识别不确定，已填入草稿，请校对后发送";}
    public Task PreviewRecognizedAsync(string text,bool confident)=>ReceiveVoiceAsync(text,confident);
    void UpdateState() => state.Text = "Ctrl+Alt+V 直接说话";
    public static List<ApiMessage> Context(IEnumerable<ChatTurn> history, string prompt, bool toolsEnabled = false)
    {
        string instruction = "当前本地时间："+DateTimeOffset.Now.ToString("yyyy-MM-dd dddd HH:mm zzz")+"。你是桌面伙伴Leo的大脑，主要任务是理解用户的自然语言并协调Leo已有功能，不是办公助手。默认用中文简短自然回答，通常不超过150字，用户要求深入时再展开。语音输入和朗读由本地界面处理，你处理识别后的文字。只有明确附带的查询数据是已联网核验的事实；没有通用网页浏览能力，不得假装搜索新闻、伤停或实时信息。资料不足时直接说明并建议用户用网页搜索。提醒仅在Leo运行期间生效，退出期间错过的会在下次启动补报，不可声称关机也会响铃。清单和提醒使用leo_action工具实际处理后再报告结果；查询任务也要先调用工具读取。";
        instruction += toolsEnabled ? "工具包括search_apps、open_app，以及提供时的leo_action。leo_action可安排清单和提醒、查询足球数据、打开Leo界面、操作系统代理、管理收藏/简称/剪贴板收藏、搜索网页、调整Leo大小。只执行用户明确要求的操作，使用返回结果回答，操作失败要如实说明。没有提供的功能不要编造。仅在用户明确要求查找或打开软件时使用。先查找再使用返回的精确 ID 打开；候选有歧义时先问用户。根据工具返回结果如实报告，不伪造执行成功。没有文件读取、删除或终端执行能力。" : "当前未启用应用工具，没有应用启动、文件访问或终端执行能力，不要声称已执行本地操作。";
        var result = new List<ApiMessage> { new("system", instruction) };
        foreach (var turn in history.Where(x => x.Status == "完成").TakeLast(20)) { result.Add(new("user", turn.User)); result.Add(new("assistant", turn.Assistant)); }
        result.Add(new("user", prompt)); return result;
    }
    async Task SendAsync()
    {
        if(InvokeRequired){await (Task)Invoke(new Func<Task>(SendAsync));return;}
        if(SynchronizationContext.Current is not WindowsFormsSynchronizationContext)SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        if (pending != null || string.IsNullOrWhiteSpace(input.Text)) return;
        voice.Stop();
        string prompt = input.Text.Trim();
        if (prompt.Length > 100_000) { state.Text = "本条消息过长，请缩短后发送"; return; }
        var intent=QuickRouter.Route(prompt);
        if(intent.Kind is not (QuickKind.Ai or QuickKind.FootballAnalysis)){input.Clear();await HandleQuickAsync(intent,prompt);return;}
        if(preview&&testApi==null){state.Text="示例模式不发送 AI 请求";return;}
        string key=settings.ReadKey();
        if(key.Length==0||settings.Model.Length==0){state.Text="分析和聊天需要先配置模型";options.Visible=true;topPanel.Height=154;return;}
        ShowView(transcript);
        var tools = useTools.Checked ? appTools : null;
        var context = Context(turns, prompt, tools != null);
        var turn = new ChatTurn { User = prompt, Status = "生成中" }; turns.Add(turn); input.Clear();
        var request = new CancellationTokenSource(); pending = request;
        request.CancelAfter(TimeSpan.FromMinutes(3));
        send.Enabled = configure.Enabled = reset.Enabled = microphone.Enabled = useTools.Enabled = false; stop.Enabled = true; state.Text = "AI 回复中…"; Render();
        try
        {
            UpdateVoice();
            if(intent.Kind==QuickKind.FootballAnalysis){state.Text="正在查询比赛数据…";context.Insert(1,new ApiMessage("system",await AnalysisFacts(intent,request.Token)));}
            using var api = testApi?.Invoke() ?? new ChatApi();
            var reply = await ChatAgent.RunAsync(api, settings, key, context, delta => OnUi(() => {
                if (IsDisposed || request.IsCancellationRequested) return;
                turn.Assistant += delta; dirty = true; state.Text = "AI 回复中…";
            }), tools, detail => OnUi(() => {
                if (IsDisposed) return;
                state.Text = detail; turn.ToolLog.Add(detail); dirty = true;
            }), request.Token);
            turn.Assistant = reply.Text; turn.Status = reply.Truncated ? "回复受限" : "完成";
            lastAnswer=reply.Text;
            if (!preview && !reply.Truncated && autoSpeak.Checked && !IsDisposed) { try { voice.Speak(reply.Text); } catch { state.Text = "回复完成，但朗读不可用"; } }
            if (!IsDisposed) state.Text = reply.Truncated ? "回复达到长度或内容限制" : "AI · 回复完成";
        }
        catch (OperationCanceledException) { turn.Status = "已停止"; if (!IsDisposed) state.Text = "已停止生成"; }
        catch (Exception error)
        {
            turn.Status = "未完成";
            string message = error is System.Net.Http.HttpRequestException ? "连接失败，请检查中转站地址、网络与系统代理" : error.Message;
            message = message.Replace(key, "[Key 已隐藏]", StringComparison.Ordinal);
            if (!IsDisposed) state.Text = message.Length > 240 ? message[..240] : message;
        }
        finally
        {
            pending = null; request.Dispose();
            if (turns.Count > 40) turns.RemoveRange(0, turns.Count - 40);
            if (!IsDisposed) { send.Enabled = configure.Enabled = reset.Enabled = microphone.Enabled = useTools.Enabled = true; UpdateVoice(); dirty = false; Render(); SaveHistory(); input.Focus(); }
        }
    }
    void SaveOptions() { if (!preview) try { settings.Save(); } catch { state.Text = "选项保存失败"; } }
    void OnUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { if (InvokeRequired) BeginInvoke((Action)(() => { if (!IsDisposed) action(); })); else action(); } catch (InvalidOperationException) { }
    }
    void UpdateVoice()
    {
        if (IsDisposed) return;
        microphone.Text = voice.Finishing ? "转写中…" : voice.Listening ? "说完了" : "说话";
        microphone.Enabled = pending==null&&!voice.Finishing;
        inputDevice.Enabled=pending==null&&!voice.Listening&&!voice.Finishing;
        readAloud.Text = voice.Speaking ? "停朗读" : "朗读";
        stop.Enabled = pending != null || voice.Listening || voice.Finishing || voice.Speaking;
        stop.Visible=stop.Enabled;
        settingsButton.Enabled=pending==null;
    }
    void SaveHistory()
    {
        if (preview || !settings.RememberConversation) return;
        try { File.WriteAllText(HistoryPath, JsonSerializer.Serialize(turns.TakeLast(40))); }
        catch { state.Text = "本地对话保存失败，本次对话仍可继续"; }
    }
    void Render() { if (!IsDisposed) transcript.Display(turns); }
    public void PopulatePreview()
    {
        turns.Add(new() { User = "你好，Leo。", Assistant = "你好！我可以陪你聊天、解释问题，帮你构思和整理文字。", Status = "完成" });
        state.Text = "示例预览 · 未发送 API 请求"; Render();
    }
    public Task PreviewSendAsync(string text) { input.Text = text; return SendAsync(); }
    public bool PreviewReplyComplete => turns.LastOrDefault()?.Status == "完成" && transcript.Text.Contains("你好，Leo");
    public void PreviewStop() => stop.PerformClick();
    public bool PreviewWasStopped => turns.LastOrDefault()?.Status == "已停止" && send.Enabled && !stop.Enabled;
    protected override void OnFormClosed(FormClosedEventArgs e) { pending?.Cancel(); voice.Dispose(); renderTimer.Dispose(); base.OnFormClosed(e); }
}
