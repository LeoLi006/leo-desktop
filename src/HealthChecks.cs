namespace LeoLauncher;

sealed class HealthChecksView:Panel
{
    readonly Label result=new(){AutoSize=true,MaximumSize=new Size(560,0),Font=BarcaUi.Font(9.5f),ForeColor=BarcaUi.Text,Location=new Point(10,10)};
    public Action? StartupFixed;
    readonly Preferences prefs;readonly bool preview;readonly Func<string>? hotkeys;
    readonly CancellationTokenSource lifetime=new();
    public HealthChecksView(Preferences current,bool demo,Func<string>? shortcutState)
    {
        prefs=current;preview=demo;hotkeys=shortcutState;Dock=DockStyle.Fill;
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};scroll.Controls.Add(result);scroll.Resize+=(_,_)=>result.MaximumSize=new Size(Math.Max(120,scroll.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-20),0);
        var tools=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=45};
        var refresh=new BarcaButton{Text="重新检查",Size=new Size(110,32)};var startup=new BarcaButton{Text="修复开机启动",Size=new Size(140,32)};var api=new BarcaButton{Text="检测 API 连接",Size=new Size(145,32)};
        tools.Controls.AddRange([refresh,startup,api]);Controls.Add(scroll);Controls.Add(tools);
        refresh.Click+=(_,_)=>Check();startup.Click+=(_,_)=>{try{if(!preview){LeoStartup.SetEnabled(true);StartupFixed?.Invoke();}Check();}catch{result.Text="开机启动写入失败，请检查启动文件夹权限。";}};
        api.Click+=async(_,_)=>{api.Enabled=false;try{if(preview){result.Text="示例模式，不联网。";return;}var settings=ChatSettings.Load();using var client=new ChatApi();var models=await client.ModelsAsync(settings,settings.ReadKey(),lifetime.Token);if(!IsDisposed)result.Text+="\n\n✓ API 连接成功，模型列表 "+models.Length+" 项。\n此检查不生成回复，不计入模型请求限额。";}catch(OperationCanceledException){}catch{if(!IsDisposed)result.Text+="\n\nAPI 检测失败，请检查接口、Key、权限和网络；未生成模型回复。";}finally{if(!IsDisposed)api.Enabled=true;}};
        VisibleChanged+=(_,_)=>{if(Visible)Check();};Check();
    }
    void Check()
    {
        if(preview){result.Text="示例模式 · 不修改启动项，不启动外部软件，不联网。";return;}
        var lines=new List<string>();
        lines.Add((LeoStartup.Enabled?"✓":"○")+" 开机启动："+(LeoStartup.Enabled?"已启用":"未启用或被 Windows 停用"));
        try{string log=Path.Combine(Store.Root,"startup-last.log");if(File.Exists(log))lines.Add("  上次登录："+File.ReadLines(log).LastOrDefault());}catch{}
        lines.Add("快捷键："+(hotkeys?.Invoke()??"请在快捷键页检查配置"));
        lines.Add((LocalAsr.Installed?"✓":"○")+" 本地语音模型："+(LocalAsr.Installed?"完整":"缺少 SenseVoice 文件"));
        void Module(string name,bool enabled,Func<bool> installed){if(!enabled){lines.Add("○ "+name+"：已停用");return;}try{lines.Add((installed()?"✓ ":"○ ")+name+"："+(installed()?"已找到":"未找到，请安装客户端"));}catch{lines.Add("○ "+name+"：暂时无法检查");}}
        Module("QQ 音乐",prefs.MusicModule,()=>QQMusic.ClientPath()!=null);Module("Clash Verge",prefs.ClashModule,()=>ClashProxy.ClientPath()!=null);
        Module("终端",prefs.TerminalModule,()=>File.Exists(FootTools.TerminalStart().FileName));Module("Edge",prefs.EdgeModule,()=>FootTools.EdgeUrlStart("https://www.bing.com").FileName.EndsWith("msedge.exe",StringComparison.OrdinalIgnoreCase));
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
    public readonly CheckBox Docking;
    Dictionary<string,string> selectedPaths;
    public ModulesView(Preferences p)
    {
        selectedPaths=new(p.ModuleExecutables);
        Dock=DockStyle.Fill;
        Controls.Add(new Label{Text="按需开启 · 图标、菜单与后台检查同步变化",Bounds=new Rectangle(8,10,550,30),Font=BarcaUi.Font(10,true),ForeColor=BarcaUi.Gold});
        int y=55;
        foreach(var (id,title,note) in new[]{("terminal","管理员终端","打开 Windows 管理员终端"),("clash","Clash Verge","代理开关与状态检查"),("music","QQ 音乐","快速点歌、播放控制和歌词快捷键"),("edge","Edge 搜索与观赛网址","桌宠搜索按钮和浏览器网址入口")}){
            var field=new CheckBox{Text=title,Checked=Modules.Enabled(p,id),Bounds=new Rectangle(10,y,540,28),ForeColor=BarcaUi.Text};fields[id]=field;Controls.Add(field);Controls.Add(new Label{Text=note,Bounds=new Rectangle(32,y+29,520,24),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f)});y+=69;
        }
        Docking=new CheckBox{Text="拖到屏幕边缘时自动停靠",Checked=p.EdgeDocking,Bounds=new Rectangle(10,340,330,30),ForeColor=BarcaUi.Text};Controls.Add(Docking);
        var paths=new BarcaButton{Text="软件路径…",Bounds=new Rectangle(365,340,180,32)};paths.Click+=(_,_)=>{using var dialog=new ModulePathsForm(selectedPaths);if(dialog.ShowDialog(FindForm())==DialogResult.OK)selectedPaths=dialog.Paths;};Controls.Add(paths);
        Controls.Add(new Label{Text="停用模块不会卸载软件，也不会关闭已开启的代理或音乐。",Bounds=new Rectangle(10,380,550,28),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f)});
    }
    public void Apply(Preferences next){next.TerminalModule=fields["terminal"].Checked;next.ClashModule=fields["clash"].Checked;next.MusicModule=fields["music"].Checked;next.EdgeModule=fields["edge"].Checked;next.EdgeDocking=Docking.Checked;next.ModuleExecutables=selectedPaths.ToDictionary();foreach(var pair in selectedPaths)if(Modules.Enabled(next,pair.Key))ModulePaths.Validate(pair.Key,pair.Value);}
}
