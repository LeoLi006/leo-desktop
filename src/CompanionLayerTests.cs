using System.Text.Json;
namespace LeoLauncher;
static class CompanionLayerTests
{
    public static void Run(string report){Directory.CreateDirectory(Path.GetDirectoryName(report)!);try{using var pet=new PetForm(new Preferences{ApplicationIndexing=false},true);pet.Show();Application.DoEvents();var checks=pet.VerifyCompanionLayer();pet.Close();File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString()}));}}
}
sealed partial class PetForm
{
    internal Dictionary<string,bool> VerifyCompanionLayer()
    {
        var c=new Dictionary<string,bool>();using var cover=new Form{StartPosition=FormStartPosition.Manual,Bounds=Bounds,Text="Layer fixture"};cover.Show();cover.Activate();Application.DoEvents();
        var foreground=CompanionLayer.GetForegroundWindow();var bounds=Bounds;CompanionLayer.SetWindowPos(Handle,new IntPtr(-2),0,0,0,0,0x1|0x2|0x10);c["fixture_can_remove_native_topmost"]=!CompanionLayer.IsTopmost(this);
        RestoreCompanionLayer();c["native_topmost_is_restored_for_pet_and_hit_surface"]=CompanionLayer.IsTopmost(this)&&CompanionLayer.IsTopmost(hit);c["repair_does_not_activate_or_move_pet"]=CompanionLayer.GetForegroundWindow()==foreground&&Bounds==bounds;
        CompanionLayer.ShowWindow(Handle,0);cover.Activate();Application.DoEvents();foreground=CompanionLayer.GetForegroundWindow();RestoreCompanionLayer();c["unexpected_native_hide_is_recovered"]=CompanionLayer.IsWindowVisible(Handle)&&CompanionLayer.IsWindowVisible(hit.Handle);
        c["hidden_repair_preserves_foreground"]=CompanionLayer.GetForegroundWindow()==foreground;
        SetFullscreenHidden(true);RestoreCompanionLayer();c["intentional_fullscreen_hide_is_respected"]=!Visible&&!hit.Visible&&!foot.Visible;SetFullscreenHidden(false);RestoreCompanionLayer();c["fullscreen_exit_restores_visible_topmost_group"]=Visible&&hit.Visible&&foot.Visible&&CompanionLayer.IsTopmost(this);
        dockSide=DockSide.Left;dockMotion.BeginTuck(0);for(int t=0;t<=2400;t+=32)dockMotion.Tick(t,true,false);foot.Hide();RestoreCompanionLayer();c["tucked_pet_does_not_reshow_footer"]=!foot.Visible&&dockMotion.Progress>=.999f;
        RevealDock(true);ShowChat();RestoreCompanionLayer();c["chat_remains_an_ordinary_non_topmost_window"]=chat!=null&&!chat.TopMost&&!CompanionLayer.IsTopmost(chat);chat?.Close();
        bool preserved=false;using(var dialog=new Form{StartPosition=FormStartPosition.Manual,Location=Location}){using var timer=new System.Windows.Forms.Timer{Interval=60};timer.Tick+=(_,_)=>{timer.Stop();var active=CompanionLayer.GetForegroundWindow();RestoreCompanionLayer();preserved=!CompanionLayer.IsWindowEnabled(Handle)&&CompanionLayer.GetForegroundWindow()==active;dialog.Close();};timer.Start();dialog.ShowDialog(this);}c["modal_dialog_keeps_focus_and_disabled_owner_is_not_raised"]=preserved;
        cover.Close();return c;
    }
}
