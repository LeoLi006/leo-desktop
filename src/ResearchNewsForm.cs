using System.Diagnostics;
using System.Speech.Synthesis;
namespace LeoLauncher;

interface IResearchReader:IDisposable{void Read(string text);void Stop();}
sealed class ResearchReader:IResearchReader
{
    SpeechSynthesizer? speaker;
    public void Read(string text){speaker??=new();speaker.SpeakAsyncCancelAll();speaker.SetOutputToDefaultAudioDevice();speaker.SpeakAsync(text);}
    public void Stop()=>speaker?.SpeakAsyncCancelAll();
    public void Dispose(){try{Stop();}finally{speaker?.Dispose();speaker=null;}}
}

sealed class ResearchNewsForm:BarcaWindow
{
    readonly ResearchNewsFeed feed;readonly IResearchReader reader;readonly bool preview;
    readonly FlowLayoutPanel list=new(){AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
    readonly Label sources=new(),message=new();readonly TextBox search=new();
    readonly BarcaButton refresh=new(){Text="刷新"},read=new(){Text="播报当前列表"},stop=new(){Text="停止朗读"};
    readonly List<(BarcaButton Button,string Id)> filters=[];
    readonly ModuleThemeBinding theme;readonly ActivityOwner activities=new("research-news");
    string filter="all";bool refreshing,released;ResearchItem[] visible=[];
    internal ResearchItem[] VisibleItems=>visible.ToArray();
    internal bool Refreshing=>refreshing;
    public ResearchNewsForm(ResearchNewsFeed feed,bool preview=false,IResearchReader? reader=null)
    {
        this.feed=feed;this.preview=preview;this.reader=reader??new ResearchReader();
        Text="Leo · AI 动态";ClientSize=new(860,700);DoubleBuffered=true;
        Controls.Add(BarcaUi.Chrome(this,"AI 动态","Agent / 全双工语音  ·  论文与开源项目"));
        int x=22;foreach(var (name,id) in new[]{("全部","all"),("Agent","agent"),("全双工 / 实时语音","voice"),("论文","papers"),("开源项目","projects")}){
            var button=new BarcaButton{Text=name,Bounds=new(x,102,id=="voice"?158:id=="projects"?110:82,32),Selected=id=="all"};
            button.Click+=(_,_)=>{filter=id;foreach(var entry in filters){entry.Button.Selected=entry.Id==id;entry.Button.Invalidate();}Render();};filters.Add((button,id));Controls.Add(button);x=button.Right+8;
        }
        refresh.Bounds=new(748,102,90,32);refresh.Click+=async(_,_)=>await RefreshFeed(true);
        search.Bounds=new(24,148,428,28);search.BorderStyle=BorderStyle.FixedSingle;search.BackColor=BarcaUi.Surface;search.ForeColor=BarcaUi.Text;search.PlaceholderText=UiLanguage.T("筛选标题、摘要或作者");search.TextChanged+=(_,_)=>Render();
        read.Bounds=new(466,146,212,32);read.Click+=(_,_)=>Speak(visible.Take(5).ToArray());
        stop.Bounds=new(690,146,148,32);stop.Click+=(_,_)=>StopReading();
        sources.Bounds=new(24,190,814,42);sources.ForeColor=BarcaUi.Muted;sources.Font=BarcaUi.Font(8.5f);sources.UseMnemonic=false;UiLanguage.Preserve(sources);
        list.Bounds=new(22,239,816,394);list.BackColor=BarcaUi.Canvas;
        message.Bounds=new(24,642,814,45);message.ForeColor=BarcaUi.Muted;message.Font=BarcaUi.Font(9);message.UseMnemonic=false;
        Controls.AddRange([refresh,search,read,stop,sources,list,message]);
        list.SizeChanged+=(_,_)=>{foreach(Control card in list.Controls)card.Width=Math.Max(200,list.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4);};
        Shown+=async(_,_)=>{if(!preview)await RefreshFeed(false);};
        VisibleChanged+=(_,_)=>{if(!Visible){activities.Reset();StopReading();}};
        UiLanguage.RefreshOnChange(this,Render);theme=new(this);MinimumSize=MaximumSize=Size;BarcaScrollTheme.Attach(list);Render();
    }
    internal async Task RefreshFeed(bool force)
    {
        if(released||refreshing)return;refreshing=true;refresh.Enabled=false;
        using var operation=activities.Begin("refresh");
        try{
            message.Text=UiLanguage.T("正在获取公开动态…");
            var result=await feed.Load(force,operation.Token);
            if(released||operation.Token.IsCancellationRequested)return;
            Render();if(result.Note.Length>0)message.Text=UiLanguage.T(result.Note);
        }catch(OperationCanceledException){}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){if(!released&&!operation.Token.IsCancellationRequested)message.Text=UiLanguage.T("获取失败，已有内容保留；可点击刷新重试。");}
        finally{refreshing=false;if(!released)refresh.Enabled=true;}
    }
    internal void Render()
    {
        if(released)return;var snapshot=feed.Snapshot;
        string term=search.Text.Trim();visible=snapshot.Items.Where(i=>filter switch{"agent"=>i.Topic is "agent" or "both","voice"=>i.Topic is "voice" or "both","papers"=>i.Source=="arxiv","projects"=>i.Source!="arxiv",_=>true})
            .Where(i=>term.Length==0||(i.Title+" "+i.Summary+" "+i.Authors).Contains(term,StringComparison.OrdinalIgnoreCase)).DistinctBy(i=>i.Id).OrderByDescending(i=>i.Date).Take(60).ToArray();
        list.SuspendLayout();try{
            foreach(Control old in list.Controls.Cast<Control>().ToArray())old.Dispose();list.Controls.Clear();
            foreach(var item in visible){var card=new ResearchNewsCard(item,()=>Open(item),()=>Speak([item])){Width=Math.Max(200,list.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4)};list.Controls.Add(card);}
            if(visible.Length==0)list.Controls.Add(new Label{Text=UiLanguage.T(snapshot.Items.Length==0?"还没有动态；点击刷新获取，断网时会保留已有缓存。":"没有匹配的动态"),Width=770,Height=80,ForeColor=BarcaUi.Muted,Padding=new(10,20,10,0)});
        }finally{list.ResumeLayout(true);}
        string SourceName(string id)=>id=="arxiv"?"arXiv":id=="github-agent"?"GitHub · Agent":"GitHub · Voice";
        sources.Text=string.Join("   /   ",snapshot.Sources.Select(s=>SourceName(s.Id)+"  "+(s.Updated is{} updated?updated.ToLocalTime().ToString("MM-dd HH:mm"):"—")+"  "+UiLanguage.T(s.State=="ok"?"已获取":s.State=="rate-limited"?"限流，保留旧内容":"暂不可用，保留旧内容")));
        message.Text=snapshot.Note.Length>0?UiLanguage.T(snapshot.Note):UiLanguage.T("摘要来自原文，未自动翻译；项目为近30天活跃仓库按 Stars 筛选，并非日增星榜。朗读前5条，使用系统本地语音。除此窗口不轮询。");
        read.Enabled=visible.Length>0;
    }
    void Open(ResearchItem item)
    {
        try{if(!ResearchNewsFeed.SafeUrl(item.Url))throw new IOException("无效原文链接");if(!preview)Process.Start(new ProcessStartInfo(item.Url){UseShellExecute=true});}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){message.Text=UiLanguage.T("无法打开链接，请重试。");}
    }
    internal void Speak(ResearchItem[] items)
    {
        if(items.Length==0)return;
        try{reader.Read(string.Join("\n",items.Take(5).Select((i,n)=>$"{n+1}. {i.Title}. {i.Summary[..Math.Min(i.Summary.Length,350)]}")));}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){message.Text=UiLanguage.T("本地朗读不可用，请检查系统语音和音频设备；文字阅读不受影响。");}
    }
    void StopReading(){try{reader.Stop();}catch(Exception e)when(FeatureFaults.Recoverable(e)){/* Audio cleanup must not close the window. */}}
    protected override bool ProcessCmdKey(ref Message m,Keys key){if(key==Keys.Escape){Close();return true;}if(key==Keys.F5){_=RefreshFeed(true);return true;}return base.ProcessCmdKey(ref m,key);}
    protected override void Dispose(bool disposing)
    {
        if(disposing&&!released){released=true;activities.Dispose();StopReading();try{reader.Dispose();}catch(Exception e)when(FeatureFaults.Recoverable(e)){}theme.Dispose();}
        base.Dispose(disposing);
    }
}

sealed class ResearchNewsCard:Control
{
    readonly ResearchItem item;readonly Action open,speak;readonly Font heading=BarcaUi.Font(10.5f,true),body=BarcaUi.Font(9),small=BarcaUi.Font(8.5f);
    bool hover;
    public ResearchNewsCard(ResearchItem item,Action open,Action speak)
    {
        this.item=item;this.open=open;this.speak=speak;Height=160;Margin=new(0,0,0,10);Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.Link;AccessibleName=item.Title;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
        MouseEnter+=(_,_)=>{hover=true;Invalidate();};MouseLeave+=(_,_)=>{hover=false;Invalidate();};
        Click+=(_,_)=>{if(PointToClient(Cursor.Position) is var point&&point.Y>=Height-30&&point.X>=Width-160)speak();else open();};
    }
    protected override bool IsInputKey(Keys keyData)=>keyData==Keys.Enter||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;if(e.Control)speak();else open();}base.OnKeyDown(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.Clear(BarcaUi.Canvas);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var shape=Appearance.Path(new Rectangle(0,0,Width-1,Height-1),12);using var fill=new SolidBrush(hover||Focused?BarcaUi.Raised:BarcaUi.Surface);g.FillPath(fill,shape);
        using var border=new Pen(Focused?BarcaUi.Gold:BarcaUi.Line);g.DrawPath(border,shape);
        string topic=item.Topic=="both"?"Agent + Voice":item.Topic=="voice"?"Voice":"Agent";
        string meta=(item.Source=="arxiv"?"arXiv · "+UiLanguage.T("论文"):"GitHub · ★ "+item.Stars?.ToString("N0"))+"   /   "+topic+"   /   "+UiLanguage.T(item.Source=="arxiv"?"发表":"更新")+" "+item.Date.ToLocalTime().ToString("MM-dd");
        TextRenderer.DrawText(g,meta,small,new Rectangle(16,10,Width-32,20),BarcaUi.Gold,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g,item.Title,heading,new Rectangle(16,35,Width-32,46),BarcaUi.Text,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g,item.Summary,body,new Rectangle(16,84,Width-32,40),BarcaUi.Muted,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g,UiLanguage.T("点击打开原文")+"  ↗",small,new Rectangle(16,134,Width/2,20),BarcaUi.Muted,TextFormatFlags.SingleLine);
        TextRenderer.DrawText(g,UiLanguage.T("朗读此条")+"  ▷",small,new Rectangle(Width-145,134,128,20),BarcaUi.Gold,TextFormatFlags.Right|TextFormatFlags.SingleLine);
    }
    protected override void Dispose(bool disposing){if(disposing){heading.Dispose();body.Dispose();small.Dispose();}base.Dispose(disposing);}
}
