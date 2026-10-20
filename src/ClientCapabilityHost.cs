using System.Text.Json;
namespace LeoLauncher;
sealed partial class PetForm
{
    readonly NativeCapabilityQueue proxyCapabilityQueue=new(),mediaCapabilityQueue=new();
    readonly MediaCapabilityState mediaCapabilityState=new();string proxyCapabilityStamp="",mediaCapabilityStamp="";
    void RegisterClientCapabilities()
    {
        string Stamp(string id,bool on)=>BrainCatalog.Hash(JsonSerializer.Serialize(new{on,path=prefs.ModuleExecutables.GetValueOrDefault(id)}));
        string proxyStamp=Stamp("clash",prefs.ClashModule),mediaStamp=Stamp("media",prefs.MediaModule);
        var windows=new ModuleWindowService(this);
        if(proxyCapabilityStamp!=proxyStamp){
        commands.Registry.ReplaceBuiltin(new ProxyCapability(()=>prefs.ClashModule,()=>Stamp("clash",prefs.ClashModule),()=>{using var passive=ModulePaths.PassiveRead();return ClashProxy.ClientPath()!=null;},t=>Task.Run(ClashProxy.ReadStatus,t),(desired,t,progress)=>Task.Run(()=>ClashProxy.SetEnabled(desired,t,progress),t),proxyCapabilityQueue,previewMode,services:new ClientHostServices("clash",()=>prefs.ClashModule,()=>Stamp("clash",prefs.ClashModule),
            ()=>[new("clash-client","unknown")],()=>{using var passive=ModulePaths.PassiveRead();return ClashProxy.ClientPath()!=null;},commands.Registry.ClientFaults)));
        proxyCapabilityStamp=proxyStamp;
        }
        if(mediaCapabilityStamp!=mediaStamp){
        mediaCapabilityState.Update([]);
        commands.Registry.ReplaceBuiltin(new MediaCapability(()=>prefs.MediaModule,()=>Stamp("media",prefs.MediaModule),MediaControl.SessionsAsync,(a,v,t)=>MediaControl.Service.ExecuteVerifiedAsync(a,v,t),(file,t)=>Task.Run(()=>{t.ThrowIfCancellationRequested();MediaControl.OpenPotPlayer(file.Length==0?null:file);},t),t=>windows.ShowAsync(ShowMediaCore,t),mediaCapabilityQueue,mediaCapabilityState,previewMode,services:new ClientHostServices("media",()=>prefs.MediaModule,()=>Stamp("media",prefs.MediaModule),
            ()=>[new("media-provider",mediaCapabilityState.Read().Length>0?"ready":"unknown"),new("potplayer","unknown")],faults:commands.Registry.ClientFaults)));
        mediaCapabilityStamp=mediaStamp;
        }
    }
}
