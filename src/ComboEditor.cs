namespace LeoLauncher;

sealed record ComboChoice(string Name,string Value) { public override string ToString()=>Name; }
sealed class ComboEditor : BarcaWindow
{
    readonly List<ComboStep> steps;
    readonly ListBox list=new(){Bounds=new Rectangle(24,182,505,174),BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text,BorderStyle=BorderStyle.None,HorizontalScrollbar=true};
    public ComboEditor(ComboStore store,string? id,Func<IEnumerable<AppEntry>> apps,bool preview=false)
    {
        var old=store.Items.FirstOrDefault(i=>i.Id==id);steps=old?.Steps.ToList()??[];
        Text=old==null?"Leo · 新建自定义":"Leo · 编辑自定义";ClientSize=new Size(680,640);StartPosition=FormStartPosition.CenterScreen;TopMost=false;
        Controls.Add(BarcaUi.Chrome(this,old==null?"新建自定义":"编辑自定义","选好操作、排好顺序，之后一键执行。无需模型 API。"));
        var name=new TextBox{Bounds=new Rectangle(112,110,540,30),MaxLength=24,Text=old?.Name??"",PlaceholderText="例如：准备看球、晚上放松",BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text,BorderStyle=BorderStyle.FixedSingle};
        Controls.Add(new Label{Text="组合名称",Bounds=new Rectangle(24,113,85,25),ForeColor=BarcaUi.Gold});Controls.Add(name);
        Controls.Add(new Label{Text="执行顺序 · 最多12步",Bounds=new Rectangle(24,151,500,24),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)});Controls.Add(list);
        var status=new Label{Bounds=new Rectangle(24,535,628,46),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9),Text="失败时可重试剩余步骤，已完成的步骤不会重复执行。"};Controls.Add(status);
        void RefreshSteps(int selected=-1){list.Items.Clear();for(int i=0;i<steps.Count;i++)list.Items.Add((i+1)+". "+(steps[i].Label.Length>0?steps[i].Label:steps[i].Kind));if(steps.Count>0)list.SelectedIndex=Math.Clamp(selected,0,steps.Count-1);}
        void Move(int delta){int from=list.SelectedIndex,to=from+delta;if(from<0||to<0||to>=steps.Count)return;(steps[from],steps[to])=(steps[to],steps[from]);RefreshSteps(to);}
        foreach(var (text,y,action) in new (string,int,Action)[]{("上移",186,()=>Move(-1)),("下移",231,()=>Move(1)),("移除",276,()=>{int index=list.SelectedIndex;if(index<0)return;steps.RemoveAt(index);RefreshSteps(index);})}){var button=new BarcaButton{Text=text,Bounds=new Rectangle(547,y,105,33)};button.Click+=(_,_)=>action();Controls.Add(button);}
        Controls.Add(new Label{Text="选择操作和参数，再点“添加一步”",Bounds=new Rectangle(24,370,628,25),ForeColor=BarcaUi.Gold,Font=BarcaUi.Font(9.5f,true)});
        var kind=new ComboBox{Bounds=new Rectangle(24,407,166,30),DropDownStyle=ComboBoxStyle.DropDownList};
        var types=new[]{new ComboChoice("打开应用","app"),new("打开网址","url"),new("打开QQ音乐","qq_client"),new("开启系统代理","proxy_on"),new("关闭系统代理","proxy_off"),new("打开每日清单","todos"),new("打开足球窗口","football"),new("打开聊天窗口","chat"),new("打开观赛入口","watch")};kind.Items.AddRange(types);
        var value=new ComboBox{Bounds=new Rectangle(202,407,327,30),DropDownStyle=ComboBoxStyle.DropDown,AutoCompleteMode=AutoCompleteMode.SuggestAppend,AutoCompleteSource=AutoCompleteSource.ListItems};
        var url=new TextBox{Bounds=value.Bounds,PlaceholderText="填写网址，例如 https://…",BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text,BorderStyle=BorderStyle.FixedSingle,MaxLength=2048};
        var hint=new Label{Bounds=new Rectangle(24,451,505,65),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)};
        Controls.AddRange([kind,value,url,hint]);
        void Choices()
        {
            string type=((ComboChoice)kind.SelectedItem!).Value;value.Items.Clear();value.Text="";url.Visible=type=="url";value.Visible=type is "app" or "watch";
            if(type=="app")foreach(var app in apps().Where(a=>a.Target!=ClipboardTool.Target).OrderBy(a=>a.Name))value.Items.Add(new ComboChoice(app.Name,app.Target));
            if(type=="watch"){value.Items.Add(new ComboChoice("第一个已填写的观赛入口","-1"));var links=new WatchLinkStore(preview?null:Path.Combine(Store.Root,"leo-watch-links.json")).Items;for(int i=0;i<links.Length;i++)value.Items.Add(new ComboChoice(links[i].Name,i.ToString()));}
            if(value.Items.Count>0)value.SelectedIndex=0;
            hint.Text=type=="app"?"可输入应用名查找，然后从下拉列表选择。若列表为空，先打开一次 Leo 应用搜索，再回来选择。":type=="url"?"在 Edge 中打开网址。可添加多条网址或应用操作。":type.StartsWith("proxy")?"Clash 未运行时，开启操作会自动启动它。关闭操作不会启动客户端。":"操作按列表从上到下依次执行。";
        }
        kind.SelectedIndexChanged+=(_,_)=>Choices();kind.SelectedIndex=0;
        ComboStep ReadStep()
        {
            var type=(ComboChoice)kind.SelectedItem!;
            if(type.Value=="url"){string normalized=WatchLinkStore.Normalize(new("网址",url.Text),0).Url;if(normalized.Length==0)throw new ArgumentException("请填写网址");return new("url",normalized,normalized.Length>90?normalized[..90]+"…":normalized);}
            if(value.Visible){var choice=value.SelectedItem as ComboChoice??value.Items.Cast<ComboChoice>().FirstOrDefault(i=>i.Name==value.Text);if(choice==null)throw new ArgumentException("请从下拉列表选择有效项目");return new(type.Value,choice.Value,type.Name+" · "+choice.Name);}
            return new(type.Value,Label:type.Name);
        }
        var add=new BarcaButton{Text="添加一步",Bounds=new Rectangle(547,403,105,34),ForeColor=BarcaUi.Gold};add.Click+=(_,_)=>{try{if(steps.Count>=12)throw new ArgumentException("最多12步");steps.Add(ReadStep());RefreshSteps(steps.Count-1);status.Text="已添加，保存后生效。";}catch(Exception e){status.Text=e.Message;}};Controls.Add(add);
        var replace=new BarcaButton{Text="替换选中",Bounds=new Rectangle(547,450,105,34)};replace.Click+=(_,_)=>{try{int index=list.SelectedIndex;if(index<0)throw new ArgumentException("请先选择要替换的步骤");steps[index]=ReadStep();RefreshSteps(index);}catch(Exception e){status.Text=e.Message;}};Controls.Add(replace);
        list.SelectedIndexChanged+=(_,_)=>
        {
            if(list.SelectedIndex<0)return;var step=steps[list.SelectedIndex];int index=Array.FindIndex(types,t=>t.Value==step.Kind);if(index<0)return;kind.SelectedIndex=index;Choices();
            if(step.Kind=="url")url.Text=step.Value;else if(value.Visible){var choice=value.Items.Cast<ComboChoice>().FirstOrDefault(c=>c.Value==step.Value);if(choice!=null)value.SelectedItem=choice;else value.Text=step.Label;}
        };
        var save=new BarcaButton{Text="保存组合",Bounds=new Rectangle(540,591,112,34),ForeColor=BarcaUi.Gold};save.Click+=(_,_)=>{try{store.Put(new(old?.Id??Guid.NewGuid().ToString("N"),name.Text,steps.ToList()));if(!preview)OperationFeedback.Show("自定义组合已保存");DialogResult=DialogResult.OK;Close();}catch(Exception e){status.Text=e.Message;}};
        var cancel=new BarcaButton{Text="取消",Bounds=new Rectangle(418,591,106,34),DialogResult=DialogResult.Cancel};Controls.AddRange([save,cancel]);CancelButton=cancel;
        if(old!=null){var delete=new BarcaButton{Text="删除组合",Bounds=new Rectangle(24,591,110,34),ForeColor=Color.LightSalmon};delete.Click+=(_,_)=>{if(MessageBox.Show(this,"删除“"+old.Name+"”？","删除组合",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;try{store.Delete(old.Id);DialogResult=DialogResult.OK;Close();}catch(Exception e){status.Text=e.Message;}};Controls.Add(delete);}
        RefreshSteps();MinimumSize=MaximumSize=Size;
    }
}

sealed class SceneSettings : Panel
{
    public SceneSettings(bool preview,Func<IEnumerable<AppEntry>>? applications=null)
    {
        Dock=DockStyle.Fill;
        Controls.Add(new Label{Text="自定义组合",Bounds=new Rectangle(8,12,550,28),ForeColor=BarcaUi.Gold,Font=BarcaUi.Font(11,true)});
        Controls.Add(new Label{Text="右键 Leo → 自定义，悬停后选择组合。",Bounds=new Rectangle(8,48,560,26),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)});
        var rows=new FlowLayoutPanel{Bounds=new Rectangle(8,88,560,205),AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};Controls.Add(rows);
        void Edit(string? id){using var editor=new ComboEditor(new ComboStore(preview?null:ComboStore.FilePath),id,applications??(()=>[]),preview);var owner=FindForm();var anchor=owner?.Bounds??new Rectangle(Cursor.Position,new Size(1,1));editor.StartPosition=FormStartPosition.Manual;editor.Location=PopupPlacement.Beside(anchor,editor.Size,Screen.FromRectangle(anchor).WorkingArea);editor.ShowDialog(owner);Refresh();}
        void Refresh(){foreach(Control c in rows.Controls.Cast<Control>().ToArray())c.Dispose();var store=new ComboStore(preview?null:ComboStore.FilePath);foreach(var combo in store.Items){var row=new Panel{Size=new Size(530,45),BackColor=BarcaUi.Surface};row.Controls.Add(new Label{Text=combo.Name+"  ·  "+combo.Steps.Count+"步",Bounds=new Rectangle(12,11,390,25),ForeColor=BarcaUi.Text});var edit=new BarcaButton{Text="编辑",Bounds=new Rectangle(437,7,75,30)};string id=combo.Id;edit.Click+=(_,_)=>Edit(id);row.Controls.Add(edit);rows.Controls.Add(row);}if(store.Items.Count==0)rows.Controls.Add(new Label{Text=store.Error??"暂无组合，点击下方新建。",Size=new Size(520,50),ForeColor=BarcaUi.Muted});}
        var create=new BarcaButton{Text="＋ 新建自定义",Bounds=new Rectangle(8,310,155,34),ForeColor=BarcaUi.Gold};create.Click+=(_,_)=>Edit(null);Controls.Add(create);Refresh();
    }
}
