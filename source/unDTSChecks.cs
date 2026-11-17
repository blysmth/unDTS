using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
class unDTSChecks {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 static void Wait(double seconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(seconds)};timer.Tick+=delegate {timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 static string Colored(TextBlock text){return string.Join("",text.Inlines.OfType<Run>().Where(r=>r.FontWeight==FontWeights.SemiBold).Select(r=>r.Text));}
 [STAThread] static int Main(string[] args){try {
  new Application();var assembly=Assembly.LoadFrom(args[0]);var type=assembly.GetType("unDTS.Main");var main=Activator.CreateInstance(type);
  type.GetMethod("Init",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{new string[0]});
  var window=(Window)type.GetField("w",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);window.Show();window.UpdateLayout();
  Check(window.Title=="unDTS","Application window is named unDTS");
  var view=type.GetField("filename",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);var vt=view.GetType();
  var original=(TextBlock)vt.GetField("original",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);var output=(TextBlock)vt.GetField("output",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
  vt.GetMethod("Start").Invoke(view,new object[]{"Movie.DTS-HD.MA.5.1.mkv","Movie.AC3.mkv"});
  Check(Colored(original)=="DTS-HD.MA.5.1","Complete DTS label is highlighted red");
  Check(Colored(output)=="AC3","Replacement is highlighted green");
  Check(((SolidColorBrush)original.Inlines.OfType<Run>().First(r=>r.FontWeight==FontWeights.SemiBold).Foreground).Color.R==255,"Source highlight uses red");
  Check(((SolidColorBrush)output.Inlines.OfType<Run>().First(r=>r.FontWeight==FontWeights.SemiBold).Foreground).Color.G==245,"Destination highlight uses green");
  Check(original.HasAnimatedProperties&&output.HasAnimatedProperties,"Both filenames have active crossfade clocks");
  Wait(2);
  Check(output.Opacity>0.9&&original.Opacity<0.1,"Pulse transitions from original to destination");
  vt.GetMethod("Complete").Invoke(view,new object[]{"Movie.AC3 (2).mkv"});
  Check(!original.HasAnimatedProperties&&!output.HasAnimatedProperties&&output.Opacity==1&&original.Opacity==0,"Completion stops pulse and keeps final output name");
  Check(new TextRange(output.ContentStart,output.ContentEnd).Text=="Movie.AC3 (2).mkv","Completion shows actual collision-resolved output filename");
  vt.GetMethod("Start").Invoke(view,new object[]{"Movie.AAC.mkv","Movie.AAC_AC3.mkv"});Check(Colored(output)=="AC3"&&Colored(original)=="","Fallback suffix is highlighted correctly");
  vt.GetMethod("Abort").Invoke(view,null);Check(original.Opacity==1&&output.Opacity==0&&!original.HasAnimatedProperties,"Cancellation stops pulse and restores source");
  window.Close();return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
