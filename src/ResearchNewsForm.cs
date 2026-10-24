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
    readonly ComboBox areaSelector=new(){DropDownStyle=ComboBoxStyle.DropDownList,IntegralHeight=false,DropDownHeight=312,DropDownWidth=520,AccessibleName="AI 专业领域"};
    readonly BarcaButton refresh=new(){Text="刷新"},read=new(){Text="播报当前列表"},stop=new(){Text="停止朗读"};
    readonly List<(BarcaButton Button,string Id)> filters=[];
    readonly ResearchTitleTranslator translator;readonly bool injectedTranslator;
    readonly BarcaButton translate=new(){Text="中文标题 · API"},original=new(){Text="显示原文"};
    readonly UiToolTip translationTip=new();bool translating,showChinese=true;CancellationTokenSource? translationCancel;
    readonly BarcaButton overview=new(){Text="速览",Selected=true},news=new(){Text="新闻"};
    readonly System.Windows.Forms.Timer selectionDelay=new(){Interval=350};
    bool newsMode,refreshing,released;int generation;string areaId="all",filter="all";ResearchItem[] visible=[];
    readonly ModuleThemeBinding theme;readonly ActivityOwner activities=new("research-news");
    string Mode=>newsMode?"news":"overview";
    internal ResearchItem[] VisibleItems=>visible.ToArray();
    internal bool Refreshing=>refreshing;
    public ResearchNewsForm(ResearchNewsFeed feed,bool preview=false,IResearchReader? reader=null,ResearchTitleTranslator? translator=null)
    {
        this.feed=feed;this.preview=preview;this.reader=reader??new ResearchReader();injectedTranslator=translator!=null;this.translator=translator??new ResearchTitleTranslator(preview?null:Path.Combine(Store.Root,"leo-news-title-translations.json"));
        Text="Leo · AI 动态";ClientSize=new(860,700);DoubleBuffered=true;
        var header=BarcaUi.Chrome(this,"AI 动态","AI 新闻 / 论文与项目速览  ·  按领域筛选");Controls.Add(header);
        overview.Bounds=new(562,18,84,30);news.Bounds=new(654,18,84,30);
        overview.Click+=(_,_)=>SetView(false);news.Click+=(_,_)=>SetView(true);header.Controls.AddRange([overview,news]);overview.BringToFront();news.BringToFront();
        void FitHeaderTitle(){header.Controls.OfType<Label>().First().Width=Math.Min(500,Math.Max(100,header.Width-360));}header.SizeChanged+=(_,_)=>FitHeaderTitle();FitHeaderTitle();
        Controls.Add(new Label{Text="领域",Bounds=new(24,107,45,25),ForeColor=BarcaUi.Muted});
        CompanionUi.ThemeCombo(areaSelector);areaSelector.Bounds=new(73,103,294,30);foreach(var area in ResearchAreas.All)areaSelector.Items.Add(new ComboChoice(area.Name,area.Id,true));areaSelector.SelectedIndex=0;
        areaSelector.SelectedIndexChanged+=(_,_)=>{areaId=((ComboChoice)areaSelector.SelectedItem!).Value;SelectionChanged();};Controls.Add(areaSelector);
        int x=383;foreach(var (name,id) in new[]{("全部","all"),("论文","papers"),("开源项目","projects")}){
            var button=new BarcaButton{Text=name,Bounds=new(x,102,id=="projects"?110:82,32),Selected=id=="all"};button.Click+=(_,_)=>{filter=id;foreach(var entry in filters){entry.Button.Selected=entry.Id==id;entry.Button.Invalidate();}Render();};filters.Add((button,id));Controls.Add(button);x=button.Right+8;
        }
        translate.Bounds=new(383,102,158,32);translate.Click+=async(_,_)=>{if(translating){translationCancel?.Cancel();return;}await TranslateTitles();};
        original.Bounds=new(549,102,166,32);original.Click+=(_,_)=>{StopReading();showChinese=!showChinese;Render();};Controls.AddRange([translate,original]);
        translationTip.SetToolTip(translate,"手动调用已配置模型，仅发送当前新闻标题；每20条合并一次，最多3次请求，计入 API 用量和上限。已翻译标题直接使用缓存。");
        refresh.Bounds=new(748,102,90,32);refresh.Click+=async(_,_)=>await RefreshFeed(true);
        search.Bounds=new(24,148,428,28);search.BorderStyle=BorderStyle.FixedSingle;search.BackColor=BarcaUi.Surface;search.ForeColor=BarcaUi.Text;search.PlaceholderText=UiLanguage.T("筛选标题、摘要或作者");search.TextChanged+=(_,_)=>Render();
        read.Bounds=new(466,146,212,32);read.Click+=(_,_)=>Speak(visible.Take(5).ToArray());stop.Bounds=new(690,146,148,32);stop.Click+=(_,_)=>StopReading();
        sources.Bounds=new(24,190,814,42);sources.ForeColor=BarcaUi.Muted;sources.Font=BarcaUi.Font(8.5f);sources.UseMnemonic=false;UiLanguage.Preserve(sources);
        list.Bounds=new(22,239,816,394);list.BackColor=BarcaUi.Canvas;message.Bounds=new(24,642,814,45);message.ForeColor=BarcaUi.Muted;message.Font=BarcaUi.Font(9);message.UseMnemonic=false;
        Controls.AddRange([refresh,search,read,stop,sources,list,message]);list.SizeChanged+=(_,_)=>{foreach(Control card in list.Controls)card.Width=Math.Max(200,list.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4);};
        selectionDelay.Tick+=async(_,_)=>{selectionDelay.Stop();await RefreshFeed(false);};Shown+=async(_,_)=>{if(!preview)await RefreshFeed(false);};
        VisibleChanged+=(_,_)=>{if(!Visible){generation++;selectionDelay.Stop();activities.Reset();refreshing=false;translating=false;StopReading();}};
        UiLanguage.RefreshOnChange(this,Render);theme=new(this);MinimumSize=MaximumSize=Size;BarcaScrollTheme.Attach(list);Render();
    }
    void SetView(bool value){if(newsMode==value)return;newsMode=value;overview.Selected=!value;news.Selected=value;overview.Invalidate();news.Invalidate();SelectionChanged();}
    void SelectionChanged(){generation++;selectionDelay.Stop();activities.Reset();refreshing=false;translating=false;refresh.Enabled=true;StopReading();Render();if(Visible&&!preview)selectionDelay.Start();}
    internal async Task RefreshFeed(bool force)
    {
        selectionDelay.Stop();if(released||refreshing)return;refreshing=true;refresh.Enabled=false;int version=generation;string mode=Mode,field=areaId;
        using var operation=activities.Begin("refresh");
        try{message.Text=UiLanguage.T("正在获取公开动态…");var result=await feed.Load(mode,field,force,operation.Token);if(released||!operation.Current||version!=generation)return;Render();if(result.Note.Length>0)message.Text=UiLanguage.T(result.Note);}
        catch(OperationCanceledException){}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){if(!released&&operation.Current&&version==generation)message.Text=UiLanguage.T("获取失败，已有内容保留；可点击刷新重试。");}
        finally{if(!released&&version==generation){refreshing=false;refresh.Enabled=true;}}
    }
    internal void Render()
    {
        if(released)return;var snapshot=feed.Get(Mode,areaId);foreach(var entry in filters)entry.Button.Visible=!newsMode;
        string term=search.Text.Trim();visible=snapshot.Items.Where(i=>newsMode?ResearchNewsFeed.IsNews(i):!ResearchNewsFeed.IsNews(i)&&(filter=="all"||filter=="papers"&&i.Source=="arxiv"||filter=="projects"&&i.Source=="github"))
            .Where(i=>term.Length==0||(i.Title+" "+i.Summary+" "+i.Authors+" "+i.Publisher+" "+translator.Display(i.Title)).Contains(term,StringComparison.OrdinalIgnoreCase))
            .DistinctBy(i=>newsMode?System.Text.RegularExpressions.Regex.Replace(i.Title.ToLowerInvariant(),@"\W",""):i.Id)
            .OrderBy(i=>newsMode?0:i.Source=="arxiv"?0:1).ThenByDescending(i=>i.Date).Take(60).ToArray();
        list.SuspendLayout();try{
            foreach(Control old in list.Controls.Cast<Control>().ToArray())old.Dispose();list.Controls.Clear();
            foreach(var group in visible.GroupBy(i=>newsMode?"news":i.Source=="arxiv"?"papers":"projects")){
                var section=new Label{Text=UiLanguage.T(group.Key=="news"?"AI 新闻":group.Key=="papers"?"论文动态":"开源项目")+"  /  "+group.Count(),Width=770,Height=26,ForeColor=BarcaUi.Gold,Margin=new(4,3,0,4)};UiLanguage.Preserve(section);list.Controls.Add(section);
                foreach(var item in group)list.Controls.Add(new ResearchNewsCard(item,()=>Open(item),()=>Speak([item]),DisplayTitle(item)){Width=Math.Max(200,list.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4)});
            }
            if(visible.Length==0)list.Controls.Add(new Label{Text=UiLanguage.T(snapshot.Items.Length==0?"该领域暂无缓存，刷新后显示相关内容。":"没有匹配的动态"),Width=770,Height=80,ForeColor=BarcaUi.Muted,Padding=new(10,20,10,0)});
        }finally{list.ResumeLayout(true);}
        translate.Visible=original.Visible=newsMode;translate.Text=UiLanguage.T(translating?"取消翻译":"中文标题 · API");
        translate.Enabled=translating||visible.Any(i=>ResearchNewsFeed.IsNews(i)&&translator.Missing(i.Title));original.Text=UiLanguage.T(showChinese?"显示原文":"显示中文");original.Enabled=visible.Any(i=>translator.Display(i.Title)!=i.Title);
        sources.Text=string.Join("   /   ",snapshot.Sources.Select(s=>ResearchNewsFeed.SourceName(s.Id)+"  "+(s.Updated is{} updated?updated.ToLocalTime().ToString("MM-dd HH:mm"):"—")+"  "+UiLanguage.T(s.State=="ok"?"已获取":s.State=="rate-limited"?"限流":"暂不可用")));
        message.Text=snapshot.Note.Length>0?UiLanguage.T(snapshot.Note):UiLanguage.T(newsMode?"新闻来自媒体与官方发布，不混入论文摘要；按发布时间排序。摘要保留原文，点击打开报道。朗读前5条；仅手动翻译标题时调用模型 API。":"速览：先论文、后 GitHub；项目为近30天活跃仓库按总 Stars 筛选。标题和短导语来自原文，不调用模型 API。");read.Enabled=visible.Length>0;
    }
    internal string DisplayTitle(ResearchItem item)=>newsMode&&showChinese?translator.Display(item.Title):item.Title;
    internal async Task TranslateTitles()
    {
        if(released||translating||!newsMode)return;if(preview&&!injectedTranslator){message.Text=UiLanguage.T("示例模式不调用翻译 API。");return;}
        var titles=visible.Where(ResearchNewsFeed.IsNews).Select(i=>i.Title).ToArray();if(!titles.Any(translator.Missing))return;
        int version=generation;string? completion=null;using var operation=activities.Begin("title-translation");using var cancel=CancellationTokenSource.CreateLinkedTokenSource(operation.Token);translationCancel=cancel;translating=true;showChinese=true;StopReading();Render();message.Text=UiLanguage.T("正在翻译标题，可点击取消翻译…");
        try{var result=await translator.Translate(titles,cancel.Token);if(operation.Current&&version==generation)completion=result.Note.Length>0?UiLanguage.T(result.Note):UiLanguage.T("标题翻译完成，可切换查看原文。");}
        catch(OperationCanceledException)when(cancel.IsCancellationRequested){completion=UiLanguage.T("已取消翻译，保留已完成的结果。");}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){completion=UiLanguage.T("标题翻译失败，原文仍可阅读：")+UiLanguage.T(e.Message);}
        finally{if(ReferenceEquals(translationCancel,cancel))translationCancel=null;if(!released&&version==generation){translating=false;Render();if(completion!=null)message.Text=completion;}}
    }
    void Open(ResearchItem item){try{if(!ResearchNewsFeed.SafeUrl(item.Url))throw new IOException("无效原文链接");if(!preview)Process.Start(new ProcessStartInfo(item.Url){UseShellExecute=true});}catch(Exception e)when(FeatureFaults.Recoverable(e)){message.Text=UiLanguage.T("无法打开链接，请重试。");}}
    internal void Speak(ResearchItem[] items){if(items.Length==0)return;try{reader.Read(string.Join("\n",items.Take(5).Select((i,n)=>$"{n+1}. {DisplayTitle(i)}. {ResearchNewsCard.Brief(i.Summary)}")));}catch(Exception e)when(FeatureFaults.Recoverable(e)){message.Text=UiLanguage.T("本地朗读不可用，请检查系统语音和音频设备；文字阅读不受影响。");}}
    void StopReading(){try{reader.Stop();}catch(Exception e)when(FeatureFaults.Recoverable(e)){}}
    protected override bool ProcessCmdKey(ref Message m,Keys key){if(key==Keys.Escape){Close();return true;}if(key==Keys.F5){_=RefreshFeed(true);return true;}return base.ProcessCmdKey(ref m,key);}
    protected override void Dispose(bool disposing){if(disposing&&!released){released=true;generation++;selectionDelay.Stop();selectionDelay.Dispose();translationTip.Dispose();activities.Dispose();StopReading();try{reader.Dispose();}catch(Exception e)when(FeatureFaults.Recoverable(e)){}theme.Dispose();}base.Dispose(disposing);}
}

sealed class ResearchNewsCard:Control
{
    readonly ResearchItem item;readonly string displayTitle;readonly Action open,speak;readonly Font heading=BarcaUi.Font(10.5f,true),body=BarcaUi.Font(9),small=BarcaUi.Font(8.5f);bool hover;
    public ResearchNewsCard(ResearchItem item,Action open,Action speak,string? displayTitle=null)
    {
        this.item=item;this.displayTitle=displayTitle??item.Title;this.open=open;this.speak=speak;Height=130;Margin=new(0,0,0,10);Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.Link;AccessibleName=this.displayTitle;AccessibleDescription=item.Title;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
        MouseEnter+=(_,_)=>{hover=true;Invalidate();};MouseLeave+=(_,_)=>{hover=false;Invalidate();};Click+=(_,_)=>{if(PointToClient(Cursor.Position) is var point&&point.Y>=Height-30&&point.X>=Width-160)speak();else open();};
    }
    internal static string Brief(string summary){string text=summary.Trim();var sentence=System.Text.RegularExpressions.Regex.Match(text,@"^.*?[.!?。！？](?:\s|$)");if(sentence.Success&&sentence.Length>=35)text=sentence.Value.Trim();if(text.Length<=180)return text;int end=text.LastIndexOf(' ',179,80);return text[..(end>=100?end:179)].TrimEnd()+"…";}
    protected override bool IsInputKey(Keys keyData)=>keyData==Keys.Enter||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;if(e.Control)speak();else open();}base.OnKeyDown(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.Clear(BarcaUi.Canvas);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var shape=Appearance.Path(new Rectangle(0,0,Width-1,Height-1),12);using var fill=new SolidBrush(hover||Focused?BarcaUi.Raised:BarcaUi.Surface);g.FillPath(fill,shape);using var border=new Pen(Focused?BarcaUi.Gold:BarcaUi.Line);g.DrawPath(border,shape);
        string meta=ResearchNewsFeed.IsNews(item)?UiLanguage.T("新闻")+"  ·  "+item.Publisher+"  ·  "+item.Date.ToLocalTime().ToString("MM-dd HH:mm"):
            UiLanguage.T(item.Source=="arxiv"?"论文速报":"项目速报")+"  ·  "+ResearchAreas.Label(item.Topic)+"  ·  "+item.Date.ToLocalTime().ToString("MM-dd")+"  ·  "+(item.Source=="arxiv"?"arXiv":"GitHub · ★ "+item.Stars?.ToString("N0"));
        if(displayTitle!=item.Title)meta+="  ·  "+UiLanguage.T("中文译题");
        TextRenderer.DrawText(g,meta,small,new Rectangle(16,10,Width-32,20),BarcaUi.Gold,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g,displayTitle,heading,new Rectangle(16,30,Width-32,42),BarcaUi.Text,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g,item.Summary.Length==0?UiLanguage.T("点击查看新闻正文"):Brief(item.Summary),body,new Rectangle(16,75,Width-32,20),BarcaUi.Muted,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g,UiLanguage.T("点击打开原文")+"  ↗",small,new Rectangle(16,Height-26,Width/2,20),BarcaUi.Muted,TextFormatFlags.SingleLine);TextRenderer.DrawText(g,UiLanguage.T("朗读此条")+"  ▷",small,new Rectangle(Width-145,Height-26,128,20),BarcaUi.Gold,TextFormatFlags.Right|TextFormatFlags.SingleLine);
    }
    protected override void Dispose(bool disposing){if(disposing){heading.Dispose();body.Dispose();small.Dispose();}base.Dispose(disposing);}
}
