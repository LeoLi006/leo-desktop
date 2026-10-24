using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
namespace LeoLauncher;

sealed record ResearchItem(string Id,string Source,string Topic,string Title,string Summary,string Url,DateTimeOffset Date,int? Stars=null,string Authors="",string Publisher="");
sealed record ResearchSource(string Id,DateTimeOffset? Updated,string State);
sealed record ResearchSnapshot(ResearchItem[] Items,ResearchSource[] Sources,string Note="",string Mode="overview",string Field="all");
sealed record ResearchCache(int Format,ResearchSnapshot[] Snapshots);

// Public metadata only. Each mode/field has independent cache, failures and freshness.
sealed class ResearchNewsFeed:IDisposable
{
    readonly Func<string,CancellationToken,Task> throttle;readonly HttpClient http;readonly bool ownsHttp;readonly string? cache;readonly Func<DateTimeOffset> now;
    readonly SemaphoreSlim gate=new(1);readonly object sync=new();
    readonly Dictionary<string,ResearchSnapshot> snapshots=new();readonly Dictionary<string,DateTimeOffset> attempted=new();
    readonly ConcurrentDictionary<string,(string Text,DateTimeOffset Time)> sharedRss=new();
    static readonly SemaphoreSlim arxivRate=new(1),githubRate=new(1);static DateTimeOffset lastArxiv,lastGithub;
    bool disposed;string cacheNote="";
    public ResearchNewsFeed(string? cache=null,HttpClient? http=null,Func<DateTimeOffset>? now=null,Func<string,CancellationToken,Task>? throttle=null)
    {
        this.throttle=throttle??RateLimit;this.cache=cache;this.http=http??new HttpClient{Timeout=TimeSpan.FromSeconds(25)};ownsHttp=http==null;this.now=now??(()=>DateTimeOffset.UtcNow);
        try{if(cache!=null&&File.Exists(cache)&&new FileInfo(cache).Length<=6_000_000){
            using var doc=JsonDocument.Parse(File.ReadAllText(cache));
            if(doc.RootElement.TryGetProperty("Format",out var format)&&format.GetInt32()==2){
                var saved=JsonSerializer.Deserialize<ResearchCache>(doc.RootElement.GetRawText());
                if(saved?.Snapshots==null||saved.Snapshots.Length>8)throw new IOException("Invalid research cache");
                foreach(var s in saved.Snapshots){ValidateSelection(s.Mode,s.Field);if(s.Items==null||s.Sources==null||s.Items.Length>120||s.Sources.Length>4||!s.Items.All(Valid)||s.Sources.Any(x=>x==null||!SourceIds(s.Mode).Contains(x.Id)||x.State is not("ok" or "unavailable" or "rate-limited"))||s.Items.Any(i=>IsNews(i)!=(s.Mode=="news")))throw new IOException("Invalid research snapshot");snapshots[Key(s.Mode,s.Field)]=s with{Note=""};}
            }else cacheNote="旧版动态缓存将按新领域重新获取。";
        }}catch(Exception e)when(FeatureFaults.Recoverable(e)){snapshots.Clear();cacheNote="动态缓存无法读取，可点击刷新重试。";}
    }
    static string Key(string mode,string field)=>mode+":"+field;
    static void ValidateSelection(string mode,string field){if(mode is not("overview" or "news"))throw new ArgumentException("未知动态视图");ResearchAreas.Get(field);}
    public ResearchSnapshot Get(string mode,string field)
    {
        ValidateSelection(mode,field);lock(sync){var s=snapshots.GetValueOrDefault(Key(mode,field))??new([],[],cacheNote,mode,field);return s with{Items=s.Items.ToArray(),Sources=s.Sources.ToArray()};}
    }
    public static bool IsNews(ResearchItem item)=>item.Source.StartsWith("news-",StringComparison.Ordinal);
    public static bool SafeUrl(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Port==443&&uri.UserInfo.Length==0&&
        (uri.Host=="arxiv.org"&&Regex.IsMatch(uri.AbsolutePath,@"^/abs/(?:[0-9]{4}\.[0-9]{4,5}|[a-z-]+/[0-9]{7})(?:v[0-9]+)?$",RegexOptions.CultureInvariant)
        ||uri.Host=="github.com"&&Regex.IsMatch(uri.AbsolutePath,@"^/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/?$",RegexOptions.CultureInvariant)
        ||uri.Host=="news.google.com"&&Regex.IsMatch(uri.AbsolutePath,@"^/(?:rss/articles|articles|read)/[A-Za-z0-9_-]+/?$",RegexOptions.CultureInvariant)
        ||uri.Host=="openai.com"&&(uri.AbsolutePath.StartsWith("/index/")||uri.AbsolutePath.StartsWith("/news/"))
        ||uri.Host=="deepmind.google"&&(uri.AbsolutePath.StartsWith("/blog/")||uri.AbsolutePath.StartsWith("/discover/blog/"))
        ||uri.Host is "www.qbitai.com" or "qbitai.com"&&uri.AbsolutePath!="/");
    static bool Valid(ResearchItem i)=>i!=null&&!string.IsNullOrEmpty(i.Id)&&i.Id.Length<180&&!string.IsNullOrEmpty(i.Title)&&i.Title.Length<=300&&i.Summary!=null&&i.Summary.Length<=700&&i.Authors!=null&&i.Authors.Length<=300&&i.Publisher!=null&&i.Publisher.Length<=120&&AllSources.Contains(i.Source)&&(i.Topic=="general"||ResearchAreas.All.Any(a=>a.Id==i.Topic))&&SafeUrl(i.Url);
    static string Clean(string value,int max,bool html=false)
    {
        string text=WebUtility.HtmlDecode(value);if(html)text=Regex.Replace(text,@"<[^>]*>"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        text=Regex.Replace(text,@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).Trim();return text[..Math.Min(text.Length,max)];
    }
    static string Normalize(string url)=>url.StartsWith("http://",StringComparison.Ordinal)?"https://"+url[7..]:url;
    internal static XDocument Xml(string text){using var reader=XmlReader.Create(new StringReader(text.TrimStart('\uFEFF')),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2_000_000});return XDocument.Load(reader);}
    static string Topic(string text)=>ResearchAreas.All.Skip(1).FirstOrDefault(a=>a.Matches(text))?.Id??"general";
    internal static ResearchItem[] ParseArxiv(string content,ResearchArea area)
    {
        var xml=Xml(content);XNamespace atom="http://www.w3.org/2005/Atom";var items=new List<ResearchItem>();
        if(xml.Root?.Name!=atom+"feed"||xml.Descendants(atom+"entry").Any(e=>((string?)e.Element(atom+"id"))?.Contains("/api/errors",StringComparison.Ordinal)==true))throw new IOException("Invalid arXiv feed");
        foreach(var e in xml.Descendants(atom+"entry").Take(80)){
            string title=Clean((string?)e.Element(atom+"title")??"",300),summary=Clean((string?)e.Element(atom+"summary")??"",700),url=Normalize((string?)e.Element(atom+"id")??"");
            if(!SafeUrl(url)||new Uri(url).Host!="arxiv.org"||!DateTimeOffset.TryParse((string?)e.Element(atom+"published"),out var date))continue;
            string authors=Clean(string.Join(", ",e.Elements(atom+"author").Take(4).Select(a=>(string?)a.Element(atom+"name")??"")),300);
            var item=new ResearchItem("arxiv:"+Regex.Replace(new Uri(url).AbsolutePath[5..],@"v[0-9]+$",""),"arxiv",area.Id=="all"?Topic(title+" "+summary):area.Id,title,summary,url,date,Authors:authors);
            if(Valid(item))items.Add(item);
        }return items.DistinctBy(i=>i.Id).ToArray();
    }
    internal static ResearchItem[] ParseGithub(string content,ResearchArea area)
    {
        using var json=JsonDocument.Parse(content);var found=new List<ResearchItem>();
        foreach(var repo in json.RootElement.GetProperty("items").EnumerateArray().Take(25)){
            string Text(string key)=>repo.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
            string title=Clean(Text("full_name"),300),description=Clean(Text("description"),700),url=Text("html_url");
            string tags=repo.TryGetProperty("topics",out var topics)&&topics.ValueKind==JsonValueKind.Array?string.Join(" ",topics.EnumerateArray().Where(t=>t.ValueKind==JsonValueKind.String).Take(50).Select(t=>t.GetString())):"";
            if(!area.Matches(title+" "+description+" "+tags)||!SafeUrl(url)||new Uri(url).Host!="github.com"||!DateTimeOffset.TryParse(Text("pushed_at"),out var date)||repo.TryGetProperty("archived",out var archived)&&archived.ValueKind==JsonValueKind.True)continue;
            int? stars=repo.TryGetProperty("stargazers_count",out var n)&&n.TryGetInt32(out var value)?Math.Max(0,value):null;
            var item=new ResearchItem("github:"+title.ToLowerInvariant(),"github",area.Id=="all"?Topic(title+" "+description+" "+tags):area.Id,title,description,url,date,stars);if(Valid(item))found.Add(item);
        }return found.ToArray();
    }
    internal static ResearchItem[] ParseNews(string content,string source,ResearchArea area)
    {
        var xml=Xml(content);if(xml.Root?.Name.LocalName!="rss")throw new IOException("Invalid news feed");var found=new List<ResearchItem>();
        foreach(var e in xml.Descendants("item").Take(60)){
            string publisher=Clean((string?)e.Element("source")??SourceName(source),120),title=Clean((string?)e.Element("title")??"",300),summary=Clean((string?)e.Element("description")??"",700,true),url=Normalize(((string?)e.Element("link")??"").Trim());
            string suffix=" - "+publisher;if(title.EndsWith(suffix,StringComparison.OrdinalIgnoreCase))title=title[..^suffix.Length];
            if(publisher.Equals("arXiv",StringComparison.OrdinalIgnoreCase)||publisher.Equals("GitHub",StringComparison.OrdinalIgnoreCase)||!area.Matches(title+" "+summary)||!SafeUrl(url)||new Uri(url).Host is "arxiv.org" or "github.com"||!DateTimeOffset.TryParse((string?)e.Element("pubDate"),out var date))continue;
            var item=new ResearchItem("news:"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))),source,area.Id=="all"?Topic(title+" "+summary):area.Id,title,summary,url,date,Publisher:publisher);if(Valid(item))found.Add(item);
        }return found.DistinctBy(i=>i.Id).Take(30).ToArray();
    }
    internal static string[] SourceIds(string mode)=>mode=="news"?["news-google","news-qbit","news-openai","news-deepmind"]:["arxiv","github"];
    static readonly string[] AllSources=["arxiv","github","news-google","news-qbit","news-openai","news-deepmind"];
    internal static string SourceName(string id)=>id switch{"arxiv"=>"arXiv","github"=>"GitHub","news-google"=>"Google News","news-qbit"=>"量子位","news-openai"=>"OpenAI","news-deepmind"=>"Google DeepMind",_=>id};
    internal static Uri Endpoint(string id,DateTimeOffset time,ResearchArea area)=>id switch{
        "arxiv"=>new("https://export.arxiv.org/api/query?search_query="+Uri.EscapeDataString("("+area.Arxiv+")")+"&sortBy=submittedDate&sortOrder=descending&max_results=60"),
        "github"=>new("https://api.github.com/search/repositories?q="+Uri.EscapeDataString(area.Github+" archived:false stars:>=10 pushed:>="+time.AddDays(-30).ToString("yyyy-MM-dd"))+"&sort=stars&order=desc&per_page=20"),
        "news-google"=>new("https://news.google.com/rss/search?q="+Uri.EscapeDataString("("+area.News+") when:14d")+"&hl=zh-CN&gl=CN&ceid=CN:zh-Hans"),
        "news-qbit"=>new("https://www.qbitai.com/feed"),"news-openai"=>new("https://openai.com/news/rss.xml"),"news-deepmind"=>new("https://deepmind.google/blog/rss.xml"),_=>throw new ArgumentException("Unknown research source")};
    static async Task RateLimit(string id,CancellationToken token)
    {
        if(id is not("arxiv" or "github"))return;var limiter=id=="arxiv"?arxivRate:githubRate;await limiter.WaitAsync(token);
        try{var last=id=="arxiv"?lastArxiv:lastGithub;var wait=TimeSpan.FromSeconds(id=="arxiv"?3:6)-(DateTimeOffset.UtcNow-last);if(wait>TimeSpan.Zero)await Task.Delay(wait,token);if(id=="arxiv")lastArxiv=DateTimeOffset.UtcNow;else lastGithub=DateTimeOffset.UtcNow;}finally{limiter.Release();}
    }
    async Task<(ResearchItem[] Items,ResearchSource Source)> ReadSource(string id,ResearchArea area,ResearchSnapshot previous,bool force,CancellationToken token)
    {
        (ResearchItem[],ResearchSource) Fallback(string state)=>(previous.Items.Where(i=>i.Source==id).ToArray(),new(id,previous.Sources.FirstOrDefault(s=>s.Id==id)?.Updated,state));
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(25));var requestToken=timeout.Token;
        try{
            string text;DateTimeOffset fetched;bool shared=id is "news-qbit" or "news-openai" or "news-deepmind";
            if(shared&&!force&&sharedRss.TryGetValue(id,out var raw)&&now()-raw.Time<TimeSpan.FromMinutes(10)){text=raw.Text;fetched=raw.Time;}
            else{
                await throttle(id,requestToken);using var request=new HttpRequestMessage(HttpMethod.Get,Endpoint(id,now(),area));request.Headers.UserAgent.ParseAdd("LeoDesktop/1.05 (+https://github.com/LeoLi006/leo-desktop)");request.Headers.Accept.ParseAdd(id=="github"?"application/vnd.github+json":"application/rss+xml, application/atom+xml, application/xml, text/xml");
                using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,requestToken);
                if(response.StatusCode==HttpStatusCode.TooManyRequests||response.StatusCode==HttpStatusCode.Forbidden&&response.Headers.TryGetValues("X-RateLimit-Remaining",out var remaining)&&remaining.Contains("0"))return Fallback("rate-limited");response.EnsureSuccessStatusCode();
                await using var stream=await response.Content.ReadAsStreamAsync(requestToken);using var buffer=new MemoryStream();var block=new byte[8192];int read;
                while((read=await stream.ReadAsync(block,requestToken))>0){if(buffer.Length+read>2_000_000)throw new IOException("Response too large");buffer.Write(block,0,read);}text=Encoding.UTF8.GetString(buffer.ToArray());fetched=now();
            }
            var items=id=="arxiv"?ParseArxiv(text,area):id=="github"?ParseGithub(text,area):ParseNews(text,id,area);
            if(shared)sharedRss[id]=(text,fetched);return (items,new(id,fetched,"ok"));
        }catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){return Fallback("unavailable");}
    }
    public async Task<ResearchSnapshot> Load(string mode,string field,bool force,CancellationToken token)
    {
        ValidateSelection(mode,field);await gate.WaitAsync(token);
        try{
            ObjectDisposedException.ThrowIf(disposed,this);token.ThrowIfCancellationRequested();string key=Key(mode,field);var previous=Get(mode,field);var time=now();
            if(attempted.TryGetValue(key,out var last)&&time-last<TimeSpan.FromMinutes(1))return previous with{Note="一分钟内无需重复刷新，当前显示上次结果。"};
            if(!force&&previous.Sources.Length==SourceIds(mode).Length&&previous.Sources.All(s=>s.State=="ok"&&s.Updated is{} updated&&time-updated<TimeSpan.FromMinutes(10)))return previous;
            attempted[key]=time;var results=await Task.WhenAll(SourceIds(mode).Select(id=>ReadSource(id,ResearchAreas.Get(field),previous,force,token)));token.ThrowIfCancellationRequested();
            var next=new ResearchSnapshot(results.SelectMany(r=>r.Items).Where(Valid).OrderByDescending(i=>i.Date).Take(120).ToArray(),results.Select(r=>r.Source).ToArray(),"",mode,field);
            lock(sync){snapshots.Remove(key);snapshots[key]=next;while(snapshots.Count>8)snapshots.Remove(snapshots.Keys.First());}
            if(cache!=null)try{ResearchSnapshot[] all;lock(sync)all=snapshots.Values.ToArray();ConfigMigration.Write(cache,JsonSerializer.Serialize(new ResearchCache(2,all)));}catch(Exception e)when(FeatureFaults.Recoverable(e)){next=next with{Note="缓存保存失败，本次结果仍可查看。"};lock(sync)snapshots[key]=next;}
            return next with{Items=next.Items.ToArray(),Sources=next.Sources.ToArray()};
        }catch(OperationCanceledException)when(token.IsCancellationRequested){attempted.Remove(Key(mode,field));throw;}
        finally{gate.Release();}
    }
    public void Dispose(){disposed=true;if(ownsHttp)http.Dispose();}
}
