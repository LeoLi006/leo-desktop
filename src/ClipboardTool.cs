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
    readonly string? file;
    readonly Stack<List<ClipItem>> undo=new();
    public bool CanUndo=>undo.Count>0;
    void Remember(IEnumerable<ClipItem> touched){var data=touched.ToList();if(data.Count>0){undo.Push(data);if(undo.Count>20){var keep=undo.Take(20).Reverse().ToArray();undo.Clear();foreach(var frame in keep)undo.Push(frame);}}}
    public void Undo(){if(undo.Count==0)return;var restore=undo.Peek();var keys=restore.Select(x=>x.Key).ToHashSet();Items=restore.Concat(Items.Where(x=>!keys.Contains(x.Key))).ToList();Trim();Save();if(Error==null)undo.Pop();}

    public List<ClipItem> Items { get; private set; } = new();
    public string? Error { get; private set; }
    public ClipHistory(string? path)
    {
        file = path;
        try { if (path != null) Items = JsonSerializer.Deserialize<List<ClipItem>>(File.ReadAllText(path)) ?? new(); }
        catch { Items = new(); }
        Trim();
    }
    void Trim()
    {
        var valid = Items.Where(x => x != null && x.Content != null && x.Content.Length <= 4_000_000 && x.Kind is "text" or "image" or "files").DistinctBy(x => x.Key).ToList();
        var pins = valid.Where(x => x.Pinned).ToList();
        var history = valid.Where(x => !x.Pinned).Take(50).ToList();
        while (history.Sum(x => (long)x.Content.Length) > 12_000_000) history.RemoveAt(history.Count - 1);
        Items = pins.Concat(history).ToList();
    }
    public void Add(ClipItem item)
    {
        var previous = Items.FirstOrDefault(x => x.Key == item.Key);
        if (previous?.Pinned == true) item = item with { Pinned = true };
        Items.RemoveAll(x => x.Key == item.Key); Items.Insert(0, item); Trim(); Save();
    }
    public bool Pin(ClipItem item, bool value)
    {
        int index = Items.FindIndex(x => x.Key == item.Key);
        if (index < 0) return false;
        if (value && !Items[index].Pinned && (Items.Count(x => x.Pinned) >= 50 || Items.Where(x => x.Pinned).Sum(x => (long)x.Content.Length) + item.Content.Length > 12_000_000)) return false;
        Remember([Items[index]]);Items[index] = Items[index] with { Pinned = value }; Trim(); Save(); return true;
    }
    public void Delete(ClipItem item) { Remember(Items.Where(x=>x.Key==item.Key));Items.RemoveAll(x => x.Key == item.Key); Save(); }
    public void Clear() { Remember(Items.Where(x=>!x.Pinned));Items.RemoveAll(x => !x.Pinned); Save(); }
    public void Expire(int days,DateTimeOffset now){if(days<=0)return;int removed=Items.RemoveAll(i=>!i.Pinned&&i.Time<now.AddDays(-days));if(removed>0){undo.Clear();Save();}}
    public List<ClipItem> Search(string query) => Items.Where(x => x.Label.Contains(query, StringComparison.OrdinalIgnoreCase) || (x.Kind != "image" && x.Content.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
    void Save()
    {
        if (file == null) return;
        try { File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(Items)); File.Move(file + ".tmp", file, true); Error = null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Error = "历史保存失败，本次记录仅保存在内存"; }
    }
}

sealed class ClipboardHistoryService : IDisposable
{
    [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 450 };
    readonly Preferences prefs;
    readonly bool preview;
    uint lastSequence;DateTimeOffset lastExpiry;
    public void RefreshPrivacy(){History.Expire(prefs.ClipboardExpireDays,DateTimeOffset.Now);lastSequence=GetClipboardSequenceNumber();}
    public void OpenPrivacy(IWin32Window? owner=null){using var form=new ClipboardPrivacyForm(prefs,RefreshPrivacy,preview);form.ShowDialog(owner);}
    public ClipHistory History { get; }
    public ClipboardHistoryService(Preferences p, bool previewMode = false)
    {
        prefs = p; preview = previewMode;
        History = new(preview ? null : Path.Combine(Store.Root, "leo-clipboard.json"));
        History.Expire(p.ClipboardExpireDays,DateTimeOffset.Now);
        timer.Tick += (_, _) => Capture();
        ClipboardPrivacy.Changed+=RefreshPrivacy;
        ConfigureActivity();
    }
    public void ConfigureActivity(){lastSequence=GetClipboardSequenceNumber();if(!preview&&prefs.ClipboardHistory)timer.Start();else timer.Stop();}
    public bool Enabled { get => prefs.ClipboardHistory; set { prefs.ClipboardHistory = value; ConfigureActivity(); Store.Save(prefs); } }
    public void Capture()
    {
        if (preview) return;
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
    public void Dispose(){ClipboardPrivacy.Changed-=RefreshPrivacy;timer.Dispose();}
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
    public static Form? Open(ClipboardHistoryService? service)
    {
        if (service == null) return null;
        service.Capture();
        var window = new Form { FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false, Text = "Leo · 剪切板历史", StartPosition = FormStartPosition.CenterScreen, ClientSize = new Size(740, 470), MinimumSize = new Size(640, 400), Font = new Font("Microsoft YaHei UI", 9.5f), BackColor = UiTheme.Color(24, 29, 44), ForeColor = Color.White };
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
            foreach (var item in service.History.Search(search.Text).Where(x => !onlyPins.Checked || x.Pinned)) list.Items.Add(item);
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
            else text.Text = item.Kind == "files" ? string.Join(Environment.NewLine, JsonSerializer.Deserialize<string[]>(item.Content) ?? Array.Empty<string>()) : item.Content;
        };
        void CopySelected()
        {
            if (list.SelectedItem is not ClipItem item) return;
            try { service.Copy(item); RefreshItems(); state.Text = "已复制到剪切板"; }
            catch (Exception e) { state.Text = "复制失败：" + e.Message; }
        }
        copy.Click += (_, _) => CopySelected(); list.DoubleClick += (_, _) => CopySelected();
        list.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { CopySelected(); e.SuppressKeyPress = true; } };
        delete.Click += (_, _) => { if (list.SelectedItem is ClipItem item) service.History.Delete(item); RefreshItems(); };
        undoButton.Click+=(_,_)=>{service.History.Undo();RefreshItems();};
        clear.Click += (_, _) => { service.History.Clear(); RefreshItems(); };
        pin.Click += (_, _) => {
            if (list.SelectedItem is not ClipItem item) return;
            if (!service.History.Pin(item, !item.Pinned)) { state.Text = "收藏已达上限（50 条或约 1200 万字符），请先移除部分收藏"; return; }
            RefreshItems();
        };
        onlyPins.CheckedChanged += (_, _) => RefreshItems();
        enabled.CheckedChanged += (_, _) => service.Enabled = enabled.Checked;
        search.TextChanged += (_, _) => RefreshItems();
        var refresh = new System.Windows.Forms.Timer { Interval = 900 };
        string signature = "";
        refresh.Tick += (_, _) => { string current = string.Join("|", service.History.Items.Select(x => x.Key + x.Pinned)); if (current != signature) { signature = current; RefreshItems(); } };
        window.FormClosed += (_, _) => { refresh.Dispose(); picture.Image?.Dispose(); };
        window.Shown += (_, _) => { split.SplitterDistance = 280; RefreshItems(); refresh.Start(); };
        window.Show();
        return window;
    }
}
