using System.Drawing.Drawing2D;
namespace RainyRemote;
static class Ui {
 public static readonly Color Ink=Color.FromArgb(23,43,57),Muted=Color.FromArgb(99,117,129),Canvas=Color.FromArgb(244,247,249),Line=Color.FromArgb(222,231,235),Accent=Color.FromArgb(0,119,106),Soft=Color.FromArgb(230,245,240);
 public static Label Text(string text,float size=11,bool bold=false,Color? color=null)=>new(){Text=text,AutoSize=true,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=color??Ink,BackColor=Color.Transparent,Margin=new Padding(0,0,0,8)};
 public static Button Button(string text,bool primary=false)=>new(){Text=text,AutoSize=false,Height=44,Width=200,FlatStyle=FlatStyle.Flat,BackColor=primary?Accent:Color.White,ForeColor=primary?Color.White:Ink,Font=new Font("Segoe UI",11,FontStyle.Bold),Cursor=Cursors.Hand,Margin=new Padding(0,8,8,0),UseVisualStyleBackColor=false};
 public static void Style(Button b,bool primary=false){b.UseVisualStyleBackColor=false;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=primary?Accent:Line;b.BackColor=primary?Accent:Color.White;b.ForeColor=primary?Color.White:Ink;b.Font=new Font("Segoe UI",11,FontStyle.Bold);b.Height=44;b.Cursor=Cursors.Hand;if(primary)b.EnabledChanged+=(_,_)=>{b.BackColor=b.Enabled?Accent:Color.FromArgb(226,234,235);b.ForeColor=b.Enabled?Color.White:Muted;};}
 public static FlowLayoutPanel Stack(int padding=24)=>new(){Dock=DockStyle.Fill,BackColor=Color.White,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(padding),AutoScroll=true};
 public static void Field(TextBox box,string placeholder,int width=350){box.Width=width;box.Font=new Font("Segoe UI",15);box.PlaceholderText=placeholder;box.BorderStyle=BorderStyle.FixedSingle;box.Margin=new Padding(0,4,0,18);}
 public static void FitStack(FlowLayoutPanel panel){
  void Fit(){int width=Math.Max(100,panel.ClientSize.Width-panel.Padding.Horizontal-4);foreach(Control child in panel.Controls){if(child is Label label){label.MaximumSize=new Size(width,0);}else child.Width=Math.Min(600,width);}}
  panel.Resize+=(_,_)=>Fit();panel.ControlAdded+=(_,_)=>Fit();
 }
 public static string Code(string code)=>string.Join("  ",Enumerable.Range(0,(code.Length+3)/4).Select(i=>code.Substring(i*4,Math.Min(4,code.Length-i*4))));
}
sealed class VerificationDialog:Form {
 public VerificationDialog(string code,bool host){
  Icon=Brand.AppIcon;Text="Холболтоо баталгаажуулах";Name="VerificationDialog";ClientSize=new Size(560,350);BackColor=Color.White;Font=new Font("Segoe UI",11);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
  var body=Ui.Stack(28);body.Controls.Add(Ui.Text("Сүүлийн нэг алхам",20,true));
  body.Controls.Add(new Label{Text="Хоёр компьютер дээрх доорх код ижил эсэхийг\nутсаар харьцуулж шалгана уу.",AutoSize=true,ForeColor=Ui.Muted,Margin=new Padding(0,6,0,18)});
  var value=Ui.Text(Ui.Code(code),23,true,Ui.Accent);value.Name="FingerprintValue";value.Tag=code;body.Controls.Add(value);
  body.Controls.Add(Ui.Text(host?"Баталсны дараа таны дэлгэц хуваалцагдана.":"Хоёр тал баталсны дараа алсын дэлгэц нээгдэнэ.",10,false,Ui.Muted));
  var yes=Ui.Button(host?"Код ижил — Дэлгэцээ хуваалцах":"Код ижил — Дэлгэц харах",true);yes.Width=480;yes.Name="ConfirmFingerprint";yes.DialogResult=DialogResult.OK;body.Controls.Add(yes);
  var no=Ui.Button("Код зөрүүтэй — Цуцлах");no.Width=480;no.DialogResult=DialogResult.Cancel;body.Controls.Add(no);Controls.Add(body);CancelButton=no;
 }
}

sealed class ControlPermissionDialog:Form {
 public ControlPermissionDialog(){
  Icon=Brand.AppIcon;Name="ControlPermission";Text="Удирдах эрхийн хүсэлт";ClientSize=new Size(520,260);StartPosition=FormStartPosition.CenterParent;BackColor=Color.White;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
  var body=Ui.Stack();body.Controls.Add(Ui.Text("Гар, хулгана удирдах эрх",19,true));body.Controls.Add(new Label{Text="Холбогдсон хүн таны гар, хулганыг удирдах\nэрх хүсэж байна. Та зөвшөөрөх үү?",AutoSize=true,ForeColor=Ui.Muted,Margin=new Padding(0,12,0,16)});
  var yes=Ui.Button("Удирдахыг зөвшөөрөх",true);yes.Name="GrantControl";yes.Width=450;yes.DialogResult=DialogResult.OK;body.Controls.Add(yes);var no=Ui.Button("Зөвхөн харах эрхийг хэвээр үлдээх");no.Width=450;no.DialogResult=DialogResult.Cancel;body.Controls.Add(no);Controls.Add(body);CancelButton=no;
 }
}
