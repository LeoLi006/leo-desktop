namespace LeoLauncher;

// Compiled example; not automatically installed or registered.
static class MinimalApplicationServicesExample
{
    public static LocalApplicationModule Create(LocalApplication app,Func<LocalApplication?> current,
        ApplicationCapabilityHost executor,ApplicationHostFaults faults)
    {
        IApplicationHostServices scoped=new ApplicationHostServices(app.Id,current,faults);
        return new LocalApplicationModule(app,(_,_,_)=>throw new InvalidOperationException("使用已有应用执行器"),
            executor,services:scoped);
    }
}
