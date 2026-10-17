using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using Microsoft.Win32;
namespace LeoLauncher;

sealed record ProxyStatus(bool Running, bool? Enabled, string Description);

static class ClashProxy
{
    static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "io.github.clash-verge-rev.clash-verge-rev", "verge.yaml");
    public static (string Host, int Port) ReadEndpoint(string text)
    {
        string host = "127.0.0.1"; int port = 7897;
        foreach (var line in text.Split('\n'))
        {
            var match = Regex.Match(line, @"^(proxy_host|verge_mixed_port):\s*([^#\r]+)");
            if (!match.Success) continue;
            string value = match.Groups[2].Value.Trim().Trim('\'', '"');
            if (match.Groups[1].Value == "proxy_host") host = value;
            else if (int.TryParse(value, out int parsed) && parsed is > 0 and <= 65535) port = parsed;
        }
        return (host, port);
    }
    public static bool MatchesServer(string server, string host, int port)
    {
        foreach (var part in server.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string endpoint = part.Contains('=') ? part[(part.IndexOf('=') + 1)..] : part;
            if (endpoint.Trim().Equals($"{host}:{port}", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
    public static ProxyStatus ReadStatus()
    {
        try
        {
            var processes = Process.GetProcessesByName("clash-verge");
            bool running = processes.Length > 0; foreach (var process in processes) process.Dispose();
            if (!running) return new(false, null, "请先启动 Clash Verge");
            if (!File.Exists(ConfigPath)) return new(true, null, "未找到 Clash Verge Rev 配置");
            var endpoint = ReadEndpoint(File.ReadAllText(ConfigPath));
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            bool enabled = Convert.ToInt32(key?.GetValue("ProxyEnable", 0)) != 0;
            string server = key?.GetValue("ProxyServer", "") as string ?? "";
            if (!enabled) return new(true, false, "Clash 系统代理已关闭 · 点击开启");
            return MatchesServer(server, endpoint.Host, endpoint.Port)
                ? new(true, true, "Clash 系统代理已开启 · 点击关闭")
                : new(true, null, "系统代理由其他程序或地址接管，请先在 Clash 中确认状态");
        }
        catch { return new(false, null, "无法读取 Clash Verge 状态"); }
    }
    public static bool IsProxyLabel(string label) => Regex.Replace(label, @"\s", "").ToLowerInvariant() is "系统代理" or "系統代理" or "systemproxy";
    public static bool IsAligned(RectangleF label, RectangleF toggle)
        => label.Width > 0 && toggle.Width > 0 && Math.Abs(label.Top + label.Height / 2 - toggle.Top - toggle.Height / 2) <= Math.Max(16, Math.Min(label.Height, toggle.Height) * .6)
        && toggle.Left >= label.Right - 4 && toggle.Left - label.Right <= 800;
    static RectangleF Bounds(AutomationElement element)
    {
        var rect = element.Current.BoundingRectangle;
        return rect.IsEmpty ? RectangleF.Empty : new((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);
    }
    static AutomationElement? FindSwitch()
    {
        var candidates = new List<AutomationElement>();
        foreach (var process in Process.GetProcessesByName("clash-verge"))
        {
            using (process)
            {
                var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id));
                foreach (AutomationElement window in windows)
                {
                    var labels = window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
                        .Cast<AutomationElement>().Where(x => IsProxyLabel(x.Current.Name)).ToArray();
                    if (labels.Length == 0) continue;
                    var toggles = window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.IsTogglePatternAvailableProperty, true));
                    foreach (AutomationElement toggle in toggles)
                        if (toggle.Current.IsEnabled && string.IsNullOrWhiteSpace(toggle.Current.Name) && labels.Any(label => IsAligned(Bounds(label), Bounds(toggle))))
                            candidates.Add(toggle);
                }
            }
        }
        // Never guess when a layout change produces multiple matching switches.
        return candidates.Count == 1 ? candidates[0] : null;
    }
    public static bool InspectSwitch() => FindSwitch()?.TryGetCurrentPattern(TogglePattern.Pattern, out _) == true;
    public static ProxyStatus Toggle()
    {
        var watch = Stopwatch.StartNew();
        var before = ReadStatus();
        if (!before.Running || before.Enabled == null) throw new InvalidOperationException(before.Description);
        var control = FindSwitch() ?? throw new InvalidOperationException("未定位到系统代理开关，请先在 Clash Verge 打开首页后重试");
        var pattern = (TogglePattern)control.GetCurrentPattern(TogglePattern.Pattern);
        if (watch.ElapsedMilliseconds > 4000) throw new InvalidOperationException("Clash Verge 响应超时，请稍后重试");
        bool desired = !before.Enabled.Value;
        if (pattern.Current.ToggleState == ToggleState.Indeterminate || (pattern.Current.ToggleState == ToggleState.On) != before.Enabled)
            throw new InvalidOperationException("Clash 与系统代理状态不一致，请先在 Clash 内重新设置系统代理");
        pattern.Toggle();
        for (int i = 0; i < 20; i++)
        {
            Thread.Sleep(150); var after = ReadStatus();
            if (after.Enabled == desired) return after;
        }
        throw new InvalidOperationException("未确认系统代理切换结果，请在 Clash Verge 检查当前状态后再操作");
    }
}
