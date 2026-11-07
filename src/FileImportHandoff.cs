using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace LeoLauncher;

// Bounded, user-triggered file handoff through the existing files_with_app action.
// A receipt proves GUI import only, never export or conversion completion.
static class FileImportHandoff
{
    internal const string Protocol="LeoFileImport/1";
    public static async Task<string> Execute(ResolvedOpenApplication app,string[] paths,CancellationToken token,
        Func<ProcessStartInfo,CancellationToken,Task<bool>> start,TimeSpan? timeout=null,string? temporaryRoot=null)
    {
        token.ThrowIfCancellationRequested();paths=FileDropActions.RequirePaths(paths);
        if(paths.Length!=1||!File.Exists(paths[0])||!ApplicationOpenCapabilities.Matches(app.Rule,paths))throw new ArgumentException("文件导入交接目前仅支持单个匹配文件。");
        if(!app.Available||app.Application==null||!File.Exists(app.Rule.ImportScript))throw new IOException("处理工具或脚本不可用，请检查打开能力配置。");
        LocalApplications.Validate(app.Application);
        string id=Guid.NewGuid().ToString("N"),folder=Path.Combine(temporaryRoot??Path.Combine(Path.GetTempPath(),"Leo-file-import"),id);
        Directory.CreateDirectory(folder);string request=Path.Combine(folder,"request.json"),receipt=Path.Combine(folder,"request.receipt.json");bool submitted=false;
        try{
            File.WriteAllText(request,JsonSerializer.Serialize(new{protocol=Protocol,requestId=id,input=paths[0]}),new UTF8Encoding(false));
            var info=LocalApplications.Start(app.Application);info.Arguments="";
            var flags=FileDropActions.ParseArguments(app.Application.Arguments);
            if(!flags.Contains(app.Rule.ImportScript,StringComparer.OrdinalIgnoreCase))info.ArgumentList.Add(app.Rule.ImportScript);
            foreach(string flag in flags)info.ArgumentList.Add(flag);
            info.ArgumentList.Add("--import-request");info.ArgumentList.Add(request);
            token.ThrowIfCancellationRequested();FileDropActions.RequirePaths(paths);
            if(!await start(info,token))throw new IOException("系统未接收打开请求。");submitted=true;
            var watch=Stopwatch.StartNew();var limit=timeout??TimeSpan.FromSeconds(20);
            while(watch.Elapsed<limit){
                token.ThrowIfCancellationRequested();
                if(File.Exists(receipt)){
                    if(new FileInfo(receipt).Length>16384)throw new IOException("处理工具返回了无效的导入回执。");
                    try{
                        using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(receipt,token));var root=doc.RootElement;
                        bool identity=root.TryGetProperty("protocol",out var p)&&p.GetString()==Protocol&&root.TryGetProperty("requestId",out var r)&&r.GetString()==id&&root.TryGetProperty("input",out var f)&&string.Equals(f.GetString(),paths[0],StringComparison.OrdinalIgnoreCase);
                        if(!identity)throw new IOException("处理工具返回了无效的导入回执。");
                        string state=root.GetProperty("state").GetString()??"";
                        if(state=="imported")return UiLanguage.T("文件已导入处理工具；请在工具中检查预览、样式和输出位置，再确认生成。尚未完成转换。");
                        if(state=="failed")throw new IOException("处理工具报告导入失败；未确认转换完成。");
                        throw new IOException("处理工具返回了无效的导入回执。");
                    }catch(JsonException){/* Receipt may still be being written; bounded wait only. */}
                }
                await Task.Delay(100,token);
            }
            return UiLanguage.T("已提交工具启动请求，但未收到导入确认；请检查工具窗口。未确认转换完成，请勿直接重复生成。");
        }catch(OperationCanceledException)when(submitted){throw new OperationCanceledException(UiLanguage.T("已停止等待导入；工具可能已接收文件，请检查其窗口。未撤回已提交请求。"),token);}
        finally{
            // Remove only this request's known transport files. Never touch input, tool state or outputs.
            foreach(string path in new[]{receipt,request})try{File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}
            try{Directory.Delete(folder);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}
