using System.Diagnostics;
namespace LeoLauncher;

sealed partial class LauncherForm : Form
{
    readonly ModuleThemeBinding theme;
    internal Func<string,CancellationToken,Task<string>>? CommandRequested;
    internal Func<string,bool>? RecognizesCommand;
    internal Action? MediaRequested;
    internal Action<string>? ManageShortcut;
    internal Func<LocalApplication,LocalApplication>? RepairApplication;internal Action<string[]>? ApplicationsDropped;
    internal Action<ModuleWindowTarget>? WindowClosed;
    Action openFootball=null!;
    FlowLayoutPanel shortcutRow=null!;GlyphButton footballShortcut=null!;
    internal Func<string,string,CancellationToken,Task<string>>? ActionRequested;
    internal Func<string,string,string,CancellationToken,Task<string>>? OriginActionRequested;
    readonly TextBox query = new();
    int selection;bool keyboardNavigation;
    Action? undoFavorite;
    List<string>? pendingFavorites;Dictionary<string,string[]>? pendingAliases;
    readonly FlowLayoutPanel recent = new();
    readonly Label status = new();
    readonly Preferences prefs;
    readonly TodoStore tasks;
    internal FootballClient? SharedFootballClient;
    readonly Action resizePet;
    readonly Action<AppEntry> launchApplication;
    readonly ClipboardHistoryService clipboard;
    internal ClipboardHistoryService ClipboardService=>clipboard;
    internal MusicClient? SharedMusicClient;internal ClipboardClient? SharedClipboardClient;Form? clipboardWindow;
    bool menuOpen, sorting;
    const string SortFormat = "LeoFavoriteTarget";
    readonly AppIcons icons = new();
    readonly Memory memory;
    readonly bool preview, demo;
    readonly System.Windows.Forms.Timer observer = new() { Interval = 1500 };
    readonly System.Windows.Forms.Timer debounce = new() { Interval = 140 };
    List<AppEntry> apps = new(), filtered = new();
    readonly ActivityOwner catalogActivities=new("application-index");
    readonly Func<CancellationToken,List<AppEntry>> scanCatalog;
    readonly Action? observeOverride;readonly IPeriodicTask observationActivity;
    void CancelCatalog(){catalogGeneration++;catalogRestartRequested=false;catalogActivities.Reset();}
    bool loading;int catalogGeneration;bool catalogStarted,catalogWorkerRunning,catalogRestartRequested;
    FootballForm? football;
    MusicForm? music;
    public bool VerifyFootballEntry()
    {
        if (!Visible) Show();
        var button = shortcutRow.Controls.OfType<GlyphButton>().Single(x => x.AccessibleName == "足球赛程");
        button.PerformClick(); Application.DoEvents();
        bool ok = football != null && football.Visible && football.Owner == null && !football.TopMost
            && (PetBounds == null || !football.Bounds.IntersectsWith(PetBounds!()));
        football?.Close(); return ok;
    }
    string lastExternal = "", lastObservedIdentity = "", activationContext = "";
    public bool IsIndexing => loading;
    IEnumerable<AppEntry> AllApplications=>apps.Concat(EntryContributions.ReadAvailable(prefs).Where(a=>a.Enabled).Select(a=>a.SearchEntry)).Concat(EntryContributions.ReadModules(prefs).Search.Select(e=>new AppEntry(e.Name,e.Target,"模块入口"))).GroupBy(a=>a.Target,StringComparer.OrdinalIgnoreCase).Select(g=>g.Last());
    public IEnumerable<AppEntry> FileApplications => AllApplications.Where(a=>!ModuleEntryContributions.IsTarget(a.Target));
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
    public LauncherForm(Preferences p, Action openSettings, bool previewMode, bool demoMode,TodoStore? planner=null,Action<AppEntry>? appLauncher=null,Func<CancellationToken,List<AppEntry>>? scanner=null,Action? observationOverride=null)
    {
        scanCatalog=scanner??Catalog.Scan;observeOverride=observationOverride;
        observationActivity=new OwnedActivityTimer(observer,"application-context","应用上下文","context-check",t=>{t.ThrowIfCancellationRequested();ObserveCore();return Task.CompletedTask;});
        launchApplication=appLauncher??Catalog.Launch;tasks=planner??new(previewMode?null:Path.Combine(Store.Root,"leo-todos.json"));prefs = p; resizePet = openSettings; preview = previewMode; demo = demoMode;
        memory = new Memory(demo ? null : Path.Combine(Store.Root, "leo-history.json"));
        Text = "Leo · 应用搜索"; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; ClientSize = new Size(600, 248);
        BackColor = Appearance.Background; ForeColor = Appearance.Ink; Font = Appearance.Body();
        Appearance.Round(this, 16);
        var brand=new Label { Tag="pet-brand",Text = "LEO", Left = 22, Top = 9, Width = 43, Height = 22, AutoEllipsis=true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Appearance.Accent };Controls.Add(brand);
        var footballButton = new GlyphButton("football") { Bounds = new Rectangle(69, 7, 28, 24), AccessibleName = "足球赛程", FlatStyle = FlatStyle.Flat };
        footballButton.FlatAppearance.BorderSize = 0;
        openFootball = () => {
            if(!prefs.FootballModule)return;
            if (football == null || football.IsDisposed){football = new FootballForm(preview,tasks,sharedQueries:SharedFootballClient);football.FormClosed+=(_,_)=>WindowClosed?.Invoke(ModuleWindowTarget.Football);}
            SetWatchVisibility(football);
            var area = Screen.FromControl(this).WorkingArea;
            football.StartPosition = FormStartPosition.Manual;
            if(!football.Visible)WindowPositions.Prepare(football,"football",PetBounds?.Invoke() ?? Bounds,preview);
            Hide();
            WindowInteraction.Show(football);
        };
        footballButton.Click+=(_,_)=>{if(!ignoreShortcutClick&&!shortcutDragging)openFootball();};
        var shortcutViewport=new Panel{Bounds=new Rectangle(69,3,308,31)};shortcutRow=new FlowLayoutPanel{Bounds=new Rectangle(0,0,308,52),WrapContents=false,AutoScroll=true,FlowDirection=FlowDirection.LeftToRight};shortcutViewport.Controls.Add(shortcutRow);Controls.Add(shortcutViewport);footballShortcut=footballButton;
        InitializeShortcutBar(shortcutViewport);
        void BrandLayout(){brand.Width=Math.Clamp(TextRenderer.MeasureText(brand.Text,brand.Font).Width+6,43,190);shortcutViewport.Left=brand.Right+4;shortcutViewport.Width=Math.Max(120,377-shortcutViewport.Left);shortcutRow.Width=Math.Max(30,shortcutViewport.Width-33);LayoutShortcutOverflow();}brand.TextChanged+=(_,_)=>BrandLayout();BrandLayout();
        RefreshShortcuts();
        var size = new MsnSlider { Value = Math.Clamp(p.PetSize, 80, 230) };
        size.AutoSize = false; size.SetBounds(390, 6, 145, 28);
        size.AccessibleName = "Leo 大小";
        var sizeTip = new UiToolTip(); sizeTip.SetToolTip(size, "拖动调整 Leo 大小");
        size.ValueChanged += (_, _) => { prefs.PetSize = size.Value; resizePet(); };
        size.MouseUp += (_, _) => { if (!preview) Store.Save(prefs); };
        size.KeyUp += (_, _) => { if (!preview) Store.Save(prefs); };
        Controls.Add(size);
        AddButton("×", 548, 6, 30, Hide);
        var field = new SoftPanel { Bounds = new Rectangle(16, 38, 568, 32) }; Appearance.Round(field, 11); Controls.Add(field);
        query.SetBounds(38, 5, 520, 24); query.PlaceholderText = UiLanguage.T("搜索应用、文件路径或指令");
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
        clipboard = new ClipboardHistoryService(prefs,preview);clipboard.StateChanged+=()=>WindowClosed?.Invoke(ModuleWindowTarget.Clipboard);
        FeatureFaults.Timer(debounce,"应用搜索",()=>{debounce.Stop();Filter();});
        query.TextChanged += (_, _) => { keyboardNavigation=false;pathLookup?.Cancel();pathChoices.Items.Clear();debounce.Stop(); debounce.Start(); };
        InitializePathCompletion();
        query.KeyDown += (_, e) => {
            if(PathKey(e))return;
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
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress=true;var route=QuickRouter.Route(query.Text);if(route.Kind==QuickKind.SearchApp){query.Text=route.Value;Filter();return;}if(CommandRequested!=null&&CanRunCommand(query.Text)){_=RunCommand();return;}debounce.Stop(); FilterIfChanged(); LaunchSelected(); }
            if (e.KeyCode == Keys.Escape) { Hide(); e.SuppressKeyPress = true; }
        };
        Deactivate += (_, _) => { if (!preview) ProcessDeactivation(PointerOverPet?.Invoke() == true); };
        Disposed+=(_,_)=>{shortcutTips.Dispose();shortcutOverflowMenu.Dispose();if(!footballShortcut.IsDisposed)footballShortcut.Dispose();};
        ConfigureActivity();theme=new(this);
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
    void PostCatalog(Action action){if(launcherReleased||IsDisposed||!IsHandleCreated)return;try{BeginInvoke((Action)(()=>{if(!launcherReleased&&!IsDisposed)FeatureFaults.Run("应用索引结果",action);}));}catch(InvalidOperationException){}}
    void LoadCatalog()
    {
        if(launcherReleased||!prefs.ApplicationIndexing)return;
        catalogStarted=true;
        if(catalogWorkerRunning){catalogRestartRequested=true;return;}
        CancelCatalog();var pending=catalogActivities.Begin("scan");int generation=catalogGeneration;
        var token=pending.Token;catalogWorkerRunning=true;loading=true;status.Text="正在读取本机应用…";_=Handle;
        void Complete(List<AppEntry>? scanned,Exception? error)
        {
            catalogWorkerRunning=false;
            if(!launcherReleased&&generation==catalogGeneration&&prefs.ApplicationIndexing&&!token.IsCancellationRequested){
                loading=false;
                if(error!=null){status.Text="应用索引失败，请稍后重新开启索引。";FeatureFaults.Report("应用索引",error);}
                else if(scanned!=null){
                    apps=scanned;
                    if(demo){var sample=apps.Where(x=>x.ExePath.Length>0).Take(14).ToArray();for(int i=0;i<sample.Length;i++)memory.Record(sample[i],"preview",DateTimeOffset.UtcNow.AddMinutes(-i-1),false);if(sample.Length>0&&!prefs.Favorites.Contains(sample[0].Target))prefs.Favorites.Insert(0,sample[0].Target);}
                    Observe();Filter();
                }
            }
            if(catalogRestartRequested&&!launcherReleased&&prefs.ApplicationIndexing)LoadCatalog();
        }
        var thread=new Thread(()=>{
            List<AppEntry>? scanned=null;Exception? error=null;
            try{scanned=scanCatalog(token);token.ThrowIfCancellationRequested();scanned.Add(ClipboardTool.Entry);}
            catch(OperationCanceledException){}
            catch(Exception e)when(FeatureFaults.Recoverable(e)){error=e;}
            finally{pending.Dispose();}
            PostCatalog(()=>Complete(scanned,error));
        }){IsBackground=true};
        try{thread.SetApartmentState(ApartmentState.STA);thread.Start();}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){pending.Dispose();Complete(null,e);}
    }
    void Observe(){if(!launcherReleased)_=observationActivity.RunNowAsync();}
    void ObserveCore()
    {
        if(launcherReleased||!prefs.ApplicationIndexing||!prefs.ContextAware||preview&&observeOverride==null)return;
        if(observeOverride!=null){observeOverride();return;}
        var active=DesktopContext.Read();if(active==null)return;
        var identity=active.AppId.Length>0?active.AppId:active.ExePath.Length>0?active.ExePath:active.Process;
        if(identity.Equals(lastObservedIdentity,StringComparison.OrdinalIgnoreCase))return;
        lastObservedIdentity=identity;var previous=lastExternal;var match=DesktopContext.Match(apps,active);
        lastExternal=match?.Name??active.Process;
        if(match!=null&&prefs.RememberHistory)memory.Record(match,previous,DateTimeOffset.UtcNow);
    }
    public void ConfigureActivity()
    {
        if(launcherReleased)return;clipboard?.ConfigureActivity();
        observationActivity.Configure(prefs.ApplicationIndexing&&prefs.ContextAware&&(!preview||observeOverride!=null));
        if(!prefs.ApplicationIndexing){CancelCatalog();catalogStarted=false;loading=false;apps=[ClipboardTool.Entry];Filter();}
        else if(!catalogStarted)LoadCatalog();
    }

    public void RefreshModules(){RefreshShortcuts();ConfigureActivity();
        if(!prefs.FootballModule){football?.Close();football=null;}
        if(!prefs.MusicModule){music?.Dispose();music=null;}
        music?.ConfigureMedia(prefs.MediaModule&&EntryDisplay.ShowShortcut(prefs,"media"));if(football!=null)SetWatchVisibility(football);
        if(Visible&&(EntryContributions.ReadModules(prefs).Entries.Length>0||filtered.Any(a=>ModuleEntryContributions.IsTarget(a.Target))))Filter();
    }
    void SetWatchVisibility(Control root){foreach(Control c in root.Controls){if(c is WatchLinksPanel)c.Visible=prefs.EdgeModule;else SetWatchVisibility(c);}}
    public void Open()
    {
        ClientSize=new Size(600,248);status.Visible=false;status.Text="";RefreshModules();Observe(); activationContext = prefs.ContextAware ? lastExternal : "";
        query.Clear(); debounce.Stop(); Filter();
        Show(); Activate(); query.Focus();
    }
    void PopulateRecent()
    {
        foreach (Control control in recent.Controls.Cast<Control>().ToArray()) control.Dispose();
        recent.Controls.Clear();
        if(CommandRequested!=null&&CanRunCommand(query.Text)){var run=new BarcaButton{Text="执行指令："+query.Text.TrimStart(':','：'),Width=550,Height=65,AccessibleName="执行本地指令"};run.Click+=async(_,_)=>await RunCommand();recent.Controls.Add(run);return;}
        var entries = VisibleEntries();
        if (entries.Count == 0)
        {
            recent.Controls.Add(new Label { Text = !string.IsNullOrWhiteSpace(query.Text) ? "没有找到应用" : prefs.RememberHistory ? "搜索应用，开始使用" : "搜索应用", Width = 460, Height = 62, Padding = new Padding(5, 15, 0, 0), ForeColor = Appearance.Muted });
            return;
        }
        var moduleEntries=EntryContributions.ReadModules(prefs).Search.ToDictionary(e=>e.Target,StringComparer.OrdinalIgnoreCase);
        int tileIndex = 0;
        foreach (var app in entries)
        {
            bool selected = tileIndex++ == selection;
            var tile = new Button { Tag = app, AccessibleName = app.Name, Width = 74, Height = 76, Text = "",TabStop=false, FlatStyle = FlatStyle.Flat, Font = Appearance.Body(8.5f), Margin = new Padding(3), ForeColor = Appearance.Ink, BackColor = selected ? Appearance.Blue : Appearance.Background };
            var moduleIcon=moduleEntries.TryGetValue(app.Target,out var moduleEntry)?ModuleEntryRendering.Icon(moduleEntry):null;
            if(moduleIcon!=null)tile.Disposed+=(_,_)=>moduleIcon.Dispose();
            Appearance.Round(tile, 12);
            tile.FlatAppearance.MouseOverBackColor = Appearance.Hover;
            tile.FlatAppearance.MouseDownBackColor = Appearance.Red;
            tile.Paint += (_, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.Clear(Appearance.Background);
                using var shape = Appearance.Path(tile.ClientRectangle, 12);
                using System.Drawing.Brush fill = selected ? new System.Drawing.Drawing2D.LinearGradientBrush(tile.ClientRectangle, Appearance.Blue, Appearance.Red, 25f) : new SolidBrush(tile.ClientRectangle.Contains(tile.PointToClient(Cursor.Position)) ? Appearance.Hover : tile.BackColor);
                e.Graphics.FillPath(fill, shape);
                e.Graphics.DrawImage(moduleIcon??icons.Get(app), new Rectangle(21, 8, 32, 32));
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
                else UiDialogs.Show("最多置顶 14 个图标，请先取消一个收藏。", "Leo");
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
        filtered = source.Search(AllApplications.ToList(), query.Text, activationContext, DateTimeOffset.UtcNow, prefs.ContextAware, ModuleSearchAliases());
        if (!string.IsNullOrWhiteSpace(query.Text) && ClipboardTool.Matches(query.Text) && !filtered.Any(x => x.Target == ClipboardTool.Target)) filtered.Insert(0, ClipboardTool.Entry);
        filtered = filtered.Take(14).ToList();
        selection = 0;
        PopulateRecent();
        status.Text = memory.LastSaveError != null ? "历史保存失败，本次记录仅保存在内存" : "";
        status.Visible = status.Text.Length > 0;
        ClientSize = new Size(600, status.Visible ? 276 : 248);
        UpdatePathCompletion();
    }
    Dictionary<string,string[]> ModuleSearchAliases()
    {
        var entries=EntryContributions.ReadModules(prefs).Search;if(entries.Length==0)return prefs.Aliases;
        var aliases=new Dictionary<string,string[]>(prefs.Aliases,StringComparer.OrdinalIgnoreCase);
        foreach(var entry in entries)aliases[entry.Target]=(aliases.GetValueOrDefault(entry.Target)??[]).Concat(entry.Aliases).Distinct().ToArray();
        return aliases;
    }
    List<AppEntry> VisibleEntries() => string.IsNullOrWhiteSpace(query.Text)
        ? FavoriteOrder.Merge(AllApplications.ToList(), prefs.Favorites, prefs.RememberHistory ? memory.Recent(AllApplications.ToList()) : new List<AppEntry>())
        : filtered;

    bool CanRunCommand(string text)=>IsCommand(text)||RecognizesCommand?.Invoke(text.Trim())==true;
    internal static bool IsCommand(string text)=>LocalPath.LooksLike(text)||text.StartsWith(":")||text.StartsWith("：")||QuickRouter.Route(text).Kind is not(QuickKind.Ai or QuickKind.SearchApp)||NaturalTodo.Parse(text,DateTime.Now,DateOnly.FromDateTime(DateTime.Today))!=null;
    readonly CancellationTokenSource commandLife=new();bool commandRunning,launcherReleased;
    async Task RunCommand()
    {
        if(commandRunning||launcherReleased)return;
        commandRunning=true;var token=commandLife.Token;
        debounce.Stop();pathLookup?.Cancel();var text=query.Text.Trim().TrimStart(':','：').Trim();
        try{query.Enabled=false;var result=await CommandRequested!(text,token);if(!launcherReleased&&!IsDisposed&&!token.IsCancellationRequested){query.Clear();Hide();OperationFeedback.Show(result);}}
        catch(OperationCanceledException)when(token.IsCancellationRequested){}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){if(!launcherReleased&&!IsDisposed){status.Visible=true;status.Text=e.Message;ClientSize=new Size(600,280);}}
        finally{commandRunning=false;if(!launcherReleased&&!IsDisposed){query.Enabled=true;query.Focus();}}
    }

    void LaunchSelected()
    {
        var entries = VisibleEntries();
        if (selection >= 0 && selection < entries.Count) Launch(entries[selection]);
    }
    LocalApplication? ConfiguredApplication(AppEntry app)=>EntryContributions.ForTarget(prefs,app.Target);
    internal ActionRequest ResolveApplicationRequest(ActionRequest request)
    {
        AppEntry? entry=null;
        if(request.Action=="app_open")entry=FileApplications.Where(AppTools.Launchable).SingleOrDefault(a=>AppTools.Id(a)==request.Value);
        if(request.Action=="app_open_name"){
            var found=new AppTools(()=>FileApplications,LaunchFromAssistant,()=>prefs.Aliases).FindLocal(request.Value);
            var exact=found.Where(a=>Catalog.Normalize(a.Name)==Catalog.Normalize(request.Value)).ToArray();
            entry=exact.Length==1?exact[0]:found.Length==1?found[0]:null;
        }
        return entry!=null&&ConfiguredApplication(entry) is {} app?request with{Action="local:"+app.Id,Value=""}:request;
    }
    public async Task<string> LaunchFromAssistantAsync(AppEntry app,string source,CancellationToken token)
    {
        if(ModuleEntryContributions.IsTarget(app.Target))return (await EntryContributions.ExecuteModule(prefs,app.Target[ModuleEntryContributions.TargetPrefix.Length..],source,token)).ToLegacy(token);
        if(ConfiguredApplication(app) is {} module){
            if(OriginActionRequested!=null)return await OriginActionRequested("local:"+module.Id,"",source,token);
            if(ActionRequested!=null)return await ActionRequested("local:"+module.Id,"",token);
            if(preview)return "示例模式，不启动应用。";
            throw new InvalidOperationException("应用启动入口未连接，请从 Leo 打开。");
        }
        token.ThrowIfCancellationRequested();launchApplication(app);RememberApplication(app);return "已打开 "+app.Name;
    }
    public void LaunchFromAssistant(AppEntry app)
    {
        if(ModuleEntryContributions.IsTarget(app.Target)||ConfiguredApplication(app)!=null){_=LaunchConfigured();return;}
        launchApplication(app);RememberApplication(app);
        async Task LaunchConfigured(){try{await LaunchFromAssistantAsync(app,"ui",CancellationToken.None);}catch(OperationCanceledException){}catch(Exception e){OperationFeedback.Error(e.Message);}}
    }
    void RememberApplication(AppEntry app){if(prefs.RememberHistory)memory.Record(app,activationContext,DateTimeOffset.UtcNow);}
    internal void RememberConfiguredLaunch(LocalApplication app)
    {
        if(IsDisposed||Disposing)return;
        if(InvokeRequired){BeginInvoke((Action)(()=>RememberConfiguredLaunch(app)));return;}
        if(!(prefs.ApplicationModules??[]).Contains(app)||!app.Enabled)return;
        RememberApplication(new AppEntry(app.Name,app.Path,"自定义应用",app.Path));
    }
    public bool VerifyMusicEntry(){if(!Visible)Show();var icon=shortcutRow.Controls.OfType<GlyphButton>().Single(b=>b.AccessibleName=="QQ音乐点歌");var footballIcon=shortcutRow.Controls.OfType<GlyphButton>().Single(b=>b.AccessibleName=="足球赛程");icon.PerformClick();Application.DoEvents();bool ok=icon.Visible&&icon.Left>footballIcon.Right&&music?.Visible==true&&music.Owner==null&&!music.TopMost;music?.Close();return ok;}
    public void ReloadImportedConfiguration(){FootballFollowing.Current.Reload();football?.Close();football=null;RefreshModules();Filter();}
    public MusicForm MusicWithoutOpening(){Modules.Require(prefs,"music");if(music==null||music.IsDisposed){music=new MusicForm(preview,shared:SharedMusicClient){MediaRequested=()=>MediaRequested?.Invoke()};music.FormClosed+=(_,_)=>WindowClosed?.Invoke(ModuleWindowTarget.Music);music.VisibleChanged+=(sender,_)=>{if(sender is Form{Visible:false})WindowClosed?.Invoke(ModuleWindowTarget.Music);};}music.ConfigureMedia(prefs.MediaModule&&EntryDisplay.ShowShortcut(prefs,"media"));return music;}
    public MusicForm OpenMusicFromAssistant(string query=""){var form=MusicWithoutOpening();if(SharedMusicClient!=null){_ = OpenRegisteredMusic(query);}else OpenMusicCore(query);return form;}
    async Task OpenRegisteredMusic(string query){try{await SharedMusicClient!.Run("music_open",query);}catch(OperationCanceledException){}catch(Exception e){if(!IsDisposed)OperationFeedback.Error(e.Message);}}
    internal void OpenMusicCore(string query=""){var form=MusicWithoutOpening();if(!form.Visible)WindowPositions.Prepare(form,"music",PetBounds?.Invoke()??Bounds,preview);form.OpenQuery(query);}
    public void OpenFootballFromAssistant(){Modules.Require(prefs,"football");openFootball();}
    public string PinClipboardText(string text){if(SharedClipboardClient==null)throw new InvalidOperationException("剪贴板能力尚未就绪");SharedClipboardClient.Execute("clipboard_pin_text",text);return "已收藏这段文字到剪贴板。";}

    public void OpenClipboardFromAssistant(){if(SharedClipboardClient!=null){SharedClipboardClient.Execute("clipboard_open");return;}OpenClipboardCore();}
    internal void OpenClipboardCore(){Hide();if(clipboardWindow is {IsDisposed:false}){WindowInteraction.Show(clipboardWindow);return;}clipboardWindow=ClipboardTool.Open(clipboard,SharedClipboardClient);if(clipboardWindow!=null)clipboardWindow.FormClosed+=(_,_)=>WindowClosed?.Invoke(ModuleWindowTarget.Clipboard);if(prefs.RememberHistory)memory.Record(ClipboardTool.Entry,activationContext,DateTimeOffset.UtcNow);}
    async void Launch(AppEntry app)
    {
        Hide();
        try
        {
            if(ModuleEntryContributions.IsTarget(app.Target)){await ModuleEntryRendering.Run(prefs,app.Target[ModuleEntryContributions.TargetPrefix.Length..],"search",()=>!launcherReleased&&!IsDisposed,commandLife.Token);return;}
            if(ActionRequested!=null){await ActionRequested(app.Target==ClipboardTool.Target?"clipboard_open":"app_open",app.Target==ClipboardTool.Target?"":AppTools.Id(app),CancellationToken.None);return;}
            if (app.Target == ClipboardTool.Target) ClipboardTool.Open(clipboard); else launchApplication(app);
            if (prefs.RememberHistory) memory.Record(app, activationContext, DateTimeOffset.UtcNow);
        }
        catch(OperationCanceledException){}
        catch (Exception e) { UiDialogs.Show("无法打开 " + app.Name + "：" + e.Message, "Leo"); }
    }
    void RememberFavoriteUndo(){pendingFavorites=prefs.Favorites.ToList();pendingAliases=prefs.Aliases.ToDictionary(p=>p.Key,p=>p.Value.ToArray());}
    string FavoriteSignature()=>System.Text.Json.JsonSerializer.Serialize(new{prefs.Favorites,prefs.Aliases});
    void FinishFavoriteUndo(){var saved=pendingFavorites;var aliases=pendingAliases;if(saved==null||aliases==null)return;string expected=FavoriteSignature();undoFavorite=()=>{if(FavoriteSignature()!=expected)throw new InvalidOperationException("收藏或简称已有新变化，未覆盖这些变化。");prefs.Favorites=saved;prefs.Aliases=aliases;if(!preview)Store.Save(prefs);PopulateRecent();undoFavorite=null;};pendingFavorites=null;pendingAliases=null;OperationFeedback.Undo("应用收藏 / 简称已调整",()=>undoFavorite?.Invoke());}
    protected override bool ProcessCmdKey(ref Message m,Keys key)
    {
        if(key==Keys.Escape){Hide();return true;}
        if(key==(Keys.Control|Keys.L)||key==(Keys.Control|Keys.F)){query.Focus();query.SelectAll();return true;}
        if(key==(Keys.Control|Keys.Z)&&query.TextLength==0&&undoFavorite!=null){try{undoFavorite();}catch(Exception e){OperationFeedback.Error(e.Message);}return true;}
        if(key==Keys.Tab&&LocalPath.LooksLike(query.Text)){FillChosenPath();return true;}
        if(key==Keys.Tab||key==(Keys.Shift|Keys.Tab)){
            debounce.Stop();FilterIfChanged();int count=VisibleEntries().Count;if(count>0){selection=(selection+(key==Keys.Tab?1:count-1))%count;keyboardNavigation=true;PopulateRecent();}query.Focus();return true;
        }
        if(key==(Keys.Shift|Keys.F10)||key==Keys.Apps){var tile=recent.Controls.OfType<Button>().ElementAtOrDefault(selection);tile?.ContextMenuStrip?.Show(tile,new Point(0,tile.Height));return true;}
        if(key is Keys.Home or Keys.End&&keyboardNavigation){selection=key==Keys.Home?0:Math.Max(0,VisibleEntries().Count-1);PopulateRecent();return true;}
        return base.ProcessCmdKey(ref m,key);
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing&&!launcherReleased){
            launcherReleased=true;
            void Cleanup(Action action){try{action();}catch(Exception e)when(FeatureFaults.Recoverable(e)){System.Diagnostics.Trace.WriteLine("Leo launcher cleanup: "+e.GetType().Name);}}
            Cleanup(theme.Dispose);Cleanup(commandLife.Cancel);Cleanup(commandLife.Dispose);Cleanup(CancelCatalog);Cleanup(catalogActivities.Dispose);Cleanup(observationActivity.Dispose);
            Cleanup(()=>clipboardWindow?.Dispose());Cleanup(()=>music?.Dispose());Cleanup(()=>football?.Dispose());
            Cleanup(observer.Dispose);Cleanup(debounce.Dispose);Cleanup(icons.Dispose);Cleanup(()=>clipboard?.Dispose());
        }
        base.Dispose(disposing);
    }
}
