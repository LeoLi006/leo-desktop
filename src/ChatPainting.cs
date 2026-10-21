using System.Runtime.InteropServices;
namespace LeoLauncher;
sealed partial class ChatForm
{
    bool chatPaintReady,chatRepaintPending;
    // Native RichEdit children need their own repaint after a resize, in addition to
    // the managed parent's buffered background. Coalesce requests on the UI queue.
    [DllImport("user32.dll")]static extern bool RedrawWindow(IntPtr window,IntPtr rectangle,IntPtr region,uint flags);
    protected override void OnClientSizeChanged(EventArgs e){base.OnClientSizeChanged(e);if(chatPaintReady)QueueChatRepaint();}
    protected override void OnResizeEnd(EventArgs e){base.OnResizeEnd(e);QueueChatRepaint();}
    void QueueChatRepaint()
    {
        if(chatRepaintPending||chatReleased||IsDisposed||Disposing||!IsHandleCreated)return;
        chatRepaintPending=true;
        try{BeginInvoke((Action)(()=>{chatRepaintPending=false;if(chatReleased||IsDisposed||Disposing||!IsHandleCreated)return;RepaintChat();}));}
        catch(InvalidOperationException){chatRepaintPending=false;}
    }
    void RepaintChat()
    {
        PerformLayout();bodyHost.PerformLayout();ResizeResults();
        if(transcript.Visible)transcript.Display(turns);
        Invalidate(true);
        // RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN. Paint normally through the
        // message loop; never draw over another child's native paint with a DC.
        RedrawWindow(Handle,IntPtr.Zero,IntPtr.Zero,0x0001|0x0004|0x0080);
    }
}
