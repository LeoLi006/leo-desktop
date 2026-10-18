namespace LeoLauncher;
static class PetTransparencyTests
{
    public static Dictionary<string,bool> Check(string output)
    {
        Directory.CreateDirectory(output);var checks=new Dictionary<string,bool>();
        using(var sample=new Bitmap(8,1)){
            int[] alpha=[0,1,64,127,128,200,254,255];
            for(int x=0;x<8;x++)sample.SetPixel(x,0,Color.FromArgb(alpha[x],245,140,190));
            PetTransparency.ResolveCoverage(sample);
            checks["faint_background_pixels_become_fully_transparent"]=Enumerable.Range(0,4).All(x=>sample.GetPixel(x,0).A==0);
            checks["opaque_pink_clothing_is_preserved_without_color_key_filtering"]=Enumerable.Range(4,4).All(x=>sample.GetPixel(x,0)==Color.FromArgb(255,245,140,190));
        }
        using var pet=new PetForm(new Preferences(),true);pet.Show();Application.DoEvents();
        foreach(var pair in pet.VerifyTransparency(output))checks[pair.Key]=pair.Value;
        return checks;
    }
    internal static int PinkLeak(Bitmap image)
    {
        int count=0;for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){
            var p=image.GetPixel(x,y);if(p.A>0&&p.R>180&&p.B>180&&p.G<100&&p.ToArgb()!=Color.Magenta.ToArgb())count++;
        }return count;
    }
}
sealed partial class PetForm
{
    internal Dictionary<string,bool> VerifyTransparency(string output)
    {
        var checks=new Dictionary<string,bool>();Size=new(136,148);row=frame=0;
        using var preview=new Bitmap(6*Width,5*Height);using var g=Graphics.FromImage(preview);g.Clear(Color.FromArgb(24,24,24));
        int line=0;bool reproduced=false;
        foreach(var id in new[]{"83","382","20232","202","164"}){
            prefs.MatchDayKit=id;prefs.MatchDayOutfits=true;UpdateMatchDay();RevealDock(true);
            using(var before=new Bitmap(Width,Height))using(var drawing=Graphics.FromImage(before)){
                drawing.Clear(Color.Magenta);drawing.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;DrawPet(drawing,PetDrawingBounds(ClientSize));reproduced|=PetTransparencyTests.PinkLeak(before)>0;
            }
            int leaks=0;for(int stage=0;stage<6;stage++){
                RevealDock(true);
                if(stage>0){dockSide=stage==5?DockSide.Left:(DockSide)stage;dockMotion.BeginTuck(0);for(int t=32;t<=(stage==5?850:200);t+=32)dockMotion.Tick(t,true,false);}
                using var image=new Bitmap(Width,Height);DrawToBitmap(image,ClientRectangle);leaks+=PetTransparencyTests.PinkLeak(image);
                using var attributes=new System.Drawing.Imaging.ImageAttributes();attributes.SetColorKey(Color.Magenta,Color.Magenta);
                g.DrawImage(image,new Rectangle(stage*Width,line*Height,Width,Height),0,0,Width,Height,GraphicsUnit.Pixel,attributes);
            }
            checks[id+"_actual_window_idle_and_four_edge_animations_have_no_pink_key_fringe"]=leaks==0;line++;
        }
        checks["regression_reproduces_original_direct_rendering_defect"]=reproduced;
        preview.Save(Path.Combine(output,"no-pink-fringe.png"));return checks;
    }
}
