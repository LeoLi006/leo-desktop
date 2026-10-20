namespace LeoLauncher;
sealed class TodoForm:BarcaWindow
{
    readonly ModuleThemeBinding theme;
    readonly TodoClient store;readonly TodoView view;
    public int Count=>store.Items.Count;
    public int CompletedCount=>store.Items.Count(t=>t.Completed!=null);
    public TodoView View=>view;
    public TodoForm(bool preview=false,TodoStore? shared=null){var data=shared??new TodoStore(preview?null:Path.Combine(Store.Root,"leo-todos.json"));store=data.Client;view=new TodoView(data);Text="Leo · 清单";StartPosition=FormStartPosition.Manual;ClientSize=new Size(920,660);BackColor=BarcaUi.Canvas;Font=BarcaUi.Font(10);TopMost=false;ShowInTaskbar=true;Controls.Add(view);Controls.Add(BarcaUi.Chrome(this,"LEO  /  清单","日历安排 · 每日 / 每周清单"));CanResize=false;MaximizeBox=false;MinimumSize=Size;MaximumSize=Size;store.Changed+=Reload;theme=new(this);}
    bool refreshQueued;
    void Reload(){if(IsDisposed||!IsHandleCreated||refreshQueued)return;refreshQueued=true;try{BeginInvoke((Action)(()=>{refreshQueued=false;if(!IsDisposed)view.Render();}));}catch(InvalidOperationException){refreshQueued=false;}}
    protected override void Dispose(bool disposing){if(disposing){theme.Dispose();store.Changed-=Reload;}base.Dispose(disposing);}
    public void Apply(QuickIntent intent){var day=DateOnly.FromDateTime(DateTime.Today).AddDays(intent.DayOffset);if(intent.Kind==QuickKind.AddTodo)store.Add(intent.Value,day);if(intent.Kind==QuickKind.CompleteTodo){var matches=store.ForDay(day).Where(t=>t.Completed==null&&t.Title.Equals(intent.Value,StringComparison.OrdinalIgnoreCase)).ToArray();if(matches.Length!=1)throw new ArgumentException("没有唯一匹配的未完成事项，请在清单中选择。");store.Toggle(matches[0].Id);}view.ShowDay(day);}
}
