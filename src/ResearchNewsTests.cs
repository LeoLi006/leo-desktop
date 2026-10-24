using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;
static class ResearchNewsTests
{
    static string Entry(string id,string title,string summary)=>$"<entry><id>http://arxiv.org/abs/{id}</id><title>{title}</title><summary>{summary}</summary><published>2025-10-20T12:00:00Z</published><author><name>Fixture Author</name></author></entry>";
    static readonly string agent=Entry("2510.12345v2","Language model agents","Agentic planning and tool use."),voice=Entry("2510.12346","Full-duplex speech interaction","Streaming speech-to-speech with turn-taking."),vision=Entry("2510.12347","Computer vision segmentation","A new segmentation method for visual recognition.");
    static string Atom(string entries)=>"<feed xmlns='http://www.w3.org/2005/Atom'>"+entries+"</feed>";
    internal const string Github="""
        {"items":[{"full_name":"fixture/voice-agent","description":"Full-duplex voice agent and real-time speech-to-speech.","html_url":"https://github.com/fixture/voice-agent","stargazers_count":1234,"pushed_at":"2025-10-22T12:00:00Z","archived":false},{"full_name":"fixture/vision","description":"Computer vision segmentation.","html_url":"https://github.com/fixture/vision","stargazers_count":555,"pushed_at":"2025-10-23T12:00:00Z","archived":false}]}
        """;
    static string Rss(string host)
    {
        string prefix=host=="news.google.com"?"https://news.google.com/rss/articles/":host=="openai.com"?"https://openai.com/index/":host=="deepmind.google"?"https://deepmind.google/blog/":"https://www.qbitai.com/2025/10/";
        return "<rss><channel>"+
            $"<item><title>Agent product release update</title><description>Agentic AI agents bring new product capabilities.</description><link>{prefix}agent-release</link><pubDate>Wed, 22 Oct 2025 12:00:00 GMT</pubDate><source>Fixture News</source></item>"+
            $"<item><title>计算机视觉推出新模型</title><description>Computer vision segmentation announcement.</description><link>{prefix}vision-release</link><pubDate>Thu, 23 Oct 2025 12:00:00 GMT</pubDate><source>Fixture News</source></item>"+
            "<item><title>Agent paper</title><description>Paper abstract.</description><link>https://arxiv.org/abs/2510.12345</link><pubDate>Wed, 22 Oct 2025 12:00:00 GMT</pubDate><source>arXiv</source></item></channel></rss>";
    }
    sealed class Http:HttpMessageHandler
    {
        public int Requests;public bool Fail,Hold;public string? FailHost;public readonly List<Uri> Uris=[];public readonly TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Requests++;var uri=request.RequestUri!;Uris.Add(uri);Started.TrySetResult();if(Hold)await Task.Delay(Timeout.Infinite,token);
            string query=Uri.UnescapeDataString(uri.Query);string text=uri.Host=="api.github.com"?Github:uri.Host=="export.arxiv.org"?Atom(query.Contains("search_query=(cat:cs.CV)")?vision:query.Contains("all:agentic")?agent:query.Contains("all:\"full-duplex\"")?voice:agent+voice+vision):Rss(uri.Host);
            return new(Fail||uri.Host==FailHost?HttpStatusCode.TooManyRequests:HttpStatusCode.OK){Content=new StringContent(text)};
        }
    }
    sealed class Reader:IResearchReader{public int Reads,Stops;public bool Released,Fail;public string Text="";public void Read(string text){if(Fail)throw new IOException("fixture audio");Reads++;Text=text;}public void Stop()=>Stops++;public void Dispose()=>Released=true;}
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    static void Area(ResearchNewsForm form,string id){var combo=Field<ComboBox>(form,"areaSelector");combo.SelectedItem=combo.Items.Cast<ComboChoice>().Single(c=>c.Value==id);}
    static ResearchNewsFeed Feed(string? path,HttpClient http,Func<DateTimeOffset> clock)=>new(path,http,clock,(_,_)=>Task.CompletedTask);
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var host=new Form{Opacity=0,ShowInTaskbar=false};host.Shown+=async(_,_)=>{
            var c=new Dictionary<string,bool>();try{
                var time=new DateTimeOffset(2025,10,24,12,0,0,TimeSpan.Zero);string cache=Path.Combine(Path.GetDirectoryName(report)!,Guid.NewGuid().ToString("N")+".json");
                var handler=new Http();using var http=new HttpClient(handler);using var feed=Feed(cache,http,()=>time);
                c["broad_specialist_fields"]=ResearchAreas.All.Length>=24&&ResearchAreas.All.Select(a=>a.Id).Distinct().Count()==ResearchAreas.All.Length&&new[]{"agent","duplex","vision","medical","systems","privacy","ai4science"}.All(id=>ResearchAreas.All.Any(a=>a.Id==id));
                c["field_specific_remote_queries"]=ResearchNewsFeed.Endpoint("arxiv",time,ResearchAreas.Get("vision"))!=ResearchNewsFeed.Endpoint("arxiv",time,ResearchAreas.Get("agent"))&&ResearchNewsFeed.Endpoint("news-google",time,ResearchAreas.Get("duplex"))!=ResearchNewsFeed.Endpoint("news-google",time,ResearchAreas.Get("all"));
                c["arxiv_beyond_agent_and_voice"]=ResearchNewsFeed.ParseArxiv(Atom(agent+voice+vision),ResearchAreas.Get("all")).Length==3;
                c["github_specialist_filter"]=ResearchNewsFeed.ParseGithub(Github,ResearchAreas.Get("vision")).Single().Title=="fixture/vision";
                c["actual_news_not_papers"]=ResearchNewsFeed.ParseNews(Rss("news.google.com"),"news-google",ResearchAreas.Get("all")) is{Length:2} newsItems&&newsItems.All(ResearchNewsFeed.IsNews);
                c["news_field_filter"]=ResearchNewsFeed.ParseNews(Rss("news.google.com"),"news-google",ResearchAreas.Get("vision")).Single().Title.Contains("视觉");
                c["unsafe_links_rejected"]=new[]{"file:///C:/test.exe","https://github.com.evil.test/a/b","https://user@github.com/a/b","https://evil.test/article","https://arxiv.org.evil.test/abs/2510.12345"}.All(u=>!ResearchNewsFeed.SafeUrl(u));
                bool xmlRejected=false;try{ResearchNewsFeed.Xml("<!DOCTYPE feed [<!ENTITY x SYSTEM 'file:///test'>]><feed>&x;</feed>");}catch(System.Xml.XmlException){xmlRejected=true;}c["external_xml_disabled"]=xmlRejected;
                var overview=await feed.Load("overview","all",false,default);c["overview_only_papers_projects"]=handler.Requests==2&&overview.Items.Length==5&&overview.Items.All(i=>!ResearchNewsFeed.IsNews(i));
                await feed.Load("overview","all",true,default);c["repeat_refresh_coalesced"]=handler.Requests==2;
                var news=await feed.Load("news","all",false,default);c["separate_news_sources"]=handler.Requests==6&&news.Items.Length==8&&news.Items.All(ResearchNewsFeed.IsNews);
                int before=handler.Requests;await feed.Load("news","vision",false,default);c["field_change_fetches_targeted_news"]=handler.Requests==before+1&&handler.Uris.Last().Host=="news.google.com"&&feed.Get("news","vision").Items.All(i=>i.Title.Contains("视觉"));
                await feed.Load("overview","vision",false,default);c["field_change_fetches_papers_and_projects"]=feed.Get("overview","vision").Items.Length==2&&feed.Get("overview","vision").Items.All(i=>i.Topic=="vision");
                c["independent_field_and_mode_cache"]=feed.Get("news","all").Items.Length==8&&feed.Get("overview","all").Items.Length==5&&feed.Get("news","duplex").Items.Length==0;
                using(var reload=Feed(cache,http,()=>time))c["cache_reopen_by_field"]=reload.Get("news","all").Items.Length==8&&reload.Get("overview","vision").Items.Length==2;
                overview.Items[0]=overview.Items[0] with{Title="mutated"};c["snapshot_detached"]=feed.Get("overview","all").Items.All(i=>i.Title!="mutated");
                var oldTime=feed.Get("overview","all").Sources.Single(s=>s.Id=="arxiv").Updated;handler.FailHost="export.arxiv.org";time=time.AddMinutes(12);var partial=await feed.Load("overview","all",true,default);
                c["failure_preserves_exact_field_content_age"]=partial.Items.Length==5&&partial.Sources.Single(s=>s.Id=="arxiv").Updated==oldTime&&partial.Sources.Single(s=>s.Id=="arxiv").State=="rate-limited"&&partial.Sources.Single(s=>s.Id=="github").State=="ok";
                handler.Fail=true;var oldNews=feed.Get("news","all");var offline=await feed.Load("news","all",true,default);
                c["offline_news_preserves_content_and_times"]=offline.Items.Length==oldNews.Items.Length&&offline.Sources.All(x=>x.State=="rate-limited")&&offline.Sources.Select(x=>x.Updated).SequenceEqual(oldNews.Sources.Select(x=>x.Updated));handler.Fail=false;
                var savedText=File.ReadAllText(cache);var hook=AtomicJson.BeforeReplace;time=time.AddMinutes(12);
                try{AtomicJson.BeforeReplace=path=>{if(path==cache)throw new IOException("fixture cache failure");};var unsaved=await feed.Load("news","all",true,default);c["cache_save_failure_preserves_previous_file"]=File.ReadAllText(cache)==savedText&&unsaved.Items.Length>0&&unsaved.Note.Contains("缓存保存失败");}finally{AtomicJson.BeforeReplace=hook;}
                int translationCalls=0;var titleTranslator=new ResearchTitleTranslator(null,(titles,_)=>{translationCalls++;return Task.FromResult(JsonSerializer.Serialize(new{translations=titles.Select((t,id)=>new{id,title="智能体产品发布更新"})}));});
                var reader=new Reader();using var form=new ResearchNewsForm(feed,true,reader,titleTranslator){StartPosition=FormStartPosition.Manual,Location=new(-12000,-12000)};before=handler.Requests;form.Show();
                c["no_background_network_or_audio"]=handler.Requests==before&&reader.Reads==0;
                c["professional_removed"]=typeof(ResearchNewsForm).GetField("professional",BindingFlags.Instance|BindingFlags.NonPublic)==null;
                c["papers_before_newer_projects"]=form.VisibleItems.Take(3).All(i=>i.Source=="arxiv")&&form.VisibleItems.Skip(3).All(i=>i.Source=="github");
                var list=Field<FlowLayoutPanel>(form,"list");c["retains_compact_brief"]=list.Controls.OfType<ResearchNewsCard>().All(card=>card.Height==130);
                Field<BarcaButton>(form,"news").PerformClick();c["news_view_contains_only_news"]=form.VisibleItems.Length==2&&form.VisibleItems.All(ResearchNewsFeed.IsNews)&&form.VisibleItems.First().Date>form.VisibleItems.Last().Date;
                c["no_automatic_translation"]=translationCalls==0;
                await form.TranslateTitles();var translatedItem=form.VisibleItems.Single(i=>i.Title=="Agent product release update");
                c["news_title_translated_without_changing_source"]=form.DisplayTitle(translatedItem)=="智能体产品发布更新"&&translationCalls==1&&translatedItem.Url.Contains("https://");
                Field<BarcaButton>(form,"original").PerformClick();c["original_toggle_no_model_call"]=form.DisplayTitle(translatedItem)==translatedItem.Title&&translationCalls==1;Field<BarcaButton>(form,"original").PerformClick();
                Field<TextBox>(form,"search").Text="智能体产品";c["chinese_translation_search"]=form.VisibleItems.Length==1;Field<TextBox>(form,"search").Clear();
                using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,form.ClientRectangle);image.Save(Path.Combine(Path.GetDirectoryName(report)!,"ai-news.png"));}
                Area(form,"vision");c["dropdown_changes_related_results"]=form.VisibleItems.Length==1&&form.VisibleItems.Single().Title.Contains("视觉");
                Field<BarcaButton>(form,"overview").PerformClick();c["shared_field_selection"]=form.VisibleItems.Length==2&&form.VisibleItems.All(i=>i.Topic=="vision");
                Field<TextBox>(form,"search").Text="segmentation";c["local_text_filter_no_network"]=form.VisibleItems.Length==2&&handler.Requests==before;
                form.Speak(form.VisibleItems);c["explicit_local_narration"]=reader.Reads==1&&reader.Text.Contains("vision");reader.Fail=true;form.Speak(form.VisibleItems);c["audio_failure_isolated"]=!form.IsDisposed;
                Field<TextBox>(form,"search").Clear();UiLanguage.Configure("en");form.Render();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,form.ClientRectangle);image.Save(Path.Combine(Path.GetDirectoryName(report)!,"ai-overview-en.png"));}UiLanguage.Configure("zh-CN");
                form.Close();c["closing_releases_reader"]=reader.Stops>0&&reader.Released;
                var prefs=new Preferences{ResearchNewsModule=true};int opened=0;var module=new ResearchNewsModule(()=>prefs.ResearchNewsModule,t=>{t.ThrowIfCancellationRequested();opened++;return Task.CompletedTask;});using var bus=new ActionDispatcher(prefs,(_,_,_)=>throw new Exception("legacy forbidden"),new ExtensionStore(Path.Combine(Path.GetDirectoryName(report)!,"no-extensions")));bus.Registry.Register(module);EntryContributions.Bind(prefs,bus.Registry,bus);
                c["contract_and_entry_valid"]=ModuleContracts.Snapshot(module).Contract.Enablement=="optional"&&EntryContributions.ReadModules(prefs).Buttons.Single().Glyph=="news";
                c["unified_entry_execution"]=(await EntryContributions.ExecuteModule(prefs,"research-news/open","test")).Status=="succeeded"&&opened==1;
                c["extra_parameters_rejected"]=(await bus.ExecuteStructuredAsync(new("leo-action/1","research_open",JsonSerializer.SerializeToElement(new{url="https://evil.test"})))).Status=="failed";
                prefs.ResearchNewsModule=false;bool disabled=false;try{await EntryContributions.ExecuteModule(prefs,"research-news/open","stale");}catch(InvalidOperationException){disabled=true;}c["disabled_old_click_rejected"]=disabled;
                using var waitingHandler=new Http{Hold=true};using var waitingHttp=new HttpClient(waitingHandler);using var waitingFeed=Feed(null,waitingHttp,()=>time);using var closing=new ResearchNewsForm(waitingFeed,true,new Reader());closing.Show();var pending=closing.RefreshFeed(true);await waitingHandler.Started.Task;Area(closing,"vision");await pending;
                c["old_field_request_cancelled"]=waitingFeed.Get("overview","all").Items.Length==0&&closing.VisibleItems.Length==0&&!closing.Refreshing;
                waitingHandler.Hold=false;await closing.RefreshFeed(false);c["new_field_recovers_after_cancel"]=closing.VisibleItems.Length==2&&closing.VisibleItems.All(i=>i.Topic=="vision");closing.Close();c["activities_released"]=ModuleActivities.Snapshot().All(s=>s.Module!="research-news");
                string bounded=Path.Combine(Path.GetDirectoryName(report)!,Guid.NewGuid().ToString("N")+".json");using(var boundedHandler=new Http())using(var boundedHttp=new HttpClient(boundedHandler))using(var boundedFeed=Feed(bounded,boundedHttp,()=>time)){
                    foreach(var area in ResearchAreas.All.Take(9))await boundedFeed.Load("news",area.Id,false,default);
                    var saved=JsonSerializer.Deserialize<ResearchCache>(File.ReadAllText(bounded))!;c["cache_bounded_to_eight_selections"]=saved.Snapshots.Length==8&&boundedFeed.Get("news","all").Items.Length==0;
                }
                var translationStarted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var blockedTranslator=new ResearchTitleTranslator(null,async(_,token)=>{translationStarted.SetResult();await Task.Delay(Timeout.Infinite,token);return "";});
                using(var translatingForm=new ResearchNewsForm(feed,true,new Reader(),blockedTranslator)){
                    translatingForm.Show();Field<BarcaButton>(translatingForm,"news").PerformClick();var translating=translatingForm.TranslateTitles();await translationStarted.Task;Area(translatingForm,"vision");await translating;
                    c["field_switch_cancels_title_translation"]=blockedTranslator.Display("Agent product release update")=="Agent product release update"&&ModuleActivities.Snapshot().All(a=>a.Module!="research-news");translatingForm.Close();
                }
                await ResearchTitleTranslationTests.Check(Path.GetDirectoryName(report)!,c);
                File.WriteAllText(report,JsonSerializer.Serialize(new{ok=c.Values.All(x=>x),checks=c,model_calls=0,microphone_used=false},new JsonSerializerOptions{WriteIndented=true}));
            }catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{host.Close();}
        };Application.Run(host);
    }
    public static void Live(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);try{using var feed=new ResearchNewsFeed();using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(80));var results=new List<object>();
            foreach(var (mode,field) in new[]{("overview","vision"),("news","all"),("news","duplex")}){var data=feed.Load(mode,field,true,cancel.Token).GetAwaiter().GetResult();results.Add(new{mode,field,items=data.Items.Length,sources=data.Sources,examples=data.Items.Take(3).Select(i=>new{i.Title,i.Url,i.Publisher})});}
            File.WriteAllText(report,JsonSerializer.Serialize(new{ok=true,results,model_calls=0},new JsonSerializerOptions{WriteIndented=true}));
        }catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.GetType().Name}));}
    }
}
