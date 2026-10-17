using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace LeoLauncher;

static class AppSearch
{
    static readonly Lazy<Dictionary<string, string>> Pinyin = new(() => {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(Store.Root, "assets", "pinyin.json"))) ?? new(); }
        catch { return new(); }
    });
    static readonly Dictionary<string, string[]> cache = new();
    public static string[] Keys(string name)
    {
        if (cache.TryGetValue(name, out var keys)) return keys;
        var full = new StringBuilder(); var initials = new StringBuilder();
        bool latinStart = true;
        foreach (char c in name)
        {
            if (Pinyin.Value.TryGetValue(c.ToString(), out var syllable)) { full.Append(syllable); initials.Append(syllable[0]); latinStart = true; }
            else if (char.IsLetterOrDigit(c)) { full.Append(char.ToLowerInvariant(c)); if (latinStart) initials.Append(char.ToLowerInvariant(c)); latinStart = false; }
            else latinStart = true;
        }
        return cache[name] = new[] { Catalog.Normalize(name), full.ToString(), initials.ToString() }.Distinct().ToArray();
    }
    public static int Rank(AppEntry app, string query, Dictionary<string, string[]>? aliases = null)
    {
        var q = Catalog.Normalize(query);
        if (q.Length == 0) return 0;
        var custom = aliases?.FirstOrDefault(x => x.Key.Equals(app.Target, StringComparison.OrdinalIgnoreCase)).Value ?? Array.Empty<string>();
        var keys = Keys(app.Name).Concat(custom.Select(Catalog.Normalize));
        if (app.Name.Contains("WeChat", StringComparison.OrdinalIgnoreCase)) keys = keys.Concat(new[] { "wx", "weixin" });
        if (app.Target == ClipboardTool.Target) keys = keys.Concat(new[] { "jtb", "jiantieban", "jqb", "jianqieban", "clipboard" });
        int rank = 99;
        foreach (var key in keys) if (key.Length > 0) rank = Math.Min(rank, key == q ? 0 : key.StartsWith(q) ? 1 : key.Contains(q) ? 2 : 99);
        return rank;
    }
    public static string[] ParseAliases(string text) => Regex.Split(text, @"[\s,，;；]+")
        .Select(Catalog.Normalize).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();
}

static class FavoriteSorting
{
    public static bool Move(List<string> favorites, string source, string target, bool after)
    {
        if (source.Equals(target, StringComparison.OrdinalIgnoreCase)) return false;
        int from = favorites.FindIndex(x => x.Equals(source, StringComparison.OrdinalIgnoreCase));
        if (from < 0 || !favorites.Any(x => x.Equals(target, StringComparison.OrdinalIgnoreCase))) return false;
        string item = favorites[from]; favorites.RemoveAt(from);
        int to = favorites.FindIndex(x => x.Equals(target, StringComparison.OrdinalIgnoreCase));
        favorites.Insert(to + (after ? 1 : 0), item); return true;
    }
}

sealed class AliasEditor : Form
{
    public string[] Aliases => AppSearch.ParseAliases(input.Text);
    readonly TextBox input = new();
    public AliasEditor(AppEntry app, string[] current)
    {
        Text = "Leo · 设置简称"; ClientSize = new Size(430, 160); StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        Font = Appearance.Body(); BackColor = Appearance.Background; ForeColor = Appearance.Ink;
        Controls.Add(new Label { Text = app.Name + " · 多个简称用空格分隔，留空删除", Bounds = new Rectangle(18, 15, 395, 28) });
        input.SetBounds(18, 50, 392, 28); input.Text = string.Join(" ", current); input.BackColor = Appearance.Field; input.ForeColor = Appearance.Ink; Controls.Add(input);
        var save = new Button { Text = "保存", Bounds = new Rectangle(315, 108, 95, 30), DialogResult = DialogResult.OK };
        Controls.Add(save); AcceptButton = save;
        var cancel = new Button { Text = "取消", Bounds = new Rectangle(210, 108, 95, 30), DialogResult = DialogResult.Cancel };
        Controls.Add(cancel); CancelButton = cancel;
    }
}
