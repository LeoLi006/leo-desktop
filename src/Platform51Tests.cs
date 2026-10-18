using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;
static class Platform51Tests
{
    static IEnumerable<Control> Walk(Control c){yield return c;foreach(Control child in c.Controls)foreach(var value in Walk(child))yield return value;}
    static void Show(Form form){form.StartPosition=FormStartPosition.Manual;form.Location=new(-18000,-18000);form.Show();Application.DoEvents();UiTheme.Attach(form);}
    static void Capture(Form form,string path){using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(path);}
    public static void Run(string report)
    {
        using var host=new Form{ShowInTaskbar=false,Opacity=0,StartPosition=FormStartPosition.Manual,Location=new(-20000,-20000)};
        host.Shown+=async(_,_)=>{try{var checks=await Check(Path.GetDirectoryName(report)!);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}finally{host.Close();}};Application.Run(host);
    }
    public static void MediaProbe(string report)
    {
        using var host=new Form{ShowInTaskbar=false,Opacity=0};host.Shown+=async(_,_)=>{try{var windows=await new WindowsMediaProvider().ReadAsync(CancellationToken.None);var pot=await new PotPlayerProvider().ReadAsync(CancellationToken.None);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=true,windows_sessions=windows.Length,potplayer_windows=pot.Length,read_only=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.Message,read_only=true}));}finally{host.Close();}};Application.Run(host);
    }
    static async Task<Dictionary<string,bool>> Check(string dir)
    {
        Directory.CreateDirectory(dir);var checks=new Dictionary<string,bool>();string previousRoot=SkinLibrary.Root;SkinLibrary.Root=Path.Combine(dir,"sample-skins-"+Guid.NewGuid().ToString("N"));
        try{
        var prefs=new Preferences();using var pet=new PetForm(prefs,true,true);Show(pet);Preferences? saved=null;
        using(var settings=new SettingsForm(prefs,nextApply,()=>{},true)){
            Show(settings);var name=Walk(settings).OfType<TextBox>().Single(t=>t.AccessibleName=="伙伴名字");var slider=Walk(settings).OfType<MsnSlider>().Single();
            checks["name_field_is_not_covered_by_label"]=name.Parent!.GetChildAtPoint(new Point(name.Left+15,name.Top+10))==name;
            checks["size_slider_is_not_covered_by_label"]=slider.Parent!.GetChildAtPoint(new Point(slider.Left+50,slider.Top+12))==slider;
            name.Text="Nova";typeof(Control).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(slider,[new MouseEventArgs(MouseButtons.Left,1,slider.Width-12,slider.Height/2,0)]);typeof(Control).GetMethod("OnMouseUp",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(slider,[new MouseEventArgs(MouseButtons.Left,1,slider.Width-12,slider.Height/2,0)]);
            checks["pointer_changes_size_value"]=slider.Value>200;
            Walk(settings).OfType<Button>().Single(b=>b.Text=="保存设置").PerformClick();Application.DoEvents();checks["settings_save_changes_actual_pet_name_and_size"]=saved!=null&&saved.DisplayName=="Nova"&&saved.PetSize>200&&pet.Width==saved.PetSize&&UiTheme.Name=="Nova";
            Capture(settings,Path.Combine(dir,"settings51.png"));settings.Close();
        }
        void nextApply(Preferences next,bool startup){saved=next;pet.ApplyPlatformSettings(next);}
        using(var settings=new SettingsForm(prefs,(_,_)=>{},()=>{},true)){Show(settings);checks["settings_reopen_retains_name_and_size"]=Walk(settings).OfType<TextBox>().Single(t=>t.AccessibleName=="伙伴名字").Text=="Nova"&&Walk(settings).OfType<MsnSlider>().Single().Value==prefs.PetSize;settings.Close();}
        var theme=new ThemeOptions("#E8A244",120,4,82);theme.Validate();prefs.SkinThemes["382"]=theme;prefs.SkinId="382";pet.ApplyPlatformSettings(prefs);using(var panel=new Form{BackColor=BarcaUi.Canvas,Size=new(200,200)}){Appearance.Round(panel,14);Show(panel);checks["theme_opacity_applies_to_windows_not_pet"]=Math.Abs(panel.Opacity-.82)<.01&&Math.Abs(pet.Opacity-1)<.01;checks["custom_accent_is_used"]=BarcaUi.Gold.ToArgb()==System.Drawing.ColorTranslator.FromHtml("#E8A244").ToArgb();panel.Close();}
        var roundtrip=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(prefs))!;checks["theme_is_saved_per_skin"]=roundtrip.SkinThemes["382"]==theme&&!roundtrip.SkinThemes.ContainsKey("83");
        bool invalid=false;try{new ThemeOptions("red",10,99,20).Validate();}catch(ArgumentException){invalid=true;}checks["invalid_theme_rejected"]=invalid;
        UiTheme.Configure("83","Leo");using(var shape=Appearance.Path(new(0,0,100,100),14))checks["round_corners_are_really_rounded"]=!shape.IsVisible(0,0);UiTheme.Configure("83","Leo",new(CornerRadius:0));using(var shape=Appearance.Path(new(0,0,100,100),14))checks["zero_radius_is_a_rectangle"]=shape.IsVisible(1,1);UiTheme.Configure("83","Leo");
        string png=Path.Combine(dir,"jersey-template.png");SkinLibrary.ExportTemplate(png);var skin=SkinLibrary.Import(png,"示例球衣","382");checks["custom_jersey_imports_and_is_selectable"]=SkinLibrary.Find(skin.Id)?.Name=="示例球衣"&&SkinSelection.Valid(skin.Id);
        using(var source=new Bitmap(png))checks["exported_template_has_required_dimensions"]=source.Size==new Size(1024,1024);
        using(var wrong=new Bitmap(256,256)){string path=Path.Combine(dir,"wrong.png");wrong.Save(path);bool denied=false;try{SkinLibrary.Import(path,"错误","83");}catch(ArgumentException){denied=true;}checks["wrong_image_dimensions_rejected"]=denied;}
        prefs.SkinId=skin.Id;pet.ApplyPlatformSettings(prefs);using(var animation=new AnimationSkin(skin.Id)){using var surface=new Bitmap(192,208);using var paint=Graphics.FromImage(surface);animation.Dock.Draw(paint,new(192,208),.8f,DockSide.Right,0);checks["imported_jersey_supports_main_vertical_and_dock_assets"]=animation.Main.Width>0&&animation.Vertical.Width>0&&animation.Bridge!=null&&SkinAnimationTests.Hash(surface).Length>0;}
        using(var editor=new ThemeEditor(prefs,next=>pet.ApplyPlatformSettings(next),true)){Show(editor);Capture(editor,Path.Combine(dir,"theme-editor51.png"));checks["theme_editor_opens_with_custom_skin_preview"]=editor.Visible&&Walk(editor).OfType<ThemeSample>().Any();editor.Close();}
        SkinLibrary.Remove(skin.Id);checks["custom_skin_removes_without_deleting_source_image"]=SkinLibrary.Find(skin.Id)==null&&File.Exists(png);prefs.SkinId="83";pet.ApplyPlatformSettings(prefs);
        ActionDispatcher? cyclic=null;cyclic=new ActionDispatcher(new(),(a,v,t)=>cyclic!.ExecuteAsync(new(a,v,"nested"),t),new ExtensionStore(Path.Combine(dir,"cycle-empty")));bool cycle=false;try{await cyclic.ExecuteAsync(new("todos_open"));}catch(InvalidOperationException){cycle=true;}checks["recursive_combo_calls_fail_instead_of_deadlocking"]=cycle;
        var shutdownDispatcher=new ActionDispatcher(new(),async(a,v,t)=>{await Task.Delay(30000,t);return "late";},new ExtensionStore(Path.Combine(dir,"shutdown-empty")));var shuttingDown=shutdownDispatcher.ExecuteAsync(new("todos_open"));shutdownDispatcher.Dispose();bool lifetimeCanceled=false;try{await shuttingDown;}catch(OperationCanceledException){lifetimeCanceled=true;}checks["dispatcher_disposal_cancels_pending_actions"]=lifetimeCanceled;
        var modulePrefs=new Preferences();var extensions=new ExtensionStore(Path.Combine(dir,"extensions-"+Guid.NewGuid().ToString("N")));int count=0;var requests=new List<(string Action,string Value)>();
        var dispatcher=new ActionDispatcher(modulePrefs,async(a,v,t)=>{Interlocked.Increment(ref count);requests.Add((a,v));await Task.Delay(25,t);return a+":"+v;},extensions);
        var pending1=dispatcher.ExecuteAsync(new("proxy_on","","chat"));var pending2=dispatcher.ExecuteAsync(new("proxy_on","","voice"));await Task.WhenAll(pending1,pending2);checks["simultaneous_cross_entry_requests_coalesce"]=count==1;
        foreach(string source in new[]{"chat","voice","search","combo"})await dispatcher.ExecuteTextAsync("开启系统代理",source);checks["all_four_entries_use_same_action"]=requests.All(r=>r.Action=="proxy_on")&&dispatcher.Receipts.Select(r=>r.Source).Contains("combo");
        modulePrefs.ClashModule=false;int before=count;bool blocked=false;try{await dispatcher.ExecuteTextAsync("开启系统代理","voice");}catch(InvalidOperationException){blocked=true;}checks["disabled_module_blocks_every_entry"]=blocked&&count==before;modulePrefs.ClashModule=true;
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool stopped=false;try{await dispatcher.ExecuteAsync(new("proxy_on"),cancel.Token);}catch(OperationCanceledException){stopped=true;}checks["canceled_request_does_not_execute"]=stopped&&count==before;}
        checks["all_builtin_actions_have_exactly_one_module_owner"]=LeoActions.Names.All(a=>dispatcher.Modules.Count(m=>m.Actions.Contains(a))==1);
        var manifest=new ExtensionManifest("LeoModule/1","sample-plan","示例计划","1.0.0",[new("today","今日计划",["示例今日计划"],"todos_open")]);extensions.Install(manifest);checks["extension_installs_independently"]=extensions.Load().Single().Version=="1.0.0";
        await dispatcher.ExecuteTextAsync("示例今日计划","search");checks["extension_command_routes_to_shared_builtin_action"]=requests.Last().Action=="todos_open";
        extensions.Install(manifest with{Version="1.1.0"});checks["extension_updates_in_place"]=extensions.Load().Length==1&&extensions.Load()[0].Version=="1.1.0";
        bool downgrade=false;try{extensions.Install(manifest);}catch(ArgumentException){downgrade=true;}checks["extension_downgrade_rejected"]=downgrade;
        modulePrefs.DisabledExtensions.Add(manifest.Id);bool off=false;try{await dispatcher.ExecuteTextAsync("示例今日计划","chat");}catch(InvalidOperationException){off=true;}checks["extension_disable_is_enforced"]=off;modulePrefs.DisabledExtensions.Clear();
        bool traverse=false;try{extensions.Install(manifest with{Id="../escape"});}catch(ArgumentException){traverse=true;}checks["extension_path_traversal_rejected"]=traverse;
        bool unknown=false;try{extensions.Install(manifest with{Id="unknown",Commands=[new("run","执行",["危险"],"run_shell","anything")]});}catch(ArgumentException){unknown=true;}checks["extension_cannot_add_arbitrary_shell_actions"]=unknown;
        extensions.Remove(manifest.Id);checks["extension_removal_takes_effect_without_restart"]=!dispatcher.RecognizesExtension("示例今日计划");
        string worker=Path.Combine(dir,"worker.exe");if(!File.Exists(worker))throw new IOException("先构建本地worker.exe测试夹具");
        var workerManifest=new ExtensionManifest("LeoModule/1","sample-worker","示例独立进程模块","1.0.0",[new("hello","执行测试",["独立模块测试"],"worker:hello"),new("slow","慢速测试",["慢速模块测试"],"worker:slow")],"stdio","worker.exe",5);
        string package=Path.Combine(dir,"sample-worker.zip");void Package(ExtensionManifest m){using var archive=new System.IO.Compression.ZipArchive(File.Create(package),System.IO.Compression.ZipArchiveMode.Create);using(var writer=new StreamWriter(archive.CreateEntry("module.json").Open()))writer.Write(JsonSerializer.Serialize(m));using var input=File.OpenRead(worker);using var binary=archive.CreateEntry("worker.exe").Open();input.CopyTo(binary);}
        Package(workerManifest);extensions.InstallPackage(package);string reply=await dispatcher.ExecuteTextAsync("独立模块测试","chat");checks["independent_process_module_executes_json_protocol"]=reply=="worker fixture completed";
        using(var cancel=new CancellationTokenSource()){var slow=dispatcher.ExecuteTextAsync("慢速模块测试","voice",cancel.Token);await Task.Delay(150);bool busy=false;try{extensions.Remove(workerManifest.Id);}catch(InvalidOperationException){busy=true;}checks["running_module_cannot_be_removed"]=busy;cancel.Cancel();bool canceled=false;try{await slow;}catch(OperationCanceledException){canceled=true;}for(int i=0;i<100&&ModuleWorker.Running(workerManifest.Id);i++)await Task.Delay(20);checks["cancel_terminates_worker_and_releases_module"]=canceled&&!ModuleWorker.Running(workerManifest.Id);}
        Package(workerManifest with{Version="1.1.0",TimeoutSeconds=1});extensions.InstallPackage(package);checks["worker_package_updates_independently"]=extensions.Load().Single().Version=="1.1.0";bool timedOut=false;try{await dispatcher.ExecuteTextAsync("慢速模块测试","search");}catch(TimeoutException){timedOut=true;}checks["timeout_terminates_worker_process"]=timedOut&&!ModuleWorker.Running(workerManifest.Id);
        string data=Path.Combine(dir,"module-data",workerManifest.Id,"keep.txt");File.WriteAllText(data,"kept");extensions.Remove(workerManifest.Id);checks["module_removal_preserves_separate_module_data"]=File.Exists(data)&&extensions.Load().Length==0;
        string unsafeZip=Path.Combine(dir,"unsafe-module.zip");using(var archive=new System.IO.Compression.ZipArchive(File.Create(unsafeZip),System.IO.Compression.ZipArchiveMode.Create)){using var writer=new StreamWriter(archive.CreateEntry("../escape.exe").Open());writer.Write("x");}bool zipBlocked=false;try{extensions.InstallPackage(unsafeZip);}catch(ArgumentException){zipBlocked=true;}checks["module_zip_traversal_is_rejected_before_install"]=zipBlocked;
        var command=CommandTranslation.From(QuickRouter.Route("明天待办：取快递"),"search");checks["local_todo_translation_keeps_date"]=command?.Action=="todo_add"&&command.Value.Contains(DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"));
        checks["media_voice_commands_use_unified_actions"]=QuickRouter.Route("下一首").Value=="media_next"&&QuickRouter.Route("暂停播放").Value=="media_pause";
        checks["plain_application_search_stays_plain"]=!LauncherForm.IsCommand("wx")&&!LauncherForm.IsCommand("weixin")&&LauncherForm.IsCommand("开启系统代理");
        using(var combo=new ComboEditor(new ComboStore(null),null,()=>[],true)){Show(combo);checks["combo_editor_offers_unified_commands_and_media"]=Walk(combo).OfType<ComboBox>().Any(c=>c.Items.Cast<object>().OfType<ComboChoice>().Any(i=>i.Value=="command"));combo.Close();}
        var provider=new FakeMedia();var media=new MediaService([provider]);media.SelectedId="fake:two";await media.ExecuteAsync("pause","",CancellationToken.None);checks["media_control_targets_selected_player"]=provider.Last==("fake:two","pause");
        bool gone=false;try{await media.ExecuteAsync("next","fake:gone",CancellationToken.None);}catch(ArgumentException){gone=true;}checks["missing_explicit_player_does_not_control_another"]=gone;
        using(var receiver=new MediaReceiver()){Show(receiver);PotPlayerProvider.Send(receiver.Handle,"pause");checks["potplayer_uses_targeted_native_media_message"]=receiver.Commands.SequenceEqual(new[]{47});receiver.Close();}
        checks["potplayer_command_codes_match_windows_contract"]=PotPlayerProvider.CommandCode("toggle")==14&&PotPlayerProvider.CommandCode("next")==11&&PotPlayerProvider.CommandCode("previous")==12;
        using(var window=new MediaForm((a,v,t)=>Task.FromResult("示例"),true)){Show(window);Capture(window,Path.Combine(dir,"media-control51.png"));checks["media_window_is_themed_and_opens"]=window.Visible&&window.BackColor==BarcaUi.Canvas;window.Close();}
        using(var manager=new ExtensionManager(prefs,true)){Show(manager);Capture(manager,Path.Combine(dir,"extensions51.png"));checks["module_manager_opens"]=manager.Visible;manager.Close();}
        checks["actual_combo_steps_resolve_to_same_actions"]=pet.VerifyPlatformCombo();
        await pet.VerifySearchCommandEntry();checks["actual_search_executes_through_dispatcher"]=pet.PlatformReceipts.Any(r=>r.Source=="search"&&r.Action=="media_toggle");
        await pet.VerifyComboCommandEntry();checks["actual_combo_executes_through_dispatcher"]=pet.PlatformReceipts.Any(r=>r.Source=="combo"&&r.Action=="media_next");
        var chatTools=new AppTools(()=>[],_=>{},leoActions:(a,v,t)=>dispatcher.ExecuteAsync(new(a,v,"chat"),t)){OriginDispatcher=(a,v,source,t)=>dispatcher.ExecuteAsync(new(a,v,source),t)};
        using(var chat=new ChatForm(true,applicationTools:chatTools)){Show(chat);await chat.PreviewSendAsync("开启系统代理");checks["actual_chat_uses_dispatcher"]=dispatcher.Receipts.Last().Action=="proxy_on"&&dispatcher.Receipts.Last().Source=="chat";await chat.PreviewRecognizedAsync("下一首",true);checks["actual_voice_uses_dispatcher_without_model"]=dispatcher.Receipts.Last().Action=="media_next"&&dispatcher.Receipts.Last().Source=="voice";chat.Close();}

        int footballQueries=0;var footballTools=new AppTools(()=>[],_=>{},leoActions:(a,v,t)=>{footballQueries++;return Task.FromResult(JsonSerializer.Serialize(new{data=new TeamData{Matches=[new("sample",DateTimeOffset.Now.AddDays(1),"西甲","83","Barcelona","86","Real Madrid","pre","Scheduled","VS",true)],MatchesUpdated=DateTimeOffset.Now},stale=false}));});using(var facts=new ChatForm(true,applicationTools:footballTools)){Show(facts);await facts.PreviewSendAsync("巴萨下一场什么时候");checks["football_cards_obtain_data_through_shared_action_entry"]=footballQueries==1&&facts.PreviewMatchCards==1;facts.Close();}
        checks["video_voice_aliases_are_local"]=QuickRouter.Route("下一集").Value=="media_next"&&QuickRouter.Route("停止播放").Value=="media_stop";
        var agentTools=new AppTools(()=>[],_=>throw new Exception("不应直接启动应用")){OriginDispatcher=(a,v,source,t)=>dispatcher.ExecuteAsync(new(a,v,source),t)};var openedIds=new HashSet<string>();
        await agentTools.ExecuteAsync(new ToolCall("find","search_apps","{\"query\":\"微信\"}"),openedIds,CancellationToken.None);checks["model_app_search_uses_shared_dispatcher"]=dispatcher.Receipts.Last().Action=="app_search";
        await agentTools.ExecuteAsync(new ToolCall("open","open_app","{\"app_id\":\"fixture-id\"}"),openedIds,CancellationToken.None);checks["model_app_open_uses_shared_dispatcher"]=dispatcher.Receipts.Last().Action=="app_open";
        using(var owned=OwnedHelpers.Start(new System.Diagnostics.ProcessStartInfo(worker){UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden,RedirectStandardInput=true})){using var registration=OwnedHelpers.Lease(owned);owned.StandardInput.AutoFlush=true;owned.StandardInput.WriteLine("{\"command\":\"slow\"}");await Task.Delay(100);OwnedHelpers.Shutdown();checks["shutdown_stops_owned_helpers_without_touching_players"]=owned.WaitForExit(1500);}
        return checks;
        }finally{SkinLibrary.Root=previousRoot;UiTheme.Configure("83","Leo");}
    }
    sealed class FakeMedia:IMediaProvider{public string Id=>"fake";public (string,string) Last;public Task<MediaSession[]> ReadAsync(CancellationToken token)=>Task.FromResult(new[]{new MediaSession("fake:one","QQ音乐","歌曲","","Playing"),new MediaSession("fake:two","PotPlayer","视频","","Paused")});public Task<string> ControlAsync(MediaSession s,string command,CancellationToken token){Last=(s.Id,command);return Task.FromResult("accepted");}}
    sealed class MediaReceiver:Form{public List<int> Commands=[];protected override void WndProc(ref Message m){if(m.Msg==0x319){Commands.Add((int)((long)m.LParam>>16)&0xFFF);m.Result=(IntPtr)1;return;}base.WndProc(ref m);}}
}
sealed partial class PetForm
{
    internal void ApplyPlatformSettings(Preferences next)=>ApplySettings(next,false);
    internal IReadOnlyList<ActionReceipt> PlatformReceipts=>commands.Receipts;
    internal Task VerifySearchCommandEntry()=>launcher.PlatformSearch("播放暂停");
    internal Task VerifyComboCommandEntry()=>BuildComboStep(new("media_next")).Item2();
    internal bool VerifyPlatformCombo()=>ComboRequest(new("todos")).Action=="todos_open"&&ComboRequest(new("media_next")).Action=="media_next"&&ComboRequest(new("url","https://example.com")).Action=="web_open";
}

sealed partial class LauncherForm{internal async Task PlatformSearch(string text){Open();query.Text=text;await RunCommand();}}
