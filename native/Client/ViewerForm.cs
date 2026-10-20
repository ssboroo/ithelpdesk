using System.Text.Json;
namespace RainyRemote;
sealed class RemoteScreen : PictureBox {
 public RemoteScreen(){SetStyle(ControlStyles.Selectable,true);TabStop=true;}
 protected override bool IsInputKey(Keys keyData)=>true;
}
sealed class ViewerForm : Form {
 readonly RemoteScreen screen=new(){Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(13,22,35),TabStop=true};
 readonly Func<object,Task> send;readonly Action stop;bool verified,control;long moved;public bool InternalClose;
 public ViewerForm(string peer,string fingerprint,bool canControl,Func<object,Task> send,Action verify,Action stop){
  this.send=send;this.stop=stop;control=canControl;
  Text="RAINY Remote · "+peer;Width=1200;Height=780;MinimumSize=new Size(700,500);KeyPreview=true;
  var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=65,Padding=new Padding(10),BackColor=Color.FromArgb(231,239,252)};
  bar.Controls.Add(new Label{Text=$"Код: {fingerprint}  ·  {(control?"Гар / хулгана зөвшөөрсөн":"Зөвхөн харах")}",AutoSize=true,Margin=new Padding(5,12,15,0)});
  var confirm=new Button{Text="Код таарлаа",AutoSize=true,Height=38};
  confirm.Click+=(_,_)=>{verified=true;confirm.Enabled=false;verify();};bar.Controls.Add(confirm);
  var disconnect=new Button{Text="Холболт салгах",AutoSize=true,Height=38,BackColor=Color.MistyRose};disconnect.Click+=(_,_)=>stop();bar.Controls.Add(disconnect);
  Controls.Add(screen);Controls.Add(bar);
  screen.MouseMove+=async(_,e)=>{if(!verified||!control||Environment.TickCount64-moved<35)return;moved=Environment.TickCount64;if(Position(e.Location,out var x,out var y))await send(new{k="input",action="move",x,y});};
  screen.MouseDown+=async(_,e)=>{if(!verified||!control||!Position(e.Location,out var x,out var y))return;screen.Focus();await send(new{k="input",action="move",x,y});await send(new{k="input",action="button",button=ButtonIndex(e.Button),up=false});};
  screen.MouseUp+=async(_,e)=>{if(verified&&control)await send(new{k="input",action="button",button=ButtonIndex(e.Button),up=true});};
  screen.MouseWheel+=async(_,e)=>{if(verified&&control)await send(new{k="input",action="wheel",delta=e.Delta});};
  KeyDown+=async(_,e)=>{if(!verified||!control||!screen.Focused)return;e.Handled=true;await send(new{k="input",action="key",key=(int)e.KeyCode,up=false});};
  KeyUp+=async(_,e)=>{if(!verified||!control||!screen.Focused)return;e.Handled=true;await send(new{k="input",action="key",key=(int)e.KeyCode,up=true});};
  screen.LostFocus+=async(_,_)=>{if(verified&&control)await send(new{k="input",action="release"});};
  Deactivate+=async(_,_)=>{if(verified&&control)await send(new{k="input",action="release"});};
  FormClosing+=(_,e)=>{if(!InternalClose){e.Cancel=true;BeginInvoke((Action)(()=>stop()));}};
  FormClosed+=(_,_)=>{screen.Image?.Dispose();screen.Image=null;};
 }
 static int ButtonIndex(MouseButtons b)=>b==MouseButtons.Right?1:b==MouseButtons.Middle?2:0;
 bool Position(Point p,out double x,out double y){
  x=y=0;if(screen.Image==null)return false;
  double scale=Math.Min((double)screen.ClientSize.Width/screen.Image.Width,(double)screen.ClientSize.Height/screen.Image.Height);
  double w=screen.Image.Width*scale,h=screen.Image.Height*scale,left=(screen.Width-w)/2,top=(screen.Height-h)/2;
  x=(p.X-left)/w;y=(p.Y-top)/h;return x>=0&&x<=1&&y>=0&&y<=1;
 }
 public void Frame(byte[] jpeg){
  using var stream=new MemoryStream(jpeg);using var image=Image.FromStream(stream);
  if(image.Width>1280||image.Height>720||image.Width<1||image.Height<1)throw new InvalidDataException("Дүрсний хэмжээ буруу");
  var bitmap=new Bitmap(image);var old=screen.Image;screen.Image=bitmap;old?.Dispose();
 }
}
