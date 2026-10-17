using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;

static class SettingsTests
{
    static IEnumerable<Control> All(Control parent) { yield return parent; foreach (Control child in parent.Controls) foreach (var nested in All(child)) yield return nested; }
    public static Dictionary<string, bool> Check(string directory)
    {
        var checks = new Dictionary<string, bool>();
        var area=new Rectangle(-1600,0,1600,900);var layout=new WindowPosition(new Rectangle(1500,650,420,400),new Rectangle(0,0,1920,1080),"removed-monitor",false);
        checks["window_layout_survives_json_roundtrip"]=JsonSerializer.Deserialize<WindowPosition>(JsonSerializer.Serialize(layout))==layout;
        var restored=WindowPositions.Restore(layout,area,new Size(700,500));
        checks["window_position_restores_inside_changed_monitor"]=area.Contains(restored)&&restored.Left<0;
        checks["window_right_bottom_alignment_survives_resolution_change"]=WindowPositions.Restore(new(new Rectangle(1500,680,420,400),new(0,0,1920,1080),"",false),new(0,0,1280,720),new(420,400)).Location==new Point(860,320);
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
        int retries=0;using(var toast=new LeoToast(()=>new Rectangle(800,600,130,140))){toast.Present(new("测试失败",true,()=>{retries++;toast.Present(new("重试成功"));return Task.CompletedTask;}));Application.DoEvents();checks["failure_toast_offers_retry_without_taskbar_window"]=toast.PreviewHasRetry&&!toast.ShowInTaskbar;All(toast).OfType<Button>().Single(b=>b.Text=="重试").PerformClick();Application.DoEvents();checks["feedback_retry_runs_once_and_clears_retry"]=retries==1&&!toast.PreviewHasRetry;}
        var summaryNow=DateTimeOffset.Now;var summaryDay=DateOnly.FromDateTime(summaryNow.LocalDateTime);var summaryTasks=new[]{new DailyTask("a","尚未完成",summaryDay,summaryNow),new DailyTask("b","已完成",summaryDay,summaryNow,Completed:summaryNow),new DailyTask("c","未来事项",summaryDay.AddDays(1),summaryNow)};
        var summary=LeoSummary.Build(summaryTasks,[new("fixture",summaryNow.AddHours(2),"esp.1","83","Barcelona","86","Real Madrid","pre","Scheduled","VS",true)],summaryNow);
        checks["hover_summary_filters_tasks_and_localizes_next_match"]=summary.Contains("尚未完成")&&!summary.Contains("· 已完成")&&!summary.Contains("未来事项")&&summary.Contains("巴塞罗那")&&summary.Contains("缓存");
        using(var card=new HoverCard()){card.Present(summary,new Rectangle(800,600,130,140));Application.DoEvents();checks["hover_summary_is_non_taskbar_and_avoids_pet"]=!card.ShowInTaskbar&&!card.Bounds.IntersectsWith(new Rectangle(800,600,130,140));using var image=new Bitmap(card.Width,card.Height);card.DrawToBitmap(image,card.ClientRectangle);image.Save(Path.Combine(directory,"preview-hover-summary.png"));card.Hide();checks["hover_summary_hides_without_disposal"]=!card.Visible&&!card.IsDisposed;}
        int websiteCalls=0,musicCalls=0;var sceneRun=new SceneRun(("网址",()=>{websiteCalls++;return Task.CompletedTask;}),("音乐",()=>{musicCalls++;if(musicCalls==1)throw new IOException("fixture failure");return Task.CompletedTask;}));
        try{sceneRun.Execute(_=>{}).GetAwaiter().GetResult();}catch(IOException){}
        checks["scene_failure_retains_completed_steps"]=sceneRun.Completed==1;
        sceneRun.Execute(_=>{}).GetAwaiter().GetResult();checks["scene_retry_does_not_repeat_opened_website"]=websiteCalls==1&&musicCalls==2&&sceneRun.Completed==2;
        checks["watch_scene_routes_locally_and_keeps_casual_chat"]=QuickRouter.Route("准备看球").Value=="scene_watch"&&QuickRouter.Route("我准备看球的时候吃什么").Kind==QuickKind.Ai;
        All(window).OfType<Button>().Single(b=>b.Text=="场景").PerformClick();Application.DoEvents();checks["watch_scene_can_be_configured_in_settings"]=All(window).OfType<SceneSettings>().Single().Visible;
        checks["scene_save_and_status_fit_inside_page"]=All(window).OfType<SceneSettings>().Single().Controls.Cast<Control>().All(c=>c.Parent!.ClientRectangle.Contains(c.Bounds));
        Capture("preview-settings-scenes.png");
        return checks;
    }
}
