using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace LeoLauncher;
// Local resources only. This service never sends UI strings or user content to a model.
static class UiLanguage
{
    static readonly Dictionary<string,string> english=Load();
    static readonly Dictionary<string,string> reverse=english.GroupBy(p=>p.Value).ToDictionary(g=>g.Key,g=>g.First().Key);
    static string PatternFor(string key)=>"^"+string.Concat(Regex.Split(key,@"(\{\d+\})").Select(part=>Regex.IsMatch(part,@"^\{\d+\}$")?"(?<v"+part[1..^1]+">[0-9][0-9,.:+\\-]*)":Regex.Escape(part)))+"$";
    static readonly (Regex Pattern,string Text)[] templates=english.Where(p=>p.Key.Contains("{0}")).Select(p=>(new Regex(PatternFor(p.Key),RegexOptions.CultureInvariant|RegexOptions.NonBacktracking),p.Value)).ToArray();
    static readonly string[] dataPrefixes=["搜索口令：","执行指令：","录音设备：","点播：","已切换：","已添加 ","已启动 "];
    static readonly string[] prefixes=english.Keys.Where(k=>k.Length>2&&!k.Contains('\n')&&(k.EndsWith("：")||k.EndsWith(" · ")||k.EndsWith(' '))).OrderByDescending(k=>k.Length).ToArray();
    static readonly ConditionalWeakTable<Control,TextBinding> bindings=new();
    static readonly ConditionalWeakTable<ToolStripItem,ItemBinding> menuBindings=new();
    static readonly ConditionalWeakTable<Control,object> excluded=new();
    static readonly ConditionalWeakTable<ToolStripItem,object> excludedItems=new();
    static readonly ConditionalWeakTable<Control,Action> refreshers=new();
    public static void RefreshOnChange(Control c,Action refresh){refreshers.Remove(c);refreshers.Add(c,refresh);}
    static bool started;
    public static string Current{get;private set;}="zh-CN";
    public static bool English=>Current=="en";
    public static CultureInfo Culture=>CultureInfo.GetCultureInfo(English?"en-US":"zh-CN");
    public static string Validate(string? id)=>id is null or "" or "zh-CN"?"zh-CN":id=="en"?"en":throw new ArgumentException("不支持的界面语言");
    static Dictionary<string,string> Load(){using var stream=typeof(UiLanguage).Assembly.GetManifestResourceStream("LeoLauncher.Localization.en.json")??throw new InvalidOperationException("Missing English UI resources");return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;}
    public static void Start(){if(started)return;started=true;Application.Idle+=AttachNewForms;UiTheme.Changed+=_=>{foreach(Form form in Application.OpenForms.Cast<Form>().ToArray())if(!form.IsDisposed)Attach(form);};}
    static void AttachNewForms(object? sender,EventArgs e){foreach(Form form in Application.OpenForms.Cast<Form>().ToArray())if(!bindings.TryGetValue(form,out _))Attach(form);}
    public static void Configure(string? id)
    {
        string next=Validate(id);if(Current==next)return;Current=next;UiToolTip.RefreshAll();
        foreach(Form form in Application.OpenForms.Cast<Form>().ToArray()){
            if(form.IsDisposed)continue;
            if(form.InvokeRequired){try{form.BeginInvoke((Action)(()=>FeatureFaults.Run("界面语言",()=>Refresh(form))));}catch(InvalidOperationException){}}else FeatureFaults.Run("界面语言",()=>Refresh(form));
        }
    }
    public static void Preserve(Control c){excluded.GetValue(c,_=>new());if(bindings.TryGetValue(c,out var binding)&&c is not TextBoxBase)c.Text=binding.RawSource;}
    public static void Preserve(ToolStripItem item){excludedItems.GetValue(item,_=>new());if(menuBindings.TryGetValue(item,out var binding))item.Text=binding.RawSource;}
    public static bool IsPreserved(Control c)=>excluded.TryGetValue(c,out _)||c.Tag as string=="user-content";
    public static bool Managed(Control c)=>bindings.TryGetValue(c,out _);
    public static bool Managed(ToolStripItem item)=>menuBindings.TryGetValue(item,out _);
    public static bool IsPreserved(ToolStripItem item)=>excludedItems.TryGetValue(item,out _)||item.Tag as string=="user-content";
    static string Canonical(string source){if(source==UiTheme.Name&&!english.ContainsKey(source)&&!reverse.ContainsKey(source))return "Leo";if(reverse.TryGetValue(source,out var original))return original;string normalized=source.Replace(UiTheme.Name,"Leo",StringComparison.Ordinal);return english.ContainsKey(normalized)||normalized.StartsWith("Leo · ")||normalized.StartsWith("Leo  /  ")?normalized:source;}
    public static string Source(Control c)=>bindings.TryGetValue(c,out var state)?state.Source:c.Text;
    public static string Source(ToolStripItem c)=>menuBindings.TryGetValue(c,out var state)?state.Source:c.Text??"";
    public static string T(string? text)=>Translate(text??"",0);
    static string Translate(string text,int depth)
    {
        if(!English||text.Length==0||depth>12)return text;
        if(english.TryGetValue(text,out var value))return value;
        if(UiTheme.Name!="Leo"&&text.Contains(UiTheme.Name,StringComparison.Ordinal)&&english.TryGetValue(text.Replace(UiTheme.Name,"Leo"),out var branded))return branded.Replace("Leo",UiTheme.Name);
        foreach(string key in dataPrefixes)if(text.StartsWith(key,StringComparison.Ordinal)&&english.TryGetValue(key,out var heading))return heading+text[key.Length..];
        if(text.Trim()!=text&&text.Trim().Length>0){int start=text.Length-text.TrimStart().Length,end=text.TrimEnd().Length;return text[..start]+Translate(text[start..end],depth+1)+text[end..];}
        foreach(var (pattern,format) in templates){var match=pattern.Match(text);if(match.Success)return Regex.Replace(format,@"\{(\d+)\}",m=>match.Groups["v"+m.Groups[1].Value].Value);}
        foreach(var key in prefixes)if(text.StartsWith(key,StringComparison.Ordinal))return english[key]+Translate(text[key.Length..],depth+1);
        foreach(var marker in new[]{"✓ ","○ ","● ","★ ","→ "})if(text.StartsWith(marker,StringComparison.Ordinal))return marker+Translate(text[marker.Length..],depth+1);
        // Brand names and data values remain unchanged; only known UI segments are localized.
        string prefix=UiTheme.Name+" · ";if(text.StartsWith(prefix,StringComparison.Ordinal))return prefix+Translate(text[prefix.Length..],depth+1);
        if(text.StartsWith("Leo · ",StringComparison.Ordinal))return UiTheme.Name+" · "+Translate(text[6..],depth+1);
        if(text.StartsWith("LEO  /  ",StringComparison.Ordinal))return UiTheme.Name+"  /  "+Translate(text[8..],depth+1);
        if(text.StartsWith(UiTheme.Name+"  /  ",StringComparison.Ordinal))return UiTheme.Name+"  /  "+Translate(text[(UiTheme.Name.Length+5)..],depth+1);
        if(text.Contains('\n'))return string.Join("\n",text.Split('\n').Select(piece=>Translate(piece,depth+1)));
        foreach(string separator in new[]{"：", " · "}){int at=text.IndexOf(separator,StringComparison.Ordinal);if(at>0){string head=text[..at];if(english.TryGetValue(head,out var translated))return translated+(separator=="："?": ":separator)+Translate(text[(at+separator.Length)..],depth+1);}}
        if(text.Contains(" · ",StringComparison.Ordinal))return string.Join(" · ",text.Split(" · ").Select(piece=>Translate(piece,depth+1)));
        return text;
    }
    public static void DrawText(IDeviceContext dc,string? text,Font? font,Rectangle bounds,Color color,TextFormatFlags flags=TextFormatFlags.Default)=>TextRenderer.DrawText(dc,T(text),font,bounds,color,flags);
    public static void Attach(Control c)
    {
        if(c.IsDisposed)return;
        var state=bindings.GetValue(c,control=>new(control));state.Apply();
        if(c is DateTimePicker date&&date.Format==DateTimePickerFormat.Custom){var format=dateFormats.GetValue(date,d=>new(d.CustomFormat??""));date.CustomFormat=English?format.Original.Replace("yyyy年MM月dd日","yyyy-MM-dd"):format.Original;}
        if(c.ContextMenuStrip is{} menu)Attach(menu);
        if(c is ToolStrip strip){foreach(ToolStripItem item in strip.Items)Attach(item);}
        foreach(Control child in c.Controls)Attach(child);
    }
    static void Attach(ToolStripItem item)
    {
        if(item.IsDisposed)return;menuBindings.GetValue(item,i=>new(i)).Apply();
        if(item is ToolStripMenuItem m){foreach(ToolStripItem child in m.DropDownItems)Attach(child);}
    }
    static void Refresh(Control c){if(c.IsDisposed)return;UpdateLocalViews(c);Attach(c);c.PerformLayout();c.Invalidate(true);}
    static void UpdateLocalViews(Control c){if(c.IsDisposed)return;if(refreshers.TryGetValue(c,out var refresh))refresh();foreach(Control child in c.Controls.Cast<Control>().ToArray())UpdateLocalViews(child);}
    sealed class TextBinding
    {
        readonly Control control;bool writing;string rendered="";public string Source{get;private set;}public string RawSource{get;private set;}
        public TextBinding(Control c){control=c;RawSource=c is TextBoxBase?"":c.Text;Source=c.Tag as string=="pet-brand"?"Leo":Canonical(RawSource);if(c is not TextBoxBase)c.TextChanged+=(_,_)=>{if(writing)return;RawSource=c.Text;Source=c.Tag as string=="pet-brand"?"Leo":Canonical(RawSource);Apply();};c.ControlAdded+=(_,e)=>{if(e.Control!=null)Attach(e.Control);};if(c is ToolStrip strip)strip.ItemAdded+=(_,e)=>{if(e.Item!=null)Attach(e.Item);};if(c is ToolStripDropDown dropdown)dropdown.Opening+=(_,_)=>{foreach(ToolStripItem item in dropdown.Items)Attach(item);};}
        public void Apply()
        {
            if(writing||control.IsDisposed)return;
            if(control is TextBox box){string hint=box.PlaceholderText;var hints=placeholder.GetValue(box,b=>new(Canonical(b.PlaceholderText)));if(hint!=hints.Rendered){hints.Original=Canonical(hint);}hints.Rendered=T(hints.Original);if(hint!=hints.Rendered)box.PlaceholderText=hints.Rendered;return;}
            if(excluded.TryGetValue(control,out _)||control.Tag as string=="user-content"||control is TextBoxBase||control is not(Form or Label or ButtonBase or GroupBox or TabPage))return;
            if(control is Label label)label.UseMnemonic=false;if(control is ButtonBase button)button.UseMnemonic=false;rendered=UiTheme.Rename(T(Source));writing=true;try{if(control.Text!=rendered)control.Text=rendered;}finally{writing=false;}
        }
    }
    sealed class Hint(string original){public string Original=original,Rendered=original;}
    static readonly ConditionalWeakTable<TextBox,Hint> placeholder=new();
    static readonly ConditionalWeakTable<DateTimePicker,Hint> dateFormats=new();
    sealed class ItemBinding
    {
        readonly ToolStripItem item;bool writing;public string Source{get;private set;}public string RawSource{get;private set;}
        public ItemBinding(ToolStripItem i){item=i;RawSource=i.Text??"";Source=Canonical(RawSource);i.TextChanged+=(_,_)=>{if(writing)return;RawSource=i.Text??"";Source=Canonical(RawSource);Apply();};}
        public void Apply(){if(writing||item.IsDisposed||(item.Tag as string=="user-content"||excludedItems.TryGetValue(item,out _)))return;writing=true;try{string text=UiTheme.Rename(T(Source));if(item.Text!=text)item.Text=text;item.Invalidate();}finally{writing=false;}}
    }
}
