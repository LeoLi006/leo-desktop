using System.Drawing.Imaging;
namespace LeoLauncher;
// Measure the largest connected cream-fur region, excluding the blue/red kit and separate legs.
static class HeadGeometry
{
    public static Size Measure(Bitmap image,Rectangle cell)=>Bounds(image,cell).Size;
    public static Rectangle Bounds(Bitmap image,Rectangle cell){int width=cell.Width,height=cell.Height;var mask=new bool[width*height];for(int y=0;y<height;y++)for(int x=0;x<width;x++){var p=image.GetPixel(cell.X+x,cell.Y+y);mask[y*width+x]=p.A>160&&p.R>=185&&p.G>=150&&p.B>=100&&p.R>=p.G&&p.G>=p.B&&p.R-p.G<65;}
        int best=0;var result=new Rectangle(0,0,Math.Max(1,width),Math.Max(1,height));for(int at=0;at<mask.Length;at++){if(!mask[at])continue;mask[at]=false;var queue=new List<int>{at};int left=at%width,right=left,top=at/width,bottom=top;for(int n=0;n<queue.Count;n++){int value=queue[n],x=value%width,y=value/width;left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);foreach(var (dx,dy) in new[]{(-1,0),(1,0),(0,-1),(0,1)}){int nx=x+dx,ny=y+dy;if(nx<0||nx>=width||ny<0||ny>=height)continue;int next=ny*width+nx;if(mask[next]){mask[next]=false;queue.Add(next);}}}if(queue.Count>best){best=queue.Count;result=new(left,top,right-left+1,bottom-top+1);}}return result;}
    public static float Fit(Size reference,Size face)=>Math.Min(reference.Width/(float)face.Width,reference.Height/(float)face.Height);
}
