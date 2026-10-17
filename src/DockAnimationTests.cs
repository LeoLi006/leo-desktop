namespace LeoLauncher;

static class DockAnimationTests
{
    public static Dictionary<string,bool> Check(string output)
    {
        Directory.CreateDirectory(output);var checks=new Dictionary<string,bool>();
        using var sprites=new DockSprites();using var contact=new Bitmap(768,560);using var graphics=Graphics.FromImage(contact);graphics.Clear(BarcaUi.Canvas);
        int row=0;
        foreach(var side in new[]{DockSide.Left,DockSide.Right,DockSide.Top,DockSide.Bottom}){
            var sources=new HashSet<Rectangle>();bool intact=true;var rendered=new HashSet<string>();
            for(int frame=0;frame<6;frame++){
                float progress=frame/5f;sources.Add(sprites.Source(progress,0,side));
                foreach(int size in new[]{80,136,230}){
                    var source=sprites.Source(progress,0,side);var bounds=sprites.Destination(new Size(size,size*208/192),progress,side,source);
                    intact&=new RectangleF(0,0,size,size*208/192).Contains(bounds);
                }
                using var image=new Bitmap(128,140);using(var g=Graphics.FromImage(image)){g.Clear(BarcaUi.Canvas);sprites.Draw(g,image.Size,progress,side,0);}
                using var stream=new MemoryStream();image.Save(stream,System.Drawing.Imaging.ImageFormat.Png);rendered.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream.ToArray())));
                graphics.DrawImageUnscaled(image,frame*128,row*140);
            }
            checks[side+"_has_six_distinct_pose_sources"]=sources.Count==6;
            checks[side+"_renders_six_distinct_frames"]=rendered.Count==6;
            checks[side+"_keeps_all_frames_inside_window_at_all_sizes"]=intact;row++;
        }
        contact.Save(Path.Combine(output,"dock-six-frames.png"));
        var sequence=Enumerable.Range(0,6).Select(i=>DockSprites.FrameIndex(i/5f)).ToArray();
        checks["forward_and_reverse_visit_every_pose_in_order"]=sequence.SequenceEqual(new[]{0,1,2,3,4,5})&&sequence.Reverse().SequenceEqual(new[]{5,4,3,2,1,0});
        using var pet=new PetForm(new Preferences(),true);pet.Show();Application.DoEvents();
        foreach(var pair in pet.VerifyDockPeeking(output))checks[pair.Key]=pair.Value;
        return checks;
    }
}
