using System.Diagnostics;
namespace LeoLauncher;

sealed class LauncherForm : Form
{
    readonly TextBox query = new();
    int selection;bool keyboardNavigation;
    Action? undoFavorite;
    List<string>? pendingFavorites;Dictionary<string,string[]>? pendingAliases;
    readonly FlowLayoutPanel recent = new();
    readonly Label status = new();
    readonly Preferences prefs;
    readonly TodoStore tasks;
    readonly Action resizePet;
    readonly Action<AppEntry> launchApplication;
    readonly ClipboardHistoryService? clipboard;
    bool menuOpen, sorting;
    const string SortFormat = "LeoFavoriteTarget";
    readonly AppIcons icons = new();
    readonly Memory memory;
    readonly bool preview, demo;
    readonly System.Windows.Forms.Timer observer = new() { Interval = 1500 };
    readonly System.Windows.Forms.Timer debounce = new() { Interval = 140 };
    List<AppEntry> apps = new(), filtered = new();
    bool loading;
    FootballForm? football;
    MusicForm? music;
    public bool VerifyFootballEntry()
    {
        if (!Visible) Show();
        var button = Controls.OfType<GlyphButton>().Single(x => x.AccessibleName == "足球赛程");
        button.PerformClick(); Application.DoEvents();
        bool ok = football != null && football.Visible && football.Owner == null && !football.TopMost
            && (PetBounds == null || !football.Bounds.IntersectsWith(PetBounds!()));
        football?.Close(); return ok;
    }
    string lastExternal = "", lastObservedIdentity = "", activationContext = "";
    public bool IsIndexing => loading;
    public IEnumerable<AppEntry> FileApplications => apps;
    public bool HasTextInput => Visible && query.Focused && !string.IsNullOrWhiteSpace(query.Text);
    public void PreviewTyping(string text) { query.Text = text; query.Focus(); }
    public bool VerifyTwoRows()
    {
        query.Clear(); debounce.Stop(); Filter(); recent.PerformLayout();
        var tiles = recent.Controls.OfType<Button>().ToArray();
        return tiles.Length == 14 && tiles.Take(7).All(x => x.Top == tiles[0].Top)
            && tiles.Skip(7).All(x => x.Top == tiles[7].Top) && tiles[7].Top > tiles[0].Top;
    }
    public bool VerifyFavoriteDrop()
    {
        query.Clear(); debounce.Stop(); Filter();
        var entries = VisibleEntries().Take(2).ToArray();
        if (entries.Length != 2) return false;
        prefs.Favorites.RemoveAll(x => entries.Any(e => e.Target.Equals(x, StringComparison.OrdinalIgnoreCase)));
        prefs.Favorites.InsertRange(0, entries.Select(x => x.Target)); PopulateRecent();
        var destination = recent.Controls.OfType<Button>().ElementAt(1);
        var payload = new DataObject(); payload.SetData(SortFormat, entries[0].Target);
        var point = destination.PointToScreen(new Point(destination.Width - 2, 20));
        var args = new DragEventArgs(payload, 0, point.X, point.Y, DragDropEffects.Move, DragDropEffects.None);
        typeof(Control).GetMethod("OnDragEnter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(destination, new object[] { args });
        typeof(Control).GetMethod("OnDragDrop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(destination, new object[] { args });
        Application.DoEvents();
        return args.Effect == DragDropEffects.Move && prefs.Favorites[0] == entries[1].Target && prefs.Favorites[1] == entries[0].Target;
    }
    public Func<Rectangle>? PetBounds;
    public Func<bool>? PointerOverPet;
    public void ProcessDeactivation(bool pointerOverPet)
    {
        // A click on Leo is handled by its mouse-up toggle, not by focus loss.
        if (!menuOpen && !sorting && !pointerOverPet) Hide();
    }
    public bool PreviewFavorite()
    {
        var tile = recent.Controls.OfType<Button>().FirstOrDefault();
        if (tile?.Tag is not AppEntry entry) return false;
        prefs.Favorites.RemoveAll(x => x.Equals(entry.Target, StringComparison.OrdinalIgnoreCase));
        PopulateRecent();
        tile = recent.Controls.OfType<Button>().First();
        ((ToolStripMenuItem)tile.ContextMenuStrip!.Items[0]).PerformClick();
        Application.DoEvents();
        query.Clear(); debounce.Stop(); Filter();
        return VisibleEntries().Any(x => x.Target == entry.Target) && prefs.Favorites.Contains(entry.Target)
            && VisibleEntries().FirstOrDefault()?.Target == prefs.Favorites[0];
    }
    public bool PreviewResize()
    {
        var slider = Controls.OfType<MsnSlider>().Single();
        slider.Value = slider.Value == 160 ? 150 : 160;
        return prefs.PetSize == slider.Value;
    }
    public void PreviewSize(int size) => Controls.OfType<MsnSlider>().Single().Value = size;
    public bool PreviewClipboardSearch()
    {
        query.Text = "剪切板"; debounce.Stop(); Filter();
        return VisibleEntries().Any(x => x.Target == ClipboardTool.Target);
    }
    public LauncherForm(Preferences p, Action openSettings, bool previewMode, bool demoMode,TodoStore? planner=null,Action<AppEntry>? appLauncher=null)
    {
        launchApplication=appLauncher??Catalog.Launch;tasks=planner??new(previewMode?null:Path.Combine(Store.Root,"leo-todos.json"));prefs = p; resizePet = openSettings; preview = previewMode; demo = demoMode;
        memory = new Memory(demo ? null : Path.Combine(Store.Root, "leo-history.json"));
        Text = "Leo · 应用搜索"; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; ClientSize = new Size(600, 248);
        BackColor = Appearance.Background; ForeColor = Appearance.Ink; Font = Appearance.Body();
        Appearance.Round(this, 16);
        Controls.Add(new Label { Text = "LEO", Left = 22, Top = 9, Width = 43, Height = 22, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Appearance.Accent });
        var footballButton = new GlyphButton("football") { Bounds = new Rectangle(69, 7, 28, 24), AccessibleName = "足球赛程", FlatStyle = FlatStyle.Flat };
        footballButton.FlatAppearance.BorderSize = 0;
        footballButton.Click += (_, _) => {
            if (football == null || football.IsDisposed) football = new FootballForm(preview,tasks);
            SetWatchVisibility(football);
            var area = Screen.FromControl(this).WorkingArea;
            football.StartPosition = FormStartPosition.Manual;
            if(!football.Visible)WindowPositions.Prepare(football,"football",PetBounds?.Invoke() ?? Bounds,preview);
            Hide();
            football.Show(); football.Activate();
        };
        Controls.Add(footballButton);
        var size = new MsnSlider { Value = Math.Clamp(p.PetSize, 80, 230) };
        size.AutoSize = false; size.SetBounds(390, 6, 145, 28);
        size.AccessibleName = "Leo 大小";
        var sizeTip = new ToolTip(); sizeTip.SetToolTip(size, "拖动调整 Leo 大小");
        size.ValueChanged += (_, _) => { prefs.PetSize = size.Value; resizePet(); };
        size.MouseUp += (_, _) => { if (!preview) Store.Save(prefs); };
        size.KeyUp += (_, _) => { if (!preview) Store.Save(prefs); };
        Controls.Add(size);
        AddButton("×", 548, 6, 30, Hide);
        var field = new SoftPanel { Bounds = new Rectangle(16, 38, 568, 32) }; Appearance.Round(field, 11); Controls.Add(field);
        query.SetBounds(38, 5, 520, 24); query.PlaceholderText = "搜索应用或剪切板";
        query.BorderStyle = BorderStyle.None; query.Font = Appearance.Body(10.5f);
        query.BackColor = Appearance.Field; query.ForeColor = Appearance.Ink; field.Controls.Add(query);
        recent.SetBounds(18, 76, 564, 166); recent.WrapContents = true; Controls.Add(recent);
        icons.IconReady += () => {
            if (!IsDisposed && IsHandleCreated)
                try { BeginInvoke((Action)(() => {
                    if (IsDisposed) return;
                    foreach (var tile in recent.Controls.OfType<Button>())
                        tile.Invalidate();
                })); } catch (InvalidOperationException) { }
        };
        // Error messages only; no instructional footer.
        status.SetBounds(22, 248, 556, 24); status.ForeColor = Color.LightSalmon; status.Visible = false; Controls.Add(status);
        if (!preview) clipboard = new ClipboardHistoryService(prefs);
        debounce.Tick += (_, _) => { debounce.Stop(); Filter(); };
        query.TextChanged += (_, _) => { keyboardNavigation=false;debounce.Stop(); debounce.Start(); };
        query.KeyDown += (_, e) => {
            if (e.KeyCode is Keys.Down or Keys.Up or Keys.Left or Keys.Right)
            {
                if(e.KeyCode is Keys.Left or Keys.Right&&query.TextLength>0&&!keyboardNavigation)return;
                keyboardNavigation=true;
                debounce.Stop(); FilterIfChanged();
                var entries = VisibleEntries();
                selection = Math.Clamp(selection + (e.KeyCode == Keys.Down ? 7 : e.KeyCode == Keys.Up ? -7 : e.KeyCode == Keys.Right ? 1 : -1), 0, Math.Max(0, entries.Count - 1));
                PopulateRecent();
                e.SuppressKeyPress = true;
            }
            if (e.KeyCode == Keys.Enter) { debounce.Stop(); FilterIfChanged(); LaunchSelected(); e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.Escape) { Hide(); e.SuppressKeyPress = true; }
        };
        Deactivate += (_, _) => { if (!preview) ProcessDeactivation(PointerOverPet?.Invoke() == true); };
        observer.Tick += (_, _) => Observe();
        LoadCatalog();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var blue = new SolidBrush(Appearance.Blue); using var red = new SolidBrush(Appearance.Red);
        e.Graphics.FillRectangle(blue, 18, 0, 282, 3); e.Graphics.FillRectangle(red, 300, 0, Width - 318, 3);
    }
    string filteredQuery = "";
    void FilterIfChanged() { if (filteredQuery != query.Text) Filter(); }
    void AddButton(string text, int x, int y, int width, Action action)
    {
        var button = new Button { Text = text, Left = x, Top = y, Width = width, Height = 24, FlatStyle = FlatStyle.Flat, BackColor = Appearance.Background, ForeColor = Appearance.Muted, TabStop = false };
        button.FlatAppearance.BorderSize = 0; button.Click += (_, _) => action(); Controls.Add(button);
    }
    void LoadCatalog()
    {
        loading = true; status.Text = "正在读取本机应用…";
        // Create the handle before the STA worker posts its completion callback.
        _ = Handle;
        var thread = new Thread(() => {
            try
            {
                var scanned = Catalog.Scan();
                scanned.Add(ClipboardTool.Entry);
                if (!IsDisposed) BeginInvoke((Action)(() => {
                    apps = scanned; loading = false;
                    if (demo)
                    {
                        var sample = apps.Where(x => x.ExePath.Length > 0).Take(14).ToArray();
                        for (int i = 0; i < sample.Length; i++) memory.Record(sample[i], "preview", DateTimeOffset.UtcNow.AddMinutes(-i - 1), false);
                        if (sample.Length > 0 && !prefs.Favorites.Contains(sample[0].Target)) prefs.Favorites.Insert(0, sample[0].Target);
                    }
                    Observe(); Filter();
                    if (!preview) observer.Start();
                }));
            }
            catch (Exception e)
            {
                if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)(() => { loading = false; status.Text = "应用索引失败：" + e.Message; }));
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
    }
    void Observe()
    {
        if (!prefs.ContextAware || preview) return;
        var active = DesktopContext.Read();
        if (active == null) return;
        var identity = active.AppId.Length > 0 ? active.AppId : active.ExePath.Length > 0 ? active.ExePath : active.Process;
        if (identity.Equals(lastObservedIdentity, StringComparison.OrdinalIgnoreCase)) return;
        lastObservedIdentity = identity;
        var previous = lastExternal;
        var match = DesktopContext.Match(apps, active);
        lastExternal = match?.Name ?? active.Process;
        if (match != null && prefs.RememberHistory) memory.Record(match, previous, DateTimeOffset.UtcNow);
    }
    public void RefreshModules(){foreach(var icon in Controls.OfType<GlyphButton>())if(icon.AccessibleName=="QQ音乐点歌")icon.Visible=prefs.MusicModule;
        if(!prefs.MusicModule){music?.Close();music=null;}
        if(football!=null)SetWatchVisibility(football);
    }
    void SetWatchVisibility(Control root){foreach(Control c in root.Controls){if(c is WatchLinksPanel)c.Visible=prefs.EdgeModule;else SetWatchVisibility(c);}}
    public void Open()
    {
        RefreshModules();Observe(); activationContext = prefs.ContextAware ? lastExternal : "";
        query.Clear(); debounce.Stop(); Filter();
        Show(); Activate(); query.Focus();
    }
    void PopulateRecent()
    {
        foreach (Control control in recent.Controls.Cast<Control>().ToArray()) control.Dispose();
        recent.Controls.Clear();
        var entries = VisibleEntries();
        if (entries.Count == 0)
        {
            recent.Controls.Add(new Label { Text = !string.IsNullOrWhiteSpace(query.Text) ? "没有找到应用" : prefs.RememberHistory ? "搜索应用，开始使用" : "搜索应用", Width = 460, Height = 62, Padding = new Padding(5, 15, 0, 0), ForeColor = Appearance.Muted });
            return;
        }
        int tileIndex = 0;
        foreach (var app in entries)
        {
            bool selected = tileIndex++ == selection;
            var tile = new Button { Tag = app, AccessibleName = app.Name, Width = 74, Height = 76, Text = "",TabStop=false, FlatStyle = FlatStyle.Flat, Font = Appearance.Body(8.5f), Margin = new Padding(3), ForeColor = Appearance.Ink, BackColor = selected ? Appearance.Blue : Appearance.Background };
            Appearance.Round(tile, 12);
            tile.FlatAppearance.MouseOverBackColor = Appearance.Hover;
            tile.FlatAppearance.MouseDownBackColor = Appearance.Red;
            tile.Paint += (_, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.Clear(Appearance.Background);
                using var shape = Appearance.Path(tile.ClientRectangle, 12);
                using System.Drawing.Brush fill = selected ? new System.Drawing.Drawing2D.LinearGradientBrush(tile.ClientRectangle, Appearance.Blue, Appearance.Red, 25f) : new SolidBrush(tile.ClientRectangle.Contains(tile.PointToClient(Cursor.Position)) ? Appearance.Hover : tile.BackColor);
                e.Graphics.FillPath(fill, shape);
                e.Graphics.DrawImage(icons.Get(app), new Rectangle(21, 8, 32, 32));
                if (prefs.Favorites.Contains(app.Target, StringComparer.OrdinalIgnoreCase))
                    TextRenderer.DrawText(e.Graphics, "★", tile.Font, new Point(57, 0), Appearance.Accent);
                TextRenderer.DrawText(e.Graphics, app.Name, tile.Font, new Rectangle(3, 48, 68, 22), Appearance.Ink, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            var menu = new ContextMenuStrip();
            bool pinned = prefs.Favorites.Contains(app.Target, StringComparer.OrdinalIgnoreCase);
            menu.Items.Add(pinned ? "取消收藏置顶" : "收藏置顶", null, (_, _) => {
                RememberFavoriteUndo();
                if (pinned) prefs.Favorites.RemoveAll(x => x.Equals(app.Target, StringComparison.OrdinalIgnoreCase));
                else if (prefs.Favorites.Count < 14) prefs.Favorites.Add(app.Target);
                else MessageBox.Show("最多置顶 14 个图标，请先取消一个收藏。", "Leo");
                FinishFavoriteUndo();if (!preview) Store.Save(prefs);
                BeginInvoke((Action)(() => { PopulateRecent(); Activate(); query.Focus(); }));
            });
            menu.Items.Add("设置简称…", null, (_, _) => {
                Hide();
                using var editor = new AliasEditor(app, prefs.Aliases.GetValueOrDefault(app.Target) ?? Array.Empty<string>());
                if (editor.ShowDialog() == DialogResult.OK) {
                    RememberFavoriteUndo();
                    if (editor.Aliases.Length == 0) prefs.Aliases.Remove(app.Target); else prefs.Aliases[app.Target] = editor.Aliases;FinishFavoriteUndo();
                    if (!preview) Store.Save(prefs);
                }
                Open();
            });
            Point press = Point.Empty; bool suppressClick = false;
            tile.MouseDown += (_, e) => { press = e.Location; suppressClick = false; };
            tile.MouseMove += (_, e) => {
                if (e.Button != MouseButtons.Left || suppressClick || !pinned || !string.IsNullOrWhiteSpace(query.Text)) return;
                if (Math.Abs(e.X - press.X) + Math.Abs(e.Y - press.Y) < 8) return;
                suppressClick = true; sorting = true;
                try { var payload = new DataObject(); payload.SetData(SortFormat, app.Target); tile.DoDragDrop(payload, DragDropEffects.Move); }
                finally { sorting = false; }
            };
            tile.AllowDrop = pinned;
            tile.DragEnter += (_, e) => e.Effect = string.IsNullOrWhiteSpace(query.Text) && e.Data?.GetDataPresent(SortFormat) == true ? DragDropEffects.Move : DragDropEffects.None;
            tile.DragDrop += (_, e) => {
                if (!string.IsNullOrWhiteSpace(query.Text) || e.Data?.GetData(SortFormat) is not string source) return;
                bool after = tile.PointToClient(new Point(e.X, e.Y)).X >= tile.Width / 2;
                var beforeSort=prefs.Favorites.ToList();
                if (FavoriteSorting.Move(prefs.Favorites, source, app.Target, after)) {
                    var afterSort=prefs.Favorites.ToList();prefs.Favorites=beforeSort;RememberFavoriteUndo();prefs.Favorites=afterSort;FinishFavoriteUndo();
                    if (!preview) Store.Save(prefs);
                    BeginInvoke((Action)(() => { PopulateRecent(); Activate(); }));
                }
            };
            menu.Opening += (_, _) => menuOpen = true;
            menu.Closed += (_, _) => { menuOpen = false; Activate(); query.Focus(); };
            tile.ContextMenuStrip = menu;
            tile.Disposed += (_, _) => menu.Dispose();
            tile.FlatAppearance.BorderSize = 0; tile.Click += (_, _) => { if (!suppressClick) Launch(app); }; recent.Controls.Add(tile);
        }
    }
    void Filter()
    {
        filteredQuery = query.Text;
        var source = prefs.RememberHistory ? memory : new Memory(null);
        filtered = source.Search(apps, query.Text, activationContext, DateTimeOffset.UtcNow, prefs.ContextAware, prefs.Aliases);
        if (!string.IsNullOrWhiteSpace(query.Text) && ClipboardTool.Matches(query.Text) && !filtered.Any(x => x.Target == ClipboardTool.Target)) filtered.Insert(0, ClipboardTool.Entry);
        filtered = filtered.Take(14).ToList();
        selection = 0;
        PopulateRecent();
        status.Text = memory.LastSaveError != null ? "历史保存失败，本次记录仅保存在内存" : "";
        status.Visible = status.Text.Length > 0;
        ClientSize = new Size(600, status.Visible ? 276 : 248);
    }
    List<AppEntry> VisibleEntries() => string.IsNullOrWhiteSpace(query.Text)
        ? FavoriteOrder.Merge(apps, prefs.Favorites, prefs.RememberHistory ? memory.Recent(apps) : new List<AppEntry>())
        : filtered;

    void LaunchSelected()
    {
        var entries = VisibleEntries();
        if (selection >= 0 && selection < entries.Count) Launch(entries[selection]);
    }
    public void LaunchFromAssistant(AppEntry app)
    {
        launchApplication(app);
        if (prefs.RememberHistory) memory.Record(app, activationContext, DateTimeOffset.UtcNow);
    }
    public bool VerifyMusicEntry(){OpenMusicFromAssistant();Application.DoEvents();bool ok=music?.Visible==true&&music.Owner==null&&!music.TopMost;music?.Close();return ok;}
    public void ReloadImportedConfiguration(){football?.Close();football=null;RefreshModules();Filter();}
    public MusicForm MusicWithoutOpening(){Modules.Require(prefs,"music");if(music==null||music.IsDisposed)music=new MusicForm(preview);return music;}
    public MusicForm OpenMusicFromAssistant(string query=""){Modules.Require(prefs,"music");if(music==null||music.IsDisposed)music=new MusicForm(preview);if(!music.Visible)WindowPositions.Prepare(music,"music",PetBounds?.Invoke()??Bounds,preview);music.OpenQuery(query);return music;}
    public void OpenFootballFromAssistant(){var button=Controls.OfType<GlyphButton>().Single(x=>x.AccessibleName=="足球赛程");button.PerformClick();}
    public string PinClipboardText(string text){if(clipboard==null)throw new InvalidOperationException("剪贴板尚未就绪");if(string.IsNullOrWhiteSpace(text)||text.Length>100000)throw new ArgumentException("文字为空或过长");var item=new ClipItem("text",text,text.Replace('\n',' ')[..Math.Min(100,text.Length)],DateTimeOffset.Now);clipboard.History.Add(item);if(!clipboard.History.Pin(item,true))throw new InvalidOperationException("剪贴板收藏已达上限");if(clipboard.History.Error!=null)throw new IOException(clipboard.History.Error);return "已收藏这段文字到剪贴板。";}
    public void OpenClipboardFromAssistant()=>Launch(ClipboardTool.Entry);
    void Launch(AppEntry app)
    {
        Hide();
        try
        {
            if (app.Target == ClipboardTool.Target) ClipboardTool.Open(clipboard); else launchApplication(app);
            if (prefs.RememberHistory) memory.Record(app, activationContext, DateTimeOffset.UtcNow);
        }
        catch (Exception e) { MessageBox.Show("无法打开 " + app.Name + "：" + e.Message, "Leo"); }
    }
    void RememberFavoriteUndo(){pendingFavorites=prefs.Favorites.ToList();pendingAliases=prefs.Aliases.ToDictionary(p=>p.Key,p=>p.Value.ToArray());}
    string FavoriteSignature()=>System.Text.Json.JsonSerializer.Serialize(new{prefs.Favorites,prefs.Aliases});
    void FinishFavoriteUndo(){var saved=pendingFavorites;var aliases=pendingAliases;if(saved==null||aliases==null)return;string expected=FavoriteSignature();undoFavorite=()=>{if(FavoriteSignature()!=expected)throw new InvalidOperationException("收藏或简称已有新变化，未覆盖这些变化。");prefs.Favorites=saved;prefs.Aliases=aliases;if(!preview)Store.Save(prefs);PopulateRecent();undoFavorite=null;};pendingFavorites=null;pendingAliases=null;OperationFeedback.Undo("应用收藏 / 简称已调整",()=>undoFavorite?.Invoke());}
    protected override bool ProcessCmdKey(ref Message m,Keys key)
    {
        if(key==Keys.Escape){Hide();return true;}
        if(key==(Keys.Control|Keys.L)||key==(Keys.Control|Keys.F)){query.Focus();query.SelectAll();return true;}
        if(key==(Keys.Control|Keys.Z)&&query.TextLength==0&&undoFavorite!=null){try{undoFavorite();}catch(Exception e){OperationFeedback.Error(e.Message);}return true;}
        if(key==Keys.Tab||key==(Keys.Shift|Keys.Tab)){
            debounce.Stop();FilterIfChanged();int count=VisibleEntries().Count;if(count>0){selection=(selection+(key==Keys.Tab?1:count-1))%count;keyboardNavigation=true;PopulateRecent();}query.Focus();return true;
        }
        if(key==(Keys.Shift|Keys.F10)||key==Keys.Apps){var tile=recent.Controls.OfType<Button>().ElementAtOrDefault(selection);tile?.ContextMenuStrip?.Show(tile,new Point(0,tile.Height));return true;}
        if(key is Keys.Home or Keys.End&&keyboardNavigation){selection=key==Keys.Home?0:Math.Max(0,VisibleEntries().Count-1);PopulateRecent();return true;}
        return base.ProcessCmdKey(ref m,key);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { music?.Dispose();football?.Dispose(); observer.Dispose(); debounce.Dispose(); icons.Dispose(); clipboard?.Dispose(); }
        base.Dispose(disposing);
    }
}
