using System.Text.Json;
namespace LeoLauncher;
static class GeminiWebTests
{
    public static void Run(string report)
    {
        var checks=new Dictionary<string,bool>();
        string url=GeminiWeb.Url(" 中文 & # ? \n second line ");var uri=new Uri(url);
        checks["exact_https_gemini_origin"]=uri.Scheme=="https"&&uri.Host=="gemini.google.com"&&uri.AbsolutePath=="/app"&&uri.Query.Length==0;
        checks["prompt_in_fragment_escaped"]=uri.Fragment.Contains("leo-prompt=%E4%B8%AD%E6%96%87%20%26%20%23%20%3F%20%0A%20second%20line");
        checks["all_three_exact_origins"]=new[]{"gemini","deepseek","chatgpt"}.All(id=>GeminiWeb.Url("fixture",id).StartsWith(GeminiWeb.Home(id)+"#leo-site="+id+"&"));
        bool invalid=false;try{GeminiWeb.Url("fixture","invalid");}catch(ArgumentException){invalid=true;}checks["unknown_provider_rejected"]=invalid;
        checks["unique_handoff"]=url!=GeminiWeb.Url(" 中文 & # ? \n second line ");
        bool empty=false,longText=false;try{GeminiWeb.Url("  ");}catch(ArgumentException){empty=true;}try{GeminiWeb.Url(new('a',2001));}catch(ArgumentException){longText=true;}
        checks["empty_and_oversized_rejected"]=empty&&longText;
        checks["no_clipboard_or_browser_debug_flags"]=!FootTools.EdgeUrlStart(url).ArgumentList.Any(x=>x.StartsWith("--"));
        using(var footer=new FootTools(130,true))
        {
            footer.Show();Application.DoEvents();
            checks["gemini_toggle_and_edge_switch"]=footer.VerifyGeminiSearch();
            checks["all_web_targets_toggle"]=footer.VerifyWebAiTargets();
            checks["edge_toggle_compatible"]=footer.VerifySearchToggle();
            checks["disclosure_and_order"]=footer.VerifyDisclosure()&&footer.VerifyButtonOrder();
            footer.ApplyScale(80);checks["small_size_switch"]=footer.VerifyGeminiSearch()&&footer.VerifyWebAiTargets();
            footer.ApplyScale(230);checks["large_size_switch"]=footer.VerifyGeminiSearch()&&footer.VerifyWebAiTargets();
            footer.ConfigureModules(new Preferences{EdgeModule=false});
            checks["edge_disabled_hides_web_inputs"]=!footer.Controls.OfType<Button>().Any(b=>b.Visible&&(b.AccessibleName??"").Contains("网页快捷输入"));
        }
        using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(GeminiWeb.ExtensionDirectory,"manifest.json")));
        checks["extension_only_three_sites_no_extra_permissions"]=manifest.RootElement.GetProperty("content_scripts")[0].GetProperty("matches").EnumerateArray().Select(x=>x.GetString()).Order().SequenceEqual(new[]{"https://gemini.google.com/*","https://chat.deepseek.com/*","https://chatgpt.com/*"}.Order())&&!manifest.RootElement.TryGetProperty("permissions",out _);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
        File.WriteAllText(report,JsonSerializer.Serialize(new{Passed=checks.Values.All(x=>x),Checks=checks},new JsonSerializerOptions{WriteIndented=true}));
        Environment.ExitCode=checks.Values.All(x=>x)?0:1;
    }
}
