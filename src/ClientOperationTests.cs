namespace LeoLauncher;
static class ClientOperationTests
{
    public static async Task<Dictionary<string,bool>> Check()
    {
        var checks=new Dictionary<string,bool>();var hub=new ClientOperationHub(_=>{});int runs=0;
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<string> Work(ClientOperation op){Interlocked.Increment(ref runs);entered.TrySetResult();await release.Task.WaitAsync(op.Token);return "done";}
        var a=hub.Run("client","same",Work);await entered.Task;var b=hub.Run("client","same",Work);release.SetResult();await Task.WhenAll(a,b);
        checks["identical_clicks_share_one_execution"]=runs==1&&ReferenceEquals(a,b);
        hub=new(_=>{});int active=0,max=0;var order=new List<int>();
        async Task<string> Ordered(int index,ClientOperation op){int n=Interlocked.Increment(ref active);max=Math.Max(max,n);await Task.Delay(20,op.Token);lock(order)order.Add(index);Interlocked.Decrement(ref active);return "done";}
        await Task.WhenAll(hub.Run("client","on",op=>Ordered(1,op)),hub.Run("client","off",op=>Ordered(2,op)));
        checks["different_commands_are_serialized_per_client"]=max==1&&order.SequenceEqual(new[]{1,2});
        hub=new(_=>{});using var cancel=new CancellationTokenSource();entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending=hub.Run("client","wait",async op=>{entered.SetResult();await Task.Delay(Timeout.Infinite,op.Token);return "unexpected";},cancel.Token);await entered.Task;cancel.Cancel();
        bool canceled=false;try{await pending;}catch(OperationCanceledException){canceled=true;}checks["cancel_stops_waiting_before_action"]=canceled;
        var notices=new System.Collections.Concurrent.ConcurrentQueue<LeoNotice>();hub=new(notices.Enqueue);entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed=hub.Run("client","slow",async op=>{op.Report("正在启动测试客户端…");entered.SetResult();await Task.Delay(Timeout.Infinite,op.Token);return "unexpected";});await entered.Task;await Task.Delay(2000);
        var offer=notices.LastOrDefault(n=>n.Busy&&n.ActionLabel=="取消"&&n.Retry!=null);if(offer?.Retry!=null)await offer.Retry();try{await delayed;}catch(OperationCanceledException){}
        checks["slow_operation_exposes_working_cancel"]=offer!=null&&delayed.IsCanceled;
        hub=new(_=>{});int failures=0;try{await hub.Run("client","retry",_=>{failures++;throw new IOException("fixture failure");});}catch(IOException){}
        string recovered=await hub.Run("client","retry",_=>Task.FromResult("recovered"));checks["failed_request_is_removed_and_can_retry"]=failures==1&&recovered=="recovered";
        return checks;
    }
}
