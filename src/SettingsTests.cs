using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;

static class SettingsTests
{
    static IEnumerable<Control> All(Control parent) { yield return parent; foreach (Control child in parent.Controls) foreach (var nested in All(child)) yield return nested; }
    public static Dictionary<string, bool> Check(string directory)
    {
        var checks = new Dictionary<string, bool>();
        var defaults = LeoHotkeys.Values(new Preferences());
        checks["old_preferences_keep_default_hotkeys"] = LeoHotkeys.Values(JsonSerializer.Deserialize<Preferences>("{}")!).SequenceEqual(defaults);
        bool rejected = false;
        try { LeoHotkeys.Validate([defaults[0], defaults[0], Keys.None]); } catch (ArgumentException) { rejected = true; }
        checks["duplicate_hotkeys_rejected"] = rejected;
        rejected = false;
        try { LeoHotkeys.Validate([Keys.A, Keys.None, Keys.None]); } catch (ArgumentException) { rejected = true; }
        checks["unmodified_keys_rejected"] = rejected;
        var active = defaults.Select((key, i) => (key, id: i + 1)).ToDictionary(x => x.id, x => x.key);
        var desired = new[] { Keys.Control | Keys.Alt | Keys.F8, Keys.Control | Keys.Alt | Keys.F9, Keys.None };
        rejected = false;
        try { LeoHotkeys.Replace(defaults, desired, id => active.Remove(id), (id, key) => { if (key == desired[1]) return false; active[id] = key; return true; }); } catch (InvalidOperationException) { rejected = true; }
        checks["occupied_hotkey_restores_previous_bindings"] = rejected && Enumerable.Range(1, 3).All(id => active[id] == defaults[id - 1]);
        LeoHotkeys.Replace(defaults, desired, id => active.Remove(id), (id, key) => { active[id] = key; return true; });
        checks["custom_bindings_and_disabled_binding_apply"] = Enumerable.Range(1, 3).All(id => active[id] == desired[id - 1]);
        var prefs = new Preferences(); Preferences? saved = null;
        using var window = new SettingsForm(prefs, (next, _) => saved = next, () => { }, true);
        window.Show(); Application.DoEvents();
        void Capture(string name) { using var image = new Bitmap(window.ClientSize.Width, window.ClientSize.Height); window.DrawToBitmap(image, window.ClientRectangle); image.Save(Path.Combine(directory, name)); }
        Capture("preview-settings-general.png");
        All(window).OfType<TrackBar>().Single().Value = 180;
        All(window).OfType<Button>().Single(b => b.Text == "快捷键").PerformClick(); Application.DoEvents();
        var fields = All(window).OfType<HotkeyField>().ToArray();
        var field = fields.Single(f => f.AccessibleName == "应用搜索快捷键");
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(field, [new KeyEventArgs(desired[0])]);
        checks["shortcut_field_records_combination"] = field.Chord == desired[0];
        checks["restore_button_is_fully_visible"]=All(window).OfType<Button>().Where(b=>b.Text=="恢复默认按键").All(b=>b.Parent!.ClientRectangle.Contains(b.Bounds));
        Capture("preview-settings-hotkeys.png");
        All(window).OfType<Button>().Single(b => b.Text == "保存设置").PerformClick();
        checks["settings_apply_size_and_hotkeys_together"] = saved?.PetSize == 180 && saved.SearchHotkey == desired[0] && prefs.PetSize == 130;
        All(window).OfType<Button>().Single(b => b.Text == "恢复默认按键").PerformClick();
        checks["restore_default_shortcuts"] = fields.Select(f => f.Chord).SequenceEqual(defaults);
        All(window).OfType<Button>().Single(b => b.Text == "API").PerformClick(); Application.DoEvents();
        var editor = All(window).OfType<ApiSettingsForm>().Single();
        checks["api_configuration_embedded_and_key_masked"] = !editor.TopLevel && editor.Visible && All(editor).OfType<TextBox>().Any(t => t.UseSystemPasswordChar);
        Capture("preview-settings-api.png");
        checks["startup_command_quotes_executable"] = LeoStartup.Command(Application.ExecutablePath).StartsWith('"') && LeoStartup.Command(Application.ExecutablePath).EndsWith('"');
        using var pet = new PetForm(new Preferences(), true);
        checks["pet_context_menu_has_settings"] = pet.ContextMenuStrip!.Items.Cast<ToolStripItem>().Any(i => i.Text == "设置");
        return checks;
    }
}
