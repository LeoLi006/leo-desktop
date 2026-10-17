using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace LeoLauncher;
static class QQPlayback
{
    const string Script = """
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object System.Text.UTF8Encoding($false)
try {
 Add-Type -AssemblyName System.Runtime.WindowsRuntime
 $type=[Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager,Windows.Media.Control,ContentType=WindowsRuntime]
 $method=[System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {$_.Name -eq 'AsTask' -and $_.IsGenericMethodDefinition -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'} | Select-Object -First 1
 $task=$method.MakeGenericMethod($type).Invoke($null,@($type::RequestAsync()))
 if(-not $task.Wait(4000)){throw '读取媒体状态超时'}
 $session=$task.Result.GetSessions() | Where-Object {$_.SourceAppUserModelId -eq 'QQMusic.exe'} | Select-Object -First 1
 if(-not $session){throw '未找到QQ音乐播放会话，请先打开QQ音乐并选择歌曲'}
 $mode='__ACTION__'
 $before=$session.GetPlaybackInfo().PlaybackStatus.ToString()
 if($mode -eq 'toggle'){$mode=if($before -eq 'Playing'){'pause'}else{'play'}}
 $target=if($mode -eq 'pause'){'Paused'}else{'Playing'}
 if($before -ne $target){
  $op=if($mode -eq 'pause'){$session.TryPauseAsync()}else{$session.TryPlayAsync()}
  $control=$method.MakeGenericMethod([bool]).Invoke($null,@($op))
  if(-not $control.Wait(4000)){throw 'QQ音乐响应超时'}
  if(-not $control.Result){throw 'QQ音乐未接受控制，请在客户端确认歌曲可播放'}
  for($i=0;$i -lt 10;$i++){Start-Sleep -Milliseconds 100;if($session.GetPlaybackInfo().PlaybackStatus.ToString() -eq $target){break}}
 }
 $confirmed=$session.GetPlaybackInfo().PlaybackStatus.ToString() -eq $target
 $message=if($confirmed){if($mode -eq 'pause'){'QQ音乐已暂停'}else{'QQ音乐已继续播放'}}else{'已向QQ音乐发送播放控制，请在客户端确认'}
 @{ok=$true;message=$message;confirmed=$confirmed}|ConvertTo-Json -Compress
} catch {@{ok=$false;message=$_.Exception.Message}|ConvertTo-Json -Compress;exit 1}
""";
    public static Task<string> ToggleAsync()=>SetPausedAsync(null);
    public static async Task<string> SetPausedAsync(bool? paused)
    {
        string action=paused==null?"toggle":paused.Value?"pause":"play";
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe")){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        foreach(string arg in new[]{"-NoLogo","-NoProfile","-NonInteractive","-EncodedCommand",Convert.ToBase64String(Encoding.Unicode.GetBytes(Script.Replace("__ACTION__",action)))})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)??throw new IOException("无法启动QQ音乐媒体控制");
        var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){try{process.Kill(true);}catch{}throw new TimeoutException("QQ音乐播放控制超时，请稍后重试");}
        string json=(await output).Trim();_=await errors;
        if(json.Length==0)throw new IOException("无法读取QQ音乐媒体状态");
        using var result=JsonDocument.Parse(json);string message=result.RootElement.GetProperty("message").GetString()??"QQ音乐未响应";
        if(!result.RootElement.GetProperty("ok").GetBoolean())throw new IOException(message);
        return message;
    }
}
