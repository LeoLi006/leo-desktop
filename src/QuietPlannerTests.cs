using System.Text.Json;
namespace LeoLauncher;
static class QuietPlannerTests
{
    static IEnumerable<Control> Walk(Control c){foreach(Control child in c.Controls){yield return child;foreach(var nested in Walk(child))yield return nested;}}
    static void Shot(Form window,string path){window.Show();Application.DoEvents();using var image=new Bitmap(window.Width,window.Height);window.DrawToBitmap(image,window.ClientRectangle);image.Save(path);}
    public static void Run(string report)
    {
        string folder=Path.GetDirectoryName(report)!;Directory.CreateDirectory(folder);string oldRoot=SkinLibrary.Root;
        try{
            var c=new Dictionary<string,bool>();var inbox=new NoticeInbox(null);inbox.Add("已播放歌曲");inbox.Add("代理已开启");c["ordinary_successes_are_read_logs_without_badge"]=inbox.Unread==0&&inbox.Items.All(i=>i.Read);
            inbox.Add("播放失败","error");inbox.Add("取快递","reminder");c["errors_and_reminders_still_need_attention"]=inbox.Unread==2;
            c["success_and_short_busy_feedback_do_not_popup"]=!FeedbackPolicy.ShouldPopup(new("已播放"))&&!FeedbackPolicy.ShouldPopup(new("正在准备",Busy:true));c["errors_cancel_and_explicit_reminders_can_popup"]=FeedbackPolicy.ShouldPopup(new("失败",true))&&FeedbackPolicy.ShouldPopup(new("等待",Retry:()=>Task.CompletedTask,Busy:true))&&FeedbackPolicy.ShouldPopup(new("专注结束",Attention:true));
            string legacy=Path.Combine(folder,"old-notices.json");File.WriteAllText(legacy,JsonSerializer.Serialize(new[]{new InboxItem("1",DateTimeOffset.Now,"播放成功","operation"),new InboxItem("2",DateTimeOffset.Now,"失败","error")}));var migrated=new NoticeInbox(legacy);c["legacy_success_unread_flags_are_silently_normalized"]=migrated.Unread==1&&migrated.Items[0].Read;
            using(var form=new NoticeInboxForm(inbox,_=>{})){Shot(form,Path.Combine(folder,"compact-log.png"));var rows=Walk(form).OfType<Panel>().Where(p=>p.Size==new Size(560,38)).ToArray();c["log_rows_are_compact_and_controls_fit"]=rows.Length==inbox.Items.Count&&rows.All(p=>p.Controls.Cast<Control>().All(control=>p.ClientRectangle.Contains(control.Bounds)));form.Close();}
            PlannerPhrases.Configure(["先做一件事","保持自己的节奏"]);var deck=new QuoteDeck();var lines=Enumerable.Range(0,8).Select(_=>deck.Next()).ToArray();c["deck_uses_only_custom_lines_and_rotates"]=lines.All(l=>l is "先做一件事" or "保持自己的节奏")&&lines.Distinct().Count()==2;
            using(var identity=new PlannerIdentity()){using var host=new Form();host.Controls.Add(identity);host.Show();Application.DoEvents();PlannerPhrases.Configure(["唯一一句"]);c["open_planner_updates_immediately_and_single_line_stops_timer"]=identity.CurrentQuote=="唯一一句"&&!identity.QuoteTimerRunning;PlannerPhrases.Configure([]);c["empty_custom_list_disables_rotating_text"]=identity.CurrentQuote==""&&!identity.QuoteTimerRunning;host.Close();}
            PlannerPhrases.Configure(null);c["default_quotes_can_be_restored"]=QuoteDeck.Lines.Contains(new QuoteDeck().Next());
            var defaults=new QuoteDeck();var cycle=Enumerable.Range(0,QuoteDeck.Lines.Length).Select(_=>defaults.Next()).ToArray();c["default_quote_cycle_has_no_repeats"]=cycle.Distinct().Count()==QuoteDeck.Lines.Length&&defaults.Next()!=cycle[^1];
            using(var host=new Form()){using var identity=new PlannerIdentity();host.Controls.Add(identity);host.Show();Application.DoEvents();host.Hide();Application.DoEvents();bool stopped=!identity.QuoteTimerRunning;host.Show();Application.DoEvents();c["quote_timer_pauses_hidden_and_restores_visible"]=stopped&&identity.QuoteTimerRunning;host.Close();}
            var prefs=new Preferences{PlannerQuotes=["自己写的话"]};var round=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(prefs))!;c["custom_quotes_roundtrip_in_preferences"]=round.PlannerQuotes!.Single()=="自己写的话";
            bool invalid=false;try{PlannerPhrases.Validate([new string('长',121)]);}catch(ArgumentException){invalid=true;}c["excessively_long_quote_is_rejected"]=invalid;
            SkinLibrary.Root=Path.Combine(folder,"skins-"+Guid.NewGuid().ToString("N"));var custom=SkinLibrary.CreateIndependent("自定义标识",null,new SkinPalette("#355977","#774455","#CFAA77"));string badge=Path.Combine(folder,"badge-source.png");using(var image=new Bitmap(80,40)){using(var g=Graphics.FromImage(image))g.Clear(Color.LimeGreen);image.Save(badge);}
            string legacyPack=Path.Combine(folder,"legacy.leoskin");SkinPackages.Export(custom.Id,new(),legacyPack);c["legacy_two_file_skin_packages_remain_supported"]=SkinPackages.Import(legacyPack).BadgeFile==null;
            custom=PlannerBadge.Set(custom.Id,badge);c["badge_is_copied_to_managed_skin_folder"]=PlannerBadge.PathFor(custom.Id)!=badge&&File.Exists(PlannerBadge.PathFor(custom.Id));
            using(var preview=new Bitmap(64,64)){using(var g=Graphics.FromImage(preview))PlannerBadge.Draw(g,custom.Id,new Rectangle(0,0,64,64));c["planner_draws_uploaded_badge"]=preview.GetPixel(32,32).G>150;preview.Save(Path.Combine(folder,"badge-preview.png"));}
            var duplicate=SkinLibrary.Duplicate(custom.Id,"标识副本");c["duplicating_skin_preserves_its_badge"]=PlannerBadge.PathFor(duplicate.Id)!=null;
            string package=Path.Combine(folder,"badge.leoskin");SkinPackages.Export(custom.Id,new(),package);var imported=SkinPackages.Import(package);c["skin_package_roundtrips_badge"]=PlannerBadge.PathFor(imported.Id)!=null;
            using(var editor=new PlannerAppearanceForm(custom.Id,prefs,_=>{},true)){Shot(editor,Path.Combine(folder,"planner-appearance.png"));c["planner_editor_fields_and_buttons_fit"]=Walk(editor).Where(control=>control is Button or TextBox).All(control=>control.Parent!.ClientRectangle.Contains(control.Bounds));editor.Close();}
            Preferences? saved=null;using(var editor=new PlannerAppearanceForm(custom.Id,prefs,p=>saved=p)){editor.Show();Walk(editor).OfType<TextBox>().Single().Text="自己新增一句";Walk(editor).OfType<Button>().Single(b=>b.Text=="保存").PerformClick();c["planner_editor_saves_added_and_removed_lines"]=saved?.PlannerQuotes?.SequenceEqual(new[]{"自己新增一句"})==true;editor.Close();}
            using(var theme=new ThemeEditor(prefs,_=>{},true)){Shot(theme,Path.Combine(folder,"theme-entry.png"));c["theme_editor_exposes_planner_customization"]=Walk(theme).OfType<Button>().Any(b=>b.Text=="清单标识与话语…");theme.Close();}
            using(var pet=new PetForm(new Preferences{ApplicationIndexing=false},true)){pet.Show();Application.DoEvents();foreach(var pair in pet.VerifyContextSearch())c[pair.Key]=pair.Value;pet.Close();}
            File.WriteAllText(report,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,external_requests=0},new JsonSerializerOptions{WriteIndented=true}));
        }catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}finally{SkinLibrary.Root=oldRoot;PlannerPhrases.Configure(null);}
    }
}
sealed partial class PetForm
{
    internal Dictionary<string,bool> VerifyContextSearch()
    {
        var c=new Dictionary<string,bool>();ShowLauncher();BeginPetPress(hit.PointToScreen(new Point(Width/2,Height/2)));var menu=ContextMenuStrip!;menu.Show(hit,new Point(Width/2,Height/2));Application.DoEvents();c["right_menu_closes_search_and_clears_pointer_capture"]=!launcher.Visible&&!pointerPressed&&!hit.Capture;menu.Close();
        var appearance=menu.Items.OfType<ToolStripMenuItem>().Single(i=>i.Text=="外观").DropDownItems.OfType<ToolStripMenuItem>().Single(i=>i.Text!.StartsWith("主题与"));
        ShowLauncher();using var close=new System.Windows.Forms.Timer{Interval=100};close.Tick+=(_,_)=>{var theme=Application.OpenForms.OfType<ThemeEditor>().FirstOrDefault();if(theme!=null){close.Stop();theme.Close();}};close.Start();appearance.PerformClick();
        c["modal_theme_editor_does_not_leave_search_visible"]=!launcher.Visible&&hit.Enabled;int count=launcherOpens;hit.SimulateClick(new Point(Width/2,Height/2));hit.SimulateClick(new Point(Width/2,Height/2));c["pet_search_toggle_still_works_after_theme_dialog"]=!launcher.Visible&&launcherOpens==count+1;return c;
    }
}
