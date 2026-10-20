namespace LeoLauncher;

// Compiled integration example, not automatically registered or installed.
static class MinimalClientServicesExample
{
    public static IClientHostServices CreateProxy(Func<bool> enabled,Func<string> configurationHash,
        Func<bool> passiveClientAvailability,ApplicationHostFaults faults)=>
        new ClientHostServices("clash",enabled,configurationHash,()=>[new("clash-client","unknown")],
            passiveClientAvailability,faults);
}
