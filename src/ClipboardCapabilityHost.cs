namespace LeoLauncher;
sealed partial class PetForm
{
    void RegisterClipboardCapability(){var ui=new ModuleWindowService(this);commands.Registry.Register(new ClipboardCapability(launcher.ClipboardService,prefs,ui.ShowAsync,launcher.OpenClipboardCore));launcher.SharedClipboardClient=new(launcher.ClipboardService,commands);}
}
