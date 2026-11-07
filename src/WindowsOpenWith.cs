using System.Runtime.InteropServices;
namespace LeoLauncher;

// Delegate actual file-type recommendations and selection to Windows, not an app-name list.
static class WindowsOpenWith
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct OpenAsInfo{[MarshalAs(UnmanagedType.LPWStr)]public string File;public IntPtr Class;public uint Flags;}
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,ExactSpelling=true,PreserveSig=true)]
    static extern int SHOpenWithDialog(IntPtr owner,ref OpenAsInfo info);
    public static bool Show(string path,IntPtr owner=default)
    {
        path=FileDropActions.RequirePath(path);if(Directory.Exists(path))throw new ArgumentException("Windows 打开方式仅支持单个文件。");
        var info=new OpenAsInfo{File=path,Flags=4}; // OAIF_EXEC; no default-association registration flags.
        int result=SHOpenWithDialog(owner,ref info);
        if(result==unchecked((int)0x800704C7))return false;
        if(result<0)Marshal.ThrowExceptionForHR(result);
        return true;
    }
}
