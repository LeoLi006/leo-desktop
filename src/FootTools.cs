using System.Diagnostics;
using System.ComponentModel;
namespace LeoLauncher;

sealed class FootTools : Form
{
    protected override bool ShowWithoutActivation => true;
    public const int Space = 64;
    float uiScale = 1f;
    Preferences modules=new();
    int CompactUnits=>94;
    int UtilityUnits=>4+30*((modules.TerminalModule?1:0)+(modules.ClashModule?1:0)+(modules.EdgeModule?1:0));
    int VisibleCompactUnits=>Math.Max(CompactUnits,utilitiesOpen?UtilityUnits:0);
    public void ConfigureModules(Preferences p){modules=p;if(UtilityUnits==4)utilitiesOpen=false;if(!p.EdgeModule)expanded=false;if(!p.ClashModule)proxyTimer.Stop();else if(IsHandleCreated&&Visible)proxyTimer.Start();LayoutTools();}
    readonly Button terminal;
    Font? scaledFont;
    int S(int value) => Math.Max(1, (int)Math.Round(value * uiScale));
    public int ScaledCompactWidth => S(VisibleCompactUnits);
    public int Gap => S(4);
    public int ReservedSpace => S(64);
    public const int CompactWidth = 94;
    readonly TextBox search = new();
    readonly GlyphButton moreButton=new("more");
    bool utilitiesOpen;
    readonly Button searchButton = new GlyphButton("search");
    readonly GlyphButton proxyButton = new("proxy");
    readonly GlyphButton chatButton = new("chat");
    readonly GlyphButton todoButton = new("todo");
    public event Action? ChatRequested;
    public event Action? TodosRequested;
    public event Action? VoiceRequested;
    readonly System.Windows.Forms.Timer proxyTimer = new() { Interval = 1200 };
    bool proxyBusy;
    Rectangle petBounds, workArea;
    bool expanded;
    public bool HasTextInput => expanded && search.Focused && !string.IsNullOrWhiteSpace(search.Text);
    public void PreviewTyping(string text) { SetExpanded(true); search.Text = text; search.Focus(); }
    public void CollapseSearch(){SetExpanded(false);SetUtilities(false);}
    public void PreviewUtilities()=>SetUtilities(true);
    public bool VerifyDisclosure(){CollapseSearch();bool hidden=Controls.OfType<Button>().Count(b=>b.Visible)==3&&chatButton.Visible&&todoButton.Visible&&!terminal.Visible&&!proxyButton.Visible&&!searchButton.Visible;moreButton.PerformClick();bool opened=terminal.Visible&&proxyButton.Visible&&searchButton.Visible&&chatButton.Visible&&Height==S(58);moreButton.PerformClick();return hidden&&opened&&chatButton.Visible&&!terminal.Visible&&!searchButton.Visible&&Height==S(28);}
    public bool VerifyRepeatedRendering()
    {
        bool ok = true;
        for (int i = 0; i < 20; i++)
        {
            SetExpanded(i % 2 == 0);if(i%2==1)SetUtilities(true);
            foreach (var button in Controls.OfType<GlyphButton>().Where(b=>b.Visible))
            {
                using var image = new Bitmap(button.Width, button.Height);
                button.Invalidate(); button.Refresh(); button.DrawToBitmap(image, button.ClientRectangle);
                int ink = 0;
                for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++)
                { var color = image.GetPixel(x, y); if (Math.Abs(color.R - button.IconColor.R) + Math.Abs(color.G - button.IconColor.G) + Math.Abs(color.B - button.IconColor.B) < 100) ink++; }
                ok &= ink >= 10;
            }
        }
        CollapseSearch(); return ok;
    }
    public FootTools(int petSize = 130, bool preview = false)
    {
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; ClientSize = new Size(CompactWidth, 28);
        BackColor = Appearance.Background; ForeColor = Appearance.Ink;
        Appearance.Round(this, 13);
        Font = Appearance.Body(9);
        terminal = new GlyphButton("terminal") { AccessibleName = "打开管理员终端", FlatStyle = FlatStyle.Flat, Bounds = new Rectangle(3, 2, 28, 24), BackColor = Appearance.Background, ForeColor = Appearance.Accent };
        terminal.FlatAppearance.BorderSize = 0;
        terminal.FlatAppearance.MouseOverBackColor = Appearance.Hover;
        var tip = new ToolTip(); tip.SetToolTip(terminal, "管理员终端");
        moreButton.AccessibleName="展开更多工具";moreButton.FlatStyle=FlatStyle.Flat;moreButton.FlatAppearance.BorderSize=0;moreButton.Click+=(_,_)=>SetUtilities(!utilitiesOpen);tip.SetToolTip(moreButton,"可选工具 · 终端 / Clash / Edge");
        searchButton.SetBounds(33, 2, 28, 24); searchButton.FlatStyle = FlatStyle.Flat;
        searchButton.FlatAppearance.BorderSize = 0; searchButton.BackColor = Appearance.Background;
        searchButton.FlatAppearance.MouseOverBackColor = Appearance.Hover;
        searchButton.AccessibleName = "展开 Edge 搜索";
        searchButton.Click += (_, _) => SetExpanded(!expanded);
        tip.SetToolTip(searchButton, "Edge 搜索");
        chatButton.AccessibleName = "问 Leo"; chatButton.FlatStyle = FlatStyle.Flat; chatButton.FlatAppearance.BorderSize = 0;
        chatButton.Click += (_, _) => ChatRequested?.Invoke(); tip.SetToolTip(chatButton, "问 Leo · 右键直接说话 · Ctrl+Alt+V");
        chatButton.MouseUp+=(_,e)=>{if(e.Button==MouseButtons.Right)VoiceRequested?.Invoke();};
        todoButton.AccessibleName="每日清单";todoButton.FlatStyle=FlatStyle.Flat;todoButton.FlatAppearance.BorderSize=0;todoButton.Click+=(_,_)=>TodosRequested?.Invoke();tip.SetToolTip(todoButton,"每日清单");
        proxyButton.AccessibleName = "Clash Verge 系统代理";
        proxyButton.FlatStyle = FlatStyle.Flat; proxyButton.FlatAppearance.BorderSize = 0;
        void RefreshProxy()
        {
            if (proxyBusy || IsDisposed || !modules.ClashModule) return;
            var state = preview ? new ProxyStatus(true, true, "Clash 系统代理已开启 · 示例") : ClashProxy.ReadStatus();
            proxyButton.ActiveState = state.Enabled;
            tip.SetToolTip(proxyButton, state.Description); proxyButton.Invalidate();
        }
        async Task ChangeProxy(bool? target=null) {
            if (preview || proxyBusy || !modules.ClashModule) return;
            proxyBusy = true; proxyButton.Enabled = false; tip.SetToolTip(proxyButton, "正在切换 Clash 系统代理…");
            bool desired=target??(ClashProxy.ReadStatus().Enabled!=true);
            OperationFeedback.Pending(desired?"正在开启系统代理…":"正在关闭系统代理…");
            try { await Task.Run(()=>ClashProxy.SetEnabled(desired));OperationFeedback.Show(desired?"系统代理已开启":"系统代理已关闭"); }
            catch (Exception error) { if (!IsDisposed) OperationFeedback.Error(error.Message,()=>ChangeProxy(desired)); }
            finally { if (!IsDisposed) { proxyBusy = false; proxyButton.Enabled = true; RefreshProxy(); } }
        }
        proxyButton.Click+=async(_,_)=>await ChangeProxy();
        proxyTimer.Tick += (_, _) => RefreshProxy();
        search.SetBounds(67, 6, 154, 21); search.PlaceholderText = "输入关键词，回车搜索";
        search.BorderStyle = BorderStyle.None; search.BackColor = Appearance.Background; search.ForeColor = Appearance.Ink;
        search.Visible = false;
        terminal.Click += (_, _) => {
            try { Process.Start(TerminalStart()); }
            catch (Win32Exception e) when (e.NativeErrorCode == 1223) { }
            catch (Exception e) { MessageBox.Show("无法打开管理员终端：" + e.Message, "Leo"); }
        };
        search.KeyDown += (_, e) => {
            if (e.KeyCode == Keys.Escape) { SetExpanded(false); e.SuppressKeyPress = true; return; }
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            if (string.IsNullOrWhiteSpace(search.Text)) return;
            try { Process.Start(EdgeStart(search.Text)); search.Clear(); SetExpanded(false); }
            catch (Exception error) { MessageBox.Show("无法打开 Edge：" + error.Message, "Leo"); }
        };
        Controls.AddRange([moreButton,terminal, searchButton, proxyButton, chatButton, todoButton, search]);
        ApplyScale(petSize);
        Shown += (_, _) => { SetExpanded(false); RefreshProxy(); if (!preview&&modules.ClashModule) proxyTimer.Start(); };
        Deactivate += (_, _) => CollapseSearch();
        Disposed += (_, _) => { proxyTimer.Dispose(); tip.Dispose(); scaledFont?.Dispose(); };
    }
    void SetExpanded(bool value)
    {
        value=value&&modules.EdgeModule;expanded = value;if(value)utilitiesOpen=true; search.Visible = value;
        LayoutTools();
        if (!workArea.IsEmpty) Follow(petBounds, workArea);
        Invalidate(true);
        if (value) { Activate(); search.Focus(); }
    }
    void SetUtilities(bool value){if(value&&UtilityUnits==4){OperationFeedback.Show("下拉区暂无工具，可在设置中开启终端、Clash 或 Edge。");value=false;}utilitiesOpen=value;if(value){expanded=false;search.Visible=false;}moreButton.ActiveState=value;LayoutTools();Invalidate(true);}
    public void ApplyScale(int petSize)
    {
        uiScale = Math.Clamp(petSize, 80, 230) / 130f;
        var previous = scaledFont; scaledFont = Appearance.Body(9 * uiScale); Font = scaledFont; previous?.Dispose();
        LayoutTools();
    }
    void LayoutTools()
    {
        SuspendLayout();
        chatButton.Bounds=new Rectangle(S(3),S(2),S(28),S(24));todoButton.Bounds=new Rectangle(S(33),S(2),S(28),S(24));moreButton.Bounds=new Rectangle(S(63),S(2),S(28),S(24));moreButton.ActiveState=utilitiesOpen;
        chatButton.Visible=todoButton.Visible=moreButton.Visible=true;
        int toolX=3;foreach(var (button,enabled) in new[]{(terminal,modules.TerminalModule),((Button)proxyButton,modules.ClashModule),(searchButton,modules.EdgeModule)}){
            button.Visible=utilitiesOpen&&enabled;if(enabled){button.Bounds=new Rectangle(S(toolX),S(32),S(28),S(24));toolX+=30;}
        }
        search.Visible=expanded&&modules.EdgeModule;search.AutoSize=false;search.Bounds=new Rectangle(S(VisibleCompactUnits+3),S(36),S(154),S(21));
        ClientSize=new Size(S(expanded?VisibleCompactUnits+168:VisibleCompactUnits),S(utilitiesOpen?58:28));
        ResumeLayout();
        if (!workArea.IsEmpty) Follow(petBounds, workArea);
        Invalidate(true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var blue = new SolidBrush(Appearance.Blue); using var red = new SolidBrush(Appearance.Red);
        e.Graphics.FillRectangle(blue, S(12), Height - S(2), Width / 2 - S(12), S(2));
        e.Graphics.FillRectangle(red, Width / 2, Height - S(2), Width / 2 - S(12), S(2));
    }
    public bool VerifySearchToggle()
    {
        CollapseSearch();moreButton.PerformClick();bool before=!search.Visible;searchButton.PerformClick();bool opened=search.Visible&&Width==S(VisibleCompactUnits+168)&&Height==S(58);searchButton.PerformClick();bool closed=!search.Visible;CollapseSearch();return before&&opened&&closed;
    }
    public bool VerifyButtonOrder()=>chatButton.Top==todoButton.Top&&todoButton.Top==moreButton.Top&&chatButton.Left<todoButton.Left&&todoButton.Left<moreButton.Left&&terminal.Top>chatButton.Top&&terminal.Left<proxyButton.Left&&proxyButton.Left<searchButton.Left;
    public static ProcessStartInfo TerminalStart()
    {
        string terminal = ModulePaths.Selected("terminal")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
        if (!File.Exists(terminal)) terminal = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        return new ProcessStartInfo(terminal) { UseShellExecute = true, Verb = "runas", WorkingDirectory = Store.Root };
    }
    public static string SearchUrl(string text) => "https://www.bing.com/search?q=" + Uri.EscapeDataString(text.Trim());
    public static ProcessStartInfo EdgeStart(string text)=>EdgeUrlStart(SearchUrl(text));
    public static void ValidateUrl(string url){if(!Uri.TryCreate(url,UriKind.Absolute,out var parsed)||parsed.Scheme is not ("http" or "https")||parsed.UserInfo.Length>0)throw new ArgumentException("请填写不带账号密码的 http 或 https 网址");}
    public static ProcessStartInfo EdgeUrlStart(string url)
    {
        ValidateUrl(url);
        var candidates = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) }
            .Select(root => Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe"));
        var edge = ModulePaths.Selected("edge")??candidates.FirstOrDefault(File.Exists);
        var start = new ProcessStartInfo(edge ?? "microsoft-edge:" + url) { UseShellExecute = true };
        if (edge != null) start.ArgumentList.Add(url);
        return start;
    }
    public void Follow(Rectangle pet, Rectangle area)
    {
        petBounds = pet; workArea = area;
        Location = new Point(Math.Clamp(pet.Left + (pet.Width - Width) / 2, area.Left, Math.Max(area.Left, area.Right - Width)), pet.Bottom + Gap);
    }
}
