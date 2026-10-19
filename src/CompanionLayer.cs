using System.ComponentModel;
using System.Runtime.InteropServices;
namespace LeoLauncher;

static class CompanionLayer
{
    [DllImport("user32.dll")]internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetStyle(IntPtr window,int index);
    [DllImport("user32.dll")]internal static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]internal static extern bool IsWindowEnabled(IntPtr window);
    [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")]internal static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll",SetLastError=true)]internal static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    internal static bool IsTopmost(Form form)=>form.IsHandleCreated&&(GetStyle(form.Handle,-20)&8)!=0;
    public static bool OwnForeground(IntPtr window){GetWindowThreadProcessId(window,out var pid);return pid==Environment.ProcessId;}
    public static void Repair(Form form,bool reorder,Form? owner=null)
    {
        // WinForms modal loops disable the native owner without changing Form.Enabled.
        if(form.IsDisposed||form.Disposing||!form.IsHandleCreated||!form.Enabled||!IsWindowEnabled(form.Handle))return;
        bool missing=!form.Visible||!IsWindowVisible(form.Handle)||IsIconic(form.Handle);
        if(!form.Visible){if(owner!=null)form.Show(owner);else form.Show();}
        if(missing)ShowWindow(form.Handle,4); // Restore without activating the companion.
        if(!IsTopmost(form)||reorder||missing){
            if(!SetWindowPos(form.Handle,new IntPtr(-1),0,0,0,0,0x1|0x2|0x10|0x200))throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
sealed partial class PetForm
{
    IntPtr lastLayerForeground;
    void RestoreCompanionLayer()
    {
        if(resourcesDisposed||hiddenForFullscreen||IsDisposed||Disposing||!IsHandleCreated||!Enabled||!CompanionLayer.IsWindowEnabled(Handle))return;
        var foreground=CompanionLayer.GetForegroundWindow();bool reorder=foreground!=lastLayerForeground&&!CompanionLayer.OwnForeground(foreground);lastLayerForeground=foreground;
        CompanionLayer.Repair(this,reorder);
        if(dockMotion.Progress<.001f)CompanionLayer.Repair(foot,reorder,this);
        CompanionLayer.Repair(hit,reorder,this);
        if(launcher.Visible)CompanionLayer.Repair(launcher,reorder);
    }
}
