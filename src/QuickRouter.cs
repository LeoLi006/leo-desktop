using System.Text.RegularExpressions;
namespace LeoLauncher;
enum QuickKind{Ai,LeoCommand,OpenApp,SearchApp,Clipboard,Web,Todos,AddTodo,CompleteTodo,Football,FootballAnalysis}
sealed record QuickIntent(QuickKind Kind,string Value="",string Team="",int DayOffset=0,string Argument="");
static class QuickRouter
{
    public static bool IsLocalMusic(string text){var intent=Route(text);return intent.Kind==QuickKind.LeoCommand&&intent.Value is "music_step" or "music_playlist_name";}
    public static string Team(string text)=>text.Contains("曼城")?"曼城":text.Contains("迈阿密")?"迈阿密国际":text.Contains("巴萨")||text.Contains("巴塞罗那")?"巴塞罗那":"";
    public static QuickIntent Route(string text){text=text.Trim().TrimEnd('。','！','？','?','!');string team=Team(text);
        if(text is "我有什么待办" or "今天有什么待办" or "我的待办有哪些" or "待办有哪些" or "清单有哪些" or "今天要做什么")return new(QuickKind.Todos);
        if(Regex.IsMatch(text,@"^(?:添加|打开|查看|搜索)?(?:我的|今天的|今日|每日)?(?:待办|清单|任务)(?:列表)?$"))return new(QuickKind.Todos);
        if(text is "每周清单" or "打开每周清单" or "本周待办")return new(QuickKind.LeoCommand,"todos_open",Argument:"week");
        if(Regex.IsMatch(text,"提醒|后天|下周|每周|每天|[0-9一二三四五六七八九十两]+[点时月日号]")&&Regex.IsMatch(text,"待办|任务|清单|提醒|安排"))return new(QuickKind.Ai,text);
        var task=Regex.Match(text,@"^(?:(今天|明天)(?:的)?)?(?:添加|新增|加一个|记一个)?(?:待办|任务|清单)[：:,，\s]*(.+)$");
        if(task.Success)return new(QuickKind.AddTodo,task.Groups[2].Value,DayOffset:task.Groups[1].Value=="明天"?1:0);
        task=Regex.Match(text,@"^(?:完成|勾选完成)(?:待办|任务)[：:,，\s]*(.+)$");if(task.Success)return new(QuickKind.CompleteTodo,task.Groups[1].Value);
        if(text is "剪贴板" or "剪切板" or "打开剪贴板" or "打开剪切板" or "搜索剪贴板")return new(QuickKind.Clipboard);
        if(text is "下一首" or "切歌" or "换一首" or "播放下一首" or "下一曲")return new(QuickKind.LeoCommand,"music_step",Argument:"1");
        if(text is "上一首" or "播放上一首" or "上一曲")return new(QuickKind.LeoCommand,"music_step",Argument:"-1");
        var musicName=Regex.Match(text,@"^(?:请|帮我)?(?:听|播放)(?:一下)?(?:我的)?(?:歌单)?[：:\s“""「]*(.+?)[”""」]*(?:歌单)?$");if(musicName.Success&&text.Length<=36&&!Regex.IsMatch(text,"然后|并且|分析|为什么|如何|怎么样|再播放|^听说|^听起来|^听到|^听不"))return new(QuickKind.LeoCommand,"music_playlist_name",Argument:musicName.Groups[1].Value.Trim());
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
        bool football=team.Length>0||Regex.IsMatch(text,"足球|比赛|看球|球赛|欧冠|英超|西甲|美职联|球队近况")||text.Contains("今晚")&&Regex.IsMatch(text,"值得看|看什么");
        if(football){bool analysis=Regex.IsMatch(text,"为什么|分析|解读|总结|怎么看|原因|预测|战术");return new(analysis?QuickKind.FootballAnalysis:QuickKind.Football,text,team,text.Contains("明天")?1:0);}
        return new(QuickKind.Ai,text);
    }
}
