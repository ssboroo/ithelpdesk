using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace RainyRemote;
static class Desktop {
 public const int FrameBudget=450000;
 public static byte[] Capture(){
  var bounds=Screen.PrimaryScreen?.Bounds??throw new InvalidOperationException("Дэлгэц олдсонгүй. Windows түгжээгээ тайлна уу.");
  using var original=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format24bppRgb);
  using(var g=Graphics.FromImage(original))g.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size);
  return Encode(original);
 }
 internal static byte[] Encode(Bitmap original){
  double scale=Math.Min(1,Math.Min(1280.0/original.Width,720.0/original.Height));
  var codec=ImageCodecInfo.GetImageEncoders().First(c=>c.MimeType=="image/jpeg");
  for(int attempt=0;attempt<7;attempt++){
   using var image=new Bitmap(Math.Max(1,(int)(original.Width*scale)),Math.Max(1,(int)(original.Height*scale)));
   using(var g=Graphics.FromImage(image)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.Bilinear;g.DrawImage(original,0,0,image.Width,image.Height);}
   using var output=new MemoryStream();using var options=new EncoderParameters(1);options.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,Math.Max(25L,50L-attempt*5));image.Save(output,codec,options);
   if(output.Length<=FrameBudget)return output.ToArray();scale*=0.8;
  }
  throw new InvalidOperationException("Дэлгэцийн зургийг бэлтгэж чадсангүй.");
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
 static void Key(ushort key,bool up){var input=new INPUT{type=1,data=new Union{key=new KEY{vk=key,flags=up?2u:0u}}};SendInput(1,[input],Marshal.SizeOf<INPUT>());}
 static void Mouse(uint flags,uint data=0){var input=new INPUT{type=0,data=new Union{mouse=new MOUSE{flags=flags,mouseData=data}}};SendInput(1,[input],Marshal.SizeOf<INPUT>());}
 static uint ButtonFlag(int button,bool up)=>button switch{0=>up?4u:2u,1=>up?16u:8u,_=>up?64u:32u};
 public void Apply(JsonElement m){
  switch(m.S("action")){
   case "move":
    double x=m.D("x"),y=m.D("y");if(!double.IsFinite(x)||!double.IsFinite(y)||x<0||x>1||y<0||y>1)return;
    var bounds=Screen.PrimaryScreen!.Bounds;SetCursorPos(bounds.Left+(int)(x*(bounds.Width-1)),bounds.Top+(int)(y*(bounds.Height-1)));break;
   case "button":int b=(int)m.N("button");if(b<0||b>2)return;bool up=m.B("up");if(up){if(buttons.Remove(b))Mouse(ButtonFlag(b,true));}else if(buttons.Add(b))Mouse(ButtonFlag(b,false));break;
   case "key":long value=m.N("key");if(value<8||value>254)return;ushort vk=(ushort)value;bool released=m.B("up");if(released){if(keys.Remove(vk))Key(vk,true);}else if(keys.Add(vk))Key(vk,false);break;
   case "wheel":int delta=(int)Math.Clamp(m.N("delta"),-1200,1200);Mouse(0x800,unchecked((uint)delta));break;
   case "release":Release();break;
  }
 }
 public void Release(){foreach(var k in keys)Key(k,true);keys.Clear();foreach(var b in buttons)Mouse(ButtonFlag(b,true));buttons.Clear();}
}
