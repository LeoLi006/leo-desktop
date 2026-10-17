using System.Text.Json;
namespace LeoLauncher;
static class MusicFixTests
{
    static IEnumerable<Control> All(Control parent){yield return parent;foreach(Control c in parent.Controls)foreach(var child in All(c))yield return child;}
    public static Dictionary<string,bool> Check(string directory)
    {
        var checks=new Dictionary<string,bool>();var song=new MusicSong("123","abc","测试歌曲","测试歌手");var old=new MusicPlaylist("local-fixture","旧本地歌单",true,[song]);string file=Path.Combine(directory,"music-sync-regression-"+Guid.NewGuid().ToString("N")+".json");
        File.WriteAllText(file,JsonSerializer.Serialize(new[]{old}));var library=new MusicLibrary(file);checks["local_only_playlists_are_not_exposed"]=library.Items.Count==0;
        var bound=library.Import(new("456","QQ来源",[song],1));checks["legacy_local_data_preserved_after_binding"]=JsonSerializer.Deserialize<MusicPlaylist[]>(File.ReadAllText(file))!.Any(p=>p.Id==old.Id);
        var replacement=song with{Id="124",Mid="abd",Name="替换后的歌曲"};library.Import(new("456","QQ新名称",[replacement],1));checks["qq_song_and_name_changes_replace_cached_data"]=library.Items.Single().Songs.Single().Id=="124"&&library.Items.Single().Name=="QQ新名称";
        File.Delete(file);
        using(var music=new MusicForm(true)){music.Library.Import(new("456","QQ来源",[song],1));music.Home();music.Show();Application.DoEvents();checks["music_ui_has_pause_and_no_local_playlist_editing"]=All(music).OfType<Button>().Any(b=>b.Text=="播放/暂停")&&!All(music).OfType<Button>().Any(b=>b.Text.Contains("＋ 歌单")||b.Text=="☆");All(music).OfType<Button>().Single(b=>b.Text.StartsWith("★")&&b.Text.Contains("QQ来源")).PerformClick();Application.DoEvents();checks["qq_playlist_browse_stays_alive"]=!music.IsDisposed&&All(music).OfType<Label>().Any(l=>l.Text==song.Name);music.Home();using var image=new Bitmap(music.Width,music.Height);music.DrawToBitmap(image,music.ClientRectangle);image.Save(Path.Combine(directory,"preview-qq-music-sync.png"));}
        using(var pet=new PetForm(new Preferences(),true)){pet.Show();pet.ContextMenuStrip!.Show(new Point(10,10));Application.DoEvents();checks["pet_context_menu_is_narrower"]=pet.ContextMenuStrip.Width<=190;pet.ContextMenuStrip.Close();checks["disposed_pet_ignores_queued_timer_callbacks"]=pet.VerifyDisposedTimerCallbacks();}
        return checks;
    }
}
sealed partial class PetForm
{
    public bool VerifyDisposedTimerCallbacks(){Dispose();PollFullscreen();Animate();SetFullscreenHidden(false);Application.DoEvents();return IsDisposed&&!IsHandleCreated&&!fullscreenTimer.Enabled&&!animation.Enabled&&!hoverTimer.Enabled;}
}
