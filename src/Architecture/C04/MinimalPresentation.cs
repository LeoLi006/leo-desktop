namespace LeoLauncher;

// Compiled documentation example; receives no Control or arbitrary UI callback.
static class MinimalPresentationExample
{
    public static Task Open(IModuleWindowPort window,CancellationToken token)=>window.ShowAsync(token);
    public static Task Report(ModulePresentationFeedback feedback,ActionResult result)=>feedback.ReportAsync(result);
}
