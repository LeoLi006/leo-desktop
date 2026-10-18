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
