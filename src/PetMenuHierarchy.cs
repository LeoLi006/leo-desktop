namespace LeoLauncher;
static class PetMenuHierarchy
{
    public static void Organize(ContextMenuStrip menu,Action notes,Action chat,Action voice,Action launcher,Action appearance)
    {
        var entries=menu.Items.OfType<ToolStripMenuItem>().ToArray();ToolStripMenuItem? Find(string name)=>entries.FirstOrDefault(e=>UiLanguage.Source(e)==name||UiLanguage.Source(e).StartsWith(name));
        var records=new ToolStripMenuItem("清单与记录");var applications=new ToolStripMenuItem("应用与媒体");var looks=new ToolStripMenuItem("外观");
        void Move(ToolStripMenuItem? item,ToolStripMenuItem parent){if(item==null)return;menu.Items.Remove(item);parent.DropDownItems.Add(item);}
        Move(Find("每日清单"),records);records.DropDownItems.Add("临时便签…",null,(_,_)=>notes());Move(Find("通知收纳盒"),records);Move(Find("临时专注"),records);
        applications.DropDownItems.Add("应用搜索…",null,(_,_)=>launcher());applications.DropDownItems.Add("文字聊天…",null,(_,_)=>chat());applications.DropDownItems.Add("直接说话…",null,(_,_)=>voice());Move(Find("音乐"),applications);Move(Find("媒体控制"),applications);
        Move(Find("皮肤"),looks);looks.DropDownItems.Add("主题与皮肤编辑器…",null,(_,_)=>appearance());var combo=Find("自定义");if(combo!=null){menu.Items.Remove(combo);combo.Text="组合动作";}
        var settings=Find("设置");var exit=Find("退出");menu.Items.Clear();menu.Items.Add(records);menu.Items.Add(applications);if(combo!=null)menu.Items.Add(combo);menu.Items.Add(looks);menu.Items.Add(new ToolStripSeparator());if(settings!=null)menu.Items.Add(settings);if(exit!=null)menu.Items.Add(exit);foreach(ToolStripMenuItem root in menu.Items.OfType<ToolStripMenuItem>())BarcaMenus.StyleSubmenu(root,menu.Font);if(menu.Renderer is BarcaMenuRenderer)BarcaMenus.Fit(menu);else BarcaMenus.Apply(menu);
    }
}
