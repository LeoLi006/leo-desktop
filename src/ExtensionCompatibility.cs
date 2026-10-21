namespace LeoLauncher;
sealed record ExtensionCompatibilityState(bool Compatible,string[] Reasons);
static class ExtensionCompatibility
{
    static Version VersionValue(string text){if(!Version.TryParse(text,out var v))throw new ArgumentException("宿主版本范围格式无效");return new(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision));}
    public static void ValidateMetadata(ExtensionManifest m)
    {
        var min=m.HostMin==null?null:VersionValue(m.HostMin);var max=m.HostMax==null?null:VersionValue(m.HostMax);if(min!=null&&max!=null&&min>max)throw new ArgumentException("宿主版本范围前后颠倒");
        if(m.Dependencies is{} deps&&(deps.Length>16||deps.Distinct(StringComparer.Ordinal).Count()!=deps.Length||deps.Any(d=>string.IsNullOrWhiteSpace(d)||d.Length>80||!d.All(c=>char.IsAsciiLetterOrDigit(c)||c is ':' or '-' or '_' or '/' or '.'))))throw new ArgumentException("扩展依赖声明无效或重复");
    }
    public static ExtensionCompatibilityState Read(ExtensionManifest m,ExtensionStore store,Version? host=null)
    {
        ValidateMetadata(m);host=VersionValue((host??typeof(Program).Assembly.GetName().Version!).ToString());var reasons=new List<string>();
        if(m.HostMin!=null&&host<VersionValue(m.HostMin))reasons.Add("需要更高版本的Leo");if(m.HostMax!=null&&host>VersionValue(m.HostMax))reasons.Add("超出扩展支持的Leo版本");
        var installed=(m.Dependencies??[]).Any(d=>d.StartsWith("module:",StringComparison.Ordinal))?store.Load():[];var visiting=new HashSet<string>(StringComparer.Ordinal);
        bool ModuleReady(string id){var other=installed.FirstOrDefault(x=>x.Id==id);if(other==null||store.Preferences?.DisabledExtensions.Contains(id)==true||!visiting.Add(id))return false;try{return (other.HostMin==null||host>=VersionValue(other.HostMin))&&(other.HostMax==null||host<=VersionValue(other.HostMax))&&(other.Dependencies??[]).All(Ready);}finally{visiting.Remove(id);}}
        bool Ready(string dependency)=>dependency switch{"windows"=>OperatingSystem.IsWindows(),"leo-module/1" or "leo-host-contract/1" or "stdio/v1"=>true,_=>dependency.StartsWith("module:",StringComparison.Ordinal)&&ModuleReady(dependency[7..])};
        visiting.Add(m.Id);
        foreach(var dependency in m.Dependencies??[]){
            bool available=Ready(dependency);
            if(!available)reasons.Add("依赖不可用："+dependency);
        }
        return new(reasons.Count==0,reasons.ToArray());
    }
    public static void Require(ExtensionManifest m,ExtensionStore store){var state=Read(m,store);if(!state.Compatible)throw new InvalidOperationException(string.Join("；",state.Reasons));}
}
