using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
class UiChecks {
 [STAThread] static int Main(string[] args) {
  try {
   var app=new Application();
   SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
   var assembly=Assembly.LoadFrom(args[0]);var type=assembly.GetType("unDTS.Main");var main=Activator.CreateInstance(type);
   type.GetMethod("Init",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{new string[0]});
   var window=(Window)type.GetField("w",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
   type.GetMethod("AddFiles",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{new string[]{args[1]}});
   var queue=(ListView)window.FindName("Queue");if(queue.Items.Count!=1)throw new Exception("Drop did not add one job.");
   Console.WriteLine("PASS: File-ingestion handler adds an MKV to the queue");
   window.Width=880;window.Height=720;window.Measure(new Size(880,720));window.Arrange(new Rect(0,0,880,720));window.UpdateLayout();
   ((Expander)window.FindName("LogExpander")).IsExpanded=true;window.UpdateLayout();
   Console.WriteLine("PASS: Final XAML loads and lays out at minimum size with log expanded");
   var graph=type.GetField("graph",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
   var graphType=graph.GetType();
   for(int i=0;i<1000;i++)graphType.GetMethod("Add").Invoke(graph,new object[]{i*0.5,(double?)(2800+900*Math.Sin(i*0.4))});
   graphType.GetMethod("Add").Invoke(graph,new object[]{500.0,null});
   graphType.GetMethod("Add").Invoke(graph,new object[]{501.0,(double?)2300.0});
   Console.WriteLine("PASS: Bitrate graph accepts live samples, rolling history, and unavailable-value gaps");
   ((Expander)window.FindName("LogExpander")).IsExpanded=false;
   var content=(FrameworkElement)window.Content;content.Measure(new Size(1100,1040));content.Arrange(new Rect(0,0,1100,1040));content.UpdateLayout();
   if(args.Length>2){var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1100,1040,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(content);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var output=File.Create(args[2]))png.Save(output);}
   ((Button)window.FindName("Start")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   var deadline=DateTime.UtcNow.AddSeconds(20);var check=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(100)};
   int code=1;
   check.Tick+=delegate {
    if(((bool)type.GetField("running",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(main)||(bool)type.GetField("preparing",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(main))&&DateTime.UtcNow<deadline)return;
    string summary=((TextBlock)window.FindName("Summary")).Text;
    if(summary.StartsWith("1 completed · 0 failed")){Console.WriteLine("PASS: Convert queue button completes actual DTS-to-AC3 conversion");code=0;}else Console.WriteLine("FAIL: "+summary);
    check.Stop();window.Close();Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
   };
   check.Start();Dispatcher.Run();return code;
  }catch(Exception ex){Console.WriteLine(ex);return 1;}
 }
}
