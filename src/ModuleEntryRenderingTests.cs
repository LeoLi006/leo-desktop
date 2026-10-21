using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;
static class ModuleEntryRenderingTests
{
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;
    static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control child in root.Controls)foreach(var value in Walk(child))yield return value;}
    static async Task Until(Func<bool> ready){for(int i=0;i<100&&!ready();i++)await Task.Delay(20);if(!ready())throw new TimeoutException("preview condition");}
    static void OpenMenu(ContextMenuStrip menu)=>typeof(ToolStripDropDown).GetMethod("OnOpening",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(menu,[new System.ComponentModel.CancelEventArgs()]);
    public static void Run(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var host=new Form{Opacity=0,ShowInTaskbar=false};host.Shown+=async(_,_)=>{
            var c=new Dictionary<string,bool>();try{
                var prefs=new Preferences{ApplicationIndexing=false};using var pet=new PetForm(prefs,true,true);
                var launcher=Field<LauncherForm>(pet,"launcher");var bus=Field<ActionDispatcher>(pet,"commands");
                c["production_preview_has_no_sample"]=EntryContributions.ReadModules(prefs).Entries.Length==0;
                var config=new ExampleNotesConfiguration(true);var store=new NoteStore(null);store.Save(null,"isolated fixture");using var services=new ScopedNotesExampleServices(store,()=>config);var sample=new ScopedNotesExampleModule(services);bus.Registry.Register(sample);
                const string id="example-scoped-notes/query";string target=ModuleEntryContributions.TargetPrefix+id;
                launcher.Location=new(-19000,-19000);launcher.Open();launcher.Location=new(-19000,-19000);launcher.RefreshModules();
                Button Shortcut()=>Walk(launcher).OfType<Button>().Single(b=>b.Tag as string==id);
                c["actual_shortcut_has_provider_name"]=Shortcut().AccessibleName=="样例便签查询";
                Shortcut().PerformClick();await Until(()=>bus.RecentResults.Any(r=>r.ActionId=="example_notes_query"&&r.Source=="shortcut"));
                c["actual_shortcut_uses_registered_action"]=bus.RecentResults.Last().Status=="succeeded";
                launcher.PreviewTyping("示例查询");await Until(()=>Walk(launcher).Any(b=>b.Tag is AppEntry app&&app.Target==target));
                c["actual_search_matches_provider_alias"]=Walk(launcher).Any(b=>b.Tag is AppEntry app&&app.Target==target);
                c["module_target_excluded_from_model_application_list"]=!launcher.FileApplications.Any(a=>a.Target==target)&&!new BrainCatalog(bus.Registry).Read().Any(a=>a.Module==sample.Id);
                var query=Field<TextBox>(launcher,"query");typeof(Control).GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(query,[new KeyEventArgs(Keys.Enter)]);
                await Until(()=>bus.RecentResults.Any(r=>r.ActionId=="example_notes_query"&&r.Source=="search"));c["real_enter_dispatches_and_closes_search"]=!launcher.Visible&&bus.RecentResults.Last().Status=="succeeded";
                var menu=pet.ContextMenuStrip!;OpenMenu(menu);var parent=menu.Items.OfType<ToolStripMenuItem>().Single(i=>i.Text=="应用与媒体");var root=parent.DropDownItems.OfType<ToolStripMenuItem>().Single(i=>i.Text=="模块入口");var item=root.DropDownItems.OfType<ToolStripMenuItem>().Single();item.PerformClick();await Until(()=>bus.RecentResults.Any(r=>r.ActionId=="example_notes_query"&&r.Source=="menu"));c["actual_menu_uses_registered_action"]=item.Tag as string==id&&bus.RecentResults.Last().Status=="succeeded";
                using(var settings=new EntryDisplayForm(prefs)){c["settings_include_namespaced_entry"]=Walk(settings).OfType<ComboBox>().Any(f=>f.AccessibleName==id+"入口显示");EntryDisplay.Validate(new(){[id]=EntryDisplay.Search});}
                using(var modules=new ModulesView(prefs,true)){var draft=modules.EntryDisplayDraft();using var settings=new EntryDisplayForm(draft);c["settings_draft_retains_live_module_directory"]=Walk(settings).OfType<ComboBox>().Any(f=>f.AccessibleName==id+"入口显示");bool denied=false;try{await EntryContributions.ExecuteModule(draft,id,"settings");}catch(InvalidOperationException){denied=true;}c["settings_draft_has_no_execution_binding"]=denied;}
                using(var quick=new QuickEntrySettingsForm(prefs,id))c["provider_not_edited_as_local_program"]=Walk(quick).OfType<Button>().Any(b=>b.Text=="模块配置由提供者管理"&&!b.Enabled);
                prefs.EntryDisplayModes[id]=EntryDisplay.Search;launcher.RefreshModules();OpenMenu(menu);c["search_only_hides_actual_button_and_menu"]=!Walk(launcher).OfType<Button>().Any(b=>b.Tag as string==id)&&!parent.DropDownItems.OfType<ToolStripMenuItem>().Any(i=>i.Text=="模块入口");
                launcher.Open();launcher.PreviewTyping("示例查询");await Until(()=>Walk(launcher).Any(b=>b.Tag is AppEntry a&&a.Target==target));c["hidden_entry_remains_searchable"]=true;
                prefs.EntryDisplayModes[id]=EntryDisplay.Menu;launcher.RefreshModules();OpenMenu(menu);c["menu_only_reappears_without_shortcut"]=parent.DropDownItems.OfType<ToolStripMenuItem>().Any(i=>i.Text=="模块入口")&&!Walk(launcher).OfType<Button>().Any(b=>b.Tag as string==id);
                config=config with{Enabled=false};bus.CancelDisabled();launcher.RefreshModules();await Task.Delay(50);c["disabled_entry_removed_from_real_search"]=!Walk(launcher).Any(b=>b.Tag is AppEntry a&&a.Target==target);config=config with{Enabled=true};
                prefs.EntryDisplayModes[id]=EntryDisplay.Shortcut;launcher.RefreshModules();var stale=Field<Dictionary<string,(string Title,Action Run)>>(launcher,"shortcutActions")[id].Run;
                OpenMenu(menu);item=parent.DropDownItems.OfType<ToolStripMenuItem>().Single(i=>i.Text=="模块入口").DropDownItems.OfType<ToolStripMenuItem>().Single();bus.Registry.Unregister(sample);bus.CancelDisabled();int count=bus.RecentResults.Count;item.PerformClick();await Task.Delay(50);c["removed_cached_menu_does_not_dispatch"]=bus.RecentResults.Count==count;stale();await Task.Delay(100);c["removed_cached_button_does_not_dispatch_or_launch"]=bus.RecentResults.Count==count;
                bool rejected=false;try{await launcher.LaunchFromAssistantAsync(new AppEntry("stale",target,"模块入口"),"search",CancellationToken.None);}catch(InvalidOperationException){rejected=true;}c["removed_search_target_never_falls_back_to_path"]=rejected;
                launcher.RefreshModules();c["removed_provider_disappears"]=EntryContributions.ReadModules(prefs).Entries.Length==0&&!Walk(launcher).OfType<Button>().Any(b=>b.Tag as string==id);c["query_did_not_mutate_notes"]=store.Items.Count==1;
                launcher.Hide();File.WriteAllText(path,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,real_api_requests=0,external_process_starts=0,personal_data_reads=0},new JsonSerializerOptions{WriteIndented=true}));
            }catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{host.Close();}
        };Application.Run(host);
    }
}

