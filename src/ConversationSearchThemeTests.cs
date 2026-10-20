using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Diagnostics;
namespace LeoLauncher;
static class ConversationSearchThemeTests
{
    sealed class Speech:ISpeechIO
    {
        public int InputDevice{get;set;}=-1;public bool Listening=>false;public bool Finishing=>false;public bool Speaking=>false;public long Generation=>0;public bool Disposed;
        public bool IsCurrent(long value)=>!Disposed&&value==0;
        public event Action<string,bool,long>? Recognized{add{}remove{}}public event Action<string,long>? Status{add{}remove{}}public event Action<long>? Changed{add{}remove{}}
        public void Start()=>throw new InvalidOperationException("microphone forbidden");public void FinishListening(){}public void StopListening(){}public void Speak(string text)=>throw new InvalidOperationException("speech forbidden");public void StopSpeaking(){}public void Stop(){}public void Dispose()=>Disposed=true;
    }
    static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    static ModuleThemeBinding Binding(Form window){var table=(ConditionalWeakTable<Form,ModuleThemeBinding>)typeof(ModuleThemeBinding).GetField("bindings",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;return table.TryGetValue(window,out var value)?value:throw new Exception("binding missing");}
    static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control child in root.Controls)foreach(var c in Walk(child))yield return c;}
    static void Queue(ModuleThemeBinding binding,ModuleThemeSnapshot snapshot)=>Task.Run(()=>typeof(ModuleThemeBinding).GetMethod("Changed",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(binding,[snapshot])).GetAwaiter().GetResult();
    public static void Run(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var host=new Form{Opacity=0,ShowInTaskbar=false};
        host.Shown+=async(_,_)=>{
            var c=new Dictionary<string,bool>();var original=UiTheme.Snapshot();var options=UiTheme.Options;
            try{
                var speech=new Speech();using var chat=new ChatForm(true,ChatTests.Settings(),()=>ChatTests.FixtureApi(),speech:speech);
                int scans=0,captures=0;var prefs=new Preferences{ApplicationIndexing=false,ContextAware=false,ClipboardExpireDays=0};
                using var launcher=new LauncherForm(prefs,()=>{},true,true,scanner:_=>{Interlocked.Increment(ref scans);return [new("隔离应用",@"C:\fixture\app.exe","fixture")];},observationOverride:()=>{});
                string clipPath=Path.Combine(Path.GetDirectoryName(path)!,"isolated-clips.json");File.Delete(clipPath);File.Delete(clipPath+".bak");
                using var clips=new ClipboardHistoryService(prefs,true,clipPath,()=>captures++);
                var entry=new ClipItem("text","隔离文字","隔离文字",DateTimeOffset.Now);clips.History.Add(entry);
                using var clipboard=ClipboardTool.Open(clips)!;Form[] windows=[chat,launcher,clipboard];var bindings=windows.Select(Binding).ToArray();
                foreach(var w in windows){w.ShowInTaskbar=false;w.StartPosition=FormStartPosition.Manual;w.Location=new(-20000,-20000);w.Show();}
                var fonts=windows.Select(w=>w.Font).ToArray();var sizes=windows.Select(w=>w.Size).ToArray();var clipboardList=Walk(clipboard).OfType<ListBox>().Single();
                c["three_windows_subscribed"]=bindings.All(b=>b.Subscribed);
                foreach(var skin in new[]{"382","20232","202","164","83"}){UiTheme.Configure(skin,"Leo");c["theme_"+skin]=bindings.All(b=>b.Current==UiTheme.Snapshot())&&chat.BackColor==BarcaUi.Canvas&&launcher.BackColor==Appearance.Background&&clipboard.BackColor==UiTheme.Color(24,29,44);}
                c["fonts_and_layout_preserved"]=windows.Select((w,i)=>ReferenceEquals(w.Font,fonts[i])&&w.Size==sizes[i]).All(v=>v);
                var sending=chat.PreviewSendAsync("你好");UiTheme.Configure("382","Leo");await sending;
                c["streamed_reply_survives_theme_change"]=chat.PreviewReplyComplete&&chat.ConversationTurnCount==1&&Walk(chat).OfType<RichTextBox>().Any(r=>r.ForeColor==BarcaUi.Text);
                prefs.ApplicationIndexing=true;launcher.ConfigureActivity();var deadline=DateTime.UtcNow.AddSeconds(3);while(launcher.IsIndexing&&DateTime.UtcNow<deadline)await Task.Delay(10);
                launcher.Open();launcher.PreviewTyping("隔离");await Task.Delay(180);
                c["dynamic_application_results_themed"]=Walk(launcher).OfType<Button>().Any(b=>b.Tag is AppEntry a&&a.Name=="隔离应用"&&b.ForeColor==Appearance.Ink);
                int scanCount=scans;UiTheme.Configure("164","Leo");c["theme_does_not_restart_index"]=scans==scanCount;
                c["search_query_preserved"]=Field<TextBox>(launcher,"query").Text=="隔离";
                c["clipboard_selection_preserved"]=clipboardList.SelectedItem is ClipItem selected&&selected.Key==entry.Key;
                var pin=Walk(clipboard).OfType<Button>().Single(b=>b.Text=="收藏");pin.PerformClick();UiTheme.Configure("202","Leo");
                c["clipboard_pin_and_body_preserved"]=clips.History.Items.Any(i=>i.Key==entry.Key&&i.Pinned&&i.Content==entry.Content);
                foreach(var pair in windows.Zip(bindings)){
                    string key=pair.First is ChatForm?"chat":pair.First is LauncherForm?"search":"clipboard";
                    var old=pair.Second.Current;Queue(pair.Second,old);pair.First.Hide();UiTheme.Configure(UiTheme.Skin=="164"?"202":"164","Leo");await Task.Delay(15);
                    c[key+"_hide_unsubscribes"]=!pair.Second.Subscribed&&pair.Second.Current==old;
                    pair.First.Show();c[key+"_reopen_current"]=pair.Second.Subscribed&&pair.Second.Current==UiTheme.Snapshot();
                    Queue(pair.Second,pair.Second.Current);pair.First.Close();await Task.Delay(15);
                    c[key+"_close_releases"]=!pair.Second.Subscribed&&!ModuleThemeBinding.Managed(pair.First);
                }
                int beforeCapture=captures;clips.Capture();await Task.Delay(20);
                c["closing_clipboard_keeps_listener_service"]=!clips.IsClosed&&captures>beforeCapture;
                c["chat_close_disposes_mock_voice"]=speech.Disposed;
                var process=Process.GetCurrentProcess();process.Refresh();var memory=process.WorkingSet64;int handles=process.HandleCount;var watch=Stopwatch.StartNew();bool drained=true;
                for(int i=0;i<12;i++){using var w=new ChatForm(true,speech:new Speech());w.ShowInTaskbar=false;w.Location=new(-20000,-20000);w.Show();var b=Binding(w);Queue(b,b.Current);w.Hide();w.Dispose();drained&=!b.Subscribed&&!ModuleThemeBinding.Managed(w);}
                await Task.Delay(25);process.Refresh();watch.Stop();c["twelve_chat_lifecycles_drained"]=drained;
                File.WriteAllText(path,JsonSerializer.Serialize(new{ok=c.Values.All(v=>v),checks=c,resources=new{rounds=12,elapsed_ms=watch.ElapsedMilliseconds,handle_delta=process.HandleCount-handles,working_set_delta=process.WorkingSet64-memory},real_api_requests=0,microphone_captures=0,real_clipboard_reads=0,external_process_starts=0}));
            }catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{ok=false,error=e.ToString(),checks=c}));}
            finally{UiTheme.Configure(original.Skin,original.Name,options);host.Close();}
        };Application.Run(host);
    }
}
