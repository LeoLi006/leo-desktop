using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;
static class ModuleStatusContractTests
{
    static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control child in root.Controls)foreach(var item in Walk(child))yield return item;}
    public static void Run(string report){Directory.CreateDirectory(Path.GetDirectoryName(report)!);using var host=new Form{Opacity=0,ShowInTaskbar=false};host.Shown+=(_,_)=>{var checks=new Dictionary<string,bool>();try{Check(Path.GetDirectoryName(report)!,checks);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(x=>x),checks,network_requests=0,external_processes_started=0,personal_data_reads=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks}));}finally{host.Close();}};Application.Run(host);}
    static void Check(string directory,Dictionary<string,bool> c)
    {
        string root=Path.Combine(directory,"status-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string program=Path.Combine(root,"fixture.exe");File.WriteAllText(program,"isolated, never execute");var prefs=new Preferences{ApplicationIndexing=false};var app=new LocalApplication(Guid.NewGuid().ToString("N"),"隔离应用",program,Arguments:"private fixture argument");prefs.ApplicationModules.Add(app);var store=new ExtensionStore(Path.Combine(root,"modules")){Preferences=prefs};int executed=0;
        using var bus=new ActionDispatcher(prefs,(_,_,_)=>{executed++;throw new InvalidOperationException("must not execute");},store);EntryContributions.Bind(prefs,bus.Registry,bus);
        ModuleContractSnapshot? Read(string action,LocalApplication? expected)=>EntryContributions.ReadStatusContract(prefs,action,expected);
        ModuleStatus[] Rows()=>ModuleStatuses.ReadConfigured(prefs,store,Read);
        ModuleStatus App()=>Rows().Single(r=>r.Id==app.Id);
        c["local_source_and_action_preserved"]=App() is{State:ModuleState.Ready,Action:"应用配置"}&&App().Source.Contains("自定义应用");
        c["ready_does_not_claim_running"]=App().Label=="检查通过"&&App().Detail.Contains("不代表程序已启动");
        prefs.EntryDisplayModes[app.Id]=EntryDisplay.Search;c["hidden_entry_does_not_disable_capability"]=App().State==ModuleState.Ready;
        prefs.ApplicationModules[0]=app with{Enabled=false,Path=Path.Combine(root,"missing.exe")};c["disabled_precedes_path_probe"]=App().State==ModuleState.Disabled;
        prefs.ApplicationModules[0]=app with{Path=Path.Combine(root,"missing.exe")};c["missing_file_not_ready"]=App().State==ModuleState.Invalid;
        prefs.ApplicationModules[0]=app with{WorkingDirectory=Path.Combine(root,"missing-directory")};c["missing_working_directory_not_ready"]=App().State==ModuleState.Invalid;
        prefs.ApplicationModules[0]=app;var contract=Read("local:"+app.Id,app)!;
        ModuleStatus State(ModuleContractSnapshot? snapshot)=>ModuleStatuses.FromContract(app.Id,app.Name,"fixture","应用配置",snapshot);
        c["unbound_contract_unknown"]=State(null).State==ModuleState.Unknown;
        c["wrong_contract_owner_rejected"]=State(contract with{Contract=contract.Contract with{Id="another-module"}}).State==ModuleState.Invalid;
        c["stale_configuration_unknown"]=State(contract with{ConfigurationIsCurrent=false}).State==ModuleState.Unknown;
        c["unknown_and_degraded_dependencies_unknown"]=State(contract with{Dependencies=[new("application-file","unknown")]}).State==ModuleState.Unknown&&State(contract with{Dependencies=[new("application-file","degraded")]}).State==ModuleState.Unknown;
        c["missing_dependency_explicit"]=State(contract with{Dependencies=[new("application-file","missing")]}).State==ModuleState.Invalid;
        var draft=LocalApplications.Clone(prefs);EntryContributions.BindReadOnlyView(prefs,draft);draft.ApplicationModules[0]=app with{Name="未保存更名"};var draftRows=ModuleStatuses.ReadConfigured(draft,store,(a,e)=>EntryContributions.ReadStatusContract(draft,a,e));c["draft_does_not_borrow_old_configuration_success"]=draftRows.Single().State==ModuleState.Unknown;
        prefs.ApplicationModules.Add(app);c["duplicate_application_isolated_once"]=Rows().Count(r=>r.Id==app.Id)==1&&App().State==ModuleState.Invalid;prefs.ApplicationModules.RemoveAt(1);
        var extension=new ExtensionManifest("LeoModule/1","fixture-status","隔离扩展","1.0.0",[new("one","命令一",[],"todos_open"),new("two","命令二",[],"todos_open")]);store.Install(extension);
        c["extension_is_one_module_not_one_row_per_entry"]=Rows().Count(r=>r.Id=="ext:"+extension.Id)==1;
        ModuleStatus Ext()=>Rows().Single(r=>r.Id=="ext:"+extension.Id);
        c["declaration_source_version_and_target_dependency"]=Ext().Source.Contains("声明式扩展 · v1.0.0 · 已启用")&&Ext().State==ModuleState.Ready;
        prefs.DisabledExtensions.Add(extension.Id);c["disabled_extension_synchronized"]=Ext().State==ModuleState.Disabled&&Ext().Source.Contains("已停用");prefs.DisabledExtensions.Clear();c["reenable_refreshes"]=Ext().State==ModuleState.Ready;
        store.Install(extension with{Version="1.1.0",Name="扩展新名字"});c["update_refreshes_name_version"]=Ext().Name=="扩展新名字"&&Ext().Source.Contains("v1.1.0");
        string manifestFile=Path.Combine(store.Root,extension.Id,"module.json");File.WriteAllText(manifestFile,JsonSerializer.Serialize(extension with{Version="1.1.0",HostMin="999.0"}));c["incompatible_does_not_claim_ready"]=Ext().State==ModuleState.Invalid&&Ext().Detail.Contains("需要更高版本");
        File.WriteAllText(manifestFile,JsonSerializer.Serialize(extension with{Version="1.1.0",Dependencies=["module:missing"]}));c["missing_declared_dependency_visible"]=Ext().State==ModuleState.Invalid&&Ext().Detail.Contains("module:missing");
        File.WriteAllText(manifestFile,JsonSerializer.Serialize(extension with{Version="1.1.0"}));c["compatibility_recovery_not_historical_failure"]=Ext().State==ModuleState.Ready;
        var worker=extension with{Id="fixture-status-worker",Runtime="stdio",Executable="fixture.exe",Commands=[new("hello","隔离worker",[],"worker:hello")]};string workerFolder=Path.Combine(store.Root,worker.Id);Directory.CreateDirectory(workerFolder);File.WriteAllText(Path.Combine(workerFolder,"module.json"),JsonSerializer.Serialize(worker));
        c["missing_worker_is_not_ready"]=Rows().Single(r=>r.Id=="ext:"+worker.Id).State==ModuleState.Invalid;File.Copy(program,Path.Combine(workerFolder,"fixture.exe"));c["existing_worker_only_passively_ready"]=Rows().Single(r=>r.Id=="ext:"+worker.Id).State==ModuleState.Ready&&!ModuleWorker.Running(worker.Id);
        var mixed=ModuleStatuses.ReadConfigured(prefs,store,(a,e)=>a.StartsWith("local:")?throw new IOException("private path / never show"):Read(a,e));c["single_service_failure_does_not_hide_other_modules"]=mixed.Single(r=>r.Id==app.Id).State==ModuleState.Invalid&&mixed.Single(r=>r.Id=="ext:"+extension.Id).State==ModuleState.Ready&&!string.Join(" ",mixed.Select(r=>r.Detail)).Contains("private path");
        string malformed=Path.Combine(store.Root,"malformed");Directory.CreateDirectory(malformed);File.WriteAllText(Path.Combine(malformed,"module.json"),"invalid");c["malformed_declaration_isolated_and_redacted"]=Rows().Any(r=>r.Id=="ext:invalid")&&!string.Join(" ",Rows().Select(r=>r.Detail)).Contains(root);
        var next=Rows();string handled="";using(var form=new ModuleStatusForm(prefs,id=>handled=id,()=>next)){form.Show();form.Location=new(-19000,-19000);Application.DoEvents();c["actual_window_renders_sources"]=Walk(form).OfType<Label>().Any(l=>l.Text.Contains("声明式扩展"));var button=Walk(form).OfType<Button>().First(b=>b.Text=="应用配置");button.PerformClick();c["application_button_preserves_existing_handler_id"]=handled==app.Id;
            prefs.DisabledExtensions.Add(extension.Id);next=Rows();typeof(ModuleStatusForm).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,null);c["actual_window_refreshes_disabled_source"]=Walk(form).OfType<Label>().Any(l=>l.Text.Contains("声明式扩展 · v1.1.0 · 已停用"));prefs.DisabledExtensions.Clear();store.Remove(extension.Id);next=Rows();typeof(ModuleStatusForm).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,null);c["removed_extension_disappears_from_window"]=!Walk(form).Any(control=>control.Tag as string=="ext:"+extension.Id);using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(directory,"module-status.png"));form.Close();}
        c["builtin_disabled_and_missing_rules_preserved"]=ModuleStatuses.Resolve("fixture-status", "fixture",false,"missing",()=>false).State==ModuleState.Disabled&&ModuleStatuses.Resolve("fixture-status", "fixture",true,null,()=>false).State==ModuleState.NotInstalled;
        c["builtin_passive_ready_label_preserved"]=ModuleStatuses.Resolve("fixture-status", "fixture",true,program,()=>true).State==ModuleState.Ready;
        long ticket=ModuleActivity.Begin("fixture-status");c["builtin_waiting_preserved"]=ModuleStatuses.Resolve("fixture-status", "fixture",true,null,()=>true).State==ModuleState.Waiting;ModuleActivity.Complete("fixture-status",ticket,ModuleState.Failed,"isolated failure");c["builtin_failure_action_preserved"]=ModuleStatuses.Resolve("fixture-status", "fixture",true,null,()=>true) is{State:ModuleState.Failed,Action:"重新检测"};
        prefs.ApplicationModules.Clear();c["removed_application_disappears"]=!Rows().Any(r=>r.Id==app.Id);c["no_launch_or_payload_diagnostics"]=executed==0&&!string.Join(" ",Rows().Select(r=>r.ToString())).Contains(app.Arguments)&&!ModuleWorker.Running(worker.Id);
    }
}
