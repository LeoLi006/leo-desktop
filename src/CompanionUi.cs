namespace LeoLauncher;
static class CompanionUi
{
    public static void ThemeCombo(ComboBox combo)
    {
        combo.BackColor=BarcaUi.Raised;combo.ForeColor=BarcaUi.Text;combo.FlatStyle=FlatStyle.Flat;combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=24;
        combo.DrawItem+=(_,e)=>{using var fill=new SolidBrush((e.State&DrawItemState.Selected)!=0?Appearance.Blue:BarcaUi.Raised);e.Graphics.FillRectangle(fill,e.Bounds);if(e.Index>=0)TextRenderer.DrawText(e.Graphics,combo.Items[e.Index]!.ToString(),combo.Font,e.Bounds,BarcaUi.Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);};
    }
}
