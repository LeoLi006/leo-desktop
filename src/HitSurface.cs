using System.Runtime.InteropServices;
namespace LeoLauncher;

sealed class HitSurface : Form
{
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
    public HitSurface()
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; BackColor = Color.Black; Opacity = 0.01;
        Cursor = Cursors.Hand;
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; } }
    public bool ReceivesPoint(Point local) => WindowFromPoint(PointToScreen(local)) == Handle;
    public void SimulateClick(Point local, Action? betweenPressAndRelease = null)
    {
        OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, local.X, local.Y, 0));
        betweenPressAndRelease?.Invoke();
        OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, local.X, local.Y, 0));
    }
}
