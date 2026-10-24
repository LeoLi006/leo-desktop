using System.Drawing.Drawing2D;
namespace LeoLauncher;

// A small, keyboard-accessible toggle without the native ComboBox border/arrow.
sealed class SearchModeButton:Button
{
    readonly bool files;bool active,hovered;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Active{get=>active;set{active=value;Invalidate();}}
    public SearchModeButton(bool files)
    {
        this.files=files;Text="";Cursor=Cursors.Hand;TabStop=true;
        AccessibleRole=AccessibleRole.PushButton;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
    }
    protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.Clear(Parent?.BackColor??Appearance.Field);g.SmoothingMode=SmoothingMode.AntiAlias;
        if(active||hovered||Focused){using var shape=Appearance.Path(new Rectangle(1,1,Width-2,Height-2),7);using var fill=new SolidBrush(active?Appearance.Blue:Appearance.Hover);g.FillPath(fill,shape);}
        using var pen=new Pen(active?Appearance.Accent:Appearance.Muted,1.5f){LineJoin=LineJoin.Round};
        g.TranslateTransform((Width-22)/2f,(Height-20)/2f);
        if(files)g.DrawLines(pen,[new(1,6),new(1,2),new(8,2),new(11,6),new(21,6),new(19,18),new(2,18),new(1,6),new(21,6)]);
        else foreach(int x in new[]{3,12})foreach(int y in new[]{3,12})g.DrawRoundedRectangle(pen,new RectangleF(x,y,6,6),new SizeF(1.5f,1.5f));
        g.ResetTransform();if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Width-6,Height-6),Appearance.Accent,Appearance.Field);
    }
}
