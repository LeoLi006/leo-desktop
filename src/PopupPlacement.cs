namespace LeoLauncher;

static class FavoriteOrder
{
    public static List<AppEntry> Merge(IEnumerable<AppEntry> apps, IEnumerable<string> favorites, IEnumerable<AppEntry> recent)
    {
        var lookup = apps.GroupBy(x => x.Target, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        return favorites.Where(lookup.ContainsKey).Select(x => lookup[x]).Concat(recent)
            .DistinctBy(x => x.Target, StringComparer.OrdinalIgnoreCase).Take(14).ToList();
    }
}

static class PopupPlacement
{
    public static Point Beside(Rectangle anchor,Size popup,Rectangle area,int gap=5)
    {
        int x=anchor.Right+gap;
        if(x+popup.Width>area.Right)x=anchor.Left-popup.Width-gap;
        return new Point(Math.Clamp(x,area.Left,Math.Max(area.Left,area.Right-popup.Width)),Math.Clamp(anchor.Top,area.Top,Math.Max(area.Top,area.Bottom-popup.Height)));
    }
    public static (Point Popup, Point Pet) Arrange(Rectangle pet, Size popup, Rectangle area)
    {
        var point = Place(pet, popup, area);
        if (!pet.IntersectsWith(new Rectangle(point, popup))) return (point, pet.Location);
        // Rare small-screen case: make room vertically by moving Leo a little.
        if (area.Height >= pet.Height + popup.Height + 14)
        {
            pet.Y = Math.Clamp(pet.Y, area.Top + popup.Height + 14, area.Bottom - pet.Height);
            return (Place(pet, popup, area), pet.Location);
        }
        if (area.Width >= pet.Width + popup.Width + 14)
        {
            pet.X = Math.Clamp(pet.X, area.Left + popup.Width + 14, area.Right - pet.Width);
            return (Place(pet, popup, area), pet.Location);
        }
        return (point, pet.Location);
    }
    public static Point Place(Rectangle pet, Size popup, Rectangle area)
    {
        const int gap = 14;
        int x = Math.Clamp(pet.Left + (pet.Width - popup.Width) / 2, area.Left, Math.Max(area.Left, area.Right - popup.Width));
        int y = Math.Clamp(pet.Top + (pet.Height - popup.Height) / 2, area.Top, Math.Max(area.Top, area.Bottom - popup.Height));
        var candidates = new[] { new Point(x, pet.Top - popup.Height - gap), new Point(pet.Left - popup.Width - gap, y), new Point(pet.Right + gap, y), new Point(x, pet.Bottom + gap) };
        foreach (var point in candidates)
            if (area.Contains(new Rectangle(point, popup)) && !pet.IntersectsWith(new Rectangle(point, popup))) return point;
        // On a very small working area, retain the smallest overlap possible.
        return candidates.Select(p => new Point(Math.Clamp(p.X, area.Left, Math.Max(area.Left, area.Right - popup.Width)), Math.Clamp(p.Y, area.Top, Math.Max(area.Top, area.Bottom - popup.Height))))
            .OrderBy(p => { var r = Rectangle.Intersect(pet, new Rectangle(p, popup)); return (long)r.Width * r.Height; }).First();
    }
}
