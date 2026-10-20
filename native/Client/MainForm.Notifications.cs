using System.Runtime.InteropServices;
namespace RainyRemote;
sealed partial class MainForm {
 readonly NotifyIcon notifications=new();readonly System.Windows.Forms.Timer reconnect=new(){Interval=2000};bool connecting,appClosing;int reconnectAttempt;
 internal int InvitationNotifications {get;private set;}
 [StructLayout(LayoutKind.Sequential)]struct FLASH{public uint size;public IntPtr window;public uint flags,count,timeout;}
 [DllImport("user32.dll")]static extern bool FlashWindowEx(ref FLASH info);
 internal void RestoreForNotification(){if(IsDisposed)return;if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;Show();Activate();}
 void InitializeNotifications(){
  notifications.Icon=Brand.AppIcon;notifications.Text="RAINY · Алсын тусламж";notifications.Visible=true;notifications.BalloonTipClicked+=(_,_)=>RestoreForNotification();notifications.DoubleClick+=(_,_)=>RestoreForNotification();
  reconnect.Tick+=async(_,_)=>{if(appClosing||connecting||HasSession||pending)return;if(net?.IsOpen==true){reconnect.Stop();reconnectAttempt=0;return;}await Connect();};
 }
 void NotifyInvitation(string peer){
  InvitationNotifications++;notifications.ShowBalloonTip(10000,"Холбогдох хүсэлт ирлээ",$"Төхөөрөмж {peer} танд туслах хүсэлт илгээлээ. Мэдэгдэл дээр дарж эрхээ сонгоно уу.",ToolTipIcon.Info);
  try{System.Media.SystemSounds.Exclamation.Play();}catch(InvalidOperationException){}
  var flash=new FLASH{size=(uint)Marshal.SizeOf<FLASH>(),window=Handle,flags=3,count=5};FlashWindowEx(ref flash);
 }
 void ScheduleReconnect(){if(appClosing||IsDisposed)return;reconnect.Interval=Math.Min(30000,2000*(1<<Math.Min(reconnectAttempt++,4)));reconnect.Start();connectionBadge.Text="● Дахин холбогдож байна";}
 void FitWindow(){var area=Screen.FromControl(this).WorkingArea;MinimumSize=new Size(Math.Min(640,area.Width),Math.Min(440,area.Height));Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height));Location=new Point(Math.Clamp(Left,area.Left,Math.Max(area.Left,area.Right-Width)),Math.Clamp(Top,area.Top,Math.Max(area.Top,area.Bottom-Height)));}
}
