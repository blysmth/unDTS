using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
class RejectionChecks {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 [STAThread] static int Main(string[] args){try {
  new Application();SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());var type=Assembly.LoadFrom(args[0]).GetType("unDTS.Main");var main=Activator.CreateInstance(type);
  type.GetMethod("Init",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{new[]{args[1],args[2],args[3]}});
  var window=(Window)type.GetField("w",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
  var task=(System.Threading.Tasks.Task)type.GetField("inspection",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
  var deadline=DateTime.UtcNow.AddSeconds(30);var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(50)};Exception error=null;bool started=false;
  timer.Tick+=delegate {
   try {
    if(DateTime.UtcNow>deadline)throw new Exception("Timed out");
    if(!task.IsCompleted)return;
    var list=(IList)type.GetField("jobs",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
    if(!started){
     Check(list.Count==3,"All three inputs are shown with inspection status");
     Check((string)list[0].GetType().GetProperty("Status").GetValue(list[0],null)=="Rejected: no DTS audio","AAC-only input rejected immediately after adding");
     Check((string)list[1].GetType().GetProperty("Status").GetValue(list[1],null)=="Rejected: no DTS audio","Silent input rejected immediately after adding");
     Check((string)list[0].GetType().GetProperty("Detail").GetValue(list[0],null)=="Does not contain any DTS audio to convert.","Rejected row has clear explanatory tooltip");
     Check((string)list[2].GetType().GetProperty("Status").GetValue(list[2],null)=="Queued","Actual DTS input accepted");
     started=true;((Button)window.FindName("Start")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));return;
    }
    if((bool)type.GetField("running",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main)||(bool)type.GetField("preparing",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main))return;
    Check(((TextBlock)window.FindName("Summary")).Text.StartsWith("1 completed"),"Batch converts only the accepted DTS file");
    Check(list[0].GetType().GetProperty("Output").GetValue(list[0],null)==null&&list[1].GetType().GetProperty("Output").GetValue(list[1],null)==null,"Rejected files produce no outputs");
    timer.Stop();window.Close();Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
   }catch(Exception ex){error=ex;timer.Stop();window.Close();Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);}
  };timer.Start();Dispatcher.Run();if(error!=null)throw error;return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
