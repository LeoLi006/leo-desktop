namespace LeoLauncher;
sealed class QuickNoteForm:BarcaWindow
{
    readonly ModuleThemeBinding theme;
    readonly TextBox input=new(){Bounds=new(24,108,472,33),MaxLength=2000,TabIndex=0,BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text,PlaceholderText="先记一句话…",AccessibleName="速记内容"};readonly Label status=new(){Bounds=new(24,152,472,38),ForeColor=BarcaUi.Muted};
    public QuickNoteForm(NoteStore store):this(NotesClient.Standalone(store)){}
    internal ActionResult? SavedResult{get;private set;}
    readonly NotesClient client;
    protected override void Dispose(bool disposing){if(disposing){theme.Dispose();client.Dispose();}base.Dispose(disposing);}
    public QuickNoteForm(NotesClient store){client=store;Text="Leo · 一句话速记";ClientSize=new(520,248);StartPosition=FormStartPosition.CenterParent;Controls.Add(BarcaUi.Chrome(this,"先记下来","Enter记下 · Esc取消 · 不用填写日期。"));var save=new BarcaButton{Text="记下",Bounds=new(370,201,126,32)};var cancel=new BarcaButton{Text="取消",Bounds=new(230,201,126,32),DialogResult=DialogResult.Cancel};save.Click+=(_,_)=>{try{store.Save(null,input.Text);SavedResult=store.LastResult;DialogResult=DialogResult.OK;Close();}catch(Exception e){status.Text="没有保存成功，请保留文字后重试。";status.AccessibleDescription=e.GetType().Name;}};cancel.Click+=(_,_)=>Close();AcceptButton=save;CancelButton=cancel;Controls.AddRange([input,status,save,cancel]);MinimumSize=MaximumSize=Size;theme=new(this);}
    internal void FocusEntry(){input.Focus();}
}
sealed partial class PetForm
{
    QuickNoteForm? quickNoteWindow;
    void ShowQuickNote()=>RunNotesEntry("note_quick_open","","quick-note-entry");
    void ShowQuickNoteCore(){RevealDock(true);launcher.Hide();if(quickNoteWindow==null||quickNoteWindow.IsDisposed){quickNoteWindow=new QuickNoteForm(new NotesClient(Notes,commands,"quick-note-ui"));quickNoteWindow.FormClosed+=(sender,closed)=>{presentation.Invalidate(ModuleWindowTarget.QuickNote);if(sender is QuickNoteForm{DialogResult:DialogResult.OK,SavedResult:{} saved})_ = presentation.Feedback(ModuleWindowTarget.QuickNote,CancellationToken.None).ReportAsync(saved with{Message="已记下"},true);};}if(!quickNoteWindow.Visible)WindowPositions.Prepare(quickNoteWindow,"quick-note",Bounds,previewMode);WindowInteraction.Show(quickNoteWindow);quickNoteWindow.FocusEntry();}
}

static class QuickNoteCompatibility
{
    public static void Migrate(System.Text.Json.JsonElement source,Preferences? p){if(p==null||source.ValueKind!=System.Text.Json.JsonValueKind.Object||source.TryGetProperty("QuickNoteHotkey",out _))return;var original=new[]{p.SearchHotkey,p.VoiceHotkey,p.MusicModule?p.LyricsHotkey:Keys.None};if(original.Contains(p.QuickNoteHotkey))p.QuickNoteHotkey=Keys.None;}
}
