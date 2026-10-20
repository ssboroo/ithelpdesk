namespace RainyRemote;
// Only one screen image may be in flight. Input and heartbeat never wait for an image ACK.
sealed class FrameFlow {
 readonly object gate=new(); TaskCompletionSource<bool>? pending; long sequence,active,sentAt; int smooth=140;
 public int RoundTripMs=>Volatile.Read(ref smooth);
 public long Begin(){lock(gate){if(pending!=null)throw new InvalidOperationException("Frame already in flight");pending=new(TaskCreationOptions.RunContinuationsAsynchronously);active=++sequence;sentAt=Environment.TickCount64;return active;}}
 public void Acknowledge(long id){lock(gate){if(pending==null||active!=id)return;var elapsed=(int)Math.Clamp(Environment.TickCount64-sentAt,1,10000);smooth=(smooth*2+elapsed)/3;pending.TrySetResult(true);}}
 public async Task Wait(CancellationToken token){Task task;lock(gate){task=pending?.Task??throw new InvalidOperationException("No frame in flight");}try{await task.WaitAsync(TimeSpan.FromSeconds(12),token);}finally{lock(gate){pending=null;}}}
}
