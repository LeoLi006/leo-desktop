using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;

static class Q6PackageTests
{
    static bool Reject(Action action){try{action();return false;}catch(Exception e)when(e is ArgumentException or JsonException or IOException){return true;}}
    static IEnumerable<Control> Walk(Control control){foreach(Control child in control.Controls){yield return child;foreach(var nested in Walk(child))yield return nested;}}
    static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(value)!;
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var ui=new Form{Opacity=0,ShowInTaskbar=false};
        ui.Shown+=async(_,_)=>{try{var checks=await Check(Path.GetDirectoryName(report)!);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(x=>x),checks,real_api_requests=0,external_process_starts=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}finally{ui.Close();}};Application.Run(ui);
    }
    static async Task<Dictionary<string,bool>> Check(string root)
    {
        var c=new Dictionary<string,bool>();
        var app=new LocalApplication(Guid.NewGuid().ToString("N"),"模块试点",Environment.ProcessPath!,Arguments:"--private-fixture",WorkingDirectory:root,Administrator:true);
        var other=app with{Id=Guid.NewGuid().ToString("N"),Name="独立应用",Arguments=""};
        var original=new Preferences{ApplicationModules=[other],DisplayName="Fixture",Favorites=["private-favorite"],ApplicationIndexing=false};
        var package=ApplicationPackages.Create(app);var json=ApplicationPackages.Export(package);
        c["pure_descriptor_roundtrip"]=ApplicationPackages.Parse(json).Id==package.Id;
        c["export_excludes_local_configuration"]=!json.Contains(app.Path)&&!json.Contains("private-fixture")&&!json.Contains(root)&&!json.Contains("Administrator")&&!json.Contains("Icon")&&!json.Contains("private-favorite");
        c["unsupported_protocol_rejected"]=Reject(()=>ApplicationPackages.Validate(package with{Protocol=2}));
        c["unknown_format_rejected"]=Reject(()=>ApplicationPackages.Validate(package with{Format="LeoModule/1"}));
        c["incompatible_host_rejected"]=Reject(()=>ApplicationPackages.Validate(package with{HostMin="2.0.0",HostMax="3.0.0"}));
        c["inverted_host_range_rejected"]=Reject(()=>ApplicationPackages.Validate(package with{HostMin="1.9.0",HostMax="1.0.0"}));
        c["unsupported_dependency_rejected"]=Reject(()=>ApplicationPackages.Validate(package with{Dependencies=["windows","run-script"]}));
        c["duplicate_dependency_rejected"]=Reject(()=>ApplicationPackages.Validate(package with{Dependencies=["windows","windows"]}));
        c["invalid_version_rejected"]=Reject(()=>ApplicationPackages.Validate(package with{Version="1.2"}));
        c["unknown_fields_cannot_smuggle_executable"]=Reject(()=>ApplicationPackages.Parse(json.TrimEnd().TrimEnd('}')+",\"Executable\":\"evil.exe\"}"));
        c["duplicate_fields_rejected"]=Reject(()=>ApplicationPackages.Parse(json.Replace("\"Protocol\": 1","\"Protocol\": 1, \"Protocol\": 1")));
        c["missing_fields_rejected"]=Reject(()=>ApplicationPackages.Parse("{}"));
        c["null_fields_rejected"]=Reject(()=>ApplicationPackages.Validate(package with{Dependencies=null!}));
        c["oversize_descriptor_rejected"]=Reject(()=>ApplicationPackages.Parse(new string(' ',16385)));
        c["corrupt_json_rejected"]=Reject(()=>ApplicationPackages.Parse("{broken"));
        var tooBig=Path.Combine(root,"oversize.leoapp.json");File.WriteAllText(tooBig,new string(' ',65537));c["oversize_file_rejected"]=Reject(()=>ApplicationPackages.Read(tooBig));
        var installed=ApplicationPackages.Install(original,package,app);
        c["installation_is_draft_until_save"]=original.ApplicationModules.Count==1&&original.ApplicationPackages.Count==0&&installed.ApplicationModules.Count==2;
        c["installation_preserves_other_data"]=installed.Favorites.SequenceEqual(original.Favorites)&&installed.ApplicationModules.Contains(other)&&installed.DisplayName==original.DisplayName;
        c["same_version_rejected"]=Reject(()=>ApplicationPackages.Install(installed,package,app));
        var updated=ApplicationPackages.Install(installed,package with{Version="1.1.0",Name="公开新名称"},app);
        c["update_preserves_local_identity_and_configuration"]=updated.ApplicationModules.Single(x=>x.Id==app.Id)==app&&updated.ApplicationPackages[app.Id].Manifest.Version=="1.1.0";
        c["downgrade_rejected"]=Reject(()=>ApplicationPackages.Install(updated,package,app));
        c["identity_change_rejected"]=Reject(()=>ApplicationPackages.Install(installed,package with{Version="1.2.0"},app with{Id=Guid.NewGuid().ToString("N")}));
        c["binding_conflict_rejected"]=Reject(()=>ApplicationPackages.Install(installed,package with{Id=Guid.NewGuid().ToString("N")},app));
        c["bad_local_path_rejected_without_touching_old"]=Reject(()=>ApplicationPackages.Install(installed,package with{Version="1.2.0"},app with{Path=Path.Combine(root,"missing.exe")}))&&installed.ApplicationModules.Contains(app);
        var path=Path.Combine(root,"transaction-settings.json");ConfigMigration.Write(path,JsonSerializer.Serialize(installed));var old=File.ReadAllText(path);
        AtomicJson.BeforeReplace=file=>{if(file==path)throw new IOException("injected disk failure");};
        try{c["failed_upgrade_keeps_original_disk_snapshot"]=Reject(()=>ConfigMigration.Write(path,JsonSerializer.Serialize(updated)))&&File.ReadAllText(path)==old;}finally{AtomicJson.BeforeReplace=null;}
        c["failed_save_cleans_staging_file"]=Directory.GetFiles(root,"transaction-settings.json.*.tmp").Length==0;
        ConfigMigration.Write(path,JsonSerializer.Serialize(updated));
        c["upgrade_keeps_previous_recoverable_snapshot"]=Store.Validate(File.ReadAllText(path+".bak")).ApplicationPackages[app.Id].Manifest.Version=="1.0.0";
        c["persisted_upgrade_reloads"]=Store.Validate(File.ReadAllText(path)).ApplicationPackages[app.Id].Manifest.Version=="1.1.0";
        var removed=ApplicationPackages.Remove(updated,app.Id);
        c["remove_preserves_local_configuration_and_other_module"]=!removed.ApplicationModules.Any(x=>x.Id==app.Id)&&removed.ApplicationModules.Contains(other)&&removed.ApplicationPackages[app.Id] is{Installed:false,Local:var retained}&&retained==app;
        c["remove_preserves_entry_preferences"]=ApplicationPackages.Remove(withPreferences(),app.Id).EntryDisplayModes.GetValueOrDefault(app.Id)==EntryDisplay.Menu;
        Preferences withPreferences(){var next=LocalApplications.Clone(updated);next.EntryDisplayModes[app.Id]=EntryDisplay.Menu;return next;}
        var reinstalled=ApplicationPackages.Install(removed,package with{Version="1.1.0"},removed.ApplicationPackages[app.Id].Local);
        c["same_version_reinstall_restores_identity"]=reinstalled.ApplicationModules.Contains(app)&&reinstalled.ApplicationPackages[app.Id].Installed;
        c["state_rejects_duplicate_package_identity"]=Reject(()=>{var invalid=LocalApplications.Clone(installed);invalid.ApplicationPackages[other.Id]=new(package,other);ApplicationPackages.ValidateState(invalid);});
        c["old_settings_without_packages_load"]=Store.Validate(JsonSerializer.Serialize(original).Replace("\"ApplicationPackages\":{},","")).ApplicationPackages.Count==0;
        c["status_identifies_version_and_dependency"]=ApplicationPackages.Summary(installed,app.Id).Contains("v1.0.0")&&ApplicationPackages.Summary(installed,app.Id).Contains("依赖就绪");
        c["status_identifies_removed_retention"]=ApplicationPackages.Summary(removed,app.Id).Contains("本机配置已保留");
        using(var manager=new LocalApplicationsForm(installed)){
            manager.Show();Application.DoEvents();var list=Walk(manager).OfType<ListBox>().Single();list.SelectedItem=list.Items.Cast<ComboChoice>().Single(x=>x.Value==app.Id);
            var button=Walk(manager).OfType<Button>().Single(x=>x.Text=="移除");button.PerformClick();
            c["manager_remove_is_staged"]=installed.ApplicationModules.Contains(app)&&!Field<Preferences>(manager,"draft").ApplicationModules.Contains(app);
            c["package_controls_visible_and_within_window"]=Walk(manager).OfType<Button>().Where(x=>x.Text.Contains("模块包")).Count()==2&&Walk(manager).OfType<Button>().All(x=>manager.ClientRectangle.Contains(x.Bounds));
            using var bitmap=new Bitmap(manager.Width,manager.Height);manager.DrawToBitmap(bitmap,manager.ClientRectangle);bitmap.Save(Path.Combine(root,"manager-preview.png"));
            Walk(manager).OfType<Button>().Single(x=>x.Text=="确定").PerformClick();c["manager_confirm_copies_retained_metadata"]=!installed.ApplicationModules.Contains(app)&&!installed.ApplicationPackages[app.Id].Installed;
        }
        using(var modules=new ModulesView(reinstalled,true)){
            var next=new Preferences();modules.Apply(next,false);c["settings_modules_view_preserves_package_metadata"]=next.ApplicationPackages[app.Id].Manifest.Id==package.Id;
        }
        using(var preview=new ApplicationPackagePreview(package,app)){
            preview.Show();Application.DoEvents();c["import_preview_shows_local_target_and_cancel"]=Walk(preview).OfType<TextBox>().Any(x=>x.Text.Contains(app.Path))&&preview.CancelButton!=null;
        }
        var live=LocalApplications.Clone(reinstalled);int starts=0;
        using(var bus=new ActionDispatcher(live,(_,_,_)=>Task.FromResult("legacy"),new ExtensionStore(Path.Combine(root,"extensions")),new ApplicationCapabilityHost((a,_)=>Task.FromResult(a),(_,_)=>{starts++;return Task.FromResult(true);}))) {
            c["installed_package_uses_existing_registry"]=bus.Registry.Resolve("local:"+app.Id) is LocalApplicationModule;
            c["installed_package_reuses_launcher"]= (await bus.ExecuteDetailedAsync(new("local_app_open",app.Id))).Status=="succeeded"&&starts==1;
            live.ApplicationModules=removed.ApplicationModules;bus.CancelDisabled();
            c["removed_package_cannot_execute"]= (await bus.ExecuteDetailedAsync(new("local_app_open",app.Id))).ErrorCode=="module_removed";
            c["other_module_still_runs_after_removal"]= (await bus.ExecuteDetailedAsync(new("local_app_open",other.Id))).Status=="succeeded"&&starts==2;
        }
        using(var pet=new PetForm(original,true,true)){
            pet.Show();Application.DoEvents();pet.ApplyPlatformSettings(reinstalled);c["actual_host_applies_package_metadata"]=original.ApplicationPackages[app.Id].Manifest.Id==package.Id;pet.Close();
        }
        return c;
    }
}
