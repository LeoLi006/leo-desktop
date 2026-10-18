using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text.Json;
namespace LeoLauncher;
static class ClipboardPrivacy
{
    public static event Action? Changed;public static void Notify()=>Changed?.Invoke();
    [DllImport("user32.dll")]static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    public static string NormalizeApp(string name){string file=Path.GetFileName(name.Trim());return (file.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)?file[..^4]:file).ToLowerInvariant();}
    public static bool Paused(Preferences p,DateTimeOffset now)=>p.ClipboardPauseUntil>now;
    public static bool Excluded(Preferences p,string? app)=>app!=null&&(p.ClipboardBlockedApps??[]).Any(n=>NormalizeApp(n)==NormalizeApp(app));
    public static bool Sensitive(string text)=>Regex.IsMatch(text,@"(?i)-----BEGIN [A-Z ]*PRIVATE KEY-----|\bsk-(?:proj-)?[a-z0-9_-]{16,}|\bBearer\s+[a-z0-9_.-]{16,}|\b(?:api[_-]?key|password|passwd|access[_-]?token|client[_-]?secret)[""']?\s*[:=]\s*[""']?\S{6,}|\beyJ[a-zA-Z0-9_-]{8,}\.[a-zA-Z0-9_-]{8,}\.[a-zA-Z0-9_-]{12,}");
    public static bool ShouldSkip(Preferences p,DateTimeOffset now,string? owner,string? foreground)=>Paused(p,now)||Excluded(p,owner)||Excluded(p,foreground);
    public static string? OwnerApp(){try{GetWindowThreadProcessId(GetClipboardOwner(),out uint id);if(id==0)return null;using var p=Process.GetProcessById((int)id);return p.ProcessName;}catch{return null;}}
    public static bool MarkedPrivate(IDataObject? data)
    {
        if(data?.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing")==true)return true;
        if(data?.GetDataPresent("CanIncludeInClipboardHistory")!=true)return false;
        object? value=data.GetData("CanIncludeInClipboardHistory");return value switch{int n=>n==0,byte[] bytes=>bytes.Length>=4&&BitConverter.ToInt32(bytes)==0,MemoryStream stream=>stream.Length>=4&&BitConverter.ToInt32(stream.ToArray())==0,_=>false};
    }
    public static string[] Apps(string text)
    {
        var names=text.Split([',','，',';','；','\r','\n'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(NormalizeApp).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();if(names.Length>60||names.Any(n=>!Regex.IsMatch(n,@"^[\p{L}\p{N} ._()\-]{1,80}$")))throw new ArgumentException("请填写应用程序名，例如 Bitwarden、WeChat；最多60项。");return names;
    }
}
sealed class ClipboardPrivacyForm:BarcaWindow
{
    public ClipboardPrivacyForm(Preferences prefs,Action changed,bool preview=false)
    {
        Text="Leo · 剪贴板保护";ClientSize=new(590,560);StartPosition=FormStartPosition.CenterParent;var chrome=BarcaUi.Chrome(this,"剪贴板保护","暂停记录、排除应用，过期历史自动移除；收藏保留。");
        var state=new Label{Bounds=new(24,102,540,29),ForeColor=BarcaUi.Gold};void State()=>state.Text=ClipboardPrivacy.Paused(prefs,DateTimeOffset.Now)?"已暂停至 "+prefs.ClipboardPauseUntil!.Value.ToLocalTime().ToString("MM.dd HH:mm"):"正在按保护规则记录";State();
        var pauses=new FlowLayoutPanel{Bounds=new(20,137,550,42)};foreach(int minutes in new[]{10,60,0}){var pause=new BarcaButton{Text=minutes==0?"恢复记录":"暂停 "+minutes+" 分钟",Size=new(160,32)};pause.Click+=(_,_)=>{try{var next=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(prefs))!;next.ClipboardPauseUntil=minutes==0?null:DateTimeOffset.Now.AddMinutes(minutes);if(!preview)ConfigMigration.Write(Store.FilePath,JsonSerializer.Serialize(next));prefs.ClipboardPauseUntil=next.ClipboardPauseUntil;changed();if(!preview)ClipboardPrivacy.Notify();State();}catch{state.Text="暂停状态未能保存，请检查配置目录权限。";}};pauses.Controls.Add(pause);}
        var secrets=new CheckBox{Text="跳过疑似密钥、令牌和私钥文本",Checked=prefs.ClipboardBlockSecrets,Bounds=new(24,188,540,28),ForeColor=BarcaUi.Text};var retention=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new(174,233,190,28),BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text};CompanionUi.ThemeCombo(retention);var days=new[]{1,7,30,0};retention.Items.AddRange(["1 天","7 天","30 天","不自动过期"]);retention.SelectedIndex=Math.Max(0,Array.IndexOf(days,prefs.ClipboardExpireDays));Controls.Add(new Label{Text="非收藏历史保留",Bounds=new(24,236,150,27),ForeColor=BarcaUi.Muted});
        Controls.Add(new Label{Text="这些应用内的复制不记录（每行一个程序名）",Bounds=new(24,282,540,26),ForeColor=BarcaUi.Muted});var apps=new TextBox{Text=string.Join(Environment.NewLine,prefs.ClipboardBlockedApps??[]),Multiline=true,ScrollBars=ScrollBars.Vertical,Bounds=new(24,315,430,130),BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text};var choose=new BarcaButton{Text="选择应用…",Bounds=new(466,315,100,33)};choose.Click+=(_,_)=>{using var picker=new OpenFileDialog{Filter="应用程序 (*.exe)|*.exe",CheckFileExists=true};if(picker.ShowDialog(this)==DialogResult.OK)apps.AppendText((apps.TextLength>0?Environment.NewLine:"")+Path.GetFileNameWithoutExtension(picker.FileName));};
        var note=new Label{Text="Windows 敏感内容标记始终尊重；自动识别无法覆盖所有密码，处理敏感内容时可先暂停。新规则不会自动删除已有收藏。",Bounds=new(24,461,408,75),ForeColor=BarcaUi.Muted,Font=BarcaUi.Font(8.5f)};var save=new BarcaButton{Text="保存规则",Bounds=new(453,490,113,36)};save.Click+=(_,_)=>{try{var names=ClipboardPrivacy.Apps(apps.Text);var next=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(prefs))!;next.ClipboardBlockedApps=names.ToList();next.ClipboardBlockSecrets=secrets.Checked;next.ClipboardExpireDays=days[retention.SelectedIndex];if(!preview)ConfigMigration.Write(Store.FilePath,JsonSerializer.Serialize(next));prefs.ClipboardBlockedApps=next.ClipboardBlockedApps;prefs.ClipboardBlockSecrets=next.ClipboardBlockSecrets;prefs.ClipboardExpireDays=next.ClipboardExpireDays;changed();if(!preview)ClipboardPrivacy.Notify();DialogResult=DialogResult.OK;}catch(Exception e){note.Text=e.Message;}};
        Controls.AddRange([state,pauses,secrets,retention,apps,choose,note,save,chrome]);MinimumSize=MaximumSize=Size;
    }
}
