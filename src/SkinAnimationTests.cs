using System.Security.Cryptography;
namespace LeoLauncher;
static class SkinAnimationTests
{
    public static Dictionary<string,bool> Check(string output)
    {
        Directory.CreateDirectory(output);var checks=new Dictionary<string,bool>();
        using var pet=new PetForm(new Preferences(),true);pet.Show();Application.DoEvents();
        foreach(var pair in pet.VerifySkins(output))checks[pair.Key]=pair.Value;
        return checks;
    }
    internal static string Hash(Bitmap image){using var stream=new MemoryStream();image.Save(stream,System.Drawing.Imaging.ImageFormat.Png);return Convert.ToHexString(SHA256.HashData(stream.ToArray()));}
}
sealed partial class PetForm
{
    internal Dictionary<string,bool> VerifySkins(string output)
    {
        var checks=new Dictionary<string,bool>();
        using var contact=new Bitmap(1120,760);using var g=Graphics.FromImage(contact);g.Clear(BarcaUi.Canvas);
        int line=0;
        foreach(var id in new[]{"382","20232","202","164"}){
            prefs.FootballModule=true;prefs.MatchDayOutfits=true;prefs.MatchDayKit=id;UpdateMatchDay();var skin=CurrentSkin();checks[id+"_loads_complete_atomic_skin"]=skin!=null;if(skin==null)continue;
            using(var original=new Bitmap(Path.Combine(Store.Root,"assets","leo.png"))){
                checks[id+"_uses_identical_default_pose_atlas"]=SkinAnimationTests.Hash(original)==SkinAnimationTests.Hash(skin.Main);
                row=frame=0;using var dressed=new Bitmap(192,208);using(var paint=Graphics.FromImage(dressed))DrawAnimationFrame(paint,new RectangleF(0,0,192,208));
                bool face=true;for(int y=0;y<110;y++)for(int x=0;x<192;x++){var p=original.GetPixel(x,y);if(p.A==255)face&=dressed.GetPixel(x,y)==p;}
                checks[id+"_preserves_default_face_wool_ears_and_eyes"]=face;
                var texture=KitTexture.Load(skin.NameOfKit);var samples=new HashSet<int>();for(int v=0;v<8;v++)for(int u=0;u<8;u++)samples.Add(texture.Sample(u/7f,v/7f).ToArgb());
                checks[id+"_uses_team_crest_collar_and_pattern_texture"]=samples.Count>20;
                var pants=dressed.GetPixel(96,166);checks[id+"_has_team_specific_shorts"]=skin.NameOfKit=="city"?pants.R>140:skin.NameOfKit=="spain"||pants.R<70;
                row=11;frame=0;using var verticalDressed=new Bitmap(192,208);using(var paint=Graphics.FromImage(verticalDressed))DrawAnimationFrame(paint,new RectangleF(0,0,192,208));
                bool verticalFace=true;for(int y=0;y<92;y++)for(int x=0;x<192;x++){var p=skin.Vertical.GetPixel(x,y);if(p.A==255)verticalFace&=verticalDressed.GetPixel(x,y)==p;}
                checks[id+"_vertical_drag_preserves_mouth_and_face_colors"]=verticalFace;
            }
            bool allFrames=true;var images=new HashSet<string>();
            for(int r=0;r<13;r++){
                int count=r is 1 or 2 or 9 or 10?8:r is 3 or 11 or 12?4:r==4?5:r==5?8:6;
                for(int f=0;f<count;f++){
                    row=r;frame=f;using var image=new Bitmap(136,148);using(var drawing=Graphics.FromImage(image))DrawAnimationFrame(drawing,new RectangleF(0,0,136,148));
                    var bounds=DockBridge.Visible(image,new Rectangle(Point.Empty,image.Size));allFrames&=bounds.Width>40&&bounds.Height>40;images.Add(SkinAnimationTests.Hash(image));
                    if(f==0&&r is 0 or 1 or 3 or 8 or 11 or 12)g.DrawImageUnscaled(image,(r switch{0=>0,1=>1,3=>2,8=>3,11=>4,_=>5})*140,line*190+28);
                }
            }
            checks[id+"_covers_every_main_and_vertical_animation_frame"]=allFrames&&images.Count>35;
            bool dockIntact=true;int sideIndex=0;
            foreach(var side in new[]{DockSide.Left,DockSide.Right,DockSide.Top,DockSide.Bottom}){
                var hashes=new HashSet<string>();
                for(int f=0;f<DockBridge.Count;f++){using var image=new Bitmap(136,148);using(var drawing=Graphics.FromImage(image))skin.Bridge.Draw(drawing,image.Size,f*DockBridge.End/DockBridge.Count,side,skin.Dock);hashes.Add(SkinAnimationTests.Hash(image));}
                dockIntact&=hashes.Count==DockBridge.Count;
                using var head=new Bitmap(64,80);using(var drawing=Graphics.FromImage(head))skin.Dock.Draw(drawing,head.Size,1,side,0);g.DrawImageUnscaled(head,840+sideIndex++*66,line*190+60);
            }
            checks[id+"_covers_four_direction_bridges_and_tucks"]=dockIntact;
            using var font=BarcaUi.Font(10,true);g.DrawString(ClubCatalog.Find(id)!.Name,font,Brushes.White,4,line*190+4);
            focus.Start(25,true,DateTimeOffset.Now);UpdateMatchDay();checks[id+"_keeps_clothes_during_focus"]=ReferenceEquals(skin,CurrentSkin());focus.Stop();line++;
        }
        contact.Save(Path.Combine(output,"complete-skins.png"));
        prefs.MatchDayKit="83";UpdateMatchDay();checks["barcelona_reuses_existing_original_animation_set"]=CurrentSkin()==null;
        prefs.MatchDayKit="382";UpdateMatchDay();prefs.MatchDayOutfits=false;UpdateMatchDay();checks["disable_outfits_restores_original_set_atomically"]=CurrentSkin()==null;
        row=frame=0;return checks;
    }
}
