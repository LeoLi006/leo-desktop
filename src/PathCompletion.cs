namespace LeoLauncher;
sealed record PathChoice(string Path,bool Directory)
{
    public override string ToString()=>(Directory?"▸  ":"    ")+System.IO.Path.GetFileName(Path.TrimEnd('\\','/'))+(Directory?"  /":"");
}
static class PathCompletion
{
    public static PathChoice[] Find(string input,CancellationToken token=default)
    {
        string expanded=LocalPath.Expand(input);if(!LocalPath.LooksLike(expanded))return [];string folder,prefix;
        try{if(Directory.Exists(expanded)){folder=expanded;prefix="";}else{folder=System.IO.Path.GetDirectoryName(expanded)??"";prefix=System.IO.Path.GetFileName(expanded);}if(!Directory.Exists(folder))return [];
            int scanned=0;var found=new List<PathChoice>();foreach(var item in Directory.EnumerateFileSystemEntries(folder)){token.ThrowIfCancellationRequested();if(++scanned>20000)break;string name=System.IO.Path.GetFileName(item);if(name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))found.Add(new(item,Directory.Exists(item)));if(found.Count>=200)break;}return found.OrderByDescending(p=>p.Directory).ThenBy(p=>p.Path,StringComparer.OrdinalIgnoreCase).Take(80).ToArray();
        }catch(IOException){return [];}catch(UnauthorizedAccessException){return [];}catch(ArgumentException){return [];}
    }
    public static string Fill(PathChoice choice)=>choice.Path+(choice.Directory&&!choice.Path.EndsWith(System.IO.Path.DirectorySeparatorChar)?System.IO.Path.DirectorySeparatorChar:"");
}
sealed partial class LauncherForm
{
    static readonly SemaphoreSlim pathReads=new(2);
    readonly ListBox pathChoices=new(){Visible=false,BorderStyle=BorderStyle.None,IntegralHeight=false,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=32};CancellationTokenSource? pathLookup;bool pathMode;
    void InitializePathCompletion()
    {
        pathChoices.Bounds=new Rectangle(22,78,556,162);pathChoices.BackColor=Appearance.Background;pathChoices.ForeColor=Appearance.Ink;Controls.Add(pathChoices);pathChoices.BringToFront();
        pathChoices.DrawItem+=(_,e)=>{if(e.Index<0)return;using var brush=new SolidBrush((e.State&DrawItemState.Selected)!=0?Appearance.Blue:Appearance.Background);e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,pathChoices.Items[e.Index].ToString(),Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-16,e.Bounds.Height),Appearance.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};
        pathChoices.DoubleClick+=async(_,_)=>await OpenChosenPath();pathChoices.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await OpenChosenPath();}else if(e.KeyCode==Keys.Tab){e.SuppressKeyPress=true;FillChosenPath();}};
        Disposed+=(_,_)=>{var lookup=pathLookup;pathLookup=null;lookup?.Cancel();lookup?.Dispose();};
    }
    async void UpdatePathCompletion()
    {
        pathLookup?.Cancel();pathLookup?.Dispose();pathLookup=new();var life=pathLookup;var token=life.Token;string text=query.Text;pathMode=LocalPath.LooksLike(text);pathChoices.Visible=pathMode;recent.Visible=!pathMode;if(!pathMode)return;pathChoices.Items.Clear();status.Text="";status.Visible=false;
        try{await pathReads.WaitAsync(token);PathChoice[] matches;try{matches=await Task.Run(()=>PathCompletion.Find(text,token),token);}finally{pathReads.Release();}if(IsDisposed||token.IsCancellationRequested||query.Text!=text)return;pathChoices.BeginUpdate();pathChoices.Items.AddRange(matches);pathChoices.SelectedIndex=-1;pathChoices.EndUpdate();if(matches.Length==0){status.Text=File.Exists(LocalPath.Expand(text))||Directory.Exists(LocalPath.Expand(text))?"按回车打开当前路径":"没有匹配的文件或文件夹";status.Visible=true;ClientSize=new(600,280);}}
        catch(OperationCanceledException){}catch(Exception e){if(!IsDisposed&&query.Text==text){status.Text="读取目录失败："+e.Message;status.Visible=true;ClientSize=new(600,280);}}
    }
    bool PathKey(KeyEventArgs e)
    {
        if(!LocalPath.LooksLike(query.Text))return false;
        if(e.KeyCode is Keys.Down or Keys.Up){e.SuppressKeyPress=true;int count=pathChoices.Items.Count;if(count>0)pathChoices.SelectedIndex=Math.Clamp(pathChoices.SelectedIndex+(e.KeyCode==Keys.Down?1:-1),0,count-1);return true;}
        if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;_=OpenChosenPath();return true;}return false;
    }
    void FillChosenPath(){if(pathChoices.Items.Count==0)return;var choice=(PathChoice)pathChoices.Items[Math.Max(0,pathChoices.SelectedIndex)];query.Text=PathCompletion.Fill(choice);query.SelectionStart=query.TextLength;query.Focus();debounce.Stop();Filter();}
    async Task OpenChosenPath(){if(pathChoices.SelectedItem is PathChoice choice)query.Text=choice.Path;if(CommandRequested!=null)await RunCommand();else try{if(!preview)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LocalPath.Resolve(query.Text)){UseShellExecute=true});Hide();}catch(Exception e){status.Text=e.Message;status.Visible=true;ClientSize=new(600,280);}}
}
