using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using unDTS;
class GraphScaleChecks {
 static void Check(bool ok,string message) {if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 [STAThread] static int Main(string[] args) {try {
  var g=new BitrateGraph();
  g.Add(0,5000);g.Add(1,5000);double normal=g.ScaleMaximum();
  g.Add(2,1000000000);Check(g.ScaleMaximum()==normal,"A billion-kb/s spike cannot expand the axis");
  g.Add(3,5000);Check(g.ScaleMaximum()==normal,"Returning to normal preserves the same range");
  g.Add(4,5000);g.Add(5,15000);Check(g.ScaleMaximum()==normal,"First high reading awaits confirmation");
  g.Add(6,15000);Check(g.ScaleMaximum()>15000,"Consecutive high readings expand the range");
  g.Reset();g.Add(0,double.MaxValue);Check(g.ScaleMaximum()==1000,"Startup outlier cannot expand the axis");
  g.Add(1,5000);g.Add(2,5000);Check(g.ScaleMaximum()==normal,"Normal startup readings establish the range");
  g.Add(60,5000);g.Add(61,5000);Check(g.ScaleMaximum()==normal,"Expiring an outlier leaves the range unchanged");
  g.Reset();g.Add(0,5000);g.Add(1,5000);g.MarkBoundary(2);g.Add(3,1000000000);
  Check(g.ScaleMaximum()==normal,"First spike in another file cannot use the previous file as support");
  g.Add(4,5000);g.Add(5,null);g.Add(6,double.NaN);g.Add(7,double.PositiveInfinity);g.Add(8,-1);
  Check(g.ScaleMaximum()==normal,"Missing and invalid rates do not affect the range");
  g.Reset();for(int i=0;i<=60;i++)g.Add(i,i==30?1000000000:5000+500*Math.Sin(i));
  g.MarkBoundary(60);g.Add(61,5000);g.Measure(new Size(900,128));g.Arrange(new Rect(0,0,900,128));g.UpdateLayout();
  var bitmap=new RenderTargetBitmap(900,128,96,96,PixelFormats.Pbgra32);bitmap.Render(g);
  byte[] pixels=new byte[900*128*4];bitmap.CopyPixels(pixels,900*4,0);int amber=0,green=0,red=0;
  for(int i=0;i<pixels.Length;i+=4){if(pixels[i+2]>220&&pixels[i+1]>150&&pixels[i+1]<220&&pixels[i]<130)amber++;if(pixels[i+1]>220&&pixels[i+2]<140)green++;if(pixels[i+2]>200&&pixels[i+1]<120)red++;}
  Check(amber>0,"Clipped spike has a visible amber cap");Check(green>200,"Ordinary bitrate trace remains visible");Check(red>100,"Red completion marker still renders");
  if(args.Length>0){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(args[0]))encoder.Save(f);}
  return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
