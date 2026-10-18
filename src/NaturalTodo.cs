using System.Globalization;
using System.Text.RegularExpressions;
namespace LeoLauncher;

sealed record NaturalTodoDraft(string Title,DateOnly Day,TimeOnly? Time,string Frequency,bool Estimated);
static class NaturalTodo
{
    static readonly Regex datePrefix=new(@"^(?<date>\d{4}[-/]\d{1,2}[-/]\d{1,2}|今天|明天|后天|大后天|每天|每日|(?:每周|每星期|下周|下星期|本周|这周|周|星期)[一二三四五六日天])",RegexOptions.CultureInvariant);
    static readonly Regex clockPrefix=new(@"^(?<period>凌晨|早上|早晨|上午|中午|下午|傍晚|晚上)?\s*(?<hour>[零〇一二两三四五六七八九十\d]{1,3})(?:点|时|[:：])(?<minute>半|一刻|三刻|[零〇一二两三四五六七八九十\d]{1,3}(?:分)?)?",RegexOptions.CultureInvariant);
    static int Number(string text){if(int.TryParse(text,out int n))return n;const string digits="零一二三四五六七八九";text=text.Replace('两','二').Replace('〇','零');if(text=="十")return 10;int split=text.IndexOf('十');if(split>=0){int tens=split==0?1:digits.IndexOf(text[0]);int ones=split==text.Length-1?0:digits.IndexOf(text[^1]);return tens<0||ones<0?-1:tens*10+ones;}return text.Length==1?digits.IndexOf(text[0]):-1;}
    public static NaturalTodoDraft? Parse(string input,DateTime now,DateOnly selected)
    {
        string rest=input.Trim();var d=datePrefix.Match(rest);DateOnly day=selected;string frequency="";bool found=d.Success,estimated=false;
        if(found){string date=d.Value;rest=rest[d.Length..].TrimStart(' ','，',',');var today=DateOnly.FromDateTime(now);
            if(date is "每天" or "每日"){frequency="daily";day=today;}
            else if(date is "今天" or "明天" or "后天" or "大后天")day=today.AddDays(date=="今天"?0:date=="明天"?1:date=="后天"?2:3);
            else if(char.IsAsciiDigit(date[0])){if(!DateOnly.TryParseExact(date.Replace('/','-'),new[]{"yyyy-M-d","yyyy-MM-dd","yyyy-M-dd","yyyy-MM-d"},CultureInfo.InvariantCulture,DateTimeStyles.None,out day))return null;}
            else {int weekday="一二三四五六日".IndexOf(date[^1]=='天'?'日':date[^1]);int current=((int)today.DayOfWeek+6)%7;bool weekly=date.StartsWith("每");frequency=weekly?"weekly":"";
                day=date.StartsWith("下")?today.AddDays(7-current+weekday):date.StartsWith("本")||date.StartsWith("这")?today.AddDays(weekday-current):today.AddDays((weekday-current+7)%7);}
        }
        TimeOnly? time=null;var t=clockPrefix.Match(rest);
        if(t.Success){int h=Number(t.Groups["hour"].Value);string minutes=t.Groups["minute"].Value.TrimEnd('分');int m=minutes.Length==0?0:minutes=="半"?30:minutes=="一刻"?15:minutes=="三刻"?45:Number(minutes);string period=t.Groups["period"].Value;
            if(period.Length>0&&h>12)return null;if(period is "下午" or "傍晚" or "晚上"&&h is >=1 and <12)h+=12;if(period=="中午"&&h is >=1 and <=4)h+=12;if(period=="凌晨"&&h==12)h=0;if(period=="晚上"&&h==12){h=0;day=day.AddDays(1);}
            if(h is <0 or >23||m is <0 or >59)return null;time=new(h,m);rest=rest[t.Length..].TrimStart(' ','，',',');found=true;
        }else{var period=Regex.Match(rest,@"^(凌晨|早上|早晨|上午|中午|下午|傍晚|晚上)");if(period.Success){time=new TimeOnly(period.Value switch{"凌晨"=>1,"早上" or "早晨"=>8,"上午"=>9,"中午"=>12,"下午"=>15,"傍晚"=>18,_=>20},0);estimated=true;rest=rest[period.Length..].TrimStart(' ','，',',');found=true;}}
        if(!found||string.IsNullOrWhiteSpace(rest)||Regex.IsMatch(rest,@"^(\d|[一二三四五六七八九十]).*(点|分)"))return null;
        if(frequency.Length>0&&time is {} clock&&day.ToDateTime(clock)<=now)day=day.AddDays(frequency=="daily"?1:7);
        return new(rest,day,time,frequency,estimated);
    }
}

sealed class NaturalTodoForm:BarcaWindow
{
    readonly TextBox title=new(){MaxLength=200,BackColor=BarcaUi.Raised,ForeColor=BarcaUi.Text};
    readonly DateTimePicker date=new(){Format=DateTimePickerFormat.Custom,CustomFormat="yyyy年MM月dd日"},time=new(){Format=DateTimePickerFormat.Custom,CustomFormat="HH:mm",ShowUpDown=true};
    readonly CheckBox reminder=new(){Text="到时提醒",ForeColor=BarcaUi.Text};readonly ComboBox repeat=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    public NaturalTodoDraft Draft=>new(title.Text.Trim(),DateOnly.FromDateTime(date.Value),reminder.Checked?TimeOnly.FromDateTime(time.Value):null,repeat.SelectedIndex==1?"daily":repeat.SelectedIndex==2?"weekly":"",false);
    public NaturalTodoForm(NaturalTodoDraft draft)
    {
        Text="确认清单安排";ClientSize=new(460,395);StartPosition=FormStartPosition.CenterParent;BackColor=BarcaUi.Canvas;ForeColor=BarcaUi.Text;Font=BarcaUi.Font(10);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;
        var header=BarcaUi.Chrome(this,"LEO  /  确认安排",draft.Estimated?"未指定具体时刻，先填入建议时间，可自行修改。":"已在本机识别，确认后才会保存。无需模型 API。");
        void Label(string text,int y){Controls.Add(new Label{Text=text,Bounds=new(22,y,92,28),ForeColor=BarcaUi.Muted});}
        title.Bounds=new(114,105,320,30);title.Text=draft.Title;Label("事项",109);
        date.Bounds=new(114,151,320,29);date.Value=draft.Day.ToDateTime(TimeOnly.MinValue);Label("日期",155);
        reminder.Bounds=new(22,198,90,30);reminder.Checked=draft.Time!=null;time.Bounds=new(114,198,145,29);time.Value=DateTime.Today.Add((draft.Time??new TimeOnly(20,0)).ToTimeSpan());time.Enabled=reminder.Checked;reminder.CheckedChanged+=(_,_)=>time.Enabled=reminder.Checked;
        repeat.BackColor=BarcaUi.Raised;repeat.ForeColor=BarcaUi.Text;repeat.FlatStyle=FlatStyle.Flat;repeat.DrawMode=DrawMode.OwnerDrawFixed;repeat.ItemHeight=25;repeat.DrawItem+=(_,e)=>{using var brush=new SolidBrush((e.State&DrawItemState.Selected)!=0?Appearance.Blue:BarcaUi.Raised);e.Graphics.FillRectangle(brush,e.Bounds);if(e.Index>=0)TextRenderer.DrawText(e.Graphics,repeat.Items[e.Index]!.ToString(),repeat.Font,e.Bounds,BarcaUi.Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);};repeat.Bounds=new(114,244,320,30);repeat.Items.AddRange(["单次","每天","每周（同一星期）"]);repeat.SelectedIndex=draft.Frequency=="daily"?1:draft.Frequency=="weekly"?2:0;Label("重复",248);
        var error=new Label{Bounds=new(22,286,412,40),ForeColor=Color.LightSalmon};var save=new BarcaButton{Text="确认添加",Bounds=new(320,341,114,34)};var cancel=new BarcaButton{Text="取消",Bounds=new(210,341,94,34),DialogResult=DialogResult.Cancel};
        save.Click+=(_,_)=>{var value=Draft;if(value.Title.Length==0){error.Text="请填写事项标题";return;}if(value.Time is {} at&&value.Day.ToDateTime(at)<=DateTime.Now){error.Text="提醒时间已过去，请调整时间或取消提醒。";return;}DialogResult=DialogResult.OK;};AcceptButton=save;CancelButton=cancel;
        Controls.AddRange([title,date,reminder,time,repeat,error,save,cancel,header]);MinimumSize=MaximumSize=Size;
    }
}
