namespace LeoLauncher;
sealed partial class ChatForm
{
    public int PreviewMatchCards=>results.Controls.OfType<MatchCard>().Count();
    public bool PreviewOptionsHidden=>!options.Visible;
    public int PreviewTableRows=>results.Controls.OfType<StandingsLine>().Count();
    public string PreviewState=>state.Text;
    public void PreviewHome()=>ShowHome();

}
static class QuickTests
{
    public static Dictionary<string,bool> Check(string directory)
    {
        using var footballFixture=new FootballTestCache();var checks=new Dictionary<string,bool>();
        var day=DateOnly.FromDateTime(DateTime.Today);string path=Path.Combine(directory,"todo-fixture.json");if(File.Exists(path))File.Delete(path);
        var store=new TodoStore(path);var old=store.Add("昨日未完成",day.AddDays(-1));var current=store.Add("今天的事项",day);store.Add("明天的事项",day.AddDays(1));
        checks["unfinished_rolls_forward_without_duplicate"]=store.ForDay(day).Count==2&&store.ForDay(day.AddDays(1)).Count==3&&store.Items.Count==3;
        store.Toggle(current.Id);var reopened=new TodoStore(path);checks["tasks_and_completion_survive_reload"]=reopened.Items.Count==3&&reopened.Items.Single(t=>t.Id==current.Id).Completed!=null;
        var deleted=reopened.Delete(old.Id)!;reopened.Restore(deleted);checks["delete_and_undo_persist"]=new TodoStore(path).Items.Count==3;
        string blocked=Path.Combine(directory,"todo-blocked-fixture");Directory.CreateDirectory(blocked);var failed=new TodoStore(blocked);try{failed.Add("保存失败事项",day);checks["failed_save_does_not_report_success"]=false;}catch(Exception e)when(e is IOException or UnauthorizedAccessException){checks["failed_save_does_not_report_success"]=failed.Items.Count==0&&Directory.Exists(blocked);}if(File.Exists(blocked+".tmp"))File.Delete(blocked+".tmp");Directory.Delete(blocked);
        string bad=Path.Combine(directory,"todo-corrupt-fixture.json");File.WriteAllText(bad,"broken fixture");try{new TodoStore(bad).Add("不能覆盖",day);checks["corrupt_file_preserved"]=false;}catch(IOException){checks["corrupt_file_preserved"]=File.ReadAllText(bad)=="broken fixture";}
        checks["local_intent_routes"]=QuickRouter.Route("打开微信").Kind==QuickKind.OpenApp&&QuickRouter.Route("巴萨下一场什么时候").Kind==QuickKind.Football&&QuickRouter.Route("曼城排第几").Kind==QuickKind.Football&&QuickRouter.Route("今晚有什么值得看").Kind==QuickKind.Football&&QuickRouter.Route("明天添加待办，买牛奶").DayOffset==1;
        checks["analysis_and_casual_chat_need_model"]=QuickRouter.Route("分析巴萨为什么输了").Kind==QuickKind.FootballAnalysis&&QuickRouter.Route("今天有点烦，聊聊").Kind==QuickKind.Ai;
        int calls=0,opened=0;var apps=new[]{new AppEntry("微信","fixture-wx"),new AppEntry("浏览器甲","fixture-a"),new AppEntry("浏览器乙","fixture-b")};
        using var daily=new TodoForm(true);
        using var window=new ChatForm(true,ChatTests.Settings(),()=>{calls++;return ChatTests.FixtureApi();},new AppTools(()=>apps,_=>opened++),onTodo:intent=>{daily.Show();daily.Apply(intent);});window.Show();Application.DoEvents();
        void Send(string text){var task=window.PreviewSendAsync(text);var watch=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted&&watch.ElapsedMilliseconds<15000){Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();Application.DoEvents();}
        checks["settings_hidden_initially"]=window.PreviewOptionsHidden;
        Send("添加待办，买牛奶");checks["task_command_opens_local_list"]=daily.Visible&&daily.Owner==null&&!daily.TopMost&&daily.Count==1;
        Send("完成待办，买牛奶");checks["task_completed_without_model"]=daily.CompletedCount==1;
        Send("打开微信");checks["single_local_app_opens"]=opened==1;
        Send("打开浏览器");checks["ambiguous_local_app_requires_selection"]=opened==1;
        Send("巴萨下一场什么时候");checks["football_fact_uses_match_cards"]=window.PreviewMatchCards>0;
        checks["local_commands_make_zero_model_requests"]=calls==0;
        Send("你好，陪我聊两句");checks["casual_chat_invokes_model_after_local_routes"]=calls==1&&window.PreviewReplyComplete;
        var links=new WatchLinkStore(Path.Combine(directory,"watch-fixture.json"));links.Save(Enumerable.Range(1,4).Select(i=>new WatchLink("入口"+i,"https://example.com/"+i)));checks["four_watch_links_persist"]=new WatchLinkStore(Path.Combine(directory,"watch-fixture.json")).Items[3].Url=="https://example.com/4";try{FootTools.EdgeUrlStart("javascript:alert(1)");checks["invalid_link_rejected"]=false;}catch(ArgumentException){checks["invalid_link_rejected"]=true;}
        File.Delete(path);File.Delete(bad);return checks;
    }
}
