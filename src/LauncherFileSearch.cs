using System.Diagnostics;
namespace LeoLauncher;

sealed record FileSearchResult(PathChoice[] Items, bool Limited);
static class LauncherFileSearch
{
    // Search only an explicit directory; never follow junctions into other trees.
    public static FileSearchResult Find(string root,string text,CancellationToken token)
    {
        var found=new List<PathChoice>();var pending=new Queue<string>();pending.Enqueue(root);
        int scanned=0;var clock=Stopwatch.StartNew();
        while(pending.Count>0){
            token.ThrowIfCancellationRequested();var dir=pending.Dequeue();
            try{foreach(var item in Directory.EnumerateFileSystemEntries(dir)){
                token.ThrowIfCancellationRequested();
                if(++scanned>30000||clock.Elapsed>TimeSpan.FromSeconds(3))return new(found.ToArray(),true);
                var attributes=File.GetAttributes(item);bool directory=(attributes&FileAttributes.Directory)!=0;
                if((attributes&(FileAttributes.ReparsePoint|FileAttributes.Hidden|FileAttributes.System))!=0)continue;
                if(directory)pending.Enqueue(item);
                if(Path.GetFileName(item).Contains(text,StringComparison.OrdinalIgnoreCase))found.Add(new(item,directory));
                if(found.Count>=80)return new(found.ToArray(),true);
            }}catch(IOException){}catch(UnauthorizedAccessException){}
        }
        return new(found.OrderByDescending(p=>p.Directory).ThenBy(p=>p.Path,StringComparer.OrdinalIgnoreCase).ToArray(),false);
    }
    public static ProcessStartInfo CodeStart(string executable,string target)
    {
        if(!Path.GetFileName(executable).Equals("Code.exe",StringComparison.OrdinalIgnoreCase)||!File.Exists(executable))throw new IOException("请选择 VS Code 的 Code.exe");
        var start=new ProcessStartInfo(executable){UseShellExecute=true};
        start.ArgumentList.Add("--new-window");start.ArgumentList.Add(LocalPath.Resolve(target));return start;
    }
}

sealed partial class LauncherForm
{
    static readonly SemaphoreSlim pathReads=new(2);
    static readonly Size SearchWindowSize=new(600,280);
    bool fileSearchMode;
    readonly SearchModeButton appSearchButton=new(false),fileSearchButton=new(true);
    readonly UiToolTip searchModeTips=new();
    readonly ListBox pathChoices=new(){Visible=false,BorderStyle=BorderStyle.None,IntegralHeight=false,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=40};
    readonly Panel pathActions=new(){Bounds=new(18,76,564,32),Visible=false};
    readonly Button searchScope=new BarcaButton{Bounds=new(0,0,240,28),AutoEllipsis=true};
    CancellationTokenSource? pathLookup;bool pathMode;
    string fileScope=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),codeExecutable="";
    void InitializePathCompletion()
    {
        DoubleBuffered=true;
        appSearchButton.Bounds=new(4,3,30,26);fileSearchButton.Bounds=new(38,3,30,26);
        appSearchButton.AccessibleName=UiLanguage.T("搜索应用");fileSearchButton.AccessibleName=UiLanguage.T("搜索文件和文件夹");
        appSearchButton.Active=true;
        query.Parent!.Controls.AddRange([appSearchButton,fileSearchButton]);
        searchModeTips.SetToolTip(appSearchButton,UiLanguage.T("搜索应用")+" (Ctrl+1)");
        searchModeTips.SetToolTip(fileSearchButton,UiLanguage.T("搜索文件和文件夹")+" (Ctrl+2)");
        appSearchButton.Click+=(_,_)=>SetFileSearchMode(false);fileSearchButton.Click+=(_,_)=>SetFileSearchMode(true);
        searchScope.Text=UiLanguage.T("搜索目录：")+fileScope;
        searchScope.Click+=(_,_)=>{
            menuOpen=true;try{using var dialog=new FolderBrowserDialog{SelectedPath=fileScope};if(dialog.ShowDialog(this)==DialogResult.OK){fileScope=dialog.SelectedPath;searchScope.Text=UiLanguage.T("搜索目录：")+fileScope;Filter();}}
            finally{menuOpen=false;Activate();query.Focus();}
        };
        var open=new BarcaButton{Text=UiLanguage.T("打开"),Bounds=new(248,0,75,28)};open.Click+=async(_,_)=>await OpenChosenPath();
        var code=new BarcaButton{Text=UiLanguage.T("VS Code 新窗口"),Bounds=new(330,0,230,28)};code.Click+=async(_,_)=>await OpenChosenPath(true);
        pathActions.Controls.AddRange([searchScope,open,code]);Controls.Add(pathActions);
        pathChoices.Bounds=new(22,112,556,120);pathChoices.BackColor=Appearance.Background;pathChoices.ForeColor=Appearance.Ink;Controls.Add(pathChoices);pathChoices.BringToFront();
        pathChoices.DrawItem+=DrawPathResult;
        pathChoices.DoubleClick+=async(_,_)=>await OpenChosenPath();pathChoices.KeyDown+=(_,e)=>PathKey(e);
        VisibleChanged+=(_,_)=>{if(!Visible){debounce.Stop();pathLookup?.Cancel();}};
        Disposed+=(_,_)=>{pathLookup?.Cancel();pathLookup?.Dispose();pathLookup=null;searchModeTips.Dispose();};
    }
    void SetFileSearchMode(bool value)
    {
        if(fileSearchMode!=value){
            fileSearchMode=value;appSearchButton.Active=!value;fileSearchButton.Active=value;
            query.PlaceholderText=UiLanguage.T(value?"搜索名称或完整路径":"搜索应用、文件路径或指令");
            debounce.Stop();Filter();
        }
        query.Focus();
    }
    void LayoutSearchResults()
    {
        bool files=fileSearchMode||LocalPath.LooksLike(query.Text);
        SuspendLayout();try{
            pathChoices.Visible=pathActions.Visible=files;recent.Visible=!files;
            status.SetBounds(22,248,556,24);
            if(ClientSize!=SearchWindowSize)ClientSize=SearchWindowSize;
        }finally{ResumeLayout(true);}
        Invalidate();
    }
    void DrawPathResult(object? sender,DrawItemEventArgs e)
    {
        if(e.Index<0||e.Index>=pathChoices.Items.Count)return;
        var item=(PathChoice)pathChoices.Items[e.Index];var saved=e.Graphics.Save();
        try{
            e.Graphics.SetClip(e.Bounds);
            using var brush=new SolidBrush((e.State&DrawItemState.Selected)!=0?Appearance.Blue:Appearance.Background);e.Graphics.FillRectangle(brush,e.Bounds);
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen=new Pen(item.Directory?Appearance.Accent:Appearance.Muted,1.5f);
            int x=e.Bounds.X+9,y=e.Bounds.Y+9;
            if(item.Directory)e.Graphics.DrawLines(pen,[new(x,y+5),new(x,y),new(x+8,y),new(x+11,y+4),new(x+22,y+4),new(x+20,y+20),new(x,y+20),new(x,y+5),new(x+22,y+5)]);
            else{e.Graphics.DrawRectangle(pen,x+3,y,16,21);e.Graphics.DrawLine(pen,x+7,y+7,x+15,y+7);e.Graphics.DrawLine(pen,x+7,y+12,x+15,y+12);}
            var flags=TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter|TextFormatFlags.PreserveGraphicsClipping;
            string name=Path.GetFileName(item.Path.TrimEnd('\\','/'));if(name.Length==0)name=item.Path;
            TextRenderer.DrawText(e.Graphics,name,Font,new Rectangle(e.Bounds.X+42,e.Bounds.Y,e.Bounds.Width-50,20),Appearance.Ink,flags);
            TextRenderer.DrawText(e.Graphics,item.Path,Font,new Rectangle(e.Bounds.X+42,e.Bounds.Y+20,e.Bounds.Width-50,20),Appearance.Muted,flags);
        }finally{e.Graphics.Restore(saved);}
    }
    async void UpdatePathCompletion()
    {
        pathLookup?.Cancel();pathLookup?.Dispose();pathLookup=new();var token=pathLookup.Token;string text=query.Text;bool mode=fileSearchMode;
        pathMode=mode||LocalPath.LooksLike(text);LayoutSearchResults();if(!pathMode)return;
        searchScope.Visible=true;pathChoices.Items.Clear();status.Visible=false;
        if(string.IsNullOrWhiteSpace(text)){status.Text=UiLanguage.T("输入名称搜索，或粘贴完整路径");status.Visible=true;LayoutSearchResults();return;}
        status.Text=UiLanguage.T("正在搜索…");status.Visible=true;LayoutSearchResults();
        try{
            await pathReads.WaitAsync(token);FileSearchResult result;
            try{result=await Task.Run(()=>{
                if(LocalPath.LooksLike(text)){
                    string expanded=LocalPath.Expand(text);var entries=PathCompletion.Find(text,token).ToList();
                    if(File.Exists(expanded)||Directory.Exists(expanded))entries.Insert(0,new(expanded,Directory.Exists(expanded)));
                    return new FileSearchResult(entries.DistinctBy(p=>p.Path,StringComparer.OrdinalIgnoreCase).ToArray(),false);
                }
                return LauncherFileSearch.Find(fileScope,text.Trim(),token);
            },token);}finally{pathReads.Release();}
            if(IsDisposed||token.IsCancellationRequested||query.Text!=text||fileSearchMode!=mode)return;
            pathChoices.BeginUpdate();try{pathChoices.Items.AddRange(result.Items);pathChoices.SelectedIndex=result.Items.Length>0?0:-1;}finally{pathChoices.EndUpdate();pathChoices.Invalidate();}
            status.Text=result.Limited?UiLanguage.T("已到搜索上限，可缩小搜索目录或补全路径"):result.Items.Length==0?UiLanguage.T("没有匹配的文件或文件夹"):"";status.Visible=status.Text.Length>0;
            LayoutSearchResults();
        }catch(OperationCanceledException){}catch(Exception e)when(FeatureFaults.Recoverable(e)){if(!IsDisposed&&!token.IsCancellationRequested){status.Text=UiLanguage.T("读取目录失败：")+e.Message;status.Visible=true;LayoutSearchResults();}}
    }
    bool PathKey(KeyEventArgs e)
    {
        if(!fileSearchMode&&!LocalPath.LooksLike(query.Text))return false;
        if(e.KeyCode is Keys.Down or Keys.Up){e.SuppressKeyPress=true;if(pathChoices.Items.Count>0)pathChoices.SelectedIndex=Math.Clamp(pathChoices.SelectedIndex+(e.KeyCode==Keys.Down?1:-1),0,pathChoices.Items.Count-1);return true;}
        if(e.KeyCode==Keys.Escape){e.SuppressKeyPress=true;Hide();return true;}
        if(e.KeyCode==Keys.Tab&&pathChoices.SelectedItem is PathChoice){e.SuppressKeyPress=true;FillChosenPath();return true;}
        if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;_=OpenChosenPath(e.Control);return true;}return false;
    }
    void FillChosenPath(){if(pathChoices.SelectedItem is not PathChoice choice)return;query.Text=PathCompletion.Fill(choice);query.SelectionStart=query.TextLength;query.Focus();debounce.Stop();Filter();}
    async Task OpenChosenPath(bool inCode=false)
    {
        if(launcherReleased||commandRunning)return;
        try{
            string target=pathChoices.SelectedItem is PathChoice choice?choice.Path:LocalPath.Resolve(query.Text);
            target=LocalPath.Resolve(target);
            if(inCode){
                string? executable=new[]{codeExecutable,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Microsoft VS Code","Code.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Microsoft VS Code","Code.exe")}
                    .Concat(FileApplications.Select(a=>a.ExePath)).FirstOrDefault(p=>Path.GetFileName(p).Equals("Code.exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(p));
                if(executable==null){menuOpen=true;try{using var dialog=new OpenFileDialog{Filter="VS Code (Code.exe)|Code.exe",Title=UiLanguage.T("请选择 VS Code 的 Code.exe")};if(dialog.ShowDialog(this)!=DialogResult.OK)return;executable=codeExecutable=dialog.FileName;}finally{menuOpen=false;Activate();}}
                var start=LauncherFileSearch.CodeStart(executable,target);if(!preview)Process.Start(start);Hide();
            }else if(CommandRequested!=null){query.Text=target;await RunCommand();}
            else{if(!preview)Process.Start(new ProcessStartInfo(target){UseShellExecute=true});Hide();}
        }catch(Exception e)when(FeatureFaults.Recoverable(e)){status.Text=e.Message;status.Visible=true;LayoutSearchResults();}
    }
}
