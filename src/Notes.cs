using System.Text.Json;
namespace LeoLauncher;
sealed record StickyNote(string Id,string Text,DateTimeOffset Created,DateTimeOffset Updated,string? TaskId=null);
sealed class NoteStore
{
    readonly string? file;public List<StickyNote> Items{get;private set;}=[];
    public NoteStore(string? path){file=path;if(file!=null&&File.Exists(file)){Items=JsonSerializer.Deserialize<List<StickyNote>>(File.ReadAllText(file))??[];if(Items.Count>1000||Items.Any(n=>n==null||n.Text.Length>20000||string.IsNullOrWhiteSpace(n.Id))||Items.Select(n=>n.Id).Distinct().Count()!=Items.Count)throw new IOException("便签文件格式无效，请保留原文件后恢复备份。");}}
    public StickyNote Save(string? id,string text){text=text.Trim();if(text.Length is <1 or >20000)throw new ArgumentException("便签须为1～20000个字符");var previous=Items.ToList();var old=Items.FirstOrDefault(n=>n.Id==id);if(old==null&&Items.Count>=1000)throw new InvalidOperationException("便签已达1000条，请先清理");var now=DateTimeOffset.Now;var item=old==null?new StickyNote(Guid.NewGuid().ToString("N"),text,now,now):old with{Text=text,Updated=now};if(old!=null)Items.Remove(old);Items.Insert(0,item);Commit(previous);return item;}
    public void Remove(string id){var previous=Items.ToList();Items.RemoveAll(n=>n.Id==id);Commit(previous);}
    public void Link(string id,string task){var previous=Items.ToList();int at=Items.FindIndex(n=>n.Id==id);if(at<0)throw new InvalidOperationException("便签已删除");Items[at]=Items[at] with{TaskId=task};Commit(previous);}
    void Commit(List<StickyNote> previous){try{if(file!=null)ConfigMigration.Write(file,JsonSerializer.Serialize(Items));}catch{Items=previous;throw;}}
}
sealed class NotesForm:BarcaWindow
{
    readonly NoteStore store;readonly Func<StickyNote,DateOnly,string> convert;readonly ListBox list=new();readonly TextBox input=new();readonly Label status=new();readonly BarcaButton turn=new(){Text="转为待办"};readonly NoteDateButton date=new();string? selected;bool changing,discardOnClose,disposed;
    public NotesForm(NoteStore notes,Func<StickyNote,DateOnly,string> addTodo)
    {
        store=notes;convert=addTodo;Text="Leo · 临时便签";ClientSize=new(760,520);StartPosition=FormStartPosition.CenterParent;Controls.Add(BarcaUi.Chrome(this,"临时便签","先记下来；Ctrl+Enter保存，选个日期就能转为待办。"));
        list.Bounds=new(24,108,224,300);list.BackColor=BarcaUi.Raised;list.ForeColor=BarcaUi.Text;list.BorderStyle=BorderStyle.None;list.DisplayMember="Text";list.DrawMode=DrawMode.OwnerDrawFixed;list.ItemHeight=48;list.DrawItem+=(_,e)=>{if(e.Index<0)return;var n=(StickyNote)list.Items[e.Index];using var body=BarcaUi.Font(9.5f);using var small=BarcaUi.Font(8);using var bg=new SolidBrush((e.State&DrawItemState.Selected)!=0?Appearance.Blue:BarcaUi.Raised);e.Graphics.FillRectangle(bg,e.Bounds);TextRenderer.DrawText(e.Graphics,(n.TaskId!=null?"✓ ":"")+n.Text.Replace('\n',' '),body,new Rectangle(e.Bounds.X+8,e.Bounds.Y+5,e.Bounds.Width-16,24),BarcaUi.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);TextRenderer.DrawText(e.Graphics,n.Updated.ToString("MM-dd HH:mm"),small,new Rectangle(e.Bounds.X+8,e.Bounds.Y+28,e.Bounds.Width-16,18),BarcaUi.Muted);};list.SelectedIndexChanged+=(_,_)=>{if(changing||list.SelectedItem is not StickyNote n)return;if(!ConfirmPending()){changing=true;list.SelectedItem=store.Items.FirstOrDefault(n=>n.Id==selected);changing=false;return;}selected=n.Id;input.Text=n.Text;turn.Enabled=n.TaskId==null;status.Text=n.TaskId==null?"编辑后保存；转待办会保留原便签。":"已转为待办，原便签保留。";};
        input.Bounds=new(268,108,468,300);input.Multiline=true;input.ScrollBars=ScrollBars.Vertical;input.MaxLength=20000;input.BackColor=BarcaUi.Raised;input.ForeColor=BarcaUi.Text;input.BorderStyle=BorderStyle.None;input.Font=BarcaUi.Font(11);input.AcceptsReturn=true;input.PlaceholderText="一句话，先记在这里…";
        var fresh=new BarcaButton{Text="新便签",Bounds=new(24,425,103,34)};fresh.Click+=(_,_)=>New();var remove=new BarcaButton{Text="删除",Bounds=new(137,425,111,34)};remove.Click+=(_,_)=>{try{if(selected==null)return;store.Remove(selected);input.Clear();selected=null;Reload();New();}catch(Exception e){status.Text=e.Message;}};
        var save=new BarcaButton{Text="记下 / 保存",Bounds=new(268,425,130,34)};save.Click+=(_,_)=>SaveNote();FormClosing+=(_,e)=>{if(!ConfirmPending())e.Cancel=true;else discardOnClose=true;};date.Bounds=new(410,427,167,30);turn.Bounds=new(589,425,147,34);turn.Click+=(_,_)=>{try{var n=SaveNote();if(n==null)return;if(n.TaskId!=null){status.Text="这条便签已经转为待办。";return;}string id=convert(n,DateOnly.FromDateTime(date.Value));store.Link(n.Id,id);Reload(n.Id);turn.Enabled=false;status.Text="已加入 "+date.Value.ToString("yyyy-MM-dd")+" 的清单，便签保留。";}catch(Exception e){status.Text=e.Message;}};
        status.Bounds=new(24,475,712,30);status.ForeColor=BarcaUi.Muted;status.Font=BarcaUi.Font(9);Controls.AddRange([list,input,fresh,remove,save,date,turn,status]);MinimumSize=MaximumSize=Size;Reload();New();
    }
    public void New(string text=""){if(!ConfirmPending())return;selected=null;changing=true;list.ClearSelected();changing=false;input.Text=text;turn.Enabled=true;status.Text="Ctrl+Enter快速记下 · 无需填写日期";input.Focus();}
    bool Dirty=>input.Text.Trim().Length>0&&input.Text.Trim()!=(store.Items.FirstOrDefault(n=>n.Id==selected)?.Text??"");
    bool ConfirmPending(){if(!Dirty)return true;using var confirm=new PendingNoteForm();var answer=confirm.ShowDialog(this);return answer==DialogResult.No||answer==DialogResult.Yes&&SaveNote()!=null;}
    protected override void Dispose(bool disposing){if(disposing&&!disposed){disposed=true;if(!discardOnClose&&Dirty)try{store.Save(selected,input.Text);}catch{}}base.Dispose(disposing);}
    StickyNote? SaveNote(){try{var n=store.Save(selected,input.Text);selected=n.Id;Reload(n.Id);status.Text="已保存到本机";return n;}catch(Exception e){status.Text=e.Message;return null;}}
    void Reload(string? id=null){changing=true;list.Items.Clear();list.Items.AddRange(store.Items.ToArray());list.SelectedItem=store.Items.FirstOrDefault(n=>n.Id==id);changing=false;}
    protected override bool ProcessCmdKey(ref Message message,Keys key){if(key==(Keys.Control|Keys.Enter)||key==(Keys.Control|Keys.S)){SaveNote();return true;}return base.ProcessCmdKey(ref message,key);}
}
sealed partial class PetForm
{
    NoteStore? noteStore;NotesForm? notesWindow;
    NoteStore Notes=>noteStore??=new(previewMode?null:Path.Combine(Store.Root,"leo-notes.json"));
    void ShowNotes(string initial="")
    {
        RevealDock(true);launcher.Hide();if(notesWindow==null||notesWindow.IsDisposed)notesWindow=new NotesForm(Notes,(note,day)=>{using var confirm=new QuickTextForm("便签转待办",note.Text,"确认标题后加入 "+day.ToString("yyyy-MM-dd")+" 的清单，原便签保留。");WindowPositions.Prepare(confirm,"note-todo",Bounds,previewMode);if(confirm.ShowDialog()!=DialogResult.OK)throw new OperationCanceledException("已取消转为待办");var task=todoStore.Add(confirm.Value,day,null);todoWindow?.View.Render();return task.Id;});if(!notesWindow.Visible)WindowPositions.Prepare(notesWindow,"notes",Bounds,previewMode);if(initial.Length>0)notesWindow.New(initial);notesWindow.Show();notesWindow.Activate();
    }
}

sealed class PendingNoteForm:BarcaWindow
{
    public PendingNoteForm(){Text="保存便签";ClientSize=new(430,225);StartPosition=FormStartPosition.CenterParent;Controls.Add(BarcaUi.Chrome(this,"这段文字还没有记下","保存后再继续，或放弃本次编辑。"));var buttons=new FlowLayoutPanel{Bounds=new(20,152,390,45),WrapContents=false};foreach(var (title,result) in new[]{("保存",DialogResult.Yes),("放弃",DialogResult.No),("取消",DialogResult.Cancel)})buttons.Controls.Add(new BarcaButton{Text=title,DialogResult=result,Size=new(120,34)});Controls.Add(buttons);MinimumSize=MaximumSize=Size;}
}

sealed class NoteDateButton:Button
{
    DateTime value=DateTime.Today;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public DateTime Value{get=>value;set{this.value=value.Date;Text=this.value.ToString("yyyy-MM-dd")+" ▾";Invalidate();}}
    public NoteDateButton(){Value=DateTime.Today;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;BackColor=BarcaUi.Raised;ForeColor=BarcaUi.Text;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Click+=(_,_)=>{using var picker=new NoteDateForm(DateOnly.FromDateTime(Value));picker.StartPosition=FormStartPosition.Manual;picker.Location=PopupPlacement.Place(new Rectangle(PointToScreen(Point.Empty),Size),picker.Size,Screen.FromControl(this).WorkingArea);if(picker.ShowDialog(FindForm())==DialogResult.OK)Value=picker.Selected.ToDateTime(TimeOnly.MinValue);};}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent?.BackColor??BarcaUi.Canvas);using var path=Appearance.Path(new Rectangle(0,0,Width-1,Height-1),9);using var fill=new SolidBrush(BarcaUi.Raised);e.Graphics.FillPath(fill,path);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,BarcaUi.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
}
sealed class NoteDateForm:BarcaWindow
{
    public DateOnly Selected{get;private set;}
    public NoteDateForm(DateOnly day){Selected=day;ClientSize=new(292,438);Text="选择待办日期";Controls.Add(BarcaUi.Chrome(this,"选择日期","选择一天，原便签会保留。"));var calendar=new PlannerCalendar{Location=new(20,100),Size=new(252,285)};calendar.Update(day,false,[]);calendar.DateSelected+=date=>{Selected=date;DialogResult=DialogResult.OK;};var today=new BarcaButton{Text="今天",Bounds=new(20,393,115,30)};today.Click+=(_,_)=>{Selected=DateOnly.FromDateTime(DateTime.Today);DialogResult=DialogResult.OK;};var cancel=new BarcaButton{Text="取消",Bounds=new(156,393,115,30),DialogResult=DialogResult.Cancel};Controls.AddRange([calendar,today,cancel]);MinimumSize=MaximumSize=Size;CancelButton=cancel;}
}
