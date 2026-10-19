using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
namespace LeoLauncher;

static class ModuleWorker
{
    static readonly ConcurrentDictionary<string,int> active=new();
    public static IDisposable Reserve(string id){active.AddOrUpdate(id,1,(_,n)=>n+1);return new Reservation(id);}
    sealed class Reservation(string id):IDisposable{bool released;public void Dispose(){if(released)return;released=true;active.AddOrUpdate(id,0,(_,n)=>Math.Max(0,n-1));}}
    public static bool Running(string id)=>active.TryGetValue(id,out int count)&&count>0;
    public static async Task<string> ExecuteAsync(ExtensionStore store,ExtensionManifest manifest,ExtensionCommand command,CancellationToken token,string origin="extension")
    {
        token.ThrowIfCancellationRequested();string executable=store.ExecutablePath(manifest);var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(executable),RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8};start.ArgumentList.Add("--leo-module");active.AddOrUpdate(manifest.Id,1,(_,n)=>n+1);
        try{
            string data=Path.Combine(Path.GetDirectoryName(store.Root)!,"module-data",manifest.Id);Directory.CreateDirectory(data);
            using var process=OwnedHelpers.Start(start);using var owned=OwnedHelpers.Lease(process);using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(manifest.TimeoutSeconds));
            process.StandardInput.AutoFlush=true;
            var errorDrain=Drain(process.StandardError,timeout.Token);
            try{
                await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new{protocol="LeoModule/1",requestId=Guid.NewGuid().ToString("N"),command=command.Action[7..],value=command.Value,dataDirectory=data,origin,theme=UiTheme.ExportTokens()}).AsMemory(),timeout.Token);process.StandardInput.Close();
                string line=await ReadLine(process.StandardOutput,timeout.Token);using var reply=JsonDocument.Parse(line);if(!reply.RootElement.TryGetProperty("ok",out var ok)||ok.ValueKind is not(JsonValueKind.True or JsonValueKind.False)||!reply.RootElement.TryGetProperty("message",out var message)||message.ValueKind!=JsonValueKind.String)throw new IOException("扩展响应格式无效");string result=message.GetString()??"";if(result.Length>4000)throw new IOException("扩展响应超过4000字");if(!ok.GetBoolean())throw new IOException(result);await process.WaitForExitAsync(timeout.Token);if(process.ExitCode!=0)throw new IOException("扩展进程异常退出");return result;
            }catch(OperationCanceledException){token.ThrowIfCancellationRequested();throw new TimeoutException("扩展执行超时，已停止其进程。");}
            finally{timeout.Cancel();if(!process.HasExited)try{process.Kill(true);await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(3));}catch(Exception e)when(e is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException){}try{await errorDrain.WaitAsync(TimeSpan.FromSeconds(1));}catch(Exception e)when(e is OperationCanceledException or TimeoutException or IOException){}}
        }finally{active.AddOrUpdate(manifest.Id,0,(_,n)=>Math.Max(0,n-1));}
    }
    static async Task<string> ReadLine(StreamReader reader,CancellationToken token){var text=new StringBuilder();var buffer=new char[1];while(await reader.ReadAsync(buffer.AsMemory(),token)>0){if(buffer[0]=='\n')return text.ToString().TrimEnd('\r');text.Append(buffer[0]);if(text.Length>65536)throw new IOException("扩展响应超过64KB");}throw new IOException("扩展未返回JSON响应");}
    static async Task Drain(StreamReader reader,CancellationToken token){var buffer=new char[1024];while(await reader.ReadAsync(buffer.AsMemory(),token)>0){}}
}
static class ModulePackage
{
    public static ExtensionManifest Inspect(string file)
    {
        if(new FileInfo(file).Length>64_000_000)throw new ArgumentException("模块包最多64MB");using var archive=ZipFile.OpenRead(file);ValidateEntries(archive);var entry=archive.GetEntry("module.json")??throw new ArgumentException("模块压缩包根目录缺少module.json");if(entry.Length>128000)throw new ArgumentException("模块声明过大");using var reader=new StreamReader(entry.Open());var manifest=JsonSerializer.Deserialize<ExtensionManifest>(reader.ReadToEnd())??throw new ArgumentException("模块声明为空");ExtensionStore.Validate(manifest);if(manifest.Runtime=="stdio"&&!archive.Entries.Any(e=>e.FullName==manifest.Executable))throw new ArgumentException("压缩包缺少扩展程序");return manifest;
    }
    internal static void ValidateEntries(ZipArchive archive)
    {
        if(archive.Entries.Count>2000||archive.Entries.Sum(e=>e.Length)>256_000_000)throw new ArgumentException("模块包过大，最多2000文件、256MB展开大小");var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in archive.Entries){string name=entry.FullName.Replace('\\','/');if(name.StartsWith('/')||name.Contains(':')||name.Split('/').Any(p=>p is "." or ".."||p.EndsWith('.')||p.EndsWith(' ')||p.Any(char.IsControl))||!names.Add(name)||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new ArgumentException("模块包含不安全或重复的文件路径");if(!name.EndsWith('/')&&Path.GetExtension(name).ToLowerInvariant() is not(".exe" or ".dll" or ".json" or ".png" or ".txt" or ".md" or ".dat" or ".bin"))throw new ArgumentException("模块包含不支持的文件类型");}
    }
}
