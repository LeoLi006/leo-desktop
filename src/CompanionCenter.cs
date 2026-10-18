using System.Text.Json;
namespace LeoLauncher;
sealed record InboxItem(string Id,DateTimeOffset Time,string Title,string Kind,bool Read=false,string TaskId="",string UniqueKey="");
sealed class NoticeInbox
{
    readonly string? path;readonly Dictionary<string,(string Label,Func<Task> Run)> actions=new();bool corrupt;
    public List<InboxItem> Items{get;private set;}=[];public string? Error{get;private set;}public int Unread=>Items.Count(i=>!i.Read);public event Action? Changed;
    public NoticeInbox(string? file){path=file;try{if(file!=null&&File.Exists(file))Items=(JsonSerializer.Deserialize<List<InboxItem>>(File.ReadAllText(file))??[]).Take(200).ToList();}catch{corrupt=true;Error="通知记录无法读取，原文件已保留。";}}
    public InboxItem Add(string title,string kind="operation",string taskId="",string unique="",Func<Task>? action=null,string label="重试",DateTimeOffset? time=null)
    {
        var now=time??DateTimeOffset.Now;var existing=Items.FirstOrDefault(i=>unique.Length>0?i.UniqueKey==unique:i.Title==title&&i.Kind==kind&&now-i.Time<TimeSpan.FromSeconds(30));if(existing!=null){if(action!=null&&(unique.Length>0||existing.UniqueKey.Length==0))actions[existing.Id]=(label,action);var refreshed=existing with{Read=false,Time=now};Items.RemoveAll(i=>i.Id==existing.Id);Items.Insert(0,refreshed);Save();return refreshed;}
        title=System.Text.RegularExpressions.Regex.Replace(title,@"(?i)(Bearer\s+|(?:api[_ -]?key|token)\s*[:=]\s*)\S+","$1[已隐藏]");if(title.Length>1200)title=title[..1200];var item=new InboxItem(Guid.NewGuid().ToString("N"),now,title,kind,TaskId:taskId,UniqueKey:unique);Items.Insert(0,item);Items=Items.Take(200).ToList();foreach(string key in actions.Keys.Where(k=>Items.All(i=>i.Id!=k)).ToArray())actions.Remove(key);if(action!=null)actions[item.Id]=(label,action);Save();return item;
    }
    public (string Label,Func<Task> Run)? ActionFor(string id)=>actions.TryGetValue(id,out var value)?value:null;
    public void Read(string id){Items=Items.Select(i=>i.Id==id?i with{Read=true}:i).ToList();Save();}
    public void ReadAll(){Items=Items.Select(i=>i with{Read=true}).ToList();Save();}
    public void ClearRead(){var removed=Items.Where(i=>i.Read).Select(i=>i.Id).ToArray();Items.RemoveAll(i=>i.Read);foreach(string id in removed)actions.Remove(id);Save();}
    void Save(){if(!corrupt&&path!=null)try{ConfigMigration.Write(path,JsonSerializer.Serialize(Items));Error=null;}catch{Error="通知未能写入，本次仍保留在内存中。";}Changed?.Invoke();}
}
sealed class NoticeInboxForm:BarcaWindow
{
    readonly NoticeInbox inbox;readonly Action<string> openTask;readonly FlowLayoutPanel list=new(){Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(18,8,14,8)};readonly Label state=new(){Dock=DockStyle.Bottom,Height=32,Padding=new Padding(20,5,0,0),ForeColor=BarcaUi.Muted};bool queued;
    public NoticeInboxForm(NoticeInbox data,Action<string> task)
    {
        inbox=data;openTask=task;Text="Leo · 通知收纳盒";ClientSize=new(620,550);StartPosition=FormStartPosition.CenterParent;var chrome=BarcaUi.Chrome(this,"通知收纳盒","提醒、失败与操作结果，留在这里慢慢看。");
        var tools=new FlowLayoutPanel{Dock=DockStyle.Top,Height=47,Padding=new Padding(17,6,0,0)};var read=new BarcaButton{Text="全部已读",Size=new(105,31)};var clear=new BarcaButton{Text="清理已读",Size=new(105,31)};read.Click+=(_,_)=>inbox.ReadAll();clear.Click+=(_,_)=>inbox.ClearRead();tools.Controls.AddRange([read,clear]);Controls.AddRange([list,state,tools,chrome]);inbox.Changed+=Changed;Render();MinimumSize=MaximumSize=Size;
    }
    void Changed(){if(IsDisposed||Disposing||queued)return;if(!IsHandleCreated){Render();return;}queued=true;BeginInvoke((Action)(()=>{queued=false;if(!IsDisposed)Render();}));}
    void Render()
    {
        list.SuspendLayout();foreach(Control c in list.Controls.Cast<Control>().ToArray())c.Dispose();foreach(var item in inbox.Items){var row=new RoundedSurface{Size=new(560,117),Margin=new Padding(0,0,0,9),BackColor=item.Read?BarcaUi.Surface:BarcaUi.Raised};var title=new Label{Text=(item.Read?"":"● ")+item.Title,Bounds=new(13,12,532,52),ForeColor=BarcaUi.Text,Font=BarcaUi.Font(9.5f),AutoEllipsis=true};title.Click+=(_,_)=>inbox.Read(item.Id);row.Controls.Add(title);row.Controls.Add(new Label{Text=item.Time.ToLocalTime().ToString("MM.dd HH:mm")+" · "+(item.Kind=="reminder"?"清单提醒":item.Kind=="error"?"操作未完成":item.Kind=="focus"?"专注":"操作结果"),Bounds=new(14,81,310,24),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f)});
            var action=inbox.ActionFor(item.Id);if(item.TaskId.Length>0||action!=null){var button=new BarcaButton{Text=item.TaskId.Length>0?"查看清单":action!.Value.Label,Bounds=new(435,76,110,29)};button.Click+=async(_,_)=>{button.Enabled=false;try{if(item.TaskId.Length>0)openTask(item.TaskId);else await action!.Value.Run();inbox.Read(item.Id);}catch(Exception e){state.Text=e.Message;}finally{if(!button.IsDisposed)button.Enabled=true;}};row.Controls.Add(button);}else if(!item.Read){var mark=new BarcaButton{Text="已读",Bounds=new(465,76,80,29)};mark.Click+=(_,_)=>inbox.Read(item.Id);row.Controls.Add(mark);}list.Controls.Add(row);}
        if(inbox.Items.Count==0)list.Controls.Add(new Label{Text="还没有通知，Leo 会替你收好错过的提醒。",Size=new(550,60),ForeColor=BarcaUi.Muted,Padding=new Padding(8,20,0,0)});state.Text=inbox.Error??$"未读 {inbox.Unread} · 最近 {inbox.Items.Count} 条 · 重试仅在当前运行会话可用";list.ResumeLayout();
    }
    protected override void Dispose(bool disposing){if(disposing)inbox.Changed-=Changed;base.Dispose(disposing);}
}
sealed class FocusSession
{
    readonly string? path;public DateTimeOffset? Until{get;private set;}public bool AllowImportant{get;private set;}=true;public bool Active(DateTimeOffset now)=>Until>now;
    public FocusSession(string? file){path=file;try{if(file!=null&&File.Exists(file)){var data=JsonSerializer.Deserialize<FocusData>(File.ReadAllText(file));Until=data?.Until;AllowImportant=data?.AllowImportant??true;}}catch{}}
    public void Start(int minutes,bool important,DateTimeOffset now){if(minutes is <1 or >180)throw new ArgumentException("专注时间请设为1至180分钟");Save(new(now.AddMinutes(minutes),important));}
    public void SetImportant(bool value)=>Save(new(Until,value));
    public void Stop()=>Save(new(null,AllowImportant));
    public bool Complete(DateTimeOffset now){if(Until is not{} end||end>now)return false;Stop();return true;}
    public bool ShouldInterrupt(DailyTask task,DateTimeOffset now)=>!Active(now)||AllowImportant&&(task.Important||task.MatchId!=null);
    public string Remaining(DateTimeOffset now){var left=Until-now;return left>TimeSpan.Zero?Math.Ceiling(left.Value.TotalMinutes)+" 分钟":"已结束";}
    void Save(FocusData next){if(path!=null)ConfigMigration.Write(path,JsonSerializer.Serialize(next));Until=next.Until;AllowImportant=next.AllowImportant;}
    sealed record FocusData(DateTimeOffset? Until,bool AllowImportant);
}
sealed class FocusForm:BarcaWindow
{
    readonly System.Windows.Forms.Timer timer=new(){Interval=1000};
    public FocusForm(FocusSession session,Action changed)
    {
        Text="Leo · 临时专注";ClientSize=new(455,360);StartPosition=FormStartPosition.CenterParent;var header=BarcaUi.Chrome(this,"专注一会儿","Leo 安静陪着你，普通提醒收进通知盒。");var state=new Label{Bounds=new(24,107,405,43),ForeColor=BarcaUi.Gold,Font=BarcaUi.Font(18,true)};var important=new CheckBox{Text="重要事项和开赛提醒仍然弹出",Checked=session.AllowImportant,Bounds=new(24,170,405,28),ForeColor=BarcaUi.Text};var error=new Label{Bounds=new(24,279,405,55),ForeColor=BarcaUi.Muted};
        void Update(){state.Text=session.Active(DateTimeOffset.Now)?"还剩 "+session.Remaining(DateTimeOffset.Now):"选一段留给自己的时间";}
        important.CheckedChanged+=(_,_)=>{try{session.SetImportant(important.Checked);changed();}catch(Exception e){error.Text=e.Message;}};
        int x=24;foreach(int minutes in new[]{25,45,60}){var start=new BarcaButton{Text=minutes+" 分钟",Bounds=new(x,224,96,34)};start.Click+=(_,_)=>{try{session.Start(minutes,important.Checked,DateTimeOffset.Now);changed();Update();error.Text="已开始；清单右键可标为重要事项。";}catch(Exception e){error.Text=e.Message;}};Controls.Add(start);x+=104;}
        var end=new BarcaButton{Text="结束",Bounds=new(336,224,94,34)};end.Click+=(_,_)=>{try{session.Stop();changed();Update();error.Text="专注已结束，提醒恢复正常。";}catch(Exception e){error.Text=e.Message;}};Controls.AddRange([state,important,error,end,header]);timer.Tick+=(_,_)=>Update();timer.Start();Update();MinimumSize=MaximumSize=Size;
    }
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
sealed partial class PetForm
{
    readonly NoticeInbox notices;readonly FocusSession focus;readonly System.Windows.Forms.Timer companionTimer=new(){Interval=1000};NoticeInboxForm? noticeWindow;FocusForm? focusWindow;bool noticePress;
    void WireCompanionMenu(ContextMenuStrip menu)
    {
        var inbox=new ToolStripMenuItem("通知收纳盒");inbox.Click+=(_,_)=>ShowNotices();var concentrate=new ToolStripMenuItem("临时专注");concentrate.Click+=(_,_)=>{RevealDock(true);if(focusWindow==null||focusWindow.IsDisposed)focusWindow=new FocusForm(focus,()=>{Invalidate();CheckReminders();});if(!focusWindow.Visible)WindowPositions.Prepare(focusWindow,"focus",Bounds,previewMode);focusWindow.Show();focusWindow.Activate();};menu.Items.Insert(0,inbox);menu.Items.Insert(1,concentrate);menu.Opening+=(_,_)=>{inbox.Text="通知收纳盒"+(notices.Unread>0?" · "+notices.Unread:"");concentrate.Text=focus.Active(DateTimeOffset.Now)?"专注中 · "+focus.Remaining(DateTimeOffset.Now):"临时专注";};
        notices.Changed+=()=>{if(!IsDisposed)Invalidate();};companionTimer.Tick+=(_,_)=>{try{if(focus.Complete(DateTimeOffset.Now)){if(QuietHours.Load().Contains(DateTime.Now))notices.Add("这段专注完成了，休息一下吧。","focus");else PresentFeedback(new("这段专注完成了，休息一下吧。"));CheckReminders();}if(focus.Active(DateTimeOffset.Now))Invalidate();UpdateMatchDay();}catch(Exception e){companionTimer.Stop();PresentFeedback(new("专注或比赛日状态未能保存："+e.Message,true));}};if(!previewMode)companionTimer.Start();
    }
    void ShowNotices(){RevealDock(true);launcher.Hide();if(noticeWindow==null||noticeWindow.IsDisposed)noticeWindow=new(notices,id=>{var task=todoStore.Items.FirstOrDefault(t=>t.Id==id);if(task==null)throw new InvalidOperationException("这条事项已经删除。");OpenReminderTask(task);});if(!noticeWindow.Visible)WindowPositions.Prepare(noticeWindow,"notifications",Bounds,previewMode);noticeWindow.Show();noticeWindow.Activate();}
    void DrawCompanionBadges(Graphics g,RectangleF bounds)
    {
        if(matchDayTeam!=null&&matchDayAsset==null&&prefs.FootballModule&&prefs.MatchDayOutfits&&!focus.Active(DateTimeOffset.Now))BarcaUi.DrawBadge(g,matchDayTeam,new Rectangle((int)bounds.Right-18,(int)(bounds.Top+bounds.Height*.64f),16,16));
        if(notices.Unread>0){using var dot=new SolidBrush(BarcaUi.Gold);g.FillEllipse(dot,bounds.Right-16,bounds.Top+4,10,10);}
    }
}
