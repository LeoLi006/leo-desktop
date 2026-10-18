using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace LeoLauncher;
static class MatchSyncService
{
    static readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(14)};
    public static FootballMatch ParseSummary(string json,string expected){using var doc=JsonDocument.Parse(json);var h=doc.RootElement.GetProperty("header");if(h.GetProperty("id").GetString()!=expected)throw new FormatException("比赛编号不符");var c=h.GetProperty("competitions")[0];string date=c.TryGetProperty("date",out var d)&&d.ValueKind==JsonValueKind.String?d.GetString()??"":"";bool known=DateTimeOffset.TryParse(date,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _);var competition=JsonNode.Parse(c.GetRawText())!;if(!known)competition["timeValid"]=false;var ev=new JsonObject{["id"]=expected,["date"]=known?date:DateTimeOffset.UnixEpoch.ToString("O"),["competitions"]=new JsonArray(competition)};return FootballFeed.Parse(new JsonObject{["events"]=new JsonArray(ev)}.ToJsonString()).Single();}

    public static async Task<FootballMatch[]> Fetch(TodoStore store,CancellationToken token){var ids=store.Items.Where(t=>t.MatchId?.StartsWith("espn:")==true&&t.MatchFollowSchedule&&t.Completed==null&&t.MatchStatus is not("finished" or "cancelled")).OrderBy(t=>t.MatchKickoff??t.Reminder??t.Created).Select(t=>t.MatchId![5..]).Where(id=>id.Length>0&&id.All(char.IsAsciiDigit)).Distinct().Take(24).ToArray();using var request=Modules.FootballRequest(token);token=request.Token;using var gate=new SemaphoreSlim(3);var jobs=ids.Select(async id=>{await gate.WaitAsync(token);try{string json=await http.GetStringAsync(FootballFeed.Base+"all/summary?event="+id,token);return ParseSummary(json,id);}catch(Exception e)when(e is HttpRequestException or TaskCanceledException or FormatException or JsonException or KeyNotFoundException or InvalidOperationException){return null;}finally{gate.Release();}});return (await Task.WhenAll(jobs)).Where(m=>m!=null).Cast<FootballMatch>().ToArray();}
}
sealed partial class PetForm
{
    ReminderForm? reminders;readonly System.Windows.Forms.Timer matchSyncTimer=new(){Interval=600000};readonly CancellationTokenSource matchSyncLife=new();bool matchSyncBusy;
    async Task SyncMatchesAsync(){if(previewMode||!prefs.FootballModule||matchSyncBusy||IsDisposed)return;matchSyncBusy=true;try{var cached=FootballFeed.LoadCache();todoStore.HydrateMatchMetadata(cached.Matches,cached.Updated);var matches=await MatchSyncService.Fetch(todoStore,matchSyncLife.Token);if(!IsDisposed&&prefs.FootballModule&&!matchSyncLife.IsCancellationRequested)todoStore.SyncMatches(matches,DateTimeOffset.Now);}catch(OperationCanceledException){}catch{/* Keep saved reminders when a source or local save is unavailable. */}finally{matchSyncBusy=false;}}
    void OpenReminderTask(DailyTask task){ShowTodos();todoWindow!.View.ShowDay(task.Day);}
}
