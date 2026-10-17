namespace LeoLauncher;

sealed record LeoNotice(string Text, bool Error = false, Func<Task>? Retry = null, bool Busy = false, string ActionLabel = "重试");
static class OperationFeedback
{
    public static Action<LeoNotice>? Sink;
    public static void Show(string text) => Sink?.Invoke(new(text));
    public static void Undo(string text,Action action)=>Sink?.Invoke(new(text,Retry:()=>{action();Show("已撤销");return Task.CompletedTask;},ActionLabel:"撤销"));
    public static void Pending(string text) => Sink?.Invoke(new(text, Busy: true));
    public static void Error(string text, Func<Task>? retry = null,string label="重试") => Sink?.Invoke(new(text, true, retry,ActionLabel:label));
}

sealed class LeoToast : Form
{
    readonly Label message = new() { Bounds = new Rectangle(14, 11, 290, 49), ForeColor = BarcaUi.Text, Font = BarcaUi.Font(9), AutoEllipsis = false };
    readonly BarcaButton retry = new() { Text = "重试", Bounds = new Rectangle(235, 66, 66, 27), ForeColor = BarcaUi.Gold };
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1900 };
    readonly Func<Rectangle> anchor;
    readonly BarcaButton close=new(){Text="×"};
    LeoNotice? current;
    protected override bool ShowWithoutActivation => true;
    public LeoToast(Func<Rectangle> pet)
    {
        anchor = pet; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual; ClientSize = new Size(320, 103); BackColor = BarcaUi.Surface;
        DoubleBuffered=true; Appearance.Round(this, 13); Controls.Add(message); Controls.Add(retry);
        close.SetBounds(237,6,22,22); close.Click += (_, _) => Hide(); Controls.Add(close);
        retry.Click += async (_, _) => { var action = current?.Retry; if (action == null) return; retry.Enabled = false; try { await action(); } catch (Exception e) { Present(new(e.Message, true, action)); } finally { if (!IsDisposed) retry.Enabled = true; } };
        timer.Tick += (_, _) => { if (!Bounds.Contains(Cursor.Position)) { timer.Stop(); Hide(); } };
    }
    public void Present(LeoNotice notice)
    {
        current = notice; message.Text = (notice.Error ? "未完成 · " : notice.Busy ? "处理中 · " : "✓ ") + notice.Text;
        message.ForeColor = notice.Error ? Color.LightSalmon : BarcaUi.Text;
        int width=message.Text.Length>90?320:270;
        int textWidth=width-54;
        int textHeight=Math.Max(25,TextRenderer.MeasureText(message.Text,message.Font,new Size(textWidth,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl).Height+6);
        ClientSize=new Size(width,Math.Max(52,14+textHeight+(notice.Retry!=null?39:10)));Appearance.Round(this,12);
        message.SetBounds(15,14,textWidth,textHeight);close.SetBounds(width-31,6,22,22);retry.SetBounds(width-76,Height-33,64,25);
        retry.Text=notice.ActionLabel;retry.Visible = notice.Retry != null; retry.Enabled = true;
        var pet = anchor(); Location = PopupPlacement.Place(pet, Size, Screen.FromRectangle(pet).WorkingArea);
        timer.Stop(); timer.Interval = notice.Error ? Math.Clamp(notice.Text.Length*80,5000,12000) : notice.Retry!=null?4500:1900; timer.Start();
        Show();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path=Appearance.Path(new Rectangle(1,1,Width-3,Height-3),12);
        using var border=new Pen(Color.FromArgb(82,67,82));e.Graphics.DrawPath(border,path);
        using var stripe=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(12,3,Width-24,4),Appearance.Blue,Appearance.Red,0f);e.Graphics.FillRectangle(stripe,12,3,Width-24,4);
    }
    public bool PreviewHasRetry => retry.Visible;
    internal bool PreviewTextFits=>message.Height>=message.GetPreferredSize(new Size(message.Width,0)).Height&&message.Bottom<(retry.Visible?retry.Top:ClientSize.Height)&&!message.Bounds.IntersectsWith(close.Bounds)&&ClientRectangle.Contains(retry.Visible?retry.Bounds:message.Bounds);
    public void Follow() { if(Visible){var pet=anchor();Location=PopupPlacement.Place(pet,Size,Screen.FromRectangle(pet).WorkingArea);} }
    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
}

sealed partial class PetForm
{
    LeoToast? feedback;
    void OnMonitorsChanged(){if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke(()=>{if(!IsDisposed){Location=Clamp(Location);PositionFoot();hoverCard?.Hide();feedback?.Follow();}});}catch(InvalidOperationException){}}
    void PresentFeedback(LeoNotice notice)
    {
        if (IsDisposed || Disposing || previewMode) return;
        if (InvokeRequired) { BeginInvoke(() => PresentFeedback(notice)); return; }
        if (hiddenForFullscreen) return;
        feedback ??= new LeoToast(() => Bounds);
        feedback.Present(notice);
    }
}
