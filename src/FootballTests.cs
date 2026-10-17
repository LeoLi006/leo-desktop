using System.Text.Json;
namespace LeoLauncher;
static class FootballTests
{
    public static object Check()
    {
        string Fixture(string state, bool known, object score) => JsonSerializer.Serialize(new { events = new[] { new {
            id = "123", date = DateTimeOffset.UtcNow.AddDays(1).ToString("O"), competitions = new[] { new {
                timeValid = known, altGameNote = "UEFA Champions League", status = new { type = new { state, description = "Scheduled" } },
                competitors = new[] { new { homeAway = "home", score, team = new { id="83", displayName="Barcelona" } }, new { homeAway="away", score, team=new { id="382", displayName="Manchester City" } } }
            } }
        } } });
        var m = FootballFeed.Parse(Fixture("pre", false, "0")).Single();
        var miami = m with { HomeId = "20232", AwayId = "999" };
        var league = TeamData.ParseTable("""{"season":{"displayName":"2026"},"children":[{"name":"East","standings":{"entries":[{"team":{"id":"20232","displayName":"Inter Miami CF"},"stats":[{"name":"rank","displayValue":"2"},{"name":"points","displayValue":"61"}]}]}},{"name":"West","standings":{"entries":[{"team":{"id":"1","displayName":"Other"},"stats":[{"name":"rank","displayValue":"1"},{"name":"points","displayValue":"63"}]}]}}]}""");
        var checks = new Dictionary<string,bool> {
            ["home_away_and_utc_date_parsed"] = m.HomeId == "83" && m.AwayId == "382" && m.Kickoff > DateTimeOffset.UtcNow,
            ["prematch_zero_score_hidden"] = m.Score == "VS",
            ["unknown_kickoff_marked"] = !m.TimeKnown,
            ["followed_clubs_tagged"] = FootballFeed.FollowTags(m, new[] { "巴塞罗那", "曼城" }).SequenceEqual(new[] { "巴塞罗那", "曼城" }),
            ["fixed_list_contains_only_three_clubs"] = FootballForm.FixedFollowing.SequenceEqual(new[]{"巴塞罗那","迈阿密国际","曼城"}),
            ["club_names_localized"] = new[]{"巴塞罗那","曼城","迈阿密国际"}.Select(Club.For).Select(TeamData.Load).SelectMany(d=>d.Rows.Select(r=>r.Name).Concat(d.Matches.SelectMany(m=>new[]{m.Home,m.Away}))).All(n=>System.Text.RegularExpressions.Regex.IsMatch(FootballFeed.Name(n),"[\\u4e00-\\u9fff]")),
            ["unfollow_removes_match_tag"] = FootballFeed.FollowTags(miami, Array.Empty<string>()).Length == 0,
            ["focus_requires_two_preset_strong_teams"] = FootballFeed.IsFocus(m) && !FootballFeed.IsFocus(miami),
            ["past_and_distant_matches_filtered"] = !FootballFeed.IsRecent(m with { Kickoff = DateTimeOffset.UtcNow.AddDays(-5) }, DateTimeOffset.UtcNow) && !FootballFeed.IsRecent(m with { Kickoff = DateTimeOffset.UtcNow.AddDays(30) }, DateTimeOffset.UtcNow),
            ["fixture_score_object_supported"] = FootballFeed.Parse(Fixture("post", true, new { displayValue="2" })).Single().Score == "2 : 2",
            ["empty_schedule_is_valid"] = FootballFeed.Parse("{\"events\":[]}").Count == 0,
            ["three_day_results_kept"] = FootballFeed.IsRecent(m with { Kickoff=DateTimeOffset.UtcNow.AddDays(-3), State="post" },DateTimeOffset.UtcNow),
            ["club_mapping_selects_correct_league"] = Club.For("巴塞罗那").League=="esp.1"&&Club.For("曼城").League=="eng.1"&&Club.For("迈阿密国际").Id=="20232",
            ["standings_preserve_conference_groups"] = league.Rows.Count==2&&league.Rows[0].Group=="East"&&league.Rows[1].Group=="West",
            ["standings_stat_names_and_missing_values"] = league.Rows[0].Rank=="2"&&league.Rows[0].Points=="61"&&league.Rows[0].Played=="—"&&league.Season=="2026",
            ["postponed_not_shown_as_ordinary_prematch"] = FootballFeed.Status(m with { Status="Postponed" }) == "延期",
            ["new_cache_roundtrip"] = JsonSerializer.Deserialize<FootballCache>(JsonSerializer.Serialize(new FootballCache { Matches = new() {m} }))!.Matches.Single() == m
        };
        return new { ok=checks.Values.All(x=>x), checks, external_requests=0 };
    }
}
