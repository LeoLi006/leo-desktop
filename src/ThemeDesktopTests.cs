using System.Drawing.Imaging;
using System.Text.Json;
namespace LeoLauncher;

static class ThemeDesktopTests
{
    static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control child in root.Controls)foreach(var value in Walk(child))yield return value;}
    static void Show(Form form){form.StartPosition=FormStartPosition.Manual;form.Location=new(-20000,-20000);form.Show();Application.DoEvents();UiTheme.Attach(form);}
    static Bitmap Capture(Form form){var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));return image;}
    public static Dictionary<string,bool> Check(string output)
    {
        Directory.CreateDirectory(output);var checks=new Dictionary<string,bool>();
        var left=new DesktopDisplay(new(-1920,0,1920,1080),new(-1920,0,1920,1040));var right=new DesktopDisplay(new(0,0,1920,1080),new(0,0,1920,1040));var duo=new[]{left,right};
        checks["horizontal_join_is_not_dockable_on_either_side"]=DesktopDock.Choose(new(-130,200,130,140),left.Work,64,new(30,0),duo)==DockSide.None&&DesktopDock.Choose(new(0,200,130,140),right.Work,64,new(-30,0),duo)==DockSide.None;
        checks["resting_across_join_survives_position_restore"]=DesktopDock.Clamp(new(-60,200,130,140),64,duo,false)==new Point(-60,200);
        checks["negative_outer_edge_docks"]=DesktopDock.Choose(new(-1918,300,130,140),left.Work,64,new(-30,0),duo)==DockSide.Left;
        checks["right_outer_edge_docks"]=DesktopDock.Choose(new(1790,300,130,140),right.Work,64,new(30,0),duo)==DockSide.Right;
        checks["crossing_join_does_not_clamp_or_jump"]=Enumerable.Range(-160,320).All(x=>DesktopDock.Clamp(new(x,200,130,140),64,duo,true)==new Point(x,200));
        var top=new DesktopDisplay(new(0,-1080,1920,1080),new(0,-1080,1920,1080));var vertical=new[]{top,right};
        checks["vertical_join_is_not_dockable"]=DesktopDock.Choose(new(300,-204,130,140),top.Work,64,new(0,30),vertical)==DockSide.None&&DesktopDock.Choose(new(300,0,130,140),right.Work,64,new(0,-30),vertical)==DockSide.None;
        checks["vertical_crossing_is_continuous"]=Enumerable.Range(-160,320).All(y=>DesktopDock.Clamp(new(300,y,130,140),64,vertical,true)==new Point(300,y));
        var staggered=new[]{right,new DesktopDisplay(new(1920,400,1280,1024),new(1920,400,1280,984))};
        checks["staggered_exposed_ledge_can_dock"]=DesktopDock.Choose(new(1790,100,130,140),right.Work,64,new(30,0),staggered)==DockSide.Right;
        checks["staggered_shared_segment_cannot_dock"]=DesktopDock.Choose(new(1790,450,130,140),right.Work,64,new(30,0),staggered)==DockSide.None;
        checks["partly_shared_segment_does_not_clip_head"]=DesktopDock.Choose(new(1790,300,130,140),right.Work,64,new(30,0),staggered)==DockSide.None;
        var gap=new[]{right,new DesktopDisplay(new(2200,0,1280,900),new(2200,0,1280,860))};
        checks["gap_is_an_outer_edge_and_clamps_safely"]=DesktopDock.Choose(new(1790,200,130,140),right.Work,64,new(30,0),gap)==DockSide.Right&&DesktopDock.Clamp(new(1950,200,130,140),64,gap,true).X==1790;
        checks["removed_monitor_restores_inside_remaining_work_area"]=right.Work.Contains(new Rectangle(DesktopDock.Clamp(new(-1800,100,130,140),64,new[]{right},false),new Size(130,140)));
        checks["corners_follow_last_drag_axis"]=DesktopDock.Choose(new(-1920,0,130,140),left.Work,64,new(-30,-1),duo)==DockSide.Left&&DesktopDock.Choose(new(-1920,0,130,140),left.Work,64,new(-1,-30),duo)==DockSide.Top;
        var sideTaskbar=new[]{new DesktopDisplay(new(0,0,1920,1080),new(0,0,1880,1080)),new DesktopDisplay(new(1920,0,1920,1080),new(1920,0,1920,1040))};
        checks["side_taskbar_does_not_turn_shared_boundary_into_outer_edge"]=DesktopDock.Choose(new(1750,200,130,140),sideTaskbar[0].Work,64,new(30,0),sideTaskbar)==DockSide.None;
        checks["drag_can_cross_taskbar_at_monitor_join"]=Enumerable.Range(1750,260).All(x=>DesktopDock.Clamp(new(x,200,130,140),64,sideTaskbar,true)==new Point(x,200));
        checks["bottom_still_reserves_footer"]=DesktopDock.Snap(new(300,840,130,140),right.Work,64,DockSide.Bottom).Y==836;
        checks["old_preferences_default_to_leo"]=JsonSerializer.Deserialize<Preferences>("{}")!.DisplayName=="Leo";
        checks["name_and_skin_survive_serialization"]=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(new Preferences{DisplayName="小羊",SkinId="202"})) is {DisplayName:"小羊",SkinId:"202"};
        bool rejected=true;foreach(var invalid in new[]{"","  ","a\nb",new string('a',25)})try{UiTheme.ValidateName(invalid);rejected=false;}catch(ArgumentException){}checks["invalid_names_rejected"]=rejected;
        checks["chat_resize_hit_targets_cover_all_eight_edges"]=new[]{new Point(0,0),new Point(100,0),new Point(199,0),new Point(0,100),new Point(199,100),new Point(0,199),new Point(100,199),new Point(199,199)}.Select(p=>BarcaWindow.ResizeHit(p,new Size(200,200))).Distinct().Count()==8;
        UiTheme.Configure("83","Leo");var prefs=new Preferences();using var pet=new PetForm(prefs,true,true);Show(pet);
        using var tasks=new TodoForm(true);using var chat=new ChatForm(true);using var music=new MusicForm(true);using var football=new FootballForm(true);using var settings=new SettingsForm(prefs,(_,_)=>{},()=>{},true);
        Form[] windows=[tasks,chat,music,football,settings];foreach(var window in windows)Show(window);
        var input=Walk(chat).OfType<TextBox>().First(t=>t.Multiline);input.Text="保留草稿：明天去踢球";
        using var gallery=new Bitmap(1600,1250);using var galleryPaint=Graphics.FromImage(gallery);galleryPaint.Clear(System.Drawing.Color.FromArgb(13,18,28));
        var colors=new HashSet<int>();int index=0;
        foreach(var option in SkinSelection.Options){
            UiTheme.Configure(option.Id,"小羊");UiTheme.RefreshAll();Application.DoEvents();colors.Add(BarcaUi.Canvas.ToArgb());
            checks[option.Id+"_all_open_windows_recolor_immediately"]=windows.All(w=>w.BackColor==BarcaUi.Canvas);
            checks[option.Id+"_chat_input_preserved"]=input.Text=="保留草稿：明天去踢球";
            checks[option.Id+"_headers_use_custom_name"]=windows.All(w=>w.Text.Contains("小羊"))&&Walk(tasks).OfType<Label>().Any(l=>l.Text.StartsWith("小羊"));
            checks[option.Id+"_menu_recolors_and_renames"]=pet.ContextMenuStrip!.Items.Cast<ToolStripItem>().Any(i=>i.Text=="退出 小羊")&&pet.ContextMenuStrip.ForeColor==BarcaUi.Text;
            using var newWindow=new MusicForm(true);Show(newWindow);checks[option.Id+"_new_windows_use_active_palette"]=newWindow.BackColor==BarcaUi.Canvas;
            chat.Size=new(510,390);Application.DoEvents();chat.Size=new(720,600);Application.DoEvents();checks[option.Id+"_chat_resizes_without_losing_draft"]=input.Text=="保留草稿：明天去踢球"&&chat.ClientSize.Width==720;
            checks[option.Id+"_settings_uses_themed_size_slider"]=Walk(settings).Any(c=>c is MsnSlider);
            var late=new BarcaButton{Text="新增按钮"};chat.Controls.Add(late);checks[option.Id+"_late_controls_inherit_theme"]=late.BackColor==BarcaUi.Surface;chat.Controls.Remove(late);late.Dispose();
            prefs.SkinId=option.Id;pet.TestSkinRefresh();
            using var surface=pet.RenderPetSurface();galleryPaint.DrawImage(surface,new Rectangle(34,index*240+28,156,169));
            using var screen=Capture(settings);galleryPaint.DrawImage(screen,new Rectangle(210,index*240+10,210,220));
            using var calendar=Capture(tasks);galleryPaint.DrawImage(calendar,new Rectangle(440,index*240+10,410,220));
            using var conversation=Capture(chat);galleryPaint.DrawImage(conversation,new Rectangle(875,index*240+10,300,220));
            using var pointSong=Capture(music);galleryPaint.DrawImage(pointSong,new Rectangle(1195,index*240+10,280,220));
            using var font=BarcaUi.Font(12,true);TextRenderer.DrawText(galleryPaint,option.Name,font,new Rectangle(28,index*240+204,180,30),BarcaUi.Gold);
            index++;
            if(option.Id=="382"){
                using var detail=new Bitmap(1700,1400);using var draw=Graphics.FromImage(detail);draw.Clear(BarcaUi.Canvas);
                using var taskShot=Capture(tasks);draw.DrawImage(taskShot,new Point(24,24));using var chatShot=Capture(chat);draw.DrawImage(chatShot,new Rectangle(972,24,700,585));using var musicShot=Capture(music);draw.DrawImage(musicShot,new Point(24,710));using var settingsShot=Capture(settings);draw.DrawImage(settingsShot,new Rectangle(700,710,620,640));
                detail.Save(Path.Combine(output,"v0.50-ui-detail.png"),ImageFormat.Png);
                pet.SaveThemeLauncher(Path.Combine(output,"v0.50-launcher.png"));
                using var footballShot=Capture(football);footballShot.Save(Path.Combine(output,"v0.50-football.png"),ImageFormat.Png);
            }
        }
        checks["all_five_palettes_are_distinct"]=colors.Count==5;
        UiTheme.Configure("83","Leo");UiTheme.RefreshAll();checks["switching_back_restores_original_barca_colors"]=BarcaUi.Canvas==System.Drawing.Color.FromArgb(11,18,33)&&windows.All(w=>w.BackColor==BarcaUi.Canvas);
        UiTheme.Configure("202","这是一个很长的伙伴名字用来检查标题布局");UiTheme.RefreshAll();checks["long_name_updates_existing_headers"]=tasks.Text.StartsWith(UiTheme.Name);
        UiTheme.Configure("382","小羊");UiTheme.RefreshAll();gallery.Save(Path.Combine(output,"v0.50-five-themes.png"),ImageFormat.Png);
        var migration=new ConfigMigration(output);string file=Path.Combine(output,"theme-export.leo.json");migration.Export(file,new Preferences{DisplayName="小羊",SkinId="382"},false);checks["config_migration_preserves_name_and_theme"]=migration.Inspect(file).Bundle.Preferences is {DisplayName:"小羊",SkinId:"382"};
        checks["actual_settings_apply_updates_identity_and_skin"]=pet.TestIdentitySettings();
        foreach(var window in windows)window.Close();UiTheme.Configure("83","Leo");return checks;
    }
}
sealed partial class PetForm{internal void TestSkinRefresh()=>UpdateSkin();internal void SaveThemeLauncher(string path){launcher.PreparePublicPreview();launcher.Open();UiTheme.Attach(launcher);using var image=new Bitmap(launcher.Width,launcher.Height);launcher.DrawToBitmap(image,launcher.ClientRectangle);image.Save(path,System.Drawing.Imaging.ImageFormat.Png);launcher.Hide();}internal bool TestIdentitySettings(){ApplySettings(new Preferences{DisplayName="Nova",SkinId="202"},false);return prefs.DisplayName=="Nova"&&UiTheme.Name=="Nova"&&UiTheme.Skin=="202"&&tray.Text.StartsWith("Nova");}}

sealed partial class LauncherForm
{
    internal void PreparePublicPreview()
    {
        if(!preview)throw new InvalidOperationException("Only preview fixtures may replace the catalog");
        var watch=System.Diagnostics.Stopwatch.StartNew();while(loading&&watch.ElapsedMilliseconds<15000){Application.DoEvents();Thread.Sleep(10);}if(loading)throw new TimeoutException("Preview catalog did not finish");
        var labels=new[]{"浏览器","记事本","文件","终端","日历","音乐","剪贴板","画图","计算器","时钟","照片","邮件","地图","设置"};
        apps=labels.Select((name,i)=>new AppEntry(name,"preview:"+i,"示例应用")).ToList();prefs.Favorites.Clear();prefs.Favorites.AddRange(apps.Take(3).Select(a=>a.Target));
        for(int i=0;i<apps.Count;i++){memory.Record(apps[i],"preview",DateTimeOffset.UtcNow.AddMinutes(-i),false);icons.PreviewGlyph(apps[i],labels[i][..1],i);}
        query.Clear();Filter();
    }
}
sealed partial class AppIcons
{
    internal void PreviewGlyph(AppEntry app,string glyph,int index)
    {
        var image=new Bitmap(48,48);using(var g=Graphics.FromImage(image)){g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var path=Appearance.Path(new Rectangle(1,1,46,46),12);using var fill=new SolidBrush(index%2==0?Appearance.Blue:Appearance.Red);g.FillPath(fill,path);using var font=BarcaUi.Font(15,true);TextRenderer.DrawText(g,glyph,font,new Rectangle(0,0,48,48),BarcaUi.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
        lock(gate){if(images.Remove(app.Target,out var old))old.Dispose();images[app.Target]=image;}
    }
}
