namespace LeoLauncher;
static class SelectionKeys
{
    public static void Wire(ListBox list,Action accept){list.KeyDown+=(_,e)=>{if(e.KeyData==Keys.Enter&&list.SelectedIndex>=0){e.SuppressKeyPress=true;FeatureFaults.Run("选择操作",accept);}};list.DoubleClick+=(_,_)=>{if(list.SelectedIndex>=0)FeatureFaults.Run("选择操作",accept);};}
}
interface IWindowBack {bool TryGoBack();}
static class WindowInteraction
{
    public static bool IsCompanion(Form form)=>form is PetForm or HitSurface or FootTools or LauncherForm or LeoToast or HoverCard;
    public static void Prepare(Form form){if(!form.TopLevel||IsCompanion(form)||form is ReminderForm)return;form.TopMost=false;var pet=WindowPositions.PetBounds?.Invoke()??Rectangle.Empty;var area=Screen.FromRectangle(form.Owner?.Bounds??(pet.IsEmpty?form.Bounds:pet)).WorkingArea;
        if(form.Modal&&form.Owner!=null){form.StartPosition=FormStartPosition.Manual;form.Location=PlaceDialog(form.Size,form.Owner.Bounds,pet,area);}
        else if(form.StartPosition!=FormStartPosition.Manual){WindowPositions.Prepare(form,form.Name.Length>0?form.Name:"window:"+form.GetType().Name,pet,false);}
        if(form.Owner==null&&Screen.AllScreens.Any(s=>s.WorkingArea.IntersectsWith(form.Bounds)))area=Screen.FromRectangle(form.Bounds).WorkingArea;form.Location=KeepVisible(form.Bounds,area).Location;}
    internal static Rectangle KeepVisible(Rectangle bounds,Rectangle area)=>new(Math.Clamp(bounds.X,area.Left,Math.Max(area.Left,area.Right-bounds.Width)),Math.Clamp(bounds.Y,area.Top,Math.Max(area.Top,area.Bottom-bounds.Height)),bounds.Width,bounds.Height);
    internal static Point PlaceDialog(Size size,Rectangle owner,Rectangle pet,Rectangle area){var center=KeepVisible(new(owner.Left+(owner.Width-size.Width)/2,owner.Top+(owner.Height-size.Height)/2,size.Width,size.Height),area);return !pet.IsEmpty&&center.IntersectsWith(pet)?PopupPlacement.Place(pet,size,area):center.Location;}
    public static void Show(Form form){if(form.WindowState==FormWindowState.Minimized)form.WindowState=FormWindowState.Normal;form.Show();form.Activate();}
    public static void FocusInput(Form form){if(form.IsDisposed||!form.Visible)return;if(form.ActiveControl is TextBoxBase or ComboBox or ListBox)return;var input=Descendants(form).FirstOrDefault(c=>c.Visible&&c.Enabled&&c.TabStop&&(c is TextBoxBase {ReadOnly:false}||c is ComboBox));if(input!=null)input.Focus();else if(Descendants(form).FirstOrDefault(c=>c is ListBox&&c.Visible&&c.Enabled) is {} list)list.Focus();else if(form.ActiveControl==null)form.SelectNextControl(null,true,true,true,true);}
    static IEnumerable<Control> Descendants(Control root){foreach(Control c in root.Controls){yield return c;foreach(var child in Descendants(c))yield return child;}}
    internal static bool Back(Form form)=>form is IWindowBack back&&back.TryGoBack();
}
