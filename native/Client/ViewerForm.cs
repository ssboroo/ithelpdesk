using System.Text.Json;
namespace RainyRemote;
sealed class RemoteScreen : PictureBox {
 public RemoteScreen(){SetStyle(ControlStyles.Selectable,true);TabStop=true;}
 protected override bool IsInputKey(Keys keyData)=>true;
}
sealed class ViewerForm : Form {
 readonly RemoteScreen screen=new(){Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(13,22,35),TabStop=true};
 readonly Func<object,Task> send;readonly Action stop;bool verified,control;long moved;public bool InternalClose;
 readonly Label waiting=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(189,207,218),Font=new Font("Segoe UI",16),BackColor=Color.FromArgb(17,33,43)};
 readonly Label frameStatus=new(){Dock=DockStyle.Bottom,Height=32,ForeColor=Ui.Muted,BackColor=Color.White,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(16,0,0,0)};
 readonly Button confirm=Ui.Button("Баталгаажуулах");readonly System.Windows.Forms.Timer waitingTimer=new(){Interval=1000};bool peerReady;int frameCount;long started=Environment.TickCount64;
 public ViewerForm(string peer,string fingerprint,bool canControl,Func<object,Task> send,Action verify,Action stop){
  this.send=send;this.stop=stop;control=canControl;Name="ViewerWindow";
  Text="Алсын дэлгэц · "+peer;Width=1180;Height=800;MinimumSize=new Size(900,620);KeyPreview=true;Font=new Font("Segoe UI",10);
  var bar=new Panel{Dock=DockStyle.Top,Height=88,BackColor=Color.White,Padding=new Padding(18,12,18,10)};
  var heading=Ui.Text("Алсын дэлгэц   /   "+peer,16,true);heading.Location=new Point(18,12);bar.Controls.Add(heading);
  var details=Ui.Text(control?"Гар, хулгана удирдах эрхтэй":"Зөвхөн дэлгэц харах эрхтэй",10,false,Ui.Muted);details.Location=new Point(18,48);bar.Controls.Add(details);
  var actions=new FlowLayoutPanel{Dock=DockStyle.Right,Width=600,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,BackColor=Color.White};
  var disconnect=Ui.Button("Холболт салгах");disconnect.Name="StopViewer";disconnect.Width=170;disconnect.Click+=(_,_)=>stop();actions.Controls.Add(disconnect);
  var full=Ui.Button("Бүтэн дэлгэц");full.Width=155;full.Click+=(_,_)=>{bool expand=FormBorderStyle!=FormBorderStyle.None;FormBorderStyle=expand?FormBorderStyle.None:FormBorderStyle.Sizable;WindowState=expand?FormWindowState.Maximized:FormWindowState.Normal;full.Text=expand?"Цонхонд буцаах":"Бүтэн дэлгэц";};actions.Controls.Add(full);
  confirm.Width=220;confirm.Click+=(_,_)=>verify();actions.Controls.Add(confirm);bar.Controls.Add(actions);
  Controls.Add(screen);Controls.Add(waiting);Controls.Add(frameStatus);Controls.Add(bar);bar.BringToFront();waiting.BringToFront();
  UpdateWaiting();waitingTimer.Tick+=(_,_)=>UpdateWaiting();waitingTimer.Start();
  screen.MouseMove+=async(_,e)=>{if(!verified||!control||Environment.TickCount64-moved<35)return;moved=Environment.TickCount64;if(Position(e.Location,out var x,out var y))await send(new{k="input",action="move",x,y});};
  screen.MouseDown+=async(_,e)=>{if(!verified||!control||!Position(e.Location,out var x,out var y))return;screen.Focus();await send(new{k="input",action="move",x,y});await send(new{k="input",action="button",button=ButtonIndex(e.Button),up=false});};
  screen.MouseUp+=async(_,e)=>{if(verified&&control)await send(new{k="input",action="button",button=ButtonIndex(e.Button),up=true});};
  screen.MouseWheel+=async(_,e)=>{if(verified&&control)await send(new{k="input",action="wheel",delta=e.Delta});};
  KeyDown+=async(_,e)=>{if(!verified||!control||!screen.Focused)return;e.Handled=true;await send(new{k="input",action="key",key=(int)e.KeyCode,up=false});};
  KeyUp+=async(_,e)=>{if(!verified||!control||!screen.Focused)return;e.Handled=true;await send(new{k="input",action="key",key=(int)e.KeyCode,up=true});};
  screen.LostFocus+=async(_,_)=>{if(verified&&control)await send(new{k="input",action="release"});};
  Deactivate+=async(_,_)=>{if(verified&&control)await send(new{k="input",action="release"});};
  FormClosing+=(_,e)=>{if(!InternalClose){e.Cancel=true;BeginInvoke((Action)(()=>stop()));}};
  FormClosed+=(_,_)=>{waitingTimer.Stop();waitingTimer.Dispose();screen.Image?.Dispose();screen.Image=null;};
 }
 public void MarkVerified(){verified=true;confirm.Enabled=false;confirm.Text="Код баталгаажсан";UpdateWaiting();}
 public void SetPeerReady(){peerReady=true;UpdateWaiting();}
 void UpdateWaiting(){
  if(frameCount>0)return;
  string title=!verified?"Холболтын кодоо баталгаажуулна уу":!peerReady?"Нөгөө талын баталгаажуулалтыг хүлээж байна":"Алсын дэлгэцийг бэлтгэж байна…";
  string hint=!verified?"Баталгаажуулах цонхонд хоёр талын кодоо харьцуулна.":!peerReady?"Нөгөө компьютер дээр «Код ижил» товчийг дарна уу.":"Эхний зургийг хүлээж байна. Энэ цонх автоматаар шинэчлэгдэнэ.";
  if(verified&&peerReady&&Environment.TickCount64-started>15000){title="Дэлгэцийн зураг хараахан ирээгүй байна";hint="Нөгөө талын Windows түгжээг тайлж, дэлгэцээ хуваалцсан эсэхийг шалгана уу.";}
  waiting.Text=title+"\n\n"+hint;frameStatus.Text=title;
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
  var bitmap=new Bitmap(image);var old=screen.Image;screen.Image=bitmap;old?.Dispose();frameCount++;waiting.Visible=false;frameStatus.Text=$"● Дэлгэц дамжиж байна   ·   {image.Width} × {image.Height}   ·   {frameCount} зураг хүлээн авсан";
 }
}
