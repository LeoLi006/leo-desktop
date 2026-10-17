using System.Text.Json;
namespace LeoLauncher;

sealed record ApiUsageConfig(int DailyLimit=100,bool RequestTokens=true)
{
    public static string FilePath=>Path.Combine(Store.Root,"leo-api-budget.json");
    public static ApiUsageConfig Load(){if(!File.Exists(FilePath))return new();return JsonSerializer.Deserialize<ApiUsageConfig>(File.ReadAllText(FilePath)) is {} c&&c.DailyLimit>=0?c:throw new IOException("API 限额配置损坏，请在用量页重新保存。");}
    public void Save(){File.WriteAllText(FilePath+".tmp",JsonSerializer.Serialize(this));File.Move(FilePath+".tmp",FilePath,true);}
}
sealed record UsageDay(string Day,int Requests=0,int Completed=0,long InputTokens=0,long OutputTokens=0,int WithoutTokens=0);
sealed class ApiUsageLedger
{
    public static readonly ApiUsageLedger Shared=new(Path.Combine(Store.Root,"leo-api-usage.json"));
    readonly string? path;readonly object gate=new();List<UsageDay>? days;
    public ApiUsageLedger(string? file){path=file;}
    void Load(){if(days!=null)return;days=path!=null&&File.Exists(path)?JsonSerializer.Deserialize<List<UsageDay>>(File.ReadAllText(path))??throw new IOException("API 用量记录无法读取，已暂停调用，请检查记录文件。"):new();}
    void Save(List<UsageDay> next){if(path!=null){File.WriteAllText(path+".tmp",JsonSerializer.Serialize(next));File.Move(path+".tmp",path,true);}days=next;}
    public UsageDay Today(DateTimeOffset? now=null){lock(gate){Load();string day=(now??DateTimeOffset.Now).ToLocalTime().ToString("yyyy-MM-dd");return days!.FirstOrDefault(d=>d.Day==day)??new(day);}}
    public UsageDay[] History(){lock(gate){Load();return days!.TakeLast(30).ToArray();}}
    public Ticket Begin(int limit,DateTimeOffset? now=null)
    {
        lock(gate){var day=Today(now);if(limit>0&&day.Requests>=limit)throw new InvalidOperationException("已达到今日 API 请求上限（"+limit+" 次）。本地清单、应用搜索等功能仍可使用。");
            Save(days!.Where(d=>d.Day!=day.Day).Append(day with{Requests=day.Requests+1}).OrderBy(d=>d.Day).TakeLast(366).ToList());return new(this,day.Day);}
    }
    void Finish(Ticket ticket){lock(gate){Load();Save(days!.Select(d=>d.Day==ticket.Day?d with{Completed=d.Completed+(ticket.Completed?1:0),InputTokens=d.InputTokens+ticket.Input,OutputTokens=d.OutputTokens+ticket.Output,WithoutTokens=d.WithoutTokens+(ticket.Reported?0:1)}:d).ToList());}}
    public sealed class Ticket:IDisposable
    {
        readonly ApiUsageLedger owner;internal string Day;internal long Input,Output;internal bool Reported;public bool Completed;bool disposed;
        internal Ticket(ApiUsageLedger ledger,string day){owner=ledger;Day=day;}
        public void Usage(JsonElement root){if(root.TryGetProperty("response",out var response))root=response;if(!root.TryGetProperty("usage",out var usage)||usage.ValueKind!=JsonValueKind.Object)return;
            long Read(string first,string second)=>usage.TryGetProperty(first,out var value)||usage.TryGetProperty(second,out value)?value.TryGetInt64(out var n)?Math.Max(0,n):0:0;
            Input=Read("input_tokens","prompt_tokens");Output=Read("output_tokens","completion_tokens");Reported=true;
        }
        public void Dispose(){if(disposed)return;disposed=true;owner.Finish(this);}
    }
}

sealed class UsageSettingsView:Panel
{
    readonly Label summary=new(){Bounds=new Rectangle(8,10,560,150),Font=BarcaUi.Font(10),ForeColor=BarcaUi.Text};
    readonly NumericUpDown limit=new(){Bounds=new Rectangle(200,181,150,30),Maximum=100000,Minimum=0,Value=100,BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text};
    readonly CheckBox tokens=new(){Bounds=new Rectangle(8,232,550,30),Text="请求 Token 统计（中转站需要支持，可关闭）",ForeColor=BarcaUi.Text};
    readonly bool preview;
    public UsageSettingsView(bool demo){preview=demo;Dock=DockStyle.Fill;Controls.AddRange([summary,limit,tokens,new Label{Text="每日模型请求上限",Bounds=new Rectangle(8,185,185,27),ForeColor=BarcaUi.Gold},new Label{Text="0 表示不限；默认每天 100 次。工具多轮回复按实际请求计次。\n失败、取消也计入请求数；模型列表查询不计入。\nToken 只累计接口返回值，缺失时显示未报告，不猜测费用。",Bounds=new Rectangle(8,285,560,90),Font=BarcaUi.Font(9),ForeColor=BarcaUi.Muted}]);
        try{var config=preview?new ApiUsageConfig():ApiUsageConfig.Load();limit.Value=Math.Min(config.DailyLimit,(int)limit.Maximum);tokens.Checked=config.RequestTokens;}catch(Exception e){summary.Text=e.Message;}
        var save=new BarcaButton{Text="保存限额",Bounds=new Rectangle(407,180,140,32)};Controls.Add(save);save.Click+=(_,_)=>{try{if(!preview)new ApiUsageConfig((int)limit.Value,tokens.Checked).Save();Reload();}catch(Exception e){summary.Text=e.Message;}};
        VisibleChanged+=(_,_)=>{if(Visible)Reload();};Reload();
    }
    void Reload(){try{var today=preview?new UsageDay(DateTime.Today.ToString("yyyy-MM-dd")):ApiUsageLedger.Shared.Today();var history=preview?new[]{today}:ApiUsageLedger.Shared.History();summary.Text=$"今日 · {today.Day}\n模型请求 {today.Requests} 次 · 完整回复 {today.Completed} 次\n输入 {today.InputTokens:N0} / 输出 {today.OutputTokens:N0} Token\n未报告 Token {today.WithoutTokens} 次\n近 30 个记录日共 {history.Sum(d=>d.Requests)} 次请求";}catch{summary.Text="用量记录无法读取，模型调用将暂停；原文件已保留。";}}
}
