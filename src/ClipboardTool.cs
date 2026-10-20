using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace LeoLauncher;

sealed record ClipItem(string Kind, string Content, string Label, DateTimeOffset Time, bool Pinned = false)
{
    public string Key => Kind + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Content)));
    public override string ToString() => (Pinned ? "★  " : Kind == "image" ? "▧  " : Kind == "files" ? "▤  " : "≡  ") + Label;
}

sealed class ClipHistory
{
    readonly string? file;readonly Stack<List<ClipItem>> undo=new();bool blocked;
    public bool CanUndo=>undo.Count>0;
    public List<ClipItem> Items{get;private set;}=[];
    public string? Error{get;private set;}
    public ClipHistory(string? path){file=path;try{if(path!=null&&(File.Exists(path)||File.Exists(path+".bak")))Items=AtomicJson.Read(path,text=>Trim(JsonSerializer.Deserialize<List<ClipItem>>(text)??throw new IOException("剪贴板数据为空")));}catch{blocked=true;Error="剪贴板文件无法读取，原文件已保留；请恢复备份。";}}
    static List<ClipItem> Trim(IEnumerable<ClipItem> data){var valid=data.Where(x=>x!=null&&x.Content!=null&&x.Label!=null&&x.Content.Length<=4_000_000&&x.Kind is "text" or "image" or "files").DistinctBy(x=>x.Key).ToList();var history=valid.Where(x=>!x.Pinned).Take(50).ToList();while(history.Sum(x=>(long)x.Content.Length)>12_000_000)history.RemoveAt(history.Count-1);return valid.Where(x=>x.Pinned).Concat(history).ToList();}
    bool Commit(IEnumerable<ClipItem> data,IEnumerable<ClipItem>? touched=null){if(blocked)return false;var next=Trim(data);try{if(file!=null)AtomicJson.Write(file,JsonSerializer.Serialize(next));if(touched!=null){var frame=touched.ToList();if(frame.Count>0){undo.Push(frame);if(undo.Count>20){var keep=undo.Take(20).Reverse().ToArray();undo.Clear();foreach(var item in keep)undo.Push(item);}}}Items=next;Error=null;return true;}catch(Exception e)when(e is IOException or UnauthorizedAccessException){Error="剪贴板保存失败，原文件和当前记录均保留。";return false;}}
    public void Add(ClipItem item){var old=Items.FirstOrDefault(x=>x.Key==item.Key);if(old?.Pinned==true)item=item with{Pinned=true};Commit(Items.Where(x=>x.Key!=item.Key).Prepend(item));}
    public bool Pin(ClipItem item,bool value){var old=Items.FirstOrDefault(x=>x.Key==item.Key);if(old==null)return false;if(value&&!old.Pinned&&(Items.Count(x=>x.Pinned)>=50||Items.Where(x=>x.Pinned).Sum(x=>(long)x.Content.Length)+old.Content.Length>12_000_000))return false;return Commit(Items.Select(x=>x.Key==item.Key?x with{Pinned=value}:x),[old]);}
    public bool AddPinned(ClipItem item){var old=Items.FirstOrDefault(x=>x.Key==item.Key);if(old?.Pinned!=true&&(Items.Count(x=>x.Pinned)>=50||Items.Where(x=>x.Pinned).Sum(x=>(long)x.Content.Length)+item.Content.Length>12_000_000))return false;return Commit(Items.Where(x=>x.Key!=item.Key).Prepend(item with{Pinned=true}),[old??item]);}
    public void Delete(ClipItem item)=>Commit(Items.Where(x=>x.Key!=item.Key),Items.Where(x=>x.Key==item.Key));
    public void Clear()=>Commit(Items.Where(x=>x.Pinned),Items.Where(x=>!x.Pinned));
    public void Undo(){if(undo.Count==0)return;var frame=undo.Peek();var keys=frame.Select(x=>x.Key).ToHashSet();if(Commit(frame.Concat(Items.Where(x=>!keys.Contains(x.Key)))))undo.Pop();}
    public void Expire(int days,DateTimeOffset now){if(days<=0)return;var next=Items.Where(x=>x.Pinned||x.Time>=now.AddDays(-days)).ToList();if(next.Count!=Items.Count&&Commit(next))undo.Clear();}
    public List<ClipItem> Search(string query)=>Items.Where(x=>x.Label.Contains(query,StringComparison.OrdinalIgnoreCase)||x.Kind!="image"&&x.Content.Contains(query,StringComparison.OrdinalIgnoreCase)).ToList();
}

sealed class ClipboardHistoryService : IDisposable
{
    [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 450 };
    readonly Preferences prefs;
    readonly bool preview;
    uint lastSequence;DateTimeOffset lastExpiry;bool disposed;readonly OwnedActivityTimer monitor;
    public void RefreshPrivacy(){if(disposed)return;if(prefs.ClipboardHistory)History.Expire(prefs.ClipboardExpireDays,DateTimeOffset.Now);lastSequence=GetClipboardSequenceNumber();}
    public void OpenPrivacy(IWin32Window? owner=null){ObjectDisposedException.ThrowIf(disposed,this);using var form=new ClipboardPrivacyForm(prefs,RefreshPrivacy,preview);form.ShowDialog(owner);}
    public ClipHistory History { get; }
    public ClipboardHistoryService(Preferences p, bool previewMode = false,string? historyPath=null,Action? captureOverride=null)
    {
        prefs = p; preview = previewMode;
        History = new(historyPath??(preview ? null : Path.Combine(Store.Root, "leo-clipboard.json")));
        History.Expire(p.ClipboardExpireDays,DateTimeOffset.Now);
        monitor=new(timer,"clipboard-monitor","剪贴板监听","capture",t=>{t.ThrowIfCancellationRequested();if(captureOverride!=null)captureOverride();else CaptureCore();return Task.CompletedTask;});
        simulatedCapture=captureOverride!=null;
        ClipboardPrivacy.Changed+=RefreshPrivacy;
        ConfigureActivity();
    }
    readonly bool simulatedCapture;
    public void ConfigureActivity(){if(disposed)return;lastSequence=GetClipboardSequenceNumber();monitor.Configure((!preview||simulatedCapture)&&prefs.ClipboardHistory);}
    public bool Enabled { get => prefs.ClipboardHistory; set { ObjectDisposedException.ThrowIf(disposed,this);var next=LocalApplications.Clone(prefs);next.ClipboardHistory=value;if(!preview)ConfigMigration.Write(Store.FilePath,JsonSerializer.Serialize(next));prefs.ClipboardHistory=value;ConfigureActivity(); } }
    internal Preferences Preferences=>prefs;
    internal bool IsClosed=>disposed;
    public void Capture(){if(!disposed)_=monitor.RunNowAsync();}
    void CaptureCore()
    {
        if(disposed||preview||!prefs.ClipboardHistory)return;
        if(DateTimeOffset.Now-lastExpiry>TimeSpan.FromSeconds(30)){History.Expire(prefs.ClipboardExpireDays,DateTimeOffset.Now);lastExpiry=DateTimeOffset.Now;}
        uint sequence = GetClipboardSequenceNumber();
        if(!Enabled||ClipboardPrivacy.Paused(prefs,DateTimeOffset.Now)){lastSequence=sequence;return;}
        if (sequence == lastSequence) return;
        if(ClipboardPrivacy.ShouldSkip(prefs,DateTimeOffset.Now,ClipboardPrivacy.OwnerApp(),DesktopContext.Read()?.Process)){lastSequence=sequence;return;}
        try
        {
            var formats = Clipboard.GetDataObject();
            // Respect Windows' exclusion marker used by password managers and sensitive apps.
            if (ClipboardPrivacy.MarkedPrivate(formats)) { lastSequence = sequence; return; }
            ClipItem? item = null;
            if (Clipboard.ContainsFileDropList())
            {
                string[] files = Clipboard.GetFileDropList().Cast<string>().ToArray();
                item = new("files", JsonSerializer.Serialize(files), string.Join(" · ", files.Select(Path.GetFileName)), DateTimeOffset.Now);
            }
            else if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                if (prefs.ClipboardBlockSecrets&&ClipboardPrivacy.Sensitive(text)){lastSequence=sequence;return;}
                if (text.Length > 0 && text.Length <= 100_000) item = new("text", text, text.Replace('\r', ' ').Replace('\n', ' ')[..Math.Min(100, text.Length)], DateTimeOffset.Now);
            }
            else if (Clipboard.ContainsImage())
            {
                using var image = Clipboard.GetImage();
                if (image != null && (long)image.Width * image.Height <= 16_000_000)
                {
                    using var stream = new MemoryStream(); image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    if (stream.Length <= 3_000_000) item = new("image", Convert.ToBase64String(stream.ToArray()), $"图片 {image.Width} × {image.Height}", DateTimeOffset.Now);
                }
            }
            if (item != null) History.Add(item);
            lastSequence = sequence;
        }
        catch (ExternalException) { /* Retry on next tick if clipboard is temporarily busy. */ }
        catch (Exception e) when (e is ArgumentException or OutOfMemoryException or ThreadStateException) { lastSequence = sequence; }
    }
    public void Copy(ClipItem item)
    {
        ObjectDisposedException.ThrowIf(disposed,this);if(preview)return;
        if (item.Kind == "text") Clipboard.SetText(item.Content);
        else if (item.Kind == "files")
        {
            var paths = JsonSerializer.Deserialize<string[]>(item.Content) ?? Array.Empty<string>();
            var collection = new System.Collections.Specialized.StringCollection(); collection.AddRange(paths); Clipboard.SetFileDropList(collection);
        }
        else
        {
            using var stream = new MemoryStream(Convert.FromBase64String(item.Content));
            using var image = Image.FromStream(stream); Clipboard.SetImage(image);
        }
        lastSequence = GetClipboardSequenceNumber(); if(Enabled&&!ClipboardPrivacy.Paused(prefs,DateTimeOffset.Now)&&!(prefs.ClipboardBlockSecrets&&item.Kind=="text"&&ClipboardPrivacy.Sensitive(item.Content)))History.Add(item with { Time = DateTimeOffset.Now });
    }
    public void Dispose(){if(disposed)return;disposed=true;ClipboardPrivacy.Changed-=RefreshPrivacy;monitor.Dispose();}
}

static class ClipboardTool
{
    public const string Target = "leo-tool:clipboard";
    public static AppEntry Entry => new("剪切板", Target, "内置工具");
    public static bool Matches(string query)
    {
        var text = Catalog.Normalize(query);
        return new[] { "剪切板", "剪贴板", "clipboard" }.Any(x => Catalog.Normalize(x).Contains(text));
    }
    public static Form? Open(ClipboardHistoryService? service,ClipboardClient? shared=null)
    {
        if (service == null) return null;
        service.Capture();
        var client=shared??ClipboardClient.Standalone(service,service.Preferences,()=>{});
        var window = new Form { Name="clipboard",FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false, Text = "Leo · 剪切板历史", StartPosition = FormStartPosition.CenterScreen, ClientSize = new Size(740, 470), MinimumSize = new Size(640, 400), Font = new Font("Microsoft YaHei UI", 9.5f), BackColor = UiTheme.Color(24, 29, 44), ForeColor = Color.White };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 112, Padding = new Padding(8), WrapContents = true };
        var search = new TextBox { Width = 190, PlaceholderText = "搜索剪切板历史", BackColor = Appearance.Field, ForeColor = Appearance.Ink, BorderStyle = BorderStyle.FixedSingle };
        var enabled = new CheckBox { Text = "记录历史", Checked = service.Enabled, AutoSize = true, Padding = new Padding(0, 4, 0, 0) };
        var copy = new Button { Text = "复制", Width = 65, Height = 28 };
        var delete = new Button { Text = "删除", Width = 65, Height = 28 };
        var undoButton=new Button{Text="撤销",Width=65,Height=28};
        var clear = new Button { Text = "清空历史", Width = 85, Height = 28 };
        var pin = new Button { Text = "收藏", Width = 80, Height = 28 };
        var onlyPins = new CheckBox { Text = "只看收藏", AutoSize = true, Padding = new Padding(0, 4, 0, 0) };
        foreach (var button in new[] { copy, delete, clear, pin,undoButton }) { button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0; button.BackColor = Appearance.Field; }
        var privacy=new BarcaButton{Text="隐私保护…",Width=105,Height=28};privacy.Click+=(_,_)=>service.OpenPrivacy(window);
        bar.Controls.AddRange([search, enabled, copy, pin, delete, clear,undoButton, onlyPins,privacy]);
        var split = new SplitContainer { Dock = DockStyle.Fill, BackColor = UiTheme.Color(32, 39, 55) };
        var list = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = UiTheme.Color(32, 39, 55), ForeColor = Color.White, IntegralHeight = false, ItemHeight = 30 };
        var text = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, BackColor = UiTheme.Color(40, 47, 65), ForeColor = Color.White, BorderStyle = BorderStyle.None };
        var picture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Visible = false };
        var state = new Label { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(10, 5, 0, 0), ForeColor = Color.LightSteelBlue };
        split.Panel1.Controls.Add(list); split.Panel2.Controls.Add(text); split.Panel2.Controls.Add(picture);
        window.Controls.Add(split); window.Controls.Add(state); window.Controls.Add(bar);
        void RefreshItems()
        {
            string? selected = (list.SelectedItem as ClipItem)?.Key;
            list.BeginUpdate(); list.Items.Clear();
            foreach (var item in client.Search(search.Text).Where(x => !onlyPins.Checked || x.Pinned)) list.Items.Add(item);
            list.SelectedIndex = list.Items.Cast<ClipItem>().ToList().FindIndex(x => x.Key == selected);
            if (list.SelectedIndex < 0 && list.Items.Count > 0) list.SelectedIndex = 0;
            list.EndUpdate();
            if (list.Items.Count == 0) { text.Text = "暂无记录"; picture.Visible = false; text.Visible = true; }
            undoButton.Enabled=service.History.CanUndo;
            state.Text = service.History.Error ?? $"历史 {service.History.Items.Count(x => !x.Pinned)}/50 · 收藏 {service.History.Items.Count(x => x.Pinned)}/50 · 清空历史保留收藏";
        }
        list.SelectedIndexChanged += (_, _) => {
            picture.Image?.Dispose(); picture.Image = null;
            if (list.SelectedItem is not ClipItem item) return;
            pin.Text = item.Pinned ? "取消收藏" : "收藏";
            picture.Visible = item.Kind == "image"; text.Visible = !picture.Visible;
            if (picture.Visible)
            {
                try { using var stream = new MemoryStream(Convert.FromBase64String(item.Content)); using var image = Image.FromStream(stream); picture.Image = new Bitmap(image); }
                catch { picture.Visible = false; text.Visible = true; text.Text = "图片无法读取"; }
            }
            else try{text.Text = item.Kind == "files" ? string.Join(Environment.NewLine, JsonSerializer.Deserialize<string[]>(item.Content) ?? Array.Empty<string>()) : item.Content;}catch(JsonException){text.Text="文件列表无法读取，原记录已保留。";}
        };
        bool Apply(string action,string value=""){try{client.Execute(action,value);RefreshItems();return true;}catch(Exception e){state.Text=e.Message;return false;}}
        void CopySelected()
        {
            if (list.SelectedItem is not ClipItem item) return;
            try { if(Apply("clipboard_copy",item.Key))state.Text = service.History.Error??"已复制到剪切板"; }
            catch (Exception e) { state.Text = "复制失败：" + e.Message; }
        }
        copy.Click += (_, _) => CopySelected();SelectionKeys.Wire(list,CopySelected);
        delete.Click += (_, _) => { if (list.SelectedItem is ClipItem item) Apply("clipboard_delete",item.Key); };
        undoButton.Click+=(_,_)=>{Apply("clipboard_undo");};
        clear.Click += (_, _) => { Apply("clipboard_clear"); };
        pin.Click += (_, _) => {
            if (list.SelectedItem is not ClipItem item) return;
            try{client.Execute("clipboard_pin",item.Key);}catch(Exception e){state.Text=e.Message;return;}
            RefreshItems();
        };
        onlyPins.CheckedChanged += (_, _) => RefreshItems();
        bool recordingUi=false;enabled.CheckedChanged += (_, _) => {if(recordingUi)return;try{client.Execute("clipboard_recording",enabled.Checked?"true":"false");}catch(Exception e){recordingUi=true;enabled.Checked=service.Enabled;recordingUi=false;state.Text=e.Message;}};
        search.TextChanged += (_, _) => RefreshItems();
        var refresh = new System.Windows.Forms.Timer { Interval = 900 };
        string signature = "";
        FeatureFaults.Timer(refresh,"剪贴板窗口刷新",()=>{ if(window.IsDisposed||window.Disposing)return;string current = string.Join("|", service.History.Items.Select(x => x.Key + x.Pinned)); if (current != signature) { signature = current; RefreshItems(); } });
        bool released=false;void Release(){if(released)return;released=true;refresh.Stop();refresh.Dispose();picture.Image?.Dispose();if(shared==null)client.Dispose();}
        window.FormClosed += (_, _) => Release();window.Disposed+=(_,_)=>Release();
        window.Shown += (_, _) => {if(!FeatureFaults.IsTestMode){FeatureFaults.Run("剪贴板窗口",()=>{WindowInteraction.Prepare(window);WindowInteraction.FocusInput(window);});} split.SplitterDistance = 280; RefreshItems(); refresh.Start(); };
        window.Show();
        return window;
    }
}
