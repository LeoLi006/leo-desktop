namespace LeoLauncher;

static class ApplicationLocation
{
    static bool ProgramPath(string path)=>Path.IsPathFullyQualified(path)&&Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase)&&!path.Contains("\\WindowsApps\\",StringComparison.OrdinalIgnoreCase);
    static string Candidate(AppEntry entry,Preferences prefs)
    {
        var configured=prefs.ApplicationModules.FirstOrDefault(a=>a.Path.Equals(entry.Target,StringComparison.OrdinalIgnoreCase)||"local:"+a.Id==entry.Target);
        string path=configured?.Path??entry.Target;
        if(Path.IsPathFullyQualified(path)&&Path.GetExtension(path).Equals(".lnk",StringComparison.OrdinalIgnoreCase))
            return Catalog.ResolveExecutable(new(entry.Name,path)).ExePath;
        if(ProgramPath(path))return path;
        // Never infer a program directory for shell IDs, URLs or Leo module entries.
        return "";
    }
    public static bool CanOffer(AppEntry entry,Preferences prefs)
    {
        try{if(Path.GetExtension(entry.Target).Equals(".lnk",StringComparison.OrdinalIgnoreCase)&&ProgramPath(entry.ExePath))return true;return ProgramPath(Candidate(entry,prefs));}catch(Exception e)when(FeatureFaults.Recoverable(e)){return false;}
    }
    public static string Resolve(AppEntry entry,Preferences prefs)
    {
        string path=Candidate(entry,prefs);
        if(!ProgramPath(path)||!File.Exists(path))throw new IOException("程序路径已失效，请重新选择应用路径。");
        return Path.GetFullPath(path);
    }
}
