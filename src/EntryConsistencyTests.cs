using System.Text.Json;
namespace LeoLauncher;
static class EntryConsistencyTests
{
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var host=new Form{Opacity=0,ShowInTaskbar=false};
        host.Shown+=(_,_)=>{var c=new Dictionary<string,bool>();try{Check(Path.GetDirectoryName(report)!,c);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,external_actions=0}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{host.Close();}};Application.Run(host);
    }
    static void Check(string root,Dictionary<string,bool> c)
    {
        var app=new LocalApplication("0123456789abcdef0123456789abcdef","PRIVATE_NAME",@"C:\PRIVATE_PATH\app.exe");
        var p=new Preferences{ApplicationIndexing=false,ApplicationModules=[app]};
        using(var pet=new PetForm(p,true,true)){
            c["actual_builtin_owners_match"]=EntryContributions.ReadBuiltins(p).All(e=>e.Fault.Length==0);
            c["actual_local_owner_matches"]=EntryContributions.ReadAvailable(p).Single().ModuleId=="local:"+app.Id;
            var edge=EntryContributions.Builtin(p,"edge");
            c["host_transfer_matches_core"]=EntryContributions.Check(edge,a=>a=="launcher_open"?"core":"edge")=="";
            c["host_transfer_wrong_owner_rejected"]=EntryContributions.Check(edge,_=>"edge")=="menu_owner_mismatch";
            c["missing_action_rejected"]=EntryContributions.Check(edge,_=>null)=="action_owner_mismatch";
            c["throwing_registry_isolated"]=EntryContributions.Check(edge,_=>throw new IOException("PRIVATE_ERROR"))=="registry_unavailable";
            var inventory=EntryInventory.Generate(p);
            c["inventory_hides_personal_fields"]=!inventory.Json.Contains("PRIVATE_")&&!inventory.Json.Contains(app.Id)&&!inventory.Json.Contains("Aliases")&&!inventory.Json.Contains("Enabled");
            c["inventory_retains_host_transfer"]=inventory.Json.Contains("launcher_open")&&inventory.Json.Contains("core");
            c["inventory_seven_entries"]=JsonDocument.Parse(inventory.Json).RootElement.GetProperty("entries").GetArrayLength()==7;
            var launcher=(LauncherForm)typeof(PetForm).GetField("launcher",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(pet)!;
            var actions=(Dictionary<string,(string Title,Action Run)>)typeof(LauncherForm).GetField("shortcutActions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(launcher)!;
            var oldMusicClick=actions["music"].Run;
            var damaged=new CapabilityRegistry(p,(_,_,_)=>throw new Exception("never execute"),()=>[],null);
            damaged.Register(new BuiltinLeoModule("football-data","fixture",()=>true,["football_open"],(_,_,_)=>throw new Exception("never execute")));
            damaged.Register(new BuiltinLeoModule("wrong-owner","fixture",()=>true,["music_open"],(_,_,_)=>throw new Exception("never execute")));
            EntryContributions.Bind(p,damaged);var music=EntryContributions.Builtin(p,"music");
            c["wrong_owner_quarantines_music"]=music.Fault=="action_owner_mismatch"&&!music.Shortcut&&!music.Menu;
            c["quarantine_keeps_saved_enablement"]=music.Enabled&&p.MusicModule;
            c["unrelated_builtin_survives"]=EntryContributions.Builtin(p,"media").Shortcut&&EntryContributions.Builtin(p,"edge").Shortcut;
            bool blocked=false;try{EntryContributions.BuiltinAction(p,"music");}catch(InvalidOperationException){blocked=true;}c["old_click_rechecks_owner"]=blocked;
            blocked=false;try{oldMusicClick();}catch(InvalidOperationException){blocked=true;}c["captured_shortcut_rechecks_owner"]=blocked;
            launcher.RefreshModules();c["actual_shortcut_quarantine_is_local"]=!actions.ContainsKey("music")&&actions.ContainsKey("media")&&actions.ContainsKey("football");
            EntryContributions.Bind(p,pet.ContractRegistry);launcher.RefreshModules();c["registry_repair_restores_shortcut"]=actions.ContainsKey("music");
        }
        var survivor=app with{Id="1123456789abcdef0123456789abcdef",Name="survivor",Path=@"C:\OTHER_PRIVATE_PATH\app.exe"};
        var prefs=new Preferences{ApplicationModules=[app,app,survivor]};
        using var bus=new ActionDispatcher(prefs,(_,_,_)=>throw new Exception("never execute"),new ExtensionStore(Path.Combine(root,"extensions")));
        EntryContributions.Bind(prefs,bus.Registry);
        c["duplicate_entries_isolated"]=EntryContributions.ReadAvailable(prefs).Single().Id==survivor.Id;
        c["duplicate_registry_keeps_survivor"]=bus.Registry.Resolve("local:"+survivor.Id).Id=="local:"+survivor.Id;
        bool bypass=false;try{EntryContributions.ForTarget(prefs,app.Path);}catch(InvalidOperationException){bypass=true;}
        c["duplicate_cannot_bypass_via_indexed_path"]=bypass;
        c["duplicate_action_not_resolved"]=bus.Registry.EntryOwner("local:"+app.Id)==null;
        prefs.ApplicationModules=[app,survivor];
        c["repair_restores_entry"]=EntryContributions.ReadAvailable(prefs).Length==2;
        prefs.ApplicationModules.Remove(app);
        bool removed=false;try{EntryContributions.CurrentAction(prefs,app.Id);}catch(InvalidOperationException){removed=true;}
        c["removed_entry_old_click_rejected"]=removed;
        c["removed_registry_action_gone"]=bus.Registry.EntryOwner("local:"+app.Id)==null;
        prefs.ApplicationModules.Add(app with{Id="invalid"});
        c["malformed_entry_isolated"]=EntryContributions.ReadAvailable(prefs).Single().Id==survivor.Id;
        c["malformed_registry_keeps_survivor"]=bus.Registry.Resolve("local:"+survivor.Id).Enabled;
    }
}
