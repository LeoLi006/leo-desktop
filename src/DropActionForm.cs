namespace LeoLauncher;

// Existing drop panel, separated from payload parsing; actions still use the shared dispatcher.
sealed class DropActionForm:BarcaWindow
{
    readonly CancellationTokenSource life=new();readonly CancellationToken lifetime;bool busy,released;
    readonly Label status=new();readonly List<BarcaButton> buttons=[];
    public DropActionForm(DropContent content,Preferences p,Func<string,string,Task<string>> execute,Action<string[]> addModule,Action<string> note,Action<string> todo,IEnumerable<AppEntry> applications,
        Func<string,string,CancellationToken,Task<string>>? cancelableExecute=null)
    {
        lifetime=life.Token;AutoScaleMode=AutoScaleMode.None;Text="Leo · 拖放动作";StartPosition=FormStartPosition.Manual;
        var ids=content.ActionIds(p,applications);var primaryIds=new[]{"default","code","potplayer","choose_app","open_url"};
        int mainRows=(ids.Count(primaryIds.Contains)+2)/3,otherRows=(ids.Count(id=>!primaryIds.Contains(id))+2)/3;
        ClientSize=new(590,350+mainRows*48+otherRows*38);
        Controls.Add(BarcaUi.Chrome(this,content.Paths.Length>0?"用什么打开？":"选择一个动作",content.Paths.Length>0?$"已接收 {content.Paths.Length} 个文件或文件夹":content.IsUrl?"已接收网址":content.Image!=null?"已接收图片":"已接收文字"));
        var summary=new TextBox{Text=content.Summary,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Bounds=new(24,104,542,88),BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text,BorderStyle=BorderStyle.None};UiLanguage.Preserve(summary);
        var scope=new Label{Bounds=new(24,199,542,44),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9),Text=content.Paths.Length>1?"打开、VS Code 和定位处理下列全部项；其他应用及 PotPlayer 仅支持单项。":"打开操作只处理上面列出的这一项。"};
        if(content.Paths.Any(DropOpenExecution.MayRun))scope.Text="程序或脚本不提供默认运行，请显式选择应用查看或添加模块。";
        var primary=new FlowLayoutPanel{Bounds=new(20,247,552,mainRows*48),WrapContents=true};
        var secondary=new FlowLayoutPanel{Bounds=new(20,281+mainRows*48,552,otherRows*38),WrapContents=true};
        var secondaryTitle=new Label{Text="辅助操作",Bounds=new(24,251+mainRows*48,542,24),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(9)};
        status.SetBounds(24,ClientSize.Height-47,542,42);status.ForeColor=BarcaUi.Muted;status.Font=BarcaUi.Font(9);
        var titles=new Dictionary<string,string>{{"default","用系统默认应用打开"},{"code","VS Code 新窗口"},{"copy_image_file","复制图片"},{"copy_paths","复制路径"},{"reveal","打开所在文件夹"},{"note","存为便签"},{"todo","添加待办"},{"add_module","添加应用模块"},{"potplayer","用PotPlayer播放"},{"choose_app","选择其他应用…"},{"copy_image","复制图片"},{"save_image","保存图片…"},{"copy_text","复制文字"},{"open_url","打开网址"}};
        if(content.Paths.Length>0&&content.Paths.All(Directory.Exists))titles["default"]="用资源管理器打开";
        foreach(string id in ids){
            var button=new BarcaButton{Text=titles[id],Tag=id,Size=new(174,primaryIds.Contains(id)?40:30),Margin=new Padding(5),TabIndex=buttons.Count};
            if(id=="choose_app"&&content.Paths.Length>1){button.Enabled=false;button.Text="其他应用（仅单项）";}
            buttons.Add(button);
            button.Click+=async(_,_)=>{
                if(busy)return;busy=true;foreach(var b in buttons)b.Enabled=false;
                async Task<string> Run(string action,string value){lifetime.ThrowIfCancellationRequested();string result=cancelableExecute!=null?await cancelableExecute(action,value,lifetime):await execute(action,value);lifetime.ThrowIfCancellationRequested();return result;}
                string Payload(string mode)=>System.Text.Json.JsonSerializer.Serialize(new{mode,paths=content.Paths});
                try{
                    switch(id){
                        case "default":case "code":status.Text=await Run("files_with_app",Payload(id));break;
                        case "copy_paths":Clipboard.SetText(string.Join(Environment.NewLine,content.Paths));status.Text="已复制路径";break;
                        case "copy_text":Clipboard.SetText(content.Text);status.Text="已复制文字";break;
                        case "copy_image_file":{if(new FileInfo(content.Paths[0]).Length>20_000_000)throw new ArgumentException("图片过大，请用图片软件打开");using var image=System.Drawing.Image.FromFile(content.Paths[0]);if((long)image.Width*image.Height>32_000_000)throw new ArgumentException("图片尺寸过大");Clipboard.SetImage(image);status.Text="已复制图片";break;}
                        case "copy_image":Clipboard.SetImage(content.Image!);status.Text="已复制图片";break;
                        case "save_image":using(var picker=new SaveFileDialog{Filter="PNG图片|*.png",FileName="Leo-image.png"}){if(picker.ShowDialog(this)==DialogResult.OK){content.Image!.Save(picker.FileName,System.Drawing.Imaging.ImageFormat.Png);status.Text="图片已保存";}else status.Text="已取消选择，未提交打开请求。";}break;
                        case "reveal":status.Text=await Run("files_with_app",Payload("reveal"));break;
                        case "open_url":status.Text=await Run("web_open",content.Text);break;
                        case "potplayer":FileDropActions.RequirePaths(content.Paths);if(!DropOpenExecution.MediaAvailable(p))throw new IOException("PotPlayer 已停用或程序不可用。");status.Text=await Run("media_file",content.Paths.Single());break;
                        case "add_module":addModule(FileDropActions.RequirePaths(content.Paths));Close();break;
                        case "note":note(content.Summary);Close();break;
                        case "todo":todo(content.Summary);Close();break;
                        case "choose_app":
                            using(var picker=new DropApplicationPicker(p,!Directory.Exists(content.Paths.Single()))){
                                picker.StartPosition=FormStartPosition.Manual;picker.Location=WindowInteraction.PlaceDialog(picker.Size,Bounds,WindowPositions.PetBounds?.Invoke()??Rectangle.Empty,Screen.FromControl(this).WorkingArea);
                                if(picker.ShowDialog(this)==DialogResult.OK)status.Text=await Run("files_with_app",System.Text.Json.JsonSerializer.Serialize(new{mode=picker.SystemChoice?"system":"app",application=picker.Executable,module=picker.ModuleId,paths=content.Paths}));
                                else status.Text="已取消选择，未提交打开请求。";
                            }break;
                    }
                }catch(OperationCanceledException){if(!IsDisposed)status.Text="已取消操作；已提交的请求不会撤回。";}
                catch(Exception error)when(FeatureFaults.Recoverable(error)){if(!IsDisposed)status.Text=UiLanguage.T(error.Message);}
                finally{busy=false;if(!IsDisposed)foreach(var b in buttons)b.Enabled=!(b.Tag as string=="choose_app"&&content.Paths.Length>1);}
            };
            (primaryIds.Contains(id)?primary:secondary).Controls.Add(button);
        }
        Controls.AddRange([summary,scope,primary,secondaryTitle,secondary,status]);MinimumSize=MaximumSize=Size;
        Shown+=(_,_)=>buttons.FirstOrDefault(b=>b.Enabled)?.Focus();
        FormClosing+=(_,_)=>{if(!released)life.Cancel();};Disposed+=(_,_)=>{if(released)return;released=true;life.Cancel();life.Dispose();content.Dispose();};
    }
    protected override bool ProcessCmdKey(ref Message message,Keys key)
    {
        if(key==Keys.Escape){Close();return true;}
        if(ActiveControl is not TextBoxBase&&key is Keys.Left or Keys.Right or Keys.Up or Keys.Down){
            var enabled=buttons.Where(b=>b.Enabled).ToArray();if(enabled.Length>0){int current=Array.IndexOf(enabled,ActiveControl);int delta=key==Keys.Left?-1:key==Keys.Right?1:key==Keys.Up?-3:3;enabled[Math.Clamp(current+delta,0,enabled.Length-1)].Focus();}return true;
        }
        if(key==Keys.Enter&&ActiveControl is Button button){button.PerformClick();return true;}return base.ProcessCmdKey(ref message,key);
    }
}
