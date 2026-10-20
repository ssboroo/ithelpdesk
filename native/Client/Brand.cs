using System.Drawing.Drawing2D;
namespace RainyRemote;
static class Brand {
 public static Icon AppIcon {get;}=Load();
 static Icon Load(){using var stream=typeof(Brand).Assembly.GetManifestResourceStream("RainyRemote.Brand.ico")??throw new InvalidOperationException("Программын дүрс олдсонгүй");using var icon=new Icon(stream);return (Icon)icon.Clone();}
}
sealed class BrandMark:Control {
 public BrandMark(){DoubleBuffered=true;Size=new Size(148,64);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  g.DrawIcon(Brand.AppIcon,new Rectangle(0,7,44,44));
  using var font=new Font("Segoe UI",19,FontStyle.Bold);using var white=new SolidBrush(Color.White);g.DrawString("RAINY",font,white,50,11);
 }
}
