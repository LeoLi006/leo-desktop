namespace LeoLauncher;
static class ModuleEntryRendering
{
    public static async Task Run(Preferences prefs,string id,string source,Func<bool> alive,CancellationToken token=default,int? menuIndex=null)
    {
        try{var result=await EntryContributions.ExecuteModule(prefs,id,source,token,menuIndex);if(alive())OperationFeedback.Show(result.ToLegacy(token));}
        catch(OperationCanceledException){}
        catch(Exception error)when(FeatureFaults.Recoverable(error)){if(alive())OperationFeedback.Error(error.Message);}
    }
    public static void AddMenus(ToolStripMenuItem parent,Preferences prefs,Font font,Func<bool> alive)
    {
        var entries=EntryContributions.ReadModules(prefs).Menu;if(entries.Length==0)return;
        var root=new ToolStripMenuItem("模块入口"){Tag="optional-entry"};
        foreach(var entry in entries){
            ToolStripMenuItem Item(string title,int? index){var item=new ToolStripMenuItem(title){Tag=entry.Id};item.Click+=async(_,_)=>await Run(prefs,entry.Id,"menu",alive,menuIndex:index);return item;}
            if(entry.MenuActions.Length<=1)root.DropDownItems.Add(Item(entry.MenuName,entry.MenuActions.Length==0?null:0));
            else{var group=new ToolStripMenuItem(entry.MenuName);for(int i=0;i<entry.MenuActions.Length;i++)group.DropDownItems.Add(Item(entry.MenuActions[i].Name,i));BarcaMenus.StyleSubmenu(group,font);root.DropDownItems.Add(group);}
        }
        BarcaMenus.StyleSubmenu(root,font);parent.DropDownItems.Add(root);
    }
    public static Bitmap Icon(EntryContribution entry)
    {
        if(LocalApplications.Icon(entry.Icon) is {} image)return image;
        using var glyph=new GlyphButton(entry.Glyph){Size=new(32,32)};var bitmap=new Bitmap(32,32);glyph.DrawToBitmap(bitmap,new(Point.Empty,bitmap.Size));return bitmap;
    }
}
