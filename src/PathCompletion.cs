namespace LeoLauncher;
sealed record PathChoice(string Path,bool Directory)
{
    public override string ToString()=>(Directory?"▸  ":"    ")+System.IO.Path.GetFileName(Path.TrimEnd('\\','/'))+(Directory?"  /":"");
}
static class PathCompletion
{
    public static PathChoice[] Find(string input,CancellationToken token=default)
    {
        string expanded=LocalPath.Expand(input);if(!LocalPath.LooksLike(expanded))return [];string folder,prefix;
        try{if(Directory.Exists(expanded)){folder=expanded;prefix="";}else{folder=System.IO.Path.GetDirectoryName(expanded)??"";prefix=System.IO.Path.GetFileName(expanded);}if(!Directory.Exists(folder))return [];
            int scanned=0;var found=new List<PathChoice>();foreach(var item in Directory.EnumerateFileSystemEntries(folder)){token.ThrowIfCancellationRequested();if(++scanned>20000)break;string name=System.IO.Path.GetFileName(item);if(name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))found.Add(new(item,Directory.Exists(item)));if(found.Count>=200)break;}return found.OrderByDescending(p=>p.Directory).ThenBy(p=>p.Path,StringComparer.OrdinalIgnoreCase).Take(80).ToArray();
        }catch(IOException){return [];}catch(UnauthorizedAccessException){return [];}catch(ArgumentException){return [];}
    }
    public static string Fill(PathChoice choice)=>choice.Path+(choice.Directory&&!choice.Path.EndsWith(System.IO.Path.DirectorySeparatorChar)?System.IO.Path.DirectorySeparatorChar:"");
}
