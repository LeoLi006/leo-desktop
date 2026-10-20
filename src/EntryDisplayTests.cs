using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;
static class EntryDisplayTests
{
    static IEnumerable<Control> Walk(Control c){yield return c;foreach(Control child in c.Controls)foreach(var v in Walk(child))yield return v;}
    static void Show(Form f){f.StartPosition=FormStartPosition.Manual;f.Location=new(-19000,-19000);f.Show();Application.DoEvents();UiTheme.Attach(f);}
    static void Capture(Form f,string file){using var image=new Bitmap(f.Width,f.Height);f.DrawToBitmap(image,new(Point.Empty,f.Size));image.Save(file);}
    static T Field<T>(object instance,string name)=>(T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(instance)!;
    static void OpenMenu(ContextMenuStrip menu)=>typeof(ToolStripDropDown).GetMethod("OnOpening",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(menu,[new System.ComponentModel.CancelEventArgs()]);
    static ToolStripMenuItem[] Entries(PetForm pet){OpenMenu(pet.ContextMenuStrip!);return pet.ContextMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(i=>i.Text=="应用与媒体").DropDownItems.OfType<ToolStripMenuItem>().ToArray();}
    static bool Throws(Action action){try{action();return false;}catch(ArgumentException){return true;}}
    public static void Run(string report){using var host=new Form{Opacity=0,ShowInTaskbar=false};host.Shown+=async(_,_)=>{try{var checks=await Check(Path.GetDirectoryName(report)!);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,external_actions=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}finally{host.Close();}};Application.Run(host);}
    static async Task<Dictionary<string,bool>> Check(string root)
    {
        Directory.CreateDirectory(root);var c=new Dictionary<string,bool>();var prefs=new Preferences();var app=new LocalApplication(Guid.NewGuid().ToString("N"),"示例应用",Environment.ProcessPath!);prefs.ApplicationModules.Add(app);
        var descriptor=EntryContributions.Read(prefs).Single();
        c["contribution_owns_action"]=descriptor.Id==app.Id&&descriptor.ModuleId=="local:"+app.Id&&descriptor.ActionId==descriptor.ModuleId;
        c["contribution_four_host_projections"]=descriptor.Shortcut&&descriptor.Menu&&descriptor.SearchEntry.Name==app.Name&&descriptor.Editor=="local-application";
        c["foreign_action_rejected"]=Throws(()=>EntryContributions.Validate(descriptor with{ActionId="proxy_on"},app));
        c["foreign_owner_rejected"]=Throws(()=>EntryContributions.Validate(descriptor with{ModuleId="local:other"},app));
        c["arbitrary_editor_rejected"]=Throws(()=>EntryContributions.Validate(descriptor with{Editor="execute"},app));
        var draft=LocalApplications.Clone(prefs);draft.ApplicationModules.Add(app);
        c["duplicate_entry_rejected"]=Throws(()=>EntryContributions.Read(draft));draft.ApplicationModules.RemoveAt(1);
        draft.Aliases[app.Path]=["demo"];draft.ApplicationModules[0]=app with{Name="更名",Arguments="--test",WorkingDirectory="C:\\",Administrator=true};
        var changed=EntryContributions.Read(draft).Single();
        c["rename_search_and_settings_sync"]=changed.Name=="更名"&&changed.SearchEntry.Name=="更名"&&EntryContributions.Configuration(draft,changed.Id)!.Name=="更名";
        c["icon_alias_preserved"]=changed.Icon==app.Path&&changed.Aliases.SequenceEqual(new[]{"demo"});
        c["launch_options_untouched"]=EntryContributions.Configuration(draft,app.Id) is {Arguments:"--test",Administrator:true};
        draft.EntryDisplayModes[app.Id]=EntryDisplay.Search;changed=EntryContributions.Read(draft).Single();
        c["hidden_entry_remains_callable"]=!changed.Shortcut&&!changed.Menu&&changed.Enabled&&EntryContributions.CurrentAction(draft,app.Id)==descriptor.ActionId;
        draft.ApplicationModules[0]=draft.ApplicationModules[0] with{Enabled=false};
        bool Rejected(){try{EntryContributions.CurrentAction(draft,app.Id);return false;}catch(InvalidOperationException){return true;}}
        c["stale_disabled_entry_rejected"]=Rejected();draft.ApplicationModules.Clear();c["stale_removed_entry_rejected"]=Rejected();
        var builtinPrefs=new Preferences();
        c["builtin_six_entries"]=EntryContributions.ReadBuiltins(builtinPrefs).Select(e=>e.Id).SequenceEqual(new[]{"football","music","media","terminal","clash","edge"});
        foreach(var id in new[]{"football","music","media","terminal","clash","edge"}){
            var entry=EntryContributions.Builtin(builtinPrefs,id);
            c[id+"_default_visible"]=entry.Shortcut&&entry.Menu;
            c[id+"_foreign_action_rejected"]=Throws(()=>EntryContributions.ValidateBuiltin(entry with{ActionId="proxy_on"}));
            c[id+"_foreign_editor_rejected"]=Throws(()=>EntryContributions.ValidateBuiltin(entry with{Editor="execute"}));
            builtinPrefs.EntryDisplayModes[id]=EntryDisplay.Search;
            entry=EntryContributions.Builtin(builtinPrefs,id);
            c[id+"_hidden_still_callable"]=!entry.Shortcut&&!entry.Menu&&EntryContributions.BuiltinAction(builtinPrefs,id)==entry.ActionId;
            builtinPrefs.ModuleIcons[id]="icon-reference";
            builtinPrefs.ModuleExecutables[id]=Path.Combine(root,"absent-client.exe");
            entry=EntryContributions.Builtin(builtinPrefs,id);
            c[id+"_passive_missing_keeps_window"]=entry.Icon=="icon-reference"&&entry.Dependency=="missing"&&entry.Enabled;
        }
        builtinPrefs.FootballModule=builtinPrefs.MusicModule=builtinPrefs.MediaModule=builtinPrefs.TerminalModule=builtinPrefs.ClashModule=builtinPrefs.EdgeModule=false;
        foreach(var id in new[]{"football","music","media","terminal","clash","edge"}){
            var entry=EntryContributions.Builtin(builtinPrefs,id);
            bool rejected=false;try{EntryContributions.BuiltinAction(builtinPrefs,id);}catch(InvalidOperationException){rejected=true;}
            c[id+"_stale_disabled_blocked"]=!entry.Enabled&&!entry.Menu&&!entry.Shortcut&&rejected;
            c[id+"_disabled_setting_retains_mode"]=EntryDisplay.Entries(builtinPrefs).Single(e=>e.Id==id).Enabled==false&&entry.Mode==EntryDisplay.Search;
        }
        var edgeDescriptor=EntryContributions.Builtin(new Preferences(),"edge");
        c["edge_menu_keeps_web_expand"]=edgeDescriptor.MenuActions.Single() is {ActionId:"launcher_open",Value:"web"};
        var clashDescriptor=EntryContributions.Builtin(new Preferences(),"clash");
        c["proxy_explicit_menu_preserved"]=clashDescriptor.MenuActions.Select(a=>a.ActionId).SequenceEqual(new[]{"proxy_on","proxy_off","proxy_status"});
        c["foreign_submenu_rejected"]=Throws(()=>EntryContributions.ValidateBuiltin(clashDescriptor with{MenuActions=[new("bad","terminal_open")]}));
        c["old_config_defaults_to_shortcuts"]=EntryDisplay.Entries(prefs).All(e=>EntryDisplay.Mode(prefs,e.Id)==EntryDisplay.Shortcut);c["custom_application_in_scope"]=EntryDisplay.Entries(prefs).Any(e=>e.Id==app.Id);c["invalid_mode_rejected"]=Throws(()=>EntryDisplay.Validate(new(){["music"]="invalid"}));c["invalid_key_rejected"]=Throws(()=>EntryDisplay.Validate(new(){["../app"]="menu"}));
        using(var form=new EntryDisplayForm(prefs)){Show(form);var combo=Walk(form).OfType<ComboBox>().Single(c=>c.AccessibleName=="music入口显示");combo.SelectedItem=combo.Items.Cast<ComboChoice>().Single(c=>c.Value==EntryDisplay.Menu);c["editor_does_not_apply_draft"]=prefs.EntryDisplayModes.Count==0;Walk(form).OfType<Button>().Single(b=>b.Text=="取消").PerformClick();c["cancel_keeps_settings"]=prefs.EntryDisplayModes.Count==0;}
        using(var form=new EntryDisplayForm(prefs)){Show(form);foreach(var combo in Walk(form).OfType<ComboBox>())combo.SelectedItem=combo.Items.Cast<ComboChoice>().Single(c=>c.Value==EntryDisplay.Menu);Capture(form,Path.Combine(root,"entry-display.png"));Walk(form).OfType<Button>().Single(b=>b.Text=="确定").PerformClick();c["editor_returns_all_modes"]=form.Result.Count==7&&form.Result.Values.All(v=>v==EntryDisplay.Menu);c["editor_does_not_change_enable_flags"]=EntryDisplay.Entries(prefs).All(e=>e.Enabled);prefs.EntryDisplayModes=new(form.Result);}
        c["mode_roundtrip"]=LocalApplications.Clone(prefs).EntryDisplayModes.SequenceEqual(prefs.EntryDisplayModes);
        using(var form=new Form()){using var view=new ModulesView(prefs,true);view.Dock=DockStyle.Fill;form.ClientSize=new(590,560);form.Controls.Add(view);Show(form);c["settings_has_entry_button"]=Walk(view).OfType<Button>().Any(b=>b.Text=="入口显示…");var next=new Preferences();view.Apply(next,false);c["module_save_preserves_modes"]=next.EntryDisplayModes.SequenceEqual(prefs.EntryDisplayModes);c["module_save_preserves_enable_flags"]=EntryDisplay.Entries(next).All(e=>e.Enabled);}
        using(var tools=new FootTools(130,true)){Show(tools);tools.ConfigureModules(prefs);tools.PreviewUtilities();c["menu_mode_hides_foot_shortcuts"]=tools.Controls.OfType<GlyphButton>().All(b=>!b.Visible||b.AccessibleName is "问 Leo" or "每日清单");c["empty_utility_arrow_hidden"]=!tools.Controls.OfType<Button>().Single(b=>b.AccessibleName=="展开更多工具").Visible&&tools.Width==64;c["hide_keeps_clash_polling"]=Field<System.Windows.Forms.Timer>(tools,"proxyTimer").Enabled;c["hide_keeps_modules_enabled"]=prefs.TerminalModule&&prefs.ClashModule&&prefs.EdgeModule;
            prefs.EntryDisplayModes["edge"]=EntryDisplay.Shortcut;tools.ConfigureModules(prefs);tools.PreviewTyping("test");c["edge_shortcut_can_expand"]=tools.IsSearchOpen;prefs.EntryDisplayModes["edge"]=EntryDisplay.Search;tools.ConfigureModules(prefs);c["hiding_edge_collapses_existing_input"]=!tools.IsSearchOpen&&tools.Width==64;var disabled=LocalApplications.Clone(prefs);disabled.ClashModule=false;tools.ConfigureModules(disabled);c["disable_stops_clash_polling"]=!Field<System.Windows.Forms.Timer>(tools,"proxyTimer").Enabled;}
        using(var tools=new FootTools(130,true)){
            var settings=new Preferences();Show(tools);tools.ConfigureModules(settings);tools.PreviewUtilities();
            var terminalButton=Walk(tools).OfType<Button>().Single(b=>b.AccessibleName=="打开管理员终端");
            var proxyButton=Walk(tools).OfType<Button>().Single(b=>b.AccessibleName=="Clash Verge 系统代理");
            var edgeButton=Walk(tools).OfType<Button>().Single(b=>b.AccessibleName=="展开 Edge 搜索");
            c["utility_three_buttons_present"]=terminalButton.Visible&&proxyButton.Visible&&edgeButton.Visible;
            using(var icon=new Bitmap(8,8)){using var g=Graphics.FromImage(icon);g.Clear(Color.Red);icon.Save(Path.Combine(root,"utility-icon.png"));}
            foreach(var id in new[]{"terminal","clash","edge"})settings.ModuleIcons[id]=Path.Combine(root,"utility-icon.png");
            tools.ConfigureModules(settings);c["utility_icons_sync"]=new[]{terminalButton,proxyButton,edgeButton}.All(b=>b.Image!=null);
            tools.PreviewTyping("hello");settings.EntryDisplayModes["edge"]=EntryDisplay.Menu;tools.ConfigureModules(settings);
            c["utility_hide_closes_search"]=!tools.IsSearchOpen&&!edgeButton.Visible&&settings.EdgeModule;
            settings.ClashModule=false;settings.TerminalModule=false;tools.ConfigureModules(settings);
            c["utility_disable_hides_and_stops_poll"]=!terminalButton.Visible&&!proxyButton.Visible&&!Field<System.Windows.Forms.Timer>(tools,"proxyTimer").Enabled;
            settings.ClashModule=true;settings.TerminalModule=true;settings.EntryDisplayModes["edge"]=EntryDisplay.Shortcut;tools.ConfigureModules(settings);tools.PreviewUtilities();
            c["utility_reenable_restores"]=terminalButton.Visible&&proxyButton.Visible&&edgeButton.Visible;
            foreach(var id in new[]{"terminal","clash","edge"})settings.ModuleIcons.Remove(id);
            tools.ConfigureModules(settings);c["utility_removed_icons_clear"]=new[]{terminalButton,proxyButton,edgeButton}.All(b=>b.Image==null);
        }
        // The real settings apply path updates live controls and retains the module's enable state.
        var live=new Preferences{ApplicationModules=[app],ShortcutOrder=["media",app.Id,"music","football"]};using(var pet=new PetForm(live,true,true)){
            var launcher=Field<LauncherForm>(pet,"launcher");Show(launcher);launcher.Open();c["default_top_buttons_present"]=Field<Dictionary<string,(string Title,Action Run)>>(launcher,"shortcutActions").Keys.Order().SequenceEqual(new[]{"football","music","media",app.Id}.Order());var originalMusic=launcher.MusicWithoutOpening();
            var menuPrefs=LocalApplications.Clone(live);foreach(var e in EntryDisplay.Entries(menuPrefs))menuPrefs.EntryDisplayModes[e.Id]=EntryDisplay.Menu;pet.ApplyPlatformSettings(menuPrefs);var menu=Entries(pet);c["menu_only_removes_top_buttons"]=Field<Dictionary<string,(string Title,Action Run)>>(launcher,"shortcutActions").Count==0;c["empty_top_overflow_hidden"]=!Walk(launcher).OfType<Button>().Single(b=>b.AccessibleName=="更多快捷功能").Visible;c["music_menu_retained"]=menu.Single(i=>i.Text=="音乐").Available;c["media_menu_retained"]=menu.Single(i=>i.Text=="媒体控制…").Available;c["football_menu_added"]=menu.Any(i=>i.Text=="足球赛程…");c["terminal_menu_added"]=menu.Any(i=>i.Text=="管理员终端");c["clash_menu_has_explicit_actions"]=menu.Single(i=>i.Text=="Clash 系统代理").DropDownItems.Count==3;c["edge_menu_added"]=menu.Any(i=>i.Text=="Edge 搜索…");c["custom_app_menu_added"]=menu.Single(i=>i.Text=="自定义应用").DropDownItems.OfType<ToolStripMenuItem>().Any(i=>i.Text==app.Name);c["hide_does_not_dispose_music"]=!originalMusic.IsDisposed&&ReferenceEquals(originalMusic,launcher.MusicWithoutOpening());c["hidden_custom_still_in_search_catalog"]=launcher.FileApplications.Any(a=>a.Name==app.Name);
            var searchPrefs=LocalApplications.Clone(live);foreach(var e in EntryDisplay.Entries(searchPrefs))searchPrefs.EntryDisplayModes[e.Id]=EntryDisplay.Search;pet.ApplyPlatformSettings(searchPrefs);menu=Entries(pet);c["search_only_hides_music_menu"]=!menu.Single(i=>i.Text=="音乐").Available;c["search_only_hides_media_menu"]=!menu.Single(i=>i.Text=="媒体控制…").Available;c["search_only_hides_other_optional_menus"]=!menu.Any(i=>i.Tag as string=="optional-entry");c["search_only_keeps_capabilities_enabled"]=EntryDisplay.Entries(live).All(e=>e.Enabled);
            var restored=LocalApplications.Clone(live);restored.EntryDisplayModes["music"]=EntryDisplay.Shortcut;restored.EntryDisplayModes[app.Id]=EntryDisplay.Shortcut;pet.ApplyPlatformSettings(restored);var actions=Field<Dictionary<string,(string Title,Action Run)>>(launcher,"shortcutActions");c["shortcut_restores_without_reenable"]=actions.Keys.SequenceEqual(new[]{"music",app.Id});var row=Walk(launcher).OfType<FlowLayoutPanel>().Single(p=>p.Controls.OfType<Button>().Any(b=>b.Tag as string==app.Id));c["existing_shortcut_order_preserved"]=row.Controls.Cast<Control>().Select(b=>b.Tag as string).SequenceEqual(new[]{app.Id,"music"});
            var off=LocalApplications.Clone(live);off.MusicModule=false;off.ApplicationModules[0]=app with{Enabled=false};pet.ApplyPlatformSettings(off);c["disable_removes_shortcut_even_if_displayed"]=Field<Dictionary<string,(string Title,Action Run)>>(launcher,"shortcutActions").Count==0;c["disable_disposes_music_activity"]=originalMusic.IsDisposed;c["disable_keeps_chosen_display_mode"]=live.EntryDisplayModes["music"]==EntryDisplay.Shortcut&&live.EntryDisplayModes[app.Id]==EntryDisplay.Shortcut;var back=LocalApplications.Clone(live);back.MusicModule=true;back.ApplicationModules[0]=app;pet.ApplyPlatformSettings(back);c["reenable_restores_saved_display"]=Field<Dictionary<string,(string Title,Action Run)>>(launcher,"shortcutActions").Count==2;launcher.Hide();
        }
        var routed=new Preferences{ApplicationModules=[app],EntryDisplayModes=EntryDisplay.Entries(prefs).ToDictionary(e=>e.Id,e=>EntryDisplay.Search)};var seen=new List<ActionRequest>();using(var bus=new ActionDispatcher(routed,(a,v,t)=>{seen.Add(new(a,v));return Task.FromResult("ok");},new ExtensionStore(Path.Combine(root,"empty-extensions")))){
            await bus.ExecuteTextAsync("点歌","search");await bus.ExecuteTextAsync("打开终端","voice");await bus.ExecuteAsync(new("web_search","test","chat"));await bus.ExecuteAsync(new("media_toggle","","combo"));await bus.ExecuteAsync(new("local:"+app.Id));c["hidden_actions_still_dispatch"]=seen.Select(r=>r.Action).SequenceEqual(new[]{"music_open","terminal_open","web_search","media_toggle","local_app_open"});routed.MusicModule=false;try{await bus.ExecuteTextAsync("点歌","search");c["disabled_action_rejected"]=false;}catch(InvalidOperationException){c["disabled_action_rejected"]=seen.Count==5;}
        }
        var migration=new ConfigMigration(root);string export=Path.Combine(root,"configuration.leo.json");migration.Export(export,prefs,false);c["migration_preserves_display_choices"]=migration.Inspect(export).Bundle.Preferences.EntryDisplayModes.SequenceEqual(prefs.EntryDisplayModes);var legacy=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(export))!;legacy["Preferences"]!.AsObject().Remove("EntryDisplayModes");File.WriteAllText(export,legacy.ToJsonString());c["old_migration_defaults_to_previous_layout"]=migration.Inspect(export).Bundle.Preferences.EntryDisplayModes.Count==0;
        return c;
    }
}
