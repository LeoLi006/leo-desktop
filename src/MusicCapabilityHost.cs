using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;
static class MusicCapabilityHost
{
    public static MusicCapability Create(Func<bool> enabled,Func<string> config,MusicSession session,NativeCapabilityQueue queue,Func<CancellationToken,Task> show,bool preview,Func<string,CancellationToken,Task<MusicSong[]>>? search=null,ApplicationHostFaults? faults=null)=>new(enabled,config,()=>{using var passive=ModulePaths.PassiveRead();return preview||QQMusic.ClientPath()!=null;},session,search??(preview?(_,_)=>Task.FromResult<MusicSong[]>([new("97773","0039MnYb0qxYhV","晴天","周杰伦","叶惠美"),new("639147","000i9BX94fzhzv","You're Not Sorry","Taylor Swift","Fearless")]):QQMusic.Search),
        async(s,t,p)=>{if(!preview)await QQNative.PlayAsync(s,t,p);},
        async(t,p)=>{if(preview)return new("示例模式，不控制音乐");await QQNative.EnsureReadyAsync(t,p);p("正在确认QQ音乐播放状态…");return await QQPlayback.SetPausedCoreAsync(null,t);},
        async(t,p)=>{if(preview)return new("示例模式，不切换歌词");await QQNative.EnsureReadyAsync(t,p);return new(await QQNative.ToggleLyrics(t));},
        async(t,p)=>{if(!preview){t.ThrowIfCancellationRequested();QQMusic.OpenClient(t);await QQNative.WaitReadyAsync(t,p);}},
        ()=>{if(preview)return new(false,false);var processes=Process.GetProcessesByName("QQMusic");try{return new(processes.Length>0,QQNative.Ready);}finally{foreach(var process in processes)process.Dispose();}},show,queue,services:new ClientHostServices("music",enabled,config,
            ()=>[new("music-source","unknown"),new("music-client","unknown"),new("song-selection",session.Candidates.Length>0||session.History.Length>0?"ready":"unknown")],
            ()=>{using var passive=ModulePaths.PassiveRead();return preview||QQMusic.ClientPath()!=null;},faults));
}
sealed partial class PetForm
{
    MusicSession? musicSession;readonly NativeCapabilityQueue musicCapabilityQueue=new();string musicCapabilityStamp="";
    void RegisterMusicCapability()
    {
        string Stamp()=>BrainCatalog.Hash(JsonSerializer.Serialize(new{enabled=prefs.MusicModule,path=prefs.ModuleExecutables.GetValueOrDefault("music")}));
        if(musicCapabilityStamp==Stamp())return;musicSession??=new(new(previewMode?null:Path.Combine(Store.Root,"leo-music.json")));musicSession.Invalidate();var ui=new ModuleWindowService(this);
        commands.Registry.ReplaceBuiltin(MusicCapabilityHost.Create(()=>prefs.MusicModule,Stamp,musicSession,musicCapabilityQueue,t=>ui.ShowAsync(()=>launcher.OpenMusicCore(),t),previewMode,faults:commands.Registry.ClientFaults));musicCapabilityStamp=Stamp();
    }
}
