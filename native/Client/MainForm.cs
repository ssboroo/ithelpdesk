using System.Text.Json;
namespace RainyRemote;
sealed class MainForm : Form {
 readonly TextBox server=new(),target=new(),pin=new();readonly Label myId=new(),myPin=new(),status=new(),session=new();
 readonly Button connect=new(),request=new(),verify=new(),end=new();readonly RemoteInput input=new();readonly SemaphoreSlim encryptedSender=new(1,1);
 Transport? net;SessionCrypto? crypto;string sid="",role="";bool canControl,localVerified,remoteVerified,pending;CancellationTokenSource? capture;
 ViewerForm? viewer;Form? consent;Form? banner;bool closingBanner;
 readonly string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RainyRemote3","server.txt");
 public MainForm(){
  Text="RAINY Remote · Бие даасан алсын удирдлага";Width=1000;Height=750;MinimumSize=new Size(920,690);Font=new Font("Segoe UI",11);BackColor=Color.FromArgb(239,243,249);
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=5,Padding=new Padding(25)};
  layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,75));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,85));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,265));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,130));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
  var title=new Label{Text="↗  RAINY REMOTE",Font=new Font("Segoe UI",25,FontStyle.Bold),ForeColor=Color.FromArgb(24,53,87),Dock=DockStyle.Fill};layout.Controls.Add(title,0,0);layout.SetColumnSpan(title,2);
  var network=new FlowLayoutPanel{Dock=DockStyle.Fill};server.Width=580;server.Text=BuildConfig.DefaultRelay;
  try{if(File.Exists(settings))server.Text=File.ReadAllText(settings);}catch(IOException){}
  connect.Text="Серверт холбогдох";connect.AutoSize=true;connect.Click+=async(_,_)=>await Connect();network.Controls.Add(server);network.Controls.Add(connect);layout.Controls.Add(network,0,1);layout.SetColumnSpan(network,2);
  var local=Card("ЭНЭ КОМПЬЮТЕР");myId.Text="ID хүлээж байна…";myId.Font=new Font("Segoe UI",25,FontStyle.Bold);myId.Height=55;myId.Width=400;local.Controls.Add(myId);
  myPin.Text="Нэг удаагийн код: —";myPin.Height=35;myPin.Width=400;local.Controls.Add(myPin);
  var copy=new Button{Text="ID, код хуулах",AutoSize=true};copy.Click+=(_,_)=>{if(myId.Tag is string id&&myPin.Tag is string code)Clipboard.SetText($"RAINY Remote ID: {id}\nНэг удаагийн код: {code}");};local.Controls.Add(copy);
  local.Controls.Add(new Label{Text="ID, кодоо зөвхөн холбогдох хүндээ өгнө.\nХүсэлт ирэхэд зөвшөөрөх эсэхээ сонгоно.",Width=400,Height=65,Margin=new Padding(0,15,0,0)});layout.Controls.Add(local,0,2);
  var remote=Card("БУСАД КОМПЬЮТЕРТ ХОЛБОГДОХ");remote.Controls.Add(new Label{Text="Нөгөө төхөөрөмжийн ID",AutoSize=true});target.Width=380;target.PlaceholderText="9 оронтой ID";remote.Controls.Add(target);
  remote.Controls.Add(new Label{Text="Нэг удаагийн код",AutoSize=true,Margin=new Padding(0,8,0,0)});pin.Width=380;pin.PlaceholderText="6 оронтой код";pin.UseSystemPasswordChar=true;remote.Controls.Add(pin);
  request.Text="Холбогдох →";request.Width=380;request.Height=43;request.BackColor=Color.FromArgb(35,103,220);request.ForeColor=Color.White;request.Click+=async(_,_)=>await Invite();remote.Controls.Add(request);layout.Controls.Add(remote,1,2);
  var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(5,12,5,5)};
  session.Text="Холболт нээгдээгүй";session.AutoSize=true;actions.Controls.Add(session);
  var row=new FlowLayoutPanel{Width=850,Height=50};verify.Text="Код таарлаа — Хуваалцах";verify.AutoSize=true;verify.Enabled=false;verify.Click+=async(_,_)=>await Verify();end.Text="Холболт салгах / цуцлах";end.AutoSize=true;end.BackColor=Color.MistyRose;end.Enabled=false;end.Click+=(_,_)=>Disconnect();row.Controls.Add(verify);row.Controls.Add(end);actions.Controls.Add(row);layout.Controls.Add(actions,0,3);layout.SetColumnSpan(actions,2);
  status.Text="Эхлээд сервертэй холбогдоно.";status.Dock=DockStyle.Fill;status.ForeColor=Color.FromArgb(81,103,129);layout.Controls.Add(status,0,4);layout.SetColumnSpan(status,2);Controls.Add(layout);
  Shown+=async(_,_)=>await Connect();FormClosing+=(_,_)=>{Disconnect();net?.Dispose();};
 }
 static FlowLayoutPanel Card(string heading){var c=new FlowLayoutPanel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(20),Margin=new Padding(4),FlowDirection=FlowDirection.TopDown,WrapContents=false};c.Controls.Add(new Label{Text=heading,AutoSize=true,Font=new Font("Segoe UI",10,FontStyle.Bold),Margin=new Padding(0,0,0,15)});return c;}
 void UI(Action action){if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke((Action)(()=>{if(!IsDisposed)action();}));}catch(InvalidOperationException){}}
 async Task Connect(){
  if(crypto!=null||pending){status.Text="Эхлээд одоогийн холболтоо салгана уу.";return;}
  connect.Enabled=false;net?.Dispose();net=new Transport();var connection=net;
  connection.Message+=m=>UI(()=>{if(net==connection)Handle(m);});connection.Closed+=s=>UI(()=>{if(net!=connection)return;StopSession();myId.Text="Офлайн";myId.Tag=null;myPin.Text="Код: —";myPin.Tag=null;status.Text=s+" · Серверт дахин холбогдоно уу.";connect.Enabled=true;});
  try{status.Text="Сервертэй холбогдож байна…";await connection.Connect(server.Text.Trim());Directory.CreateDirectory(Path.GetDirectoryName(settings)!);File.WriteAllText(settings,server.Text.Trim());}
  catch(Exception e){status.Text="Холбогдсонгүй: "+e.Message;connection.Dispose();}finally{connect.Enabled=true;}
 }
 async Task Invite(){
  try{
   if(net?.IsOpen!=true)throw new InvalidOperationException("Эхлээд серверт холбогдоно уу.");
   if(crypto!=null||pending)throw new InvalidOperationException("Өөр холболт нээлттэй байна.");
   string id=target.Text.Replace(" ","").Trim(),code=pin.Text.Trim();if(!System.Text.RegularExpressions.Regex.IsMatch(id,"^[0-9]{9}$")||!System.Text.RegularExpressions.Regex.IsMatch(code,"^[0-9]{6}$"))throw new ArgumentException("ID 9 цифр, код 6 цифр байна.");
   crypto=new SessionCrypto();pending=true;end.Enabled=true;request.Enabled=false;await net.Send(new{t="invite",target=id,pin=code,key=crypto.PublicKey});pin.Clear();status.Text="Зөвшөөрөл хүлээж байна…";
  }catch(Exception e){StopSession();status.Text=e.Message;}
 }
 async void Handle(JsonElement m){
  try{
   switch(m.S("t")){
    case "registered":myId.Text=m.S("id");myId.Tag=m.S("id");myPin.Text="Нэг удаагийн код: "+m.S("pin");myPin.Tag=m.S("pin");status.Text="Бэлэн · Дэлгэц дамжуулаагүй. Холбогдох хүний хүсэлтийг зөвшөөрсний дараа хуваалцана.";break;
    case "waiting":status.Text=m.S("message");break;
    case "invite":
     if(crypto!=null||pending){await net!.Send(new{t="reject",id=m.S("id")});return;}
     string requestId=m.S("id");consent=new Form{Text="RAINY Remote · Холболтын зөвшөөрөл",Width=550,Height=270,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
     var label=new Label{Text=$"{m.S("from")} төхөөрөмж таны дэлгэцийг харах хүсэлт илгээлээ.\nЗөвшөөрсний дараа хоёр талын баталгаажуулах кодыг харьцуулна.",Dock=DockStyle.Top,Height=85,Padding=new Padding(15),Font=Font};
     var control=new CheckBox{Text="Хулгана, гар удирдахыг мөн зөвшөөрөх",Dock=DockStyle.Top,Height=45,Padding=new Padding(15),Font=Font};
     var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=60,Padding=new Padding(15)};var allow=new Button{Text="Зөвшөөрөх",DialogResult=DialogResult.OK,AutoSize=true};var deny=new Button{Text="Татгалзах",DialogResult=DialogResult.Cancel,AutoSize=true};buttons.Controls.Add(allow);buttons.Controls.Add(deny);consent.Controls.Add(control);consent.Controls.Add(label);consent.Controls.Add(buttons);
     var choice=consent.ShowDialog(this);bool accepted=choice==DialogResult.OK;bool granted=control.Checked;consent.Dispose();consent=null;
     if(accepted){crypto=new SessionCrypto();pending=true;end.Enabled=true;request.Enabled=false;await net!.Send(new{t="accept",id=requestId,key=crypto.PublicKey,control=granted});}else await net!.Send(new{t="reject",id=requestId});break;
    case "accepted":
     if(crypto==null){await net!.Send(new{t="end"});return;}
     crypto.Agree(m.S("key"));sid=m.S("sid");role=m.S("role");canControl=m.B("control");pending=false;localVerified=remoteVerified=false;end.Enabled=true;
     session.Text=$"{m.S("peer")} · Баталгаажуулах код: {crypto.Fingerprint}";status.Text="Хоёр талын код ИЖИЛ эсэхийг утсаар харьцуулна. Дараа нь код таарсныг хоёр тал батална.";
     if(role=="host"){verify.Enabled=true;Text="RAINY Remote · Хуваалцах зөвшөөрөл хүлээж байна";}
     else if(role=="viewer"){viewer=new ViewerForm(m.S("peer"),crypto.Fingerprint,canControl,SendProtected,()=>_=Verify(),Disconnect);viewer.Show(this);}else Disconnect();break;
    case "data":
     if(crypto==null||m.S("sid")!=sid)return;var data=crypto.Open(sid,m);
     if(data.S("k")=="ready"){remoteVerified=true;MaybeCapture();}
     else if(data.S("k")=="frame"&&role=="viewer"&&localVerified&&remoteVerified){viewer?.Frame(Convert.FromBase64String(data.S("jpeg")));status.Text="Алсын дэлгэц дамжиж байна";}
     else if(data.S("k")=="input"&&role=="host"&&canControl&&localVerified&&remoteVerified)input.Apply(data);break;
    case "ended":case "cancelled":consent?.Close();StopSession();status.Text=m.S("message");break;
    case "error":status.Text=m.S("message");if(pending)StopSession();break;
   }
  }catch(Exception e){Disconnect();status.Text="Холболт зогслоо: "+e.Message;}
 }
 async Task Verify(){localVerified=true;verify.Enabled=false;await SendProtected(new{k="ready"});MaybeCapture();}
 void MaybeCapture(){
  if(role!="host"||!localVerified||!remoteVerified||capture!=null||crypto==null)return;
  capture=new CancellationTokenSource();var token=capture.Token;Text="● RAINY Remote · ДЭЛГЭЦ ХУВААЛЦАЖ БАЙНА";status.Text=canControl?"Дэлгэц, гар, хулгана хуваалцаж байна. Салгах товчоор хүссэн үедээ зогсооно.":"Дэлгэцийг зөвхөн харах эрхээр хуваалцаж байна.";
  banner=new Form{Text="Дэлгэц хуваалцаж байна",Width=500,Height=75,TopMost=true,FormBorderStyle=FormBorderStyle.FixedToolWindow,StartPosition=FormStartPosition.Manual,Location=new Point(20,10)};
  var stopButton=new Button{Text="● RAINY Remote · Хуваалцаж байна — САЛГАХ",Dock=DockStyle.Fill,BackColor=Color.Firebrick,ForeColor=Color.White};stopButton.Click+=(_,_)=>Disconnect();banner.Controls.Add(stopButton);banner.FormClosing+=(_,e)=>{if(!closingBanner){e.Cancel=true;BeginInvoke((Action)(()=>Disconnect()));}};banner.Show();
  _=Task.Run(async()=>{try{while(!token.IsCancellationRequested){byte[] jpeg=Desktop.Capture();if(jpeg.Length<500000)await SendProtected(new{k="frame",jpeg=Convert.ToBase64String(jpeg)});await Task.Delay(170,token);}}catch(OperationCanceledException){}catch(Exception e){UI(()=>{Disconnect();status.Text="Дэлгэц дамжуулалт зогслоо: "+e.Message;});}});
 }
 public async Task SendProtected(object message){
  await encryptedSender.WaitAsync();
  try{if(crypto!=null&&sid!=""&&net?.IsOpen==true)await net.Send(crypto.Seal(sid,message));}
  catch(Exception e){UI(()=>{Disconnect();status.Text="Илгээхэд алдаа: "+e.Message;});}finally{encryptedSender.Release();}
 }
 void Disconnect(){if(net?.IsOpen==true)_=net.Send(new{t="end"}).ContinueWith(_=>{});StopSession();status.Text="Холболт салгасан. Дэлгэц дамжуулахгүй.";}
 void StopSession(){
  capture?.Cancel();capture=null;input.Release();sid="";pending=false;localVerified=remoteVerified=false;role="";canControl=false;
  crypto?.Dispose();crypto=null;verify.Enabled=false;end.Enabled=false;request.Enabled=true;session.Text="Холболт нээгдээгүй";Text="RAINY Remote · Бие даасан алсын удирдлага";
  if(viewer!=null){var old=viewer;viewer=null;old.InternalClose=true;old.Close();}
  if(banner!=null){var old=banner;banner=null;closingBanner=true;old.Close();closingBanner=false;}
 }
}
