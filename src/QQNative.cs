using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
namespace LeoLauncher;
static class QQNative
{
    const string EndpointClass="csQQMusicComApiWnd2017",EndpointTitle="QQMusic_COM_WND_B2DA2B76_8235_4739_8298_89D994522476";
    delegate bool EnumCallback(IntPtr h,IntPtr param);
    [StructLayout(LayoutKind.Sequential)]struct CopyData{public IntPtr Kind;public int Size;public IntPtr Bytes;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string cls,string title);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SendMessageTimeout(IntPtr h,uint msg,IntPtr w,ref CopyData data,uint flags,uint timeout,out IntPtr result);
    [DllImport("user32.dll")]static extern bool EnumWindows(EnumCallback callback,IntPtr param);
    [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr parent,EnumCallback callback,IntPtr param);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,StringBuilder text,int count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
    [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll",SetLastError=true)]static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern ushort GlobalFindAtom(string name);
    static (IntPtr Hwnd,Process Process) Endpoint(){var hwnd=FindWindow(EndpointClass,EndpointTitle);if(hwnd==IntPtr.Zero)throw new InvalidOperationException("请先启动QQ音乐；后台运行后即可静默点歌。");GetWindowThreadProcessId(hwnd,out uint id);var process=Process.GetProcessById((int)id);try{string? path=process.MainModule?.FileName;if(path==null||!Path.GetFileName(path).Equals("QQMusic.exe",StringComparison.OrdinalIgnoreCase))throw new IOException("QQ控制窗口身份不匹配");return(hwnd,process);}catch{process.Dispose();throw;}}
    public static string PlayCommand(MusicSong song){if(!QQMusic.Valid(song))throw new ArgumentException("歌曲编号无效");return "/playbysongid 'cmd_count==1&&id_0=="+song.Id+"&&songtype_0==0' /silent";}
    public static byte[] Packet(string command){if(command.Length>4096)throw new ArgumentException("控制命令过长");var text=Encoding.Unicode.GetBytes(command);using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);writer.Write(18+text.Length);writer.Write(0x514d4153);writer.Write((ushort)100);writer.Write(4);writer.Write(text.Length);writer.Write(text);return stream.ToArray();}
    public static ProcessStartInfo StartupRequest(string path)=>new(path){Arguments="/silent",UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden};
    static readonly SemaphoreSlim playbackGate=new(1,1);
    internal static bool Ready=>FindWindow(EndpointClass,EndpointTitle)!=IntPtr.Zero&&FindWindow("QQMusic_Daemon_Wnd",null!)!=IntPtr.Zero;
    internal static async Task EnsureReadyAsync(CancellationToken token,Action<string>? progress)
    {
        bool started=false;
        if(FindWindow(EndpointClass,EndpointTitle)==IntPtr.Zero){string path=QQMusic.ClientPath()??throw new InvalidOperationException("未找到QQ音乐，请先安装客户端");progress?.Invoke("正在静默启动 QQ音乐…");token.ThrowIfCancellationRequested();using var process=Process.Start(StartupRequest(path));if(process==null)throw new IOException("QQ音乐未能启动，请稍后重试");started=true;}
        progress?.Invoke("正在等待 QQ音乐就绪…");var watch=Stopwatch.StartNew();while(!Ready){token.ThrowIfCancellationRequested();if(watch.Elapsed>TimeSpan.FromSeconds(25))throw new TimeoutException("QQ音乐启动超时，请检查客户端是否需要登录或更新");await Task.Delay(250,token);}
        if(started)await Task.Delay(1000,token);
    }
    public static async Task PlayAsync(MusicSong song,CancellationToken token=default,Action<string>? progress=null)
    {
        _=PlayCommand(song);await playbackGate.WaitAsync(token);
        try{await EnsureReadyAsync(token,progress);token.ThrowIfCancellationRequested();progress?.Invoke("正在点歌并确认客户端接收…");await Task.Run(()=>Play(song),token);}finally{playbackGate.Release();}
    }
    public static void Play(MusicSong song){string command=PlayCommand(song);var target=Endpoint();using var process=target.Process;var bytes=Packet(command);IntPtr ptr=Marshal.AllocHGlobal(bytes.Length);try{Marshal.Copy(bytes,0,ptr,bytes.Length);var data=new CopyData{Size=bytes.Length,Bytes=ptr};if(SendMessageTimeout(target.Hwnd,0x4a,IntPtr.Zero,ref data,3,2000,out _)==IntPtr.Zero)throw new IOException("QQ音乐暂未响应点歌，请稍后重试");}finally{Marshal.FreeHGlobal(ptr);}}
    public static string PlaybackTitle()=>Title(FindWindow("QQMusic_Daemon_Wnd",null!));
    static string Class(IntPtr h){var b=new StringBuilder(256);GetClassName(h,b,256);return b.ToString();}
    static string Title(IntPtr h){var b=new StringBuilder(512);GetWindowText(h,b,512);return b.ToString();}
    static List<IntPtr> Windows(int pid){var windows=new List<IntPtr>();EnumWindows((h,_)=>{GetWindowThreadProcessId(h,out uint owner);if(owner==(uint)pid){windows.Add(h);EnumChildWindows(h,(child,_)=>{windows.Add(child);return true;},IntPtr.Zero);}return true;},IntPtr.Zero);return windows;}
    public static bool LyricsVisible(int pid)=>Windows(pid).Any(h=>IsWindowVisible(h)&&(Title(h)=="桌面歌词"||Title(h)=="DynamicLyricWindow"));
    public static async Task<string> ToggleLyrics(){var target=Endpoint();using var process=target.Process;ProcessModule? module=process.Modules.Cast<ProcessModule>().FirstOrDefault(m=>m.ModuleName.Equals("QQMusic.dll",StringComparison.OrdinalIgnoreCase));if(module?.FileVersionInfo.FileVersion!="21.21")throw new InvalidOperationException("当前QQ版本未验证原生歌词控制，请使用QQ音乐自己的桌面歌词快捷键。");string receiverClass="ATL:"+(module.BaseAddress.ToInt64()+0xad2d68).ToString("X");var receiver=Windows(process.Id).FirstOrDefault(h=>Class(h).Equals(receiverClass,StringComparison.OrdinalIgnoreCase));ushort atom=GlobalFindAtom("Tencent - QQMusicPlayer - 0x00030057");if(receiver==IntPtr.Zero||atom==0)throw new InvalidOperationException("未找到QQ歌词开关，请在QQ设置中启用默认的 Ctrl+Alt+W 歌词快捷键。");bool before=LyricsVisible(process.Id);if(!PostMessage(receiver,0x312,(IntPtr)atom,(IntPtr)(0x57*65536+3)))throw new IOException("QQ音乐未接收歌词开关");for(int i=0;i<20;i++){await Task.Delay(50);if(process.HasExited)break;bool after=LyricsVisible(process.Id);if(after!=before)return after?"已显示QQ音乐桌面歌词":"已隐藏QQ音乐桌面歌词";}return "已发送QQ歌词开关；请确保正在播放有歌词的歌曲。";}
}
