namespace LeoLauncher;
sealed partial class PetForm
{
    bool lyricsHotkeyRegistered;
    async Task ToggleLyricsWithFeedback(){try{OperationFeedback.Show(await commands.ExecuteAsync(new("music_lyrics","","lyrics-hotkey")));}catch(OperationCanceledException){}catch(Exception e){if(!IsDisposed&&!Disposing)OperationFeedback.Error(e.Message);}}

    void WireMusicMenu(ContextMenuStrip menu)
    {
        var media=new ToolStripMenuItem(EntryContributions.Builtin(prefs,"media").MenuName);media.Click+=(_,_)=>{try{EntryContributions.BuiltinAction(prefs,"media");ShowMedia();}catch(Exception e){OperationFeedback.Error(e.Message);}};menu.Items.Insert(Math.Max(0,menu.Items.Count-1),media);menu.Opening+=(_,_)=>{var entry=EntryContributions.Builtin(prefs,"media");media.Text=entry.MenuName;media.Visible=entry.Menu;};
        var root=new ToolStripMenuItem(EntryContributions.Builtin(prefs,"music").MenuName);
        async Task Result(string action,string value="",bool acknowledge=true){var delivery=presentation.Feedback(ModuleWindowTarget.Music,CancellationToken.None);var result=await commands.ExecuteDetailedAsync(new(action,value,"menu"));await delivery.ReportAsync(result,acknowledge);}
        void Item(string name,Func<Task> action){root.DropDownItems.Add(name,null,async(_,_)=>{try{Modules.Require(prefs,"music");if(!IsDisposed)await action();}catch(Exception e){if(!IsDisposed&&!Disposing)OperationFeedback.Error(e.Message);}});}
        Item("快速点歌…",()=>Result(EntryContributions.BuiltinAction(prefs,"music"),acknowledge:false));
        Item("播放 / 暂停",()=>Result("music_pause"));
        Item("上一首",()=>Result("music_step","-1"));
        Item("下一首",()=>Result("music_step","1"));
        var lyric=new ToolStripMenuItem("显示 / 隐藏歌词");lyric.Click+=async(_,_)=>{try{await Result("music_lyrics");}catch(Exception e){if(!IsDisposed)OperationFeedback.Error(e.Message);}};root.DropDownItems.Add(lyric);
        Item("打开QQ音乐",()=>Result("music_client",acknowledge:false));
        menu.Opening+=(_,_)=>{var entry=EntryContributions.Builtin(prefs,"music");root.Text=entry.MenuName;root.Visible=entry.Menu;lyric.ShortcutKeyDisplayString=lyricsHotkeyRegistered?LeoHotkeys.Display(prefs.LyricsHotkey):"";};
        menu.Items.Insert(Math.Max(0,menu.Items.Count-1),root);BarcaMenus.StyleSubmenu(root,menu.Font);BarcaMenus.Apply(menu);
    }
}
