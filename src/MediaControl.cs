using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
namespace LeoLauncher;

sealed record MediaSession(string Id,string Provider,string Title,string Artist,string Status){public override string ToString()=>Provider+" · "+(Title.Length>0?Title:"未提供曲目名");}
interface IMediaProvider
{
    string Id{get;}Task<MediaSession[]> ReadAsync(CancellationToken token);Task<string> ControlAsync(MediaSession session,string command,CancellationToken token);
}
sealed class MediaService(IEnumerable<IMediaProvider> providers)
{
    public string? LastError{get;private set;}
    readonly IMediaProvider[] sources=providers.ToArray();public string SelectedId{get;set;}="";
    public async Task<MediaSession[]> ReadAsync(CancellationToken token){LastError=null;var list=new List<MediaSession>();foreach(var source in sources){try{list.AddRange(await source.ReadAsync(token));}catch(OperationCanceledException){throw;}catch(Exception e)when(e is IOException or JsonException or InvalidOperationException or System.ComponentModel.Win32Exception){LastError=e.Message;if(source.Id!="smtc")throw;}}if(list.Any(s=>s.Id.StartsWith("smtc:")&&s.Provider.Contains("PotPlayer",StringComparison.OrdinalIgnoreCase)))list.RemoveAll(s=>s.Id.StartsWith("potplayer:"));return list.ToArray();}
    public async Task<string> ExecuteAsync(string command,string id,CancellationToken token){if(command is not("toggle" or "play" or "pause" or "next" or "previous" or "stop"))throw new ArgumentException("不支持的媒体指令");var sessions=await ReadAsync(token);var target=id.Length>0?sessions.FirstOrDefault(s=>s.Id==id):sessions.FirstOrDefault(s=>s.Id==SelectedId)??sessions.FirstOrDefault(s=>s.Status=="Playing")??sessions.FirstOrDefault(s=>s.Id.StartsWith("potplayer:"))??sessions.FirstOrDefault();if(target==null)throw new ArgumentException(id.Length>0?"所选播放器已关闭，请重新选择。":"没有可控制的播放器。请打开PotPlayer并选择媒体，或在QQ音乐中播放一首歌。");SelectedId=target.Id;var provider=sources.Single(s=>target.Id.StartsWith(s.Id+":"));return await provider.ControlAsync(target,command,token);}
}
static class MediaControl
{
    internal static Func<IEnumerable<AppEntry>>? Applications;
    internal static MediaService Service{get;set;}=new([new WindowsMediaProvider(),new PotPlayerProvider()]);
    public static Task<MediaSession[]> SessionsAsync(CancellationToken token=default)=>Service.ReadAsync(token);
    public static Task<string> ExecuteAsync(string command,string id="",CancellationToken token=default)=>ClientOperations.Hub.Run("media",command+":"+id,op=>{op.Report("正在控制播放器…");return Service.ExecuteAsync(command,id,op.Token);},token);
    public static string? PotPlayerPath(){var roots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)};var selected=ModulePaths.Selected("media");string? path=selected??roots.SelectMany(r=>new[]{Path.Combine(r,"DAUM","PotPlayer","PotPlayerMini64.exe"),Path.Combine(r,"DAUM","PotPlayer","PotPlayerMini.exe"),Path.Combine(r,"PotPlayer","PotPlayerMini64.exe")}).FirstOrDefault(File.Exists);if(path==null){foreach(string key in new[]{"PotPlayerMini64.exe","PotPlayerMini.exe"}){using var app=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\"+key);if(app?.GetValue("") is string candidate&&File.Exists(candidate)){path=candidate;break;}}}if(path==null)path=Applications?.Invoke().Where(a=>a.Name.Contains("PotPlayer",StringComparison.OrdinalIgnoreCase)).Select(a=>a.ExePath).FirstOrDefault(p=>File.Exists(p)&&new[]{"PotPlayerMini64.exe","PotPlayerMini.exe","PotPlayer64.exe","PotPlayer.exe"}.Contains(Path.GetFileName(p),StringComparer.OrdinalIgnoreCase));return path;}
    public static void OpenPotPlayer(string? file=null){var path=PotPlayerPath();if(path==null)throw new IOException("未找到PotPlayer，请在设置 → 功能 → 应用模块与图标中指定程序。");var start=new ProcessStartInfo(path){UseShellExecute=true};if(file!=null){if(!File.Exists(file))throw new IOException("媒体文件不存在");start.ArgumentList.Add(file);}Process.Start(start);}
}
sealed class WindowsMediaProvider:IMediaProvider
{
    public string Id=>"smtc";
    const string Script="""
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object System.Text.UTF8Encoding($false)
try {
 Add-Type -AssemblyName System.Runtime.WindowsRuntime
 $type=[Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager,Windows.Media.Control,ContentType=WindowsRuntime]
 $method=[System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {$_.Name -eq 'AsTask' -and $_.IsGenericMethodDefinition -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'} | Select-Object -First 1
 function Await($operation,$resultType){$task=$method.MakeGenericMethod($resultType).Invoke($null,@($operation));if(-not $task.Wait(3500)){throw '媒体响应超时'};return $task.Result}
 $manager=Await ($type::RequestAsync()) $type
 $mode='__MODE__'
 $id=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__ID__'))
 if($mode -eq 'list'){
  $rows=@();foreach($session in $manager.GetSessions()){
   $props=$null;try{$props=Await ($session.TryGetMediaPropertiesAsync()) ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties,Windows.Media.Control,ContentType=WindowsRuntime])}catch{}
   $rows+=@{Id=('smtc:'+$session.SourceAppUserModelId);Provider=$session.SourceAppUserModelId;Title=$(if($props){$props.Title}else{''});Artist=$(if($props){$props.Artist}else{''});Status=$session.GetPlaybackInfo().PlaybackStatus.ToString()}
  };@{ok=$true;sessions=$rows}|ConvertTo-Json -Depth 4 -Compress
 }else{
  $session=$manager.GetSessions()|Where-Object {$_.SourceAppUserModelId -eq $id}|Select-Object -First 1
  if(-not $session){throw '播放器会话已关闭'}
  $op=switch($mode){'toggle'{$session.TryTogglePlayPauseAsync()};'play'{$session.TryPlayAsync()};'pause'{$session.TryPauseAsync()};'next'{$session.TrySkipNextAsync()};'previous'{$session.TrySkipPreviousAsync()};'stop'{$session.TryStopAsync()};default{throw '不支持的媒体操作'}}
  $accepted=Await $op ([bool]);if(-not $accepted){throw '播放器未接受此操作，可能没有上一项或下一项'}
  @{ok=$true;message='媒体指令已发送，请以播放器状态为准'}|ConvertTo-Json -Compress
 }
}catch{@{ok=$false;message=$_.Exception.Message}|ConvertTo-Json -Compress;exit 1}
""";
    public async Task<MediaSession[]> ReadAsync(CancellationToken token){using var result=await Run("list","",token);return (result.RootElement.GetProperty("sessions").Deserialize<MediaSession[]>()??[]).Select(s=>s with{Title=s.Title??"",Artist=s.Artist??"",Provider=DisplayName(s.Provider)}).ToArray();}
    static string DisplayName(string? source)=>Path.GetFileNameWithoutExtension(source??"") switch{"QQMusic"=>"QQ音乐","msedge"=>"Microsoft Edge","chrome"=>"Chrome","PotPlayerMini64" or "PotPlayerMini" or "PotPlayer64"=>"PotPlayer",var other=>other.Length==0?"播放器":other};
    public async Task<string> ControlAsync(MediaSession session,string command,CancellationToken token){using var result=await Run(command,session.Id[5..],token);return session.Provider+" · "+result.RootElement.GetProperty("message").GetString();}
    static async Task<JsonDocument> Run(string command,string id,CancellationToken token){var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe")){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8};string script=Script.Replace("__MODE__",command).Replace("__ID__",Convert.ToBase64String(Encoding.UTF8.GetBytes(id)));foreach(string a in new[]{"-NoLogo","-NoProfile","-NonInteractive","-EncodedCommand",Convert.ToBase64String(Encoding.Unicode.GetBytes(script))})start.ArgumentList.Add(a);using var process=OwnedHelpers.Start(start);using var owned=OwnedHelpers.Lease(process);var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(15000);try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){try{process.Kill(true);}catch{}token.ThrowIfCancellationRequested();throw new IOException("读取媒体会话超时");}string json=(await output).Trim();_=await errors;if(json.Length==0)throw new IOException("媒体接口没有返回内容");var doc=JsonDocument.Parse(json);if(!doc.RootElement.GetProperty("ok").GetBoolean()){string message=doc.RootElement.GetProperty("message").GetString()??"媒体接口不可用";doc.Dispose();throw new IOException(message);}return doc;}
}
sealed class PotPlayerProvider:IMediaProvider
{
    public string Id=>"potplayer";
    public static int CommandCode(string command)=>command switch{"toggle"=>14,"play"=>46,"pause"=>47,"next"=>11,"previous"=>12,"stop"=>13,_=>throw new ArgumentException("不支持的播放器操作")};
    static bool Known(Process p)=>p.ProcessName is "PotPlayerMini64" or "PotPlayerMini" or "PotPlayer64" or "PotPlayer";
    public Task<MediaSession[]> ReadAsync(CancellationToken token){token.ThrowIfCancellationRequested();var rows=new List<MediaSession>();foreach(var p in Process.GetProcesses())using(p)try{if(Known(p)&&p.MainWindowHandle!=IntPtr.Zero)rows.Add(new("potplayer:"+p.Id,"PotPlayer",p.MainWindowTitle,"","Unknown"));}catch(InvalidOperationException){}return Task.FromResult(rows.ToArray());}
    public Task<string> ControlAsync(MediaSession session,string command,CancellationToken token){token.ThrowIfCancellationRequested();if(!int.TryParse(session.Id[10..],out int pid))throw new ArgumentException("播放器标识无效");using var p=Process.GetProcessById(pid);if(!Known(p)||p.MainWindowHandle==IntPtr.Zero)throw new IOException("PotPlayer已关闭");Send(p.MainWindowHandle,command);return Task.FromResult("已向PotPlayer发送"+(command switch{"toggle"=>"播放/暂停","play"=>"播放","pause"=>"暂停","next"=>"下一项","previous"=>"上一项",_=>"停止"})+"指令，请以播放器状态为准。");}
    internal static void Send(IntPtr window,string command){if(SendMessageTimeout(window,0x319,window,(IntPtr)(CommandCode(command)<<16),2,1500,out _)==IntPtr.Zero)throw new IOException("播放器响应超时，或权限不一致，请检查播放器是否以管理员运行。");}
    [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SendMessageTimeout(IntPtr hwnd,int msg,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
}
