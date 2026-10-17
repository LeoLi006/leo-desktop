namespace LeoLauncher;
static class MusicFixTests
{
    static IEnumerable<Control> All(Control parent){yield return parent;foreach(Control c in parent.Controls)foreach(var child in All(c))yield return child;}
    public static Dictionary<string,bool> Check(string directory)
    {
        var checks=new Dictionary<string,bool>();
        using(var music=new MusicForm(true))
        {
            music.Show();Application.DoEvents();
            checks["single_song_ui_has_controls_without_playlist_features"]=All(music).OfType<Button>().Any(b=>b.Text=="播放/暂停")&&!All(music).OfType<Button>().Any(b=>b.Text.Contains("歌单")||b.Text.Contains("同步")||b.Text=="☆");
            music.SearchAsync("晴天").GetAwaiter().GetResult();Application.DoEvents();
            All(music).OfType<Button>().First(b=>b.Text=="播放 ▶").PerformClick();Application.DoEvents();
            checks["single_song_search_and_click_stay_alive"]=!music.IsDisposed&&All(music).OfType<Label>().Any(l=>l.Text.Contains("已发送到QQ音乐：晴天"));
            music.Home();using var image=new Bitmap(music.Width,music.Height);music.DrawToBitmap(image,music.ClientRectangle);image.Save(Path.Combine(directory,"preview-quick-song.png"));
        }
        using(var pet=new PetForm(new Preferences(),true)){pet.Show();pet.ContextMenuStrip!.Show(new Point(10,10));Application.DoEvents();checks["pet_context_menu_fits_short_labels"]=pet.ContextMenuStrip.Width<160;pet.ContextMenuStrip.Close();checks["disposed_pet_ignores_queued_timer_callbacks"]=pet.VerifyDisposedTimerCallbacks();}
        var area=new Rectangle(0,0,1920,1080);var anchor=new Rectangle(600,400,150,34);checks["combo_editor_opens_immediately_beside_anchor"]=PopupPlacement.Beside(anchor,new Size(680,640),area)==new Point(755,400);
        checks["combo_editor_near_edge_remains_visible"]=area.Contains(new Rectangle(PopupPlacement.Beside(new(1800,1000,120,30),new Size(680,640),area),new Size(680,640)));
        return checks;
    }
}
sealed partial class PetForm
{
    public bool VerifyDisposedTimerCallbacks(){Dispose();PollFullscreen();Animate();SetFullscreenHidden(false);Application.DoEvents();return IsDisposed&&!IsHandleCreated&&!fullscreenTimer.Enabled&&!animation.Enabled&&!hoverTimer.Enabled;}
}
