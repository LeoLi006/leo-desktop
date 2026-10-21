using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
namespace LeoLauncher;

static class BarcaUi
{
    public static Color Canvas=>UiTheme.Color(11,18,33); public static Color Surface=>UiTheme.Color(19,30,49); public static Color Raised=>UiTheme.Color(25,40,63); public static Color Line=>UiTheme.Color(39,55,78); public static Color Gold=>UiTheme.Color(231,194,119); public static Color Text=>UiTheme.Color(237,242,250); public static Color Muted=>UiTheme.Color(139,158,184);
    public static Font Font(float size=10, bool bold=false) => new("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular);
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
    public static void Drag(Form form){ReleaseCapture();SendMessage(form.Handle,0xA1,(IntPtr)2,IntPtr.Zero);}
    public static Panel Chrome(Form form,string title,string subtitle)
    {
        form.FormBorderStyle=FormBorderStyle.None; form.BackColor=Canvas; form.ForeColor=Text; form.Padding=new Padding(1); form.Font=Font();
        var header=new StripeHeader {Dock=DockStyle.Top,Height=88};
        var name=new Label {Text=title,AutoEllipsis=true,Bounds=new Rectangle(26,18,480,30),Font=Font(17,true),ForeColor=Text,BackColor=Color.Transparent};
        var sub=new Label {Text=subtitle,Bounds=new Rectangle(28,54,650,21),Font=Font(9),ForeColor=Muted,BackColor=Color.Transparent};
        header.Controls.Add(name); header.Controls.Add(sub);
        foreach(Control c in new Control[]{header,name,sub}) c.MouseDown+=(_,e)=> {if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(form.Handle,0xA1,(IntPtr)2,IntPtr.Zero);}};
        var close=new BarcaButton {Text="×",Size=new Size(32,29),Font=Font(15),Anchor=AnchorStyles.Top|AnchorStyles.Right};
        var min=new BarcaButton {Text="−",Size=new Size(32,29),Anchor=AnchorStyles.Top|AnchorStyles.Right};
        header.Controls.Add(close);header.Controls.Add(min);
        void Layout(){close.Location=new Point(header.Width-45,17);min.Location=new Point(header.Width-83,17);name.Width=Math.Max(100,header.Width-140);sub.Width=Math.Max(100,header.Width-60);}
        header.Resize+=(_,_)=>Layout();Layout();close.Click+=(_,_)=>form.Close();min.Click+=(_,_)=>form.WindowState=FormWindowState.Minimized;
        return header;
    }
    public static Image? Badge(string id)
    {
        if(images.TryGetValue(id,out var cached))return cached;
        try {using var source=Image.FromFile(Path.Combine(Store.Root,"assets","badges",id+".png")); var copy=new Bitmap(source);images[id]=copy;return copy;}catch{images[id]=null;return null;}
    }
    static readonly Dictionary<string,Image?> images=new();
    public static void DrawBadge(Graphics g,string id,Rectangle bounds,string fallback="●")
    { var image=Badge(id);if(image!=null)g.DrawImage(image,bounds);else TextRenderer.DrawText(g,fallback,SystemFonts.MessageBoxFont,bounds,Gold,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter); }
}
sealed class StripeHeader:Panel
{
    public StripeHeader(){DoubleBuffered=true;BackColor=BarcaUi.Canvas;}
    protected override void OnPaintBackground(PaintEventArgs e){if(Width<1||Height<1)return;using var fill=new LinearGradientBrush(ClientRectangle,UiTheme.Color(16,33,58),UiTheme.Color(44,21,39),0f);e.Graphics.FillRectangle(fill,ClientRectangle);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Width<2||Height<2)return;using var b=new LinearGradientBrush(new Rectangle(0,0,Width,4),Appearance.Blue,Appearance.Red,0f);e.Graphics.FillRectangle(b,0,0,Width,4);using var p=new Pen(BarcaUi.Line);e.Graphics.DrawLine(p,22,Height-1,Width-22,Height-1);}
}
sealed class RoundedSurface:Panel
{
    public RoundedSurface(){DoubleBuffered=true;}
    protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent?.BackColor??BarcaUi.Canvas);if(Width<2||Height<2)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var shape=Appearance.Path(new Rectangle(0,0,Width-1,Height-1),14);using var fill=new SolidBrush(BackColor);e.Graphics.FillPath(fill,shape);}
}
sealed class BarcaButton:Button
{
    public bool Selected;
    bool hover;
    public BarcaButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=BarcaUi.Surface;ForeColor=BarcaUi.Text;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e){if(Width<2||Height<2)return;e.Graphics.Clear(Parent?.BackColor??BarcaUi.Canvas);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var path=Appearance.Path(new Rectangle(0,0,Width-1,Height-1),9);using var fill=new SolidBrush(Selected?Appearance.Blue:hover?BarcaUi.Raised:BackColor);e.Graphics.FillPath(fill,path);if(Selected){using var pen=new Pen(BarcaUi.Gold,1);e.Graphics.DrawPath(pen,path);}TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?(Selected?BarcaUi.Gold:ForeColor):BarcaUi.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
}
sealed class MatchCard:Control
{
    readonly FootballMatch match;
    readonly string tag;
    readonly Font body=BarcaUi.Font(11,true),small=BarcaUi.Font(9),score= new("Segoe UI",16,FontStyle.Bold);
    public MatchCard(FootballMatch m,string tags,TodoStore? tasks=null,DateTimeOffset? observed=null){match=m;tag=tags;BackColor=BarcaUi.Surface;Height=86;Margin=new Padding(0,0,0,8);Cursor=Cursors.Hand;AccessibleName=FootballFeed.Name(m.Home)+" 对阵 "+FootballFeed.Name(m.Away);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
        if(tasks!=null&&m.TimeKnown&&m.State=="pre"&&m.Kickoff>DateTimeOffset.Now&&!m.Status.Contains("POSTPON",StringComparison.OrdinalIgnoreCase)&&!m.Status.Contains("CANCEL",StringComparison.OrdinalIgnoreCase)){Height=119;bool exists=tasks.Items.Any(t=>t.MatchId=="espn:"+m.Id);var note=new Label{Text=m.Kickoff.AddMinutes(-10)>DateTimeOffset.Now?"开赛前 10 分钟提醒":"即将开赛 · 添加后立即提醒",Bounds=new Rectangle(16,91,310,20),ForeColor=BarcaUi.Muted,Font=small};var add=new BarcaButton{Text=exists?"已加入清单":"＋ 加入清单",AccessibleName="加入清单并提醒",BackColor=BarcaUi.Raised,Size=new Size(138,26),Enabled=!exists,Font=small};void LayoutAction(){add.Location=new Point(Math.Max(15,Width-153),86);note.Width=Math.Max(80,Width-180);}Resize+=(_,_)=>LayoutAction();Controls.Add(note);Controls.Add(add);LayoutAction();add.Click+=async(_,_)=>{add.Enabled=false;try{var result=await tasks.ImportMatchAsync(m,observed);if(IsDisposed)return;add.Text="已加入清单";add.Enabled=false;note.Text=(result.Added?"已设置提醒 · ":"已在清单中 · ")+(result.Task.Reminder?.ToLocalTime().ToString("MM.dd HH:mm")??"无提醒");}catch(Exception e){if(IsDisposed)return;add.Enabled=true;note.Text=e.Message;note.ForeColor=Color.LightSalmon;}};}
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.Clear(Parent?.BackColor??BarcaUi.Canvas);g.SmoothingMode=SmoothingMode.AntiAlias;using var path=Appearance.Path(new Rectangle(0,0,Width-1,Height-1),13);using var fill=new SolidBrush(BarcaUi.Surface);g.FillPath(fill,path);using var line=new Pen(BarcaUi.Line);g.DrawPath(line,path);
        TextRenderer.DrawText(g,FootballFeed.Name(match.League),small,new Rectangle(16,11,Width-180,22),BarcaUi.Muted,TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g,match.Kickoff.ToLocalTime().ToString("MM-dd ddd ")+(match.TimeKnown?match.Kickoff.ToLocalTime().ToString("HH:mm"):"时间待定"),small,new Rectangle(Width-185,11,170,22),BarcaUi.Muted,TextFormatFlags.Right);
        int center=Width/2;
        BarcaUi.DrawBadge(g,match.HomeId,new Rectangle(16,42,27,27));
        TextRenderer.DrawText(g,FootballFeed.Name(match.Home),body,new Rectangle(52,45,center-110,25),BarcaUi.Text,TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g,match.Score,score,new Rectangle(center-54,37,108,35),BarcaUi.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g,FootballFeed.Name(match.Away),body,new Rectangle(center+64,45,center-112,25),BarcaUi.Text,TextFormatFlags.Right|TextFormatFlags.EndEllipsis);
        BarcaUi.DrawBadge(g,match.AwayId,new Rectangle(Width-42,42,27,27));
        TextRenderer.DrawText(g,FootballFeed.Status(match),small,new Rectangle(center-50,68,100,16),match.State=="in"?UiTheme.Color(97,219,157):BarcaUi.Muted,TextFormatFlags.HorizontalCenter);
    }
    protected override void Dispose(bool disposing){if(disposing){body.Dispose();small.Dispose();score.Dispose();}base.Dispose(disposing);}
}
sealed class FollowButton:BarcaFollowBase
{
    public FollowButton(string name,string id):base(name,id){}
}
class BarcaFollowBase:Control
{
    readonly string name,id;
    readonly Font font=BarcaUi.Font(10,true),small=BarcaUi.Font(8.5f);
    public BarcaFollowBase(string label,string badge){name=label;id=badge;Width=174;Height=66;Margin=new Padding(0,0,0,10);Cursor=Cursors.Hand;AccessibleName=label;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent?.BackColor??BarcaUi.Canvas);g.SmoothingMode=SmoothingMode.AntiAlias;using var p=Appearance.Path(new Rectangle(0,0,Width-1,Height-1),12);using var b=new SolidBrush(BarcaUi.Surface);g.FillPath(b,p);BarcaUi.DrawBadge(g,id,new Rectangle(12,17,31,31),"M");TextRenderer.DrawText(g,name,font,new Rectangle(53,13,115,24),BarcaUi.Text,TextFormatFlags.EndEllipsis);TextRenderer.DrawText(g,"比赛与积分榜  ›",small,new Rectangle(54,39,Math.Max(30,Width-60),18),BarcaUi.Muted);}
    protected override void Dispose(bool disposing){if(disposing){font.Dispose();small.Dispose();}base.Dispose(disposing);}
}
sealed class ConversationView:Panel
{
    readonly List<Panel> rows=new();
    readonly List<RichTextBox> texts=new();
    readonly List<string> signatures=new();
    readonly Font bodyFont=BarcaUi.Font(11),labelFont=BarcaUi.Font(8.5f,true);
    List<ChatTurn> history=new();
    bool rendering,reflowPending;
    public ConversationView(){SetStyle(ControlStyles.ResizeRedraw,true);AutoScroll=true;DoubleBuffered=true;BackColor=BarcaUi.Canvas;Resize+=(_,_)=>QueueReflow();VisibleChanged+=(_,_)=>{if(Visible)QueueReflow();};}
    void QueueReflow(){if(reflowPending||IsDisposed||!IsHandleCreated)return;reflowPending=true;try{BeginInvoke((Action)(()=>{try{if(!IsDisposed){Display(history);PerformLayout();}}finally{reflowPending=false;}}));}catch(InvalidOperationException){reflowPending=false;}}
    protected override Point ScrollToControl(Control activeControl)=>AutoScrollPosition;
    public void Display(IEnumerable<ChatTurn> turns)
    {
        if(rendering||IsDisposed)return;
        rendering=true;
        try {
        history=turns.ToList();var data=new List<(string Name,string Text,bool User)>();
        if(history.Count==0){data.Add(("LEO",UiTheme.Rename("你好，我是 Leo。\n\n有什么想聊的？也可以让我打开应用，或点击麦克风直接说话。"),false));}
        foreach(var turn in history.TakeLast(40)){data.Add(("你",turn.User,true));string details=turn.ToolLog.Count>0?string.Join("\n",turn.ToolLog)+"\n\n":"";data.Add(("LEO",details+(turn.Assistant.Length>0?turn.Assistant:"正在思考…")+(turn.Status=="完成"?"":"\n["+turn.Status+"]"),false));}
        bool follow=VerticalScroll.Value+ClientSize.Height>=DisplayRectangle.Height-70;
        int savedScroll=-AutoScrollPosition.Y;
        AutoScrollPosition=Point.Empty;
        SuspendLayout();
        while(rows.Count>data.Count){int i=rows.Count-1;rows[i].Dispose();rows.RemoveAt(i);texts.RemoveAt(i);signatures.RemoveAt(i);}
        int y=12,available=Math.Max(300,ClientSize.Width-32-SystemInformation.VerticalScrollBarWidth);
        for(int i=0;i<data.Count;i++){
            var item=data[i];
            if(i==rows.Count){var row=new SpeechSurface{BackColor=BarcaUi.Surface};var label=new Label{Font=labelFont,ForeColor=BarcaUi.Gold,Bounds=new Rectangle(17,11,500,20)};var rich=new RichTextBox{ReadOnly=true,BorderStyle=BorderStyle.None,DetectUrls=false,Font=bodyFont,ForeColor=BarcaUi.Text,ScrollBars=RichTextBoxScrollBars.None};row.Controls.Add(label);row.Controls.Add(rich);Controls.Add(row);rows.Add(row);texts.Add(rich);signatures.Add("");}
            var panel=rows[i];var text=texts[i];int width=item.User?Math.Max(260,(int)(available*.82)):available;
            ((SpeechSurface)panel).IsUser=item.User;panel.BackColor=item.User?UiTheme.Color(27,48,79):UiTheme.Color(32,27,48);text.BackColor=panel.BackColor;
            ((Label)panel.Controls[0]).Text=item.User?"你":UiTheme.Name+"  ·  随时在你身边";((Label)panel.Controls[0]).Width=width-34;
            text.SetBounds(17,38,width-34,50);
            if(signatures[i]!=item.Text){text.Text=item.Text;signatures[i]=item.Text;}
            text.SelectionStart=0;text.ScrollToCaret();
            var first=text.GetPositionFromCharIndex(0);var last=text.GetPositionFromCharIndex(text.TextLength);
            int height=Math.Clamp(last.Y-first.Y+text.Font.Height+16,35,1400);
            text.Height=height;text.ScrollBars=height>=1400?RichTextBoxScrollBars.Vertical:RichTextBoxScrollBars.None;
            panel.SetBounds(item.User?available-width+8:8,y,width,height+54);text.Invalidate();panel.Invalidate();y+=panel.Height+18;
        }
        AutoScrollMinSize=new Size(0,y+10);ResumeLayout();
        base.Text=string.Join("\n",data.Select(x=>x.Text));
        AutoScrollPosition=new Point(0,follow?y:savedScroll);
        } finally { rendering=false; }
    }
    protected override void Dispose(bool disposing){if(disposing){bodyFont.Dispose();labelFont.Dispose();}base.Dispose(disposing);}
}
class BarcaWindow:Form
{
    protected bool CanResize=false;
    protected override void OnShown(EventArgs e){if(!FeatureFaults.IsTestMode)FeatureFaults.Run("窗口显示",()=>WindowInteraction.Prepare(this));base.OnShown(e);if(!FeatureFaults.IsTestMode)FeatureFaults.Run("输入焦点",()=>WindowInteraction.FocusInput(this));}
    protected override bool ProcessCmdKey(ref Message message,Keys key){if(key is Keys.BrowserBack||key==(Keys.Alt|Keys.Left)){if(ActiveControl is not TextBoxBase&&WindowInteraction.Back(this))return true;}return base.ProcessCmdKey(ref message,key);}
    protected override void WndProc(ref Message m)
    {
        if(!CanResize&&m.Msg==0x112&&(m.WParam.ToInt64()&0xFFF0) is 0xF000 or 0xF030){m.Result=IntPtr.Zero;return;}
        base.WndProc(ref m);
        if(CanResize&&FormBorderStyle==FormBorderStyle.None&&m.Msg==0x84&&WindowState==FormWindowState.Normal){var p=PointToClient(new Point(unchecked((short)(long)m.LParam),unchecked((short)((long)m.LParam>>16))));m.Result=(IntPtr)ResizeHit(p,ClientSize);}
        if(!CanResize&&m.Msg==0x84&&m.Result.ToInt64() is >=10 and <=17)m.Result=(IntPtr)1;
    }
    internal static int ResizeHit(Point p,Size size){bool l=p.X<7,r=p.X>=size.Width-7,t=p.Y<7,b=p.Y>=size.Height-7;return t?(l?13:r?14:12):b?(l?16:r?17:15):l?10:r?11:1;}
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);if(CanResize){int dark=1;try{DwmSetWindowAttribute(Handle,20,ref dark,4);}catch{}}}
}
