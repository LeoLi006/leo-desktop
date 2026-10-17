using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
namespace LeoLauncher;

static class ClashShortcut
{
    [DllImport("user32.dll")]static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
    [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
    const int ProbeId=0x6C31;
    public static string? Read(string text)
    {
        var match=Regex.Match(text,@"toggle_system_proxy,([^'""\r\n\]]+)");
        return match.Success?match.Groups[1].Value.Trim():null;
    }
    public static (uint Mods,byte Key)? Parse(string chord)
    {
        uint mods=0;byte key=0;
        foreach(string part in chord.Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)){
            switch(part.ToLowerInvariant()){
                case "ctrl":case "control":case "cmdorctrl":case "commandorcontrol":mods|=2;break;
                case "alt":mods|=1;break;case "shift":mods|=4;break;
                case "super":case "win":case "meta":mods|=8;break;
                default:if(key!=0||!Enum.TryParse<Keys>(part,true,out var parsed)||(uint)parsed>255)return null;key=(byte)parsed;break;
            }
        }
        return mods!=0&&key!=0?(mods,key):null;
    }
    static bool Free(uint mods,byte key)
    {
        if(!Native.RegisterHotKey(IntPtr.Zero,ProbeId,mods,key))return false;
        Native.UnregisterHotKey(IntPtr.Zero,ProbeId);return true;
    }
    public static string Prepare(string text,string chord)
    {
        if(Read(text)!=null)return text;
        string newline=text.Contains("\r\n")?"\r\n":"\n",entry="- toggle_system_proxy,"+chord;
        var root=Regex.Match(text,@"(?m)^hotkeys:[^\r\n]*");
        if(root.Success){
            string value=root.Value[8..].Trim();
            if(value.Length!=0&&value is not("[]" or "null"))throw new InvalidOperationException("请先在 Clash 快捷键设置中配置系统代理切换快捷键");
            return text[..root.Index]+"hotkeys:"+newline+entry+text[(root.Index+root.Length)..];
        }
        return text+(text.Length==0||text.EndsWith('\n')?"":newline)+"hotkeys:"+newline+entry+newline;
    }
    public static string PrepareAvailable(string text)
    {
        if(Read(text)==null){
            string? chord=null;
            foreach(int number in new[]{11,10,9,8,7})if(Free(7,(byte)(0x6F+number))){chord="Ctrl+Alt+Shift+F"+number;break;}
            if(chord==null)throw new InvalidOperationException("Clash 后台控制快捷键被占用，请在 Clash 设置系统代理快捷键");
            text=Prepare(text,chord);
        }
        const string enabled=@"(?m)^enable_global_hotkey:[^\r\n]*";
        if(Regex.IsMatch(text,enabled))text=Regex.Replace(text,enabled,"enable_global_hotkey: true");
        else text+="\nenable_global_hotkey: true\n";
        return text;
    }
    public static bool TrySend(string text)
    {
        if(!Regex.IsMatch(text,@"(?m)^enable_global_hotkey:[ \t]*true\b"))return false;
        var chord=Read(text);var parsed=chord==null?null:Parse(chord);if(parsed==null)return false;
        var (mods,key)=parsed.Value;
        // A free chord means the newly started client has not registered it yet.
        if(Free(mods,key)||GetAsyncKeyState(key)<0)return false;
        var pressed=new List<byte>();
        try{
            foreach(var (mask,vk) in new[]{(2u,(byte)0x11),(1u,(byte)0x12),(4u,(byte)0x10),(8u,(byte)0x5B)})
                if((mods&mask)!=0&&GetAsyncKeyState(vk)>=0){keybd_event(vk,0,0,UIntPtr.Zero);pressed.Add(vk);}
            keybd_event(key,0,0,UIntPtr.Zero);keybd_event(key,0,2,UIntPtr.Zero);return true;
        }finally{foreach(byte vk in pressed.AsEnumerable().Reverse())keybd_event(vk,0,2,UIntPtr.Zero);}
    }
}
