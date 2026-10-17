namespace LeoLauncher;
static class Upgrade37Tests
{
    public static Dictionary<string,bool> Check(string directory)
    {
        var checks=new Dictionary<string,bool>();
        checks["spoken_song_names_open_local_search"]=QuickRouter.Route("听晴天。").Value=="music_open"&&QuickRouter.Route("听晴天。").Argument=="晴天";
        checks["music_navigation_stays_explicit"]=QuickRouter.Route("下一首！").Argument=="1"&&QuickRouter.Route("上一首").Argument=="-1"&&QuickRouter.Route("下一首是什么歌").Kind==QuickKind.Ai&&QuickRouter.Route("听说巴萨赢了").Kind==QuickKind.Ai;
        checks["playlist_requests_go_to_qq_client"]=QuickRouter.Route("打开歌单").Value=="music_client";
        using var music=new MusicForm(true);int models=0,actions=0;
        var tools=new AppTools(()=>[],_=>{},leoActions:async(action,value,token)=>{actions++;if(action=="music_open"){await music.SearchAsync(value);return "已显示歌曲候选";}return await music.Step(int.Parse(value));});
        using var chat=new ChatForm(true,ChatTests.Settings(),()=>{models++;throw new InvalidOperationException("本地音乐口令不应调用模型");},tools);
        chat.Show();Pump(chat.PreviewRecognizedAsync("听晴天",true));
        bool picked=music.TryPlayCandidate(music.Candidates[0].Id).GetAwaiter().GetResult();
        Pump(chat.PreviewRecognizedAsync("下一首",true));Pump(chat.PreviewRecognizedAsync("上一首",true));
        checks["voice_song_search_and_controls_without_model"]=picked&&actions==3&&models==0;
        Pump(chat.PreviewRecognizedAsync("下一首",false));checks["uncertain_voice_does_not_execute"]=actions==3;
        music.Show();music.Close();checks["closing_music_preserves_single_song_controls"]=!music.IsDisposed&&!music.Visible&&music.Step(1).GetAwaiter().GetResult().Length>0;
        return checks;
    }
    static void Pump(Task task){var until=DateTime.UtcNow.AddSeconds(10);while(!task.IsCompleted&&DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(2);}if(!task.IsCompleted)throw new TimeoutException();task.GetAwaiter().GetResult();Application.DoEvents();}
}
