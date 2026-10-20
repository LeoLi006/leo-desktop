namespace LeoLauncher;
static class ShortcutOrder
{
    public static string[] Arrange(IEnumerable<string> ids,IEnumerable<string>? saved)=> (saved??[]).Concat(ids).Distinct(StringComparer.OrdinalIgnoreCase).Where(id=>ids.Contains(id,StringComparer.OrdinalIgnoreCase)).ToArray();
    public static bool Move(Preferences p,string source,string target,bool after,IEnumerable<string> available){var list=Arrange(available.Concat(p.ShortcutOrder),p.ShortcutOrder).ToList();if(!FavoriteSorting.Move(list,source,target,after))return false;p.ShortcutOrder=list;return true;}
}
sealed partial class LauncherForm
{
    readonly ToolTip shortcutTips=new();readonly GlyphButton shortcutMore=new("more");readonly ContextMenuStrip shortcutOverflowMenu=new();
    readonly Dictionary<string,(string Title,Action Run)> shortcutActions=new();bool shortcutDragging,ignoreShortcutClick;Point shortcutDown;string? draggedShortcut;
    const string ShortcutFormat="LeoShortcut53";
    void InitializeShortcutBar(Panel viewport)
    {
        BarcaMenus.Apply(shortcutOverflowMenu);shortcutRow.AutoScroll=false;shortcutRow.Height=30;shortcutRow.AllowDrop=true;shortcutMore.AccessibleName="更多快捷功能";shortcutMore.Size=new(28,24);shortcutMore.Top=3;viewport.Controls.Add(shortcutMore);shortcutMore.Click+=(_,_)=>ShowShortcutOverflow();viewport.SizeChanged+=(_,_)=>LayoutShortcutOverflow();
        shortcutRow.DragEnter+=(_,e)=>ShortcutDragEnter(e);shortcutRow.DragOver+=(_,e)=>ShortcutDragEnter(e);shortcutRow.DragDrop+=(_,e)=>{if(e.Data?.GetData(DataFormats.FileDrop) is string[] paths)ApplicationsDropped?.Invoke(paths);};
        shortcutMore.AllowDrop=true;shortcutMore.DragEnter+=(_,e)=>{if(e.Data?.GetDataPresent(ShortcutFormat)==true){e.Effect=DragDropEffects.Move;ShowShortcutOverflow();}};
        shortcutOverflowMenu.Closed+=(_,_)=>{menuOpen=false;shortcutDragging=false;};
    }
    void ShortcutDragEnter(DragEventArgs e){e.Effect=e.Data?.GetDataPresent(ShortcutFormat)==true?DragDropEffects.Move:e.Data?.GetData(DataFormats.FileDrop) is string[] paths&&paths.Any(ApplicationDrop.IsProgram)?DragDropEffects.Copy:DragDropEffects.None;}
    void WireShortcut(Button button,string id)
    {
        button.Tag=id;button.AllowDrop=true;ShortcutManagement.Wire(button,id,key=>ManageShortcut?.Invoke(key));
        button.MouseDown+=(_,e)=>{if(e.Button==MouseButtons.Left){shortcutDown=e.Location;draggedShortcut=id;}};
        button.MouseMove+=(_,e)=>{if(e.Button!=MouseButtons.Left||draggedShortcut!=id||Math.Abs(e.X-shortcutDown.X)+Math.Abs(e.Y-shortcutDown.Y)<8)return;shortcutDragging=true;sorting=true;ignoreShortcutClick=true;try{var data=new DataObject();data.SetData(ShortcutFormat,id);button.DoDragDrop(data,DragDropEffects.Move);}finally{shortcutDragging=false;sorting=false;draggedShortcut=null;BeginInvoke((Action)(()=>{ignoreShortcutClick=false;RefreshShortcuts();}));}};
        button.DragEnter+=(_,e)=>ShortcutDragEnter(e);button.DragOver+=(_,e)=>ShortcutDragEnter(e);button.DragDrop+=(_,e)=>{if(e.Data?.GetData(ShortcutFormat) is string source){bool after=button.PointToClient(new Point(e.X,e.Y)).X>button.Width/2;if(ShortcutOrder.Move(prefs,source,id,after,shortcutActions.Keys)){if(!preview)Store.Save(prefs);if(!shortcutDragging)BeginInvoke((Action)RefreshShortcuts);}}else if(e.Data?.GetData(DataFormats.FileDrop) is string[] files)ApplicationsDropped?.Invoke(files);};
    }
    void LayoutShortcutOverflow()
    {
        if(shortcutRow?.Parent==null)return;shortcutRow.SuspendLayout();int capacity=Math.Max(1,shortcutRow.Width/32),i=0;foreach(Control button in shortcutRow.Controls)button.Visible=i++<capacity;shortcutRow.ResumeLayout();shortcutMore.Left=shortcutRow.Parent.Width-30;shortcutMore.Visible=shortcutRow.Controls.Count>capacity;
    }
    void ShowShortcutOverflow()
    {
        foreach(ToolStripItem old in shortcutOverflowMenu.Items.Cast<ToolStripItem>().ToArray())old.Dispose();shortcutOverflowMenu.Items.Clear();var hidden=shortcutRow.Controls.Cast<Control>().Where(c=>!c.Visible&&c.Tag is string).Select(c=>(string)c.Tag!).ToArray();
        foreach(var id in hidden){if(!shortcutActions.TryGetValue(id,out var item))continue;var entry=new ToolStripMenuItem(item.Title){Tag=id};entry.Click+=(_,_)=>RunShortcut(id);entry.MouseUp+=(_,e)=>{if(e.Button==MouseButtons.Right){shortcutOverflowMenu.Close();ManageShortcut?.Invoke(id);}};shortcutOverflowMenu.Items.Add(entry);}
        if(hidden.Length>0)shortcutOverflowMenu.Items.Add(new ToolStripSeparator());var order=new ToolStripMenuItem("调整快捷顺序…");order.Click+=(_,_)=>{using var editor=new ShortcutOrderForm(prefs.ShortcutOrder,shortcutActions.Select(p=>(p.Key,p.Value.Title)).ToArray());if(editor.ShowDialog(this)==DialogResult.OK){prefs.ShortcutOrder=editor.Order.Concat(prefs.ShortcutOrder).Distinct().ToList();if(!preview)Store.Save(prefs);RefreshShortcuts();}};shortcutOverflowMenu.Items.Add(order);BarcaMenus.Fit(shortcutOverflowMenu);menuOpen=true;shortcutOverflowMenu.Show(shortcutMore,new Point(0,shortcutMore.Height));
    }
    void RunShortcut(string id){if(ignoreShortcutClick||shortcutDragging)return;try{shortcutActions[id].Run();}catch(Exception e){OperationFeedback.Error(e.Message);}}
    void RefreshShortcuts()
    {
        if(shortcutRow==null)return;foreach(Control old in shortcutRow.Controls.Cast<Control>().ToArray()){shortcutRow.Controls.Remove(old);if(old!=footballShortcut)old.Dispose();}shortcutActions.Clear();
        var buttons=new Dictionary<string,Button>();
        void Add(string id,string name,string glyph,bool enabled,Action action,string? iconPath=null){if(!enabled)return;var button=new GlyphButton(glyph){Width=28,Height=24,Margin=new Padding(2,3,2,0),AccessibleName=name,FlatStyle=FlatStyle.Flat};button.FlatAppearance.BorderSize=0;var image=LocalApplications.Icon(iconPath??prefs.ModuleIcons.GetValueOrDefault(id));if(image!=null)button.Image=image;button.Disposed+=(_,_)=>button.Image?.Dispose();button.Click+=(_,_)=>RunShortcut(id);shortcutTips.SetToolTip(button,name+" · 拖动排序");shortcutActions[id]=(name,action);WireShortcut(button,id);buttons[id]=button;}
        if(prefs.FootballModule&&EntryDisplay.ShowShortcut(prefs,"football")){var previousIcon=footballShortcut.Image;footballShortcut.Image=LocalApplications.Icon(prefs.ModuleIcons.GetValueOrDefault("football"));previousIcon?.Dispose();footballShortcut.Visible=true;footballShortcut.Margin=new Padding(2,3,2,0);shortcutActions["football"]=("足球赛程",openFootball);footballShortcut.Tag="football";if(footballShortcut.AllowDrop==false){WireShortcut(footballShortcut,"football");footballShortcut.Disposed+=(_,_)=>footballShortcut.Image?.Dispose();}buttons["football"]=footballShortcut;}
        Add("music","QQ音乐点歌","music",prefs.MusicModule&&EntryDisplay.ShowShortcut(prefs,"music"),()=>OpenMusicFromAssistant());Add("media","PotPlayer · 媒体控制","play",prefs.MediaModule&&EntryDisplay.ShowShortcut(prefs,"media"),()=>MediaRequested?.Invoke());
        foreach(var app in EntryContributions.Read(prefs))Add(app.Id,app.Name,"terminal",app.Shortcut,async()=>{try{if(OriginActionRequested!=null)await OriginActionRequested(EntryContributions.CurrentAction(prefs,app.Id),"","shortcut",CancellationToken.None);else if(ActionRequested!=null)await ActionRequested(EntryContributions.CurrentAction(prefs,app.Id),"",CancellationToken.None);else if(!preview)throw new InvalidOperationException("应用启动入口未连接");}catch(OperationCanceledException){}catch(Exception e){OperationFeedback.Show(e.Message);}},app.Icon);
        foreach(string id in ShortcutOrder.Arrange(buttons.Keys,prefs.ShortcutOrder))shortcutRow.Controls.Add(buttons[id]);LayoutShortcutOverflow();
    }
}
sealed class ShortcutOrderForm:BarcaWindow
{
    readonly ListBox list=new();public List<string> Order=>list.Items.Cast<ComboChoice>().Select(c=>c.Value).ToList();Point down;
    public ShortcutOrderForm(List<string> order,(string Id,string Title)[] entries)
    {
        Text="快捷按钮顺序";ClientSize=new(460,430);StartPosition=FormStartPosition.CenterParent;Controls.Add(BarcaUi.Chrome(this,"快捷按钮顺序","拖动条目排序，也可使用上下移动。"));list.Bounds=new(24,105,412,230);list.BackColor=BarcaUi.Raised;list.ForeColor=BarcaUi.Text;foreach(string id in ShortcutOrder.Arrange(entries.Select(e=>e.Id),order))list.Items.Add(new ComboChoice(entries.First(e=>e.Id==id).Title,id));list.AllowDrop=true;list.MouseDown+=(_,e)=>down=e.Location;list.MouseMove+=(_,e)=>{if(e.Button==MouseButtons.Left&&list.SelectedIndex>=0&&Math.Abs(e.Y-down.Y)>8){var payload=new DataObject();payload.SetData("LeoShortcutOrderRow53",((ComboChoice)list.SelectedItem!).Value);list.DoDragDrop(payload,DragDropEffects.Move);}};list.DragOver+=(_,e)=>e.Effect=e.Data?.GetDataPresent("LeoShortcutOrderRow53")==true?DragDropEffects.Move:DragDropEffects.None;list.DragDrop+=(_,e)=>{if(e.Data?.GetData("LeoShortcutOrderRow53") is not string sourceId)return;var source=list.Items.Cast<ComboChoice>().FirstOrDefault(c=>c.Value==sourceId);if(source==null)return;int destination=list.IndexFromPoint(list.PointToClient(new(e.X,e.Y)));int from=list.Items.IndexOf(source);if(from<0)return;list.Items.RemoveAt(from);list.Items.Insert(Math.Clamp(destination,0,list.Items.Count),source);list.SelectedItem=source;};
        var up=new BarcaButton{Text="上移",Bounds=new(24,359,100,34)};var next=new BarcaButton{Text="下移",Bounds=new(134,359,100,34)};void Move(int delta){int i=list.SelectedIndex,j=i+delta;if(i<0||j<0||j>=list.Items.Count)return;var value=list.Items[i];list.Items.RemoveAt(i);list.Items.Insert(j,value);list.SelectedIndex=j;}up.Click+=(_,_)=>Move(-1);next.Click+=(_,_)=>Move(1);var save=new BarcaButton{Text="保存",Bounds=new(336,359,100,34),DialogResult=DialogResult.OK};AcceptButton=save;SelectionKeys.Wire(list,()=>save.PerformClick());Controls.AddRange([list,up,next,save]);MinimumSize=MaximumSize=Size;
    }
}
