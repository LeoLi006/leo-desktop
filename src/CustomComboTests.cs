using System.Text.Json;
namespace LeoLauncher;

static class CustomComboTests
{
    static IEnumerable<Control> All(Control parent){yield return parent;foreach(Control child in parent.Controls)foreach(var item in All(child))yield return item;}
    public static Dictionary<string,bool> Check(string directory)
    {
        var checks=new Dictionary<string,bool>();string root=Path.Combine(directory,"combos-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string file=Path.Combine(root,"leo-custom-combos.json"),legacy=Path.Combine(root,"leo-scenes.json");
        File.WriteAllText(legacy,JsonSerializer.Serialize(new WatchScene{Proxy=false,WatchIndex=2,PlaylistId="fixture-list"}));
        var store=new ComboStore(file);var migrated=store.Items.Single();
        checks["legacy_watch_scene_migrates_without_changing_options"]=migrated.Name=="准备看球"&&migrated.Steps.Count==2&&migrated.Steps[0].Value=="2"&&migrated.Steps[1].Value=="fixture-list"&&!File.Exists(file);
        using(var editor=new ComboEditor(store,null,()=>[new AppEntry("测试应用","fixture-app")],true))
        {
            editor.Show();Application.DoEvents();
            All(editor).OfType<TextBox>().Single(t=>t.PlaceholderText.StartsWith("例如")).Text="晚间放松";
            All(editor).OfType<Button>().Single(b=>b.Text=="添加一步").PerformClick();
            var kind=All(editor).OfType<ComboBox>().Single(c=>c.Items.OfType<ComboChoice>().Any(i=>i.Value=="url"));
            kind.SelectedItem=kind.Items.Cast<ComboChoice>().Single(i=>i.Value=="url");
            var url=All(editor).OfType<TextBox>().Single(t=>t.PlaceholderText.StartsWith("填写网址"));url.Text="https://example.com/relax";
            All(editor).OfType<Button>().Single(b=>b.Text=="添加一步").PerformClick();
            All(editor).OfType<Button>().Single(b=>b.Text=="上移").PerformClick();Application.DoEvents();
            using(var image=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(image,editor.ClientRectangle);image.Save(Path.Combine(directory,"preview-custom-combo-editor.png"));}
            checks["custom_combo_editor_controls_fit"]=editor.Controls.Cast<Control>().All(c=>editor.ClientRectangle.Contains(c.Bounds));
            All(editor).OfType<Button>().Single(b=>b.Text=="保存组合").PerformClick();checks["custom_combo_editor_saves"]=editor.DialogResult==DialogResult.OK;
        }
        var reloaded=new ComboStore(file);var added=reloaded.Items.Single(c=>c.Name=="晚间放松");
        checks["custom_combo_order_and_values_persist"]=added.Steps.Select(s=>s.Kind).SequenceEqual(new[]{"url","app"})&&added.Steps[1].Value=="fixture-app";
        bool rejected=false;try{reloaded.Put(new("other","晚间放松",[new("todos")]));}catch(ArgumentException){rejected=true;}checks["duplicate_combo_name_rejected"]=rejected;
        rejected=false;try{reloaded.Put(new("bad","非法网址",[new("url","javascript:alert(1)")]));}catch(ArgumentException){rejected=true;}checks["custom_combo_rejects_invalid_urls"]=rejected;
        rejected=false;try{reloaded.Put(new("bad","任意命令",[new("shell","cmd.exe")]));}catch(ArgumentException){rejected=true;}checks["custom_combo_rejects_unsupported_commands"]=rejected;
        reloaded.Delete(added.Id);checks["custom_combo_delete_persists"]=new ComboStore(file).Items.All(c=>c.Id!=added.Id);
        File.WriteAllText(file,"broken fixture");var corrupt=new ComboStore(file);rejected=false;try{corrupt.Put(added);}catch(IOException){rejected=true;}checks["corrupt_combo_file_is_preserved"]=rejected&&File.ReadAllText(file)=="broken fixture";
        checks["custom_combo_voice_text_routes_without_model"]=QuickRouter.Route("运行组合晚间放松").Value=="combo_name"&&QuickRouter.Route("运行组合晚间放松").Argument=="晚间放松";
        using(var pet=new PetForm(new Preferences(),true))
        {
            pet.Show();var menu=pet.ContextMenuStrip!;menu.Show(new Point(20,20));Application.DoEvents();var rootMenu=menu.Items.OfType<ToolStripMenuItem>().Single(i=>i.Text=="自定义");rootMenu.ShowDropDown();Application.DoEvents();checks["custom_combo_submenu_contains_presets_and_create"]=rootMenu.DropDown.Visible&&rootMenu.DropDownItems.Cast<ToolStripItem>().Any(i=>i.Text=="准备看球")&&rootMenu.DropDownItems.Cast<ToolStripItem>().Any(i=>i.Text=="＋ 新建自定义…")&&menu.Items.Cast<ToolStripItem>().All(i=>i.Text!="准备看球");using var image=new Bitmap(rootMenu.DropDown.Width,rootMenu.DropDown.Height);rootMenu.DropDown.DrawToBitmap(image,rootMenu.DropDown.ClientRectangle);image.Save(Path.Combine(directory,"preview-custom-combo-menu.png"));rootMenu.HideDropDown();menu.Close();
        }
        File.Delete(file);File.Delete(legacy);Directory.Delete(root);
        return checks;
    }
}
