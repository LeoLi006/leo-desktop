using System.Reflection;
using System.Net.Http;
using System.Text;
using System.Text.Json;
namespace LeoLauncher;

static class Feature40Tests
{
    static IEnumerable<Control> All(Control root){yield return root;foreach(Control c in root.Controls)foreach(var child in All(c))yield return child;}
    sealed class Handler:HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent("{\"choices\":[{\"message\":{\"content\":\"完成\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":7,\"completion_tokens\":3}}",Encoding.UTF8,"application/json")});}
    }
    public static Dictionary<string,bool> Check(string directory)
    {
        var checks=new Dictionary<string,bool>();var today=DateOnly.FromDateTime(DateTime.Today);var now=DateTimeOffset.Now;
        string root=Path.Combine(directory,"feature40-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var data=new TodoStore(Path.Combine(root,"todos.json"));var task=data.Add("原始标题",today,now.AddHours(2));
        data.Rename(task.Id,"修改标题");data.Undo();checks["undo_title_preserves_reminder"]=data.Items.Single()==task;
        data.MoveTo(task.Id,today.AddDays(2));data.Undo();checks["undo_move_restores_day_and_reminder"]=data.Items.Single()==task;
        data.Toggle(task.Id);data.Undo();checks["undo_completion_persists"]=new TodoStore(Path.Combine(root,"todos.json")).Items.Single()==task;
        data.Delete(task.Id);data.Undo();checks["undo_deleted_task_restores_it"]=data.Items.Single()==task;
        data.MarkNotified([task.Id],now);bool conflict=false;try{data.Undo();}catch(InvalidOperationException){conflict=true;}checks["undo_cannot_overwrite_later_notification"]=conflict&&data.Items.Single().Notified==now;
        var recurring=new TodoStore(Path.Combine(root,"repeat.json"));var first=recurring.AddRepeating("运动",today,"daily",now.AddHours(2));recurring.SkipOccurrence(first.Id);recurring.EnsureThrough(today.AddDays(45));
        checks["skipped_occurrence_stays_skipped_after_generation_and_restart"]=new TodoStore(Path.Combine(root,"repeat.json")).Items.All(t=>t.Id!=first.Id);
        recurring.Undo();checks["undo_skip_keeps_later_generated_occurrences"]=recurring.Items.Any(t=>t.Id==first.Id)&&recurring.Items.Any(t=>t.Day==today.AddDays(45));
        recurring.Rename(first.Id,"本次运动");recurring.MoveTo(first.Id,today.AddDays(2));recurring.EnsureThrough(today.AddDays(48));
        checks["one_occurrence_edit_does_not_change_series"]=recurring.Items.Single(t=>t.Id==first.Id).Title=="本次运动"&&recurring.Items.Any(t=>t.Day==today.AddDays(1)&&t.Title=="运动")&&recurring.Items.Count(t=>t.SeriesId==first.SeriesId&&t.OccurrenceDay==today)==1;
        var grouped=new TodoStore(null);var old=grouped.Add("逾期任务",today.AddDays(-2));grouped.Add("今日任务",today);var done=grouped.Add("已完成旧任务",today.AddDays(-1));grouped.Toggle(done.Id);
        checks["overdue_group_contains_only_unfinished_past_tasks"]=grouped.Overdue(today).Single().Id==old.Id;
        using(var planner=new TodoForm(true,grouped)){
            planner.Show();Application.DoEvents();checks["daily_view_does_not_mix_overdue_tasks"]=!All(planner).OfType<Label>().Any(l=>l.Text==old.Title);
            All(planner).OfType<Button>().Single(b=>b.Text.StartsWith("逾期 ")).PerformClick();Application.DoEvents();checks["overdue_tab_displays_old_tasks"]=All(planner).OfType<Label>().Any(l=>l.Text==old.Title);
            Capture(planner,"preview-overdue40.png",directory);
        }
        var clip=new ClipHistory(null);var clipItem=new ClipItem("text","收藏内容","收藏内容",now,true);clip.Add(clipItem);clip.Delete(clipItem);clip.Add(new("text","后来复制","后来复制",now));clip.Undo();checks["clipboard_undo_preserves_new_capture"]=clip.Items.Any(i=>i.Key==clipItem.Key&&i.Pinned)&&clip.Items.Any(i=>i.Content=="后来复制");
        string usageFile=Path.Combine(root,"usage.json");var ledger=new ApiUsageLedger(usageFile);using(var ticket=ledger.Begin(1,now)){using var doc=JsonDocument.Parse("{\"usage\":{\"prompt_tokens\":7,\"completion_tokens\":3}}");ticket.Usage(doc.RootElement);ticket.Completed=true;}
        bool blocked=false;try{ledger.Begin(1,now);}catch(InvalidOperationException){blocked=true;}
        checks["daily_api_limit_is_persistent_and_counts_reported_tokens"]=blocked&&new ApiUsageLedger(usageFile).Today(now) is {Requests:1,Completed:1,InputTokens:7,OutputTokens:3};
        using(ledger.Begin(1,now.AddDays(1))){}checks["api_limit_resets_on_local_day_change"]=ledger.Today(now.AddDays(1)) is {Requests:1,WithoutTokens:1};
        var handler=new Handler();var apiLedger=new ApiUsageLedger(null);using(var api=new ChatApi(handler,apiLedger)){api.StreamAsync(ChatTests.Settings(),ChatTests.FixtureKey,[new("user","测试")],_=>{},CancellationToken.None).GetAwaiter().GetResult();}
        checks["api_http_path_records_usage_once"]=handler.Calls==1&&apiLedger.Today() is {Requests:1,Completed:1,InputTokens:7,OutputTokens:3};
        var full=new ApiUsageLedger(null);for(int i=0;i<100;i++)using(full.Begin(0)){}handler=new Handler();using(var api=new ChatApi(handler,full)){try{api.StreamAsync(ChatTests.Settings(),ChatTests.FixtureKey,[new("user","不要发送")],_=>{},CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidOperationException){}}
        checks["budget_blocks_before_any_http_request"]=handler.Calls==0;
        string sse="data: {\"choices\":[{\"delta\":{\"content\":\"好\"},\"finish_reason\":\"stop\"}]}\n\ndata: {\"choices\":[],\"usage\":{\"prompt_tokens\":11,\"completion_tokens\":2}}\n\ndata: [DONE]\n\n";
        using(var ticket=new ApiUsageLedger(null).Begin(0)){using var stream=new MemoryStream(Encoding.UTF8.GetBytes(sse));ChatApi.ReadSseAsync(stream,false,_=>{},CancellationToken.None,false,ticket.Usage).GetAwaiter().GetResult();checks["stream_usage_chunk_after_finish_is_not_lost"]=ticket.Reported&&ticket.Input==11&&ticket.Output==2;}
        var prefs=new Preferences{TerminalModule=false,ClashModule=false,MusicModule=false,EdgeModule=false};
        using(var footer=new FootTools(130,true)){footer.ConfigureModules(prefs);footer.Show();footer.PreviewUtilities();Application.DoEvents();checks["disabled_footer_modules_hide_icons_and_close_gaps"]=!All(footer).Any(c=>c.Visible&&c.AccessibleName is "打开管理员终端" or "Clash Verge 系统代理" or "展开 Edge 搜索")&&footer.Width==94;}
        checks["disabled_music_does_not_register_lyrics_hotkey"]=LeoHotkeys.Values(prefs)[2]==Keys.None;
        bool gated=false;try{Modules.Require(prefs,Modules.ForAction("music_play"));}catch(InvalidOperationException){gated=true;}checks["disabled_module_blocks_tool_action"]=gated;
        var catalog=Enumerable.Range(0,14).Select(i=>new AppEntry("测试应用"+i,"fixture-app-"+i)).ToList();AppEntry? launched=null;
        using(var launcher=new LauncherForm(new Preferences{RememberHistory=false,Favorites=catalog.Select(a=>a.Target).ToList()},()=>{},true,false,appLauncher:app=>launched=app)){
            launcher.Show();var watch=System.Diagnostics.Stopwatch.StartNew();while(launcher.IsIndexing&&watch.ElapsedMilliseconds<5000){Application.DoEvents();Thread.Sleep(5);}
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(LauncherForm).GetField("apps",flags)!.SetValue(launcher,catalog);launcher.Open();
            var query=(TextBox)typeof(LauncherForm).GetField("query",flags)!.GetValue(launcher)!;
            void Key(Keys key)=>typeof(Control).GetMethod("OnKeyDown",flags)!.Invoke(query,[new KeyEventArgs(key)]);
            int Selection()=>(int)typeof(LauncherForm).GetField("selection",flags)!.GetValue(launcher)!;
            Key(Keys.Right);Key(Keys.Down);checks["search_keyboard_navigates_seven_column_grid"]=Selection()==8;
            Key(Keys.Up);Key(Keys.Left);checks["search_keyboard_can_return_to_first_result"]=Selection()==0;
            query.Text="测试";query.SelectionStart=1;Key(Keys.Left);checks["search_left_right_preserves_text_editing_before_navigation"]=!(bool)typeof(LauncherForm).GetField("keyboardNavigation",flags)!.GetValue(launcher)!;
            query.Clear();Key(Keys.Down);Key(Keys.Enter);checks["search_enter_launches_selected_app_without_mouse"]=launched?.Target==catalog[7].Target&&!launcher.Visible;
            launcher.Open();EscapeWindows.Dismiss(launcher);checks["escape_hides_launcher_without_disposing_it"]=!launcher.Visible&&!launcher.IsDisposed;
        }
        using(var settings=new SettingsForm(new(),(_,_)=>{},()=>{},true)){
            settings.Show();Application.DoEvents();foreach(string tab in new[]{"功能","运行检查","用量"}){All(settings).OfType<Button>().Single(b=>b.Text==tab).PerformClick();Application.DoEvents();Capture(settings,"preview-settings-"+tab+"40.png",directory);}
            checks["new_settings_tabs_are_visible"]=All(settings).OfType<Button>().Where(b=>b.Text is "功能" or "运行检查" or "用量").All(b=>b.Parent!.ClientRectangle.Contains(b.Bounds));
            All(settings).OfType<Button>().Single(b=>b.Text=="API").PerformClick();Application.DoEvents();var keyField=All(settings).OfType<TextBox>().Single(t=>t.UseSystemPasswordChar);
            var escape=Message.Create(keyField.Handle,0x100,(IntPtr)(int)Keys.Escape,IntPtr.Zero);new EscapeWindows().PreFilterMessage(ref escape);checks["escape_closes_parent_window_from_embedded_api_page"]=settings.IsDisposed;
        }
        var area=new Rectangle(-1600,0,1600,900);checks["dock_snaps_on_negative_coordinate_monitor"]=EdgeDock.Snap(new(-1590,300,130,140),area,64)==new Point(-1600,300);
        checks["dock_leaves_far_positions_unchanged"]=EdgeDock.Snap(new(-900,300,130,140),area,64)==new Point(-900,300);
        checks["dock_bottom_reserves_footer"]=EdgeDock.Snap(new(-900,690,130,140),area,64).Y==696;
        var next=new FootballMatch("countdown",now.AddMinutes(95),"esp.1","83","Barcelona","86","Real Madrid","pre","Scheduled","VS",true);
        checks["match_day_countdown_uses_local_date"]=LeoSummary.Build([], [next],now).Contains(DateOnly.FromDateTime(next.Kickoff.LocalDateTime)==today?"距开球 1 小时 35 分钟":"下一场关注比赛");
        return checks;
    }
    static void Capture(Form form,string file,string directory){using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(directory,file));}
}
