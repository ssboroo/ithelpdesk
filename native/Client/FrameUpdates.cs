using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace RainyRemote;
sealed record ImageTile(int x,int y,string jpeg);
sealed record FrameUpdate(int width,int height,string? jpeg,ImageTile[]? tiles){
 public object Packet(long id)=>new{k="frame",id,width,height,jpeg,tiles};
 internal int EncodedBytes=>(jpeg?.Length??0)+(tiles?.Sum(t=>t.jpeg.Length)??0);
}
// Compare reusable, scaled surfaces. Only changed 128px tiles cross the network.
sealed class FrameEncoder:IDisposable {
 const int Tile=128;Bitmap? scaled;Graphics? painter;byte[] current=[],previous=[];int stride,profile=-1,lastMode=-1;Size sourceSize;long lastFull,profileAt;bool hasBase;
 static (int width,long quality,int budget,int key) Profile(int latency,int mode)=>mode==1?(1920,85,450000,1):mode==2?(640,50,45000,2):latency>650?(960,52,65000,3):latency>280?(1280,65,140000,4):(1920,78,240000,5);
 void Resize(Size size){painter?.Dispose();scaled?.Dispose();scaled=new Bitmap(size.Width,size.Height,PixelFormat.Format24bppRgb);painter=Graphics.FromImage(scaled);painter.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.Bilinear;hasBase=false;}
 void Draw(Bitmap source){painter!.DrawImage(source,new Rectangle(Point.Empty,scaled!.Size));var data=scaled.LockBits(new Rectangle(Point.Empty,scaled.Size),ImageLockMode.ReadOnly,PixelFormat.Format24bppRgb);try{stride=data.Stride;int count=stride*scaled.Height;if(current.Length!=count){current=new byte[count];previous=new byte[count];hasBase=false;}Marshal.Copy(data.Scan0,current,0,count);}finally{scaled.UnlockBits(data);}}
 public FrameUpdate? Encode(Bitmap source,long now,int latency=140,int mode=0){
  var settings=Profile(latency,mode);
  if(scaled==null||source.Size!=sourceSize||mode!=lastMode||(settings.key!=profile&&now-profileAt>5000)){
   double ratio=Math.Min(1,Math.Min((double)settings.width/source.Width,(double)(settings.width*9/16)/source.Height));Resize(new Size(Math.Max(1,(int)(source.Width*ratio)),Math.Max(1,(int)(source.Height*ratio))));sourceSize=source.Size;lastMode=mode;profile=settings.key;profileAt=now;
  }
  // Keep profile parameters stable during the cooldown as well as its dimensions.
  settings=profile switch{1=>Profile(0,1),2=>Profile(0,2),3=>Profile(900,0),4=>Profile(400,0),_=>Profile(100,0)};
  Draw(source);var dirty=new List<Rectangle>();
  if(hasBase)for(int y=0;y<scaled!.Height;y+=Tile)for(int x=0;x<scaled.Width;x+=Tile){var area=new Rectangle(x,y,Math.Min(Tile,scaled.Width-x),Math.Min(Tile,scaled.Height-y));bool changed=false;for(int row=y;row<area.Bottom&&!changed;row++){int offset=row*stride+x*3;changed=!current.AsSpan(offset,area.Width*3).SequenceEqual(previous.AsSpan(offset,area.Width*3));}if(changed)dirty.Add(area);}
  if(hasBase&&dirty.Count==0&&now-lastFull<10000)return null;
  int total=((scaled!.Width+Tile-1)/Tile)*((scaled.Height+Tile-1)/Tile);
  if(!hasBase||now-lastFull>=10000||dirty.Count>total*0.45)return Full(source,now,settings.quality,settings.budget);
  var tiles=new List<ImageTile>();int bytes=0;
  foreach(var area in dirty){using var crop=scaled.Clone(area,PixelFormat.Format24bppRgb);var jpeg=Desktop.Encode(crop,Tile,Tile,settings.quality,45000);bytes+=jpeg.Length;if(bytes>settings.budget)return Full(source,now,settings.quality,settings.budget);tiles.Add(new(area.X,area.Y,Convert.ToBase64String(jpeg)));}
  Array.Copy(current,previous,current.Length);return new(scaled.Width,scaled.Height,null,tiles.ToArray());
 }
 FrameUpdate Full(Bitmap source,long now,long quality,int budget){
  byte[] jpeg=Desktop.Encode(scaled!,scaled!.Width,scaled.Height,quality,budget);using var stream=new MemoryStream(jpeg);using var decoded=Image.FromStream(stream);
  if(decoded.Size!=scaled.Size){Resize(decoded.Size);Draw(source);}Array.Copy(current,previous,current.Length);hasBase=true;lastFull=now;return new(scaled.Width,scaled.Height,Convert.ToBase64String(jpeg),null);
 }
 public void Dispose(){painter?.Dispose();scaled?.Dispose();}
}
// The worker owns the composition surface; the UI receives an independent display bitmap.
sealed class FrameRenderer:IDisposable {
 readonly object gate=new();Bitmap? canvas;bool disposed;
 public Bitmap Apply(JsonElement packet){lock(gate){
  ObjectDisposedException.ThrowIf(disposed,this);
  string jpeg=packet.S("jpeg");
  if(jpeg!=""){using var bytes=new MemoryStream(Convert.FromBase64String(jpeg));using var image=Image.FromStream(bytes);if(image.Width>1920||image.Height>1080||image.Width<1||image.Height<1)throw new InvalidDataException("Дүрсний хэмжээ буруу");var next=new Bitmap(image);canvas?.Dispose();canvas=next;}
  else{
   if(canvas==null||packet.N("width")!=canvas.Width||packet.N("height")!=canvas.Height||!packet.TryGetProperty("tiles",out var tiles)||tiles.ValueKind!=JsonValueKind.Array||tiles.GetArrayLength()>144)throw new InvalidDataException("Дүрсний суурь тохирсонгүй");
   using var graphics=Graphics.FromImage(canvas);foreach(var tile in tiles.EnumerateArray()){
    int x=checked((int)tile.N("x")),y=checked((int)tile.N("y"));using var bytes=new MemoryStream(Convert.FromBase64String(tile.S("jpeg")));using var image=Image.FromStream(bytes);
    if(x<0||y<0||image.Width>128||image.Height>128||x+image.Width>canvas.Width||y+image.Height>canvas.Height)throw new InvalidDataException("Дүрсний хэсэг буруу");graphics.DrawImageUnscaled(image,x,y);
   }
  }
  return new Bitmap(canvas);
 }}
 public void Dispose(){lock(gate){disposed=true;canvas?.Dispose();canvas=null;}}
}
