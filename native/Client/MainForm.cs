using System.Text.Json;
namespace RainyRemote;
sealed partial class MainForm : Form {
 readonly TextBox server=new(),target=new(),pin=new();readonly Label myId=new(),myPin=new(),status=new(),session=new();
 readonly Button connect=new(),request=new(),verify=new(),end=new();readonly RemoteInput input=new();readonly SemaphoreSlim encryptedSender=new(1,1);
 Transport? net;SessionCrypto? crypto;string sid="",role="";bool canControl,localVerified,remoteVerified,pending;CancellationTokenSource? capture;
 CancellationTokenSource? heartbeat;long lastPeerPacket;
 ViewerForm? viewer;Form? consent;Form? banner;bool closingBanner;
 readonly string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RainyRemote3","server.txt");
 public MainForm(){InitializeInterface();}
 internal string DeviceId=>myId.Tag as string??"";
 internal string PairCode=>myPin.Tag as string??"";
 internal int ReceivedFrames {get;private set;}
 internal int SentFrames;
 internal bool Sharing=>capture!=null;
 internal bool HasSession=>sid!="";
 internal ViewerForm? Viewer=>viewer;
 void UI(Action action){if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke((Action)(()=>{if(!IsDisposed)action();}));}catch(InvalidOperationException){}}
 async Task Connect(){
  if(crypto!=null||pending){status.Text="Эхлээд одоогийн холболтоо салгана уу.";return;}
  connect.Enabled=false;net?.Dispose();net=new Transport();var connection=net;
  connection.Message+=m=>UI(()=>{if(net==connection)OnPacket(m);});connection.Closed+=s=>UI(()=>{if(net!=connection)return;StopSession();connectionBadge.Text="● Холболт тасарсан";myId.Text="Холболтгүй";myId.Tag=null;myPin.Text="Код: —";myPin.Tag=null;status.Text=s+" · Серверт дахин холбогдоно уу.";connect.Enabled=true;});
  try{status.Text="Сервертэй холбогдож байна…";await connection.Connect(server.Text.Trim());Directory.CreateDirectory(Path.GetDirectoryName(settings)!);File.WriteAllText(settings,server.Text.Trim());}
  catch(Exception e){status.Text="Холбогдсонгүй. "+ErrorText(e);connection.Dispose();}finally{connect.Enabled=true;}
 }
 async Task Invite(){
  try{
   if(net?.IsOpen!=true)throw new InvalidOperationException("Эхлээд серверт холбогдоно уу.");
   if(crypto!=null||pending)throw new InvalidOperationException("Өөр холболт нээлттэй байна.");
   string id=target.Text.Replace(" ","").Trim(),code=pin.Text.Trim();if(!System.Text.RegularExpressions.Regex.IsMatch(id,"^[0-9]{9}$")||!System.Text.RegularExpressions.Regex.IsMatch(code,"^[0-9]{6}$"))throw new ArgumentException("Төхөөрөмжийн дугаар 9 цифр, код 6 цифр байна.");
   crypto=new SessionCrypto();pending=true;end.Enabled=true;request.Enabled=false;await net.Send(new{t="invite",target=id,pin=code,key=crypto.PublicKey});pin.Clear();status.Text="Зөвшөөрөл хүлээж байна…";
  }catch(Exception e){StopSession();status.Text=ErrorText(e);}
 }
 async void OnPacket(JsonElement m){
  try{
   switch(m.S("t")){
    case "registered":connectionBadge.Text="● Холболт бэлэн";myId.Text=string.Join(" ",Enumerable.Range(0,3).Select(i=>m.S("id").Substring(i*3,3)));myId.Tag=m.S("id");myPin.Text=m.S("pin");myPin.Tag=m.S("pin");status.Text="Бэлэн · Дэлгэц дамжуулаагүй. Холбогдох хүний хүсэлтийг зөвшөөрсний дараа хуваалцана.";break;
    case "waiting":session.Text="Нөгөө талын зөвшөөрлийг хүлээж байна";status.Text=m.S("message");break;
    case "invite":
     if(crypto!=null||pending){await net!.Send(new{t="reject",id=m.S("id")});return;}
     string requestId=m.S("id");consent=new Form{Text="RAINY Remote · Холболтын зөвшөөрөл",Width=550,Height=270,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
     var label=new Label{Text=$"{m.S("from")} төхөөрөмж таны дэлгэцийг харах хүсэлт илгээлээ.\nЗөвшөөрсний дараа хоёр талын баталгаажуулах кодыг харьцуулна.",Dock=DockStyle.Top,Height=85,Padding=new Padding(15),Font=Font};
     var control=new CheckBox{Text="Хулгана, гар удирдахыг мөн зөвшөөрөх",Dock=DockStyle.Top,Height=45,Padding=new Padding(15),Font=Font};
     var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=60,Padding=new Padding(15)};var allow=new Button{Name="AcceptConsent",Text="Зөвшөөрөх",DialogResult=DialogResult.OK,AutoSize=true};var deny=new Button{Text="Татгалзах",DialogResult=DialogResult.Cancel,AutoSize=true};buttons.Controls.Add(allow);buttons.Controls.Add(deny);consent.Controls.Add(control);consent.Controls.Add(label);consent.Controls.Add(buttons);
     var choice=consent.ShowDialog(this);bool accepted=choice==DialogResult.OK;bool granted=control.Checked;consent.Dispose();consent=null;
     if(accepted){crypto=new SessionCrypto();pending=true;end.Enabled=true;request.Enabled=false;await net!.Send(new{t="accept",id=requestId,key=crypto.PublicKey,control=granted});}else await net!.Send(new{t="reject",id=requestId});break;
    case "accepted":
     if(crypto==null){await net!.Send(new{t="end"});return;}
     ReceivedFrames=0;SentFrames=0;crypto.Agree(m.S("key"));sid=m.S("sid");role=m.S("role");canControl=m.B("control");pending=false;localVerified=remoteVerified=false;end.Enabled=true;
     lastPeerPacket=Environment.TickCount64;heartbeat=new CancellationTokenSource();var heartToken=heartbeat.Token;
     _=Task.Run(async()=>{try{while(!heartToken.IsCancellationRequested){await Task.Delay(5000,heartToken);if(Environment.TickCount64-lastPeerPacket>25000){UI(()=>{Disconnect();status.Text="Нөгөө талын хариу тасарсан тул хуваалцахыг зогсоолоо.";});return;}await SendProtected(new{k="alive"});}}catch(OperationCanceledException){}});
     session.Text=$"{m.S("peer")} · Холболтын кодоо баталгаажуулна уу";status.Text="Баталгаажуулах цонхонд кодоо харьцуулж, хоёр тал зөвшөөрнө.";
     if(role=="host"){verify.Enabled=true;Text="RAINY Remote · Хуваалцах зөвшөөрөл хүлээж байна";}
     else if(role=="viewer"){viewer=new ViewerForm(m.S("peer"),crypto.Fingerprint,canControl,SendProtected,()=>_=Verify(),Disconnect);viewer.Show(this);}else {Disconnect();break;}
     using(var confirmation=new VerificationDialog(crypto.Fingerprint,role=="host")){
      if(confirmation.ShowDialog(viewer is null?this:viewer)==DialogResult.OK&&crypto!=null)await Verify();else Disconnect();
     }
     break;
    case "data":
     if(crypto==null||m.S("sid")!=sid)return;var data=crypto.Open(sid,m);lastPeerPacket=Environment.TickCount64;
     if(data.S("k")=="ready"){remoteVerified=true;if(role=="viewer")viewer?.SetPeerReady();MaybeCapture();}
     else if(data.S("k")=="frame"&&role=="viewer"&&localVerified&&remoteVerified){viewer?.Frame(Convert.FromBase64String(data.S("jpeg")));ReceivedFrames++;session.Text=$"Дэлгэц дамжиж байна · {ReceivedFrames} зураг хүлээн авлаа";status.Text="Холболт хэвийн. Дуусгахдаа «Холболт салгах» дарна уу.";if(ReceivedFrames==1)await SendProtected(new{k="frame_ack"});}
     else if(data.S("k")=="frame_ack"&&role=="host"){session.Text="Дэлгэц амжилттай хүрсэн · Хуваалцаж байна";}
     else if(data.S("k")=="input"&&role=="host"&&canControl&&localVerified&&remoteVerified)input.Apply(data);break;
    case "ended":case "cancelled":consent?.Close();StopSession();status.Text=m.S("message");break;
    case "error":status.Text=m.S("message");if(pending)StopSession();break;
   }
  }catch(Exception e){Disconnect();status.Text="Холболт зогслоо. "+ErrorText(e);}
 }
 async Task Verify(){localVerified=true;verify.Enabled=false;viewer?.MarkVerified();session.Text=remoteVerified?"Дэлгэц дамжуулалтыг эхлүүлж байна":"Нөгөө тал кодоо баталгаажуулахыг хүлээж байна";await SendProtected(new{k="ready"});MaybeCapture();}
 void MaybeCapture(){
  if(role!="host"||!localVerified||!remoteVerified||capture!=null||crypto==null)return;
  capture=new CancellationTokenSource();var token=capture.Token;Text="● RAINY Remote · ДЭЛГЭЦ ХУВААЛЦАЖ БАЙНА";status.Text=canControl?"Дэлгэц, гар, хулгана хуваалцаж байна. Салгах товчоор хүссэн үедээ зогсооно.":"Дэлгэцийг зөвхөн харах эрхээр хуваалцаж байна.";
  banner=new Form{Text="Дэлгэц хуваалцаж байна",Width=500,Height=75,TopMost=true,FormBorderStyle=FormBorderStyle.FixedToolWindow,StartPosition=FormStartPosition.Manual,Location=new Point(20,10)};
  var stopButton=new Button{Text="● RAINY Remote · Хуваалцаж байна — САЛГАХ",Dock=DockStyle.Fill,BackColor=Color.Firebrick,ForeColor=Color.White};stopButton.Click+=(_,_)=>Disconnect();banner.Controls.Add(stopButton);banner.FormClosing+=(_,e)=>{if(!closingBanner){e.Cancel=true;BeginInvoke((Action)(()=>Disconnect()));}};banner.Show();
  _=Task.Run(async()=>{try{while(!token.IsCancellationRequested){byte[] jpeg=Desktop.Capture();await SendProtected(new{k="frame",jpeg=Convert.ToBase64String(jpeg)});Interlocked.Increment(ref SentFrames);await Task.Delay(170,token);}}catch(OperationCanceledException){}catch(Exception e){UI(()=>{Disconnect();status.Text="Дэлгэц дамжуулалт зогслоо. "+ErrorText(e);});}});
 }
 public async Task SendProtected(object message){
  await encryptedSender.WaitAsync();
  try{if(crypto!=null&&sid!=""&&net?.IsOpen==true)await net.Send(crypto.Seal(sid,message));}
  catch(Exception e){UI(()=>{Disconnect();status.Text="Өгөгдөл илгээж чадсангүй. "+ErrorText(e);});}finally{encryptedSender.Release();}
 }
 static string ErrorText(Exception error){
  if(error is System.Security.Cryptography.CryptographicException)return "Холболтын хамгаалалт тохирсонгүй. Хоёр тал хамгийн шинэ хувилбарыг ашиглана уу.";
  if(error is OperationCanceledException)return "Холболтын хугацаа хэтэрлээ. Интернэтээ шалгаад дахин холбоно уу.";
  if((error is InvalidOperationException or ArgumentException)&&error.Message.Any(c=>c>1024&&c<1300))return error.Message;
  return "Интернэт, серверийн холболтоо шалгаад дахин оролдоно уу.";
 }
 void Disconnect(){if(net?.IsOpen==true)_=net.Send(new{t="end"}).ContinueWith(_=>{});StopSession();status.Text="Холболт салгасан. Дэлгэц дамжуулахгүй.";}
 void StopSession(){
  heartbeat?.Cancel();heartbeat=null;capture?.Cancel();capture=null;input.Release();sid="";pending=false;localVerified=remoteVerified=false;role="";canControl=false;
  crypto?.Dispose();crypto=null;verify.Enabled=false;end.Enabled=false;request.Enabled=true;session.Text="Холболт нээгдээгүй";Text="RAINY Remote · Бие даасан алсын удирдлага";
  if(viewer!=null){var old=viewer;viewer=null;old.InternalClose=true;old.Close();}
  if(banner!=null){var old=banner;banner=null;closingBanner=true;old.Close();closingBanner=false;}
 }
}
