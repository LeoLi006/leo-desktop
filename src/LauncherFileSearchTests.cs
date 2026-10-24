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
                checks["file_name_recursive"]=LauncherFileSearch.Find(root,"notes",default).Items.Single().Path==file;
                checks["folder_name"]=LauncherFileSearch.Find(root,"workspace",default).Items.Single().Path==folder;
                string matchingFolder=Path.Combine(root,"notes folder");Directory.CreateDirectory(matchingFolder);
                checks["mixed_file_folder_results"]=LauncherFileSearch.Find(root,"notes",default).Items.Select(i=>i.Directory).Distinct().Count()==2;
                using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{LauncherFileSearch.Find(root,"x",cancel.Token);checks["cancel"]=false;}catch(OperationCanceledException){checks["cancel"]=true;}}
                checks["missing_directory"]=LauncherFileSearch.Find(Path.Combine(root,"missing"),"x",default).Items.Length==0;
                foreach(var target in new[]{folder,file}){
                    var start=LauncherFileSearch.CodeStart(exe,target);
                    checks["new_window_"+(target==folder?"folder":"file")]=start.FileName==exe&&start.ArgumentList.SequenceEqual(new[]{"--new-window",target})&&start.UseShellExecute;
                }
                File.Delete(file);try{LauncherFileSearch.CodeStart(exe,file);checks["reject_deleted"]=false;}catch(IOException){checks["reject_deleted"]=true;}File.WriteAllText(file,"");
                checks["old_prefix_completion"]=PathCompletion.Find(Path.Combine(folder,"计划")).Single().Path==file;
                var prefs=new Preferences{ApplicationIndexing=false,ContextAware=false};
                Action? resized=null;
                using var form=new LauncherForm(prefs,()=>resized?.Invoke(),true,true,scanner:_=>Enumerable.Range(0,14).Select(i=>new AppEntry("Fixture "+i,@"C:\fixture\app"+i+".exe","fixture",@"C:\fixture\app"+i+".exe")).ToList());
                form.StartPosition=FormStartPosition.Manual;form.Location=new(-10000,-10000);form.Open();
                var apps=Read<SearchModeButton>(form,"appSearchButton");var files=Read<SearchModeButton>(form,"fileSearchButton");var list=Read<ListBox>(form,"pathChoices");
                checks["default_app_mode"]=!Read<bool>(form,"fileSearchMode")&&!list.Visible;
                var appSize=form.ClientSize;
                prefs.ApplicationIndexing=true;form.ConfigureActivity();await Task.Delay(350);checks["app_two_rows_preserved"]=form.VerifyTwoRows();
                Field("fileScope").SetValue(form,root);Read<Button>(form,"searchScope").Text=UiLanguage.T("搜索目录：")+root;files.PerformClick();form.PreviewTyping("notes");await Task.Delay(500);
                checks["real_file_results"]=list.Visible&&list.Items.Cast<PathChoice>().Any(i=>i.Path==file)&&list.Items.Cast<PathChoice>().Any(i=>i.Path==matchingFolder);
                files.PerformClick();form.PreviewTyping("workspace");await Task.Delay(500);
                checks["real_folder_results"]=list.Items.Cast<PathChoice>().Single().Path==folder;
                var key=new KeyEventArgs(Keys.Down);typeof(LauncherForm).GetMethod("PathKey",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,[key]);checks["keyboard_selection"]=key.SuppressKeyPress&&list.SelectedIndex==0;
                form.PreviewTyping(folder);await Task.Delay(500);
                checks["exact_folder_first"]=list.SelectedItem is PathChoice exact&&exact.Path==folder;
                form.PreviewTyping("notes");form.Hide();await Task.Delay(350);
                checks["hide_cancels"]=Read<CancellationTokenSource>(form,"pathLookup").IsCancellationRequested;
                form.Open();checks["reopen_defaults_apps"]=!Read<bool>(form,"fileSearchMode")&&!list.Visible;
                form.PreviewTyping(file);await Task.Delay(500);
                checks["app_mode_preserves_paths"]=list.SelectedItem is PathChoice current&&current.Path==file;
                checks["two_icon_switch_no_dropdown"]=apps.Active&&!files.Active&&!form.Controls.OfType<ComboBox>().Any();
                files.PerformClick();form.PreviewTyping("notes");await Task.Delay(500);var fileSize=form.ClientSize;
                checks["full_rows"]=list.Height%list.ItemHeight==0;
                checks["identical_mode_sizes"]=fileSize==appSize;
                var slider=form.Controls.OfType<MsnSlider>().Single();int resizeCalls=0,lateInvalidations=0;bool placementComplete=false;
                slider.Invalidated+=(_,_)=>{if(placementComplete)lateInvalidations++;};
                resized=()=>{resizeCalls++;form.Location=new(form.Left+1,form.Top);slider.Update();placementComplete=true;};
                bool sliderPixels=true;
                foreach(int value in new[]{80,155,230,80,230}){
                    placementComplete=false;slider.Value=value;
                    using var paint=new Bitmap(slider.Width,slider.Height);slider.DrawToBitmap(paint,slider.ClientRectangle);
                    int knob=8+(slider.Width-16)*(value-80)/150;
                    sliderPixels&=paint.GetPixel(knob,slider.Height/2).ToArgb()==Appearance.Accent.ToArgb();
                }
                checks["file_slider_updates_size"]=resizeCalls==5&&prefs.PetSize==230;
                checks["slider_repaints_after_popup_move"]=lateInvalidations>=5;
                checks["file_slider_knob_rendered"]=sliderPixels;
                checks["slider_keeps_window_size"]=form.ClientSize==appSize;
                resized=null;

                form.PreviewTyping("not-a-match");await Task.Delay(500);checks["empty_layout_stable"]=form.ClientSize==fileSize;
                form.PreviewTyping("notes");await Task.Delay(500);checks["results_layout_stable"]=form.ClientSize==fileSize;
                var actions=Read<Panel>(form,"pathActions");var status=Read<Label>(form,"status");
                checks["no_control_overlap"]=actions.Bottom<=list.Top&&list.Bottom<=status.Top&&status.Bottom<=form.ClientSize.Height;
                // Paint long mixed results and exercise scroll/repaint after result replacement.
                for(int n=0;n<12;n++)File.WriteAllText(Path.Combine(folder,"notes_长名称_"+new string('a',90)+n+".txt"),"");
                form.PreviewTyping("notes_");await Task.Delay(500);checks["long_results"]=list.Items.Count==12;
                list.TopIndex=8;list.Refresh();list.TopIndex=0;
                using(var redraw=new Bitmap(form.Width,form.Height))form.DrawToBitmap(redraw,form.ClientRectangle);
                form.PreviewTyping("notes folder");await Task.Delay(500);checks["old_rows_cleared"]=list.Items.Count==1;
                form.PreviewTyping("notes");await Task.Delay(500);
                using(var screenshot=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(screenshot,form.ClientRectangle);screenshot.Save(Path.Combine(Path.GetDirectoryName(report)!,"file-search.png"));}
                form.CommandRequested=(_,_)=>throw new IOException("fixture failure");
                await (Task)typeof(LauncherForm).GetMethod("OpenChosenPath",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,[false])!;
                checks["failure_message_not_clipped"]=status.Visible&&status.Text=="fixture failure"&&form.ClientSize==fileSize&&status.Bottom<=form.ClientSize.Height;
                form.CommandRequested=null;form.PreviewTyping("notes");
                apps.PerformClick();checks["app_icon_restores_layout"]=!list.Visible&&apps.Active&&!files.Active&&form.ClientSize.Height==280;
                files.PerformClick();form.PreviewTyping("notes");apps.PerformClick();await Task.Delay(500);
                checks["switch_discards_old_search"]=!list.Visible&&!Read<bool>(form,"fileSearchMode");
                File.WriteAllText(report,JsonSerializer.Serialize(new{ok=checks.Values.All(v=>v),checks},new JsonSerializerOptions{WriteIndented=true}));
            }catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks}));}
            finally{host.Close();}
        };Application.Run(host);
    }
}
