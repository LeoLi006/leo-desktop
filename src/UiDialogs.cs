namespace LeoLauncher;
static class UiDialogs
{
    public static DialogResult Show(string text,string caption)=>MessageBox.Show(UiLanguage.T(text),UiLanguage.T(caption));
    public static DialogResult Show(IWin32Window? owner,string text,string caption)=>MessageBox.Show(owner,UiLanguage.T(text),UiLanguage.T(caption));
    public static DialogResult Show(IWin32Window? owner,string text,string caption,MessageBoxButtons buttons)=>MessageBox.Show(owner,UiLanguage.T(text),UiLanguage.T(caption),buttons);
    public static DialogResult Show(IWin32Window? owner,string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon)=>MessageBox.Show(owner,UiLanguage.T(text),UiLanguage.T(caption),buttons,icon);
}
