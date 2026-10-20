using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace RainyRemote;
static class J {
 public static string S(this JsonElement e,string k)=>e.TryGetProperty(k,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
 public static bool B(this JsonElement e,string k)=>e.TryGetProperty(k,out var v)&&v.ValueKind==JsonValueKind.True;
 public static long N(this JsonElement e,string k)=>e.TryGetProperty(k,out var v)&&v.TryGetInt64(out var n)?n:0;
 public static double D(this JsonElement e,string k)=>e.TryGetProperty(k,out var v)&&v.TryGetDouble(out var n)?n:0;
}
sealed class Transport : IDisposable {
 readonly ClientWebSocket socket=new();readonly SemaphoreSlim sender=new(1,1);readonly CancellationTokenSource stop=new();
 public event Action<JsonElement>? Message;public event Action<string>? Closed;
 public bool IsOpen=>socket.State==WebSocketState.Open;
 public async Task Connect(string url){
  var uri=new Uri(url);if(uri.Scheme!="wss"&&!(uri.Scheme=="ws"&&uri.IsLoopback))throw new ArgumentException("Сервер wss:// хаягтай байна");
  socket.Options.KeepAliveInterval=TimeSpan.FromSeconds(20);
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(20));
  await socket.ConnectAsync(uri,timeout.Token);_=Task.Run(Receive);
 }
 public async Task Send(object value){
  byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value);
  if(bytes.Length>1500000)throw new InvalidOperationException("Багц хэт том");
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(12));
  await sender.WaitAsync(timeout.Token);
  try{if(!IsOpen)throw new IOException("Сервертэй холболт тасарсан");await socket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,timeout.Token);}
  finally{sender.Release();}
 }
 async Task Receive(){
  string reason="Сервертэй холболт хаагдлаа";
  try{
   var buffer=new byte[32768];
   while(!stop.IsCancellationRequested&&IsOpen){
    using var message=new MemoryStream();WebSocketReceiveResult result;
    do{result=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),stop.Token);if(result.MessageType==WebSocketMessageType.Close)return;
     if(result.MessageType!=WebSocketMessageType.Text||message.Length+result.Count>1500000)throw new IOException("Буруу багц");
     message.Write(buffer,0,result.Count);
    }while(!result.EndOfMessage);
    using var doc=JsonDocument.Parse(message.ToArray());Message?.Invoke(doc.RootElement.Clone());
   }
  }catch(Exception e)when(e is WebSocketException or IOException or JsonException or OperationCanceledException){reason=e is OperationCanceledException?"Холболт хаагдлаа":"Сүлжээний холболт тасарлаа";}
  finally{Closed?.Invoke(reason);}
 }
 public void Dispose(){stop.Cancel();socket.Abort();socket.Dispose();}
}
sealed class SessionCrypto : IDisposable {
 readonly ECDiffieHellman exchange=ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
 byte[]? key;long sent,last;
 public string PublicKey=>Convert.ToBase64String(exchange.ExportSubjectPublicKeyInfo());
 public string Fingerprint=>key==null?"":Convert.ToHexString(SHA256.HashData(key))[..16];
 public void Agree(string peer){using var remote=ECDiffieHellman.Create();remote.ImportSubjectPublicKeyInfo(Convert.FromBase64String(peer),out _);key=exchange.DeriveKeyFromHash(remote.PublicKey,HashAlgorithmName.SHA256);sent=last=0;}
 public object Seal(string sid,object value){
  if(key==null)throw new InvalidOperationException("Түлхүүр бэлэн биш");
  long n=Interlocked.Increment(ref sent);byte[] plain=JsonSerializer.SerializeToUtf8Bytes(value),iv=RandomNumberGenerator.GetBytes(12),tag=new byte[16],cipher=new byte[plain.Length];
  using var aes=new AesGcm(key,16);aes.Encrypt(iv,plain,cipher,tag,Encoding.UTF8.GetBytes(sid+":"+n));
  return new{t="data",sid,n,iv=Convert.ToBase64String(iv),tag=Convert.ToBase64String(tag),body=Convert.ToBase64String(cipher)};
 }
 public JsonElement Open(string sid,JsonElement envelope){
  if(key==null||envelope.S("sid")!=sid||envelope.N("n")<=last)throw new CryptographicException("Хуучин эсвэл өөр холболтын багц");
  long n=envelope.N("n");byte[] cipher=Convert.FromBase64String(envelope.S("body")),plain=new byte[cipher.Length];
  using var aes=new AesGcm(key,16);aes.Decrypt(Convert.FromBase64String(envelope.S("iv")),cipher,Convert.FromBase64String(envelope.S("tag")),plain,Encoding.UTF8.GetBytes(sid+":"+n));
  using var doc=JsonDocument.Parse(plain);last=n;return doc.RootElement.Clone();
 }
 public void Dispose(){if(key!=null)CryptographicOperations.ZeroMemory(key);exchange.Dispose();}
}
