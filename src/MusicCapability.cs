using System.Text.Json;
namespace LeoLauncher;

sealed record MusicControlReceipt(string Message,bool Confirmed=false);
sealed record MusicClientStatus(bool Running,bool Ready);
sealed class MusicSession(MusicHistory history)
{
    readonly object gate=new();MusicSong[] latest=[],queue=[];int index=-1;long search;
    public MusicSong[] History{get{lock(gate)return history.Items.ToArray();}}
    public MusicSong[] Candidates{get{lock(gate)return latest.ToArray();}}
    public event Action<string,MusicSong[]>? Published;
    public long BeginSearch(){lock(gate)return ++search;}
    public void Publish(long ticket,string query,MusicSong[] songs){lock(gate){if(ticket!=search)return;latest=songs;}if(Published!=null)foreach(Action<string,MusicSong[]> listener in Published.GetInvocationList())try{listener(query,songs);}catch(Exception e)when(FeatureFaults.Recoverable(e)){FeatureFaults.Report("音乐结果展示",e);}}
    public void Invalidate(){lock(gate){search++;latest=[];queue=[];index=-1;}}
    public MusicSong Resolve(string id){lock(gate)return latest.Concat(history.Items).FirstOrDefault(s=>s.Id==id)??throw new ArgumentException("歌曲候选已失效，请先搜索或从最近点歌选择");}
    public MusicSong Next(int direction){lock(gate){if(queue.Length==0)throw new ArgumentException("还没有点过歌曲，请先搜索并选择一首歌");return queue[(index+direction+queue.Length)%queue.Length];}}
    public bool Accepted(MusicSong song,bool step=false)
    {
        lock(gate){if(!step)queue=latest.Any(s=>s.Id==song.Id)?latest:history.Items.ToArray();if(queue.Length==0)queue=[song];index=Array.FindIndex(queue,s=>s.Id==song.Id);try{history.Add(song);return true;}catch(Exception e)when(FeatureFaults.Recoverable(e)){return false;}}
    }
}
sealed class MusicCapability(Func<bool> enabled,Func<string> configuration,Func<bool> available,MusicSession session,Func<string,CancellationToken,Task<MusicSong[]>> search,Func<MusicSong,CancellationToken,Action<string>,Task> play,Func<CancellationToken,Action<string>,Task<MusicControlReceipt>> pause,Func<CancellationToken,Action<string>,Task<MusicControlReceipt>> lyrics,Func<CancellationToken,Action<string>,Task> client,Func<MusicClientStatus> status,Func<CancellationToken,Task> show,NativeCapabilityQueue queue,IClientHostServices? services=null):IStructuredLeoModule,IActionRequestAdapter,IBrainContributor,IModuleContractSource
{
    public ModuleContract DescribeModule()=>ModuleContracts.Music(this);
    public DependencyState[] ReadDependencies()=>hostServices.ReadDependencies();

    readonly ClientHostContext hostServices=new("music",services??new ClientHostServices("music",enabled,configuration,()=>[new("music-source","unknown"),new("music-client","unknown"),new("song-selection",session.Candidates.Length>0||session.History.Length>0?"ready":"unknown")],available));
    public string Id=>"music";public string Name=>"QQ音乐点歌";public string Version=>"1";public bool Enabled=>hostServices.Enabled;public bool ConfigurationIsCurrent=>hostServices.ConfigurationIsCurrent;public string BrainStamp=>hostServices.Stamp;public bool BrainAvailable=>ConfigurationIsCurrent;
    bool Available{get{try{return hostServices.ClientAvailable();}catch(Exception e)when(FeatureFaults.Recoverable(e)){return false;}}}
    public IReadOnlyList<string> Actions=>["music_open","music_search","music_play","music_step","music_pause","music_client","music_lyrics","music_status"];
    public IReadOnlyList<BrainAction> BrainActions=>new[]{new BrainAction("music_search","搜索QQ音乐歌曲","music-query",true,Check:Query),new BrainAction("music_open","打开快速点歌","text",Check:OptionalQuery),new BrainAction("music_status","查询QQ音乐状态","none",true,Check:Empty)}.Concat(!Available?[]:new[]{new BrainAction("music_play","点播歌曲","music-song",Hint:"先查询歌曲，使用实际返回的Id；同名或多首候选先澄清",Check:SongId,Preview:id=>{var s=session.Resolve(id);return "点播："+s.Name+" · "+s.Artist+"（仅发送客户端）";},TimeoutMs:45000),new BrainAction("music_client","打开QQ音乐客户端","none",Check:Empty,TimeoutMs:45000),new BrainAction("music_pause","QQ音乐播放/暂停","none",Check:Empty,TimeoutMs:45000),new BrainAction("music_lyrics","QQ音乐歌词开关","none",Check:Empty,TimeoutMs:45000)}).ToArray();
    static void Empty(string v){if(v.Length!=0)throw new ArgumentException("此音乐动作不接受参数");}
    static void Query(string v){if(v.Trim().Length is <1 or >120||v.Any(char.IsControl))throw new ArgumentException("请输入1至120个字的歌名或歌手");}
    static void OptionalQuery(string v){if(v.Length>0)Query(v);}
    static void SongId(string v){if(v.Length is <1 or >18||!v.All(char.IsAsciiDigit))throw new ArgumentException("歌曲编号无效");}
    public ActionRequest Adapt(StructuredActionRequest r)
    {
        if(r.Protocol!="leo-action/1"||!Actions.Contains(r.ActionId)||r.Parameters.ValueKind!=JsonValueKind.Object)throw new ArgumentException("音乐请求无效");
        string? field=r.ActionId switch{"music_open" or "music_search"=>"query","music_play"=>"id","music_step"=>"direction",_=>null};var props=r.Parameters.EnumerateObject().ToArray();
        if(props.Length>(field==null?0:1)||props.Any(p=>p.Name!=field||p.Value.ValueKind!=JsonValueKind.String))throw new ArgumentException("音乐参数包含额外、重复或无效字段");
        return new(r.ActionId,props.Length==0?"":props[0].Value.GetString()!,r.Source,r.RequestId,r.TraceId,r.Deadline);
    }
    public CapabilityDefinition Describe(string a)=>new(a,Id,"in-process","music-text",a is "music_search" or "music_status"?"read":a=="music_open"?"window":"external","coalesce-inflight",a=="music_status"?5000:45000);
    public string FailureCode(Exception e)=>e is ArgumentException?"invalid_request":"music_unconfirmed";
    void Current(CancellationToken t){t.ThrowIfCancellationRequested();if(!Enabled||!ConfigurationIsCurrent)throw new ApplicationConfigurationChangedException();}
    public Task<ActionPayload> ExecuteStructuredAsync(ActionRequest r,CancellationToken token)=>hostServices.Execute(()=>ExecuteCoreAsync(r,token));
    async Task<ActionPayload> ExecuteCoreAsync(ActionRequest r,CancellationToken token)
    {
        Current(token);
        if(r.Action is "music_search" or "music_open")OptionalQuery(r.Value);else if(r.Action=="music_play")SongId(r.Value);else if(r.Action=="music_step"){if(r.Value is not("1" or "-1"))throw new ArgumentException("请选择上一首或下一首");}else Empty(r.Value);
        if(r.Action=="music_status"){var s=status();return new(JsonSerializer.Serialize(s),JsonSerializer.SerializeToElement(s),"state-observed");}
        if(r.Action=="music_open"){await show(token);Current(token);if(r.Value.Length==0)return new("已打开QQ音乐点歌面板。",Evidence:"window-shown");}
        if(r.Action is "music_search" or "music_open"){
            Query(r.Value);long ticket=session.BeginSearch();var songs=(await search(r.Value.Trim(),token)).Where(QQMusic.Valid).DistinctBy(s=>s.Id).Take(8).ToArray();Current(token);session.Publish(ticket,r.Value,songs);
            return new(r.Action=="music_open"?"已打开QQ音乐点歌面板，请从候选选择歌曲。":JsonSerializer.Serialize(songs),JsonSerializer.SerializeToElement(songs),"snapshot",PrivateData:true,DiagnosticMessage:"已查询歌曲候选。");
        }
        return await queue.Run("music",async(t,report)=>{
            Current(t);
            if(r.Action=="music_client"){await client(t,report);Current(t);return new ActionPayload("QQ音乐客户端启动请求已完成，歌单请在客户端选择。",Evidence:"accepted");}
            if(r.Action is "music_pause" or "music_lyrics"){var receipt=await (r.Action=="music_pause"?pause(t,report):lyrics(t,report));Current(t);return new ActionPayload(receipt.Message,JsonSerializer.SerializeToElement(receipt),receipt.Confirmed?"state-observed":"accepted");}
            if(r.Action is not("music_play" or "music_step"))throw new ArgumentException("音乐动作不存在");
            var song=r.Action=="music_step"?session.Next(int.Parse(r.Value)):session.Resolve(r.Value);
            await play(song,t,report);Current(t);bool saved=session.Accepted(song,r.Action=="music_step");
            return new ActionPayload("已向QQ音乐发送点歌："+song.Name+"；播放及歌曲权限以客户端为准。"+(saved?"":" 最近点歌未保存，请检查本地存储。"),JsonSerializer.SerializeToElement(new{id=song.Id,accepted=true,historySaved=saved}),"accepted",PrivateData:true,DiagnosticMessage:"已发送点歌请求；未确认实际播放。");
        },token);
    }
    public async Task<string> ExecuteAsync(ActionRequest r,CancellationToken t)=>(await ExecuteStructuredAsync(r,t)).Message;
}

sealed class MusicClient(MusicSession session,ActionDispatcher dispatcher,string source="music-ui",bool ownsDispatcher=false):IDisposable
{
    public MusicSong[] History=>session.History;
    public event Action<string,MusicSong[]> Published{add=>session.Published+=value;remove=>session.Published-=value;}
    public async Task<string> Run(string action,string value="",CancellationToken token=default)=>(await dispatcher.ExecuteDetailedAsync(new(action,value,source),token)).ToLegacy(token);
    public async Task<MusicSong[]> Search(string query,CancellationToken t){var r=await dispatcher.ExecuteDetailedAsync(new("music_search",query,source),t);r.ToLegacy(t);return r.Data!.Value.Deserialize<MusicSong[]>()!;}
    public void Dispose(){if(ownsDispatcher)dispatcher.Dispose();}
    public static MusicClient Standalone(bool preview,Func<string,CancellationToken,Task<MusicSong[]>>? searcher=null)
    {
        var session=new MusicSession(new(preview?null:Path.Combine(Store.Root,"leo-music.json")));var bus=new ActionDispatcher(new(),(_,_,_)=>throw new InvalidOperationException("音乐不能绕回旧入口"));
        bus.Registry.ReplaceBuiltin(MusicCapabilityHost.Create(()=>true,()=>"standalone",session,new(),_=>Task.CompletedTask,preview,searcher));return new(session,bus,ownsDispatcher:true);
    }
}
