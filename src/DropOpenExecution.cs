using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;

// A payload adapter for the existing files_with_app action; no second dispatcher.
static class DropOpenExecution
{
    static readonly HashSet<string> runnable=new(new[]{".exe",".com",".scr",".msi",".msix",".lnk",".appref-ms",".bat",".cmd",".ps1",".vbs",".vbe",".js",".jse",".wsf",".wsh",".hta",".cpl",".reg",".url",".pif",".py",".pyw",".sh"},StringComparer.OrdinalIgnoreCase);
    public static bool MayRun(string path)=>!Directory.Exists(path)&&runnable.Contains(Path.GetExtension(path));
    public static bool MediaAvailable(Preferences p)
    {if(!p.MediaModule)return false;try{using var passive=ModulePaths.PassiveRead();return MediaControl.PotPlayerPath() is {} path&&File.Exists(path);}catch(Exception e)when(FeatureFaults.Recoverable(e)){return false;}}
    public static async Task<string> Execute(Preferences prefs,string payload,CancellationToken token,
        Func<ProcessStartInfo,CancellationToken,Task<bool>> start,Func<string,bool> openWith,Func<string?> code,IEnumerable<AppEntry>? catalog=null,Func<string,string,CancellationToken,Task<string>>? dispatch=null)
    {
        token.ThrowIfCancellationRequested();using var doc=JsonDocument.Parse(payload);var root=doc.RootElement;
        string mode=root.TryGetProperty("mode",out var m)?m.GetString()??"app":"app";
        var paths=FileDropActions.RequirePaths(root.GetProperty("paths").EnumerateArray().Select(e=>e.GetString()??""));
        if(mode=="system"){
            if(paths.Length!=1||Directory.Exists(paths[0]))throw new ArgumentException("Windows 打开方式仅支持单个文件。");
            token.ThrowIfCancellationRequested();return openWith(paths[0])?"Windows 打开方式已返回；请以所选应用的实际状态为准。":"已取消选择，未提交打开请求。";
        }
        var requests=new List<ProcessStartInfo>();bool allInOne=false;
        if(mode=="capability"){
            string id=root.GetProperty("opener").GetString()??"";var resolved=ApplicationOpenCapabilities.Resolve(prefs,catalog).SingleOrDefault(a=>a.Rule.Id==id)??throw new ArgumentException("打开能力已移除，请重新选择。");
            if(!resolved.Available)throw new IOException("应用已停用、移除或程序路径无效。");
            if(root.TryGetProperty("expected",out var expected)&&expected.GetString()!=resolved.Stamp)throw new InvalidOperationException("打开能力配置已变化，请重新选择。");
            var info=ApplicationOpenCapabilities.Start(resolved,paths);
            if(resolved.Rule.Adapter=="media"){
                if(dispatch==null)throw new InvalidOperationException("媒体入口尚未就绪。");
                return await dispatch("media_file",paths.Single(),token);
            }
            requests.Add(info);allInOne=true;
        }
        if(mode!="capability")switch(mode){
            case "default":
                if(paths.Any(MayRun))throw new ArgumentException("程序或脚本不提供默认运行，请显式选择应用查看或添加模块。");
                foreach(string path in paths){var info=new ProcessStartInfo(Directory.Exists(path)?"explorer.exe":path){UseShellExecute=true};if(Directory.Exists(path))info.ArgumentList.Add(path);requests.Add(info);}break;
            case "code":
                string exe=code()??throw new IOException("未找到 VS Code，请先配置或安装。");
                var vscode=LauncherFileSearch.CodeStart(exe,paths[0]);foreach(string path in paths.Skip(1))vscode.ArgumentList.Add(path);requests.Add(vscode);break;
            case "reveal":requests.AddRange(paths.Select(FileDropActions.FolderStart));break;
            case "app":
                // No batch/format support declaration exists for manually selected applications.
                if(paths.Length!=1)throw new ArgumentException("手动指定应用仅支持单项，请每次拖入一个文件或文件夹。");
                requests.Add(root.TryGetProperty("module",out var module)&&module.GetString() is {Length:>0} id
                    ?FileDropActions.ConfiguredStart(prefs,id,paths)
                    :FileDropActions.AppStart(root.GetProperty("application").GetString()??"",paths));break;
            default:throw new ArgumentException("不支持的文件打开方式。");
        }
        int accepted=0;
        try{
            foreach(var request in requests){token.ThrowIfCancellationRequested();if(mode=="code"||allInOne)FileDropActions.RequirePaths(paths);else FileDropActions.RequirePath(paths[accepted]);if(!await start(request,token))throw new IOException("系统未接收打开请求。");accepted++;}
        }catch(Exception error)when(FeatureFaults.Recoverable(error)){
            if(accepted==0)throw;
            int handled=mode=="code"||allInOne?paths.Length:accepted;
            throw new IOException(UiLanguage.T("已提交打开请求：")+handled+" / "+paths.Length+"\n"+UiLanguage.T(error is OperationCanceledException?"其余项已取消，已提交的请求不会撤回。":"其余项未提交，未自动重试。")+" "+UiLanguage.T(error.Message),error);
        }
        return UiLanguage.T("已向系统提交打开请求，共 ")+paths.Length+UiLanguage.T(" 项；不代表文件已成功打开。");
    }
}
