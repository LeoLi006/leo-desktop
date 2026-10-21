namespace LeoLauncher;

sealed record LeoNotice(string Text, bool Error = false, Func<Task>? Retry = null, bool Busy = false, string ActionLabel = "重试",string? Module=null,string? OperationId=null,bool Attention=false);
static class FeedbackPolicy{public static bool ShouldPopup(LeoNotice notice)=>notice.Error||notice.Attention||notice.Retry!=null;}
static class OperationFeedback
{
    public static Action<LeoNotice>? Sink;
    public static void Show(string text) => Sink?.Invoke(new(text));
    public static void Result(string text,string module,string operationId)=>Sink?.Invoke(new(text,Module:module,OperationId:operationId));
    public static void Undo(string text,Action action)=>Sink?.Invoke(new(text,Retry:()=>{action();Show("已撤销");return Task.CompletedTask;},ActionLabel:"撤销"));
    public static void Pending(string text) => Sink?.Invoke(new(text, Busy: true));
    public static void Error(string text, Func<Task>? retry = null,string label="重试",string? module=null,string? operationId=null) => Sink?.Invoke(new(text, true, retry,ActionLabel:label,Module:module,OperationId:operationId));
}

sealed class LeoToast : Form
{
    readonly Font noticeFont = BarcaUi.Font(9);
    const TextFormatFlags NoticeFlags=TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPrefix;
    Rectangle messageBounds;
    string messageText="";
    readonly BarcaButton retry = new() { Text = "重试", Bounds = new Rectangle(235, 66, 66, 27), ForeColor = BarcaUi.Gold };
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1900 };
    readonly Func<Rectangle> anchor;
    readonly BarcaButton close=new(){Text="×"};
    LeoNotice? current;
    protected override bool ShowWithoutActivation => true;
    public LeoToast(Func<Rectangle> pet)
    {
        anchor = pet; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual; ClientSize = new Size(320, 103); BackColor = BarcaUi.Surface;
        AutoScaleMode=AutoScaleMode.None;
        SetStyle(ControlStyles.ResizeRedraw|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
        DoubleBuffered=true; Appearance.Round(this, 13); Controls.Add(retry);
        close.SetBounds(237,6,22,22); close.Click += (_, _) => Hide(); Controls.Add(close);
        retry.Click += async (_, _) => { var action = current?.Retry; if (action == null) return; retry.Enabled = false; try { await action(); } catch (Exception e) { Present(new(e.Message, true, action)); } finally { if (!IsDisposed) retry.Enabled = true; } };
        timer.Tick += (_, _) => { if (!Bounds.Contains(Cursor.Position)) { timer.Stop(); Hide(); } };
    }
    public void Present(LeoNotice notice)
    {
        current = notice; messageText = UiLanguage.T((notice.Error ? "未完成 · " : notice.Busy ? "处理中 · " : "✓ ") + notice.Text);
        int width=messageText.Length>90?320:270;
        int textWidth=width-54;
        int textHeight=Math.Max(25,TextRenderer.MeasureText(messageText,noticeFont,new Size(textWidth,int.MaxValue),NoticeFlags).Height+6);
        SuspendLayout();
        ClientSize=new Size(width,Math.Max(52,14+textHeight+(notice.Retry!=null?39:10)));Appearance.Round(this,12);
        messageBounds=new(15,14,textWidth,textHeight);close.SetBounds(width-31,6,22,22);retry.SetBounds(width-76,Height-33,64,25);
        retry.Text=notice.ActionLabel;retry.Visible = notice.Retry != null; retry.Enabled = true;
        ResumeLayout(true);
        var pet = anchor(); Location = PopupPlacement.Place(pet, Size, Screen.FromRectangle(pet).WorkingArea);
        timer.Stop(); timer.Interval = notice.Error ? Math.Clamp(notice.Text.Length*80,5000,12000) : notice.Retry!=null?4500:1900; if(!notice.Busy)timer.Start();
        Show();Invalidate(true);Update();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        UiLanguage.DrawText(e.Graphics,messageText,noticeFont,messageBounds,current?.Error==true?Color.LightSalmon:BarcaUi.Text,NoticeFlags);
        e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path=Appearance.Path(new Rectangle(1,1,Width-3,Height-3),12);
        using var border=new Pen(UiTheme.Color(82,67,82));e.Graphics.DrawPath(border,path);
        using var stripe=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(12,3,Width-24,4),Appearance.Blue,Appearance.Red,0f);e.Graphics.FillRectangle(stripe,12,3,Width-24,4);
    }
    internal bool IsBusy=>current?.Busy==true;
    internal bool Matches(LeoNotice notice)=>current?.OperationId==notice.OperationId;
    public bool PreviewHasRetry => retry.Visible;
    internal bool PreviewTextFits=>messageBounds.Height>=TextRenderer.MeasureText(messageText,noticeFont,new Size(messageBounds.Width,int.MaxValue),NoticeFlags).Height&&messageBounds.Bottom<(retry.Visible?retry.Top:ClientSize.Height)&&!messageBounds.IntersectsWith(close.Bounds)&&ClientRectangle.Contains(messageBounds)&&ClientRectangle.Contains(retry.Visible?retry.Bounds:close.Bounds);
    public void Follow() { if(Visible){var pet=anchor();Location=PopupPlacement.Place(pet,Size,Screen.FromRectangle(pet).WorkingArea);} }
    protected override void Dispose(bool disposing) { if (disposing){timer.Dispose();noticeFont.Dispose();} base.Dispose(disposing); }
}

sealed partial class PetForm
{
    LeoToast? feedback;
    void OnMonitorsChanged(){if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke(()=>{if(!IsDisposed){RevealDock(true);dockSide=DockSide.None;Location=Clamp(Location);PositionFoot();hoverCard?.Hide();feedback?.Follow();}});}catch(InvalidOperationException){}}
    void PresentFeedback(LeoNotice notice)
    {
        if (IsDisposed || Disposing || previewMode) return;
        if (InvokeRequired) { BeginInvoke(() => PresentFeedback(notice)); return; }
        if(!notice.Busy&&notice.Retry!=null&&notice.Module!=null){var retryAction=notice.Retry;string module=notice.Module;notice=notice with{Retry=()=>{Modules.Require(prefs,module);return retryAction();}};}
        if(!notice.Busy)notices.Add(notice.Text,notice.Error?"error":notice.Attention?"attention":"operation",unique:notice.OperationId==null?"":(notice.Error?"error:":"result:")+notice.OperationId+":"+DateTime.Now.ToString("yyyyMMddHHmm"),action:notice.Retry,label:notice.ActionLabel);
        if(!FeedbackPolicy.ShouldPopup(notice)){if(!notice.Busy&&feedback?.IsBusy==true&&feedback.Matches(notice))feedback.Hide();return;}
        if (hiddenForFullscreen||focus.Active(DateTimeOffset.Now)&&!notice.Busy&&!notice.Error) return;
        feedback ??= new LeoToast(() => Bounds);
        feedback.Present(notice);
    }
}
