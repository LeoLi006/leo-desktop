using System.Diagnostics;
namespace LeoLauncher;

// The browser extension handles only Gemini's DOM, never global keyboard input.
static class GeminiWeb
{
    public const int MaxPromptLength=2000;
    public static string ExtensionDirectory=>Path.Combine(AppContext.BaseDirectory,"assets","gemini-bridge");
    public static string Url(string text)
    {
        text=text.Trim();
        if(text.Length==0)throw new ArgumentException(UiLanguage.T("请先输入要发送给 Gemini 的内容。"));
        if(text.Length>MaxPromptLength)throw new ArgumentException(UiLanguage.T("Gemini 快捷输入最多支持2000个字符。"));
        return "https://gemini.google.com/app#leo-id="+Guid.NewGuid().ToString("N")+"&leo-at="+DateTimeOffset.UtcNow.ToUnixTimeSeconds()+"&leo-prompt="+Uri.EscapeDataString(text);
    }
    public static void Open(string text)=>Process.Start(FootTools.EdgeUrlStart(Url(text)));
    public static void Setup()
    {
        if(!Directory.Exists(ExtensionDirectory))throw new IOException(UiLanguage.T("未找到 Gemini 网页扩展，请重新更新 Leo。"));
        UiDialogs.Show(UiLanguage.T("首次使用：在 Edge 扩展页打开“开发人员模式”，点击“加载解压缩的扩展”，选择接下来打开的 gemini-bridge 文件夹。然后登录 Gemini。只需设置一次；以后在 Gemini 输入框回车即可自动填入并发送。扩展仅访问 Gemini 页面，不需要 API Key。"),"Gemini");
        Process.Start(new ProcessStartInfo("explorer.exe"){UseShellExecute=true,ArgumentList={ExtensionDirectory}});
        var start=FootTools.EdgeUrlStart("https://gemini.google.com/app");
        if(start.ArgumentList.Count>0){start.ArgumentList.Clear();start.ArgumentList.Add("edge://extensions/");Process.Start(start);}
        else Process.Start(new ProcessStartInfo("microsoft-edge:edge://extensions/"){UseShellExecute=true});
    }
}
