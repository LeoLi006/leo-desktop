namespace LeoLauncher;
static class LeoBrand
{
    public static readonly Icon Icon=Load();
    static Icon Load(){string file=Path.Combine(Store.Root,"leo.ico");if(File.Exists(file))return new Icon(file,32,32);return System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)??SystemIcons.Application;}
}
