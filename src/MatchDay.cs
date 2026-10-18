using System.Drawing.Imaging;
namespace LeoLauncher;
sealed record MatchDayChoice(Club Club,FootballMatch Match);
static class MatchDay
{
    public static MatchDayChoice? Choose(IEnumerable<FootballMatch> matches,IEnumerable<string> following,DateTimeOffset now)
    {
        var ids=following.ToHashSet();var day=DateOnly.FromDateTime(now.LocalDateTime);return matches.Where(m=>m.TimeKnown&&DateOnly.FromDateTime(m.Kickoff.LocalDateTime)==day&&m.Kickoff>now.AddHours(-6)&&m.State is "pre" or "in" or "post"&&!m.Status.Contains("POSTPON",StringComparison.OrdinalIgnoreCase)&&!m.Status.Contains("CANCEL",StringComparison.OrdinalIgnoreCase)).OrderBy(m=>m.State=="in"?0:m.Kickoff>=now?1:2).ThenBy(m=>Math.Abs((m.Kickoff-now).TotalMinutes)).Select(m=>new{Match=m,Club=new[]{m.HomeId,m.AwayId}.Where(ids.Contains).Select(ClubCatalog.Find).FirstOrDefault(c=>c!=null)}).Where(x=>x.Club!=null).Select(x=>new MatchDayChoice(x.Club!,x.Match)).FirstOrDefault();
    }
    public static string? Asset(string id)=>id switch{"382"=>"city-v1.png","20232"=>"miami-v1.png","202"=>"argentina-v1.png","164"=>"spain-v1.png",_=>null};
    public static Color ColorFor(string id)=>id switch{"382"=>Color.SkyBlue,"20232"=>Color.LightPink,"202"=>Color.LightSkyBlue,"164"=>Color.Gold,_=>BarcaUi.Gold};
}
sealed class MatchDaySprites:IDisposable
{
    readonly Dictionary<string,(Bitmap Image,Rectangle Source)> images=new();
    public (Bitmap Image,Rectangle Source) Get(string name)
    {
        if(images.TryGetValue(name,out var saved))return saved;var image=new Bitmap(Path.Combine(Store.Root,"assets","MatchDay",name));int left=image.Width,top=image.Height,right=0,bottom=0;
        var bits=image.LockBits(new Rectangle(Point.Empty,image.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{var pixels=new byte[Math.Abs(bits.Stride)*image.Height];System.Runtime.InteropServices.Marshal.Copy(bits.Scan0,pixels,0,pixels.Length);for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)if(pixels[y*Math.Abs(bits.Stride)+x*4+3]>64){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}}finally{image.UnlockBits(bits);}if(left>right||top>bottom){image.Dispose();throw new IOException("比赛日素材缺少可见内容");}var source=Rectangle.FromLTRB(left,top,right+1,bottom+1);source.Inflate(2,2);source=Rectangle.Intersect(source,new Rectangle(Point.Empty,image.Size));var value=(image,source);images[name]=value;return value;
    }
    public void Draw(Graphics graphics,RectangleF target,string name,long milliseconds)
    {
        var value=Get(name);float height=target.Height*.86f*(1+.006f*(float)Math.Sin(milliseconds/1100d));float scale=height/value.Source.Height;float width=value.Source.Width*scale;var destination=new RectangleF(target.Left+(target.Width-width)/2,target.Bottom-target.Height*.045f-height,width,height);graphics.DrawImage(value.Image,destination,value.Source,GraphicsUnit.Pixel);
    }
    public void Dispose(){foreach(var value in images.Values)value.Image.Dispose();images.Clear();}
}
sealed partial class PetForm
{
    MatchDayChoice? matchDayChoice;long matchDayLoaded=-60000;int matchDayFollowing=-1;string? matchDayAsset,matchDayTeam;
    void UpdateMatchDay()
    {
        if(!prefs.FootballModule||!prefs.MatchDayOutfits){if(matchDayTeam!=null){matchDayTeam=matchDayAsset=null;matchDayLoaded=-60000;Invalidate();}return;}
        if(prefs.MatchDayKit!="auto"){var club=ClubCatalog.Find(prefs.MatchDayKit);SetMatchDay(club?.Id);return;}
        if(clock.ElapsedMilliseconds-matchDayLoaded>60000||matchDayFollowing!=FootballFollowing.Current.Version){matchDayLoaded=clock.ElapsedMilliseconds;matchDayFollowing=FootballFollowing.Current.Version;matchDayChoice=MatchDay.Choose(LeoSummary.CachedMatches(),FootballFollowing.Current.Ids,DateTimeOffset.Now);SetMatchDay(matchDayChoice?.Club.Id);}
    }
    void SetMatchDay(string? id){if(matchDayTeam==id)return;matchDayTeam=id;matchDayAsset=id==null?null:MatchDay.Asset(id);Invalidate();}
    bool DrawMatchDay(Graphics graphics,RectangleF destination)
    {
        if(!prefs.FootballModule||!prefs.MatchDayOutfits||matchDayTeam==null)return false;
        if(CurrentSkin()!=null){DrawAnimationFrame(graphics,destination);return true;}
        // Other followed teams retain Leo's existing artwork with their authentic badge.
        return false;
    }
    void WireMatchDayMenu(ContextMenuStrip menu)
    {
        var outfits=new ToolStripMenuItem("比赛日装扮");menu.Items.Insert(2,outfits);menu.Opening+=(_,_)=>{outfits.Visible=prefs.FootballModule;outfits.DropDownItems.Clear();foreach(var (id,name) in new[]{("auto","自动跟随关注比赛"),("83","巴萨"),("382","曼城"),("20232","迈阿密国际"),("202","阿根廷"),("164","西班牙"),("off","关闭装扮")}){var option=new ToolStripMenuItem(name){Checked=id=="off"?!prefs.MatchDayOutfits:prefs.MatchDayOutfits&&prefs.MatchDayKit==id};option.Click+=(_,_)=>{prefs.MatchDayOutfits=id!="off";if(id!="off")prefs.MatchDayKit=id;matchDayLoaded=-60000;if(!previewMode)Store.Save(prefs);UpdateMatchDay();};outfits.DropDownItems.Add(option);}};
    }
}
