namespace LeoLauncher;
static class CompanionUi
{
    public static void ThemeCombo(ComboBox combo)
    {
        combo.BackColor=BarcaUi.Raised;combo.ForeColor=BarcaUi.Text;combo.FlatStyle=FlatStyle.Flat;combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=24;
        combo.DrawItem+=(_,e)=>{using var fill=new SolidBrush((e.State&DrawItemState.Selected)!=0?Appearance.Blue:BarcaUi.Raised);e.Graphics.FillRectangle(fill,e.Bounds);if(e.Index>=0){var value=combo.Items[e.Index];string text=value is ComboChoice choice?choice.ToString():value is string literal&&combo.DropDownStyle==ComboBoxStyle.DropDownList?UiLanguage.T(literal):value?.ToString()??"";TextRenderer.DrawText(e.Graphics,text,combo.Font,e.Bounds,BarcaUi.Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);}};
    }
}
