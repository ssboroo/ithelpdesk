using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Security.Cryptography;
namespace RainyRemote;
static class Desktop {
 public const int FrameBudget=450000;
 static readonly ImageCodecInfo Jpeg=ImageCodecInfo.GetImageEncoders().First(c=>c.MimeType=="image/jpeg");
 public static byte[] Capture(){
  var bounds=Screen.PrimaryScreen?.Bounds??throw new InvalidOperationException("Дэлгэц олдсонгүй. Windows түгжээгээ тайлна уу.");
  using var original=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format24bppRgb);
  using(var g=Graphics.FromImage(original))g.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size);
  return Encode(original);
 }
 internal static byte[] Encode(Bitmap original,int maxWidth=1920,int maxHeight=1080,long quality=78,int budget=FrameBudget){
  double scale=Math.Min(1,Math.Min((double)maxWidth/original.Width,(double)maxHeight/original.Height));
  var codec=Jpeg;
  for(int attempt=0;attempt<7;attempt++){
   using var image=new Bitmap(Math.Max(1,(int)(original.Width*scale)),Math.Max(1,(int)(original.Height*scale)));
   using(var g=Graphics.FromImage(image)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.Bilinear;g.DrawImage(original,0,0,image.Width,image.Height);}
   using var output=new MemoryStream();using var options=new EncoderParameters(1);options.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,Math.Max(25L,quality-attempt*9));image.Save(output,codec,options);
   if(output.Length<=budget)return output.ToArray();scale*=0.8;
  }
  throw new InvalidOperationException("Дэлгэцийн зургийг бэлтгэж чадсангүй.");
 }

}
sealed class CaptureSession : IDisposable {
 Bitmap? surface;Graphics? graphics;readonly FrameEncoder encoder=new();
 public FrameUpdate? Next(int latency=140,int mode=0){
  var bounds=Screen.PrimaryScreen?.Bounds??throw new InvalidOperationException("Дэлгэц олдсонгүй.");
  if(surface is null||surface.Size!=bounds.Size){graphics?.Dispose();surface?.Dispose();surface=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format24bppRgb);graphics=Graphics.FromImage(surface);}
  graphics!.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size);DesktopCursor.Draw(graphics,bounds.Location);return encoder.Encode(surface,Environment.TickCount64,latency,mode);
 }
 public void Dispose(){graphics?.Dispose();surface?.Dispose();encoder.Dispose();}
}
static class DesktopCursor {
 [StructLayout(LayoutKind.Sequential)]struct CURSORINFO{public int size,flags;public IntPtr cursor;public Point position;}
 [StructLayout(LayoutKind.Sequential)]struct ICONINFO{public bool icon;public int x,y;public IntPtr mask,color;}
 [DllImport("user32.dll")]static extern bool GetCursorInfo(ref CURSORINFO info);
 [DllImport("user32.dll")]static extern bool GetIconInfo(IntPtr icon,out ICONINFO info);
 [DllImport("user32.dll")]static extern bool DrawIconEx(IntPtr dc,int x,int y,IntPtr icon,int width,int height,int step,IntPtr brush,int flags);
 [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr value);
 public static void Draw(Graphics graphics,Point origin){
  var info=new CURSORINFO{size=Marshal.SizeOf<CURSORINFO>()};if(!GetCursorInfo(ref info)||(info.flags&1)==0||!GetIconInfo(info.cursor,out var icon))return;
  try{var dc=graphics.GetHdc();try{DrawIconEx(dc,info.position.X-origin.X-icon.x,info.position.Y-origin.Y-icon.y,info.cursor,0,0,0,IntPtr.Zero,3);}finally{graphics.ReleaseHdc(dc);}}finally{if(icon.mask!=IntPtr.Zero)DeleteObject(icon.mask);if(icon.color!=IntPtr.Zero)DeleteObject(icon.color);}
 }
}
sealed class RemoteInput {
 [StructLayout(LayoutKind.Sequential)]struct INPUT{public uint type;public Union data;}
 [StructLayout(LayoutKind.Explicit)]struct Union{[FieldOffset(0)]public MOUSE mouse;[FieldOffset(0)]public KEY key;}
 [StructLayout(LayoutKind.Sequential)]struct MOUSE{public int x,y;public uint mouseData,flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)]struct KEY{public ushort vk,scan;public uint flags,time;public UIntPtr extra;}
 [DllImport("user32.dll",SetLastError=true)]static extern uint SendInput(uint count,INPUT[] inputs,int size);
 [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
 readonly HashSet<ushort> keys=[];readonly HashSet<int> buttons=[];
 static void Key(ushort key,bool up){var input=new INPUT{type=1,data=new Union{key=new KEY{vk=key,flags=up?2u:0u}}};if(SendInput(1,[input],Marshal.SizeOf<INPUT>())!=1)throw new InvalidOperationException("Windows удирдлагыг хориглосон");}
 static void Mouse(uint flags,uint data=0){var input=new INPUT{type=0,data=new Union{mouse=new MOUSE{flags=flags,mouseData=data}}};if(SendInput(1,[input],Marshal.SizeOf<INPUT>())!=1)throw new InvalidOperationException("Windows удирдлагыг хориглосон");}
 static uint ButtonFlag(int button,bool up)=>button switch{0=>up?4u:2u,1=>up?16u:8u,_=>up?64u:32u};
 public void Apply(JsonElement m){
  switch(m.S("action")){
   case "move":
    double x=m.D("x"),y=m.D("y");if(!double.IsFinite(x)||!double.IsFinite(y)||x<0||x>1||y<0||y>1)return;
    var bounds=Screen.PrimaryScreen!.Bounds;if(!SetCursorPos(bounds.Left+(int)(x*(bounds.Width-1)),bounds.Top+(int)(y*(bounds.Height-1))))throw new InvalidOperationException("Windows хулганы удирдлагыг хориглосон");break;
   case "button":int b=(int)m.N("button");if(b<0||b>2)return;bool up=m.B("up");if(up){if(buttons.Remove(b))Mouse(ButtonFlag(b,true));}else if(buttons.Add(b))Mouse(ButtonFlag(b,false));break;
   case "key":long value=m.N("key");if(value<8||value>254)return;ushort vk=(ushort)value;bool released=m.B("up");if(released){if(keys.Remove(vk))Key(vk,true);}else{keys.Add(vk);Key(vk,false);}break;
   case "wheel":int delta=(int)Math.Clamp(m.N("delta"),-1200,1200);Mouse(0x800,unchecked((uint)delta));break;
   case "release":Release();break;
  }
 }
 public void Release(){foreach(var k in keys)try{Key(k,true);}catch(InvalidOperationException){}keys.Clear();foreach(var b in buttons)try{Mouse(ButtonFlag(b,true));}catch(InvalidOperationException){}buttons.Clear();}
}
