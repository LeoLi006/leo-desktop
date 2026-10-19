using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace LeoLauncher;
sealed record LeoRelease(Version Version,string Tag,string Notes,string PackageUrl,string ChecksumUrl,bool SourceOnly=false);
static class LeoUpdates
{
    public const string Repository="LeoLi006/leo-desktop",Page="https://github.com/"+Repository+"/releases";
    public static Version Current=>Normalize(typeof(LeoUpdates).Assembly.GetName().Version??new Version(0,0));
    public static string CurrentTag=>"v"+(typeof(LeoUpdates).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0]??Current.ToString());
    public static string PackageName(string tag){if(TagVersion(tag)==null)throw new IOException("发布版本格式无法识别。");return "Leo-v"+tag.TrimStart('v','V')+"-win-x64.zip";}
    static Version Normalize(Version value)=>new(value.Major,value.Minor,Math.Max(0,value.Build));
    public static Version? TagVersion(string tag)=>Version.TryParse(tag.TrimStart('v','V'),out var version)&&version.Revision<=0?Normalize(version):null;
    public static HttpClient Client(){var http=new HttpClient{Timeout=TimeSpan.FromMinutes(10)};http.DefaultRequestHeaders.UserAgent.ParseAdd("LeoDesktop/"+Current);http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");return http;}
    static async Task<string> Text(HttpClient http,string url,int limit,CancellationToken token){using var response=await http.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();await using var input=await response.Content.ReadAsStreamAsync(token);using var output=new MemoryStream();var buffer=new byte[8192];int n;while((n=await input.ReadAsync(buffer,token))>0){if(output.Length+n>limit)throw new IOException("更新信息过大，已停止读取。");output.Write(buffer,0,n);}return Encoding.UTF8.GetString(output.ToArray());}
    public static string AssetUrl(string tag,string name)=>"https://github.com/"+Repository+"/releases/download/"+Uri.EscapeDataString(tag)+"/"+Uri.EscapeDataString(name);
    public static LeoRelease ParseRelease(string json)
    {
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;string tag=root.GetProperty("tag_name").GetString()??"";var version=TagVersion(tag)??throw new IOException("发布版本格式无法识别。");if(root.GetProperty("draft").GetBoolean()||root.GetProperty("prerelease").GetBoolean())throw new IOException("暂不安装预览版本。");string name=PackageName(tag);string package="",checksum="";
        foreach(var asset in root.GetProperty("assets").EnumerateArray()){string file=asset.GetProperty("name").GetString()??"",url=asset.GetProperty("browser_download_url").GetString()??"";if(file==name&&url==AssetUrl(tag,name))package=url;if(file==name+".sha256"&&url==AssetUrl(tag,name+".sha256"))checksum=url;}
        string notes=root.TryGetProperty("body",out var body)?body.GetString()??"":"";if(notes.Length>12000)notes=notes[..12000];return new(version,tag,notes,package,checksum,package.Length==0||checksum.Length==0);
    }
    public static async Task<LeoRelease?> Check(HttpClient http,CancellationToken token)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(25));
        try{return ParseRelease(await Text(http,"https://api.github.com/repos/"+Repository+"/releases/latest",2_000_000,timeout.Token));}
        catch(HttpRequestException e)when(e.StatusCode==HttpStatusCode.NotFound){using var tags=JsonDocument.Parse(await Text(http,"https://api.github.com/repos/"+Repository+"/tags?per_page=50",1_000_000,timeout.Token));var latest=tags.RootElement.EnumerateArray().Select(t=>t.GetProperty("name").GetString()??"").Select(t=>(Tag:t,Version:TagVersion(t))).Where(t=>t.Version!=null).OrderByDescending(t=>t.Version).FirstOrDefault();return latest.Version==null?null:new(latest.Version,latest.Tag,"此版本仅发布了源码，尚未提供带校验文件的 Windows 安装包。","","",true);}
    }
    public static string ParseChecksum(string text,string expectedName)
    {
        var match=System.Text.RegularExpressions.Regex.Match(text.Trim(),@"^([0-9a-fA-F]{64})(?:\s+\*?([^\r\n]+))?$");if(!match.Success||match.Groups[2].Success&&match.Groups[2].Value.Trim()!=expectedName)throw new IOException("校验文件格式或包名不匹配。");return match.Groups[1].Value;
    }
    internal static string ExtractVerified(string zip,string destination,LeoRelease release)
    {
        using(var archive=ZipFile.OpenRead(zip)){if(archive.Entries.Count>2500||archive.Entries.Sum(e=>e.Length)>1_000_000_000)throw new IOException("安装包内容超出限制。");var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in archive.Entries){string name=entry.FullName.Replace('\\','/');if(name is "Install-Leo.cmd" or "使用说明.txt")continue;if(!name.StartsWith("payload/",StringComparison.Ordinal))throw new IOException("安装包目录结构不匹配。");string relative=name[8..];if(relative.Length==0)continue;if(relative.EndsWith('/')){LeoInstaller.Inside(destination,relative.TrimEnd('/'));continue;}if(!paths.Add(relative)||!LeoInstaller.Allowed(relative))throw new IOException("安装包包含未认可的文件。");string path=LeoInstaller.Inside(destination,relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);entry.ExtractToFile(path,false);}
        }
        var manifest=LeoInstaller.Verify(destination);if(TagVersion(manifest.Version)!=release.Version)throw new IOException("安装包版本与发布记录不符。");return destination;
    }
    public static async Task<string> Download(HttpClient http,LeoRelease release,string cacheRoot,IProgress<int>? progress,CancellationToken token)
    {
        if(release.SourceOnly||release.Version<=Current)throw new IOException("没有可安装的较新正式版本。");string filename=PackageName(release.Tag);if(release.PackageUrl!=AssetUrl(release.Tag,filename)||release.ChecksumUrl!=AssetUrl(release.Tag,filename+".sha256"))throw new IOException("更新来源不匹配。");
        string root=Path.GetFullPath(cacheRoot),stage=LeoInstaller.Inside(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);string zip=Path.Combine(stage,filename);
        try{string expected=ParseChecksum(await Text(http,release.ChecksumUrl,4096,token),filename);using(var response=await http.GetAsync(release.PackageUrl,HttpCompletionOption.ResponseHeadersRead,token)){response.EnsureSuccessStatusCode();long total=response.Content.Headers.ContentLength??0;if(total>250_000_000)throw new IOException("安装包超过250MB，已停止下载。");await using var input=await response.Content.ReadAsStreamAsync(token);await using var output=new FileStream(zip,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true);var buffer=new byte[65536];int n;long copied=0;while((n=await input.ReadAsync(buffer,token))>0){copied+=n;if(copied>250_000_000)throw new IOException("安装包超过大小限制。");await output.WriteAsync(buffer.AsMemory(0,n),token);if(total>0)progress?.Report((int)Math.Min(98,copied*98/total));}}
            token.ThrowIfCancellationRequested();await using(var data=File.OpenRead(zip)){string hash=Convert.ToHexString(await SHA256.HashDataAsync(data,token));if(!hash.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new IOException("安装包校验失败，未启动安装。");}
            string payload=Path.Combine(stage,"payload");await Task.Run(()=>{token.ThrowIfCancellationRequested();ExtractVerified(zip,payload,release);token.ThrowIfCancellationRequested();},token);progress?.Report(100);return payload;
        }catch{try{if(stage.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(stage),"^[a-f0-9]{32}$"))Directory.Delete(stage,true);}catch{}throw;}
    }
}
sealed class UpdateForm:BarcaWindow
{
    readonly HttpClient http=LeoUpdates.Client();readonly CancellationTokenSource life=new();CancellationTokenSource? request;LeoRelease? release;
    public UpdateForm(bool preview=false)
    {
        Text="Leo · 手动检查更新";ClientSize=new(620,485);StartPosition=FormStartPosition.CenterParent;var chrome=BarcaUi.Chrome(this,"检查更新","仅在你点击时联网检查，不自动下载或安装。");var version=new Label{Text="当前版本  "+LeoUpdates.CurrentTag,Bounds=new(24,105,570,28),ForeColor=BarcaUi.Gold};var notes=new TextBox{Bounds=new(24,145,570,205),ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=BarcaUi.Surface,ForeColor=BarcaUi.Text,Font=BarcaUi.Font(9.5f),Text="更新来自本项目的 GitHub 正式发布。下载后核对校验文件及包内清单，再打开安装确认窗口。更新前会备份，失败可回退。"};var state=new Label{Bounds=new(24,370,570,40),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)};var progress=new ProgressBar{Bounds=new(24,354,570,6),Visible=false};var check=new BarcaButton{Text="检查更新",Bounds=new(24,428,110,34)};var page=new BarcaButton{Text="发布页面",Bounds=new(146,428,110,34)};var install=new BarcaButton{Text="下载并安装",Bounds=new(442,428,152,34),Enabled=false};var cancel=new BarcaButton{Text="取消",Bounds=new(330,428,100,34),Visible=false};cancel.Click+=(_,_)=>request?.Cancel();page.Click+=(_,_)=>{if(!preview)Process.Start(new ProcessStartInfo(LeoUpdates.Page){UseShellExecute=true});};
        async Task Run(bool download){if(request!=null)return;if(preview){state.Text="示例模式不联网。";return;}var pending=CancellationTokenSource.CreateLinkedTokenSource(life.Token);request=pending;check.Enabled=install.Enabled=false;cancel.Visible=true;
            try{if(!download){state.Text="正在检查公开发布…";var found=await LeoUpdates.Check(http,pending.Token);if(IsDisposed)return;release=found;if(found==null){state.Text="目前还没有公开发布版本。";return;}notes.Text=found.Notes;state.Text=found.Version>LeoUpdates.Current?(found.SourceOnly?"发现源码版本 "+found.Tag+"，暂时没有可安装包。":"发现新版本 "+found.Tag):"当前版本已是最新，或高于公开发布版本。";}
                else{if(release==null)return;progress.Visible=true;progress.Value=0;state.Text="正在下载并验证安装包…";var reports=new Progress<int>(n=>{if(!IsDisposed){progress.Value=n;state.Text="下载与校验 "+n+"%";}});string payload=await LeoUpdates.Download(http,release,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LeoUpdates"),reports,pending.Token);pending.Token.ThrowIfCancellationRequested();var start=new ProcessStartInfo(Path.Combine(payload,"LeoLauncher.exe")){UseShellExecute=true,WorkingDirectory=payload};start.ArgumentList.Add("--install-wizard");start.ArgumentList.Add("--target");start.ArgumentList.Add(Store.Root);Process.Start(start);state.Text="已校验并打开安装窗口，请确认安装目录后更新。";}}
            catch(OperationCanceledException){if(!IsDisposed)state.Text=pending.IsCancellationRequested?"已取消，当前版本保持运行。":"连接超时，可重新检查。";}
            catch(Exception e){if(!IsDisposed)state.Text=e is HttpRequestException{StatusCode:HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests}?"GitHub 暂时限流，请稍后再检查。":"未完成："+e.Message;}
            finally{request=null;pending.Dispose();if(!IsDisposed){check.Enabled=true;install.Enabled=release is {SourceOnly:false}&&release.Version>LeoUpdates.Current;cancel.Visible=false;}}}
        Shown+=(_,_)=>{notes.Select(0,0);check.Focus();};
        check.Click+=async(_,_)=>await Run(false);install.Click+=async(_,_)=>await Run(true);Controls.AddRange([version,notes,state,progress,check,page,install,cancel,chrome]);MinimumSize=MaximumSize=Size;
    }
    protected override void Dispose(bool disposing){if(disposing){life.Cancel();life.Dispose();http.Dispose();}base.Dispose(disposing);}
}
