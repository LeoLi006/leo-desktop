using System.Text.RegularExpressions;
namespace LeoLauncher;
sealed record OptionalEntry(string Id,string Name,bool Enabled,string Hint);
static class EntryDisplay
{
    public const string Shortcut="shortcut",Menu="menu",Search="search";
    public static readonly (string Value,string Label)[] Modes=[(Shortcut,"快捷栏显示"),(Menu,"仅菜单显示"),(Search,"仅搜索调用")];
    public static string Mode(Preferences p,string id)=>p.EntryDisplayModes?.TryGetValue(id,out var value)==true&&Modes.Any(m=>m.Value==value)?value:Shortcut;
    public static bool ShowShortcut(Preferences p,string id)=>Mode(p,id)==Shortcut;
    public static bool ShowMenu(Preferences p,string id)=>Mode(p,id)!=Search;
    public static void Validate(Dictionary<string,string>? modes){if(modes==null||modes.Count>512||modes.Any(p=>p.Key.Length is <1 or >100||!Regex.IsMatch(p.Key,@"^[A-Za-z0-9:_-]+$")||!Modes.Any(m=>m.Value==p.Value)))throw new ArgumentException("入口显示配置无效，请重新选择显示方式。");}
    public static OptionalEntry[] Entries(Preferences p)=>EntryContributions.ReadBuiltins(p).Select(e=>new OptionalEntry(e.Id,e.SettingsName,e.Enabled,e.Hint))
        .OrderBy(e=>Array.IndexOf(new[]{"terminal","clash","music","edge","football","media"},e.Id))
        .Concat(EntryContributions.ReadAvailable(p).Select(a=>new OptionalEntry(a.Id,a.Name,a.Enabled,"搜索应用名："+a.Name))).ToArray();
}
sealed class EntryDisplayForm:BarcaWindow
{
    readonly Dictionary<string,ComboBox> fields=new();readonly Dictionary<string,string> original;
    public Dictionary<string,string> Result{get;private set;}=new();
    public EntryDisplayForm(Preferences prefs)
    {
        original=new(prefs.EntryDisplayModes??new());Text="Leo · 入口显示";ClientSize=new(770,590);StartPosition=FormStartPosition.CenterParent;Controls.Add(BarcaUi.Chrome(this,"入口显示","隐藏入口保留功能；模块是否启用，在功能设置中单独控制。"));
        var scroll=new Panel{Bounds=new(22,108,726,346),AutoScroll=true};int y=0;
        foreach(var entry in EntryDisplay.Entries(prefs)){
            var title=new Label{Text=entry.Name,Bounds=new(8,y+4,305,25),AutoEllipsis=true,ForeColor=BarcaUi.Text,Font=BarcaUi.Font(10,true)};
            var enabled=new Label{Text=entry.Enabled?"已启用":"已停用",Bounds=new(320,y+5,77,24),ForeColor=entry.Enabled?BarcaUi.Gold:BarcaUi.Muted,Font=BarcaUi.Font(9)};
            var field=new ComboBox{Bounds=new(408,y,260,31),DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName=entry.Id+"入口显示"};CompanionUi.ThemeCombo(field);foreach(var mode in EntryDisplay.Modes)field.Items.Add(new ComboChoice(mode.Label,mode.Value));field.SelectedItem=field.Items.Cast<ComboChoice>().First(m=>m.Value==EntryDisplay.Mode(prefs,entry.Id));fields[entry.Id]=field;
            var hint=new Label{Text="搜索口令："+entry.Hint,Bounds=new(8,y+33,660,23),AutoEllipsis=true,ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f)};scroll.Controls.AddRange([title,enabled,field,hint]);y+=70;
        }
        scroll.AutoScrollMinSize=new(0,y+4);var note=new Label{Text="快捷栏显示：保留原位置的快捷按钮，也能从右键菜单进入。\n仅菜单显示：隐藏快捷按钮，保留右键入口。仅搜索调用：两种入口都隐藏。\n聊天、语音、组合、已有快捷键和内容操作仍可调用已启用能力。\n停用模块的显示选择会保留，重新启用后恢复。确认后还需保存设置。",Bounds=new(28,468,710,75),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f)};
        var save=new BarcaButton{Text="确定",Bounds=new(628,542,112,32)};save.Click+=(_,_)=>{try{Result=new(original);foreach(var field in fields)Result[field.Key]=((ComboChoice)field.Value.SelectedItem!).Value;EntryDisplay.Validate(Result);DialogResult=DialogResult.OK;}catch(Exception e){note.Text=e.Message;note.ForeColor=Color.LightSalmon;}};var cancel=new BarcaButton{Text="取消",Bounds=new(504,542,112,32),DialogResult=DialogResult.Cancel};AcceptButton=save;CancelButton=cancel;Controls.AddRange([scroll,note,cancel,save]);MinimumSize=MaximumSize=Size;
    }
}
sealed partial class PetForm
{
    void WireOptionalEntryMenus(ContextMenuStrip menu)
    {
        var parent=menu.Items.OfType<ToolStripMenuItem>().Single(i=>i.Text=="应用与媒体");
        menu.Opening+=(_,_)=>{
            foreach(var old in parent.DropDownItems.OfType<ToolStripMenuItem>().Where(i=>i.Tag as string=="optional-entry").ToArray()){parent.DropDownItems.Remove(old);old.Dispose();}
            ToolStripMenuItem Item(string title,string action,string value="",string owner=""){var item=new ToolStripMenuItem(title){Tag="optional-entry"};item.Click+=async(_,_)=>{try{if(owner.Length>0)EntryContributions.BuiltinAction(prefs,owner);var result=await commands.ExecuteAsync(new(action.StartsWith("local:",StringComparison.Ordinal)?EntryContributions.CurrentAction(prefs,action[6..]):action,value,"menu"));if(!IsDisposed)OperationFeedback.Show(result);}catch(OperationCanceledException){}catch(Exception e){if(!IsDisposed)OperationFeedback.Error(e.Message);}};return item;}
            foreach(var entry in EntryContributions.ReadBuiltins(prefs).Where(e=>e.Id is "football" or "terminal" or "clash" or "edge")){
                if(!entry.Menu)continue;
                if(entry.MenuActions.Length==1){var action=entry.MenuActions[0];parent.DropDownItems.Add(Item(entry.MenuName,action.ActionId,action.Value,entry.Id));}
                else{var group=new ToolStripMenuItem(entry.MenuName){Tag="optional-entry"};foreach(var action in entry.MenuActions)group.DropDownItems.Add(Item(action.Name,action.ActionId,action.Value,entry.Id));BarcaMenus.StyleSubmenu(group,menu.Font);parent.DropDownItems.Add(group);}
            }
            var apps=EntryContributions.ReadAvailable(prefs).Where(a=>a.Menu).ToArray();if(apps.Length>0){var root=new ToolStripMenuItem("自定义应用"){Tag="optional-entry"};foreach(var app in apps){var item=Item(app.Name,app.ActionId);root.DropDownItems.Add(item);}BarcaMenus.StyleSubmenu(root,menu.Font);parent.DropDownItems.Add(root);}
        };
    }
}
