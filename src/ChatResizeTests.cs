using System.Text.Json;
namespace LeoLauncher;
static class ChatResizeTests
{
    public static void Run(string path){Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var host=new Form{Opacity=0,ShowInTaskbar=false};host.Shown+=async(_,_)=>{var c=new Dictionary<string,bool>();try{using var chat=new ChatForm(true){TopMost=true,StartPosition=FormStartPosition.Manual,Location=new(80,60)};chat.Show();await chat.VerifyResizePainting(c,Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,model_requests=0,microphone_reads=0},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}finally{host.Close();}};Application.Run(host);}
}
sealed partial class ChatForm
{
    internal async Task VerifyResizePainting(Dictionary<string,bool> c,string folder)
    {
        c["parent_invalidates_old_and_new_resize_area"]=GetStyle(ControlStyles.ResizeRedraw)&&GetStyle(ControlStyles.OptimizedDoubleBuffer);
        turns.Add(new(){User="隔离重绘检查",Assistant=string.Concat(Enumerable.Repeat("缩放后文字应完整刷新；native text repaint 0123456789。\n",12)),Status="完成"});ShowView(transcript);Render();
        int invalidated=0;transcript.Invalidated+=(_,_)=>invalidated++;
        bool fit=true;foreach(var size in new[]{new Size(510,390),new(900,700),new(490,360),new(750,570),new(990,740),new(520,490)}){ClientSize=size;QueueChatRepaint();QueueChatRepaint();await Task.Delay(35);fit&=transcript.Right<=bodyHost.ClientSize.Width&&transcript.Bottom<=bodyHost.ClientSize.Height&&!transcript.HorizontalScroll.Visible;}
        c["resize_repaints_transcript_descendants"]=invalidated>=6;c["resize_bounds_and_scroll_stay_valid"]=fit;
        c["queued_repaint_drains"]=!chatRepaintPending;
        int previous=invalidated;await Task.Delay(100);c["idle_has_no_repaint_loop"]=invalidated==previous;
        WindowState=FormWindowState.Maximized;await Task.Delay(40);WindowState=FormWindowState.Normal;await Task.Delay(40);c["maximize_restore_drains_repaint"]=!chatRepaintPending;
        ClearResults();ResultText(string.Concat(Enumerable.Repeat("本地反馈在缩放后重新排版。",35)));ClientSize=new(510,480);await Task.Delay(40);c["local_result_view_repaints"]=results.Visible&&results.Controls.OfType<Label>().Single().Height>100;
        ShowView(transcript);ClientSize=new(720,600);QueueChatRepaint();await Task.Delay(60);using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new(Point.Empty,Size));image.Save(Path.Combine(folder,"chat-resize.png"));}
        Hide();Show();await Task.Delay(40);c["reshow_repaints_without_stale_task"]=Visible&&!chatRepaintPending;
        QueueChatRepaint();Close();await Task.Delay(25);c["close_discards_queued_repaint"]=chatReleased&&IsDisposed;
    }
}
