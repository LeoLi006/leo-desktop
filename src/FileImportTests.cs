using System.Diagnostics;
using System.Text.Json;
namespace LeoLauncher;
static class FileImportTests
{
    public static async Task Check(Dictionary<string,bool> c,string folder)
    {
        string source=Path.Combine(folder,"中文 %USER% & note.md"),exe=Path.Combine(folder,"pythonw.exe"),script=Path.Combine(folder,"import gui.py");
        foreach(string file in new[]{source,exe,script})File.WriteAllText(file,"isolated fixture; never execute");
        var local=new LocalApplication(Guid.NewGuid().ToString("N"),"Import tool",exe,Arguments:"--no-install",WorkingDirectory:folder);
        var rule=ApplicationOpenCapabilities.Defaults().Single(r=>r.Id=="mojian") with{ModuleId=local.Id,ImportScript=script};
        var prefs=new Preferences{ApplicationIndexing=false,ApplicationModules=[local],ApplicationOpenRules=[rule]};
        var resolved=ApplicationOpenCapabilities.Resolve(prefs).Single(a=>a.Rule.Id==rule.Id);
        c["handoff_config_resolves_without_start"]=resolved.Available;
        int starts=0;bool structured=false;string lastRequest="";
        Task<bool> Start(ProcessStartInfo info,CancellationToken token){
            starts++;lastRequest=info.ArgumentList.Last();
            using var request=JsonDocument.Parse(File.ReadAllText(lastRequest));var r=request.RootElement;
            structured=info.FileName==exe&&info.WorkingDirectory==folder&&info.Verb==""&&info.ArgumentList.SequenceEqual(new[]{script,"--no-install","--import-request",lastRequest})&&r.GetProperty("input").GetString()==source;
            File.WriteAllText(Path.ChangeExtension(lastRequest,".receipt.json"),JsonSerializer.Serialize(new{protocol=FileImportHandoff.Protocol,requestId=r.GetProperty("requestId").GetString(),input=source,state="imported"}));
            return Task.FromResult(true);
        }
        string payload=JsonSerializer.Serialize(new{mode="capability",opener=rule.Id,expected=resolved.Stamp,paths=new[]{source}});
        string result=await DropOpenExecution.Execute(prefs,payload,default,Start,_=>false,()=>null);
        c["handoff_uses_shared_dispatch_adapter_and_receipt"]=starts==1&&structured&&result.Contains("已导入")&&result.Contains("尚未完成转换");
        c["handoff_transport_cleaned_input_untouched"]=!File.Exists(lastRequest)&&File.ReadAllText(source)=="isolated fixture; never execute";
        result=await FileImportHandoff.Execute(resolved,[source],default,(_,_)=>Task.FromResult(true),TimeSpan.FromMilliseconds(20),folder);
        c["handoff_timeout_never_claims_import_or_conversion"]=result.Contains("未收到导入确认")&&result.Contains("未确认转换完成");
        using(var stop=new CancellationTokenSource()){
            bool cancelled=false;try{await FileImportHandoff.Execute(resolved,[source],stop.Token,(_,_)=>{stop.Cancel();return Task.FromResult(true);},temporaryRoot:folder);}catch(OperationCanceledException e){cancelled=e.Message.Contains("未撤回");}
            c["handoff_cancel_is_accurate_after_submission"]=cancelled;
        }
        bool rejected=false;
        try{await FileImportHandoff.Execute(resolved,[source],default,(info,_)=>{
            string request=info.ArgumentList.Last();File.WriteAllText(Path.ChangeExtension(request,".receipt.json"),"{\"protocol\":\"wrong\"}");return Task.FromResult(true);
        },temporaryRoot:folder);}catch(IOException){rejected=true;}
        c["handoff_wrong_identity_not_accepted"]=rejected;
        rejected=false;try{await FileImportHandoff.Execute(resolved,[source,script],default,Start,temporaryRoot:folder);}catch(ArgumentException){rejected=true;}
        c["handoff_batch_never_silently_truncated"]=rejected&&starts==1;
        var disabled=LocalApplications.Clone(prefs);disabled.ApplicationModules[0]=local with{Enabled=false};
        rejected=false;try{await DropOpenExecution.Execute(disabled,payload,default,Start,_=>false,()=>null);}catch(IOException){rejected=true;}
        c["handoff_disabled_module_refuses_old_click"]=rejected&&starts==1;
        var changed=LocalApplications.Clone(prefs);changed.ApplicationOpenRules[0]=rule with{ImportScript=source};
        rejected=false;try{await DropOpenExecution.Execute(changed,payload,default,Start,_=>false,()=>null);}catch(InvalidOperationException){rejected=true;}
        c["handoff_script_change_invalidates_old_click"]=rejected&&starts==1;
        var reused=resolved with{Application=local with{Arguments=FileDropActions.QuoteArgument(script)+" --no-install"}};
        result=await FileImportHandoff.Execute(reused,[source],default,Start,temporaryRoot:folder);
        c["handoff_existing_launcher_script_argument_not_duplicated"]=structured&&result.Contains("已导入");
        rejected=false;try{await FileImportHandoff.Execute(resolved,[source],default,(info,_)=>{
            string request=info.ArgumentList.Last();using var data=JsonDocument.Parse(File.ReadAllText(request));
            File.WriteAllText(Path.ChangeExtension(request,".receipt.json"),JsonSerializer.Serialize(new{protocol=FileImportHandoff.Protocol,requestId=data.RootElement.GetProperty("requestId").GetString(),input=source,state="failed",code="startup_failed"}));return Task.FromResult(true);
        },temporaryRoot:folder);}catch(IOException e){rejected=e.Message.Contains("导入失败");}
        c["handoff_tool_failure_not_reported_as_success"]=rejected;
        string removed=Path.Combine(folder,"removed.md");File.WriteAllText(removed,"fixture");File.Delete(removed);
        rejected=false;try{await FileImportHandoff.Execute(resolved,[removed],default,Start,temporaryRoot:folder);}catch(IOException){rejected=true;}
        c["handoff_deleted_input_refused_before_launch"]=rejected&&starts==2;
        File.Delete(script);c["handoff_missing_script_unavailable"]=!ApplicationOpenCapabilities.Resolve(prefs).Single(a=>a.Rule.Id==rule.Id).Available;
        c["markdown_prioritizes_processing_and_code"]=DropActionForm.PrimaryIds(["default","code","mojian","choose_app","note"],true).SequenceEqual(new[]{"mojian","code","default"});
        c["pdf_prioritizes_reader_not_all_apps"]=DropActionForm.PrimaryIds(["default","foxit","annotate","sumatra","choose_app"],true).SequenceEqual(new[]{"sumatra","default"});
        c["folders_prioritize_code_and_explorer"]=DropActionForm.PrimaryIds(["default","code","choose_app"],true).SequenceEqual(new[]{"code","default"});
        c["handoff_draft_preserves_script_and_uses_private_app_reference"]=LocalApplications.Clone(prefs).ApplicationOpenRules.Single().ImportScript==script&&!ApplicationPackages.Export(ApplicationPackages.Create(local)).Contains(folder);
        File.WriteAllText(script,"isolated fixture; never execute");File.Delete(exe);
        c["handoff_missing_program_is_unavailable"]=!ApplicationOpenCapabilities.Resolve(prefs).Single(a=>a.Rule.Id==rule.Id).Available;
        File.WriteAllText(exe,"isolated fixture; never execute");
        var absent=LocalApplications.Clone(prefs);absent.ApplicationModules.Clear();
        c["handoff_removed_module_is_unavailable"]=ApplicationOpenCapabilities.Resolve(absent).Single(a=>a.Rule.Id==rule.Id).State=="removed";
        using(var panel=new DropActionForm(new(){Paths=[source]},absent,(_,_)=>Task.FromResult("unused"),_=>{},_=>{},_=>{},[],configure:()=>{})){
            panel.Show();Application.DoEvents();var buttons=panel.Controls.OfType<FlowLayoutPanel>().SelectMany(f=>f.Controls.OfType<Button>()).ToArray();
            c["handoff_absent_tool_only_disables_its_button"]=!buttons.Single(b=>b.Tag as string=="mojian").Enabled&&buttons.Single(b=>b.Tag as string=="configure_open").Enabled&&!panel.IsDisposed;
            panel.Close();
        }
        int configs=0;
        using(var panel=new DropActionForm(new(){Paths=[source]},prefs,(_,_)=>Task.FromException<string>(new IOException("isolated startup failure")),_=>{},_=>{},_=>{},[],configure:()=>configs++)){
            panel.Show();Application.DoEvents();var buttons=panel.Controls.OfType<FlowLayoutPanel>().SelectMany(f=>f.Controls.OfType<Button>()).ToArray();
            buttons.Single(b=>b.Tag as string=="mojian").PerformClick();
            c["handoff_launch_failure_does_not_close_or_disable_panel"]=!panel.IsDisposed&&buttons.Single(b=>b.Tag as string=="mojian").Enabled;
            buttons.Single(b=>b.Tag as string=="configure_open").PerformClick();c["handoff_other_action_survives_failure"]=configs==1;
            panel.Close();
        }
        foreach(string text in new[]{"文件快捷处理","转为墨笺手写","导入预览，生成需确认","用 SumatraPDF 阅读"}){
            string old=UiLanguage.Current;UiLanguage.Configure("en");c["translated_"+text]=UiLanguage.T(text)!=text;UiLanguage.Configure(old);
        }
    }
}
