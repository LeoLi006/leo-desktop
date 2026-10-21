using System.Text.Json;
namespace LeoLauncher;
static partial class ModuleEntryRenderingTests
{
    // Test-only second module: same limited notes service, distinct registered identities.
    sealed class Peer(IExampleNotesServices services):IStructuredLeoModule,IActionRequestAdapter,IModuleContractSource,IModuleEntryContributor
    {
        readonly ScopedNotesExampleModule inner=new(services);
        public string Id=>"closure-peer";public string Name=>"隔离对照";public string Version=>"1";
        public bool Enabled=>services.Configuration.Enabled;public bool ConfigurationIsCurrent=>true;
        public IReadOnlyList<string> Actions=>["closure_peer_query"];public int MaximumInputLength=>12000;
        public string Title="对照查询",Icon="",Mode=EntryDisplay.Menu,Text="",Alias="对照简称";public bool Broken;public int Calls;
        public DependencyState[] ReadDependencies()=>services.Dependencies();
        public CapabilityDefinition Describe(string action)=>new(action,Id,"in-process","text-object","read","serialized-store",5000);
        public ModuleContract DescribeModule()=>ModuleContracts.Build(this,"example",Name,"optional","host.scoped-example-notes",[],_ =>("object","text字符串","notes-example","私有快照",true,["snapshot"]));
        public EntryContribution[] DescribeEntries(){if(Broken)throw new IOException("isolated provider");return [new(Id+"/query",Id,Actions[0],Title,Icon,true,Mode,ModuleEntryContributions.TargetPrefix+Id+"/query",[Alias],"module-action"){Parameters=JsonSerializer.Serialize(new{text=Text}),Glyph="notes",MenuActions=[new(Title,Actions[0],JsonSerializer.Serialize(new{text=Text}))]}];}
        public ActionRequest Adapt(StructuredActionRequest request){var adapted=inner.Adapt(request with{ActionId="example_notes_query"});return adapted with{Action=Actions[0]};}
        public Task<ActionPayload> ExecuteStructuredAsync(ActionRequest request,CancellationToken token){Calls++;return inner.ExecuteStructuredAsync(request with{Action="example_notes_query"},token);}
        public async Task<string> ExecuteAsync(ActionRequest request,CancellationToken token)=>(await ExecuteStructuredAsync(request,token)).Message;
        public string FailureCode(Exception e)=>inner.FailureCode(e);
    }
    static async Task CheckClosure(string root,Dictionary<string,bool> c)
    {
        var p=new Preferences{ApplicationIndexing=false};using var pet=new PetForm(p,true,true);var bus=Field<ActionDispatcher>(pet,"commands");var launcher=Field<LauncherForm>(pet,"launcher");
        var aConfig=new ExampleNotesConfiguration(true);var bConfig=new ExampleNotesConfiguration(true);
        var aStore=new NoteStore(null);var bStore=new NoteStore(null);aStore.Save(null,"first");bStore.Save(null,"second");
        using var aService=new ScopedNotesExampleServices(aStore,()=>aConfig);using var bService=new ScopedNotesExampleServices(bStore,()=>bConfig);
        var a=new ScopedNotesExampleModule(aService);var b=new Peer(bService);bus.Registry.Register(a);bus.Registry.Register(b);
        const string aid="example-scoped-notes/query",bid="closure-peer/query";string bt=ModuleEntryContributions.TargetPrefix+bid;
        var menu=pet.ContextMenuStrip!;
        ToolStripMenuItem[] Menus(){OpenMenu(menu);return menu.Items.OfType<ToolStripMenuItem>().Single(i=>i.Text=="应用与媒体").DropDownItems.OfType<ToolStripMenuItem>().Single(i=>i.Text=="模块入口").DropDownItems.OfType<ToolStripMenuItem>().ToArray();}
        Button Button(string id)=>Walk(launcher).OfType<Button>().Single(v=>v.Tag as string==id);
        launcher.Open();launcher.Location=new(-19000,-19000);launcher.RefreshModules();
        c["closure_two_scoped_modules_share_directory"]=EntryContributions.ReadModules(p).Entries.Length==2&&Menus().Length==2;
        c["provider_default_menu_respected"]=!Walk(launcher).OfType<Button>().Any(v=>v.Tag as string==bid)&&EntryDisplay.EffectiveMode(p,bid)==EntryDisplay.Menu;
        using(var settings=new EntryDisplayForm(p))c["settings_preserve_provider_default"]=Walk(settings).OfType<ComboBox>().Single(v=>v.AccessibleName==bid+"入口显示").SelectedItem is ComboChoice{Value:EntryDisplay.Menu};
        using(var modules=new ModulesView(p,true)){
            var draft=modules.EntryDisplayDraft();using var settings=new EntryDisplayForm(draft);settings.Show();settings.Location=new(-19000,-19000);
            foreach(var combo in Walk(settings).OfType<ComboBox>().Where(v=>v.AccessibleName==bid+"入口显示"))combo.SelectedItem=combo.Items.Cast<ComboChoice>().Single(v=>v.Value==EntryDisplay.Shortcut);
            ((Button)settings.AcceptButton!).PerformClick();
            c["draft_saves_namespaced_mode_without_applying"]=settings.Result.GetValueOrDefault(bid)==EntryDisplay.Shortcut&&!p.EntryDisplayModes.ContainsKey(bid);
            var next=LocalApplications.Clone(p);next.EntryDisplayModes=new(settings.Result);pet.ApplyPlatformSettings(next);
        }
        c["applied_draft_restores_provider_button"]=Button(bid).AccessibleName==b.Title;
        p.ShortcutOrder=[bid,aid,"music","football","media"];launcher.RefreshModules();
        var row=Field<FlowLayoutPanel>(launcher,"shortcutRow");c["mixed_shortcut_order_preserved"]=row.Controls.Cast<Control>().Select(v=>v.Tag as string).Take(2).SequenceEqual(new[]{bid,aid});
        string red=Path.Combine(root,"closure-red.png"),blue=Path.Combine(root,"closure-blue.png");
        foreach(var pair in new[]{(red,Color.Red),(blue,Color.Blue)}){using var bitmap=new Bitmap(8,8);using var graphics=Graphics.FromImage(bitmap);graphics.Clear(pair.Item2);bitmap.Save(pair.Item1);}
        b.Icon=red;launcher.RefreshModules();var oldButton=Button(bid);c["provider_icon_reaches_actual_button"]=oldButton.Image is Bitmap icon&&icon.GetPixel(2,2).R>200;
        var stale=Field<Dictionary<string,(string Title,Action Run)>>(launcher,"shortcutActions")[bid].Run;
        b.Icon=blue;b.Title="更名对照";b.Alias="更新简称";b.Text="second";launcher.RefreshModules();
        c["rename_and_icon_refresh_without_new_identity"]=oldButton.IsDisposed&&Button(bid).AccessibleName==b.Title&&Button(bid).Image is Bitmap image&&image.GetPixel(2,2).B>200&&Menus().Single(v=>v.Tag as string==bid).Text==b.Title;
        launcher.PreviewTyping(b.Alias);await Until(()=>Walk(launcher).Any(v=>v.Tag is AppEntry app&&app.Target==bt));
        c["changed_alias_and_name_reach_search"]=Walk(launcher).OfType<Button>().Single(v=>v.Tag is AppEntry app&&app.Target==bt).AccessibleName==b.Title;
        var result=await EntryContributions.ExecuteModule(p,bid,"closure");
        var aResult=await EntryContributions.ExecuteModule(p,aid,"closure");c["distinct_services_return_only_own_data"]=result.Data!.Value.GetRawText().Contains("second")&&!result.Data.Value.GetRawText().Contains("first")&&aResult.Data!.Value.GetRawText().Contains("first")&&!aResult.Data.Value.GetRawText().Contains("second")&&aStore.Items.Count==1&&bStore.Items.Count==1;
        b.Text="missing";int before=b.Calls;stale();await Until(()=>b.Calls>before);c["cached_callback_reads_current_parameters"]=bus.RecentResults.Last(r=>r.ModuleId==b.Id).Status=="succeeded"&&(await EntryContributions.ExecuteModule(p,bid,"closure")).Data!.Value.GetArrayLength()==0;
        b.Broken=true;launcher.RefreshModules();c["provider_fault_isolates_real_views"]=EntryContributions.ReadModules(p).Entries.Single().Id==aid&&Menus().Length==1&&!Walk(launcher).OfType<Button>().Any(v=>v.Tag as string==bid);
        c["peer_service_continues_after_provider_fault"]=(await EntryContributions.ExecuteModule(p,aid,"closure")).Status=="succeeded";b.Broken=false;b.Text="second";
        bConfig=bConfig with{Enabled=false};bus.CancelDisabled();launcher.RefreshModules();c["disable_keeps_mode_and_order"]=p.EntryDisplayModes[bid]==EntryDisplay.Shortcut&&p.ShortcutOrder[0]==bid&&EntryContributions.ReadModules(p).Search.All(e=>e.Id!=bid);
        before=b.Calls;stale();await Task.Delay(30);c["disabled_cached_callback_does_not_execute"]=b.Calls==before;
        bConfig=bConfig with{Enabled=true};launcher.RefreshModules();c["reenable_restores_current_name_icon_and_order"]=Button(bid).AccessibleName==b.Title&&row.Controls[0].Tag as string==bid&&Button(bid).Image!=null;
        var held=Menus().Single(v=>v.Tag as string==bid);bus.Registry.Unregister(b);bus.CancelDisabled();bService.Dispose();before=b.Calls;held.PerformClick();stale();await Task.Delay(30);
        c["removed_controls_do_not_call_disposed_service"]=b.Calls==before;
        launcher.RefreshModules();c["removal_preserves_peer_and_display_preferences"]=EntryContributions.ReadModules(p).Entries.Single().Id==aid&&p.EntryDisplayModes.ContainsKey(bid)&&(await EntryContributions.ExecuteModule(p,aid,"closure")).Status=="succeeded";
        bool disposed=false;try{await bService.Query("",CancellationToken.None);}catch(Exception e)when(e is ObjectDisposedException or OperationCanceledException or InvalidOperationException){disposed=true;}c["host_explicitly_releases_removed_service"]=disposed;
        bus.Registry.Unregister(a);bus.CancelDisabled();aService.Dispose();launcher.RefreshModules();c["closure_all_entries_released_without_data_deletion"]=EntryContributions.ReadModules(p).Entries.Length==0&&aStore.Items.Count==1&&bStore.Items.Count==1;
        c["closure_no_automatic_model_tools"]=!new BrainCatalog(bus.Registry).Read().Any(v=>v.Module is "closure-peer" or "example-scoped-notes");launcher.Hide();
    }
}
