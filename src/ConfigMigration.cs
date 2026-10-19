using System.Text.Json;
namespace LeoLauncher;

sealed record ConfigBundle(string Format,int Version,DateTimeOffset Created,Preferences Preferences,List<CustomCombo> Combos,WatchLink[] WatchLinks,QuietHours Quiet,ApiUsageConfig Budget,List<DailyTask>? Tasks,List<AppEntry>? Applications=null,List<string>? FollowingIds=null);
sealed record ConfigImport(ConfigBundle Bundle,string[] Notes);
sealed class ConfigMigration
{
    readonly string root;readonly Func<IEnumerable<AppEntry>>? applications;
    static readonly JsonSerializerOptions json=new(){WriteIndented=true};
    public ConfigMigration(string directory,Func<IEnumerable<AppEntry>>? catalog=null){root=directory;applications=catalog;}
    T Read<T>(string name,Func<T> fallback){string path=Path.Combine(root,name);return File.Exists(path)?JsonSerializer.Deserialize<T>(File.ReadAllText(path))??throw new IOException("配置内容不完整："+name):fallback();}
    public ConfigBundle Capture(Preferences prefs,bool tasks)
    {
        var copy=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(prefs))!;copy.X=copy.Y=-1;
        var combos=Read("leo-custom-combos.json",()=>new ComboStore(null).Items);
        var targets=copy.Favorites.Concat(copy.Aliases.Keys).Concat(combos.SelectMany(c=>c.Steps).Where(s=>s.Kind=="app").Select(s=>s.Value)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hints=applications?.Invoke().Where(a=>targets.Contains(a.Target)).Select(a=>a with{ExePath=Path.GetFileName(a.ExePath)}).ToList();
        return new("LeoConfig",1,DateTimeOffset.Now,copy,combos,Read("leo-watch-links.json",()=>new WatchLinkStore(null).Items),Read("leo-reminder-settings.json",()=>new QuietHours()),Read("leo-api-budget.json",()=>new ApiUsageConfig()),tasks?Read("leo-todos.json",()=>new List<DailyTask>()):null,hints,Read("leo-football-following.json",()=>FootballFollowing.Defaults.ToList()));
    }
    public void Export(string destination,Preferences prefs,bool tasks)=>Write(destination,JsonSerializer.Serialize(Capture(prefs,tasks),json));
    public static void Write(string path,string content)=>AtomicJson.Write(path,content);
    public ConfigImport Inspect(string file)
    {
        if(new FileInfo(file).Length>16_000_000)throw new ArgumentException("配置文件过大，最多支持16MB。");
        string text=File.ReadAllText(file);using var doc=JsonDocument.Parse(text);var obj=doc.RootElement;
        foreach(string name in new[]{"Format","Version","Preferences","Combos","WatchLinks","Quiet","Budget"})if(!obj.TryGetProperty(name,out _))throw new ArgumentException("不是完整的 Leo 配置文件。");
        var bundle=JsonSerializer.Deserialize<ConfigBundle>(text)??throw new ArgumentException("无法读取配置文件。");QuickNoteCompatibility.Migrate(obj.GetProperty("Preferences"),bundle.Preferences);
        if(bundle.Format!="LeoConfig"||bundle.Version!=1)throw new ArgumentException("不支持这个配置文件版本。");
        var p=bundle.Preferences??throw new ArgumentException("配置缺少个人设置。");p.EntryDisplayModes??=new();EntryDisplay.Validate(p.EntryDisplayModes);
        if(p==null||p.PetSize is <80 or >230||p.Favorites==null||p.Favorites.Any(string.IsNullOrWhiteSpace)||p.Aliases==null||p.ModuleExecutables==null||p.Aliases.Any(x=>x.Value==null||x.Value.Any(a=>string.IsNullOrWhiteSpace(a)||a.Length>30)))throw new ArgumentException("外观、收藏或简称配置无效。");
        if(p.ClipboardExpireDays is not(0 or 1 or 7 or 30)||p.ClipboardBlockedApps==null||p.ClipboardBlockedApps.Count>60)throw new ArgumentException("剪贴板保护规则无效。");ClipboardPrivacy.Apps(string.Join("\n",p.ClipboardBlockedApps));bool missingCustom=p.SkinId!=null&&System.Text.RegularExpressions.Regex.IsMatch(p.SkinId,@"^custom:[a-f0-9]{32}$")&&SkinLibrary.Find(p.SkinId)==null;if(missingCustom)p.SkinId="83";if(p.SkinId!=null&&!SkinSelection.Valid(p.SkinId)||p.MatchDayKit is not(null or "auto" or "83" or "382" or "20232" or "202" or "164"))throw new ArgumentException("皮肤配置无效。");
        if(p.SkinThemes==null||p.DisabledExtensions==null)throw new ArgumentException("主题或扩展配置不完整");if(p.SkinThemes.Count>128||p.DisabledExtensions.Count>256||p.SkinThemes.Any(x=>x.Value==null)||p.DisabledExtensions.Any(id=>id==null||!System.Text.RegularExpressions.Regex.IsMatch(id,@"^[a-z][a-z0-9-]{1,39}$")))throw new ArgumentException("主题或扩展配置无效");foreach(var theme in p.SkinThemes.Values)theme.Validate();p.DisplayName=UiTheme.ValidateName(p.DisplayName);LeoHotkeys.Validate(LeoHotkeys.Values(p));
        if(bundle.Combos==null||bundle.Combos.Count>24||bundle.Combos.Select(c=>c.Id).Distinct().Count()!=bundle.Combos.Count||bundle.Combos.Select(c=>c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=bundle.Combos.Count)throw new ArgumentException("自定义组合配置无效。");
        foreach(var combo in bundle.Combos)ComboStore.Validate(combo);
        if(bundle.WatchLinks==null||bundle.WatchLinks.Length!=4)throw new ArgumentException("观赛网址配置无效。");
        var links=bundle.WatchLinks.Select(WatchLinkStore.Normalize).ToArray();
        if(bundle.Quiet==null||bundle.Quiet.Start is <0 or >=1440||bundle.Quiet.End is <0 or >=1440||bundle.Quiet.Start==bundle.Quiet.End||bundle.Budget==null||bundle.Budget.DailyLimit is <0 or >100000)throw new ArgumentException("提醒时段或 API 限额无效。");
        if(bundle.Tasks!=null){if(bundle.Tasks.Count>20000)throw new ArgumentException("清单数量过多。");TodoStore.ValidateSnapshot(JsonSerializer.Serialize(bundle.Tasks));if(bundle.Tasks.Any(t=>t.Repeat!=null&&t.Repeat.Frequency is not("daily" or "weekly")))throw new ArgumentException("清单中包含不支持的重复规则。");}
        if(bundle.FollowingIds!=null)FootballFollowing.Validate(bundle.FollowingIds);
        var notes=new List<string>();if(missingCustom)notes.Add("自定义球衣图片不在配置文件中，已恢复巴萨；请在主题编辑器重新导入球衣PNG。");var catalog=applications?.Invoke().ToArray()??[];var mapping=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        if(bundle.Applications!=null&&catalog.Length>0)foreach(var hint in bundle.Applications){
            if(hint==null||string.IsNullOrWhiteSpace(hint.Target)||string.IsNullOrWhiteSpace(hint.Name)||hint.ExePath==null)throw new ArgumentException("应用迁移信息不完整。");
            if(catalog.Any(a=>a.Target.Equals(hint.Target,StringComparison.OrdinalIgnoreCase)))continue;
            var matches=catalog.Where(a=>Catalog.Normalize(a.Name)==Catalog.Normalize(hint.Name)).ToArray();
            if(matches.Length>1&&hint.ExePath.Length>0)matches=matches.Where(a=>Path.GetFileName(a.ExePath).Equals(Path.GetFileName(hint.ExePath),StringComparison.OrdinalIgnoreCase)).ToArray();
            if(matches.Length==1)mapping[hint.Target]=matches[0].Target;else notes.Add("应用“"+hint.Name+"”未能唯一匹配，请在本机重新选择。");
        }
        p.Favorites=p.Favorites.Select(t=>mapping.GetValueOrDefault(t)??t).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        p.Aliases=p.Aliases.GroupBy(pair=>mapping.GetValueOrDefault(pair.Key)??pair.Key,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.SelectMany(p=>p.Value).Distinct().ToArray());
        bundle=bundle with{Combos=bundle.Combos.Select(c=>c with{Steps=c.Steps.Select(step=>step.Kind=="app"&&mapping.TryGetValue(step.Value,out var target)?step with{Value=target}:step).ToList()}).ToList()};
        foreach(var (id,path) in p.ModuleExecutables.ToArray()){
            if(!ModulePaths.Entries.Any(e=>e.Id==id))throw new ArgumentException("配置中包含未知的软件模块。");
            try{p.ModuleExecutables[id]=ModulePaths.Validate(id,path);}catch{p.ModuleExecutables.Remove(id);notes.Add(ModulePaths.Entries.First(e=>e.Id==id).Name+"路径在本机不可用，将自动检测。");}
        }
        return new(bundle with{WatchLinks=links},notes.ToArray());
    }
    public string? LatestBackup=>Directory.Exists(Path.Combine(root,"config-backups"))?Directory.GetFiles(Path.Combine(root,"config-backups"),"*-before-import.leo.json").OrderDescending().FirstOrDefault():null;
    public string Apply(ConfigImport plan,Preferences current,Action<Preferences> apply,Action reload,bool restoring=false)
    {
        // Only these local configuration files can be written; imported JSON cannot supply paths.
        var bundle=plan.Bundle;var prefs=bundle.Preferences;prefs.X=current.X;prefs.Y=current.Y;
        var files=new Dictionary<string,string>{["leo-settings.json"]=JsonSerializer.Serialize(prefs,json),["leo-custom-combos.json"]=JsonSerializer.Serialize(bundle.Combos,json),["leo-watch-links.json"]=JsonSerializer.Serialize(bundle.WatchLinks,json),["leo-reminder-settings.json"]=JsonSerializer.Serialize(bundle.Quiet,json),["leo-api-budget.json"]=JsonSerializer.Serialize(bundle.Budget,json)};
        if(bundle.FollowingIds!=null)files["leo-football-following.json"]=JsonSerializer.Serialize(bundle.FollowingIds,json);
        if(bundle.Tasks!=null)files["leo-todos.json"]=JsonSerializer.Serialize(bundle.Tasks,json);
        var before=files.Keys.ToDictionary(name=>name,name=>File.Exists(Path.Combine(root,name))?File.ReadAllBytes(Path.Combine(root,name)):null);
        var previous=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(current))!;
        string folder=Path.Combine(root,"config-backups");Directory.CreateDirectory(folder);
        string backup=Path.Combine(folder,DateTime.Now.ToString("yyyyMMdd-HHmmss-fffffff")+(restoring?"-before-restore.leo.json":"-before-import.leo.json"));Export(backup,current,bundle.Tasks!=null);
        try{
            foreach(var pair in files)Write(Path.Combine(root,pair.Key),pair.Value);
            apply(prefs);reload();return backup;
        }catch(Exception failure){
            var errors=new List<Exception>{failure};foreach(var pair in before)try{string path=Path.Combine(root,pair.Key);if(pair.Value==null)File.Delete(path);else{File.WriteAllBytes(path+".tmp",pair.Value);File.Move(path+".tmp",path,true);}}catch(Exception e){errors.Add(e);}
            try{apply(previous);reload();}catch(Exception e){errors.Add(e);}
            throw new IOException(errors.Count==1?"导入未完成，原配置已恢复。":"导入未完成，部分恢复失败；导入前备份已保留在 config-backups。",new AggregateException(errors));
        }
    }
}

sealed class ConfigMigrationForm:BarcaWindow
{
    readonly ConfigMigration migration;readonly Preferences prefs;readonly Action<Preferences> apply;readonly Action reload;readonly bool preview;
    readonly CheckBox includeTasks=new(){Text="导出时包含清单、重复事项和提醒",Checked=true,Bounds=new Rectangle(25,176,550,30),ForeColor=BarcaUi.Text};
    readonly Label result=new(){Bounds=new Rectangle(25,278,565,175),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)};
    readonly BarcaButton restore=new(){Text="恢复上次导入前…",Bounds=new Rectangle(25,466,200,34)};
    public ConfigMigrationForm(Preferences p,Action<Preferences> applySettings,Action reloadSettings,bool demo=false,Func<IEnumerable<AppEntry>>? applications=null)
    {
        prefs=p;apply=applySettings;reload=reloadSettings;preview=demo;migration=new(Store.Root,applications);Text="Leo · 配置迁移与恢复";StartPosition=FormStartPosition.CenterParent;ClientSize=new Size(620,525);
        Controls.Add(BarcaUi.Chrome(this,"配置迁移与恢复","一份配置文件，带走你习惯的 Leo。"));
        Controls.Add(new Label{Text="包含收藏、简称、快捷键、模块路径、组合动作、观赛网址、\n提醒时段、球队关注及 API 限额。开机启动和窗口位置沿用本机设置。",Bounds=new Rectangle(25,108,565,58),ForeColor=BarcaUi.Text,Font=BarcaUi.Font(9.5f)});
        var export=new BarcaButton{Text="导出配置…",Bounds=new Rectangle(25,223,170,36)};var import=new BarcaButton{Text="导入配置…",Bounds=new Rectangle(211,223,170,36)};
        export.Click+=(_,_)=>{if(preview)return;using var picker=new SaveFileDialog{Filter="Leo 配置 (*.leo.json)|*.leo.json",FileName="Leo配置-"+DateTime.Today.ToString("yyyyMMdd")+".leo.json"};if(picker.ShowDialog(this)!=DialogResult.OK)return;Run(()=>{migration.Export(picker.FileName,prefs,includeTasks.Checked);result.Text="配置已导出。API 配置与 Key、聊天、剪贴板、使用历史和语音模型均未导出。";});};
        import.Click+=(_,_)=>{if(preview)return;using var picker=new OpenFileDialog{Filter="Leo 配置 (*.leo.json)|*.leo.json|JSON 文件 (*.json)|*.json"};if(picker.ShowDialog(this)==DialogResult.OK)Import(picker.FileName,false);};
        restore.Click+=(_,_)=>{if(!preview&&migration.LatestBackup is {} file)Import(file,true);};restore.Enabled=!preview&&migration.LatestBackup!=null;
        result.Text="API 配置与 Key、聊天、剪贴板、使用历史和语音模型不会导出。\n\n导入时替换文件中包含的配置；未包含清单时保留本机清单。\n导入前自动备份，失败时尝试还原；导入成功后立即生效。";
        Controls.AddRange([includeTasks,export,import,result,restore]);
    }
    void Import(string file,bool restoring)=>Run(()=>{
        var plan=migration.Inspect(file);var bundle=plan.Bundle;
        string summary=$"收藏 {bundle.Preferences.Favorites.Count} 项 · 简称 {bundle.Preferences.Aliases.Count} 项\n组合 {bundle.Combos.Count} 个 · "+(bundle.Tasks==null?"保留本机清单":$"清单 {bundle.Tasks.Count(t=>t.Repeat==null&&!t.Deleted)} 项")+"\n"+string.Join("\n",plan.Notes)+"\n将替换这些配置，并先保存当前备份。";
        if(MessageBox.Show(this,summary,restoring?"恢复导入前配置":"导入配置",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
        migration.Apply(plan,prefs,apply,reload,restoring);restore.Enabled=migration.LatestBackup!=null;result.Text="配置已"+(restoring?"恢复":"导入")+"，立即生效。\n"+string.Join("\n",plan.Notes);DialogResult=DialogResult.OK;
    });
    void Run(Action action){try{action();}catch(Exception e){result.Text=e.Message;}}
}
