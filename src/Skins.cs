namespace LeoLauncher;

static class SkinSelection
{
    public static readonly (string Id,string Name)[] Options=[("83","巴萨（默认）"),("202","阿根廷"),("164","西班牙"),("20232","迈阿密国际"),("382","曼城")];
    public static bool Valid(string? id)=>Options.Any(x=>x.Id==id)||SkinLibrary.Find(id)!=null;
    public static string Id(Preferences prefs)=>Valid(prefs.SkinId)?prefs.SkinId!:prefs.MatchDayOutfits!=false&&Valid(prefs.MatchDayKit)?prefs.MatchDayKit!:"83";
    public static bool Migrate(Preferences prefs)
    {
        string id=Id(prefs);bool changed=prefs.SkinId!=id||prefs.MatchDayKit!=null||prefs.MatchDayOutfits!=null;
        prefs.SkinId=id;prefs.MatchDayKit=null;prefs.MatchDayOutfits=null;return changed;
    }
}

sealed partial class PetForm
{
    void UpdateSkin(){bool changed=SkinSelection.Migrate(prefs);if(changed&&!previewMode)Store.Save(prefs);UiTheme.Configure(SkinSelection.Id(prefs),prefs.DisplayName,prefs.SkinThemes.GetValueOrDefault(SkinSelection.Id(prefs)));if(tray!=null)tray.Text=UiTheme.Name+" · 桌面伙伴";Invalidate();}
    void WireSkinMenu(ContextMenuStrip menu)
    {
        UpdateSkin();var skins=new ToolStripMenuItem("皮肤");menu.Items.Insert(2,skins);
        void CheckSelected(){foreach(ToolStripMenuItem item in skins.DropDownItems.OfType<ToolStripMenuItem>().Where(i=>i.Tag is string))item.Checked=(string)item.Tag! == SkinSelection.Id(prefs);}
        foreach(var (id,name) in SkinSelection.Options){
            var item=new ToolStripMenuItem(name){Tag=id};item.Click+=(_,_)=>{prefs.SkinId=id;UpdateSkin();CheckSelected();if(!previewMode)Store.Save(prefs);};skins.DropDownItems.Add(item);
        }
        CheckSelected();menu.Opening+=(_,_)=>{foreach(var old in skins.DropDownItems.OfType<ToolStripMenuItem>().Where(i=>i.Tag is string id&&id.StartsWith("custom:")).ToArray()){skins.DropDownItems.Remove(old);old.Dispose();}foreach(var custom in SkinLibrary.Load()){var option=new ToolStripMenuItem(custom.Name){Tag=custom.Id};option.Click+=(_,_)=>{prefs.SkinId=custom.Id;UpdateSkin();if(!previewMode)Store.Save(prefs);};skins.DropDownItems.Add(option);}CheckSelected();};
    }
}
