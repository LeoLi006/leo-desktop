namespace LeoLauncher;

readonly record struct DesktopDisplay(Rectangle Bounds,Rectangle Work);
static class DesktopDock
{
    public static DesktopDisplay[] Displays()=>Screen.AllScreens.Select(s=>new DesktopDisplay(s.Bounds,s.WorkingArea)).ToArray();
    public static Rectangle Area(Rectangle pet,IReadOnlyList<DesktopDisplay> screens)=>screens.OrderByDescending(s=>{var r=Rectangle.Intersect(pet,s.Bounds);return (long)Math.Max(0,r.Width)*Math.Max(0,r.Height);}).ThenBy(s=>Distance(pet,s.Bounds)).First().Work;
    static long Distance(Rectangle a,Rectangle b){long dx=Math.Max(0,Math.Max(b.Left-a.Right,a.Left-b.Right)),dy=Math.Max(0,Math.Max(b.Top-a.Bottom,a.Top-b.Bottom));return dx*dx+dy*dy;}
    internal static bool Exposed(Rectangle pet,Rectangle area,int footer,DockSide side,IReadOnlyList<DesktopDisplay> screens)
    {
        // Only the occupied segment matters: a staggered monitor can leave an exposed ledge.
        var physical=screens.First(s=>s.Work==area).Bounds;
        var edge=side switch{DockSide.Left=>new Rectangle(physical.Left-2,pet.Top,2,pet.Height),DockSide.Right=>new Rectangle(physical.Right,pet.Top,2,pet.Height),DockSide.Top=>new Rectangle(pet.Left,physical.Top-2,pet.Width,2),_=>new Rectangle(pet.Left,physical.Bottom,pet.Width,2)};
        return !screens.Any(s=>s.Work!=area&&s.Bounds.IntersectsWith(edge));
    }
    public static DockSide Choose(Rectangle pet,Rectangle area,int footer,Size movement,IReadOnlyList<DesktopDisplay> screens,int threshold=26)
    {
        var candidates=new[]{DockSide.Left,DockSide.Right,DockSide.Top,DockSide.Bottom}.Where(side=>side switch{DockSide.Left=>Math.Abs(pet.Left-area.Left)<=threshold,DockSide.Right=>Math.Abs(pet.Right-area.Right)<=threshold,DockSide.Top=>Math.Abs(pet.Top-area.Top)<=threshold,_=>Math.Abs(pet.Bottom+footer-area.Bottom)<=threshold}).Where(side=>Exposed(pet,area,footer,side,screens)).ToArray();
        return candidates.OrderBy(side=>side is DockSide.Top or DockSide.Bottom?Math.Abs(movement.Height)>Math.Abs(movement.Width)?0:1:Math.Abs(movement.Width)>=Math.Abs(movement.Height)?0:1).FirstOrDefault();
    }
    public static DockSide Detect(Rectangle pet,int footer,DockSide preferred)
    {
        var screens=Displays();var area=Area(pet,screens);
        if(preferred!=DockSide.None&&Choose(pet,area,footer,preferred is DockSide.Top or DockSide.Bottom?new Size(0,1):new Size(1,0),screens,3)==preferred)return preferred;
        return Choose(pet,area,footer,default,screens,3);
    }
    public static Point Snap(Rectangle pet,Rectangle area,int footer,DockSide side)=>side switch{DockSide.Left=>new(area.Left,pet.Y),DockSide.Right=>new(area.Right-pet.Width,pet.Y),DockSide.Top=>new(pet.X,area.Top),DockSide.Bottom=>new(pet.X,area.Bottom-pet.Height-footer),_=>pet.Location};
    public static Point Clamp(Rectangle pet,int footer,IReadOnlyList<DesktopDisplay> screens,bool dragging)
    {
        var area=Area(pet,screens);
        // While crossing a real join, permit the sprite to span both screens.
        bool Covered(Rectangle value,bool physical){long coverage=0;foreach(var s in screens){var r=Rectangle.Intersect(value,physical?s.Bounds:s.Work);coverage+=(long)Math.Max(0,r.Width)*Math.Max(0,r.Height);}return coverage>=(long)value.Width*value.Height;}
        if(dragging&&Covered(pet,true)||!dragging&&Covered(new Rectangle(pet.X,pet.Y,pet.Width,pet.Height+footer),false))return pet.Location;
        return new(Math.Clamp(pet.X,area.Left,Math.Max(area.Left,area.Right-pet.Width)),Math.Clamp(pet.Y,area.Top,Math.Max(area.Top,area.Bottom-pet.Height-footer)));
    }
}
