using System.Diagnostics;
namespace LeoLauncher;

// Compatibility name retained; the shared bridge handles three explicitly allowed AI sites.
static class GeminiWeb
{
    public const int MaxPromptLength=2000;
    public static string ExtensionDirectory=>Path.Combine(AppContext.BaseDirectory,"assets","gemini-bridge");
    public static string Name(string provider)=>provider switch{"gemini"=>"Gemini","deepseek"=>"DeepSeek","chatgpt"=>"ChatGPT",_=>throw new ArgumentException("Unknown web AI provider")};
    public static string Home(string provider)=>provider switch{"gemini"=>"https://gemini.google.com/app","deepseek"=>"https://chat.deepseek.com/","chatgpt"=>"https://chatgpt.com/",_=>throw new ArgumentException("Unknown web AI provider")};
    public static string Url(string text,string provider="gemini")
    {
        text=text.Trim();
        if(text.Length==0)throw new ArgumentException(UiLanguage.T("请先输入要发送的内容。"));
        if(text.Length>MaxPromptLength)throw new ArgumentException(UiLanguage.T("网页 AI 快捷输入最多支持2000个字符。"));
        return Home(provider)+"#leo-site="+provider+"&leo-lang="+(UiLanguage.English?"en":"zh-CN")+"&leo-id="+Guid.NewGuid().ToString("N")+"&leo-at="+DateTimeOffset.UtcNow.ToUnixTimeSeconds()+"&leo-prompt="+Uri.EscapeDataString(text);
    }
    public static void Open(string text,string provider="gemini")=>Process.Start(FootTools.EdgeUrlStart(Url(text,provider)));
    public static void Setup()
    {
        if(!Directory.Exists(ExtensionDirectory))throw new IOException(UiLanguage.T("未找到网页 AI 扩展，请重新更新 Leo。"));
        UiDialogs.Show(UiLanguage.T("首次使用：在 Edge 扩展页打开“开发人员模式”，点击“加载解压缩的扩展”，选择接下来打开的 gemini-bridge 文件夹。更新已有扩展时点击“重新加载”，并允许新增的 ChatGPT、DeepSeek 站点权限。分别登录要使用的网站；以后在对应输入框回车即可填入并发送。无需 API Key。"),"Leo Web AI");
        Process.Start(new ProcessStartInfo("explorer.exe"){UseShellExecute=true,ArgumentList={ExtensionDirectory}});
        var start=FootTools.EdgeUrlStart("https://gemini.google.com/app");
        if(start.ArgumentList.Count>0){start.ArgumentList.Clear();start.ArgumentList.Add("edge://extensions/");Process.Start(start);}
        else Process.Start(new ProcessStartInfo("microsoft-edge:edge://extensions/"){UseShellExecute=true});
    }
}
