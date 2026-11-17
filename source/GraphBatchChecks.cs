using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
class GraphBatchChecks {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 [STAThread] static int Main(string[] args){try {
  new Application();SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
  var assembly=Assembly.LoadFrom(args[0]);var type=assembly.GetType("unDTS.Main");var main=Activator.CreateInstance(type);
  type.GetMethod("Init",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{new string[]{args[1],args[2]}});
  var window=(Window)type.GetField("w",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
  ((Button)window.FindName("Start")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  var check=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(100)};var deadline=DateTime.UtcNow.AddSeconds(30);Exception error=null;
  check.Tick+=delegate {
   if(((bool)type.GetField("running",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main)||(bool)type.GetField("preparing",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main))&&DateTime.UtcNow<deadline)return;
   check.Stop();try {
    Check(((TextBlock)window.FindName("Summary")).Text.StartsWith("2 completed"),"Two real conversions complete in one batch");
    var graph=type.GetField("graph",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);var gt=graph.GetType();
    var markers=(IList)gt.GetField("boundaries",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(graph);
    Check(markers.Count==2,"One completion marker per file; previous marker retained");
    var samples=(IList)gt.GetField("samples",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(graph);double previous=-1;int gaps=0;
    foreach(var sample in samples){var st=sample.GetType();double time=(double)st.GetField("Time").GetValue(sample);Check(time>=previous,"Graph sample timestamps remain monotonic across files");previous=time;if(st.GetField("Rate").GetValue(sample)==null)gaps++;}
    Check(gaps>=2,"Completed files break the trace so adjacent conversions are not joined");
    gt.GetMethod("Reset").Invoke(graph,null);gt.GetMethod("Add").Invoke(graph,new object[]{0.0,(double?)1000});gt.GetMethod("Add").Invoke(graph,new object[]{5.0,(double?)1200});gt.GetMethod("MarkBoundary").Invoke(graph,new object[]{5.0});gt.GetMethod("Add").Invoke(graph,new object[]{10.0,(double?)900});
    var element=(FrameworkElement)graph;element.Measure(new Size(700,128));element.Arrange(new Rect(0,0,700,128));element.UpdateLayout();
    var bitmap=new RenderTargetBitmap(700,128,96,96,PixelFormats.Pbgra32);bitmap.Render(element);byte[] pixels=new byte[700*128*4];bitmap.CopyPixels(pixels,700*4,0);int red=0;
    for(int i=0;i<pixels.Length;i+=4)if(pixels[i+2]>200&&pixels[i+1]<120&&pixels[i]<130)red++;
    Check(red>100,"Vertical completion marker renders in red after next file samples arrive");
    gt.GetMethod("Add").Invoke(graph,new object[]{100.0,(double?)900});Check(markers.Count==0,"Markers scroll out with the 60-second history window");
   }catch(Exception ex){error=ex;}finally {window.Close();Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);}
  };check.Start();Dispatcher.Run();if(error!=null)throw error;return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
