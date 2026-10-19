using System.Text.Json;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.IO.Compression;
namespace LeoLauncher;
static class Release100Tests
{
    sealed class FixtureHttp(byte[] zip,string checksum):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=request.RequestUri!.AbsolutePath.EndsWith(".sha256")?new StringContent(checksum):new ByteArrayContent(zip)});}
    public static void Run(string report){using var host=new Form{Opacity=0,ShowInTaskbar=false};host.Shown+=async(_,_)=>{try{var checks=await Check(Path.GetDirectoryName(report)!);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,network_requests=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}finally{host.Close();}};Application.Run(host);}
    static async Task<Dictionary<string,bool>> Check(string root)
    {
        Directory.CreateDirectory(root);var checks=new Dictionary<string,bool>();checks["display_preserves_requested_version"]=LeoUpdates.CurrentTag=="v1.00.0";checks["assembly_version_comparison"]=LeoUpdates.Current==new Version(1,0,0)&&LeoUpdates.Current>new Version(0,53,0);checks["leading_zero_tag_comparison"]=LeoUpdates.TagVersion("v1.00.0")==new Version(1,0,0);checks["requested_package_name"]=LeoUpdates.PackageName("v1.00.0")=="Leo-v1.00.0-win-x64.zip";checks["legacy_standard_tag_unchanged"]=LeoUpdates.PackageName("v0.50.0")=="Leo-v0.50.0-win-x64.zip";
        string tag="v999.00.0",filename=LeoUpdates.PackageName(tag),package=LeoUpdates.AssetUrl(tag,filename),sha=package+".sha256";string json=JsonSerializer.Serialize(new{tag_name=tag,draft=false,prerelease=false,body="release fixture",assets=new[]{new{name=filename,browser_download_url=package},new{name=filename+".sha256",browser_download_url=sha}}});var release=LeoUpdates.ParseRelease(json);checks["leading_zero_assets_recognized"]=!release.SourceOnly&&release.Tag==tag&&release.PackageUrl==package;checks["untrusted_url_rejected"]=LeoUpdates.ParseRelease(json.Replace(package,"https://example.com/file.zip")).SourceOnly;checks["source_only_without_checksum"]=LeoUpdates.ParseRelease(json.Replace(filename+".sha256","missing.sha256")).SourceOnly;
        using var memory=new MemoryStream();using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true)){var manifest=new ReleaseManifest{Version="999.00.0"};foreach(string name in new[]{"LeoLauncher.exe","LeoLauncher.dll","coreclr.dll","System.Windows.Forms.dll"}){byte[] bytes=System.Text.Encoding.UTF8.GetBytes("fixture "+name);manifest.Files[name]=Convert.ToHexString(SHA256.HashData(bytes));using var stream=zip.CreateEntry("payload/"+name).Open();stream.Write(bytes);}using var writer=new StreamWriter(zip.CreateEntry("payload/release-manifest.json").Open());writer.Write(JsonSerializer.Serialize(manifest));}byte[] data=memory.ToArray();string checksum=Convert.ToHexString(SHA256.HashData(data))+"  "+filename;using(var http=new HttpClient(new FixtureHttp(data,checksum))){var payload=await LeoUpdates.Download(http,release,Path.Combine(root,"fixture-cache"),null,CancellationToken.None);checks["leading_zero_download_and_manifest"]=LeoInstaller.Verify(payload).Version=="999.00.0";}
        checks["old_updater_alias_name"]=new Version(1,0,0).ToString()=="1.0.0"&&"Leo-v"+LeoUpdates.TagVersion("v1.00.0")+"-win-x64.zip"=="Leo-v1.0.0-win-x64.zip";return checks;
    }
}
