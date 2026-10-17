using System.Drawing.Drawing2D;
namespace LeoLauncher;
sealed class PlannerCalendar:Control
{
    DateOnly selected=DateOnly.FromDateTime(DateTime.Today),month=new(DateTime.Today.Year,DateTime.Today.Month,1);bool weekly;
    Dictionary<DateOnly,(bool Pending,bool Reminder)> marks=new();int hovered=-1;
    readonly Font monthFont=BarcaUi.Font(12,true),labelFont=BarcaUi.Font(8.5f),numberFont=new("Segoe UI",10),selectedFont=new("Segoe UI",10,FontStyle.Bold);
    public event Action<DateOnly>? DateSelected;
    public event Action<string,DateOnly>? TaskDropped;
    public const string TaskDragFormat="Leo.Planner.TaskId";
    public DateOnly Selected=>selected;
    public DateOnly VisibleMonth=>month;
    public PlannerCalendar(){AllowDrop=true;DragEnter+=(_,e)=>UpdateDrag(e);DragOver+=(_,e)=>UpdateDrag(e);DragLeave+=(_,_)=>{hovered=-1;Invalidate();};DragDrop+=(_,e)=>DropTask(e);Size=new Size(252,324);TabStop=true;AccessibleName="清单日历";AccessibleRole=AccessibleRole.Table;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
    int DropCell(Point point)=>Enumerable.Range(0,42).FirstOrDefault(i=>Cell(i).Contains(point),-1);
    void UpdateDrag(DragEventArgs e){int index=DropCell(PointToClient(new Point(e.X,e.Y)));bool valid=e.Data?.GetDataPresent(TaskDragFormat)==true&&index>=0;e.Effect=valid?DragDropEffects.Move:DragDropEffects.None;hovered=valid?index:-1;Invalidate();}
    void DropTask(DragEventArgs e){int index=DropCell(PointToClient(new Point(e.X,e.Y)));if(index<0||e.Data?.GetData(TaskDragFormat) is not string id||id.Length==0){e.Effect=DragDropEffects.None;return;}e.Effect=DragDropEffects.Move;var date=GridStart.AddDays(index);hovered=-1;TaskDropped?.Invoke(id,date);Invalidate();}
    public bool PreviewDrop(string id,DateOnly date){int i=date.DayNumber-GridStart.DayNumber;if(i<0||i>41)return false;var r=Cell(i);var p=PointToScreen(new Point(r.Left+r.Width/2,r.Top+r.Height/2));var data=new DataObject();data.SetData(TaskDragFormat,id);var e=new DragEventArgs(data,0,p.X,p.Y,DragDropEffects.Move,DragDropEffects.None);OnDragDrop(e);return e.Effect==DragDropEffects.Move;}
    public void Update(DateOnly date,bool week,IEnumerable<DailyTask> tasks){if(date!=selected)month=new(date.Year,date.Month,1);selected=date;weekly=week;marks=tasks.GroupBy(t=>t.Day).ToDictionary(g=>g.Key,g=>(g.Any(t=>t.Completed==null),g.Any(t=>t.Completed==null&&t.Reminder!=null&&t.Notified==null)));AccessibleDescription=date.ToString("yyyy年MM月dd日 dddd");Invalidate();}
    DateOnly GridStart=>month.AddDays(-(((int)month.DayOfWeek+6)%7));
    Rectangle Cell(int index){int left=10,top=83,w=(Width-20)/7,h=33;return new(left+index%7*w,top+index/7*h,w,h);}
    public void GoTo(DateOnly date){selected=date;month=new(date.Year,date.Month,1);Invalidate();}
    public void MoveMonth(int delta){try{month=month.AddMonths(delta);Invalidate();}catch(ArgumentOutOfRangeException){}}
    void Choose(DateOnly date){selected=date;month=new(date.Year,date.Month,1);Invalidate();DateSelected?.Invoke(date);}
    public bool PreviewSelect(DateOnly date){var start=GridStart;int index=date.DayNumber-start.DayNumber;if(index<0||index>41)return false;var r=Cell(index);OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,r.Left+r.Width/2,r.Top+r.Height/2,0));return selected==date;}
    protected override bool IsInputKey(Keys keyData)=>keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.PageUp or Keys.PageDown||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);try{if(e.KeyCode==Keys.PageUp)Choose(selected.AddMonths(-1));else if(e.KeyCode==Keys.PageDown)Choose(selected.AddMonths(1));else if(e.KeyCode==Keys.Home)Choose(DateOnly.FromDateTime(DateTime.Today));else if(e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down)Choose(selected.AddDays(e.KeyCode==Keys.Left?-1:e.KeyCode==Keys.Right?1:e.KeyCode==Keys.Up?-7:7));else return;e.Handled=true;e.SuppressKeyPress=true;}catch(ArgumentOutOfRangeException){}}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();if(e.Y<51&&e.X>Width-76){MoveMonth(e.X<Width-40?-1:1);return;}for(int i=0;i<42;i++)if(Cell(i).Contains(e.Location)){Choose(GridStart.AddDays(i));return;}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);int next=Enumerable.Range(0,42).FirstOrDefault(i=>Cell(i).Contains(e.Location),-1);if(next!=hovered){hovered=next;Invalidate();}}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hovered=-1;Invalidate();}
    protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e){if(Width<30||Height<30)return;var g=e.Graphics;g.Clear(Parent?.BackColor??BarcaUi.Canvas);g.SmoothingMode=SmoothingMode.AntiAlias;using var path=Appearance.Path(new Rectangle(0,0,Width-1,Height-1),16);using var bg=new SolidBrush(BarcaUi.Surface);g.FillPath(bg,path);TextRenderer.DrawText(g,month.ToString("yyyy年 M月"),monthFont,new Rectangle(18,17,Width-97,28),BarcaUi.Text);TextRenderer.DrawText(g,"‹",monthFont,new Rectangle(Width-76,14,33,30),BarcaUi.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);TextRenderer.DrawText(g,"›",monthFont,new Rectangle(Width-40,14,30,30),BarcaUi.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        string[] weekdays=["一","二","三","四","五","六","日"];for(int i=0;i<7;i++)TextRenderer.DrawText(g,weekdays[i],labelFont,new Rectangle(Cell(i).Left,56,Cell(i).Width,22),i<5?BarcaUi.Muted:BarcaUi.Gold,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        var start=GridStart;var weekStart=selected.AddDays(-(((int)selected.DayOfWeek+6)%7));for(int i=0;i<42;i++){var date=start.AddDays(i);var r=Cell(i);var inner=Rectangle.Inflate(r,-2,-2);bool pick=date==selected,today=date==DateOnly.FromDateTime(DateTime.Today);using var shape=Appearance.Path(inner,9);if(pick){using var fill=new LinearGradientBrush(inner,Appearance.Blue,Appearance.Red,20);g.FillPath(fill,shape);}else if(weekly&&date>=weekStart&&date<=weekStart.AddDays(6)){using var fill=new SolidBrush(Color.FromArgb(29,48,77));g.FillPath(fill,shape);}else if(i==hovered){using var fill=new SolidBrush(BarcaUi.Raised);g.FillPath(fill,shape);}if(today){using var pen=new Pen(BarcaUi.Gold,1.3f);g.DrawPath(pen,shape);}if(Focused&&pick){using var pen=new Pen(Color.FromArgb(100,BarcaUi.Gold));g.DrawPath(pen,shape);}TextRenderer.DrawText(g,date.Day.ToString(),pick?selectedFont:numberFont,new Rectangle(r.Left,r.Top+1,r.Width,23),pick?Color.White:date.Month==month.Month?BarcaUi.Text:Color.FromArgb(74,92,119),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);if(marks.TryGetValue(date,out var mark)){using var dot=new SolidBrush(mark.Reminder?BarcaUi.Gold:mark.Pending?Color.FromArgb(122,171,232):Color.FromArgb(78,101,129));g.FillEllipse(dot,r.Left+r.Width/2f-2,r.Bottom-7,4,4);}}
        using var rule=new Pen(BarcaUi.Line);g.DrawLine(rule,16,289,Width-16,289);using var pendingDot=new SolidBrush(Color.FromArgb(122,171,232));using var reminderDot=new SolidBrush(BarcaUi.Gold);g.FillEllipse(pendingDot,19,301,4,4);g.FillEllipse(reminderDot,113,301,4,4);TextRenderer.DrawText(g,"有安排",labelFont,new Rectangle(29,294,70,20),BarcaUi.Muted);TextRenderer.DrawText(g,"待提醒",labelFont,new Rectangle(123,294,75,20),BarcaUi.Muted);
    }
    protected override void Dispose(bool disposing){if(disposing){monthFont.Dispose();labelFont.Dispose();numberFont.Dispose();selectedFont.Dispose();}base.Dispose(disposing);}
}
sealed class PlannerProgress:Control
{
    public int Done,Total;
    public PlannerProgress(){Height=5;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BarcaUi.Canvas);if(Width<2)return;using var bg=new SolidBrush(BarcaUi.Line);e.Graphics.FillRectangle(bg,0,0,Width,Height);if(Total>0&&Done>0){using var brush=new LinearGradientBrush(ClientRectangle,Appearance.Blue,Appearance.Red,0f);e.Graphics.FillRectangle(brush,0,0,(float)Width*Done/Total,Height);}}
}
sealed class PlannerIcon:Button
{
    readonly string kind;public bool Checked;bool hover;
    public PlannerIcon(string name){kind=name;TabStop=true;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Invalidate();}
    protected override void OnPaint(PaintEventArgs e){if(Width<4||Height<4)return;var g=e.Graphics;g.Clear(Parent?.BackColor??BarcaUi.Surface);g.SmoothingMode=SmoothingMode.AntiAlias;float x=Width/2f,y=Height/2f;if(hover||Focused){using var b=new SolidBrush(BarcaUi.Raised);g.FillEllipse(b,x-13,y-13,26,26);}using var pen=new Pen(Checked?BarcaUi.Gold:hover?BarcaUi.Text:BarcaUi.Muted,1.5f){StartCap=LineCap.Round,EndCap=LineCap.Round};if(kind=="check"){g.DrawEllipse(pen,x-9,y-9,18,18);if(Checked)g.DrawLines(pen,[new PointF(x-4,y),new PointF(x-1,y+3),new PointF(x+5,y-4)]);}else if(kind=="postpone"){g.DrawLine(pen,x-7,y,x+6,y);g.DrawLines(pen,[new PointF(x+1,y-5),new PointF(x+6,y),new PointF(x+1,y+5)]);}else if(kind=="repeat"){g.DrawArc(pen,x-7,y-7,14,14,35,290);g.DrawLines(pen,[new PointF(x+5,y-7),new PointF(x+8,y-3),new PointF(x+3,y-2)]);}else if(kind=="clock"){g.DrawEllipse(pen,x-7,y-7,14,14);g.DrawLines(pen,[new PointF(x,y-4),new PointF(x,y),new PointF(x+4,y+2)]);}else{g.DrawLine(pen,x-6,y-5,x+6,y-5);g.DrawLines(pen,[new PointF(x-5,y-2),new PointF(x-4,y+7),new PointF(x+4,y+7),new PointF(x+5,y-2)]);g.DrawLine(pen,x-2,y-8,x+2,y-8);}}
}
