using System.Text.Json;
namespace LeoLauncher;

static class Feature41Tests
{
    static IEnumerable<Control> All(Control root){yield return root;foreach(Control c in root.Controls)foreach(var child in All(c))yield return child;}
    static void Capture(Form form,string file,string output){using var b=new Bitmap(form.Width,form.Height);form.DrawToBitmap(b,new Rectangle(Point.Empty,form.Size));b.Save(Path.Combine(output,file));}
    public static Dictionary<string,bool> Check(string output)
    {
        var c=new Dictionary<string,bool>();string directory=Path.Combine(output,"feature41-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        using(var footer=new FootTools(130,true)){
            footer.Show();Application.DoEvents();var buttons=All(footer).OfType<Button>().ToArray();Button Button(string name)=>buttons.Single(b=>b.AccessibleName==name);
            c["footer_chat_todos_and_disclosure_are_always_on_first_row"]=buttons.Count(b=>b.Visible)==3&&Button("问 Leo").Top==Button("每日清单").Top&&Button("每日清单").Top==Button("展开更多工具").Top;
            Button("展开更多工具").PerformClick();int requests=0;footer.MusicRequested+=()=>requests++;Button("QQ音乐点歌").PerformClick();
            c["all_four_optional_modules_share_dropdown_row"]=new[]{"打开管理员终端","Clash Verge 系统代理","QQ音乐点歌","展开 Edge 搜索"}.All(n=>Button(n).Visible&&Button(n).Top>Button("问 Leo").Top)&&requests==1;
            c["expanded_tools_fit_with_no_clipped_icons"]=buttons.Where(b=>b.Visible).All(b=>footer.ClientRectangle.Contains(b.Bounds));
            Capture(footer,"preview-footer41.png",output);
            Button("展开 Edge 搜索").PerformClick();c["edge_input_fits_alongside_optional_row"]=All(footer).OfType<TextBox>().Single().Visible&&footer.ClientRectangle.Contains(All(footer).OfType<TextBox>().Single().Bounds);
            footer.ConfigureModules(new(){TerminalModule=false,ClashModule=false,MusicModule=false,EdgeModule=false});footer.PreviewUtilities();
            c["disabling_all_modules_keeps_three_main_buttons_without_empty_row"]=buttons.Count(b=>b.Visible)==3&&footer.Height==28;
        }
        var original=ModulePaths.Snapshot();
        try{
            var paths=new Dictionary<string,string>();foreach(var (id,name) in new[]{("music","QQMusic.exe"),("clash","clash-verge.exe"),("edge","msedge.exe"),("terminal","pwsh.exe")}){string path=Path.Combine(directory,name);File.WriteAllText(path,"test fixture, never executed");paths[id]=path;}
            ModulePaths.Configure(paths);c["manual_paths_take_priority_without_launching_apps"]=QQMusic.ClientPath()==paths["music"]&&ClashProxy.ClientPath()==paths["clash"]&&FootTools.TerminalStart().FileName==paths["terminal"]&&FootTools.EdgeStart("测试").FileName==paths["edge"];
            c["manual_terminal_keeps_windows_elevation"]=FootTools.TerminalStart().Verb=="runas";
            bool wrong=false;try{ModulePaths.Validate("music",paths["edge"]);}catch(ArgumentException){wrong=true;}c["wrong_module_executable_is_rejected"]=wrong;
            ModulePaths.Configure(new());using var dialog=new ModulePathsForm(paths);dialog.Show();Application.DoEvents();Capture(dialog,"preview-module-paths41.png",output);
        }finally{ModulePaths.Configure(original);}
        var today=DateOnly.FromDateTime(DateTime.Today);var store=new TodoStore(null);var item=store.Add("右键事项",today,DateTimeOffset.Now.AddHours(2));var repeat=store.AddRepeating("重复运动",today,"daily");
        using(var planner=new TodoForm(true,store)){
            planner.Show();Application.DoEvents();Control Row(string id)=>All(planner).First(r=>r.Tag is string value&&value==id);
            var row=Row(item.Id);c["task_context_menu_is_available_on_title_and_row"]=row.ContextMenuStrip!=null&&row.Controls.Cast<Control>().All(child=>child.ContextMenuStrip==row.ContextMenuStrip);
            ((ToolStripMenuItem)row.ContextMenuStrip!.Items.Cast<ToolStripItem>().Single(i=>i.Text=="移到明天")).PerformClick();Application.DoEvents();
            c["context_move_changes_day_and_shifts_reminder"]=store.Items.Single(t=>t.Id==item.Id).Day==today.AddDays(1)&&store.Items.Single(t=>t.Id==item.Id).Reminder==item.Reminder?.AddDays(1);
            store.Undo();planner.View.Render();row=Row(repeat.Id);((ToolStripMenuItem)row.ContextMenuStrip!.Items.Cast<ToolStripItem>().Single(i=>i.Text=="仅跳过本次")).PerformClick();Application.DoEvents();
            c["context_skip_keeps_future_repeat_instances"]=store.Items.All(t=>t.Id!=repeat.Id)&&store.Items.Any(t=>t.SeriesId==repeat.SeriesId&&t.Day==today.AddDays(1));
        }
        string source=Path.Combine(directory,"source"),target=Path.Combine(directory,"target");Directory.CreateDirectory(source);Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(source,"leo-ai.json"),"source-secret-not-for-export");File.WriteAllText(Path.Combine(target,"leo-ai.json"),"target-key-must-stay");
        var sourceTasks=new TodoStore(Path.Combine(source,"leo-todos.json"));sourceTasks.AddRepeating("迁移任务",today,"daily");
        var sourcePrefs=new Preferences{PetSize=160,Favorites=["clipboard:fixture"],Aliases=new(){["fixture"]=["简称"]},ModuleExecutables=new(){["music"]=@"C:\missing-leo-fixture\QQMusic.exe"}};
        var exporter=new ConfigMigration(source);string package=Path.Combine(directory,"export.leo.json");exporter.Export(package,sourcePrefs,true);string exported=File.ReadAllText(package);
        c["export_excludes_api_key_chat_and_clipboard_data"]=!exported.Contains("source-secret-not-for-export")&&!exported.Contains("EncryptedKey")&&!exported.Contains("leo-chat-history");
        var importer=new ConfigMigration(target);var plan=importer.Inspect(package);c["missing_paths_fall_back_to_auto_with_visible_note"]=plan.Notes.Length==1&&plan.Bundle.Preferences.ModuleExecutables.Count==0;
        Preferences current=new(){PetSize=120,X=200,Y=300};var targetTasks=new TodoStore(Path.Combine(target,"leo-todos.json"));
        importer.Apply(plan,current,next=>current=next,()=>targetTasks.ReloadImported());
        c["import_restores_preferences_and_live_recurring_tasks"]=current.PetSize==160&&current.Aliases["fixture"].Single()=="简称"&&targetTasks.Items.Count==36&&current.X==200&&current.Y==300;
        c["import_preserves_local_api_key"]=File.ReadAllText(Path.Combine(target,"leo-ai.json"))=="target-key-must-stay";
        string backup=importer.LatestBackup!;importer.Apply(importer.Inspect(backup),current,next=>current=next,()=>targetTasks.ReloadImported(),true);
        c["one_click_restore_returns_to_pre_import_settings"]=current.PetSize==120&&targetTasks.Items.Count==0;
        var before=Directory.GetFiles(target,"*.json").ToDictionary(f=>f,File.ReadAllText);int attempts=0;bool failed=false;
        try{importer.Apply(importer.Inspect(package),current,next=>{if(++attempts==1)throw new IOException("fixture application failure");current=next;},()=>targetTasks.ReloadImported());}catch(IOException){failed=true;}
        c["failed_import_rolls_back_all_changed_files"]=failed&&before.All(p=>File.ReadAllText(p.Key)==p.Value)&&current.PetSize==120;
        exporter.Export(package,sourcePrefs,false);string taskFile=Path.Combine(target,"leo-todos.json");File.WriteAllText(taskFile,"[]");string taskBefore=File.ReadAllText(taskFile);importer.Apply(importer.Inspect(package),current,next=>current=next,()=>{});
        c["configuration_only_import_preserves_existing_todo_file"]=File.ReadAllText(taskFile)==taskBefore;
        string invalid=Path.Combine(directory,"invalid.json");File.WriteAllText(invalid,"{\"Format\":\"LeoConfig\",\"Version\":1}");bool rejected=false;try{importer.Inspect(invalid);}catch(ArgumentException){rejected=true;}c["incomplete_config_is_rejected_before_writing"]=rejected;
        string portable=Path.Combine(directory,"portable.leo.json");var oldApp=new AppEntry("示例应用",@"C:\old-user\app.lnk",ExePath:"Example.exe");var newApp=oldApp with{Target=@"D:\new-user\app.lnk"};
        File.WriteAllText(Path.Combine(source,"leo-custom-combos.json"),JsonSerializer.Serialize(new[]{new CustomCombo("portable","启动示例应用",[new("app",oldApp.Target)])}));
        var portablePrefs=new Preferences{Favorites=[oldApp.Target],Aliases=new(){[oldApp.Target]=["example"]}};
        new ConfigMigration(source,()=>[oldApp]).Export(portable,portablePrefs,false);var portablePlan=new ConfigMigration(target,()=>[newApp]).Inspect(portable);
        c["cross_machine_import_remaps_favorites_and_aliases_by_app_identity"]=portablePlan.Bundle.Preferences.Favorites.Single()==newApp.Target&&portablePlan.Bundle.Preferences.Aliases[newApp.Target].Single()=="example"&&portablePlan.Bundle.Combos.Single().Steps.Single().Value==newApp.Target;
        using(var transfer=new ConfigMigrationForm(new(),_=>{},()=>{},true)){transfer.Show();Application.DoEvents();Capture(transfer,"preview-config-transfer41.png",output);c["migration_dialog_actions_fit_window"]=All(transfer).OfType<Button>().All(b=>b.Parent!.ClientRectangle.Contains(b.Bounds));}
        return c;
    }
}
