namespace LeoLauncher;

sealed record LeoNotice(string Text, bool Error = false, Func<Task>? Retry = null, bool Busy = false, string ActionLabel = "重试");
static class OperationFeedback
{
    public static Action<LeoNotice>? Sink;
    public static void Show(string text) => Sink?.Invoke(new(text));
    public static void Pending(string text) => Sink?.Invoke(new(text, Busy: true));
    public static void Error(string text, Func<Task>? retry = null,string label="重试") => Sink?.Invoke(new(text, true, retry,ActionLabel:label));
}

sealed class LeoToast : Form
{
    readonly Label message = new() { Bounds = new Rectangle(14, 11, 290, 49), ForeColor = BarcaUi.Text, Font = BarcaUi.Font(9), AutoEllipsis = true };
    readonly BarcaButton retry = new() { Text = "重试", Bounds = new Rectangle(235, 66, 66, 27), ForeColor = BarcaUi.Gold };
    readonly System.Windows.Forms.Timer timer = new() { Interval = 4500 };
    readonly Func<Rectangle> anchor;
    LeoNotice? current;
    protected override bool ShowWithoutActivation => true;
    public LeoToast(Func<Rectangle> pet)
    {
        anchor = pet; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual; ClientSize = new Size(320, 103); BackColor = BarcaUi.Surface;
        Appearance.Round(this, 13); Controls.Add(message); Controls.Add(retry);
        var close = new BarcaButton { Text = "×", Bounds = new Rectangle(12, 66, 30, 27) }; close.Click += (_, _) => Hide(); Controls.Add(close);
        retry.Click += async (_, _) => { var action = current?.Retry; if (action == null) return; retry.Enabled = false; try { await action(); } catch (Exception e) { Present(new(e.Message, true, action)); } finally { if (!IsDisposed) retry.Enabled = true; } };
        timer.Tick += (_, _) => { if (!Bounds.Contains(Cursor.Position)) { timer.Stop(); Hide(); } };
    }
    public void Present(LeoNotice notice)
    {
        current = notice; message.Text = (notice.Error ? "未完成 · " : notice.Busy ? "处理中 · " : "✓ ") + notice.Text;
        message.ForeColor = notice.Error ? Color.LightSalmon : BarcaUi.Text;
        retry.Text=notice.ActionLabel;retry.Visible = notice.Retry != null; retry.Enabled = true;
        var pet = anchor(); Location = PopupPlacement.Place(pet, Size, Screen.FromRectangle(pet).WorkingArea);
        timer.Stop(); if (!notice.Busy) { timer.Interval = notice.Error ? 14000 : 4500; timer.Start(); }
        Show();
    }
    public bool PreviewHasRetry => retry.Visible;
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
