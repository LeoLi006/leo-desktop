using System.Text.Json;
namespace LeoLauncher;

sealed partial class PetForm
{
    BrainSession CreateBrainSession()=>new(new BrainCatalog(commands.Registry),commands.ExecuteDetailedAsync,VerifyWorkflowStep,()=>{
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        var store=new ComboStore(previewMode?null:ComboStore.FilePath);if(store.Error!=null)return result;
        foreach(var combo in store.Items){try{ComboStore.Validate(combo);var plan=combo.Steps.Select(PlanComboStep).ToArray();if(plan.All(s=>commands.Registry.Resolve(s.Request.Action).Enabled))result[combo.Name]=BrainCatalog.Hash(JsonSerializer.Serialize(plan));}catch(Exception e)when(FeatureFaults.Recoverable(e)){} }
        return result;
    },comboPreview:name=>{
        var combo=new ComboStore(previewMode?null:ComboStore.FilePath).Items.Single(x=>x.Name==name);
        return "执行已保存组合 · "+name+Environment.NewLine+string.Join(Environment.NewLine,combo.Steps.Select(PlanComboStep).Select((step,index)=>"  "+(index+1)+". "+commands.Registry.Resolve(step.Request.Action).Name+" · "+step.Request.Action+(step.Request.Value.Length>0?"："+step.Request.Value:"")));
    });
}

sealed partial class ChatForm
{
    void ShowBrainPlan(BrainSession brain)
    {
        var plan=brain.Pending;if(plan==null||IsDisposed)return;ClearResults();ResultText("执行前确认",true);
        ResultText("以下计划尚未执行，5分钟内有效。确认后可随时按“停止”。");
        var details=new TextBox{Text=string.Join(Environment.NewLine+Environment.NewLine,plan.Preview.Select((line,index)=>(index+1)+". "+line)),Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Width=ResultWidth,Height=180,BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text,Font=BarcaUi.Font(10),BorderStyle=BorderStyle.None,AccessibleName="待确认计划详情"};details.Height=Math.Clamp(TextRenderer.MeasureText(details.Text,details.Font,new Size(Math.Max(100,ResultWidth-32),2000),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl).Height+24,60,210);results.Controls.Add(details);BarcaScrollTheme.Attach(details);
        var run=new BarcaButton{Text="确认执行这 "+plan.Steps.Length+" 步",Width=ResultWidth,Height=38,AccessibleName="确认执行计划"};
        var cancel=new BarcaButton{Text="取消计划",Width=ResultWidth,Height=32};bool allowUnknown=false,started=false,completed=false;
        cancel.Click+=(_,_)=>{if(pending!=null)return;brain.Clear();ShowHome();state.Text=completed?"已返回，刚才的执行结果保留。":started?"已放弃后续计划；已提交的操作不会撤销。":"计划已取消，没有执行。";};
        run.Click+=async(_,_)=>{
            if(pending!=null||chatReleased)return;using var request=CancellationTokenSource.CreateLinkedTokenSource(chatLife.Token);request.CancelAfter(TimeSpan.FromSeconds(120));pending=request;started=true;
            run.Enabled=cancel.Enabled=send.Enabled=configure.Enabled=reset.Enabled=false;UpdateVoice();
            try{var result=await brain.Confirm(plan.Id,(index,step)=>{if(!IsDisposed)state.Text=(index+1)+"/"+plan.Steps.Length+" · "+step.Label;},request.Token,allowUnknown);if(IsDisposed)return;completed=true;run.Text="已完成 "+result.Steps.Count(s=>s.State=="succeeded")+" 步";state.Text=lastAnswer="计划已完成；应用启动只确认请求已提交。";cancel.Text="返回";cancel.Enabled=true;}
            catch(WorkflowRecoveryRequired e){allowUnknown=true;if(!IsDisposed){state.Text=e.Message;run.Text="确认重试此步（可能重复）";run.Enabled=true;cancel.Enabled=true;}}
            catch(OperationCanceledException){if(!IsDisposed){state.Text="已停止，后续步骤未执行；已提交的操作请核对状态。";run.Text="核验后重试";run.Enabled=cancel.Enabled=true;}}
            catch(Exception e)when(FeatureFaults.Recoverable(e)){if(!IsDisposed){state.Text=e.Message;run.Text="核验后重试";run.Enabled=cancel.Enabled=true;}}
            finally{pending=null;if(!IsDisposed){send.Enabled=configure.Enabled=reset.Enabled=true;UpdateVoice();}}
        };results.Controls.Add(run);results.Controls.Add(cancel);
    }
}
