using System.Text.Json;
namespace RainyRemote;
sealed partial class MainForm : Form {
 readonly TextBox server=new(),target=new(),pin=new();readonly Label myId=new(),myPin=new(),status=new(),session=new();
 readonly Button connect=new(),request=new(),verify=new(),end=new();readonly RemoteInput input=new();readonly SemaphoreSlim encryptedSender=new(1,1);
 Transport? net;SessionCrypto? crypto;string sid="",role="";bool canControl,localVerified,remoteVerified,pending;CancellationTokenSource? capture;
 CancellationTokenSource? heartbeat;long lastPeerPacket;
 ViewerForm? viewer;Form? consent;FrameFlow? frames;int generation;
 readonly string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RainyRemote3","server.txt");
 public MainForm(){InitializeInterface();}
 internal string DeviceId=>myId.Tag as string??"";
 internal string PairCode=>myPin.Tag as string??"";
 internal int ReceivedFrames {get;private set;}
 internal int SentFrames;
 internal int InputAppliedCount {get;private set;}
 internal int PointerAppliedCount {get;private set;}
 bool controlRequestPending;
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
     string requestId=m.S("id");consent=new Form{Icon=Brand.AppIcon,Text="Холбогдох хүсэлт",ClientSize=new Size(560,360),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
     var body=Ui.Stack(26);body.Controls.Add(Ui.Text("Танд туслах хүсэлт ирлээ",19,true));body.Controls.Add(new Label{Text=$"Төхөөрөмжийн дугаар: {m.S("from")}\nЯмар эрх олгохоо сонгоно уу.",AutoSize=true,ForeColor=Ui.Muted,Margin=new Padding(0,6,0,18)});
     bool granted=false;
     var allow=Ui.Button("Гар, хулгана удирдахыг зөвшөөрөх",true);allow.Width=490;allow.Name="AcceptConsent";allow.Click+=(_,_)=>{granted=true;consent.DialogResult=DialogResult.OK;};body.Controls.Add(allow);
     var viewOnly=Ui.Button("Зөвхөн дэлгэц харахыг зөвшөөрөх");viewOnly.Name="AcceptViewOnly";viewOnly.Width=490;viewOnly.DialogResult=DialogResult.OK;body.Controls.Add(viewOnly);
     var deny=Ui.Button("Татгалзах");deny.Width=490;deny.DialogResult=DialogResult.Cancel;body.Controls.Add(deny);consent.Controls.Add(body);consent.CancelButton=deny;
     var choice=await ShowInline(consent);bool accepted=choice==DialogResult.OK;consent.Dispose();consent=null;
     if(accepted){crypto=new SessionCrypto();pending=true;end.Enabled=true;request.Enabled=false;await net!.Send(new{t="accept",id=requestId,key=crypto.PublicKey,control=granted});}else await net!.Send(new{t="reject",id=requestId});break;
    case "accepted":
     if(crypto==null){await net!.Send(new{t="end"});return;}
     frames=new FrameFlow();ReceivedFrames=0;SentFrames=0;InputAppliedCount=0;PointerAppliedCount=0;crypto.Agree(m.S("key"));sid=m.S("sid");role=m.S("role");canControl=m.B("control");pending=false;localVerified=remoteVerified=false;end.Enabled=true;
     lastPeerPacket=Environment.TickCount64;heartbeat=new CancellationTokenSource();var heartToken=heartbeat.Token;
     _=Task.Run(async()=>{try{while(!heartToken.IsCancellationRequested){await Task.Delay(5000,heartToken);if(Environment.TickCount64-lastPeerPacket>25000){UI(()=>{Disconnect();status.Text="Нөгөө талын хариу тасарсан тул хуваалцахыг зогсоолоо.";});return;}await SendProtected(new{k="alive"});}}catch(OperationCanceledException){}});
     session.Text=$"{m.S("peer")} · Холболтын кодоо баталгаажуулна уу";status.Text="Доорх кодоо харьцуулж, хоёр тал зөвшөөрнө.";
     if(role=="host"){verify.Enabled=true;Text="RAINY Remote · Хуваалцах зөвшөөрөл хүлээж байна";}
     else if(role=="viewer"){viewer=new ViewerForm(m.S("peer"),crypto.Fingerprint,canControl,SendProtected,()=>_=Verify(),Disconnect);viewer.TopLevel=false;viewer.FormBorderStyle=FormBorderStyle.None;viewer.MinimumSize=Size.Empty;viewer.Dock=DockStyle.Fill;foreach(Control home in Controls)home.Visible=false;Controls.Add(viewer);viewer.Show();viewer.BringToFront();}else {Disconnect();break;}
     using(var confirmation=new VerificationDialog(crypto.Fingerprint,role=="host")){
      if(await ShowInline(confirmation)==DialogResult.OK&&crypto!=null)await Verify();else Disconnect();
     }
     break;
    case "data":
     if(crypto==null||m.S("sid")!=sid)return;var data=crypto.Open(sid,m);lastPeerPacket=Environment.TickCount64;
     if(data.S("k")=="ready"){remoteVerified=true;if(role=="viewer")viewer?.SetPeerReady();MaybeCapture();}
     else if(data.S("k")=="frame"&&role=="viewer"&&localVerified&&remoteVerified){if(viewer==null)return;var activeViewer=viewer;int version=generation;await activeViewer.FrameAsync(Convert.FromBase64String(data.S("jpeg")));if(version!=generation||viewer!=activeViewer)return;ReceivedFrames++;session.Text=$"Дэлгэц дамжиж байна · {ReceivedFrames} зураг хүлээн авлаа";status.Text="Холболт хэвийн. Дуусгахдаа «Холболт салгах» дарна уу.";await SendProtected(new{k="frame_ack",id=data.N("id")});}
     else if(data.S("k")=="frame_ack"&&role=="host"){frames?.Acknowledge(data.N("id"));session.Text="Дэлгэц амжилттай хүрсэн · Хуваалцаж байна";}
     else if(data.S("k")=="request_control"&&role=="host"&&!canControl&&localVerified&&remoteVerified&&!controlRequestPending){
      controlRequestPending=true;using var dialog=new ControlPermissionDialog();bool grant=await ShowInline(dialog)==DialogResult.OK;controlRequestPending=false;
      if(grant&&crypto!=null){canControl=true;await SendProtected(new{k="control_granted"});status.Text="Нөгөө талд гар, хулгана удирдах эрх олголоо.";}else await SendProtected(new{k="control_denied"});
     }
     else if(data.S("k")=="control_granted"&&role=="viewer"){canControl=true;viewer?.SetControl(true);}
     else if(data.S("k")=="control_denied"&&role=="viewer")viewer?.ControlDenied();
     else if(data.S("k")=="input_denied"&&role=="viewer")viewer?.InputDenied();
     else if(data.S("k")=="input"&&role=="host"&&canControl&&localVerified&&remoteVerified){
      try{input.Apply(data);InputAppliedCount++;if(data.S("action")=="move")PointerAppliedCount++;}catch(Exception error)when(error is InvalidOperationException or System.ComponentModel.Win32Exception){await SendProtected(new{k="input_denied"});status.Text="Windows удирдлагыг зөвшөөрсөнгүй. Өндөр эрхтэй цонх удирдах бол программыг администратороор нээнэ үү.";}
     }break;
    case "ended":case "cancelled":consent?.Close();StopSession();status.Text=m.S("message");break;
    case "error":status.Text=m.S("message");if(pending)StopSession();break;
   }
  }catch(Exception e){Disconnect();status.Text="Холболт зогслоо. "+ErrorText(e);}
 }
 async Task Verify(){localVerified=true;verify.Enabled=false;viewer?.MarkVerified();session.Text=remoteVerified?"Дэлгэц дамжуулалтыг эхлүүлж байна":"Нөгөө тал кодоо баталгаажуулахыг хүлээж байна";await SendProtected(new{k="ready"});MaybeCapture();}
 void MaybeCapture(){
  if(role!="host"||!localVerified||!remoteVerified||capture!=null||crypto==null)return;
  capture=new CancellationTokenSource();var token=capture.Token;Text="● RAINY Remote · ДЭЛГЭЦ ХУВААЛЦАЖ БАЙНА";status.Text=canControl?"Дэлгэц, гар, хулгана хуваалцаж байна. Салгах товчоор хүссэн үедээ зогсооно.":"Дэлгэцийг зөвхөн харах эрхээр хуваалцаж байна.";
  var flow=frames!;int version=generation;string activeSid=sid;
  _=Task.Run(async()=>{try{using var grabber=new CaptureSession();while(!token.IsCancellationRequested){long started=Environment.TickCount64;byte[]? jpeg=grabber.Next(flow.RoundTripMs);if(jpeg is not null){long id=flow.Begin();await SendProtectedFor(new{k="frame",id,jpeg=Convert.ToBase64String(jpeg)},activeSid);Interlocked.Increment(ref SentFrames);await flow.Wait(token);}int pause=(int)Math.Max(1,50-(Environment.TickCount64-started));await Task.Delay(pause,token);}}catch(OperationCanceledException){}catch(Exception e){UI(()=>{if(version!=generation)return;Disconnect();status.Text="Дэлгэц дамжуулалт зогслоо. "+ErrorText(e);});}});
 }
 public Task SendProtected(object message)=>SendProtectedFor(message,null);
 async Task SendProtectedFor(object message,string? expectedSession){
  await encryptedSender.WaitAsync();
  try{if(crypto!=null&&sid!=""&&net?.IsOpen==true&&(expectedSession==null||sid==expectedSession))await net.Send(crypto.Seal(sid,message));}
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
  generation++;CancelInline();frames=null;heartbeat?.Cancel();heartbeat=null;capture?.Cancel();capture=null;input.Release();sid="";pending=false;localVerified=remoteVerified=false;role="";canControl=false;
  crypto?.Dispose();crypto=null;verify.Enabled=false;end.Enabled=false;request.Enabled=true;session.Text="Холболт нээгдээгүй";Text="RAINY Remote · Бие даасан алсын удирдлага";
  if(viewer!=null){var old=viewer;viewer=null;old.InternalClose=true;Controls.Remove(old);old.Close();old.Dispose();foreach(Control home in Controls)home.Visible=true;}

 }
}
