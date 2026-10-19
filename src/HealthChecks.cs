namespace LeoLauncher;

sealed class HealthChecksView:Panel
{
    readonly Label result=new(){AutoSize=true,MaximumSize=new Size(560,0),Font=BarcaUi.Font(9.5f),ForeColor=BarcaUi.Text,Location=new Point(10,10)};
    public Action? StartupFixed,RepairPaths,RepairHotkeys,ShowModuleStatus;
    readonly BarcaButton startup=new(){Text="启用 / 修复启动",Size=new Size(150,32)},paths=new(){Text="修复软件路径…",Size=new Size(150,32)},shortcuts=new(){Text="处理快捷键冲突…",Size=new Size(165,32)};
    readonly Preferences prefs;readonly bool preview;readonly Func<string>? hotkeys;
    readonly CancellationTokenSource lifetime=new();
    public HealthChecksView(Preferences current,bool demo,Func<string>? shortcutState)
    {
        prefs=current;preview=demo;hotkeys=shortcutState;Dock=DockStyle.Fill;
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};scroll.Controls.Add(result);scroll.Resize+=(_,_)=>result.MaximumSize=new Size(Math.Max(120,scroll.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-20),0);
        var tools=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=86};
        var refresh=new BarcaButton{Text="重新检查",Size=new Size(110,32)};var api=new BarcaButton{Text="检测 API 连接",Size=new Size(145,32)};
        var moduleStatus=new BarcaButton{Text="模块状态与处理…",Size=new Size(165,32)};moduleStatus.Click+=(_,_)=>ShowModuleStatus?.Invoke();tools.Height=120;tools.Controls.AddRange([moduleStatus,refresh,startup,api,paths,shortcuts]);paths.Click+=(_,_)=>{RepairPaths?.Invoke();Check();};shortcuts.Click+=(_,_)=>RepairHotkeys?.Invoke();Controls.Add(scroll);Controls.Add(tools);
        refresh.Click+=(_,_)=>Check();startup.Click+=(_,_)=>{try{if(!preview){LeoStartup.SetEnabled(true);StartupFixed?.Invoke();}Check();}catch{result.Text="开机启动写入失败，请检查启动文件夹权限。";}};
        api.Click+=async(_,_)=>{api.Enabled=false;try{if(preview){result.Text="示例模式，不联网。";return;}var settings=ChatSettings.Load();using var client=new ChatApi();var models=await client.ModelsAsync(settings,settings.ReadKey(),lifetime.Token);if(!IsDisposed)result.Text+="\n\n✓ API 连接成功，模型列表 "+models.Length+" 项。\n此检查不生成回复，不计入模型请求限额。";}catch(OperationCanceledException){}catch{if(!IsDisposed)result.Text+="\n\nAPI 检测失败，请检查接口、Key、权限和网络；未生成模型回复。";}finally{if(!IsDisposed)api.Enabled=true;}};
        VisibleChanged+=(_,_)=>{if(Visible)Check();};Check();
    }
    void Check()
    {
        using var passive=ModulePaths.PassiveRead();
        if(preview){startup.Enabled=paths.Enabled=shortcuts.Enabled=false;result.Text="示例模式 · 不修改启动项，不启动外部软件，不联网。";return;}
        startup.Enabled=!LeoStartup.Enabled;shortcuts.Enabled=hotkeys?.Invoke().Contains("被占用")==true;paths.Enabled=false;
        var lines=new List<string>();
        lines.Add((LeoStartup.Enabled?"✓":"○")+" 开机启动："+(LeoStartup.Enabled?"已启用":"未启用或被 Windows 停用"));
        try{string log=Path.Combine(Store.Root,"startup-last.log");if(File.Exists(log))lines.Add("  上次登录："+File.ReadLines(log).LastOrDefault());}catch{}
        lines.Add("快捷键："+(hotkeys?.Invoke()??"请在快捷键页检查配置"));
        lines.Add((LocalAsr.Installed?"✓":"○")+" 本地语音模型："+(LocalAsr.Installed?"完整":"缺少 SenseVoice 文件"));
        foreach(var module in ModuleStatuses.Read(prefs)){lines.Add((module.State==ModuleState.Ready?"✓ ":"○ ")+module.Name+"："+module.Label+" · "+module.Detail);if(module.State is ModuleState.Invalid or ModuleState.NotInstalled)paths.Enabled=true;}
        try{string file=Path.Combine(Store.Root,".health-"+Guid.NewGuid().ToString("N")+".tmp");File.WriteAllText(file,"Leo");File.Delete(file);lines.Add("✓ 个人配置目录：可写");}catch{lines.Add("○ 个人配置目录：无法写入，设置可能无法保存");}
        var config=ChatSettings.Load();lines.Add("API 配置："+(config.ReadKey().Length>0&&config.Model.Length>0?"已填写（连接请手动检测）":"尚未完整填写"));
        try{var used=ApiUsageLedger.Shared.Today();var limit=ApiUsageConfig.Load().DailyLimit;lines.Add("今日模型请求："+used.Requests+" / "+(limit==0?"不限":limit));}catch{lines.Add("○ API 用量配置或记录无法读取，模型调用暂停");}
        result.Text=string.Join("\n\n",lines);
    }
    protected override void Dispose(bool disposing){if(disposing){lifetime.Cancel();lifetime.Dispose();}base.Dispose(disposing);}
}

sealed class ModulesView:Panel
{
    readonly Dictionary<string,CheckBox> fields=new();
    public Action? ShowModuleStatus;
    public readonly CheckBox Docking;
    Dictionary<string,string> selectedPaths;
    List<LocalApplication> applicationModules;Dictionary<string,string> moduleIcons;Dictionary<string,string> entryDisplayModes;
    public ModulesView(Preferences p,bool preview=false)
    {
        entryDisplayModes=new(p.EntryDisplayModes??new());selectedPaths=new(p.ModuleExecutables);applicationModules=(p.ApplicationModules??[]).ToList();moduleIcons=new(p.ModuleIcons??new());
        Dock=DockStyle.Fill;AutoScroll=true;AutoScrollMinSize=new Size(0,585);
        Controls.Add(new Label{Text="按需开启 · 图标、菜单与后台检查同步变化",Bounds=new Rectangle(8,10,550,30),Font=BarcaUi.Font(10,true),ForeColor=BarcaUi.Gold});
        var statusButton=new BarcaButton{Text="模块状态与处理…",Bounds=new Rectangle(10,542,535,32)};statusButton.Click+=(_,_)=>ShowModuleStatus?.Invoke();Controls.Add(statusButton);
        int y=50;
        foreach(var (id,title,note) in new[]{("terminal","管理员终端","打开 Windows 管理员终端"),("clash","Clash Verge","代理开关与状态检查"),("music","QQ 音乐","快速点歌、播放控制和歌词快捷键"),("edge","Edge 搜索与观赛网址","桌宠搜索按钮和浏览器网址入口"),("football","足球","赛程、球队关注、积分榜和赛程后台更新"),("media","统一媒体控制","PotPlayer与Windows媒体会话的播放、暂停和切换")}){
            var field=new CheckBox{Text=title,Checked=Modules.Enabled(p,id),Bounds=new Rectangle(10,y,540,28),ForeColor=BarcaUi.Text};fields[id]=field;Controls.Add(field);Controls.Add(new Label{Text=note,Bounds=new Rectangle(32,y+29,520,24),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f)});y+=58;
        }
        Docking=new CheckBox{Text="拖到屏幕边缘时自动停靠",Checked=p.EdgeDocking,Bounds=new Rectangle(10,410,330,30),ForeColor=BarcaUi.Text};Controls.Add(Docking);
        var extensions=new BarcaButton{Text="管理扩展模块…",Bounds=new Rectangle(10,455,230,32)};extensions.Click+=(_,_)=>{using var manager=new ExtensionManager(p,preview);manager.ShowDialog(FindForm());};Controls.Add(extensions);
        var display=new BarcaButton{Text="入口显示…",Bounds=new Rectangle(265,455,280,32)};display.Click+=(_,_)=>{var draft=new Preferences();Apply(draft,false);using var dialog=new EntryDisplayForm(draft);if(dialog.ShowDialog(FindForm())==DialogResult.OK)entryDisplayModes=new(dialog.Result);};Controls.Add(display);
        var paths=new BarcaButton{Text="应用模块与图标…",Bounds=new Rectangle(365,410,180,32)};paths.Click+=(_,_)=>EditPaths(FindForm());Controls.Add(paths);
        Controls.Add(new Label{Text="停用模块不会卸载软件，也不会关闭已开启的代理或音乐。",Bounds=new Rectangle(10,503,550,28),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f)});
    }
    public bool EditPaths(IWin32Window? owner){var draft=new Preferences();Apply(draft,false);using var dialog=new LocalApplicationsForm(draft);if(dialog.ShowDialog(owner)!=DialogResult.OK)return false;selectedPaths=new(draft.ModuleExecutables);applicationModules=draft.ApplicationModules.ToList();moduleIcons=new(draft.ModuleIcons);foreach(var pair in fields)pair.Value.Checked=Modules.Enabled(draft,pair.Key);return true;}
    public Dictionary<string,string> Paths=>selectedPaths.ToDictionary();
    public void Apply(Preferences next,bool validatePaths=true){next.EntryDisplayModes=new(entryDisplayModes);next.ApplicationModules=applicationModules.ToList();next.ModuleIcons=new(moduleIcons);next.MediaModule=fields["media"].Checked;next.TerminalModule=fields["terminal"].Checked;next.ClashModule=fields["clash"].Checked;next.MusicModule=fields["music"].Checked;next.EdgeModule=fields["edge"].Checked;next.FootballModule=fields["football"].Checked;next.EdgeDocking=Docking.Checked;next.ModuleExecutables=selectedPaths.ToDictionary();foreach(var pair in selectedPaths)if(validatePaths&&Modules.Enabled(next,pair.Key))ModulePaths.Validate(pair.Key,pair.Value);}
}
