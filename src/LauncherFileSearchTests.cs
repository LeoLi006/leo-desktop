using System.Reflection;
using System.Text.Json;
namespace LeoLauncher;
static class LauncherFileSearchTests
{
    static FieldInfo Field(string name)=>typeof(LauncherForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!;
    static T Read<T>(LauncherForm form,string name)=>(T)Field(name).GetValue(form)!;
    public static void Run(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        using var host=new Form{ShowInTaskbar=false,Opacity=0};
        host.Shown+=async(_,_)=>{
            var checks=new Dictionary<string,bool>();
            try{
                string root=Path.Combine(Path.GetDirectoryName(report)!,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
                string folder=Path.Combine(root,"项目 workspace");Directory.CreateDirectory(folder);
                string file=Path.Combine(folder,"计划 notes.txt");File.WriteAllText(file,"fixture");
                string exe=Path.Combine(root,"Code.exe");File.WriteAllText(exe,"");
                checks["file_name_recursive"]=LauncherFileSearch.Find(root,"notes",false,default).Items.Single().Path==file;
                checks["folder_name"]=LauncherFileSearch.Find(root,"workspace",true,default).Items.Single().Path==folder;
                checks["type_filter"]=LauncherFileSearch.Find(root,"workspace",false,default).Items.Length==0;
                using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{LauncherFileSearch.Find(root,"x",false,cancel.Token);checks["cancel"]=false;}catch(OperationCanceledException){checks["cancel"]=true;}}
                checks["missing_directory"]=LauncherFileSearch.Find(Path.Combine(root,"missing"),"x",false,default).Items.Length==0;
                foreach(var target in new[]{folder,file}){
                    var start=LauncherFileSearch.CodeStart(exe,target);
                    checks["new_window_"+(target==folder?"folder":"file")]=start.FileName==exe&&start.ArgumentList.SequenceEqual(new[]{"--new-window",target})&&start.UseShellExecute;
                }
                File.Delete(file);try{LauncherFileSearch.CodeStart(exe,file);checks["reject_deleted"]=false;}catch(IOException){checks["reject_deleted"]=true;}File.WriteAllText(file,"");
                checks["old_prefix_completion"]=PathCompletion.Find(Path.Combine(folder,"计划")).Single().Path==file;
                var prefs=new Preferences{ApplicationIndexing=false,ContextAware=false};
                using var form=new LauncherForm(prefs,()=>{},true,true,scanner:_=>Enumerable.Range(0,14).Select(i=>new AppEntry("Fixture "+i,@"C:\fixture\app"+i+".exe","fixture",@"C:\fixture\app"+i+".exe")).ToList());
                form.StartPosition=FormStartPosition.Manual;form.Location=new(-10000,-10000);form.Open();
                var mode=Read<ComboBox>(form,"searchKind");var list=Read<ListBox>(form,"pathChoices");
                checks["default_app_mode"]=mode.SelectedIndex==0&&!list.Visible;
                prefs.ApplicationIndexing=true;form.ConfigureActivity();await Task.Delay(350);checks["app_two_rows_preserved"]=form.VerifyTwoRows();
                Field("fileScope").SetValue(form,root);Read<Button>(form,"searchScope").Text=UiLanguage.T("搜索目录：")+root;mode.SelectedIndex=1;form.PreviewTyping("notes");await Task.Delay(500);
                checks["real_file_results"]=list.Visible&&list.Items.Cast<PathChoice>().Single().Path==file;
                mode.SelectedIndex=2;form.PreviewTyping("workspace");await Task.Delay(500);
                checks["real_folder_results"]=list.Items.Cast<PathChoice>().Single().Path==folder;
                var key=new KeyEventArgs(Keys.Down);typeof(LauncherForm).GetMethod("PathKey",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,[key]);checks["keyboard_selection"]=key.SuppressKeyPress&&list.SelectedIndex==0;
                form.PreviewTyping(folder);await Task.Delay(500);
                checks["exact_folder_first"]=list.SelectedItem is PathChoice exact&&exact.Path==folder;
                form.PreviewTyping("notes");form.Hide();await Task.Delay(350);
                checks["hide_cancels"]=Read<CancellationTokenSource>(form,"pathLookup").IsCancellationRequested;
                form.Open();checks["reopen_defaults_apps"]=mode.SelectedIndex==0&&!list.Visible;
                form.PreviewTyping(file);await Task.Delay(500);
                checks["app_mode_preserves_paths"]=list.SelectedItem is PathChoice current&&current.Path==file;
                using var screenshot=new Bitmap(form.Width,form.Height);mode.SelectedIndex=1;form.PreviewTyping("notes");await Task.Delay(500);form.DrawToBitmap(screenshot,form.ClientRectangle);screenshot.Save(Path.Combine(Path.GetDirectoryName(report)!,"file-search.png"));
                File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks},new JsonSerializerOptions{WriteIndented=true}));
            }catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks}));}
            finally{host.Close();}
        };Application.Run(host);
    }
}
