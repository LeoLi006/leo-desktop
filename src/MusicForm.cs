using System.Diagnostics;
namespace LeoLauncher;
sealed partial class MusicForm:BarcaWindow
{
    internal Action? MediaRequested;
    BarcaButton? mediaControlButton;
    internal void ConfigureMedia(bool enabled){if(mediaControlButton!=null)mediaControlButton.Visible=enabled;}
    readonly bool preview;
    readonly TextBox input=new();
    readonly FlowLayoutPanel songs=new(){Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(18,8,14,8)};
    readonly BarcaButton retrySearch=new(){Text="重试搜索",Dock=DockStyle.Right,Width=90,Visible=false};
    readonly Label state=new(){Dock=DockStyle.Bottom,Height=36,Padding=new Padding(20,8,12,0),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f)};
    readonly MusicClient client;readonly bool ownsClient;
    readonly ActivityOwner searchActivities=new("music-window"),controlActivities=new("music-window");
    CancellationTokenSource? pending;
    MusicSong[] latest=[];long presentationGeneration;
    readonly ModuleThemeBinding theme;
    public MusicForm(bool previewMode=false,Func<string,CancellationToken,Task<MusicSong[]>>? searcher=null,MusicClient? shared=null)
    {
        preview=previewMode;client=shared??MusicClient.Standalone(preview,searcher);ownsClient=shared==null;client.Published+=Published;
        Text="Leo · 快速点歌";ClientSize=new Size(620,490);TopMost=false;ShowInTaskbar=true;
        var chrome=BarcaUi.Chrome(this,"LEO  /  点一首","搜歌名或歌手，点完即走。歌单请在QQ音乐中操作。");
        var top=new Panel{Dock=DockStyle.Top,Height=136,Padding=new Padding(18,8,18,0)};
        var field=new RoundedSurface{Dock=DockStyle.Top,Height=45,Padding=new Padding(13,12,7,8),BackColor=BarcaUi.Raised};
        input.Dock=DockStyle.Fill;input.Font=BarcaUi.Font(11);input.BorderStyle=BorderStyle.None;input.BackColor=BarcaUi.Raised;input.ForeColor=BarcaUi.Text;input.MaxLength=120;input.PlaceholderText=UiLanguage.T("歌名 / 歌手，回车搜索");
        var search=new BarcaButton{Text="搜索",Dock=DockStyle.Right,Width=66};field.Controls.Add(input);field.Controls.Add(search);top.Controls.Add(field);
        search.Click+=async(_,_)=>await SearchAsync(input.Text);input.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await SearchAsync(input.Text);}};
        var controls=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=70,WrapContents=true};
        void Button(string text,int width,Func<Task> action){var button=new BarcaButton{Text=text,Size=new Size(width,30),Font=BarcaUi.Font(8.5f)};button.Click+=async(_,_)=>{try{await action();}catch(OperationCanceledException){}catch(Exception e){if(!IsDisposed){state.Text=e.Message;if(!preview)OperationFeedback.Error(e.Message);}}};if(text=="媒体控制")mediaControlButton=button;controls.Controls.Add(button);}
        Button("媒体控制",90,()=>{MediaRequested?.Invoke();return Task.CompletedTask;});
        Button("最近点歌",87,()=>{Home();return Task.CompletedTask;});
        Button("上一首",70,async()=>{string message=await Step(-1);if(!IsDisposed)state.Text=message;});Button("下一首",70,async()=>{string message=await Step(1);if(!IsDisposed)state.Text=message;});
        Button("播放/暂停",90,async()=>{string message=await RunControl("music_pause");if(!IsDisposed)state.Text=message;});
        Button("歌词",57,async()=>{string message=await RunControl("music_lyrics");if(!IsDisposed)state.Text=message;});
        Button("打开QQ音乐",112,async()=>{string message=await RunControl("music_client");if(!IsDisposed)state.Text=message;});
        var searchStatus=new Panel{Dock=DockStyle.Bottom,Height=36};state.Dock=DockStyle.Fill;searchStatus.Controls.Add(state);searchStatus.Controls.Add(retrySearch);retrySearch.Click+=async(_,_)=>await SearchAsync(input.Text);
        top.Controls.Add(controls);Controls.Add(songs);Controls.Add(searchStatus);Controls.Add(top);Controls.Add(chrome);
        MaximizeBox=false;MinimumSize=MaximumSize=Size;Home();state.Text="快速点一首 · QQ音乐未运行时会自动启动";Shown+=(_,_)=>input.Focus();theme=new(this);VisibleChanged+=(_,_)=>{Interlocked.Increment(ref presentationGeneration);if(!Visible&&!released)searchActivities.Reset();};
    }
    public async Task SearchAsync(string query)
    {
        if(IsDisposed||released)return;input.Text=query;Interlocked.Increment(ref presentationGeneration);searchActivities.Reset();if(string.IsNullOrWhiteSpace(query))return;
        using var activity=searchActivities.Begin("search");var request=CancellationTokenSource.CreateLinkedTokenSource(activity.Token);pending=request;retrySearch.Visible=false;state.Text="正在搜索QQ音乐…";
        try{var found=await client.Search(query,request.Token);if(!released&&!IsDisposed&&activity.Current&&ReferenceEquals(pending,request)){latest=found;Display(latest,"搜索结果");state.Text=latest.Length>0?"点选歌曲，在QQ音乐播放":"没有找到歌曲，试试歌名加歌手";}}
        catch(OperationCanceledException){}catch(Exception e){if(!released&&!IsDisposed&&activity.Current&&ReferenceEquals(pending,request)){state.Text=e.Message+" · 已保留原结果";retrySearch.Visible=true;}}finally{if(ReferenceEquals(pending,request))pending=null;request.Dispose();}
    }
    void Display(MusicSong[] list,string title)
    {
        foreach(Control c in songs.Controls.Cast<Control>().ToArray())c.Dispose();
        songs.Controls.Add(new Label{Text=title,Width=555,Height=28,ForeColor=BarcaUi.Gold,Font=BarcaUi.Font(9,true)});
        foreach(var song in list)
        {
            var row=new RoundedSurface{Width=555,Height=72,BackColor=BarcaUi.Surface,Margin=new Padding(0,0,0,8)};
            row.Controls.Add(new Label{Tag="user-content",Text=song.Name,Bounds=new Rectangle(14,10,365,25),ForeColor=BarcaUi.Text,Font=BarcaUi.Font(10.5f,true),AutoEllipsis=true});
            row.Controls.Add(new Label{Tag="user-content",Text=song.Artist+(song.Album.Length>0?" · "+song.Album:""),Bounds=new Rectangle(15,41,365,22),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f),AutoEllipsis=true});
            var play=new BarcaButton{Text="播放 ▶",Bounds=new Rectangle(398,21,100,32),ForeColor=BarcaUi.Gold};
            play.Click+=async(_,_)=>{await Play(song);};row.Controls.Add(play);songs.Controls.Add(row);
        }
        if(list.Length==0)songs.Controls.Add(new Label{Text="输入歌名或歌手，快速点一首喜欢的歌。",Width=550,Height=60,Padding=new Padding(6,15,0,0),ForeColor=BarcaUi.Muted});
    }
    async Task<bool> Play(MusicSong song)
    {
        try{state.Text="正在准备QQ音乐并点歌…";string message=await RunControl("music_play",song.Id);if(IsDisposed)return false;state.Text=message;return true;}
        catch(OperationCanceledException){if(!released&&!IsDisposed)state.Text="已停止后续点歌；已提交的请求可能生效";return false;}
        catch(Exception e){if(!IsDisposed){state.Text=e.Message;if(!preview)OperationFeedback.Error("点歌失败："+e.Message,async()=>{await Play(song);},module:"music",operationId:"music:play:"+song.Id);}return false;}
    }
    async Task<string> RunControl(string action,string value="")
    {
        using var activity=controlActivities.Begin("control");
        try{var result=await client.Run(action,value,activity.Token);activity.ThrowIfNotCurrent();return result;}catch(Exception e)when(!activity.Current&&FeatureFaults.Recoverable(e)){throw new OperationCanceledException(activity.Token);}
    }
    public void Home()=>Display(client.History,"最近点歌");
    public MusicSong[] Candidates=>latest;
    internal bool SearchRetryVisible=>retrySearch.Visible;
    internal bool SearchStatusFits=>songs.Bottom<=retrySearch.Parent!.Top;
    public async Task<bool> TryPlayCandidate(string id){var song=latest.Concat(client.History).FirstOrDefault(s=>s.Id==id);return song!=null&&await Play(song);}
    public Task<string> Step(int direction)=>RunControl("music_step",direction.ToString());
    void Published(string query,MusicSong[] rows)=>ApplyPublished(Volatile.Read(ref presentationGeneration),query,rows);
    void ApplyPublished(long generation,string query,MusicSong[] rows)
    {
        if(released||IsDisposed||Disposing||!IsHandleCreated)return;
        if(InvokeRequired){try{BeginInvoke(()=>ApplyPublished(generation,query,rows));}catch(InvalidOperationException){}return;}
        if(!Visible||generation!=Volatile.Read(ref presentationGeneration)||pending!=null)return;
        input.Text=query;latest=rows;Display(rows,"搜索结果");state.Text=rows.Length>0?"点选歌曲，在QQ音乐播放":"没有找到歌曲，试试歌名加歌手";
    }

    public void OpenQuery(string query){input.Text=query;WindowInteraction.Show(this);if(query.Length>0)_=SearchAsync(query);}
    bool released;
    protected override void Dispose(bool disposing){if(disposing&&!released){released=true;theme.Dispose();client.Published-=Published;searchActivities.Dispose();controlActivities.Dispose();if(ownsClient)client.Dispose();}base.Dispose(disposing);}
    protected override void OnFormClosing(FormClosingEventArgs e){if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;searchActivities.Reset();Hide();return;}base.OnFormClosing(e);}
}
