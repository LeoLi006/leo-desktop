using System.Drawing.Drawing2D;
namespace LeoLauncher;

static class Appearance
{
    public static Color Background => UiTheme.Color(16, 27, 49);
    public static Color Ink => UiTheme.Color(235, 239, 248);
    public static Color Muted => UiTheme.Color(157, 174, 200);
    public static Color Field => UiTheme.Color(28, 44, 70);
    public static Color Accent => UiTheme.Color(224, 191, 118);
    public static Color Blue => UiTheme.Color(37, 75, 132);
    public static Color Red => UiTheme.Color(130, 34, 67);
    public static Color Hover => UiTheme.Color(36, 53, 82);
    public static Font Body(float size = 9.5f) => new("Microsoft YaHei UI", size, FontStyle.Regular);
    public static GraphicsPath Path(Rectangle bounds, int radius)
    {
        radius=Math.Clamp((int)Math.Round(radius*UiTheme.Options.CornerRadius/14d),0,Math.Min(bounds.Width,bounds.Height)/2);var path = new GraphicsPath();if(radius==0){path.AddRectangle(bounds);return path;}int d = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control,RoundState> rounds=new();
    internal static void RefreshRound(Control control){if(rounds.TryGetValue(control,out var state))state.Apply();}
    sealed class RoundState
    {
        readonly Control control;public int Radius;
        public RoundState(Control c){control=c;c.SizeChanged+=(_,_)=>Apply();}
        public void Apply(){if(control.IsDisposed||control.Width<2||control.Height<2)return;using var path=Appearance.Path(control.ClientRectangle,Math.Min(Radius,Math.Min(control.Width,control.Height)/2));var old=control.Region;control.Region=new Region(path);old?.Dispose();}
    }
    public static void Round(Control control,int radius){var state=rounds.GetValue(control,c=>new RoundState(c));state.Radius=radius;state.Apply();}

}

sealed class SoftPanel : Panel
{
    public SoftPanel() { DoubleBuffered = true; BackColor = Appearance.Field; }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Appearance.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Appearance.Path(ClientRectangle, 11);
        using var brush = new SolidBrush(BackColor); e.Graphics.FillPath(brush, path);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(ContainsFocus ? Appearance.Accent : Appearance.Muted, 1.5f);
        e.Graphics.DrawEllipse(pen, 13, 10, 10, 10); e.Graphics.DrawLine(pen, 22, 19, 27, 24);
        if (ContainsFocus) { using var line = new Pen(Appearance.Accent, 1); e.Graphics.DrawLine(line, 33, Height - 2, Width - 15, Height - 2); }
    }
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control != null) { e.Control.GotFocus += (_, _) => Invalidate(); e.Control.LostFocus += (_, _) => Invalidate(); }
    }
}

sealed class GlyphButton : Button
{
    readonly string glyph;
    bool hovered;
    public bool? ActiveState;
    public Color IconColor => glyph == "proxy" && ActiveState != true ? Appearance.Muted : Appearance.Accent;
    public GlyphButton(string kind)
    {
        glyph = kind;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = true;
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Appearance.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (hovered || Focused)
        {
            using var shape = Appearance.Path(ClientRectangle, Math.Min(9, Height / 2));
            using var brush = new LinearGradientBrush(ClientRectangle, Appearance.Blue, Appearance.Red, 20f); e.Graphics.FillPath(brush, shape);
        }
        e.Graphics.ScaleTransform(Width / 28f, Height / 24f);
        using var pen = new Pen(IconColor, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        if (glyph == "chat")
        {
            e.Graphics.DrawRoundedRectangle(pen, new RectangleF(5, 4, 18, 13), new SizeF(4, 4));
            e.Graphics.DrawLines(pen, new[] { new PointF(9, 17), new PointF(8, 21), new PointF(14, 17) });
            e.Graphics.DrawLine(pen, 9, 9, 19, 9); e.Graphics.DrawLine(pen, 9, 13, 16, 13);
        }
        else if(glyph=="more"){e.Graphics.DrawLines(pen,ActiveState==true?new[]{new PointF(8,14),new PointF(14,8),new PointF(20,14)}:new[]{new PointF(8,9),new PointF(14,15),new PointF(20,9)});}
        else if(glyph=="todo"){e.Graphics.DrawRectangle(pen,6,3,16,18);for(int y=7;y<=17;y+=5){e.Graphics.DrawLines(pen,new[]{new PointF(9,y),new PointF(10,y+1),new PointF(12,y-1)});e.Graphics.DrawLine(pen,15,y,19,y);}}
        else if(glyph=="music"){e.Graphics.DrawLines(pen,new[]{new PointF(11,17),new PointF(11,5),new PointF(21,3),new PointF(21,15)});e.Graphics.DrawLine(pen,11,9,21,7);e.Graphics.DrawEllipse(pen,6,15,5,4);e.Graphics.DrawEllipse(pen,16,13,5,4);}
        else if (glyph == "football")
        {
            e.Graphics.DrawEllipse(pen, 5, 3, 18, 18);
            PointF[] pentagon = Enumerable.Range(0, 5).Select(i => { double a = -Math.PI / 2 + i * Math.PI * 2 / 5; return new PointF(14 + 4 * (float)Math.Cos(a), 12 + 4 * (float)Math.Sin(a)); }).ToArray();
            e.Graphics.DrawPolygon(pen, pentagon);
            foreach (var p in pentagon) e.Graphics.DrawLine(pen, p, new PointF(14 + (p.X - 14) * 2.2f, 12 + (p.Y - 12) * 2.2f));
        }
        else if (glyph == "proxy")
        {
            e.Graphics.DrawArc(pen, 7, 5, 14, 14, -45, 270);
            e.Graphics.DrawLine(pen, 14, 3, 14, 12);
        }
        else if (glyph == "search")
        {
            e.Graphics.DrawEllipse(pen, 7, 5, 9, 9); e.Graphics.DrawLine(pen, 15, 13, 20, 18);
        }
        else
        {
            e.Graphics.DrawLines(pen, new[] { new PointF(7, 7), new PointF(11, 11), new PointF(7, 15) });
            e.Graphics.DrawLine(pen, 14, 16, 21, 16);
        }
    }
}

sealed class MsnSlider : Control
{
    int current = 130;
    public event EventHandler? ValueChanged;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Value { get => current; set { int next = Math.Clamp(value, 80, 230); if (next == current) return; current = next; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); } }
    public MsnSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.Slider; BackColor = Appearance.Background;
    }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
    void SetFromPointer(int x) => Value = 80 + (int)Math.Round(Math.Clamp((x - 8f) / Math.Max(1, Width - 16), 0, 1) * 150);
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Focus(); Capture = true; SetFromPointer(e.X); } base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (Capture && e.Button == MouseButtons.Left) SetFromPointer(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { SetFromPointer(e.X); Capture = false; } base.OnMouseUp(e); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Left or Keys.Down) Value -= 5;
        else if (e.KeyCode is Keys.Right or Keys.Up) Value += 5;
        else if (e.KeyCode == Keys.Home) Value = 80;
        else if (e.KeyCode == Keys.End) Value = 230;
        base.OnKeyDown(e);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var track = new LinearGradientBrush(new Rectangle(8, 0, Width - 16, Height), Appearance.Blue, Appearance.Red, 0f);
        using var pen = new Pen(track, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawLine(pen, 8, Height / 2f, Width - 8, Height / 2f);
        float x = 8 + (Width - 16) * (Value - 80) / 150f;
        using var gold = new SolidBrush(Appearance.Accent); e.Graphics.FillEllipse(gold, x - 5, Height / 2f - 5, 10, 10);
        if (Focused) { using var ring = new Pen(Appearance.Accent); e.Graphics.DrawEllipse(ring, x - 8, Height / 2f - 8, 16, 16); }
    }
}
