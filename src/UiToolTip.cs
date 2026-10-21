namespace LeoLauncher;
sealed class UiToolTip:ToolTip
{
    static readonly List<WeakReference<UiToolTip>> live=new();
    readonly Dictionary<Control,string> originals=new();
    public UiToolTip(){live.Add(new(this));}
    public new void SetToolTip(Control control,string? caption){if(!originals.ContainsKey(control))control.Disposed+=(_,_)=>originals.Remove(control);originals[control]=caption??"";base.SetToolTip(control,UiLanguage.IsPreserved(control)?caption:UiLanguage.T(caption));}
    public new void RemoveAll(){originals.Clear();base.RemoveAll();}
    internal static void RefreshAll(){for(int i=live.Count-1;i>=0;i--){if(!live[i].TryGetTarget(out var tip)){live.RemoveAt(i);continue;}foreach(var pair in tip.originals.ToArray())if(!pair.Key.IsDisposed)tip.baseSet(pair.Key,UiLanguage.IsPreserved(pair.Key)?pair.Value:UiLanguage.T(pair.Value));}}
    void baseSet(Control c,string value)=>base.SetToolTip(c,value);
    protected override void Dispose(bool disposing){if(disposing){originals.Clear();live.RemoveAll(w=>!w.TryGetTarget(out var target)||ReferenceEquals(target,this));}base.Dispose(disposing);}
}
