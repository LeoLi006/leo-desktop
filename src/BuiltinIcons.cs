namespace LeoLauncher;
// Stable references: no personal path, theme-aware when rendered, no image dependency.
static class BuiltinIcons
{
    public const string Prefix="glyph:";
    public static readonly (string Id,string Name)[] Entries=[("monitor","电脑状态"),("mail","邮件"),("folder","文件夹"),("globe","浏览器"),("code","开发工具"),("shield","安全"),("image","图片"),("game","游戏"),("clock","时钟"),("star","收藏"),("notes","便签"),("media","视频"),("music","音乐"),("chat","聊天"),("todo","清单"),("football","足球"),("search","搜索"),("terminal","终端")];
    public static Bitmap? TryCreate(string? reference)
    {
        if(reference==null||!reference.StartsWith(Prefix,StringComparison.Ordinal))return null;
        string id=reference[Prefix.Length..];if(!Entries.Any(e=>e.Id==id))return null;
        using var control=new GlyphButton(id){Size=new(48,48)};var image=new Bitmap(48,48);control.DrawToBitmap(image,new(Point.Empty,image.Size));return image;
    }
}
sealed class BuiltinIconPicker:BarcaWindow
{
    public string Selected{get;private set;}="";
    readonly ModuleThemeBinding theme;
    public BuiltinIconPicker()
    {
        Text="选择快捷图标";ClientSize=new(584,434);StartPosition=FormStartPosition.CenterParent;
        Controls.Add(BarcaUi.Chrome(this,"选择快捷图标","统一线条图标 · 可用于自定义应用，也可选择自己的图片"));
        var grid=new FlowLayoutPanel{Bounds=new(20,104,548,312),WrapContents=true};
        foreach(var entry in BuiltinIcons.Entries){var cell=new Panel{Size=new(86,94),Margin=new Padding(2)};var button=new GlyphButton(entry.Id){Bounds=new(19,4,48,48),AccessibleName=entry.Name};var caption=new Label{Text=entry.Name,TextAlign=ContentAlignment.MiddleCenter,Bounds=new(0,56,86,26),ForeColor=BarcaUi.Text,Font=BarcaUi.Font(9)};button.Click+=(_,_)=>{Selected=BuiltinIcons.Prefix+entry.Id;DialogResult=DialogResult.OK;};cell.Controls.AddRange([button,caption]);grid.Controls.Add(cell);}Controls.Add(grid);theme=new(this);MinimumSize=MaximumSize=Size;
    }
    protected override void Dispose(bool disposing){if(disposing){theme.Dispose();}base.Dispose(disposing);}
}
