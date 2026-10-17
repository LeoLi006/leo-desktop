using System.Text.Json;
namespace LeoLauncher;

static class FeatureTests
{
    public static void Check(Dictionary<string, bool> checks, string output)
    {
        var wx = new AppEntry("微信", "fixture-wx");
        var code = new AppEntry("Visual Studio Code", "fixture-code");
        var dictionary = new Dictionary<string, string[]> { [code.Target] = new[] { "编辑器", "mycode" } };
        var search = new Memory(null);
        checks["pinyin_initials_find_chinese_app"] = search.Search(new[] { wx, code }, "wx", "", DateTimeOffset.Now, true).Single() == wx;
        checks["full_pinyin_finds_chinese_app"] = search.Search(new[] { wx, code }, "weixin", "", DateTimeOffset.Now, true).Single() == wx;
        checks["english_word_initials_find_vsc"] = search.Search(new[] { wx, code }, "vsc", "", DateTimeOffset.Now, true).Single() == code;
        checks["custom_alias_used_by_real_search"] = search.Search(new[] { wx, code }, "mycode", "", DateTimeOffset.Now, true, dictionary).Single() == code;
        dictionary.Remove(code.Target);
        checks["removed_alias_stops_matching"] = search.Search(new[] { code }, "mycode", "", DateTimeOffset.Now, true, dictionary).Count == 0;
        checks["alias_parser_normalizes_and_deduplicates"] = AppSearch.ParseAliases(" WX；wx, weixin  ").SequenceEqual(new[] { "wx", "weixin" });
        var prefs = new Preferences { Aliases = new() { [wx.Target] = new[] { "chat" } } };
        checks["aliases_survive_restart_serialization"] = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(prefs))!.Aliases[wx.Target].Single() == "chat";
        var favorites = Enumerable.Range(0, 14).Select(x => "app" + x).ToList();
        FavoriteSorting.Move(favorites, "app0", "app13", true);
        FavoriteSorting.Move(favorites, "app12", "app1", false);
        checks["favorite_reorder_crosses_both_rows_without_duplicates"] = favorites.First() == "app12" && favorites.Last() == "app0" && favorites.Distinct().Count() == 14;
        checks["favorite_reorder_rejects_nonfavorite"] = !FavoriteSorting.Move(favorites, "unknown", "app1", true);
        var path = Path.Combine(output, "checks", "fixture-pinned-clipboard.json");
        File.WriteAllText(path, "[]"); var clips = new ClipHistory(path);
        var pinned = new ClipItem("text", "permanent template", "template", DateTimeOffset.Now);
        clips.Add(pinned); clips.Pin(pinned, true);
        for (int i = 0; i < 80; i++) clips.Add(new("text", "history" + i, "history" + i, DateTimeOffset.Now));
        clips.Add(pinned);
        checks["clipboard_pin_survives_eviction_and_duplicate_copy"] = clips.Items.Single(x => x.Key == pinned.Key).Pinned && clips.Items.Count(x => !x.Pinned) == 50;
        clips.Clear(); var reloaded = new ClipHistory(path);
        checks["clear_history_keeps_pinned_after_restart"] = reloaded.Items.Single().Key == pinned.Key && reloaded.Items.Single().Pinned;
        reloaded.Pin(reloaded.Items.Single(), false); reloaded.Clear();
        checks["unpin_allows_history_clear"] = new ClipHistory(path).Items.Count == 0;
        var file = Path.Combine(output, "checks", "example file & name.txt"); File.WriteAllText(file, "fixture");
        checks["drop_filters_missing_paths_and_deduplicates"] = FileDropActions.ValidPaths(new[] { file, file, file + ".missing" }).SequenceEqual(new[] { Path.GetFullPath(file) });
        var executable = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        var appStart = FileDropActions.AppStart(executable, new[] { file });
        checks["file_open_uses_one_quoted_argument_without_shell_command"] = appStart.FileName == executable && appStart.ArgumentList.Single() == file && appStart.Arguments == "";
        checks["open_folder_selects_dropped_file"] = FileDropActions.FolderStart(file).ArgumentList.Single() == "/select," + file;
        var screen = new Rectangle(-1920, 0, 1920, 1080);
        checks["fullscreen_detects_negative_monitor"] = FullscreenMonitor.CoversMonitor(screen, screen, "GameWindow", false);
        checks["maximized_workarea_not_fullscreen"] = !FullscreenMonitor.CoversMonitor(new Rectangle(-1920, 0, 1920, 1040), screen, "Browser", false);
        checks["desktop_and_own_windows_not_fullscreen"] = !FullscreenMonitor.CoversMonitor(screen, screen, "WorkerW", false) && !FullscreenMonitor.CoversMonitor(screen, screen, "Leo", true);
        var endpoint = ClashProxy.ReadEndpoint("proxy_host: '127.0.0.1'\nverge_mixed_port: 7897\nignored: value\n");
        checks["clash_endpoint_parsed_without_credentials"] = endpoint == ("127.0.0.1", 7897);
        checks["clash_server_match_requires_exact_endpoint"] = ClashProxy.MatchesServer("http=127.0.0.1:7897;https=127.0.0.1:7897", endpoint.Host, endpoint.Port)
            && !ClashProxy.MatchesServer("127.0.0.1:78970", endpoint.Host, endpoint.Port);
        checks["clash_selector_requires_exact_label"] = ClashProxy.IsProxyLabel("系 统 代 理") && ClashProxy.IsProxyLabel("System Proxy") && !ClashProxy.IsProxyLabel("TUN 模式");
        checks["clash_selector_aligns_hidden_window_and_rejects_wrong_row"] = ClashProxy.IsAligned(new RectangleF(-31452, -30943, 120, 53), new RectangleF(-31055, -30939, 132, 45))
            && !ClashProxy.IsAligned(new RectangleF(100, 100, 120, 30), new RectangleF(300, 200, 60, 30));
    }
}
