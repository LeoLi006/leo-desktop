namespace LeoLauncher;

static class Modules
{
    static readonly object footballGate=new();static CancellationTokenSource footballLife=new();static bool footballEnabled=true;
    public static void SetFootball(bool enabled){lock(footballGate){if(enabled==footballEnabled)return;footballEnabled=enabled;if(!enabled)footballLife.Cancel();else{footballLife.Dispose();footballLife=new();}}}
    public static void RequireFootball(){lock(footballGate)if(!footballEnabled)throw new InvalidOperationException("足球已停用，请在设置 → 功能中开启。");}
    public static CancellationTokenSource FootballRequest(CancellationToken caller){lock(footballGate){RequireFootball();return CancellationTokenSource.CreateLinkedTokenSource(caller,footballLife.Token);}}

    public static string? ForAction(string action)=>action.StartsWith("football_")||action=="match_add"?"football":action.StartsWith("music_")?"music":action.StartsWith("proxy_")?"clash":action=="terminal_open"?"terminal":action is "web_search" or "watch_open"?"edge":null;
    public static bool Enabled(Preferences p,string module)=>module switch{"terminal"=>p.TerminalModule,"clash"=>p.ClashModule,"music"=>p.MusicModule,"football"=>p.FootballModule,"edge"=>p.EdgeModule,_=>true};
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
    public static DockSide ChooseSide(Rectangle pet,Rectangle area,int footer,Size movement,int threshold=26)
    {
        var horizontal=Math.Abs(pet.Left-area.Left)<=threshold?DockSide.Left:Math.Abs(pet.Right-area.Right)<=threshold?DockSide.Right:DockSide.None;
        var vertical=Math.Abs(pet.Top-area.Top)<=threshold?DockSide.Top:Math.Abs(pet.Bottom+footer-area.Bottom)<=threshold?DockSide.Bottom:DockSide.None;
        if(horizontal!=DockSide.None&&vertical!=DockSide.None)return Math.Abs(movement.Height)>Math.Abs(movement.Width)?vertical:horizontal;
        return horizontal!=DockSide.None?horizontal:vertical;
    }
    public static Point Snap(Rectangle pet,Rectangle area,int footer,int threshold=26,Size movement=default)
    {
        return ChooseSide(pet,area,footer,movement,threshold) switch{
            DockSide.Left=>new(area.Left,pet.Y),DockSide.Right=>new(area.Right-pet.Width,pet.Y),
            DockSide.Top=>new(pet.X,area.Top),DockSide.Bottom=>new(pet.X,area.Bottom-pet.Height-footer),_=>pet.Location};
    }
}
