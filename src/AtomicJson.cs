using System.Text;
using System.Text.Json;
namespace LeoLauncher;

static class AtomicJson
{
    static readonly object gate=new();
    internal static Action<string>? BeforeReplace;
    internal static readonly HashSet<string> Recovered=new(StringComparer.OrdinalIgnoreCase);
    public static void Write(string path,string content)
    {
        // Validate before touching the original. Keep the temporary file on the same volume.
        using var parsed=JsonDocument.Parse(content);
        lock(gate){string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                    var bytes=Encoding.UTF8.GetBytes(content);stream.Write(bytes);stream.Flush(true);
                }
                BeforeReplace?.Invoke(path);
                if(File.Exists(path)){
                    bool valid;try{using var old=JsonDocument.Parse(File.ReadAllText(path));valid=true;}catch(JsonException){valid=false;}
                    if(!valid)PreserveCorrupt(path);
                    File.Replace(temporary,path,valid?path+".bak":null);
                }else File.Move(temporary,path);
            }finally{if(File.Exists(temporary))try{File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
    }
    static void PreserveCorrupt(string path)=>File.Copy(path,path+".corrupt-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff")+"-"+Guid.NewGuid().ToString("N"),false);
    public static T Read<T>(string path,Func<string,T> validate,params string[] alternatives)
    {
        lock(gate){
            Exception? original=null;
            try{return validate(File.ReadAllText(path));}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException){original=e;}
            foreach(var backup in alternatives.Append(path+".bak").Distinct()){
                if(!File.Exists(backup))continue;
                T value;string content;
                try{content=File.ReadAllText(backup);value=validate(content);}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException){continue;}
                // Never overwrite the known-good backup with a corrupt primary.
                if(File.Exists(path))PreserveCorrupt(path);
                string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                try{using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var bytes=Encoding.UTF8.GetBytes(content);stream.Write(bytes);stream.Flush(true);}if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}
                finally{if(File.Exists(temporary))try{File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
                Recovered.Add(Path.GetFileName(path));return value;
            }
            throw new IOException("本地数据无法读取，原文件已保留；没有可用备份。",original);
        }
    }
}
