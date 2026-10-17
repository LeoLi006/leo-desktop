using System.Text.Json;
namespace LeoLauncher;
// Test-only scope: no dependence on personal caches, live ESPN or a particular date.
sealed class FootballTestCache:IDisposable
{
    readonly string path=TeamData.CachePath(Club.For("巴塞罗那"));
    readonly byte[]? saved;
    public FootballTestCache(){saved=File.Exists(path)?File.ReadAllBytes(path):null;var now=DateTimeOffset.Now;var data=new TeamData{MatchesUpdated=now,TableUpdated=now,Season="测试赛季",Matches=[new FootballMatch("9000001",now.AddDays(1),"西甲","83","Barcelona","86","Real Madrid","pre","Scheduled","VS",true)],Rows=[new LeagueRow("西甲","83","Barcelona","1","10","8","1","1","25:8","17","25")]};File.WriteAllText(path,JsonSerializer.Serialize(data));}
    public void Dispose(){if(saved==null)File.Delete(path);else File.WriteAllBytes(path,saved);}
}
