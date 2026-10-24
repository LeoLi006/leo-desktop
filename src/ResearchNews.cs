using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
namespace LeoLauncher;

sealed record ResearchItem(string Id,string Source,string Topic,string Title,string Summary,string Url,DateTimeOffset Date,int? Stars=null,string Authors="");
sealed record ResearchSource(string Id,DateTimeOffset? Updated,string State);
sealed record ResearchSnapshot(ResearchItem[] Items,ResearchSource[] Sources,string Note="");

// Public metadata only. No credentials, model calls, full papers, downloads or background polling.
sealed class ResearchNewsFeed:IDisposable
{
    readonly HttpClient http;readonly bool ownsHttp;readonly string? cache;readonly Func<DateTimeOffset> now;
    readonly SemaphoreSlim gate=new(1);DateTimeOffset? attempted;bool disposed;
    ResearchSnapshot snapshot=new([],[]);
    public ResearchNewsFeed(string? cache=null,HttpClient? http=null,Func<DateTimeOffset>? now=null)
    {
        this.cache=cache;this.http=http??new HttpClient{Timeout=TimeSpan.FromSeconds(25)};ownsHttp=http==null;this.now=now??(()=>DateTimeOffset.UtcNow);
        try{if(cache!=null&&File.Exists(cache)&&new FileInfo(cache).Length<=2_000_000){var data=JsonSerializer.Deserialize<ResearchSnapshot>(File.ReadAllText(cache));if(data!=null&&data.Items!=null&&data.Sources!=null&&data.Items.Length<=120&&data.Sources.Length<=3&&data.Items.All(Valid)&&data.Sources.All(s=>s!=null&&SourceIds.Contains(s.Id)&&s.State is "ok" or "unavailable" or "rate-limited"))snapshot=data with{Note=""};}}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){snapshot=new([],[],"动态缓存无法读取，可点击刷新重试。");}
    }
    public ResearchSnapshot Snapshot=>snapshot with{Items=snapshot.Items.ToArray(),Sources=snapshot.Sources.ToArray()};
    public static bool SafeUrl(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Port==443&&uri.UserInfo.Length==0&&
        (uri.Host=="arxiv.org"&&Regex.IsMatch(uri.AbsolutePath,@"^/abs/(?:[0-9]{4}\.[0-9]{4,5}|[a-z-]+/[0-9]{7})(?:v[0-9]+)?$",RegexOptions.CultureInvariant)||uri.Host=="github.com"&&Regex.IsMatch(uri.AbsolutePath,@"^/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/?$",RegexOptions.CultureInvariant));
    static bool Valid(ResearchItem i)=>i!=null&&i.Id.Length is >0 and <180&&i.Title.Length is >0 and <=300&&i.Summary.Length<=700&&i.Authors.Length<=300&&i.Source is "arxiv" or "github-agent" or "github-voice"&&i.Topic is "agent" or "voice" or "both"&&SafeUrl(i.Url);
    static string Clean(string value,int max)
    {
        string text=Regex.Replace(WebUtility.HtmlDecode(value),@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).Trim();
        return text[..Math.Min(text.Length,max)];
    }
    public static string? Topic(string text)
    {
        bool agent=Regex.IsMatch(text,@"\bagents?\b|agentic|tool.use|multi.agent",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        bool voice=Regex.IsMatch(text,@"full[ -]duplex|speech.to.speech|spoken (dialog|language)|real.time (voice|speech|audio|dialog)|turn.taking|voice agent|speech language model|simultaneous speech",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        return agent&&voice?"both":voice?"voice":agent?"agent":null;
    }
    internal static ResearchItem[] ParseArxiv(string content)
    {
        using var reader=XmlReader.Create(new StringReader(content),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2_000_000});
        var xml=XDocument.Load(reader);XNamespace atom="http://www.w3.org/2005/Atom";var items=new List<ResearchItem>();
        if(xml.Root?.Name!=atom+"feed"||xml.Descendants(atom+"entry").Any(e=>((string?)e.Element(atom+"id"))?.Contains("/api/errors",StringComparison.Ordinal)==true))throw new IOException("Invalid arXiv feed");
        foreach(var entry in xml.Descendants(atom+"entry").Take(80)){
            string title=Clean((string?)entry.Element(atom+"title")??"",300),summary=Clean((string?)entry.Element(atom+"summary")??"",700);
            string url=((string?)entry.Element(atom+"id")??"").Replace("http://arxiv.org/","https://arxiv.org/",StringComparison.Ordinal);
            var topic=Topic(title+" "+summary);if(topic==null||!SafeUrl(url)||!DateTimeOffset.TryParse((string?)entry.Element(atom+"published"),out var date))continue;
            string authors=Clean(string.Join(", ",entry.Elements(atom+"author").Take(4).Select(a=>(string?)a.Element(atom+"name")??"")),300);
            var item=new ResearchItem("arxiv:"+Regex.Replace(new Uri(url).AbsolutePath[5..],@"v[0-9]+$",""),"arxiv",topic,title,summary,url,date,Authors:authors);
            if(Valid(item))items.Add(item);
        }
        return items.DistinctBy(i=>i.Id).ToArray();
    }
    internal static ResearchItem[] ParseGithub(string content,string source)
    {
        using var json=JsonDocument.Parse(content);var found=new List<ResearchItem>();
        foreach(var repo in json.RootElement.GetProperty("items").EnumerateArray().Take(25)){
            string Text(string key)=>repo.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
            string title=Clean(Text("full_name"),300),description=Clean(Text("description"),700),url=Text("html_url");
            string tags=repo.TryGetProperty("topics",out var topics)&&topics.ValueKind==JsonValueKind.Array?string.Join(" ",topics.EnumerateArray().Where(t=>t.ValueKind==JsonValueKind.String).Select(t=>t.GetString())):"";
            string? topic=Topic(title+" "+description+" "+tags);
            if(source=="github-voice"&&topic is not("voice" or "both")||source=="github-agent"&&topic is not("agent" or "both"))continue;
            if(!SafeUrl(url)||!DateTimeOffset.TryParse(Text("pushed_at"),out var date)||repo.TryGetProperty("archived",out var archived)&&archived.ValueKind==JsonValueKind.True)continue;
            int? stars=repo.TryGetProperty("stargazers_count",out var count)&&count.TryGetInt32(out var n)?Math.Max(0,n):null;
            var item=new ResearchItem("github:"+title.ToLowerInvariant(),source,topic!,title,description,url,date,stars);
            if(Valid(item))found.Add(item);
        }
        return found.ToArray();
    }
    public static string[] SourceIds=>["arxiv","github-agent","github-voice"];
    internal static Uri Endpoint(string id,DateTimeOffset time)
    {
        if(id=="arxiv"){
            string query="(cat:cs.AI OR cat:cs.CL OR cat:cs.LG OR cat:cs.SD) AND ((ti:agent AND all:\"language model\") OR all:\"full-duplex\" OR all:\"full duplex\" OR all:\"speech-to-speech\" OR all:\"spoken dialogue\" OR all:\"real-time voice\")";
            return new("https://export.arxiv.org/api/query?search_query="+Uri.EscapeDataString(query)+"&sortBy=submittedDate&sortOrder=descending&max_results=60");
        }
        string words=id=="github-agent"?"agent llm":"\"full-duplex\" OR \"speech-to-speech\" OR \"voice-agent\"";
        string q=words+" in:name,description,topics archived:false stars:>=10 pushed:>="+time.AddDays(-30).ToString("yyyy-MM-dd");
        return new("https://api.github.com/search/repositories?q="+Uri.EscapeDataString(q)+"&sort=stars&order=desc&per_page=16");
    }
    async Task<(ResearchItem[] Items,ResearchSource Source)> ReadSource(string id,CancellationToken token)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(25));var requestToken=timeout.Token;
        try{
            using var request=new HttpRequestMessage(HttpMethod.Get,Endpoint(id,now()));request.Headers.UserAgent.ParseAdd("LeoDesktop/1.05 (+https://github.com/LeoLi006/leo-desktop)");
            request.Headers.Accept.ParseAdd(id=="arxiv"?"application/atom+xml":"application/vnd.github+json");
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,requestToken);
            if(response.StatusCode==HttpStatusCode.TooManyRequests||response.StatusCode==HttpStatusCode.Forbidden&&response.Headers.TryGetValues("X-RateLimit-Remaining",out var remaining)&&remaining.Contains("0"))return Fallback(id,"rate-limited");
            response.EnsureSuccessStatusCode();
            await using var stream=await response.Content.ReadAsStreamAsync(requestToken);using var buffer=new MemoryStream();var block=new byte[8192];int read;
            while((read=await stream.ReadAsync(block,requestToken))>0){if(buffer.Length+read>2_000_000)throw new IOException("Response too large");buffer.Write(block,0,read);}
            string text=Encoding.UTF8.GetString(buffer.ToArray());
            var items=id=="arxiv"?ParseArxiv(text):ParseGithub(text,id);
            return (items,new(id,now(),"ok"));
        }catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
        catch(Exception e)when(FeatureFaults.Recoverable(e)){return Fallback(id,"unavailable");}
    }
    (ResearchItem[],ResearchSource) Fallback(string id,string state)=>(snapshot.Items.Where(i=>i.Source==id).ToArray(),new(id,snapshot.Sources.FirstOrDefault(s=>s.Id==id)?.Updated,state));
    public async Task<ResearchSnapshot> Load(bool force,CancellationToken token)
    {
        await gate.WaitAsync(token);
        try{
            ObjectDisposedException.ThrowIf(disposed,this);token.ThrowIfCancellationRequested();var time=now();
            if(attempted is{} last&&time-last<TimeSpan.FromMinutes(1))return Snapshot with{Note="一分钟内无需重复刷新，当前显示上次结果。"};
            if(!force&&snapshot.Sources.Length==3&&snapshot.Sources.All(s=>s.State=="ok"&&s.Updated is{} updated&&time-updated<TimeSpan.FromMinutes(10)))return Snapshot;
            attempted=time;var results=await Task.WhenAll(SourceIds.Select(id=>ReadSource(id,token)));token.ThrowIfCancellationRequested();
            snapshot=new(results.SelectMany(r=>r.Items).Where(Valid).OrderByDescending(i=>i.Date).Take(120).ToArray(),results.Select(r=>r.Source).ToArray());
            if(cache!=null)try{ConfigMigration.Write(cache,JsonSerializer.Serialize(snapshot));}catch(Exception e)when(FeatureFaults.Recoverable(e)){snapshot=snapshot with{Note="缓存保存失败，本次结果仍可查看。"};}
            return Snapshot;
        }finally{gate.Release();}
    }
    public void Dispose(){disposed=true;if(ownsHttp)http.Dispose();/* Waiters own their tokens; do not dispose an in-use semaphore. */}
}
