namespace LeoLauncher;

static class SkinSelection
{
    public static readonly (string Id,string Name)[] Options=[("83","巴萨（默认）"),("202","阿根廷"),("164","西班牙"),("20232","迈阿密国际"),("382","曼城")];
    public static bool Valid(string? id)=>Options.Any(x=>x.Id==id);
    public static string Id(Preferences prefs)=>Valid(prefs.SkinId)?prefs.SkinId!:prefs.MatchDayOutfits!=false&&Valid(prefs.MatchDayKit)?prefs.MatchDayKit!:"83";
    public static bool Migrate(Preferences prefs)
    {
        string id=Id(prefs);bool changed=prefs.SkinId!=id||prefs.MatchDayKit!=null||prefs.MatchDayOutfits!=null;
        prefs.SkinId=id;prefs.MatchDayKit=null;prefs.MatchDayOutfits=null;return changed;
    }
}

sealed partial class PetForm
{
    void UpdateSkin(){bool changed=SkinSelection.Migrate(prefs);if(changed&&!previewMode)Store.Save(prefs);Invalidate();}
    void WireSkinMenu(ContextMenuStrip menu)
    {
        UpdateSkin();var skins=new ToolStripMenuItem("皮肤");menu.Items.Insert(2,skins);
        void CheckSelected(){foreach(ToolStripMenuItem item in skins.DropDownItems)item.Checked=(string)item.Tag! == SkinSelection.Id(prefs);}
        foreach(var (id,name) in SkinSelection.Options){
            var item=new ToolStripMenuItem(name){Tag=id};item.Click+=(_,_)=>{prefs.SkinId=id;UpdateSkin();CheckSelected();if(!previewMode)Store.Save(prefs);};skins.DropDownItems.Add(item);
        }
        CheckSelected();menu.Opening+=(_,_)=>CheckSelected();
    }
}
