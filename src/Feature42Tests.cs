using System.Text.Json;
namespace LeoLauncher;

static class Feature42Tests
{
    public static Dictionary<string,bool> Check(string output)
    {
        var checks=new Dictionary<string,bool>();
        var motion=new DockMotion();motion.Reset(0);for(int t=0;t<=2400;t+=32)motion.Tick(t,true,false);checks["edge_motion_tucks_after_idle"]=motion.Progress>=.999f;
        motion.Tick(2432,true,true);for(int t=2464;t<=3500;t+=32)motion.Tick(t,true,false);checks["brief_hover_latches_until_full_reveal"]=motion.Progress==0;
        for(int t=3200;t<=5500;t+=32)motion.Tick(t,true,false);float middle=motion.Progress;motion.Tick(5532,false,false);checks["undocking_reverses_tuck"]=middle>motion.Progress;
        using(var sprites=new DockSprites())using(var image=new Bitmap(720,350))using(var g=Graphics.FromImage(image)){
            g.Clear(BarcaUi.Canvas);foreach(var (side,index) in new[]{(DockSide.Left,0),(DockSide.Right,1),(DockSide.Top,2),(DockSide.Bottom,3)}){var state=g.Save();g.TranslateTransform(12+index*175,200);var box=sprites.Draw(g,new Size(150,150),1,side,0);checks["head_only_sprite_within_"+side]=new RectangleF(0,0,150,150).Contains(box)&&box.Height<125;g.Restore(state);}
            for(int i=0;i<6;i++){var state=g.Save();g.TranslateTransform(i*120,0);sprites.Draw(g,new Size(115,150),i/5f,DockSide.Bottom,0);g.Restore(state);}image.Save(Path.Combine(output,"preview-edge-animation42.png"));
        }
        using(var sprites=new DockSprites()){
            bool contained=true,separated=true;var sources=new List<Rectangle>();
            foreach(var side in new[]{DockSide.Left,DockSide.Right,DockSide.Top,DockSide.Bottom}){
                var source=sprites.Source(1,0,side);sources.Add(source);
                foreach(int size in new[]{80,136,230})foreach(float progress in new[]{0f,.25f,.5f,.75f,1f}){
                    var bounds=sprites.Destination(new Size(size,size*208/192),progress,side,source);
                    contained&=new RectangleF(0,0,size,size*208/192).Contains(bounds);
                }
            }
            for(int a=0;a<sources.Count;a++)for(int b=a+1;b<sources.Count;b++)separated&=!sources[a].IntersectsWith(sources[b]);
            checks["all_four_complete_heads_stay_inside_at_every_scale_and_transition"]=contained;
            checks["directional_artwork_cells_do_not_overlap"]=separated;
        }
        using(var pet=new PetForm(new Preferences(),true)){pet.Show();Application.DoEvents();foreach(var check in pet.VerifyDockPeeking(output))checks[check.Key]=check.Value;}
        checks["catalog_covers_big_five_and_97_clubs"]=ClubCatalog.All.Length>=97&&new[]{"英超","西甲","意甲","德甲","法甲"}.All(l=>ClubCatalog.All.Count(c=>c.LeagueName==l)>=15);
        checks["major_clubs_resolve_to_own_ids_and_leagues"]=Club.For("皇马").Id=="86"&&Club.For("拜仁").League=="ger.1"&&Club.For("国米").Id=="110"&&Club.For("巴黎").League=="fra.1"&&Club.For("利物浦").League=="eng.1";
        bool unknown=false;try{Club.For("不存在的球队");}catch(ArgumentException){unknown=true;}checks["unknown_team_never_silently_becomes_barcelona"]=unknown;
        checks["new_club_facts_route_locally_but_casual_mentions_do_not"]=QuickRouter.Route("皇马下一场什么时候").Kind==QuickKind.Football&&QuickRouter.Route("拜仁积分榜").Team=="拜仁慕尼黑"&&QuickRouter.Route("我喜欢国米的球衣").Kind==QuickKind.Ai;
        string path=Path.Combine(output,"following42-"+Guid.NewGuid().ToString("N")+".json");var following=new FootballFollowing(path);following.Replace(["86","132","110","160","364"]);var reopened=new FootballFollowing(path);
        checks["custom_following_persists_across_restart"]=reopened.Ids.SequenceEqual(following.Ids)&&!reopened.Ids.Contains("83");
        var match=new FootballMatch("fixture42",DateTimeOffset.Now.AddHours(2),"ger.1","132","Bayern Munich","124","Borussia Dortmund","pre","Scheduled","VS",true);
        checks["arbitrary_followed_club_matches_are_tagged"]=FootballFeed.FollowTags(match,reopened.Names).SequenceEqual(new[]{"拜仁慕尼黑"});
        using(var football=new FootballForm(true,favorites:reopened)){football.Show();Application.DoEvents();checks["following_sidebar_uses_saved_clubs"]=football.VerifyFollowing();Capture(football,"preview-football-following42.png",output);}
        using(var manager=new FollowingManager(reopened)){manager.Show();Application.DoEvents();Capture(manager,"preview-following-manager42.png",output);}
        bool invalidId=false;try{FootballFollowing.Validate(["巴萨"]);}catch(ArgumentException){invalidId=true;}checks["following_rejects_names_in_id_storage"]=invalidId;
        following.Replace([]);checks["unfollow_all_stays_empty_after_restart"]=new FootballFollowing(path).Names.Length==0;
        string root=Path.Combine(output,"migration-following42-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"leo-football-following.json"),JsonSerializer.Serialize(reopened.Ids));var migration=new ConfigMigration(root);string export=Path.Combine(root,"export.leo.json");migration.Export(export,new(),false);checks["migration_includes_custom_following"]=migration.Inspect(export).Bundle.FollowingIds!.SequenceEqual(reopened.Ids);
        return checks;
    }
    static void Capture(Form form,string name,string directory){using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(directory,name));}
}

sealed partial class PetForm
{
    internal Dictionary<string,bool> VerifyDockPeeking(string output)
    {
        var checks=new Dictionary<string,bool>();var area=Screen.FromControl(this).WorkingArea;var far=new Point(area.Right-2,area.Top+5);
        for(int i=0;i<5;i++)RevealDock(true);
        checks["repeated_click_reveal_keeps_visible_footer_without_reshowing"]=foot.Visible&&foot.Opacity==1;
        foot.Hide();RevealDock(true);RevealDock(true);
        checks["click_reveal_restores_hidden_footer_and_is_repeatable"]=foot.Visible;
        Location=new Point(area.Left,area.Top+Math.Max(20,(area.Height-Height)/2));dockMotion.Reset(0);
        for(int t=0;t<=2400;t+=32)AdvanceDock(t,far);
        checks["tucked_pet_hides_all_footer_buttons"]=dockMotion.Progress>=.999f&&!foot.Visible;
        checks["tucked_hit_target_is_only_visible_head"]=hit.Height<Height*.65&&hit.Bounds.Left>=Left&&hit.Bounds.Left<=Left+Width*.05;
        using(var image=RenderPetSurface()){image.Save(Path.Combine(output,"preview-peeking-head42.png"));}
        var hover=new Point(hit.Left+hit.Width/2,hit.Top+hit.Height/2);for(int t=2432;t<=3700;t+=32)AdvanceDock(t,hover);
        checks["hover_restores_full_pet_hit_area_and_footer"]=dockMotion.Progress==0&&hit.Bounds==Bounds&&foot.Visible&&foot.Opacity>.99;
        RevealDock(true);Location=new Point(area.Left+(area.Width-Width)/2,area.Bottom-Height-foot.ReservedSpace);var original=Location;dockMotion.Reset(0);
        for(int t=0;t<=2400;t+=32)AdvanceDock(t,far);
        checks["bottom_peek_reaches_working_area_edge_without_taskbar_overlap"]=Bottom==area.Bottom&&hit.Bounds.Bottom<=area.Bottom&&!foot.Visible;
        RevealDock(true);checks["revealing_bottom_dock_restores_saved_anchor"]=Location==original&&hit.Bounds==Bounds;
        using(var preview=new Bitmap(Width*4,Height))using(var graphics=Graphics.FromImage(preview)){
            graphics.Clear(BarcaUi.Canvas);int column=0;
            foreach(var side in new[]{DockSide.Left,DockSide.Right,DockSide.Top,DockSide.Bottom}){
                dockSide=side;dockMotion.Reset(0);dockMotion.BeginTuck(0);for(int t=32;t<=1000;t+=32)dockMotion.Tick(t,true,false);
                var state=graphics.Save();graphics.TranslateTransform(column++*Width,0);DrawPet(graphics,PetDrawingBounds(ClientSize));graphics.Restore(state);
            }
            preview.Save(Path.Combine(output,"preview-complete-heads44.png"));
        }
        RevealDock(true);Location=new Point(area.Left+Width+40,area.Top+80);
        var target=new Point(area.Left,Top+30);
        hit.SimulateClick(new Point(10,10),()=>ApplyDrag(new Size(target.X-Left,target.Y-Top)));
        checks["actual_drag_release_clears_dragging_and_commits_tuck"]=!dragging&&dockAwaitExit;
        long start=clock.ElapsedMilliseconds;for(int t=0;t<=1200;t+=32)AdvanceDock(start+t,far);
        checks["actual_drag_release_reaches_half_hidden_without_footer"]=dockMotion.Progress>=.999f&&!foot.Visible;
        int opens=launcherOpens;hit.SimulateClick(new Point(5,5));
        checks["half_hidden_click_only_reveals_without_opening_launcher"]=launcherOpens==opens&&!dockAwaitExit;
        foreach(var side in new[]{DockSide.Left,DockSide.Right,DockSide.Top,DockSide.Bottom}){
            RevealDock(true);Location=side switch{DockSide.Left=>new Point(area.Left,area.Top+Height),DockSide.Right=>new Point(area.Right-Width,area.Top+Height),DockSide.Top=>new Point(area.Left+Width*2,area.Top),_=>new Point(area.Left+Width*2,area.Bottom-Height-foot.ReservedSpace)};
            BeginDockTuck();long begun=clock.ElapsedMilliseconds;for(int t=32;t<=1200;t+=32)AdvanceDock(begun+t,far);
            var destination=new Point(area.Left+area.Width/2,area.Top+area.Height/2);
            hit.SimulateClick(new Point(5,5),()=>{checks[side+"_hidden_press_captures_drag"]=hit.Capture;ApplyDrag(new Size(destination.X-Left,destination.Y-Top));});
            long released=clock.ElapsedMilliseconds;for(int t=32;t<=1800;t+=32)AdvanceDock(released+t,far);Animate();
            checks[side+"_drag_out_ends_animation_and_restores_tools"]=!dragging&&!hit.Capture&&!dockClickReveal&&!dockAwaitExit&&dockMotion.Progress==0&&row==0&&foot.Visible&&Location==destination;
        }
        return checks;
    }
}
