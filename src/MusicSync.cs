namespace LeoLauncher;
sealed partial class PetForm
{
    bool lyricsHotkeyRegistered;
    async Task ToggleLyricsWithFeedback(){try{OperationFeedback.Show(await commands.ExecuteAsync(new("music_lyrics","","lyrics-hotkey")));}catch(OperationCanceledException){}catch(Exception e){if(!IsDisposed&&!Disposing)OperationFeedback.Error(e.Message);}}

    void WireMusicMenu(ContextMenuStrip menu)
    {
        var media=new ToolStripMenuItem("媒体控制…");media.Click+=(_,_)=>ShowMedia();menu.Items.Insert(Math.Max(0,menu.Items.Count-1),media);menu.Opening+=(_,_)=>media.Visible=prefs.MediaModule&&EntryDisplay.ShowMenu(prefs,"media");
        var root=new ToolStripMenuItem("音乐");
        void Item(string name,Func<Task> action){root.DropDownItems.Add(name,null,async(_,_)=>{try{Modules.Require(prefs,"music");if(!IsDisposed)await action();}catch(Exception e){if(!IsDisposed&&!Disposing)OperationFeedback.Error(e.Message);}});}
        Item("快速点歌…",async()=>{await commands.ExecuteAsync(new("music_open","","menu"));});
        Item("播放 / 暂停",async()=>{OperationFeedback.Show(await commands.ExecuteAsync(new("music_pause","","menu")));});
        Item("上一首",async()=>{OperationFeedback.Show(await commands.ExecuteAsync(new("music_step","-1","menu")));});
        Item("下一首",async()=>{OperationFeedback.Show(await commands.ExecuteAsync(new("music_step","1","menu")));});
        var lyric=new ToolStripMenuItem("显示 / 隐藏歌词");lyric.Click+=async(_,_)=>await ToggleLyricsWithFeedback();root.DropDownItems.Add(lyric);
        Item("打开QQ音乐",async()=>{await commands.ExecuteAsync(new("music_client","","menu"));});
        menu.Opening+=(_,_)=>{root.Visible=prefs.MusicModule&&EntryDisplay.ShowMenu(prefs,"music");lyric.ShortcutKeyDisplayString=lyricsHotkeyRegistered?LeoHotkeys.Display(prefs.LyricsHotkey):"";};
        menu.Items.Insert(Math.Max(0,menu.Items.Count-1),root);BarcaMenus.StyleSubmenu(root,menu.Font);BarcaMenus.Apply(menu);
    }
}
