using System.Text.Json;
namespace LeoLauncher;
sealed partial class PetForm
{
    readonly NativeCapabilityQueue proxyCapabilityQueue=new(),mediaCapabilityQueue=new();
    readonly MediaCapabilityState mediaCapabilityState=new();string proxyCapabilityStamp="",mediaCapabilityStamp="";
    void RegisterClientCapabilities()
    {
        string Stamp(string id,bool on)=>BrainCatalog.Hash(JsonSerializer.Serialize(new{on,path=prefs.ModuleExecutables.GetValueOrDefault(id),opening=id=="media"?prefs.ApplicationOpenRules:null,apps=id=="media"?prefs.ApplicationModules:null}));
        string proxyStamp=Stamp("clash",prefs.ClashModule),mediaStamp=Stamp("media",prefs.MediaModule);
        var window=presentation.For(ModuleWindowTarget.Media);
        if(proxyCapabilityStamp!=proxyStamp){
        commands.Registry.ReplaceBuiltin(new ProxyCapability(()=>prefs.ClashModule,()=>Stamp("clash",prefs.ClashModule),()=>{using var passive=ModulePaths.PassiveRead();return ClashProxy.ClientPath()!=null;},t=>Task.Run(ClashProxy.ReadStatus,t),(desired,t,progress)=>Task.Run(()=>ClashProxy.SetEnabled(desired,t,progress),t),proxyCapabilityQueue,previewMode,services:new ClientHostServices("clash",()=>prefs.ClashModule,()=>Stamp("clash",prefs.ClashModule),
            ()=>[new("clash-client","unknown")],()=>{using var passive=ModulePaths.PassiveRead();return ClashProxy.ClientPath()!=null;},commands.Registry.ClientFaults)));
        proxyCapabilityStamp=proxyStamp;
        }
        if(mediaCapabilityStamp!=mediaStamp){
        mediaCapabilityState.Update([]);
        commands.Registry.ReplaceBuiltin(new MediaCapability(()=>prefs.MediaModule,()=>Stamp("media",prefs.MediaModule),MediaControl.SessionsAsync,(a,v,t)=>MediaControl.Service.ExecuteVerifiedAsync(a,v,t),(file,t)=>Task.Run(()=>{t.ThrowIfCancellationRequested();var opener=ApplicationOpenCapabilities.Resolve(prefs).Single(a=>a.Rule.Id=="potplayer");
            if(!opener.Available||opener.Application==null)throw new IOException("应用已停用、移除或程序路径无效。");
            System.Diagnostics.ProcessStartInfo start;
            if(file.Length>0)start=ApplicationOpenCapabilities.Start(opener,[file]);
            else{start=LocalApplications.Start(opener.Application);start.Arguments="";foreach(string arg in FileDropActions.ParseArguments(opener.Application.Arguments))start.ArgumentList.Add(arg);}
            t.ThrowIfCancellationRequested();MediaControl.OpenPotPlayer(file.Length==0?null:file,start);},t),window.ShowAsync,mediaCapabilityQueue,mediaCapabilityState,previewMode,services:new ClientHostServices("media",()=>prefs.MediaModule,()=>Stamp("media",prefs.MediaModule),
            ()=>[new("media-provider",mediaCapabilityState.Read().Length>0?"ready":"unknown"),new("potplayer","unknown")],faults:commands.Registry.ClientFaults)));
        mediaCapabilityStamp=mediaStamp;
        }
    }
}
