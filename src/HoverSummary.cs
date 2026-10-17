namespace LeoLauncher;

static class LeoSummary
{
    public static string Build(IEnumerable<DailyTask> tasks, IEnumerable<FootballMatch> matches, DateTimeOffset now)
    {
        var day = DateOnly.FromDateTime(now.LocalDateTime);
        var pending = tasks.Where(t => t.Day <= day && t.Completed == null && !t.Deleted).OrderBy(t => t.Day).ThenBy(t => t.Created).ToArray();
        string todos = pending.Length == 0 ? "今天没有未完成事项 ✓" : $"今日待办 {pending.Length} 项" + (pending.Any(t => t.Day < day) ? "（含未完成旧事项）" : "") + "\n" + string.Join("\n", pending.Take(3).Select(t => "· " + (t.Title.Length>24?t.Title[..24]+"…":t.Title))) + (pending.Length > 3 ? "\n· 还有 " + (pending.Length - 3) + " 项" : "");
        var next = matches.Where(m => m.TimeKnown && m.State == "pre" && m.Kickoff > now && !m.Status.Contains("POSTPON", StringComparison.OrdinalIgnoreCase) && !m.Status.Contains("CANCEL", StringComparison.OrdinalIgnoreCase) && FootballFeed.FollowTags(m, FootballForm.FixedFollowing).Length > 0).OrderBy(m => m.Kickoff).FirstOrDefault();
        string football = next == null ? "暂无已缓存的关注比赛" : next.Kickoff.ToLocalTime().ToString("MM-dd HH:mm") + "  " + FootballFeed.Name(next.Home) + " vs " + FootballFeed.Name(next.Away);
        return todos + "\n\n下一场关注比赛 · 缓存\n" + football;
    }
    public static FootballMatch[] CachedMatches()
    {
        var matches = FootballFeed.LoadCache().Matches.ToList();
        foreach (string name in FootballForm.FixedFollowing) matches.AddRange(TeamData.Load(Club.For(name)).Matches);
        return matches.GroupBy(m => m.Id).Select(g => g.Last()).ToArray();
    }
}

sealed class HoverCard : Form
{
    readonly Label body = new() { Bounds = new Rectangle(16, 40, 327, 205), Font = BarcaUi.Font(9), ForeColor = BarcaUi.Text, AutoEllipsis = true };
    protected override bool ShowWithoutActivation => true;
    public HoverCard()
    {
        StartPosition = FormStartPosition.Manual; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; ClientSize = new Size(360, 255); BackColor = BarcaUi.Surface;
        Appearance.Round(this, 14); Controls.Add(new Label { Text = "LEO  /  今天", Bounds = new Rectangle(16, 12, 327, 25), Font = BarcaUi.Font(10, true), ForeColor = BarcaUi.Gold }); Controls.Add(body);
    }
    public void Present(string text, Rectangle anchor)
    {
        body.Text = text;Height=Math.Clamp(TextRenderer.MeasureText(text,body.Font,new Size(body.Width,1000),TextFormatFlags.WordBreak).Height+65,120,255);body.Height=Height-50;Appearance.Round(this,14); Location = PopupPlacement.Place(anchor, Size, Screen.FromRectangle(anchor).WorkingArea); Show();
    }
}

sealed partial class PetForm
{
    readonly System.Windows.Forms.Timer hoverTimer = new() { Interval = 180 };
    HoverCard? hoverCard;
    long hoverSince;
    Point hoverOrigin;
    long summaryLoadedAt = -60000;
    FootballMatch[] summaryMatches = [];
    void StartHoverSummary()
    {
        if (previewMode) return;
        hoverTimer.Tick += (_, _) =>
        {
            bool over = hit.Visible && hit.Bounds.Contains(Cursor.Position) && Control.MouseButtons == MouseButtons.None && !dragging && !launcher.Visible && ContextMenuStrip?.Visible != true && !hiddenForFullscreen && feedback?.Visible != true;
            if (!over) { hoverSince = 0; hoverCard?.Hide(); return; }
            if (hoverSince == 0) { hoverSince = clock.ElapsedMilliseconds; hoverOrigin = Cursor.Position; return; }
            if (Math.Abs(Cursor.Position.X-hoverOrigin.X)>5||Math.Abs(Cursor.Position.Y-hoverOrigin.Y)>5) { hoverSince=clock.ElapsedMilliseconds;hoverOrigin=Cursor.Position;hoverCard?.Hide();return; }
            if (clock.ElapsedMilliseconds - hoverSince < 650 || hoverCard?.Visible == true) return;
            if (clock.ElapsedMilliseconds - summaryLoadedAt > 60000) { summaryMatches = LeoSummary.CachedMatches(); summaryLoadedAt = clock.ElapsedMilliseconds; }
            hoverCard ??= new HoverCard(); hoverCard.Present(LeoSummary.Build(todoStore.Items, summaryMatches, DateTimeOffset.Now), Bounds);
        };
        hoverTimer.Start();
    }
}
