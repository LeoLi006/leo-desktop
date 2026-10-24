using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;

static class ResearchNewsTests
{
    internal const string Atom="""
        <feed xmlns="http://www.w3.org/2005/Atom">
        <entry><id>http://arxiv.org/abs/2510.12345v2</id><title>Full-duplex speech agents</title><summary>A streaming speech-to-speech agent with interruption and turn-taking.</summary><published>2025-10-20T12:00:00Z</published><author><name>Fixture Author</name></author></entry>
        <entry><id>https://arxiv.org/abs/2510.54321</id><title>Language model agents</title><summary>Tool use and planning.</summary><published>2025-10-21T12:00:00Z</published></entry>
        <entry><id>https://evil.test/abs/2510.54321</id><title>Agent</title><summary>Untrusted link.</summary><published>2025-10-21T12:00:00Z</published></entry>
        </feed>
        """;
    internal const string Github="""
        {"incomplete_results":false,"items":[{"full_name":"fixture/voice-agent","description":"Full-duplex voice agent and real-time speech-to-speech.","html_url":"https://github.com/fixture/voice-agent","stargazers_count":1234,"pushed_at":"2025-10-22T12:00:00Z","archived":false}]}
        """;
    sealed class Http:HttpMessageHandler
    {
        public int Requests;public bool Fail,Hold;public string? FailHost;public readonly TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Requests++;Started.TrySetResult();if(Hold)await Task.Delay(Timeout.Infinite,token);
            return new(Fail||request.RequestUri!.Host==FailHost?HttpStatusCode.TooManyRequests:HttpStatusCode.OK){Content=new StringContent(request.RequestUri!.Host=="export.arxiv.org"?Atom:Github)};
        }
    }
    sealed class Reader:IResearchReader{public int Reads,Stops;public bool Released,Fail;public string Text="";public void Read(string text){if(Fail)throw new IOException("fixture audio");Reads++;Text=text;}public void Stop()=>Stops++;public void Dispose()=>Released=true;}
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var host=new Form{Opacity=0,ShowInTaskbar=false};
        host.Shown+=async(_,_)=>{
            var c=new Dictionary<string,bool>();
            try{
                var time=new DateTimeOffset(2025,10,23,12,0,0,TimeSpan.Zero);string cache=Path.Combine(Path.GetDirectoryName(report)!,"cache-"+Guid.NewGuid().ToString("N")+".json");
                var handler=new Http();using var http=new HttpClient(handler);using var feed=new ResearchNewsFeed(cache,http,()=>time);
                c["arxiv_metadata_and_canonical_id"]=ResearchNewsFeed.ParseArxiv(Atom) is {Length:2} papers&&papers[0].Id=="arxiv:2510.12345"&&papers[0].Topic=="both"&&papers[0].Authors=="Fixture Author";
                c["irrelevant_voice_results_rejected"]=ResearchNewsFeed.ParseGithub(Github.Replace("voice-agent","plain-framework").Replace("Full-duplex voice agent and real-time speech-to-speech.","A generic framework."),"github-voice").Length==0;
                c["github_metadata"]=ResearchNewsFeed.ParseGithub(Github,"github-voice").Single().Stars==1234;
                c["unsafe_links_rejected"]=new[]{"file:///C:/test.exe","https://github.com.evil.test/a/b","https://user@github.com/a/b","https://github.com/a/b/issues","https://arxiv.org.evil.test/abs/2510.12345"}.All(u=>!ResearchNewsFeed.SafeUrl(u));
                bool xmlRejected=false;try{ResearchNewsFeed.ParseArxiv("<!DOCTYPE feed [<!ENTITY x SYSTEM 'file:///test'>]><feed>&x;</feed>");}catch(System.Xml.XmlException){xmlRejected=true;}c["external_xml_disabled"]=xmlRejected;
                var snapshot=await feed.Load(false,default);c["three_public_requests"]=handler.Requests==3&&snapshot.Items.Length==4&&snapshot.Sources.All(s=>s.State=="ok");
                await feed.Load(false,default);await feed.Load(true,default);c["rapid_refresh_coalesced"]=handler.Requests==3;
                time=time.AddMinutes(2);await feed.Load(false,default);c["fresh_cache_avoids_network"]=handler.Requests==3;
                using(var reload=new ResearchNewsFeed(cache,http,()=>time))c["cache_reopen"]=reload.Snapshot.Items.Length==4;
                snapshot.Items[0]=snapshot.Items[0] with{Title="mutated"};c["snapshot_is_detached"]=feed.Snapshot.Items.All(i=>i.Title!="mutated");
                handler.FailHost="export.arxiv.org";time=time.AddMinutes(12);var partial=await feed.Load(true,default);
                c["single_source_failure_isolated"]=partial.Items.Length==4&&partial.Sources.Single(s=>s.Id=="arxiv").State=="rate-limited"&&partial.Sources.Where(s=>s.Id!="arxiv").All(s=>s.State=="ok");
                var last=feed.Snapshot.Sources.Select(s=>s.Updated).ToArray();handler.Fail=true;time=time.AddMinutes(12);var stale=await feed.Load(true,default);
                c["partial_source_failure_keeps_content_and_age"]=stale.Items.Length==4&&stale.Sources.All(s=>s.State=="rate-limited")&&stale.Sources.Select(s=>s.Updated).SequenceEqual(last);
                var reader=new Reader();using var form=new ResearchNewsForm(feed,true,reader){StartPosition=FormStartPosition.Manual,Location=new(-12000,-12000)};
                form.Show();c["opening_preview_no_requests_or_audio"]=handler.Requests==9&&reader.Reads==0;
                c["cross_source_deduplication"]=form.VisibleItems.Length==3;
                Field<TextBox>(form,"search").Text="turn-taking";c["local_filter"]=form.VisibleItems.Length==1;
                form.Speak(form.VisibleItems);c["explicit_read_only"]=reader.Reads==1&&reader.Text.Contains("Full-duplex");
                Field<TextBox>(form,"search").Clear();form.Render();
                using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,form.ClientRectangle);image.Save(Path.Combine(Path.GetDirectoryName(report)!,"research-news.png"));}
                UiTheme.Configure("382","Leo");await Task.Delay(20);c["theme_binding"]=form.BackColor==BarcaUi.Canvas;
                reader.Fail=true;form.Speak(form.VisibleItems);c["audio_failure_keeps_window"]=!form.IsDisposed&&Field<Label>(form,"message").Text.Contains("本地朗读");
                UiLanguage.Configure("en");form.Render();using(var english=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(english,form.ClientRectangle);english.Save(Path.Combine(Path.GetDirectoryName(report)!,"research-news-en.png"));}UiLanguage.Configure("zh-CN");
                form.Close();c["reader_stopped_and_released"]=reader.Stops>0&&reader.Released;
                c["window_activities_released"]=ModuleActivities.Snapshot().All(s=>s.Module!="research-news");
                var prefs=new Preferences{ResearchNewsModule=true};int opened=0;var module=new ResearchNewsModule(()=>prefs.ResearchNewsModule,t=>{t.ThrowIfCancellationRequested();opened++;return Task.CompletedTask;});
                using var bus=new ActionDispatcher(prefs,(_,_,_)=>throw new Exception("legacy forbidden"),new ExtensionStore(Path.Combine(Path.GetDirectoryName(report)!,"no-extensions")));bus.Registry.Register(module);EntryContributions.Bind(prefs,bus.Registry,bus);
                c["contract_valid"]=ModuleContracts.Snapshot(module).Contract.Enablement=="optional";
                c["entry_registered"]=EntryContributions.ReadModules(prefs).Buttons.Single().Glyph=="news";
                c["unified_entry_execution"]=(await EntryContributions.ExecuteModule(prefs,"research-news/open","test")).Status=="succeeded"&&opened==1;
                c["parameter_injection_rejected"]=(await bus.ExecuteStructuredAsync(new("leo-action/1","research_open",JsonSerializer.SerializeToElement(new{url="https://evil.test"})))).Status=="failed"&&opened==1;
                prefs.ResearchNewsModule=false;bool disabled=false;try{await EntryContributions.ExecuteModule(prefs,"research-news/open","stale");}catch(InvalidOperationException){disabled=true;}c["disable_rejects_stale_entry"]=disabled;
                using(var modules=new ModulesView(new Preferences{ResearchNewsModule=false},true)){var draft=new Preferences();modules.Apply(draft,false);c["settings_switch_preserved"]=!draft.ResearchNewsModule;}
                var pure=UsageProfiles.Prepare(UsageProfiles.Templates()[0],prefs,new ExtensionStore(Path.Combine(Path.GetDirectoryName(report)!,"no-extensions")));c["pure_pet_disables_research"]=!pure.ResearchNewsModule;
                using var waitingHandler=new Http{Hold=true};using var waitingHttp=new HttpClient(waitingHandler);using var waitingFeed=new ResearchNewsFeed(null,waitingHttp,()=>time);using var cancel=new CancellationTokenSource();
                var pending=waitingFeed.Load(true,cancel.Token);await waitingHandler.Started.Task;cancel.Cancel();bool cancelled=false;try{await pending;}catch(OperationCanceledException){cancelled=true;}c["cancel_no_snapshot_commit"]=cancelled&&waitingFeed.Snapshot.Items.Length==0;
                using var closingFeed=new ResearchNewsFeed(null,waitingHttp,()=>time);using var closing=new ResearchNewsForm(closingFeed,true,new Reader());closing.Show();var closingTask=closing.RefreshFeed(true);closing.Close();await closingTask;
                c["closing_cancels_requests_and_activity"]=closingFeed.Snapshot.Items.Length==0&&ModuleActivities.Snapshot().All(s=>s.Module!="research-news");
                File.WriteAllText(report,JsonSerializer.Serialize(new{ok=c.Values.All(x=>x),checks=c,model_calls=0,microphone_used=false},new JsonSerializerOptions{WriteIndented=true}));
            }catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}
            finally{host.Close();}
        };Application.Run(host);
    }
    public static void Live(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        try{using var feed=new ResearchNewsFeed();using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(35));var data=feed.Load(true,cancel.Token).GetAwaiter().GetResult();
            File.WriteAllText(report,JsonSerializer.Serialize(new{ok=data.Items.Length>0,sources=data.Sources,items=data.Items.Length,examples=data.Items.Take(5).Select(i=>new{i.Title,i.Url,i.Source}),model_calls=0},new JsonSerializerOptions{WriteIndented=true}));}
        catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.GetType().Name}));}
    }
}
