using System.Text.Json;
namespace LeoLauncher;
sealed record QuietHours(bool Enabled=true,int Start=23*60,int End=8*60)
{
    public bool Contains(DateTime local){int m=local.Hour*60+local.Minute;return Enabled&&(Start<End?m>=Start&&m<End:m>=Start||m<End);}
    public static string FilePath=>Path.Combine(Store.Root,"leo-reminder-settings.json");
    public static QuietHours Load(){try{return JsonSerializer.Deserialize<QuietHours>(File.ReadAllText(FilePath)) is {} q&&q.Start>=0&&q.Start<1440&&q.End>=0&&q.End<1440&&q.Start!=q.End?q:new();}catch{return new();}}
    public void Save(){if(Start<0||Start>=1440||End<0||End>=1440)throw new ArgumentException("时间范围无效");if(Start==End)throw new ArgumentException("开始和结束时间不能相同");AtomicJson.Write(FilePath,JsonSerializer.Serialize(this));}
}
sealed partial class TodoStore
{
    public string? BackupError{get;private set;}
    public string? BackupDirectory=>path==null?null:Path.Combine(Path.GetDirectoryName(path)!,"todo-backups");
    public string[] Backups()=>BackupDirectory!=null&&Directory.Exists(BackupDirectory)?Directory.GetFiles(BackupDirectory,"*.json").OrderDescending().ToArray():[];
    void Snapshot(string json,string kind){if(BackupDirectory==null)return;ValidateSnapshot(json);Directory.CreateDirectory(BackupDirectory);string target=Path.Combine(BackupDirectory,DateTime.Now.ToString("yyyyMMdd-HHmmss-fffffff")+"-"+kind+".json");File.WriteAllText(target+".tmp",json);File.Move(target+".tmp",target);foreach(var old in Backups().Skip(60))File.Delete(old);}
    internal static List<DailyTask> ValidateSnapshot(string json){var data=JsonSerializer.Deserialize<List<DailyTask>>(json)??throw new IOException("不是有效的清单备份");if(data.Any(t=>t==null||string.IsNullOrWhiteSpace(t.Id)||string.IsNullOrWhiteSpace(t.Title)||t.Title.Length>200)||data.Select(t=>t.Id).Distinct().Count()!=data.Count)throw new IOException("备份内容不完整，未更改当前清单");return data;}
    void AutoBackup(){if(path==null||!File.Exists(path))return;try{string today=DateTime.Now.ToString("yyyyMMdd");if(!Backups().Any(f=>Path.GetFileName(f).StartsWith(today)&&f.EndsWith("-daily.json")))Snapshot(File.ReadAllText(path),"daily");BackupError=null;}catch(Exception e){BackupError="自动备份失败："+e.Message;}}
    void LatestBackup(){if(BackupDirectory==null)return;try{Directory.CreateDirectory(BackupDirectory);string latest=Path.Combine(BackupDirectory,"latest.json");AtomicJson.Write(latest,JsonSerializer.Serialize(records));foreach(var old in Backups().Skip(60))File.Delete(old);}catch(Exception e){BackupError="最新备份失败："+e.Message;}}
    public void ReloadImported(){records=path!=null&&File.Exists(path)?ValidateSnapshot(File.ReadAllText(path)):new();corrupt=false;Generation++;undoHistory.Clear();Error=null;EnsureThrough(DateOnly.FromDateTime(DateTime.Today).AddDays(35));LatestBackup();NotifyChanged();}
    public void BackupNow(){Snapshot(JsonSerializer.Serialize(records),"manual");BackupError=null;}
    public void RestoreBackup(string file){if(!Backups().Contains(file))throw new ArgumentException("请选择本机清单备份");var next=ValidateSnapshot(File.ReadAllText(file));if(!corrupt)Snapshot(JsonSerializer.Serialize(records),"before-restore");OperationToken.ThrowIfCancellationRequested();if(path!=null){AtomicJson.Write(path,JsonSerializer.Serialize(next));}corrupt=false;undoHistory.Clear();records=next;Generation++;Error=null;LatestBackup();NotifyChanged();}
}
sealed class PlannerSettings:BarcaWindow
{
    readonly TodoClient store;readonly ListBox backups=new(){Bounds=new Rectangle(22,267,516,130),BackColor=BarcaUi.Surface,ForeColor=BarcaUi.Text,BorderStyle=BorderStyle.None,Font=BarcaUi.Font(9.5f),ItemHeight=26};
    readonly Label status=new(){Bounds=new Rectangle(23,452,510,44),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f)};
    public PlannerSettings(TodoStore data):this(data.Client){}
    public PlannerSettings(TodoClient data){store=data;Text="Leo · 提醒与备份";ClientSize=new Size(600,510);Controls.Add(BarcaUi.Chrome(this,"提醒与备份","安静休息，安心记录。"));var q=store.Quiet();var enabled=new CheckBox{Text="启用免打扰时段（本地时间）",Checked=q.Enabled,Bounds=new Rectangle(23,104,450,30),ForeColor=BarcaUi.Text};var start=Clock(q.Start,23,144);var end=Clock(q.End,170,144);var save=new BarcaButton{Text="保存时段",Bounds=new Rectangle(323,144,114,32)};save.Click+=(_,_)=>Run(()=>{store.SaveQuiet(new QuietHours(enabled.Checked,ReadClock(start),ReadClock(end)));status.Text="已保存。免打扰结束后统一补报未完成提醒。";});Controls.AddRange([enabled,start,end,save,new Label{Text="期间不弹窗、不发气泡；结束后补报。Leo 需保持运行。",Bounds=new Rectangle(23,190,510,25),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f)},new Label{Text="自动备份 · 每次保存更新最新副本，每日留档 · 最多60份",Bounds=new Rectangle(23,230,556,28),ForeColor=BarcaUi.Gold},backups,status]);var now=new BarcaButton{Text="立即备份",Bounds=new Rectangle(23,411,110,32)};var restore=new BarcaButton{Text="恢复所选备份…",Bounds=new Rectangle(146,411,159,32)};now.Click+=(_,_)=>Run(()=>{store.BackupNow();Reload();status.Text="已备份当前完整清单（包含重复规则）。";});restore.Click+=(_,_)=>{if(backups.SelectedItem is not BackupChoice choice)return;if(UiDialogs.Show(this,"将恢复所选时刻的完整清单。当前版本会先保存一份备份。\n确认恢复？","恢复清单",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;Run(()=>{store.RestoreBackup(choice.Path);Reload();status.Text="恢复成功，恢复前版本也已保存。";});};Controls.AddRange([now,restore]);Reload();CanResize=false;MaximizeBox=false;MinimumSize=MaximumSize=Size;}
    MaskedTextBox Clock(int minutes,int x,int y)=>new(){Bounds=new Rectangle(x,y,130,32),Mask="00:00",Text=$"{minutes/60:00}{minutes%60:00}",BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text,BorderStyle=BorderStyle.FixedSingle,Font=BarcaUi.Font(12)};
    static int ReadClock(MaskedTextBox field){if(!TimeOnly.TryParseExact(field.Text,"HH:mm",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var t))throw new ArgumentException("时间请填写为 HH:mm，例如 23:00");return t.Hour*60+t.Minute;}
    void Run(Action a){try{a();}catch(Exception e){status.Text=e.Message;}}
    void Reload(){backups.Items.Clear();foreach(var file in store.Backups())backups.Items.Add(new BackupChoice(file));status.Text=store.BackupError??"备份仅保存清单，不包含 API Key 或聊天记录。";}
    sealed record BackupChoice(string Path){public override string ToString(){string name=System.IO.Path.GetFileNameWithoutExtension(Path);if(name=="latest")return "最新完整副本 · 每次保存更新";if(name.Length>=15&&DateTime.TryParseExact(name[..15],"yyyyMMdd-HHmmss",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var time))return time.ToString("yyyy.MM.dd  HH:mm:ss")+"  ·  "+(name.EndsWith("daily")?"每日留档":name.EndsWith("manual")?"手动备份":"恢复前留档");return name;}}
}
