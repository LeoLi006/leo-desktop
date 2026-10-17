namespace LeoLauncher;
static class PlannerPreview
{
    public static void Save(string path){var day=DateOnly.FromDateTime(DateTime.Today);var store=new TodoStore(null);store.AddRepeating("每周出去踢一场球",day,"weekly",day.ToDateTime(new TimeOnly(18,30)));store.Add("给周末留一点休息时间",day);var done=store.Add("整理好这一周的安排",day);store.Toggle(done.Id);store.Add("准备下周的看球零食",day.AddDays(1),day.AddDays(1).ToDateTime(new TimeOnly(19,0)));using var window=new TodoForm(true,store);window.Show();window.View.ShowDay(day);Application.DoEvents();Capture(path);window.View.SetWeek(true);Application.DoEvents();Capture(Path.ChangeExtension(path,".week.png"));
        void Capture(string file){using var b=new Bitmap(window.Width,window.Height);window.DrawToBitmap(b,window.ClientRectangle);b.Save(file);}
    }
}
