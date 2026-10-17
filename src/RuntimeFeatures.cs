namespace LeoLauncher;

static class Modules
{
    public static string? ForAction(string action)=>action.StartsWith("music_")?"music":action.StartsWith("proxy_")?"clash":action=="terminal_open"?"terminal":action is "web_search" or "watch_open"?"edge":null;
    public static bool Enabled(Preferences p,string module)=>module switch{"terminal"=>p.TerminalModule,"clash"=>p.ClashModule,"music"=>p.MusicModule,"edge"=>p.EdgeModule,_=>true};
    public static void Require(Preferences p,string? module){if(module!=null&&!Enabled(p,module))throw new InvalidOperationException("此功能已停用，请在设置 → 功能中开启。");}
}

sealed class EscapeWindows:IMessageFilter
{
    public bool PreFilterMessage(ref Message message)
    {
        if(message.Msg!=0x100||(Keys)message.WParam.ToInt32()!=Keys.Escape)return false;
        var control=Control.FromChildHandle(message.HWnd);
        if(control is ToolStripDropDown||control is ComboBox{DroppedDown:true})return false;
        var window=control?.FindForm();
        while(window!=null&&!window.TopLevel)window=window.Parent?.FindForm();
        if(window!=null&&HasMenu(window))return false;
        if(window==null||!window.TopLevel||window is PetForm or HitSurface)return false;
        return Dismiss(window);
    }
    static bool HasMenu(Control c)=>c.ContextMenuStrip?.Visible==true||c.Controls.Cast<Control>().Any(HasMenu);
    internal static bool Dismiss(Form window)
    {
        if(window is FootTools tools){tools.CollapseSearch();return true;}
        if(window is LauncherForm){window.Hide();return true;}
        window.Close();return true;
    }
}

static class EdgeDock
{
    public static Point Snap(Rectangle pet,Rectangle area,int footer,int threshold=26)
    {
        int x=pet.X,y=pet.Y;
        int right=area.Right-pet.Width,bottom=area.Bottom-pet.Height-footer;
        if(Math.Abs(x-area.Left)<=threshold)x=area.Left;else if(Math.Abs(x-right)<=threshold)x=right;
        if(Math.Abs(y-area.Top)<=threshold)y=area.Top;else if(Math.Abs(y-bottom)<=threshold)y=bottom;
        return new Point(x,y);
    }
}
