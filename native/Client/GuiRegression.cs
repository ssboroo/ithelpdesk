namespace RainyRemote;
static class GuiRegression {
 static T Find<T>(Control root,string name)where T:Control=>root.Controls.Find(name,true).OfType<T>().First();
 static void TestLayouts(MainForm host,MainForm guest){
  foreach(var size in new[]{new Size(640,480),new Size(800,600),new Size(1024,768),new Size(1366,768)}){
   host.ClientSize=size;Application.DoEvents();var scroll=Find<Panel>(host,"HomeScroll");AssertInside(Find<Button>(host,"RequestConnection"),scroll);
   using var bitmap=new Bitmap(host.Width,host.Height);host.DrawToBitmap(bitmap,new Rectangle(Point.Empty,host.Size));bitmap.Save($"ui-layout-{size.Width}.png");
  }
  using var viewer=new ViewerForm("123456789","test",true,_=>Task.CompletedTask,()=>{},()=>{}){TopLevel=false,FormBorderStyle=FormBorderStyle.None,Dock=DockStyle.Fill,MinimumSize=Size.Empty};guest.Controls.Add(viewer);viewer.Show();viewer.BringToFront();guest.ClientSize=new Size(640,480);Application.DoEvents();AssertInside(Find<Button>(viewer,"StopViewer"),viewer,true);AssertInside(Find<ComboBox>(viewer,"StreamQuality"),viewer,true);using var image=new Bitmap(guest.Width,guest.Height);guest.DrawToBitmap(image,new Rectangle(Point.Empty,guest.Size));image.Save("ui-viewer-small.png");viewer.InternalClose=true;guest.Controls.Remove(viewer);
 }
 static void AssertInside(Control child,Control container,bool vertical=false){var rect=container.RectangleToClient(child.RectangleToScreen(child.ClientRectangle));if(rect.Left<0||rect.Right>container.ClientSize.Width||(vertical&&(rect.Top<0||rect.Bottom>container.ClientSize.Height)))throw new Exception("Horizontal clipping at "+container.Size+": "+child.Name);}
 public static void Run(){
  using var host=new MainForm();using var guest=new MainForm();host.Show();guest.Show();bool invited=false,matched=false,complete=false;Exception? failure=null;Transport? interrupted=null;
  var started=DateTime.UtcNow;int phase=0;long phaseTime=0;
  using var timer=new System.Windows.Forms.Timer{Interval=100};
  timer.Tick+=(_,_)=>{
   try{
    if(!invited&&host.DeviceId!=""&&guest.DeviceId!=""){
     using(var idle=new Bitmap(host.Width,host.Height)){host.DrawToBitmap(idle,new Rectangle(Point.Empty,host.Size));idle.Save("ui-main-idle.png");}
     host.WindowState=FormWindowState.Minimized;Find<TextBox>(guest,"TargetId").Text=host.DeviceId;Find<TextBox>(guest,"TargetPin").Text=host.PairCode;invited=true;Find<Button>(guest,"RequestConnection").PerformClick();
    }
    foreach(var form in Application.OpenForms.Cast<Form>().ToArray()){
     var consent=form.Controls.Find("AcceptViewOnly",true).OfType<Button>().FirstOrDefault();if(consent?.Enabled==true){if(host.InvitationNotifications!=1)throw new Exception("Invitation notification missing");host.RestoreForNotification();if(host.WindowState==FormWindowState.Minimized)throw new Exception("Notification restore failed");consent.PerformClick();}
     var grant=form.Controls.Find("GrantControl",true).OfType<Button>().FirstOrDefault();if(grant?.Enabled==true)grant.PerformClick();
    }
    var dialogs=new[]{host,guest}.SelectMany(f=>f.Controls.Find("VerificationDialog",true)).OfType<Form>().ToArray();
    if(!matched&&dialogs.Length==2){var codes=dialogs.Select(f=>Find<Label>(f,"FingerprintValue").Tag as string).ToArray();if(codes[0]!=codes[1])throw new Exception("GUI fingerprint mismatch");matched=true;}
    if(matched)foreach(var dialog in dialogs){var b=Find<Button>(dialog,"ConfirmFingerprint");if(b.Enabled)b.PerformClick();}
    if(Application.OpenForms.Cast<Form>().Count(f=>f.TopLevel)>2)throw new Exception("Unexpected extra top-level window");
    if(phase==0&&guest.ReceivedFrames>=3&&host.SentFrames>=3){
     using(var bitmap=new Bitmap(host.Width,host.Height)){host.DrawToBitmap(bitmap,new Rectangle(Point.Empty,host.Size));bitmap.Save("ui-main.png");}
     if(guest.Viewer!=null)using(var bitmap=new Bitmap(guest.Viewer.Width,guest.Viewer.Height)){guest.Viewer.DrawToBitmap(bitmap,new Rectangle(Point.Empty,guest.Viewer.Size));bitmap.Save("ui-viewer.png");}
     phase=1;phaseTime=Environment.TickCount64;_=guest.SendProtected(new{k="input",action="move",x=0.01,y=0.99});
    }
    if(phase==1&&Environment.TickCount64-phaseTime>300){if(host.InputAppliedCount!=0)throw new Exception("Read-only peer could inject input");phase=2;Find<Button>(guest.Viewer!,"RequestControl").PerformClick();}
    if(phase==2&&guest.Viewer?.ControlAllowed==true){phase=3;_=guest.SendProtected(new{k="input",action="move",x=0.01,y=0.99,probe=17});}
    if(phase==3&&host.LastPointerProbe==17){var bounds=Screen.PrimaryScreen!.Bounds;var point=host.LastPointerApplied;if(Math.Abs(point.X-(bounds.Left+(int)(0.01*(bounds.Width-1))))>2)throw new Exception("Remote pointer command did not move host cursor");phase=4;Find<Button>(guest.Viewer!,"Fullscreen").PerformClick();Find<Button>(guest.Viewer!,"StopViewer").PerformClick();}
    if(phase==4&&!host.HasSession&&!host.Sharing&&!guest.HasSession){if(guest.FormBorderStyle!=FormBorderStyle.Sizable)throw new Exception("Disconnect left fullscreen chrome hidden");phase=5;interrupted=host.Connection;interrupted!.Dispose();}
    if(phase==5&&host.Connection!=interrupted&&host.Connection?.IsOpen==true&&host.DeviceId!=""){if(host.HasSession||host.Sharing)throw new Exception("Reconnect restored a session without consent");complete=true;timer.Stop();}
   }catch(Exception e){failure=e;timer.Stop();}
  };
  timer.Start();
  while(!complete&&failure==null&&(DateTime.UtcNow-started).TotalSeconds<70){Application.DoEvents();Thread.Sleep(15);}
  timer.Stop();if(complete)TestLayouts(host,guest);host.Close();guest.Close();Application.DoEvents();
  if(failure!=null)throw failure;
  if(!complete)throw new Exception($"GUI consent→verification→first-frame timeout. invited={invited}, matched={matched}, guestFrames={guest.ReceivedFrames}, hostFrames={host.SentFrames}");
 }
}
