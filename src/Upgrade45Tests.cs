using System.Security.Cryptography;
using System.Text.Json;
namespace LeoLauncher;
static class Upgrade45Tests
{
    public static Dictionary<string,bool> Check(string output)
    {
        Directory.CreateDirectory(output);var checks=new Dictionary<string,bool>();var now=new DateTime(2026,10,18,10,0,0);var today=DateOnly.FromDateTime(now);
        var draft=NaturalTodo.Parse("明天下午三点取快递",now,today);
        checks["local_tomorrow_afternoon_and_title"]=draft is {Title:"取快递",Time:var time}&&time==new TimeOnly(15,0)&&draft.Day==today.AddDays(1)&&draft.Frequency=="";
        draft=NaturalTodo.Parse("每周六晚上踢球",now,today);
        checks["weekly_saturday_suggested_time_requires_confirmation"]=draft is {Title:"踢球",Frequency:"weekly",Estimated:true}&&draft.Day.DayOfWeek==DayOfWeek.Saturday&&draft.Time==new TimeOnly(20,0);
        checks["noon_and_late_midnight_are_distinct"]=NaturalTodo.Parse("明天上午十二点吃饭",now,today)?.Time==new TimeOnly(12,0)&&NaturalTodo.Parse("明天晚上十二点睡觉",now,today) is {Day:var midnightDay,Time:var midnight}&&midnightDay==today.AddDays(2)&&midnight==TimeOnly.MinValue;
        checks["daily_passed_occurrence_moves_forward"]=NaturalTodo.Parse("每天早上八点运动",now,today)?.Day==today.AddDays(1);
        checks["clock_without_date_uses_selected_day"]=NaturalTodo.Parse("15:30开会",now,today.AddDays(3)) is {Day:var date,Time:var at}&&date==today.AddDays(3)&&at==new TimeOnly(15,30);
        checks["date_and_chinese_minutes"]=NaturalTodo.Parse("2026-10-23晚上八点三十分训练",now,today) is {Title:"训练",Time:var evening}&&evening==new TimeOnly(20,30);
        checks["plain_title_and_invalid_time_are_not_silently_rescheduled"]=NaturalTodo.Parse("取快递",now,today)==null&&NaturalTodo.Parse("明天25:90取快递",now,today)==null&&NaturalTodo.Parse("明天",now,today)==null;
        var store=new TodoStore(null);var confirmed=NaturalTodo.Parse("每周六晚上踢球",now,today)!;int before=store.Items.Count;using(var dialog=new NaturalTodoForm(confirmed)){checks["opening_confirmation_does_not_save"]=store.Items.Count==before;Snapshot(dialog,Path.Combine(output,"natural-todo45.png"));}
        var task=store.AddRepeating(confirmed.Title,confirmed.Day,confirmed.Frequency,new DateTimeOffset(confirmed.Day.ToDateTime(confirmed.Time!.Value)));store.EnsureThrough(confirmed.Day.AddDays(14));checks["confirmed_weekly_rule_produces_correct_reminders"]=store.Items.Where(t=>t.SeriesId==task.SeriesId).All(t=>t.Day.DayOfWeek==DayOfWeek.Saturday&&t.Reminder?.LocalDateTime.TimeOfDay==new TimeSpan(20,0,0));
        var area=new Rectangle(0,0,1920,1080);var corner=new Rectangle(0,0,130,140);checks["corner_follows_drag_axis"]=EdgeDock.ChooseSide(corner,area,30,new Size(-40,-2))==DockSide.Left&&EdgeDock.ChooseSide(corner,area,30,new Size(-2,-40))==DockSide.Top;
        using(var request=Modules.FootballRequest(CancellationToken.None)){Modules.SetFootball(false);bool blocked=false;try{Modules.RequireFootball();}catch(InvalidOperationException){blocked=true;}checks["football_off_cancels_requests_and_blocks_entry"]=request.IsCancellationRequested&&blocked;Modules.SetFootball(true);}
        checks["football_module_action_gate"]=!Modules.Enabled(new Preferences{FootballModule=false},Modules.ForAction("football_open")!)&&Modules.ForAction("match_add")=="football";
        using(var form=new FirstRunForm(new Preferences()))Snapshot(form,Path.Combine(output,"first-run45.png"));using(var setup=new InstallForm())Snapshot(setup,Path.Combine(output,"installer45.png"));
        using(var settings=new SettingsForm(new Preferences(),(_,_)=>{},()=>{},true)){settings.Show();Application.DoEvents();var tab=Descendants(settings).OfType<BarcaButton>().First(b=>b.Text=="功能");tab.PerformClick();Application.DoEvents();Snapshot(settings,Path.Combine(output,"modules45.png"));tab=Descendants(settings).OfType<BarcaButton>().First(b=>b.Text=="运行检查");tab.PerformClick();Application.DoEvents();Snapshot(settings,Path.Combine(output,"health45.png"));}
        Installer(checks,output);Recovery(checks,output);return checks;
    }
    sealed class FailingConnection:System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken token)=>Task.FromException<System.Net.Http.HttpResponseMessage>(new System.Net.Http.HttpRequestException("simulated offline"));
    }
    internal static void Recovery(Dictionary<string,bool> checks,string output)
    {
        void Pump(Task task){var watch=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted&&watch.ElapsedMilliseconds<8000){Application.DoEvents();Thread.Sleep(5);}if(!task.IsCompleted)throw new TimeoutException("recovery test");task.GetAwaiter().GetResult();}
        int searches=0;using(var music=new MusicForm(true,(_,_)=>{searches++;if(searches==2)throw new System.Net.Http.HttpRequestException("offline");return Task.FromResult(new[]{new MusicSong("123","songmid","本地样例","歌手","")});})){
            music.Show();Application.DoEvents();Pump(music.SearchAsync("test"));Pump(music.SearchAsync("test"));checks["music_failure_keeps_results_and_offers_retry"]=music.Candidates.Length==1&&music.SearchRetryVisible&&music.SearchStatusFits;Snapshot(music,Path.Combine(output,"music-retry45.png"));music.Show();Pump(music.SearchAsync("test"));checks["music_retry_recovers_independently"]=music.Candidates.Length==1&&!music.SearchRetryVisible;music.Hide();}
        int attempts=0;using var chat=new ChatForm(true,ChatTests.Settings(),()=>++attempts==1?new ChatApi(new FailingConnection()):ChatTests.FixtureApi());chat.Show();Application.DoEvents();Pump(chat.PreviewSendAsync("你好啊"));checks["chat_failure_releases_composer_and_offers_retry"]=chat.ReplyRetryAvailable;Snapshot(chat,Path.Combine(output,"chat-retry45.png"));chat.Show();chat.PreviewRetryReply();var timer=System.Diagnostics.Stopwatch.StartNew();while(!chat.PreviewReplyComplete&&timer.ElapsedMilliseconds<8000){Application.DoEvents();Thread.Sleep(5);}checks["chat_retry_streams_successfully_after_failure"]=chat.PreviewReplyComplete&&attempts==2;
    }
    static IEnumerable<Control> Descendants(Control control){foreach(Control child in control.Controls){yield return child;foreach(var inner in Descendants(child))yield return inner;}}
    static void Snapshot(Form window,string file){window.Show();Application.DoEvents();using var image=new Bitmap(window.Width,window.Height);window.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(file);window.Hide();}
    static void Installer(Dictionary<string,bool> checks,string output)
    {
        string root=Path.Combine(output,"install45-"+Guid.NewGuid().ToString("N")),source=Path.Combine(root,"source"),target=Path.Combine(root,"target");Directory.CreateDirectory(source);
        void Release(string version){var manifest=new ReleaseManifest{Version=version};foreach(string name in new[]{"LeoLauncher.exe","LeoLauncher.dll","coreclr.dll","System.Windows.Forms.dll"}){File.WriteAllText(Path.Combine(source,name),version+name);manifest.Files[name]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(source,name))));}File.WriteAllText(Path.Combine(source,LeoInstaller.ManifestName),JsonSerializer.Serialize(manifest));}
        Release("one");LeoInstaller.Install(source,target,stop:false);File.WriteAllText(Path.Combine(target,"leo-settings.json"),"personal configuration");Release("two");string backup=LeoInstaller.Install(source,target,stop:false);
        checks["update_keeps_config_and_backs_it_up"]=File.ReadAllText(Path.Combine(target,"leo-settings.json"))=="personal configuration"&&File.ReadAllText(Path.Combine(backup,"config","leo-settings.json"))=="personal configuration"&&File.ReadAllText(Path.Combine(target,"LeoLauncher.dll"))=="twoLeoLauncher.dll";
        Release("three");int copies=0;bool failed=false;try{LeoInstaller.Install(source,target,copy:(a,b)=>{if(++copies==2)throw new IOException("simulated failure");File.Copy(a,b,true);},stop:false);}catch(IOException){failed=true;}
        checks["failed_update_restores_all_runtime_and_preserves_data"]=failed&&File.ReadAllText(Path.Combine(target,"LeoLauncher.dll"))=="twoLeoLauncher.dll"&&File.ReadAllText(Path.Combine(target,"LeoLauncher.exe"))=="twoLeoLauncher.exe"&&File.ReadAllText(Path.Combine(target,"leo-settings.json"))=="personal configuration";
        File.WriteAllText(Path.Combine(target,"leo-settings.json"),"new personal settings");LeoInstaller.Restore(target,backup,stop:false);checks["manual_rollback_keeps_newer_personal_settings"]=File.ReadAllText(Path.Combine(target,"LeoLauncher.dll"))=="oneLeoLauncher.dll"&&File.ReadAllText(Path.Combine(target,"leo-settings.json"))=="new personal settings";
        File.WriteAllText(Path.Combine(source,"coreclr.dll"),"tampered");bool rejected=false;try{LeoInstaller.Install(source,target,stop:false);}catch(IOException){rejected=true;}checks["corrupt_package_rejected_before_touching_install"]=rejected&&File.ReadAllText(Path.Combine(target,"LeoLauncher.dll"))=="oneLeoLauncher.dll";
        rejected=false;try{LeoInstaller.Inside(target,"../outside.txt");}catch(IOException){rejected=true;}checks["path_escape_and_personal_payload_rejected"]=rejected&&!LeoInstaller.Allowed("leo-ai.json")&&!LeoInstaller.Allowed("data/LeoLauncher.exe");
        File.WriteAllText(Path.Combine(backup,"runtime","LeoLauncher.dll"),"corrupt backup");rejected=false;try{LeoInstaller.Restore(target,backup,stop:false);}catch(IOException){rejected=true;}checks["damaged_backup_rejected_before_writes"]=rejected&&File.ReadAllText(Path.Combine(target,"LeoLauncher.exe"))=="oneLeoLauncher.exe";
    }
}
