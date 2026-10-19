using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace LeoLauncher;

// Paint the native scrollbar in place: Windows retains wheel, keyboard and thumb hit testing.
static class BarcaScrollTheme
{
    static readonly ConditionalWeakTable<Control, ScrollSkin> skins=new();
    static bool started;
    internal static int AttachmentCount{get;private set;}
    public static void Start(){if(started)return;started=true;Application.Idle+=(_,_)=>{foreach(Form form in Application.OpenForms.Cast<Form>().ToArray())if(!skins.TryGetValue(form,out _))FeatureFaults.Run("滚动条主题",()=>Attach(form));};}
    internal static void Attach(Control control)
    {
        if(control.IsDisposed)return;
        if(skins.TryGetValue(control,out _))return;
        if(!skins.TryGetValue(control,out _)){
            skins.Add(control,new ScrollSkin(control));
            AttachmentCount++;
            control.ControlAdded+=(_,e)=>{if(e.Control!=null)Attach(e.Control);};
        }
        foreach(Control child in control.Controls)Attach(child);
    }
    sealed class ScrollSkin:NativeWindow
    {
        readonly Control control;
        bool painting,disabled;
        public ScrollSkin(Control owner){control=owner;owner.HandleCreated+=(_,_)=>Connect();owner.HandleDestroyed+=(_,_)=>ReleaseHandle();if(owner.IsHandleCreated)Connect();}
        void Connect(){AssignHandle(control.Handle);try{SetWindowTheme(Handle,"DarkMode_Explorer",null);}catch{}}
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if(!painting&&!disabled&&m.Msg is 0x85 or 0xF or 0x115 or 0x114 or 0x20A or 0xA0 or 0xA1 or 0xA2 or 0x200 or 0x2A2 or 0x113 or 0x5){
                painting=true;try{Paint(-5,true);Paint(-6,false);}catch(Exception e)when(FeatureFaults.Recoverable(e)){disabled=true;FeatureFaults.Report("单个滚动条绘制",e);}finally{painting=false;}
            }
        }
        void Paint(int id,bool vertical)
        {
            if(Handle==IntPtr.Zero||control.IsDisposed)return;
            var info=new ScrollInfo{Size=Marshal.SizeOf<ScrollInfo>(),States=new uint[6]};
            if(!GetScrollBarInfo(Handle,id,ref info)||(info.States[0]&0x8000)!=0||!GetWindowRect(Handle,out var window))return;
            var rect=Rectangle.FromLTRB(info.Bounds.Left-window.Left,info.Bounds.Top-window.Top,info.Bounds.Right-window.Left,info.Bounds.Bottom-window.Top);
            if(rect.Width<1||rect.Height<1)return;
            var dc=GetWindowDC(Handle);if(dc==IntPtr.Zero)return;
            try{using var g=Graphics.FromHdc(dc);using var track=new SolidBrush(BarcaUi.Canvas);g.FillRectangle(track,rect);
                var thumb=vertical?new Rectangle(rect.X+4,rect.Y+info.ThumbTop,Math.Max(3,rect.Width-8),info.ThumbBottom-info.ThumbTop):new Rectangle(rect.X+info.ThumbTop,rect.Y+4,info.ThumbBottom-info.ThumbTop,Math.Max(3,rect.Height-8));
                if(thumb.Width>0&&thumb.Height>0){g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var shape=Appearance.Path(thumb,Math.Min(5,Math.Min(thumb.Width,thumb.Height)/2));using var fill=new System.Drawing.Drawing2D.LinearGradientBrush(thumb,Appearance.Blue,Appearance.Red,vertical?90f:0f);g.FillPath(fill,shape);}
                using var pen=new Pen(BarcaUi.Gold,1.5f);int a=info.LineButton/2;
                if(vertical){int x=rect.Left+rect.Width/2;g.DrawLines(pen,[new(x-3,rect.Top+a+1),new(x,rect.Top+a-2),new(x+3,rect.Top+a+1)]);g.DrawLines(pen,[new(x-3,rect.Bottom-a-2),new(x,rect.Bottom-a+1),new(x+3,rect.Bottom-a-2)]);}
                else{int y=rect.Top+rect.Height/2;g.DrawLines(pen,[new(rect.Left+a+1,y-3),new(rect.Left+a-2,y),new(rect.Left+a+1,y+3)]);g.DrawLines(pen,[new(rect.Right-a-2,y-3),new(rect.Right-a+1,y),new(rect.Right-a-2,y+3)]);}
            }finally{ReleaseDC(Handle,dc);}
        }
    }
    [StructLayout(LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]struct ScrollInfo{public int Size;public Rect Bounds;public int LineButton,ThumbTop,ThumbBottom,Reserved;[MarshalAs(UnmanagedType.ByValArray,SizeConst=6)]public uint[] States;}
    [DllImport("user32.dll")]static extern bool GetScrollBarInfo(IntPtr hwnd,int id,ref ScrollInfo info);
    [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
    [DllImport("user32.dll")]static extern IntPtr GetWindowDC(IntPtr hwnd);
    [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
    [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)]static extern int SetWindowTheme(IntPtr hwnd,string app,string? list);
}

sealed class SpeechSurface:Panel
{
    public bool IsUser;
    public SpeechSurface(){DoubleBuffered=true;}
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g=e.Graphics;g.Clear(Parent?.BackColor??BarcaUi.Canvas);if(Width<3||Height<3)return;
        g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path=Appearance.Path(new Rectangle(1,1,Width-3,Height-3),15);using var fill=new SolidBrush(BackColor);g.FillPath(fill,path);
        using var outline=new Pen(IsUser?UiTheme.Color(44,68,104):UiTheme.Color(76,48,68));g.DrawPath(outline,path);
        using var accent=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(18,5,60,3),Appearance.Blue,Appearance.Red,0f);g.FillRectangle(accent,18,5,60,3);
    }
}
