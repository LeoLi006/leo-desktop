using System.Text.Json;
namespace LeoLauncher;

static class MemoryTests
{
    public static void Check(Dictionary<string, bool> checks, string output)
    {
        var folder = Path.Combine(output, "checks"); Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "fixture-history.json");
        File.WriteAllText(file, "[]");
        var a = new AppEntry("Editor", "fixture-editor", ExePath: @"C:\fixture\editor.exe");
        var b = new AppEntry("Browser", "fixture-browser", ExePath: @"C:\fixture\browser.exe");
        var c = new AppEntry("Viewer", "fixture-viewer");
        var apps = new[] { a, b, c };
        var now = DateTimeOffset.UtcNow;
        var memory = new Memory(file);
        checks["first_run_has_no_fabricated_history"] = memory.Recent(apps).Count == 0;
        memory.Record(a, "browser", now.AddMinutes(-5));
        memory.Record(b, "editor", now.AddMinutes(-1));
        checks["recent_row_order"] = memory.Recent(apps).Select(x => x.Name).SequenceEqual(new[] { "Browser", "Editor" });
        var loaded = new Memory(file);
        checks["history_survives_restart"] = loaded.Recent(apps).Select(x => x.Target).SequenceEqual(memory.Recent(apps).Select(x => x.Target));
        loaded.Record(b, "editor", now.AddMinutes(-1).AddSeconds(1));
        checks["focus_poll_does_not_duplicate_visit"] = loaded.Find(b)!.Count == 1;
        checks["uninstalled_entries_not_suggested"] = loaded.Recent(new[] { c }).Count == 0;
        var contextMemory = new Memory(null);
        for (int i = 0; i < 8; i++) contextMemory.Record(a, "design-app", now.AddDays(-2).AddSeconds(i * 5));
        contextMemory.Record(b, "other-app", now.AddMinutes(-1));
        checks["context_changes_empty_query_ranking"] = contextMemory.Search(apps, "", "design-app", now, true).First() == a
            && contextMemory.Search(apps, "", "design-app", now, false).First() == b;
        checks["recent_row_stays_recency_based"] = contextMemory.Recent(apps).First() == b;
        checks["exact_executable_context_match"] = DesktopContext.Match(apps, new ForegroundApp("editor", @"C:\fixture\editor.exe")) == a;
        checks["unknown_foreground_not_falsely_matched"] = DesktopContext.Match(apps, new ForegroundApp("unrelated", @"C:\fixture\other.exe")) == null;
        var packaged = new AppEntry("Packaged app", "shell:AppsFolder\\Fixture!App");
        checks["packaged_app_context_match"] = DesktopContext.Match(new[] { packaged }, new ForegroundApp("host", "", "Fixture!App")) == packaged;
        File.WriteAllText(Path.Combine(folder, "fixture-corrupt.json"), "broken");
        checks["corrupt_history_does_not_crash"] = new Memory(Path.Combine(folder, "fixture-corrupt.json")).Entries.Count == 0;
        checks["clipboard_aliases"] = new[] { "剪切板", "剪贴板", "clipboard", "clip" }.All(ClipboardTool.Matches);
        var toolHistory = new Memory(null);
        toolHistory.Record(ClipboardTool.Entry, "editor", now);
        checks["clipboard_tool_enters_recents_without_storing_content"] = toolHistory.Recent(new[] { ClipboardTool.Entry }).Single().Target == ClipboardTool.Target;
        var dockArea = new Rectangle(0, 0, 1920, 1080);
        checks["favorites_precede_recents_without_duplicates"] = FavoriteOrder.Merge(apps, new[] { a.Target, "uninstalled" }, new[] { b, a }).SequenceEqual(new[] { a, b });
        var savedPrefs = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(new Preferences { Favorites = new() { a.Target } }));
        checks["favorites_survive_settings_roundtrip"] = savedPrefs!.Favorites.Single() == a.Target;
        var clipPath = Path.Combine(folder, "fixture-clipboard.json");
        File.WriteAllText(clipPath, "[]");
        var clips = new ClipHistory(clipPath);
        var clipA = new ClipItem("text", "first content", "first", now);
        var clipB = new ClipItem("text", "second content", "second", now);
        clips.Add(clipA); clips.Add(clipB); clips.Add(clipA);
        checks["clipboard_deduplicates_and_promotes"] = clips.Items.Count == 2 && clips.Items[0].Content == clipA.Content;
        checks["clipboard_searches_content"] = clips.Search("CONTENT").Count == 2;
        checks["clipboard_survives_restart"] = new ClipHistory(clipPath).Items.Select(x => x.Key).SequenceEqual(clips.Items.Select(x => x.Key));
        clips.Delete(clipA);
        checks["clipboard_delete_persists"] = new ClipHistory(clipPath).Items.Single().Key == clipB.Key;
        for (int i = 0; i < 55; i++) clips.Add(new("text", "item" + i, "item" + i, now));
        checks["clipboard_bounded_to_50"] = clips.Items.Count == 50 && clips.Items[0].Content == "item54";
        clips.Clear();
        checks["clipboard_clear_persists"] = new ClipHistory(clipPath).Items.Count == 0;
        checks["popup_avoids_pet_at_edges_and_negative_monitor"] = new[] { dockArea, new Rectangle(-1920, -200, 1920, 1080), new Rectangle(0, 0, 800, 600) }.All(area =>
            new[] { new Point(area.Left, area.Top), new Point(area.Right - 230, area.Top), new Point(area.Left, area.Bottom - 250), new Point(area.Right - 230, area.Bottom - 250), new Point(area.Left + area.Width / 2 - 115, area.Top + area.Height / 2 - 125) }.All(point => {
                var pet = new Rectangle(point, new Size(230, 250));
                var layout = PopupPlacement.Arrange(pet, new Size(520, 220), area);
                pet.Location = layout.Pet;
                var popup = new Rectangle(layout.Popup, new Size(520, 220));
                return area.Contains(popup) && !pet.IntersectsWith(popup);
            }));
    }
}
