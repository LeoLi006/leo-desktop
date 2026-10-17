namespace LeoLauncher;

sealed partial class TodoStore
{
    readonly List<(Dictionary<string,DailyTask> Before,Dictionary<string,DailyTask> After)> undoHistory=new();
    public bool CanUndo=>undoHistory.Count>0;
    void RememberUndo(List<DailyTask> before,List<DailyTask> after)
    {
        var a=before.ToDictionary(t=>t.Id);var b=after.ToDictionary(t=>t.Id);
        var changed=a.Keys.Union(b.Keys).Where(id=>a.GetValueOrDefault(id)!=b.GetValueOrDefault(id)).ToHashSet();
        if(changed.Count==0)return;
        undoHistory.Add((a.Where(p=>changed.Contains(p.Key)).ToDictionary(),b.Where(p=>changed.Contains(p.Key)).ToDictionary()));
        if(undoHistory.Count>20)undoHistory.RemoveAt(0);
    }
    public bool Undo()
    {
        if(!CanUndo)return false;
        var frame=undoHistory[^1];var ids=frame.Before.Keys.Union(frame.After.Keys).ToHashSet();
        if(ids.Any(id=>records.FirstOrDefault(t=>t.Id==id)!=frame.After.GetValueOrDefault(id)))
            throw new InvalidOperationException("相关事项已被提醒或赛程同步更新，为保留新信息，本次操作不能撤销。");
        var next=records.Where(t=>!ids.Contains(t.Id)).Concat(frame.Before.Values).ToList();
        Commit(next,false);undoHistory.RemoveAt(undoHistory.Count-1);return true;
    }
    public void SkipOccurrence(string id)
    {
        var task=Find(id);if(task.SeriesId==null)throw new ArgumentException("这不是重复事项");
        Commit(records.Select(t=>t.Id==id?t with{Deleted=true}:t).ToList());
    }
    public List<DailyTask> Overdue(DateOnly today)=>Items.Where(t=>t.Day<today&&t.Completed==null).OrderBy(t=>t.Day).ThenBy(t=>t.Created).ToList();
}
