using System.Text.Json;
namespace LeoLauncher;
sealed record MusicPlaylist(string Id,string Name,bool Pinned,List<MusicSong> Songs,string? QQId=null,string? QQName=null,DateTimeOffset? Synced=null,List<string>? HiddenIds=null,List<MusicSong>? ExtraSongs=null,int RemoteCount=0);
sealed class MusicLibrary
{
    readonly string? path;List<MusicPlaylist> records=[];public string? Error{get;private set;}
    // Old local-only playlists remain in the file for recovery, but are no longer offered as playable playlists.
    public List<MusicPlaylist> Items=>records.Where(p=>p.QQId!=null).ToList();
    public MusicLibrary(string? file){path=file;if(file!=null&&File.Exists(file))try{records=JsonSerializer.Deserialize<List<MusicPlaylist>>(File.ReadAllText(file))??[];if(records.Any(p=>string.IsNullOrWhiteSpace(p.Id)||string.IsNullOrWhiteSpace(p.Name)||p.Songs==null||p.Songs.Any(s=>!QQMusic.Valid(s))||p.QQId!=null&&!QQPlaylists.ValidId(p.QQId))||records.Select(p=>p.Id).Distinct().Count()!=records.Count)throw new IOException();}catch{records=[];Error="歌单缓存无法读取，原文件已保留";}}
    void Save(List<MusicPlaylist> next){if(Error!=null)throw new IOException(Error);if(path!=null){File.WriteAllText(path+".tmp",JsonSerializer.Serialize(next));File.Move(path+".tmp",path,true);}records=next;}
    public MusicPlaylist Import(QQPlaylistData data,string? localName=null,string? targetId=null)
    {
        if(!QQPlaylists.ValidId(data.Id)||string.IsNullOrWhiteSpace(data.Name)||data.Songs.Any(s=>!QQMusic.Valid(s)))throw new ArgumentException("无效的QQ歌单");
        var old=targetId==null?records.FirstOrDefault(p=>p.QQId==data.Id):records.FirstOrDefault(p=>p.Id==targetId);
        if(targetId!=null&&old==null)throw new ArgumentException("歌单已解除绑定，未保存同步结果");
        if(old==null&&Items.Count>=30)throw new ArgumentException("最多绑定30个QQ歌单");
        string name=old!=null&&old.Name!=old.QQName?old.Name:localName??data.Name;if(name.Length>24)name=name[..24];
        var next=new MusicPlaylist(old?.Id??Guid.NewGuid().ToString("N"),name,old?.Pinned??true,data.Songs.DistinctBy(s=>s.Id).Take(200).ToList(),data.Id,data.Name,DateTimeOffset.Now,null,null,data.Total);
        Save(old==null?records.Append(next).ToList():records.Select(p=>p.Id==old.Id?next:p).ToList());return next;
    }
    public void Pin(string id)=>Save(records.Select(p=>p.Id==id?p with{Pinned=!p.Pinned}:p).ToList());
    public void Delete(string id)=>Save(records.Where(p=>p.Id!=id).ToList());
}
