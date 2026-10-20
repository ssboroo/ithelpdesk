namespace RainyRemote;
static class GuiRegression {
 static T Find<T>(Control root,string name)where T:Control=>root.Controls.Find(name,true).OfType<T>().First();
 public static void Run(){
  using var host=new MainForm();using var guest=new MainForm();host.Show();guest.Show();bool invited=false,matched=false,complete=false;Exception? failure=null;
  var started=DateTime.UtcNow;int phase=0;long phaseTime=0;
  using var timer=new System.Windows.Forms.Timer{Interval=100};
  timer.Tick+=(_,_)=>{
   try{
    if(!invited&&host.DeviceId!=""&&guest.DeviceId!=""){
     using(var idle=new Bitmap(host.Width,host.Height)){host.DrawToBitmap(idle,new Rectangle(Point.Empty,host.Size));idle.Save("ui-main-idle.png");}
     Find<TextBox>(guest,"TargetId").Text=host.DeviceId;Find<TextBox>(guest,"TargetPin").Text=host.PairCode;invited=true;Find<Button>(guest,"RequestConnection").PerformClick();
    }
    foreach(var form in Application.OpenForms.Cast<Form>().ToArray()){
     var consent=form.Controls.Find("AcceptViewOnly",true).OfType<Button>().FirstOrDefault();if(consent?.Enabled==true)consent.PerformClick();
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
    if(phase==2&&guest.Viewer?.ControlAllowed==true){phase=3;_=guest.SendProtected(new{k="input",action="move",x=0.01,y=0.99});}
    if(phase==3&&host.PointerAppliedCount>0){var bounds=Screen.PrimaryScreen!.Bounds;var point=Cursor.Position;if(Math.Abs(point.X-(bounds.Left+(int)(0.01*(bounds.Width-1))))>2)throw new Exception("Remote pointer command did not move host cursor");phase=4;Find<Button>(guest.Viewer!,"StopViewer").PerformClick();}
    if(phase==4&&!host.HasSession&&!host.Sharing&&!guest.HasSession){complete=true;timer.Stop();}
   }catch(Exception e){failure=e;timer.Stop();}
  };
  timer.Start();
  while(!complete&&failure==null&&(DateTime.UtcNow-started).TotalSeconds<70){Application.DoEvents();Thread.Sleep(15);}
  timer.Stop();host.Close();guest.Close();Application.DoEvents();
  if(failure!=null)throw failure;
  if(!complete)throw new Exception($"GUI consent→verification→first-frame timeout. invited={invited}, matched={matched}, guestFrames={guest.ReceivedFrames}, hostFrames={host.SentFrames}");
 }
}
