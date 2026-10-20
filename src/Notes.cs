using System.Text.Json;
namespace LeoLauncher;
sealed record StickyNote(string Id,string Text,DateTimeOffset Created,DateTimeOffset Updated,string? TaskId=null,bool Pinned=false);
sealed class NoteStore
{
    public event Action? Changed;
    readonly ModuleJsonData<List<StickyNote>> data;
    public List<StickyNote> Items=>data.Read(items=>items.ToList());
    public NoteStore(string? path){data=new(path,[],Validate,items=>items.ToList(),AtomicJson.WriteNotes);}
    internal NoteStore(string? path,Action<string,string> writer){data=new(path,[],Validate,items=>items.ToList(),writer);}
    internal static List<StickyNote> Validate(string text){var items=JsonSerializer.Deserialize<List<StickyNote>>(text)??throw new IOException("便签内容为空");if(items.Count>1000||items.Any(n=>n==null||n.Text==null||n.Text.Length>20000||string.IsNullOrWhiteSpace(n.Id))||items.Select(n=>n.Id).Distinct().Count()!=items.Count)throw new IOException("便签文件格式无效，请保留原文件后恢复备份。");return items;}
    public StickyNote Save(string? id,string text,CancellationToken token=default){text=text.Trim();if(text.Length is <1 or >20000)throw new ArgumentException("便签须为1～20000个字符");return Change(items=>{var old=items.FirstOrDefault(n=>n.Id==id);if(id!=null&&old==null)throw new ArgumentException("便签已删除，请另存为新便签");if(old==null&&items.Count>=1000)throw new InvalidOperationException("便签已达1000条，请先清理");var now=DateTimeOffset.Now;var item=old==null?new StickyNote(Guid.NewGuid().ToString("N"),text,now,now):old with{Text=text,Updated=now};if(old!=null)items.Remove(old);items.Insert(0,item);return item;},token);}
    public void Remove(string id,CancellationToken token=default)=>Change(items=>items.RemoveAll(n=>n.Id==id),token);
    public void Link(string id,string task,CancellationToken token=default)=>Change(items=>{int at=items.FindIndex(n=>n.Id==id);if(at<0)throw new ArgumentException("便签已删除");items[at]=items[at] with{TaskId=task};return true;},token);
    public List<StickyNote> Query(string text="",string scope="active")=>data.Read(items=>items.Where(n=>(scope=="all"||scope=="linked"?scope=="all"||n.TaskId!=null:n.TaskId==null)&&n.Text.Contains(text.Trim(),StringComparison.OrdinalIgnoreCase)).OrderByDescending(n=>n.Pinned).ThenByDescending(n=>n.Updated).ToList());
    public void Pin(string id,bool value,CancellationToken token=default)=>Change(items=>{int at=items.FindIndex(n=>n.Id==id);if(at<0)throw new ArgumentException("便签已不存在");items[at]=items[at] with{Pinned=value};return true;},token);
    T Change<T>(Func<List<StickyNote>,T> edit,CancellationToken token){var result=data.Change(edit,token);if(Changed is {} changed)foreach(Action subscriber in changed.GetInvocationList())FeatureFaults.Run("便签列表刷新",subscriber);return result;}
}
sealed class NotesForm:BarcaWindow
{
    readonly ModuleThemeBinding theme;
    readonly NotesClient store;readonly Dictionary<string,string> pendingLinks;readonly Func<StickyNote,DateOnly,string> convert;readonly ListBox list=new();readonly TextBox input=new(),search=new();readonly ComboBox filter=new();readonly Label status=new();readonly BarcaButton turn=new(){Text="转为待办"},pin=new(){Text="置顶"};readonly NoteDateButton date=new();string? selected;bool changing,discardOnClose,disposed;
    public NotesForm(NoteStore notes,Func<StickyNote,DateOnly,string> addTodo):this(NotesClient.Standalone(notes),addTodo){}
    public NotesForm(NotesClient notes,Func<StickyNote,DateOnly,string> addTodo,Dictionary<string,string>? links=null)
    {
        store=notes;convert=addTodo;pendingLinks=links??new();Text="Leo · 临时便签";ClientSize=new(800,620);StartPosition=FormStartPosition.CenterParent;Controls.Add(BarcaUi.Chrome(this,"临时便签","Ctrl+Enter保存 · Ctrl+F搜索 · 已转待办的原文单独收好。"));
        search.AccessibleName="便签搜索";input.AccessibleName="便签正文";filter.AccessibleName="便签分类";list.AccessibleName="便签列表";search.Bounds=new(24,108,240,30);search.PlaceholderText="搜索便签…";search.BackColor=BarcaUi.Raised;search.ForeColor=BarcaUi.Text;search.BorderStyle=BorderStyle.FixedSingle;
        filter.Bounds=new(24,150,240,30);filter.DropDownStyle=ComboBoxStyle.DropDownList;CompanionUi.ThemeCombo(filter);filter.Items.AddRange([new ComboChoice("便签","active"),new ComboChoice("已转待办","linked"),new ComboChoice("全部内容","all")]);filter.SelectedIndex=0;
        list.Bounds=new(24,192,240,310);list.BackColor=BarcaUi.Raised;list.ForeColor=BarcaUi.Text;list.BorderStyle=BorderStyle.None;list.DrawMode=DrawMode.OwnerDrawFixed;list.ItemHeight=56;list.DrawItem+=(_,e)=>{if(e.Index<0)return;var n=(StickyNote)list.Items[e.Index];using var body=BarcaUi.Font(9.5f);using var small=BarcaUi.Font(8);using var bg=new SolidBrush((e.State&DrawItemState.Selected)!=0?Appearance.Blue:BarcaUi.Raised);e.Graphics.FillRectangle(bg,e.Bounds);TextRenderer.DrawText(e.Graphics,(n.Pinned?"★ ":"")+(n.TaskId!=null?"✓ ":"")+n.Text.Replace('\n',' '),body,new Rectangle(e.Bounds.X+8,e.Bounds.Y+6,e.Bounds.Width-16,24),BarcaUi.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);TextRenderer.DrawText(e.Graphics,n.Updated.ToString("MM-dd HH:mm"),small,new Rectangle(e.Bounds.X+8,e.Bounds.Y+31,e.Bounds.Width-16,18),BarcaUi.Muted);};
        list.SelectedIndexChanged+=(_,_)=>{if(changing||list.SelectedItem is not StickyNote n)return;if(!ConfirmPending()){changing=true;list.SelectedItem=list.Items.Cast<StickyNote>().FirstOrDefault(n=>n.Id==selected);changing=false;return;}selected=n.Id;input.Text=n.Text;turn.Enabled=n.TaskId==null;pin.Text=n.Pinned?"取消置顶":"置顶";status.Text=n.TaskId==null?"编辑后保存；转待办会保留原文。":"已转为待办，原文保留在此。";};
        search.TextChanged+=(_,_)=>Reload(selected);filter.SelectedIndexChanged+=(_,_)=>Reload(selected);search.KeyDown+=(_,e)=>{if(e.KeyCode is Keys.Down or Keys.Enter&&list.Items.Count>0){list.SelectedIndex=Math.Max(0,list.SelectedIndex);list.Focus();e.SuppressKeyPress=true;}};SelectionKeys.Wire(list,()=>input.Focus());
        input.Bounds=new(286,108,490,394);input.Multiline=true;input.ScrollBars=ScrollBars.Vertical;input.MaxLength=20000;input.BackColor=BarcaUi.Raised;input.ForeColor=BarcaUi.Text;input.BorderStyle=BorderStyle.None;input.Font=BarcaUi.Font(11);input.AcceptsReturn=true;input.PlaceholderText="一句话，先记在这里…";
        var fresh=new BarcaButton{Text="新便签",Bounds=new(24,520,112,34)};fresh.Click+=(_,_)=>New();var remove=new BarcaButton{Text="删除",Bounds=new(152,520,112,34)};remove.Click+=(_,_)=>{try{if(selected==null)return;store.Remove(selected);input.Clear();selected=null;Reload();New();}catch(Exception e){status.Text=e.Message;}};
        pin.Bounds=new(24,562,240,32);pin.Click+=(_,_)=>{try{var n=store.Items.FirstOrDefault(n=>n.Id==selected);if(n==null){status.Text="先选择一条便签。";return;}store.Pin(n.Id,!n.Pinned);pin.Text=n.Pinned?"置顶":"取消置顶";}catch(Exception e){status.Text=e.Message;}};
        var save=new BarcaButton{Text="记下 / 保存",Bounds=new(286,520,140,34)};save.Click+=(_,_)=>SaveNote();FormClosing+=(_,e)=>{if(!ConfirmPending())e.Cancel=true;else discardOnClose=true;};date.Bounds=new(438,522,166,30);turn.Bounds=new(616,520,160,34);turn.Click+=(_,_)=>{try{var n=SaveNote();if(n==null)return;if(n.TaskId!=null){status.Text="这条便签已经转为待办。";return;}if(!pendingLinks.TryGetValue(n.Id,out string? id)){id=convert(n,DateOnly.FromDateTime(date.Value));pendingLinks[n.Id]=id;}store.Link(n.Id,id);pendingLinks.Remove(n.Id);Reload(n.Id);turn.Enabled=false;status.Text="已加入 "+date.Value.ToString("yyyy-MM-dd")+" 的清单；原文可在“已转待办”查看。";}catch(OperationCanceledException){status.Text="已取消转为待办，原文保留。";}catch(Exception e){status.Text=e.Message;}};
        status.Bounds=new(286,566,490,42);status.ForeColor=BarcaUi.Muted;status.Font=BarcaUi.Font(9);Controls.AddRange([search,filter,list,input,fresh,remove,pin,save,date,turn,status]);MinimumSize=MaximumSize=Size;store.Changed+=StoreChanged;Reload();New();theme=new(this);
    }
    void StoreChanged(){if(IsDisposed||disposed)return;if(InvokeRequired){try{BeginInvoke((Action)StoreChanged);}catch(InvalidOperationException){}return;}FeatureFaults.Run("便签列表",()=>Reload(selected));}
    public void New(string text=""){if(!ConfirmPending())return;selected=null;changing=true;list.ClearSelected();changing=false;input.Text=text;turn.Enabled=true;pin.Text="置顶";status.Text="Ctrl+Enter快速记下 · 无需填写日期";input.Focus();}
    bool Dirty=>input.Text.Trim()!=(store.Items.FirstOrDefault(n=>n.Id==selected)?.Text??"");
    bool ConfirmPending(){if(!Dirty)return true;using var confirm=new PendingNoteForm();var answer=confirm.ShowDialog(this);return answer==DialogResult.No||answer==DialogResult.Yes&&SaveNote()!=null;}
    protected override void Dispose(bool disposing){if(disposing&&!disposed){disposed=true;theme.Dispose();store.Changed-=StoreChanged;try{if(!discardOnClose&&Dirty)store.Save(selected,input.Text);}catch{}store.Dispose();}base.Dispose(disposing);}
    StickyNote? SaveNote(){try{var n=store.Save(selected,input.Text);selected=n.Id;Reload(n.Id);status.Text="已保存到本机";return n;}catch(Exception e){status.Text=e.Message;return null;}}
    void Reload(string? id=null){changing=true;list.Items.Clear();list.Items.AddRange(store.Query(search.Text,(filter.SelectedItem as ComboChoice)?.Value??"active").ToArray());list.SelectedItem=list.Items.Cast<StickyNote>().FirstOrDefault(n=>n.Id==id);changing=false;}
    protected override bool ProcessCmdKey(ref Message message,Keys key){if(key==(Keys.Control|Keys.Enter)||key==(Keys.Control|Keys.S)){SaveNote();return true;}if(key==(Keys.Control|Keys.F)){search.Focus();search.SelectAll();return true;}if(key==(Keys.Control|Keys.N)){New();return true;}return base.ProcessCmdKey(ref message,key);}
}
sealed partial class PetForm
{
    readonly object noteGate=new();readonly Dictionary<string,string> pendingNoteLinks=new();
    NoteStore? noteStore;NotesForm? notesWindow;
    NoteStore Notes{get{lock(noteGate)return noteStore??=new(previewMode?null:Path.Combine(Store.Root,"leo-notes.json"));}}
    void ShowNotes(string initial="")=>RunNotesEntry("notes_open",initial,"notes-entry");
    void ShowNotesCore(string initial="")
    {
        RevealDock(true);launcher.Hide();if(notesWindow==null||notesWindow.IsDisposed){notesWindow=new NotesForm(new NotesClient(Notes,commands),(note,day)=>{using var confirm=new QuickTextForm("便签转待办",note.Text,"确认标题后加入 "+day.ToString("yyyy-MM-dd")+" 的清单，原便签保留。");WindowPositions.Prepare(confirm,"note-todo",Bounds,previewMode);if(confirm.ShowDialog()!=DialogResult.OK)throw new OperationCanceledException("已取消转为待办");var task=todoClient.From("note-conversion").Add(confirm.Value,day,null,note.Id);todoWindow?.View.Render();return task.Id;},pendingNoteLinks);notesWindow.FormClosed+=(_,_)=>presentation.Invalidate(ModuleWindowTarget.Notes);}if(!notesWindow.Visible)WindowPositions.Prepare(notesWindow,"notes",Bounds,previewMode);if(initial.Length>0)notesWindow.New(initial);WindowInteraction.Show(notesWindow);
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
