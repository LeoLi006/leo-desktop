using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
namespace LeoLauncher;

// Team tokens are shared by custom painting, ordinary WinForms controls and menus.
static class UiTheme
{
    internal sealed record Palette(string Id,string Team,string Motto,System.Drawing.Color Primary,System.Drawing.Color Secondary,System.Drawing.Color Accent);
    static System.Drawing.Color R(int r,int g,int b)=>System.Drawing.Color.FromArgb(r,g,b);
    internal static readonly Palette[] Palettes=[
        new("83","巴萨","Visca el Barça!",R(37,75,132),R(130,34,67),R(231,194,119)),
        new("202","阿根廷","¡Vamos Argentina!",R(65,131,177),R(111,166,202),R(160,213,244)),
        new("164","西班牙","¡Vamos España!",R(148,35,51),R(110,72,27),R(255,207,93)),
        new("20232","迈阿密国际","Freedom to dream",R(130,63,96),R(79,47,68),R(255,178,210)),
        new("382","曼城","Blue Moon",R(46,108,150),R(53,81,111),R(142,212,249))];
    static Palette active=Palettes[0];
    static readonly Dictionary<int,System.Drawing.Color> originals=new();
    static readonly ConditionalWeakTable<Control,Style> styles=new();
    static readonly ConditionalWeakTable<ToolStripItem,ItemStyle> items=new();
    static bool started;
    static readonly ConditionalWeakTable<Form,object> seen=new();
    public static string Skin=>active.Id;
    public static string Name{get;private set;}="Leo";
    public static string TeamName=>active.Team;
    public static string Motto=>active.Motto;
    public static string ValidateName(string? value){var name=(value??"").Trim();if(name.Length is <1 or >24||name.Any(char.IsControl))throw new ArgumentException("名字请填写 1～24 个字符，不含换行或控制字符。");return name;}
    public static string Rename(string text)=>Regex.Replace(text,@"\bLeo\b|\bLEO\b",_=>Name).Replace("BLAUGRANA",TeamName).Replace("Visca el Barça!",Motto);
    static bool Branded(string text)=>Regex.IsMatch(text,@"^(Leo|LEO)(\b|\s)|^问 Leo|^退出 Leo|^把 Leo|^登录 Windows 后自动启动 Leo|^按.*Leo 设置|BLAUGRANA|Visca el Barça!");
    static System.Drawing.Color Mix(System.Drawing.Color a,System.Drawing.Color b,double t)=>R((int)(a.R*(1-t)+b.R*t),(int)(a.G*(1-t)+b.G*t),(int)(a.B*(1-t)+b.B*t));
    public static System.Drawing.Color Color(int r,int g,int b){var raw=R(r,g,b);originals[raw.ToArgb()]=raw;return Map(raw);}
    public static System.Drawing.Color Color(int alpha,int r,int g,int b)=>System.Drawing.Color.FromArgb(alpha,Color(r,g,b));
    public static System.Drawing.Color Color(int alpha,System.Drawing.Color color)=>System.Drawing.Color.FromArgb(alpha,color);
    static System.Drawing.Color Map(System.Drawing.Color raw)
    {
        if(active.Id=="83"||raw.A==0||raw==System.Drawing.Color.LightSalmon||raw==System.Drawing.Color.OrangeRed)return raw;
        var c=R(raw.R,raw.G,raw.B);System.Drawing.Color result;
        // Status green, warning/error red and neutral white stay legible and meaningful.
        if(c.G>c.R*1.15&&c.G>c.B*1.1||c.R>180&&c.G<140&&c.B<140)return raw;
        if(c.R>190&&c.G>145&&c.B<155)result=active.Accent;
        else if(c.R>210&&c.G>210&&c.B>210)result=R(239,244,249);
        else if(c.R>100&&c.G>100&&c.B>100)result=Mix(R(150,161,178),active.Accent,.15);
        else if(c.R>c.G*1.35&&c.R>55)result=Mix(R(11,16,24),active.Secondary,Math.Clamp(c.R/150d,.22,1));
        else if(c.B>c.R*1.5&&c.B>95)result=Mix(R(11,16,24),active.Primary,Math.Clamp(c.B/132d,.3,1));
        else {double level=Math.Clamp((c.R+c.G+c.B)/3d/90,.12,.78);result=Mix(R(10,14,21),active.Primary,level*.48);}
        return System.Drawing.Color.FromArgb(raw.A,result);
    }
    static System.Drawing.Color Canonical(System.Drawing.Color value)
    {
        if(value.IsEmpty||value.A==0)return value;
        foreach(var raw in originals.Values)if(Map(raw).ToArgb()==value.ToArgb())return raw;
        return value;
    }
    public static void Start(){if(started)return;started=true;Application.Idle+=(_,_)=>{foreach(Form f in Application.OpenForms.Cast<Form>().ToArray())if(!seen.TryGetValue(f,out _)){seen.Add(f,new object());Attach(f);}};}
    public static void Configure(string id,string? name)
    {
        var next=Palettes.FirstOrDefault(p=>p.Id==id)??Palettes[0];string valid;try{valid=ValidateName(name);}catch{valid="Leo";}
        if(next==active&&valid==Name)return;
        // Capture controls under the old palette before changing tokens.
        RefreshAll();active=next;Name=valid;RefreshAll();
    }
    internal static void RefreshAll(){foreach(Form form in Application.OpenForms.Cast<Form>().ToArray())Attach(form);}
    internal static void Attach(Control control)
    {
        if(control.IsDisposed)return;
        bool sprite=control is PetForm or HitSurface;
        if(!sprite){var state=styles.GetValue(control,c=>new Style(c));state.Apply();}
        if(control.ContextMenuStrip is { } menu)Attach(menu);
        if(control is ToolStrip strip){if(strip.Renderer is not BarcaMenuRenderer)strip.Renderer=new BarcaMenuRenderer();foreach(ToolStripItem item in strip.Items)ApplyItem(item);}
        foreach(Control child in control.Controls)Attach(child);
    }
    static void ApplyItem(ToolStripItem item){if(item.IsDisposed)return;items.GetValue(item,i=>new ItemStyle(i)).Apply();if(item is ToolStripMenuItem entry&&entry.HasDropDownItems){entry.DropDown.Renderer=new BarcaMenuRenderer();entry.DropDown.BackColor=BarcaUi.Surface;entry.DropDown.ForeColor=BarcaUi.Text;foreach(ToolStripItem child in entry.DropDownItems)ApplyItem(child);}}
    sealed class ItemStyle
    {
        readonly ToolStripItem item;string? text;string rendered="";
        public ItemStyle(ToolStripItem i){item=i;}
        public void Apply(){item.BackColor=BarcaUi.Surface;item.ForeColor=BarcaUi.Text;if(item.Text!=rendered){text=Branded(item.Text??"")?item.Text:null;}if(text!=null){rendered=Rename(text);item.Text=rendered;}}
    }
    [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    static void Caption(Form form){if(!form.IsHandleCreated||form.FormBorderStyle==FormBorderStyle.None)return;try{foreach(var pair in new[]{(35,BarcaUi.Canvas),(36,BarcaUi.Text),(34,BarcaUi.Line)}){int value=pair.Item2.R|(pair.Item2.G<<8)|(pair.Item2.B<<16);DwmSetWindowAttribute(form.Handle,pair.Item1,ref value,4);}}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
    sealed class Style
    {
        readonly Control control;System.Drawing.Color back,fore;bool applying;string? text;string rendered="";string skin="",name="";
        public Style(Control c){control=c;back=Canonical(c.BackColor);fore=Canonical(c.ForeColor);c.BackColorChanged+=(_,_)=>{if(!applying)back=Canonical(c.BackColor);};c.ForeColorChanged+=(_,_)=>{if(!applying)fore=Canonical(c.ForeColor);};c.ControlAdded+=(_,e)=>{if(e.Control!=null)Attach(e.Control);};c.HandleCreated+=(_,_)=>Apply();if(c is ComboBox combo&&combo.DrawMode==DrawMode.Normal)CompanionUi.ThemeCombo(combo);c.TextChanged+=(_,_)=>{if(!applying)CaptureText();};if(c is ToolStrip strip)strip.ItemAdded+=(_,e)=>ApplyItem(e.Item!);CaptureText();}
        void CaptureText(){if(control is TextBoxBase or ListControl)return;var current=control.Text;if(current==rendered)return;text=Branded(current)?current:null;}
        public void Apply(){if(control.IsDisposed)return;applying=true;try{
            var bg=back==SystemColors.Control||back==SystemColors.Window?BarcaUi.Canvas:Map(back);
            var fg=fore==SystemColors.ControlText||fore==SystemColors.WindowText?BarcaUi.Text:Map(fore);
            if(control is ButtonBase button){button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.CheckedBackColor=Appearance.Blue;if(button is Button){button.FlatAppearance.BorderColor=BarcaUi.Line;button.FlatAppearance.MouseOverBackColor=BarcaUi.Raised;button.FlatAppearance.MouseDownBackColor=Appearance.Blue;}if(back==SystemColors.Control)bg=BarcaUi.Surface;}
            if(control.BackColor!=bg)control.BackColor=bg;if(control.ForeColor!=fg)control.ForeColor=fg;
            if(text!=null){rendered=Rename(text);if(control.Text!=rendered)control.Text=rendered;}
            if(control.AccessibleName is string label&&Branded(label))control.AccessibleName=Rename(label);
            if(control is DateTimePicker date){date.CalendarMonthBackground=BarcaUi.Surface;date.CalendarForeColor=BarcaUi.Text;date.CalendarTitleBackColor=Appearance.Blue;date.CalendarTitleForeColor=BarcaUi.Text;date.CalendarTrailingForeColor=BarcaUi.Muted;}
            if(skin!=Skin||name!=Name){if(control is Form window)Caption(window);skin=Skin;name=Name;control.Invalidate(true);}
        }finally{applying=false;}}
    }
}
