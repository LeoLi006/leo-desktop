namespace LeoLauncher;

sealed partial class TodoView
{
    void WireTaskMenu(Control row,DailyTask task)
    {
        row.Tag=task.Id;var menu=new ContextMenuStrip();BarcaMenus.Apply(menu);
        void Item(string text,Action action,bool enabled=true){var item=new ToolStripMenuItem(text){Enabled=enabled};item.Click+=(_,_)=>Run(()=>{action();if(IsHandleCreated)BeginInvoke((Action)Render);else Render();});menu.Items.Add(item);}
        Item(task.Completed==null?"标记完成":"恢复待完成",()=>store.Toggle(task.Id));
        Item(task.SeriesId==null?"修改标题…":"修改本次标题…",()=>EditTitle(task));
        menu.Items.Add(new ToolStripSeparator());
        Item("移到明天",()=>store.MoveTo(task.Id,DateOnly.FromDateTime(DateTime.Today).AddDays(1)));
        Item("选择日期…",()=>PickTaskDate(task));
        Item("设置提醒…",()=>EditReminder(task));
        if(task.SeriesId!=null)Item("仅跳过本次",()=>store.SkipOccurrence(task.Id),task.Completed==null);
        menu.Items.Add(new ToolStripSeparator());Item("删除事项",()=>store.Delete(task.Id));Item("撤销上一步",()=>store.Undo(),store.CanUndo);
        void Attach(Control control){control.ContextMenuStrip=menu;foreach(Control child in control.Controls)Attach(child);}
        Attach(row);row.Disposed+=(_,_)=>menu.Dispose();
    }
    void PickTaskDate(DailyTask task)
    {
        using var dialog=new BarcaWindow{Text="Leo · 选择日期",ClientSize=new Size(292,490),StartPosition=FormStartPosition.CenterParent};
        dialog.Controls.Add(BarcaUi.Chrome(dialog,"改期","仅修改这一次，提醒同步移动。"));
        var dates=new PlannerCalendar{Bounds=new Rectangle(20,101,252,324)};dates.Update(task.Day,false,store.Items);dates.GoTo(task.Day);var chosen=task.Day;dates.DateSelected+=date=>chosen=date;
        var error=new Label{Bounds=new Rectangle(20,439,125,40),ForeColor=Color.LightSalmon,Font=BarcaUi.Font(8)};dialog.Controls.Add(error);
        var confirm=new BarcaButton{Text="确认改期",Bounds=new Rectangle(153,441,119,33)};confirm.Click+=(_,_)=>{try{store.MoveTo(task.Id,chosen);dialog.DialogResult=DialogResult.OK;}catch(Exception e){error.Text=e.Message;}};dialog.Controls.AddRange([dates,confirm]);dialog.AcceptButton=confirm;dialog.ShowDialog(FindForm());
    }
}
