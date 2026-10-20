namespace LeoLauncher;

// Compiled documentation example; receives no Control or arbitrary UI callback.
static class MinimalPresentationExample
{
    public static Task Open(IModuleWindowPort window,CancellationToken token)=>window.ShowAsync(token);
    // The host binds a fixed target before handing the port to a module.
    public static IModuleWindowPort PlannerPort(ModulePresentationService host)=>host.For(ModuleWindowTarget.Todos);
    public static IModuleWindowPort QuickNotePort(ModulePresentationService host)=>host.For(ModuleWindowTarget.QuickNote);
    public static Task Report(ModulePresentationFeedback feedback,ActionResult result)=>feedback.ReportAsync(result);
}
