using System.Text.Json;
using Microsoft.Win32;
namespace LeoLauncher;

sealed record WindowPosition(Rectangle Bounds, Rectangle WorkArea, string Monitor, bool Maximized);
static class WindowPositions
{
    static readonly Dictionary<string, WindowPosition> saved = Load();
    static readonly Dictionary<Form, Action> tracked = new();
    static string FilePath => Path.Combine(Store.Root, "leo-window-layouts.json");
    public static Func<Rectangle>? PetBounds;
    static Dictionary<string, WindowPosition> Load() { try { return JsonSerializer.Deserialize<Dictionary<string, WindowPosition>>(File.ReadAllText(FilePath)) ?? new(); } catch { return new(); } }
    static WindowPositions()
    {
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        Application.ApplicationExit += (_, _) => SystemEvents.DisplaySettingsChanged -= DisplayChanged;
    }
    static void DisplayChanged(object? sender, EventArgs e)
    {
        foreach (var form in tracked.Keys.ToArray())
            if (!form.IsDisposed && form.IsHandleCreated) try { form.BeginInvoke(() => { if (!form.IsDisposed) Prepare(form, form.Name, PetBounds?.Invoke() ?? Rectangle.Empty, false); }); } catch (InvalidOperationException) { }
    }
    public static Rectangle Restore(WindowPosition position, Rectangle area, Size size)
    {
        double x = position.WorkArea.Width > position.Bounds.Width ? (double)(position.Bounds.Left - position.WorkArea.Left) / (position.WorkArea.Width - position.Bounds.Width) : 0;
        double y = position.WorkArea.Height > position.Bounds.Height ? (double)(position.Bounds.Top - position.WorkArea.Top) / (position.WorkArea.Height - position.Bounds.Height) : 0;
        return new Rectangle(area.Left + (int)Math.Round(Math.Clamp(x, 0, 1) * Math.Max(0, area.Width - size.Width)), area.Top + (int)Math.Round(Math.Clamp(y, 0, 1) * Math.Max(0, area.Height - size.Height)), size.Width, size.Height);
    }
    public static void Prepare(Form form, string name, Rectangle pet, bool preview)
    {
        form.StartPosition = FormStartPosition.Manual;
        var screen = Screen.FromRectangle(pet);
        if (!preview && saved.TryGetValue(name, out var position))
        {
            screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == position.Monitor) ?? screen;
            if (form.FormBorderStyle == FormBorderStyle.Sizable)
                form.Size = new Size(Math.Clamp(position.Bounds.Width, form.MinimumSize.Width, Math.Max(form.MinimumSize.Width, screen.WorkingArea.Width)), Math.Clamp(position.Bounds.Height, form.MinimumSize.Height, Math.Max(form.MinimumSize.Height, screen.WorkingArea.Height)));
            form.Location = Restore(position, screen.WorkingArea, form.Size).Location;
            if (form.Bounds.IntersectsWith(pet)) form.Location = PopupPlacement.Place(pet, form.Size, screen.WorkingArea);
            if (position.Maximized && form.MaximizeBox) form.WindowState = FormWindowState.Maximized;
        }
        else form.Location = PopupPlacement.Place(pet, form.Size, screen.WorkingArea);
        if (preview || tracked.ContainsKey(form)) return;
        form.Name = name;
        void Save()
        {
            if (!form.IsHandleCreated || form.IsDisposed || form.WindowState == FormWindowState.Minimized) return;
            var bounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
            var monitor = Screen.FromRectangle(bounds);
            saved[name] = new(bounds, monitor.WorkingArea, monitor.DeviceName, form.WindowState == FormWindowState.Maximized);
            try { File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(saved)); File.Move(FilePath + ".tmp", FilePath, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        tracked[form] = Save;
        form.ResizeEnd += (_, _) => Save();
        form.FormClosing += (_, _) => Save();
        form.VisibleChanged += (_, _) => { if (!form.Visible) Save(); };
        form.FormClosed += (_, _) => tracked.Remove(form);
    }
}
