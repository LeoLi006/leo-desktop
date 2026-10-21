using System.Drawing.Drawing2D;
namespace LeoLauncher;
static class BarcaMenus
{
    public static void Apply(ContextMenuStrip menu){var font=BarcaUi.Font(9.5f);menu.Renderer=new BarcaMenuRenderer();menu.Font=font;menu.BackColor=BarcaUi.Surface;menu.ForeColor=BarcaUi.Text;menu.ShowImageMargin=false;menu.ShowCheckMargin=false;menu.DropShadowEnabled=true;menu.Opening+=(_,_)=>Fit(menu);menu.Disposed+=(_,_)=>font.Dispose();}
    public static void Fit(ToolStripDropDown menu)
    {
        UiLanguage.Attach(menu);menu.AutoSize=false;menu.Padding=new Padding(4,5,4,5);int width=96;
        foreach(ToolStripItem item in menu.Items){if(item is ToolStripSeparator)continue;int text=TextRenderer.MeasureText(item.Text??"",menu.Font,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine).Width;int extra=0;if(item is ToolStripMenuItem entry){if(entry.HasDropDownItems)extra+=15;if(!string.IsNullOrEmpty(entry.ShortcutKeyDisplayString))extra+=TextRenderer.MeasureText(entry.ShortcutKeyDisplayString,menu.Font).Width+14;}width=Math.Max(width,text+extra+32);}
        foreach(ToolStripItem item in menu.Items){if(item is ToolStripSeparator)continue;item.AutoSize=false;item.Size=new Size(width,34);item.Padding=new Padding(9,0,20,0);}
        menu.Size=new Size(width+menu.Padding.Horizontal,menu.Padding.Vertical+menu.Items.Cast<ToolStripItem>().Sum(i=>i.Height)+2);menu.PerformLayout();
    }
    public static void StyleSubmenu(ToolStripMenuItem item,Font font)
    {
        var drop=(ToolStripDropDownMenu)item.DropDown;drop.Renderer=new BarcaMenuRenderer();drop.BackColor=BarcaUi.Surface;drop.ForeColor=BarcaUi.Text;drop.Font=font;drop.ShowImageMargin=false;drop.ShowCheckMargin=false;
        drop.Opening+=(_,_)=>{drop.Font=item.Owner?.Font??font;Fit(drop);};
        void Align(){if(drop.IsDisposed||!drop.Visible||item.Owner==null||item.Owner.IsDisposed)return;var itemPoint=item.Owner.PointToScreen(item.Bounds.Location);var anchor=new Rectangle(item.Owner.PointToScreen(Point.Empty).X,itemPoint.Y,item.Owner.Width,item.Bounds.Height);drop.Location=PopupPlacement.Beside(anchor,drop.Size,Screen.FromRectangle(anchor).WorkingArea,0);}
        drop.Opened+=(_,_)=>{Align();drop.BeginInvoke((Action)Align);};
    }
}
sealed class BarcaMenuRenderer:ToolStripProfessionalRenderer
{
    public BarcaMenuRenderer():base(new BarcaMenuColors()){RoundedEdges=false;}
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(BarcaUi.Surface);if(e.ToolStrip.Width<2)return;using var stripe=new LinearGradientBrush(new Rectangle(0,0,e.ToolStrip.Width,3),Appearance.Blue,Appearance.Red,0f);e.Graphics.FillRectangle(stripe,0,0,e.ToolStrip.Width,3);}
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){using var p=new Pen(BarcaUi.Line);e.Graphics.DrawRectangle(p,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1);}
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){if(e.Item.Width<8||e.Item.Height<4)return;var r=new Rectangle(2,1,e.Item.Width-4,e.Item.Height-2);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var path=Appearance.Path(r,8);bool check=e.Item is ToolStripMenuItem item&&item.Checked;if(e.Item.Enabled&&(e.Item.Selected||check)){using var b=new LinearGradientBrush(r,check?Appearance.Blue:BarcaUi.Raised,check?Appearance.Red:UiTheme.Color(40,39,59),0f);e.Graphics.FillPath(b,path);}if(check){using var p=new Pen(BarcaUi.Gold,1.6f){StartCap=LineCap.Round,EndCap=LineCap.Round};float x=e.Item.Width-24,y=e.Item.Height/2f;e.Graphics.DrawLines(p,[new PointF(x-4,y),new PointF(x-1,y+3),new PointF(x+5,y-4)]);}}
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var item=e.Item as ToolStripMenuItem;var color=!e.Item.Enabled?BarcaUi.Muted:item?.Checked==true?BarcaUi.Gold:BarcaUi.Text;
        string shortcut=item?.ShortcutKeyDisplayString??"";bool drawingShortcut=shortcut.Length>0&&e.Text==shortcut;
        int shortcutWidth=shortcut.Length>0?TextRenderer.MeasureText(shortcut,e.TextFont,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine).Width+10:0;
        var bounds=drawingShortcut?new Rectangle(e.Item.Width-shortcutWidth-10,0,shortcutWidth,e.Item.Height):new Rectangle(10,0,e.Item.Width-20-(shortcutWidth>0?shortcutWidth+10:item?.HasDropDownItems==true?15:0),e.Item.Height);
        TextRenderer.DrawText(e.Graphics,e.Text,e.TextFont,bounds,drawingShortcut?BarcaUi.Muted:color,(drawingShortcut?TextFormatFlags.Right:TextFormatFlags.Left)|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine);
    }

}
sealed class BarcaMenuColors:ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground=>BarcaUi.Surface;
    public override Color ImageMarginGradientBegin=>BarcaUi.Surface;
    public override Color ImageMarginGradientMiddle=>BarcaUi.Surface;
    public override Color ImageMarginGradientEnd=>BarcaUi.Surface;
    public override Color MenuBorder=>BarcaUi.Line;
    public override Color MenuItemBorder=>BarcaUi.Line;
}
sealed class QuoteDeck
{
    public static readonly string[] Lines=[
        "一步一步，完成今天。","把热爱，变成每天的行动。","慢一点没关系，继续向前。","今天的小进步，也值得庆祝。","先完成一件，再走下一步。","状态有起伏，努力会留下。","给自己耐心，也给梦想时间。","认真生活，就是你的主场。","每一次坚持，都在积蓄力量。","累了就休息，恢复后再出发。","把目光放近，先做好这一刻。","不必完美，开始就很有意义。","日子很长，进步可以慢慢来。","今天的你，也值得被鼓励。","把难事拆小，一件一件完成。","给热爱留时间，给自己留余地。","比分会变化，热爱不散场。","保持自己的节奏，稳稳向前。","每一步，都是属于你的进球。","偶尔慢下来，也是在照顾自己。","从一件小事，找回今天的节奏。","做得到的先做，远方慢慢抵达。","好好休息，也是计划的一部分。","为下一次出发，积攒一点力量。"
    ];
    int[] order=[];int position,last=-1,revision=-1;
    public string Next(){var lines=PlannerPhrases.Current;if(revision!=PlannerPhrases.Revision){revision=PlannerPhrases.Revision;order=[];position=0;last=-1;}if(lines.Length==0)return "";if(position>=order.Length){order=Enumerable.Range(0,lines.Length).ToArray();Random.Shared.Shuffle(order);if(order.Length>1&&order[0]==last)(order[0],order[1])=(order[1],order[0]);position=0;}last=order[position++];return lines[last];}
}
sealed class PlannerIdentity:Control
{
    readonly Font title=BarcaUi.Font(10,true),small=BarcaUi.Font(8);
    readonly System.Windows.Forms.Timer timer=new(){Interval=8000};
    readonly UiToolTip tip=new();readonly QuoteDeck deck=new();
    string quote,next="";bool transitioning,hover,closing;long started;Form? owner;
    public string CurrentQuote=>quote;
    public bool QuoteTimerRunning=>timer.Enabled;
    public PlannerIdentity(){PlannerPhrases.Changed+=RefreshPhrases;quote=deck.Next();Size=new Size(252,116);Cursor=Cursors.Hand;AccessibleName="球队励志短句，点击换一句";SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);tip.SetToolTip(this,"每8秒滚动换一句 · 悬停暂停 · 点击换一句");timer.Tick+=(_,_)=>TickQuote();}
    void RefreshPhrases(){if(closing||IsDisposed)return;quote=deck.Next();next="";transitioning=false;Restart();Invalidate();}
    bool CanAnimate=>PlannerPhrases.Current.Length>1&&!closing&&Visible&&IsHandleCreated&&FindForm()?.WindowState!=FormWindowState.Minimized;
    void Restart(){timer.Stop();timer.Interval=8000;if(CanAnimate&&!hover)timer.Start();}
    void Finish(){if(transitioning){quote=next;next="";transitioning=false;}AccessibleDescription=quote;Invalidate();Restart();}
    void BeginNext(){if(transitioning)Finish();next=deck.Next();transitioning=true;started=Environment.TickCount64;timer.Stop();timer.Interval=30;if(CanAnimate&&!hover)timer.Start();else Finish();Invalidate();}
    void TickQuote(){if(!CanAnimate){Finish();return;}if(!transitioning){if(!hover)BeginNext();return;}if(Environment.TickCount64-started>=450)Finish();else Invalidate();}
    void WatchOwner(){var form=FindForm();if(ReferenceEquals(form,owner))return;if(owner!=null){owner.SizeChanged-=OwnerChanged;owner.VisibleChanged-=OwnerChanged;}owner=form;if(owner!=null){owner.SizeChanged+=OwnerChanged;owner.VisibleChanged+=OwnerChanged;}}
    void OwnerChanged(object? sender,EventArgs e){if(closing)return;if(!CanAnimate)Finish();else Restart();}
    protected override void OnParentChanged(EventArgs e){base.OnParentChanged(e);WatchOwner();}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);WatchOwner();Restart();}
    protected override void OnHandleDestroyed(EventArgs e){timer.Stop();base.OnHandleDestroyed(e);}
    protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(closing)return;if(Visible)Restart();else timer.Stop();}
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Finish();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Restart();}
    protected override void OnClick(EventArgs e){base.OnClick(e);BeginNext();}
    public void PreviewNext()=>OnClick(EventArgs.Empty);
    protected override void OnPaint(PaintEventArgs e){if(Width<20||Height<20)return;var g=e.Graphics;g.Clear(Parent?.BackColor??BarcaUi.Canvas);g.SmoothingMode=SmoothingMode.AntiAlias;var state=g.Save();using var path=Appearance.Path(new Rectangle(0,0,Width-1,Height-1),14);g.SetClip(path);using var bg=new LinearGradientBrush(ClientRectangle,UiTheme.Color(22,45,78),UiTheme.Color(64,23,47),12);g.FillRectangle(bg,ClientRectangle);for(int i=0;i<6;i++){using var stripe=new SolidBrush(i%2==0?UiTheme.Color(45,42,81,143):UiTheme.Color(45,143,34,66));g.FillPolygon(stripe,[new Point(i*57-20,0),new Point(i*57+9,0),new Point(i*57-20,Height),new Point(i*57-49,Height)]);}using var line=new Pen(UiTheme.Color(28,BarcaUi.Gold));g.DrawEllipse(line,Width-77,-12,110,110);g.DrawRectangle(line,Width-125,-12,148,135);PlannerBadge.Draw(g,UiTheme.Skin,new Rectangle(18,17,38,38));TextRenderer.DrawText(g,UiTheme.Motto,title,new Rectangle(69,18,176,25),BarcaUi.Gold);TextRenderer.DrawText(g,UiTheme.TeamName+" / DAILY PLANNER",small,new Rectangle(69,45,175,20),BarcaUi.Muted);
        g.SetClip(new Rectangle(18,82,Width-36,24),CombineMode.Intersect);using var ink=new SolidBrush(BarcaUi.Text);using var format=new StringFormat{Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap,LineAlignment=StringAlignment.Center};float t=transitioning?Math.Clamp((Environment.TickCount64-started)/450f,0,1):0;t=t*t*(3-2*t);g.DrawString(quote,small,ink,new RectangleF(18,82-24*t,Width-36,24),format);if(transitioning)g.DrawString(next,small,ink,new RectangleF(18,106-24*t,Width-36,24),format);g.Restore(state);
    }
    protected override void Dispose(bool disposing){if(disposing){PlannerPhrases.Changed-=RefreshPhrases;closing=true;if(owner!=null){owner.SizeChanged-=OwnerChanged;owner.VisibleChanged-=OwnerChanged;}timer.Stop();timer.Dispose();tip.Dispose();title.Dispose();small.Dispose();}base.Dispose(disposing);}
}
