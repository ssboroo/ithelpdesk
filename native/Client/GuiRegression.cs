namespace RainyRemote;
static class GuiRegression {
 static T Find<T>(Control root,string name)where T:Control=>root.Controls.Find(name,true).OfType<T>().First();
 public static void Run(){
  using var host=new MainForm();using var guest=new MainForm();host.Show();guest.Show();bool invited=false,matched=false,complete=false;Exception? failure=null;
  var started=DateTime.UtcNow;int phase=0;
  using var timer=new System.Windows.Forms.Timer{Interval=100};
  timer.Tick+=(_,_)=>{
   try{
    if(!invited&&host.DeviceId!=""&&guest.DeviceId!=""){
     Find<TextBox>(guest,"TargetId").Text=host.DeviceId;Find<TextBox>(guest,"TargetPin").Text=host.PairCode;invited=true;Find<Button>(guest,"RequestConnection").PerformClick();
    }
    foreach(var form in Application.OpenForms.Cast<Form>().ToArray()){
     var consent=form.Controls.Find("AcceptConsent",true).OfType<Button>().FirstOrDefault();if(consent?.Enabled==true)consent.PerformClick();
    }
    var dialogs=Application.OpenForms.Cast<Form>().Where(f=>f.Name=="VerificationDialog").ToArray();
    if(!matched&&dialogs.Length==2){var codes=dialogs.Select(f=>Find<Label>(f,"FingerprintValue").Tag as string).ToArray();if(codes[0]!=codes[1])throw new Exception("GUI fingerprint mismatch");matched=true;}
    if(matched)foreach(var dialog in dialogs){var b=Find<Button>(dialog,"ConfirmFingerprint");if(b.Enabled)b.PerformClick();}
    if(phase==0&&guest.ReceivedFrames>=3&&Volatile.Read(ref host.SentFrames)>=3){
     using(var bitmap=new Bitmap(host.Width,host.Height)){host.DrawToBitmap(bitmap,new Rectangle(Point.Empty,host.Size));bitmap.Save("ui-main.png");}
     if(guest.Viewer!=null)using(var bitmap=new Bitmap(guest.Viewer.Width,guest.Viewer.Height)){guest.Viewer.DrawToBitmap(bitmap,new Rectangle(Point.Empty,guest.Viewer.Size));bitmap.Save("ui-viewer.png");}
     phase=1;Find<Button>(guest,"StopSession").PerformClick();
    }
    if(phase==1&&!host.HasSession&&!host.Sharing&&!guest.HasSession){complete=true;timer.Stop();}
   }catch(Exception e){failure=e;timer.Stop();}
  };
  timer.Start();
  while(!complete&&failure==null&&(DateTime.UtcNow-started).TotalSeconds<70){Application.DoEvents();Thread.Sleep(15);}
  timer.Stop();host.Close();guest.Close();Application.DoEvents();
  if(failure!=null)throw failure;
  if(!complete)throw new Exception($"GUI consent→verification→first-frame timeout. invited={invited}, matched={matched}, guestFrames={guest.ReceivedFrames}, hostFrames={host.SentFrames}");
 }
}
