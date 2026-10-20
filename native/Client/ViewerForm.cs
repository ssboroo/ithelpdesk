using System.Text.Json;
namespace RainyRemote;
sealed class RemoteScreen : PictureBox {
 public RemoteScreen(){SetStyle(ControlStyles.Selectable,true);TabStop=true;}
 protected override bool IsInputKey(Keys keyData)=>true;
}
sealed class ViewerForm : Form {
 readonly RemoteScreen screen=new(){Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(13,22,35),TabStop=true};
 readonly FrameRenderer renderer=new();readonly SemaphoreSlim renderOrder=new(1,1);bool resourcesDisposed;Form? expandedWindow;Rectangle normalBounds;FormWindowState normalState;
 readonly Func<object,Task> send;readonly Action stop;bool verified,control;Point? pendingPointer;readonly System.Windows.Forms.Timer pointerTimer=new(){Interval=25};bool sendingPointer;public bool InternalClose;
 readonly Label waiting=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(189,207,218),Font=new Font("Segoe UI",16),BackColor=Color.FromArgb(17,33,43)};
 readonly Label frameStatus=new(){Dock=DockStyle.Bottom,Height=32,ForeColor=Ui.Muted,BackColor=Color.White,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(16,0,0,0)};
 readonly Label modeLabel=Ui.Text("",10,false,Ui.Muted);readonly Button controlButton=Ui.Button("Удирдах эрх хүсэх");
 readonly Button confirm=Ui.Button("Баталгаажуулах");readonly System.Windows.Forms.Timer waitingTimer=new(){Interval=1000};bool peerReady;int frameCount;long started=Environment.TickCount64;
 public ViewerForm(string peer,string fingerprint,bool canControl,Func<object,Task> send,Action verify,Action stop){
  this.send=send;this.stop=stop;control=canControl;Name="ViewerWindow";
  Icon=Brand.AppIcon;Text="Алсын дэлгэц · "+peer;Width=1180;Height=800;MinimumSize=new Size(640,440);KeyPreview=true;Font=new Font("Segoe UI",10);
  var bar=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};
  var headingRow=new FlowLayoutPanel{AutoSize=false,WrapContents=true,BackColor=Color.White};headingRow.Controls.Add(Ui.Text("Алсын дэлгэц   /   "+peer,16,true));modeLabel.Text=control?"Гар, хулгана удирдах эрхтэй":"Зөвхөн дэлгэц харах эрхтэй";modeLabel.Margin=new Padding(24,6,0,0);headingRow.Controls.Add(modeLabel);bar.Controls.Add(headingRow);
  var actions=new FlowLayoutPanel{AutoSize=false,FlowDirection=FlowDirection.LeftToRight,WrapContents=true,BackColor=Color.White};
  var disconnect=Ui.Button("Холболт салгах");disconnect.Name="StopViewer";disconnect.Width=170;disconnect.Click+=(_,_)=>stop();actions.Controls.Add(disconnect);
  var full=Ui.Button("Бүтэн дэлгэц");full.Name="Fullscreen";full.Width=155;full.Click+=(_,_)=>{if(expandedWindow!=null){RestoreWindow();full.Text="Бүтэн дэлгэц";}else{expandedWindow=TopLevel?this:(Form)Parent!;normalBounds=expandedWindow.Bounds;normalState=expandedWindow.WindowState;expandedWindow.FormBorderStyle=FormBorderStyle.None;expandedWindow.WindowState=FormWindowState.Maximized;full.Text="Цонхонд буцаах";}};actions.Controls.Add(full);
  confirm.Width=190;confirm.Click+=(_,_)=>verify();
  controlButton.Name="RequestControl";controlButton.Width=220;controlButton.Enabled=!control;controlButton.Text=control?"Удирдах эрх идэвхтэй":"Удирдах эрх хүсэх";controlButton.Click+=async(_,_)=>{controlButton.Enabled=false;modeLabel.Text="Эзэмшигчийн зөвшөөрлийг хүлээж байна";await send(new{k="request_control"});};actions.Controls.Add(controlButton);
  var quality=new ComboBox{Name="StreamQuality",DropDownStyle=ComboBoxStyle.DropDownList,Width=210,Font=new Font("Segoe UI",11),Margin=new Padding(16,16,0,0)};quality.Items.AddRange(["Ухаалаг чанар","Өндөр чанар · 1080p","Хурд чухал"]);quality.SelectedIndex=0;quality.SelectedIndexChanged+=async(_,_)=>{if(verified)await send(new{k="quality",mode=quality.SelectedIndex});};actions.Controls.Add(quality);foreach(var button in actions.Controls.OfType<Button>())button.Tag=button.Width;bar.Controls.Add(actions);
  var content=new Panel{Dock=DockStyle.Fill,BackColor=screen.BackColor};content.Controls.Add(screen);content.Controls.Add(waiting);waiting.BringToFront();
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty,Padding=Padding.Empty};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,120));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
  bar.Margin=Padding.Empty;content.Margin=Padding.Empty;frameStatus.Margin=Padding.Empty;root.Controls.Add(bar,0,0);root.Controls.Add(content,0,1);root.Controls.Add(frameStatus,0,2);Controls.Add(root);
  static int FlowHeight(FlowLayoutPanel panel,int width){int used=0,line=0,total=0;foreach(Control child in panel.Controls){int w=child.Width+child.Margin.Horizontal,h=child.Height+child.Margin.Vertical;if(used>0&&used+w>width){total+=line;used=0;line=0;}used+=w;line=Math.Max(line,h);}return total+line;}
  bool fitting=false;void FitToolbar(){if(fitting)return;fitting=true;try{int available=Math.Max(120,ClientSize.Width-24);int head=FlowHeight(headingRow,available);headingRow.SetBounds(12,8,available,head);foreach(var button in actions.Controls.OfType<Button>()){button.Width=Math.Min((int)((button.Tag is int desired?desired:170)*DeviceDpi/96.0),available-8);button.Height=Math.Max(44,TextRenderer.MeasureText(button.Text,button.Font,new Size(Math.Max(50,button.Width-16),1000),TextFormatFlags.WordBreak).Height+16);}quality.Width=Math.Min((int)(210*DeviceDpi/96.0),available-24);int height=FlowHeight(actions,available);actions.SetBounds(12,head+12,available,height);root.RowStyles[0].Height=head+height+20;}finally{fitting=false;}}
  Resize+=(_,_)=>{if(IsHandleCreated)BeginInvoke((Action)(()=>{if(IsDisposed)return;FitToolbar();root.PerformLayout();}));};bar.Resize+=(_,_)=>FitToolbar();modeLabel.TextChanged+=(_,_)=>FitToolbar();FitToolbar();
  UpdateWaiting();waitingTimer.Tick+=(_,_)=>UpdateWaiting();waitingTimer.Start();
  // Keep the latest pointer position instead of queuing every MouseMove event.
  pointerTimer.Tick+=async(_,_)=>{if(sendingPointer||pendingPointer is not Point point||!verified||!control)return;pendingPointer=null;if(!Position(point,out var x,out var y))return;sendingPointer=true;try{await send(new{k="input",action="move",x,y});}finally{sendingPointer=false;}};pointerTimer.Start();
  screen.MouseMove+=(_,e)=>{if(verified&&control)pendingPointer=e.Location;};
  screen.MouseDown+=async(_,e)=>{if(!verified||!control||!Position(e.Location,out var x,out var y))return;screen.Focus();pendingPointer=null;await send(new{k="input",action="move",x,y});await send(new{k="input",action="button",button=ButtonIndex(e.Button),up=false});};
  screen.MouseUp+=async(_,e)=>{if(verified&&control)await send(new{k="input",action="button",button=ButtonIndex(e.Button),up=true});};
  screen.MouseWheel+=async(_,e)=>{if(verified&&control)await send(new{k="input",action="wheel",delta=e.Delta});};
  screen.KeyDown+=async(_,e)=>{if(!verified||!control||!screen.Focused)return;e.Handled=true;await send(new{k="input",action="key",key=(int)e.KeyCode,up=false});};
  screen.KeyUp+=async(_,e)=>{if(!verified||!control||!screen.Focused)return;e.Handled=true;await send(new{k="input",action="key",key=(int)e.KeyCode,up=true});};
  screen.LostFocus+=async(_,_)=>{pendingPointer=null;if(verified&&control)await send(new{k="input",action="release"});};
  Deactivate+=async(_,_)=>{if(verified&&control)await send(new{k="input",action="release"});};
  FormClosing+=(_,e)=>{if(!InternalClose){e.Cancel=true;BeginInvoke((Action)(()=>stop()));}};
  FormClosed+=(_,_)=>ReleaseResources();
 }
 internal void RestoreWindow(){if(expandedWindow==null)return;var window=expandedWindow;expandedWindow=null;if(window.IsDisposed||window.Disposing)return;window.FormBorderStyle=FormBorderStyle.Sizable;window.WindowState=FormWindowState.Normal;window.Bounds=normalBounds;window.WindowState=normalState;}
 void ReleaseResources(){if(resourcesDisposed)return;RestoreWindow();resourcesDisposed=true;pointerTimer.Stop();pointerTimer.Dispose();waitingTimer.Stop();waitingTimer.Dispose();screen.Image?.Dispose();screen.Image=null;renderer.Dispose();}
 protected override void Dispose(bool disposing){if(disposing)ReleaseResources();base.Dispose(disposing);}
 public void SetControl(bool granted){control=granted;controlButton.Enabled=!granted;controlButton.Text=granted?"Удирдах эрх идэвхтэй":"Удирдах эрх хүсэх";modeLabel.Text=granted?"Гар, хулгана удирдах эрхтэй":"Зөвхөн дэлгэц харах эрхтэй";if(granted)screen.Focus();}
 public void ControlDenied(){controlButton.Enabled=true;modeLabel.Text="Эзэмшигч зөвхөн харах эрхийг үлдээлээ.";}
 internal bool ControlAllowed=>control;
 public void InputDenied(){if(!control)return;control=false;controlButton.Enabled=false;controlButton.Text="Дахин холбогдоно уу";modeLabel.Text="Windows эрх хүрэлцэхгүй байна";frameStatus.Text="Эзэмшигч RAINY-г администратороор нээгээд дахин холбоно уу.";}
 public void MarkVerified(){verified=true;confirm.Enabled=false;confirm.Text="Код баталгаажсан";UpdateWaiting();}
 public void SetPeerReady(){peerReady=true;UpdateWaiting();}
 void UpdateWaiting(){
  if(frameCount>0)return;
  string title=!verified?"Холболтын кодоо баталгаажуулна уу":!peerReady?"Нөгөө талын баталгаажуулалтыг хүлээж байна":"Алсын дэлгэцийг бэлтгэж байна…";
  string hint=!verified?"Хоёр талын кодоо үндсэн цонхонд харьцуулна.":!peerReady?"Нөгөө компьютер дээр «Код ижил» товчийг дарна уу.":"Эхний зургийг хүлээж байна. Энэ цонх автоматаар шинэчлэгдэнэ.";
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
 static Bitmap Decode(byte[] jpeg){
  using var stream=new MemoryStream(jpeg);using var image=Image.FromStream(stream);
  if(image.Width>1920||image.Height>1080||image.Width<1||image.Height<1)throw new InvalidDataException("Дүрсний хэмжээ буруу");return new Bitmap(image);
 }
 public async Task FrameAsync(JsonElement packet){
  if(resourcesDisposed||IsDisposed)return;await renderOrder.WaitAsync();
  try{if(resourcesDisposed||IsDisposed)return;Bitmap bitmap;try{bitmap=await Task.Run(()=>renderer.Apply(packet));}catch(ObjectDisposedException)when(resourcesDisposed||IsDisposed){return;}if(resourcesDisposed||IsDisposed||Disposing){bitmap.Dispose();return;}Display(bitmap);}finally{renderOrder.Release();}
 }
 public void Frame(byte[] jpeg)=>Display(Decode(jpeg));
 void Display(Bitmap bitmap){var old=screen.Image;screen.Image=bitmap;old?.Dispose();frameCount++;waiting.Visible=false;frameStatus.Text=$"● Шууд дамжуулалт   ·   {bitmap.Width} × {bitmap.Height}   ·   Сүлжээнд автоматаар тохируулна";}
}
