namespace RainyRemote;
sealed partial class MainForm {
 readonly Label connectionBadge=Ui.Text("● Холбогдож байна",10,true,Ui.Accent);
 void InitializeInterface(){
  Icon=Brand.AppIcon;Text="RAINY · Алсын тусламж";Name="MainWindow";ClientSize=new Size(1060,730);MinimumSize=new Size(980,710);Font=new Font("Segoe UI",11);BackColor=Ui.Canvas;StartPosition=FormStartPosition.CenterScreen;
  var nav=new Panel{Dock=DockStyle.Left,Width=190,BackColor=Color.FromArgb(20,43,51),Padding=new Padding(22,28,18,20)};
  var brand=new BrandMark{Location=new Point(22,26)};nav.Controls.Add(brand);
  var side=new Label{Text="АЛСЫН ТУСЛАМЖ\n\n\n↗   Холболт\n\n\nКомпьютер хооронд\nаюулгүй тусламж",ForeColor=Color.FromArgb(181,205,207),Location=new Point(22,132),Size=new Size(148,250),Font=new Font("Segoe UI",10)};nav.Controls.Add(side);
  var settingsButton=Ui.Button("Тохиргоо");settingsButton.Dock=DockStyle.Bottom;settingsButton.Click+=(_,_)=>OpenSettings();nav.Controls.Add(settingsButton);Controls.Add(nav);
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=4,Padding=new Padding(28,24,28,20),BackColor=Ui.Canvas};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,90));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,340));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,130));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
  var header=new Panel{Dock=DockStyle.Fill};var title=Ui.Text("Алсын тусламж",24,true);title.Location=new Point(0,0);header.Controls.Add(title);connectionBadge.Location=new Point(0,50);header.Controls.Add(connectionBadge);layout.Controls.Add(header,0,0);layout.SetColumnSpan(header,2);
  var local=Ui.Stack();local.Margin=new Padding(0,0,10,0);local.Controls.Add(Ui.Text("Энэ компьютер",15,true));local.Controls.Add(Ui.Text("Тусламж авахдаа дугаар, кодоо өгнө үү.",10,false,Ui.Muted));
  myId.Text="— — —";myId.Font=new Font("Segoe UI",29,FontStyle.Bold);myId.ForeColor=Ui.Ink;myId.AutoSize=true;myId.Margin=new Padding(0,12,0,15);local.Controls.Add(myId);
  local.Controls.Add(Ui.Text("Нэг удаагийн код",10,false,Ui.Muted));myPin.Text="— — —";myPin.Font=new Font("Consolas",22,FontStyle.Bold);myPin.AutoSize=true;myPin.ForeColor=Ui.Accent;myPin.Margin=new Padding(0,0,0,14);local.Controls.Add(myPin);
  var copy=Ui.Button("Дугаар, код хуулах");copy.Width=300;copy.Click+=(_,_)=>{if(DeviceId!=""&&PairCode!=""){Clipboard.SetText($"RAINY · Төхөөрөмжийн дугаар: {DeviceId}\nНэг удаагийн код: {PairCode}");status.Text="Дугаар, код хуулагдлаа.";}};local.Controls.Add(copy);layout.Controls.Add(local,0,1);
  var remote=Ui.Stack();remote.Margin=new Padding(10,0,0,0);remote.Controls.Add(Ui.Text("Бусдад туслах",15,true));remote.Controls.Add(Ui.Text("Нөгөө талын дугаар, кодыг оруулна уу.",10,false,Ui.Muted));
  Ui.Field(target,"9 оронтой дугаар",300);target.Name="TargetId";remote.Controls.Add(target);remote.Controls.Add(Ui.Text("Нэг удаагийн код",10,false,Ui.Muted));Ui.Field(pin,"6 оронтой код",300);pin.Name="TargetPin";pin.UseSystemPasswordChar=true;remote.Controls.Add(pin);
  request.Name="RequestConnection";request.Text="Холбогдох  →";Ui.Style(request,true);request.Width=300;request.Click+=async(_,_)=>await Invite();remote.Controls.Add(request);layout.Controls.Add(remote,1,1);
  var progress=Ui.Stack(18);progress.BackColor=Ui.Soft;progress.Margin=new Padding(0,18,0,0);session.Text="Холболт нээгдээгүй";session.AutoSize=true;session.Font=new Font("Segoe UI",12,FontStyle.Bold);session.ForeColor=Ui.Ink;progress.Controls.Add(session);
  var actions=new FlowLayoutPanel{Width=720,Height=52};verify.Text="Кодоо баталгаажуулах";Ui.Style(verify);verify.Width=245;verify.Enabled=false;verify.Click+=async(_,_)=>{if(crypto!=null){using var dialog=new VerificationDialog(crypto.Fingerprint,role=="host");if(await ShowInline(dialog)==DialogResult.OK)await Verify();}};
  end.Text="Холболт салгах";end.Name="StopSession";Ui.Style(end);end.Width=200;end.Enabled=false;end.Click+=(_,_)=>Disconnect();actions.Controls.Add(verify);actions.Controls.Add(end);progress.Controls.Add(actions);layout.Controls.Add(progress,0,2);layout.SetColumnSpan(progress,2);
  status.Text="Холболтыг бэлдэж байна…";status.ForeColor=Ui.Muted;status.Dock=DockStyle.Fill;status.Padding=new Padding(0,14,0,0);status.Font=new Font("Segoe UI",10);layout.Controls.Add(status,0,3);layout.SetColumnSpan(status,2);Controls.Add(layout);layout.BringToFront();
  server.Text=BuildConfig.DefaultRelay;try{if(File.Exists(settings))server.Text=File.ReadAllText(settings);}catch(IOException){}
  connect.Text="Дахин холбогдох";Ui.Style(connect);connect.Width=220;connect.Click+=async(_,_)=>await Connect();
  Shown+=async(_,_)=>await Connect();FormClosing+=(_,_)=>{Disconnect();net?.Dispose();};
 }
 async void OpenSettings(){
  using var dialog=new Form{Text="Холболтын тохиргоо",ClientSize=new Size(620,300),BackColor=Color.White,StartPosition=FormStartPosition.CenterParent,Font=Font,MaximizeBox=false,MinimizeBox=false};
  var body=Ui.Stack();body.Controls.Add(Ui.Text("Серверийн тохиргоо",19,true));body.Controls.Add(Ui.Text("Ердийн хэрэглээнд өөрчлөх шаардлагагүй.",10,false,Ui.Muted));server.Width=550;body.Controls.Add(server);body.Controls.Add(connect);
  body.Controls.Add(new Label{Text="Холбогдох үед кодоо хоёр тал харьцуулж батална.\nДэлгэц хуваалцах эрхийг зөвхөн эзэмшигч олгоно.",AutoSize=true,ForeColor=Ui.Muted,Margin=new Padding(0,16,0,0)});var back=Ui.Button("Буцах");back.DialogResult=DialogResult.Cancel;body.Controls.Add(back);dialog.Controls.Add(body);await ShowInline(dialog);body.Controls.Remove(server);body.Controls.Remove(connect);
 }
}
