using System.Text.Json;
namespace LeoLauncher;

static class ClubCatalog
{
    public static readonly Club[] All=JsonSerializer.Deserialize<Club[]>(File.ReadAllText(Path.Combine(Store.Root,"assets","clubs.json")))??throw new IOException("球队目录缺失");
    public static Club? Find(string name)=>All.FirstOrDefault(c=>c.Id==name||c.Name.Equals(name,StringComparison.OrdinalIgnoreCase)||c.English.Equals(name,StringComparison.OrdinalIgnoreCase)||c.Aliases?.Contains(name,StringComparer.OrdinalIgnoreCase)==true);
    public static IEnumerable<string> Names(Club c)=>new[]{c.Name,c.English}.Concat(c.Aliases??[]).Where(s=>s.Length>1);
    public static string AliasPattern(string alias)=>System.Text.RegularExpressions.Regex.IsMatch(alias,@"^[A-Za-z0-9 .-]+$")?@"(?<![A-Za-z0-9])"+System.Text.RegularExpressions.Regex.Escape(alias)+@"(?![A-Za-z0-9])":System.Text.RegularExpressions.Regex.Escape(alias);
    public static string Mention(string text)=>All.SelectMany(c=>Names(c).Select(alias=>(c,alias))).OrderByDescending(x=>x.alias.Length).FirstOrDefault(x=>System.Text.RegularExpressions.Regex.IsMatch(text,AliasPattern(x.alias),System.Text.RegularExpressions.RegexOptions.IgnoreCase)).c?.Name??"";
    public static string Pattern=>"(?:"+string.Join("|",All.SelectMany(Names).Distinct().OrderByDescending(s=>s.Length).Select(AliasPattern))+")";
}

sealed class FootballFollowing
{
    public static readonly string[] Defaults=["83","20232","382"];
    public static readonly FootballFollowing Current=new(Path.Combine(Store.Root,"leo-football-following.json"));
    readonly string? path;List<string> ids=new();
    public int Version{get;private set;}
    public string? Error{get;private set;}
    public Club[] Clubs=>ids.Select(id=>ClubCatalog.Find(id)!).ToArray();
    public string[] Names=>Clubs.Select(c=>c.Name).ToArray();
    public string[] Ids=>ids.ToArray();
    public event Action? Changed;
    public FootballFollowing(string? file){path=file;Reload();}
    public static List<string> Validate(IEnumerable<string> values){var next=values.Distinct().ToList();if(next.Count>24||next.Any(id=>!ClubCatalog.All.Any(c=>c.Id==id)))throw new ArgumentException("最多关注24支目录中的球队");return next;}
    public void Reload(){try{ids=path!=null&&File.Exists(path)?Validate(JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path))??throw new IOException()):Defaults.ToList();Error=null;}catch{ids=Defaults.ToList();Error="关注配置无法读取，原文件已保留，请先检查关注配置文件";}Version++;Changed?.Invoke();}
    public void Replace(IEnumerable<string> values){if(Error!=null)throw new IOException(Error);var next=Validate(values);if(path!=null)ConfigMigration.Write(path,JsonSerializer.Serialize(next));ids=next;Version++;Changed?.Invoke();}
}

sealed class FollowingManager:BarcaWindow
{
    readonly FootballFollowing following;readonly HashSet<string> selected;
    readonly FlowLayoutPanel rows=new(){Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(16,8,8,8)};
    readonly TextBox query=new(){Bounds=new Rectangle(22,13,410,28),PlaceholderText="搜索球队、简称或英文名",BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text,BorderStyle=BorderStyle.FixedSingle};
    readonly Label count=new(){Bounds=new Rectangle(24,12,460,27),ForeColor=BarcaUi.Gold};
    string league="全部";
    public FollowingManager(FootballFollowing store)
    {
        following=store;selected=store.Ids.ToHashSet();Text="Leo · 管理关注";StartPosition=FormStartPosition.CenterParent;ClientSize=new Size(740,635);
        var header=BarcaUi.Chrome(this,"我的球队","俱乐部与国家队 · 搜索、筛选，选择自己的关注");
        var filters=new Panel{Dock=DockStyle.Top,Height=102};filters.Controls.Add(query);var leagues=new FlowLayoutPanel{Bounds=new Rectangle(20,53,700,40)};
        foreach(string name in new[]{"全部","英超","西甲","意甲","德甲","法甲","美职联","国家队"}){var button=new BarcaButton{Text=name,Size=new Size(75,30),Selected=name==league};button.Click+=(_,_)=>{league=name;foreach(var b in leagues.Controls.OfType<BarcaButton>()){b.Selected=b.Text==name;b.Invalidate();}Render();};leagues.Controls.Add(button);}filters.Controls.Add(leagues);
        var footer=new Panel{Dock=DockStyle.Bottom,Height=58};var save=new BarcaButton{Text="保存关注",Bounds=new Rectangle(581,9,130,35)};save.Click+=(_,_)=>{try{following.Replace(ClubCatalog.All.Where(c=>selected.Contains(c.Id)).Select(c=>c.Id));DialogResult=DialogResult.OK;}catch(Exception e){count.Text=e.Message;}};footer.Controls.AddRange([count,save]);
        Controls.Add(rows);Controls.Add(footer);Controls.Add(filters);Controls.Add(header);query.TextChanged+=(_,_)=>Render();Render();
    }
    void Render()
    {
        rows.SuspendLayout();foreach(Control old in rows.Controls.Cast<Control>().ToArray())old.Dispose();string text=query.Text.Trim();
        foreach(var club in ClubCatalog.All.Where(c=>(league=="全部"||c.LeagueName==league)&&(text.Length==0||ClubCatalog.Names(c).Any(n=>n.Contains(text,StringComparison.OrdinalIgnoreCase)))).OrderBy(c=>!selected.Contains(c.Id)).ThenBy(c=>c.LeagueName)){
            var card=new FollowChoice(club,selected.Contains(club.Id));card.Click+=(_,_)=>{if(!selected.Remove(club.Id)){if(selected.Count>=24){count.Text="最多关注24支球队";return;}selected.Add(club.Id);}card.Checked=selected.Contains(club.Id);card.Invalidate();count.Text=$"已选 {selected.Count} / 24 · 目录 {ClubCatalog.All.Length} 支";};rows.Controls.Add(card);
        }
        if(rows.Controls.Count==0)rows.Controls.Add(new Label{Text="没有找到球队，试试其他名称。",Width=600,Height=60,ForeColor=BarcaUi.Muted});count.Text=$"已选 {selected.Count} / 24 · 目录 {ClubCatalog.All.Length} 支";rows.ResumeLayout();
    }
}

sealed class FollowChoice:Control
{
    readonly Club club;public bool Checked;readonly Font title=BarcaUi.Font(10,true),small=BarcaUi.Font(8.5f);
    public FollowChoice(Club value,bool selected){club=value;Checked=selected;Size=new Size(335,70);Margin=new Padding(0,0,10,10);AccessibleName=value.Name;Cursor=Cursors.Hand;DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(BarcaUi.Canvas);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var path=Appearance.Path(new Rectangle(0,0,Width-2,Height-2),12);using var fill=new SolidBrush(Checked?BarcaUi.Raised:BarcaUi.Surface);g.FillPath(fill,path);using var pen=new Pen(Checked?BarcaUi.Gold:BarcaUi.Line);g.DrawPath(pen,path);BarcaUi.DrawBadge(g,club.Id,new Rectangle(12,19,32,32));TextRenderer.DrawText(g,club.Name,title,new Rectangle(57,13,238,26),BarcaUi.Text);TextRenderer.DrawText(g,club.LeagueName,small,new Rectangle(58,40,238,23),BarcaUi.Muted);TextRenderer.DrawText(g,Checked?"★":"＋",title,new Rectangle(300,23,25,25),BarcaUi.Gold);}
    protected override void Dispose(bool disposing){if(disposing){title.Dispose();small.Dispose();}base.Dispose(disposing);}
}
