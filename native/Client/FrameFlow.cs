namespace RainyRemote;
// A bounded two-frame window hides network roundtrips without building a stale queue.
sealed class FrameFlow {
 public const int Capacity=2;
 readonly object gate=new();readonly SemaphoreSlim slots=new(Capacity,Capacity);readonly Dictionary<long,long> pending=[];long sequence;int smooth=140;
 public int RoundTripMs=>Volatile.Read(ref smooth);
 internal int PendingCount {get{lock(gate)return pending.Count;}}
 public async Task<long> Reserve(CancellationToken token){
  int remaining;lock(gate){remaining=pending.Count==0?12000:(int)Math.Clamp(12000-(Environment.TickCount64-pending.Values.Min()),1,12000);}
  if(!await slots.WaitAsync(remaining,token))throw new TimeoutException("Дэлгэцийн хариу хүлээх хугацаа дууслаа.");
  lock(gate){if(pending.Values.Any(t=>Environment.TickCount64-t>=12000)){slots.Release();throw new TimeoutException("Дэлгэцийн хариу хүлээх хугацаа дууслаа.");}long id=++sequence;pending.Add(id,Environment.TickCount64);return id;}
 }
 public void Acknowledge(long id){lock(gate){if(!pending.Remove(id,out long sentAt))return;int elapsed=(int)Math.Clamp(Environment.TickCount64-sentAt,1,10000);smooth=(smooth*2+elapsed)/3;slots.Release();}}
 public void Skip(long id){lock(gate){if(pending.Remove(id))slots.Release();}}
}
