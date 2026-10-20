using System.Text.Json;
using System.Threading.Channels;
using System.Security.Cryptography;
namespace RainyRemote;
static class SelfTest {
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value);
 static async Task<JsonElement> Next(Channel<JsonElement> queue,string type){using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(20));while(true){var m=await queue.Reader.ReadAsync(limit.Token);if(m.S("t")=="error")throw new Exception(m.S("message"));if(m.S("t")==type)return m;}}
 public static void Run(){
  var results=new List<string>();
  try{
   using var a=new SessionCrypto();using var b=new SessionCrypto();a.Agree(b.PublicKey);b.Agree(a.PublicKey);Assert(a.Fingerprint==b.Fingerprint,"Fingerprint mismatch");
   var sealedPacket=Json(a.Seal("test",new{k="ready"}));Assert(b.Open("test",sealedPacket).S("k")=="ready","AES roundtrip");
   bool replay=false;try{b.Open("test",sealedPacket);}catch(CryptographicException){replay=true;}Assert(replay,"Replay accepted");
   var wrong=Json(a.Seal("test",new{k="input"}));bool rejected=false;try{b.Open("other-session",wrong);}catch(CryptographicException){rejected=true;}Assert(rejected,"Session isolation failed");results.Add("ECDH, AES-GCM, replay and session isolation passed");
   byte[] jpeg=Desktop.Capture();using(var stream=new MemoryStream(jpeg))using(var image=Image.FromStream(stream)){Assert(image.Width<=1920&&image.Height<=1080,"Capture dimensions");}results.Add("Native Windows screen capture passed");
   using(var viewer=new ViewerForm("test","12345678",false,_=>Task.CompletedTask,()=>{},()=>{})){viewer.Frame(jpeg);Assert(viewer.Controls.Count>0,"Viewer UI");}results.Add("Native viewer JPEG rendering passed");
   using(var noise=new Bitmap(1920,1080)){var random=new Random(7);for(int y=0;y<noise.Height;y++)for(int x=0;x<noise.Width;x++)noise.SetPixel(x,y,Color.FromArgb(random.Next(256),random.Next(256),random.Next(256)));Assert(Desktop.Encode(noise).Length<=Desktop.FrameBudget,"Adaptive frame budget");}results.Add("High-entropy 1080p capture stays inside relay packet budget");
   TestDeltaFrames();results.Add("Delta tiles: static suppression, small change reconstruction, bandwidth reduction, refresh and dimension change passed");
   Task.Run(TestFrameFlow).GetAwaiter().GetResult();results.Add("Slow consumer: one frame in flight, stale ACK rejection, cancellation and recovery passed");
   TestInput();results.Add("Native Windows keyboard injection and release passed");
   Task.Run(()=>RoundTrip(jpeg)).GetAwaiter().GetResult();results.Add("Two native clients: live relay pairing, encrypted screen frame, encrypted input and disconnect passed");
   GuiRegression.Run();results.Add("Two GUI clients: real consent, matching inline verification panels, at least 3 displayed frames, read-only input denial, explicit control grant, actual relayed host cursor movement notification restore, disconnect, automatic server reconnect without session restore, 640/800/1024/1366 layouts without clipping and no extra top-level windows passed");
   File.WriteAllText("self-test-result.json",JsonSerializer.Serialize(new{ok=true,results},new JsonSerializerOptions{WriteIndented=true}));Environment.ExitCode=0;
  }catch(Exception e){File.WriteAllText("self-test-result.json",JsonSerializer.Serialize(new{ok=false,results,error=e.ToString()},new JsonSerializerOptions{WriteIndented=true}));Environment.ExitCode=1;}
 }
 static void TestDeltaFrames(){
  using var source=new Bitmap(1280,720,System.Drawing.Imaging.PixelFormat.Format24bppRgb);using(var g=Graphics.FromImage(source)){g.Clear(Color.DarkBlue);for(int x=0;x<1280;x+=40)g.DrawLine(Pens.White,x,0,x,719);}
  using var encoder=new FrameEncoder();using var renderer=new FrameRenderer();var first=encoder.Encode(source,0,100,1)!;using(var image=renderer.Apply(Json(first.Packet(1))))Assert(image.Size==source.Size,"Initial canvas size");
  Assert(encoder.Encode(source,83,100,1) is null,"Static screen resent");using(var g=Graphics.FromImage(source))g.FillRectangle(Brushes.Lime,20,20,24,24);
  var patch=encoder.Encode(source,166,100,1)!;Assert(patch.jpeg==null&&patch.tiles?.Length==1,"Small change did not use one tile");Assert(patch.EncodedBytes<first.EncodedBytes,"Patch did not reduce bandwidth");
  using(var image=renderer.Apply(Json(patch.Packet(2)))){Assert(image.GetPixel(30,30).G>200,"Changed tile missing");Assert(image.GetPixel(900,600).B>50,"Unchanged canvas lost");}
  Assert(encoder.Encode(source,10200,100,1)?.jpeg is not null,"Refresh missing");using var rotated=new Bitmap(720,1280);var changed=encoder.Encode(rotated,10300,100,1)!;using(var image=renderer.Apply(Json(changed.Packet(3))))Assert(image.Width<image.Height,"Portrait monitor aspect lost");
  bool rejected=false;try{using var ignored=renderer.Apply(Json(new{width=1,height=1,tiles=new[]{new{x=0,y=0,jpeg=""}}}));}catch(InvalidDataException){rejected=true;}Assert(rejected,"Wrong patch dimensions accepted");
 }
 static async Task TestFrameFlow(){
  var flow=new FrameFlow();long first=flow.Begin();using var cancel=new CancellationTokenSource();var waiting=flow.Wait(cancel.Token);
  await Task.Delay(200);Assert(!waiting.IsCompleted,"Slow viewer did not apply backpressure");
  flow.Acknowledge(first+1);Assert(!waiting.IsCompleted,"Wrong ACK released frame");flow.Acknowledge(first);await waiting;
  long next=flow.Begin();var nextWait=flow.Wait(cancel.Token);flow.Acknowledge(first);Assert(!nextWait.IsCompleted,"Stale ACK released new frame");flow.Acknowledge(next);await nextWait;
  flow.Begin();using var timeout=new CancellationTokenSource(30);bool cancelled=false;try{await flow.Wait(timeout.Token);}catch(OperationCanceledException){cancelled=true;}Assert(cancelled,"Capture cancellation blocked");
  long recovered=flow.Begin();var final=flow.Wait(cancel.Token);flow.Acknowledge(recovered);await final;
 }
 static void TestInput(){
  using var form=new Form{Text="RAINY controlled test target",Width=400,Height=180,StartPosition=FormStartPosition.CenterScreen};
  var text=new TextBox{Dock=DockStyle.Fill};form.Controls.Add(text);form.Show();form.Activate();text.Focus();Application.DoEvents();Thread.Sleep(100);
  var inject=new RemoteInput();
  try{inject.Apply(Json(new{k="input",action="key",key=65,up=false}));inject.Apply(Json(new{k="input",action="key",key=65,up=true}));Thread.Sleep(100);Application.DoEvents();Assert(text.Text.ToLowerInvariant()=="a","Native keyboard input did not reach controlled TextBox");}
  finally{inject.Release();form.Close();}
 }
 static async Task RoundTrip(byte[] jpeg){
  using var host=new Transport();using var viewer=new Transport();var h=Channel.CreateUnbounded<JsonElement>();var v=Channel.CreateUnbounded<JsonElement>();
  host.Message+=m=>h.Writer.TryWrite(m);viewer.Message+=m=>v.Writer.TryWrite(m);
  await host.Connect(BuildConfig.DefaultRelay);await viewer.Connect(BuildConfig.DefaultRelay);
  var registered=await Next(h,"registered");await Next(v,"registered");using var hc=new SessionCrypto();using var vc=new SessionCrypto();
  await viewer.Send(new{t="invite",target=registered.S("id"),pin=registered.S("pin"),key=vc.PublicKey});
  var invite=await Next(h,"invite");await host.Send(new{t="accept",id=invite.S("id"),key=hc.PublicKey,control=true});
  var ha=await Next(h,"accepted");var va=await Next(v,"accepted");hc.Agree(ha.S("key"));vc.Agree(va.S("key"));string sid=ha.S("sid");Assert(hc.Fingerprint==vc.Fingerprint,"Live key agreement");
  await host.Send(hc.Seal(sid,new{k="frame",jpeg=Convert.ToBase64String(jpeg)}));var frame=vc.Open(sid,await Next(v,"data"));Assert(Convert.FromBase64String(frame.S("jpeg")).SequenceEqual(jpeg),"Live frame corrupted");
  await viewer.Send(vc.Seal(sid,new{k="input",action="move",x=0.5,y=0.5}));var input=hc.Open(sid,await Next(h,"data"));Assert(input.S("action")=="move"&&input.D("x")==0.5,"Live input corrupted");
  await viewer.Send(new{t="end"});await Next(h,"ended");
 }
}
