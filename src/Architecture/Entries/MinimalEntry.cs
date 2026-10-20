namespace LeoLauncher;

// Compiled documentation example only; never registered or installed automatically.
static class MinimalEntryExample
{
    public static EntryContribution[] Read(Preferences prefs,CapabilityRegistry registry)
    {
        // Register capabilities first using the normal host path. This binding only
        // checks ownership; it grants no action permissions and executes nothing.
        EntryContributions.Bind(prefs,registry);
        return EntryContributions.ReadAvailable(prefs);
    }
}
