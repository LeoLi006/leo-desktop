using System.Diagnostics;
using System.ComponentModel;
namespace LeoLauncher;

sealed class FootTools : Form
{
    protected override bool ShowWithoutActivation => true;
    public const int Space = 64;
    float uiScale = 1f;
    readonly Button terminal;
    Font? scaledFont;
    int S(int value) => Math.Max(1, (int)Math.Round(value * uiScale));
    public int ScaledCompactWidth => S(CompactWidth);
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
    public bool VerifyDisclosure(){CollapseSearch();bool hidden=Controls.OfType<Button>().Count(b=>b.Visible)==3&&!terminal.Visible&&!proxyButton.Visible&&!chatButton.Visible;moreButton.PerformClick();bool opened=terminal.Visible&&proxyButton.Visible&&chatButton.Visible&&Height==S(58);moreButton.PerformClick();return hidden&&opened&&!terminal.Visible&&!proxyButton.Visible&&!chatButton.Visible&&Height==S(28);}
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
        moreButton.AccessibleName="展开更多工具";moreButton.FlatStyle=FlatStyle.Flat;moreButton.FlatAppearance.BorderSize=0;moreButton.Click+=(_,_)=>SetUtilities(!utilitiesOpen);tip.SetToolTip(moreButton,"终端 · Clash · 聊天");
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
            if (proxyBusy || IsDisposed) return;
            var state = preview ? new ProxyStatus(true, true, "Clash 系统代理已开启 · 示例") : ClashProxy.ReadStatus();
            proxyButton.ActiveState = state.Enabled;
            tip.SetToolTip(proxyButton, state.Description); proxyButton.Invalidate();
        }
        async Task ChangeProxy(bool? target=null) {
            if (preview || proxyBusy) return;
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
        Shown += (_, _) => { SetExpanded(false); RefreshProxy(); if (!preview) proxyTimer.Start(); };
        Deactivate += (_, _) => CollapseSearch();
        Disposed += (_, _) => { proxyTimer.Dispose(); tip.Dispose(); scaledFont?.Dispose(); };
    }
    void SetExpanded(bool value)
    {
        expanded = value;if(value)utilitiesOpen=false; search.Visible = value;
        LayoutTools();
        if (!workArea.IsEmpty) Follow(petBounds, workArea);
        Invalidate(true);
        if (value) { Activate(); search.Focus(); }
    }
    void SetUtilities(bool value){utilitiesOpen=value;if(value){expanded=false;search.Visible=false;}moreButton.ActiveState=value;LayoutTools();Invalidate(true);}
    public void ApplyScale(int petSize)
    {
        uiScale = Math.Clamp(petSize, 80, 230) / 130f;
        var previous = scaledFont; scaledFont = Appearance.Body(9 * uiScale); Font = scaledFont; previous?.Dispose();
        LayoutTools();
    }
    void LayoutTools()
    {
        SuspendLayout();
        moreButton.Bounds=new Rectangle(S(3),S(2),S(28),S(24));moreButton.ActiveState=utilitiesOpen;
        todoButton.Bounds=new Rectangle(S(33),S(2),S(28),S(24));searchButton.Bounds=new Rectangle(S(63),S(2),S(28),S(24));
        terminal.Bounds=new Rectangle(S(3),S(32),S(28),S(24));proxyButton.Bounds=new Rectangle(S(33),S(32),S(28),S(24));chatButton.Bounds=new Rectangle(S(63),S(32),S(28),S(24));
        terminal.Visible=proxyButton.Visible=chatButton.Visible=utilitiesOpen;
        search.AutoSize=false;search.Bounds=new Rectangle(S(97),S(6),S(154),S(21));
        ClientSize=new Size(S(expanded?262:CompactWidth),S(utilitiesOpen?58:28));
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
        bool compact = Width == ScaledCompactWidth && !search.Visible;
        searchButton.PerformClick();
        bool opened = Width == S(262) && search.Visible;
        searchButton.PerformClick();
        return compact && opened && Width == ScaledCompactWidth && !search.Visible;
    }
    public bool VerifyButtonOrder()=>moreButton.Left<todoButton.Left&&todoButton.Left<searchButton.Left&&terminal.Left<proxyButton.Left&&proxyButton.Left<chatButton.Left&&terminal.Top>moreButton.Top;
    public static ProcessStartInfo TerminalStart()
    {
        string terminal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
        if (!File.Exists(terminal)) terminal = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        return new ProcessStartInfo(terminal) { UseShellExecute = true, Verb = "runas", WorkingDirectory = Store.Root };
    }
    public static string SearchUrl(string text) => "https://www.bing.com/search?q=" + Uri.EscapeDataString(text.Trim());
    public static ProcessStartInfo EdgeStart(string text)=>EdgeUrlStart(SearchUrl(text));
    public static ProcessStartInfo EdgeUrlStart(string url)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var parsed)||parsed.Scheme is not ("http" or "https"))throw new ArgumentException("请填写 http 或 https 网址");
        var candidates = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) }
            .Select(root => Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe"));
        var edge = candidates.FirstOrDefault(File.Exists);
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
