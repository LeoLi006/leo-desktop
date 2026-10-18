using System.Text.RegularExpressions;
namespace LeoLauncher;
enum QuickKind{Ai,LeoCommand,OpenApp,SearchApp,Clipboard,Web,Todos,AddTodo,CompleteTodo,Football,FootballAnalysis}
sealed record QuickIntent(QuickKind Kind,string Value="",string Team="",int DayOffset=0,string Argument="");
static class QuickRouter
{
    public static bool IsLocalMusic(string text){var intent=Route(text);return intent.Kind==QuickKind.LeoCommand&&(intent.Value is "music_step" or "scene_watch" or "combo_name" or "music_pause"||intent.Value=="music_open"&&intent.Argument.Length>0);}
    public static string Team(string text)=>ClubCatalog.Mention(text);
    public static QuickIntent Route(string text){text=text.Trim().TrimEnd('。','！','？','?','!');string team=Team(text);
        if(text is "撤销上一步" or "撤销清单操作")return new(QuickKind.LeoCommand,"todo_undo");
        var combo=Regex.Match(text,@"^(?:执行自定义|运行组合|启动组合)[：:\s]*(.+)$");if(combo.Success)return new(QuickKind.LeoCommand,"combo_name",Argument:combo.Groups[1].Value.Trim());
        if(text is "准备看球" or "进入看球模式" or "开始看球")return new(QuickKind.LeoCommand,"scene_watch");
        if(text is "看看今天的清单" or "看看今天的清单计划" or "今天的计划" or "我有什么待办" or "今天有什么待办" or "我的待办有哪些" or "待办有哪些" or "清单有哪些" or "今天要做什么")return new(QuickKind.Todos);
        if(Regex.IsMatch(text,@"^(?:添加|打开|查看|搜索)?(?:我的|今天的|今日|每日)?(?:待办|清单|任务)(?:列表)?$"))return new(QuickKind.Todos);
        if(text is "每周清单" or "打开每周清单" or "本周待办")return new(QuickKind.LeoCommand,"todos_open",Argument:"week");
        if(Regex.IsMatch(text,"提醒|后天|下周|每周|每天|[0-9一二三四五六七八九十两]+[点时月日号]")&&Regex.IsMatch(text,"待办|任务|清单|提醒|安排"))return new(QuickKind.Ai,text);
        var task=Regex.Match(text,@"^(?:(今天|明天)(?:的)?)?(?:添加|新增|加一个|记一个)?(?:待办|任务|清单)[：:,，\s]*(.+)$");
        if(task.Success)return new(QuickKind.AddTodo,task.Groups[2].Value,DayOffset:task.Groups[1].Value=="明天"?1:0);
        task=Regex.Match(text,@"^(?:完成|勾选完成)(?:待办|任务)[：:,，\s]*(.+)$");if(task.Success)return new(QuickKind.CompleteTodo,task.Groups[1].Value);
        if(text is "剪贴板" or "剪切板" or "打开剪贴板" or "打开剪切板" or "搜索剪贴板")return new(QuickKind.Clipboard);
        if(Regex.IsMatch(text,@"^(?:打开|播放|听)(?:我的)?歌单.*$"))return new(QuickKind.LeoCommand,"music_client");
        if(text is "播放暂停" or "切换音乐播放状态")return new(QuickKind.LeoCommand,"music_pause");
        if(text is "下一首" or "切歌" or "换一首" or "播放下一首" or "下一曲")return new(QuickKind.LeoCommand,"music_step",Argument:"1");
        if(text is "上一首" or "播放上一首" or "上一曲")return new(QuickKind.LeoCommand,"music_step",Argument:"-1");
        var musicName=Regex.Match(text,@"^(?:请|帮我)?(?:听|播放)(?:一下)?(?:我的)?[：:\s“""「]*(.+?)[”""」]*$");if(musicName.Success&&text.Length<=36&&!Regex.IsMatch(text,"然后|并且|分析|为什么|如何|怎么样|再播放|^听说|^听起来|^听到|^听不"))return new(QuickKind.LeoCommand,"music_open",Argument:musicName.Groups[1].Value.Trim());
        if(text is "点歌" or "打开点歌" or "音乐面板")return new(QuickKind.LeoCommand,"music_open");
        if(text is "打开足球" or "打开足球窗口" or "足球界面")return new(QuickKind.LeoCommand,"football_open");
        if(text is "打开应用搜索" or "应用搜索")return new(QuickKind.LeoCommand,"launcher_open");
        if(text is "打开终端" or "打开管理员终端")return new(QuickKind.LeoCommand,"terminal_open");
        if(text is "查看代理状态" or "系统代理状态")return new(QuickKind.LeoCommand,"proxy_status");
        if(text is "开启系统代理" or "打开系统代理")return new(QuickKind.LeoCommand,"proxy_on");
        if(text is "关闭系统代理")return new(QuickKind.LeoCommand,"proxy_off");
        var match=Regex.Match(text,@"^(?:帮我|请|帮忙)?(?:打开|启动|运行)\s*(.+)$");if(match.Success)return Regex.IsMatch(match.Groups[1].Value,"然后|再打开|并且|同时")?new(QuickKind.Ai,text):new(QuickKind.OpenApp,match.Groups[1].Value);
        match=Regex.Match(text,@"^(?:查找应用|搜索应用|找应用)[：:\s]*(.*)$");if(match.Success)return new(QuickKind.SearchApp,match.Groups[1].Value);
        match=Regex.Match(text,@"^(?:网页搜索|联网搜索|搜一下|搜索)[：:\s]*(.+)$");if(match.Success)return new(QuickKind.Web,match.Groups[1].Value);
        if(Regex.IsMatch(text,"天气|新闻|汇率|股价")&&!Regex.IsMatch(text,"分析|总结|解读"))return new(QuickKind.Web,text);
        bool national=ClubCatalog.Find(team)?.IsNational==true;
        bool nationalFootball=Regex.IsMatch(text,"国家队|足球|球队|世界杯|欧洲杯|美洲杯|亚洲杯|进球|踢球|战术|开球")||Regex.IsMatch(text,"下一场|上一场|赛程|赛果|比分|积分榜|近况|排名")&&!Regex.IsMatch(text,"经济|人口|历史|旅游|签证|政治|地理|面积|语言|首都|科技|教育|文化|电影|总统");
        bool football=team.Length>0&&(!national||nationalFootball)||Regex.IsMatch(text,"足球|比赛|看球|球赛|欧冠|英超|西甲|美职联|球队近况")||text.Contains("今晚")&&Regex.IsMatch(text,"值得看|看什么");
        if(football){
            bool analysis=Regex.IsMatch(text,"为什么|分析|解读|总结|怎么看|原因|预测|战术");
            if(analysis)return new(QuickKind.FootballAnalysis,text,team);
            // Only explicit fact queries bypass conversation; a club mention is not a command.
            string club=ClubCatalog.Pattern;
            bool facts=Regex.IsMatch(text,@"^(?:(?:请|帮我|看看|查查|查询|查看|看一下|查一下)\s*)?(?:"+club+@"(?:的)?\s*)?(?:下一场(?:比赛)?(?:什么时候|几点|何时|时间|对手|和谁踢)?|上一场(?:比赛)?(?:比分|赛果|结果)?|(?:今天|明天|今晚|昨天|前天|最近|近期)(?:的)?(?:比赛|赛程|赛果|比分|看球)(?:有哪些|有什么|安排|时间|几点)?|(?:今天|明天|今晚)(?:有什么值得看|有什么比赛|有哪些比赛|有什么球赛)|(?:联赛)?(?:积分榜|积分|排名)|排第几|近况|球队近况|赛程|赛果|比分|比赛计划|比赛安排)$",RegexOptions.IgnoreCase);
            if(facts)return new(QuickKind.Football,text,team,text.Contains("明天")?1:0);
        }
        return new(QuickKind.Ai,text);
    }
}
