using System.Text.Json;
namespace LeoLauncher;
sealed partial class PetForm
{
    readonly NativeCapabilityQueue proxyCapabilityQueue=new(),mediaCapabilityQueue=new();
    readonly MediaCapabilityState mediaCapabilityState=new();string clientCapabilityStamp="";
    void RegisterClientCapabilities()
    {
        string Stamp(string id,bool on)=>BrainCatalog.Hash(JsonSerializer.Serialize(new{on,path=prefs.ModuleExecutables.GetValueOrDefault(id)}));
        string proxyStamp=Stamp("clash",prefs.ClashModule),mediaStamp=Stamp("media",prefs.MediaModule),stamp=proxyStamp+mediaStamp;
        if(clientCapabilityStamp==stamp)return;
        var windows=new ModuleWindowService(this);
        commands.Registry.ReplaceBuiltin(new ProxyCapability(()=>prefs.ClashModule,()=>Stamp("clash",prefs.ClashModule),()=>{using var passive=ModulePaths.PassiveRead();return ClashProxy.ClientPath()!=null;},t=>Task.Run(ClashProxy.ReadStatus,t),(desired,t,progress)=>Task.Run(()=>ClashProxy.SetEnabled(desired,t,progress),t),proxyCapabilityQueue,previewMode));
        mediaCapabilityState.Update([]);
        commands.Registry.ReplaceBuiltin(new MediaCapability(()=>prefs.MediaModule,()=>Stamp("media",prefs.MediaModule),MediaControl.SessionsAsync,(a,v,t)=>MediaControl.Service.ExecuteVerifiedAsync(a,v,t),(file,t)=>Task.Run(()=>{t.ThrowIfCancellationRequested();MediaControl.OpenPotPlayer(file.Length==0?null:file);},t),t=>windows.ShowAsync(ShowMediaCore,t),mediaCapabilityQueue,mediaCapabilityState,previewMode));
        clientCapabilityStamp=stamp;
    }
}
