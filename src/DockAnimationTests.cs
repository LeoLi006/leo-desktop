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
        using(var main=new Bitmap(Path.Combine(Store.Root,"assets","leo.png")))
        using(var bridge=new DockBridge(Path.Combine(Store.Root,"assets","edge-bridge-v1.png"),main))
        using(var transition=new Bitmap(15*110,4*125))
        using(var drawing=Graphics.FromImage(transition)){
            drawing.Clear(BarcaUi.Canvas);int line=0;
            foreach(var side in new[]{DockSide.Left,DockSide.Right,DockSide.Top,DockSide.Bottom}){
                var unique=new HashSet<string>();bool intact=true;
                for(int f=0;f<15;f++){
                    float p=f<9?f*DockBridge.End/9:DockBridge.End+(f-9)/5f*(1-DockBridge.End);
                    using var image=new Bitmap(110,125);using(var frameGraphics=Graphics.FromImage(image)){
                        var bounds=f<9?bridge.Draw(frameGraphics,image.Size,p,side,sprites):sprites.Draw(frameGraphics,image.Size,PetForm.DockProgress(p,side),side,0,positionProgress:p);
                        intact&=new RectangleF(0,0,110,125).Contains(bounds);
                    }
                    if(f<9)unique.Add(SkinAnimationTests.Hash(image));drawing.DrawImageUnscaled(image,f*110,line*125);
                }
                checks[side+"_has_nine_distinct_early_transition_frames"]=unique.Count==9;
                checks[side+"_early_transition_keeps_ears_inside_window"]=intact;line++;
            }
            transition.Save(Path.Combine(output,"dock-with-inbetweens.png"));
        }
        var sequence=Enumerable.Range(0,6).Select(i=>DockSprites.FrameIndex(i/5f)).ToArray();
        checks["forward_and_reverse_visit_every_pose_in_order"]=sequence.SequenceEqual(new[]{0,1,2,3,4,5})&&sequence.Reverse().SequenceEqual(new[]{5,4,3,2,1,0});
        using var pet=new PetForm(new Preferences(),true);pet.Show();Application.DoEvents();
        foreach(var pair in pet.VerifyDockPeeking(output))checks[pair.Key]=pair.Value;
        foreach(var pair in pet.VerifyDockPolish())checks[pair.Key]=pair.Value;
        return checks;
    }
}
