using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
class WindowSizingChecks {
 static void Check(bool condition,string text){if(!condition)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static void Layout(Window w){w.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate {}));w.UpdateLayout();}
 [STAThread] static int Main(string[] args) {
  try {
   new Application();
   var type=Assembly.LoadFrom(args[0]).GetType("unDTS.Main");var main=Activator.CreateInstance(type);
   type.GetMethod("Init",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{new string[0]});
   var w=(Window)type.GetField("w",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
   var viewer=(ScrollViewer)w.FindName("MainScroll");var expander=(Expander)w.FindName("LogExpander");
   w.Show();Layout(w);
   Rect area=(Rect)type.GetMethod("WorkArea",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);
   double collapsed=w.Height,originalTop=w.Top;
   Console.WriteLine("Startup height: "+collapsed+"; work area: "+area.Height+"; scroll overflow: "+viewer.ScrollableHeight);
   Check(w.Height<=area.Height+1&&w.Top>=area.Top-1&&w.Top+w.Height<=area.Bottom+1,"Startup stays within monitor work area");
   Check(viewer.ScrollableHeight<3 || Math.Abs(w.Height-area.Height)<3,"All startup content fits whenever the screen is tall enough");
   expander.IsExpanded=true;Layout(w);double expanded=w.Height;
   Console.WriteLine("Expanded height: "+expanded+"; scroll overflow: "+viewer.ScrollableHeight);
   Check(expanded>=collapsed,"Opening log never reduces window height");
   Check(expanded>collapsed+100 || Math.Abs(expanded-area.Height)<3,"Opening log grows window to fit or reaches screen height");
   Check(viewer.ScrollableHeight<3 || Math.Abs(expanded-area.Height)<3,"Expanded content fits whenever screen space permits");
   Check(w.Top>=area.Top-1&&w.Top+w.Height<=area.Bottom+1,"Expanded window stays on screen");
   expander.IsExpanded=false;Layout(w);
   Check(Math.Abs(w.Height-collapsed)<3,"Closing log restores collapsed height");
   Check(Math.Abs(w.Top-originalTop)<3,"Closing log restores vertical position");
   for(int i=0;i<3;i++){expander.IsExpanded=true;Layout(w);expander.IsExpanded=false;Layout(w);}
   Check(Math.Abs(w.Height-collapsed)<3,"Repeated toggles do not accumulate height drift");
   w.WindowState=WindowState.Maximized;Layout(w);expander.IsExpanded=true;Layout(w);
   Check(w.WindowState==WindowState.Maximized,"Opening log preserves maximized state");
   w.WindowState=WindowState.Normal;expander.IsExpanded=false;Layout(w);
   type.GetMethod("FitWindowWithin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{new Rect(area.Left,area.Top,area.Width,720),false});Layout(w);
   Check(w.Height<=720&&viewer.ScrollableHeight>0,"Short screens cap height and retain scroll access to all controls");
   expander.IsExpanded=true;Layout(w);
   type.GetMethod("FitWindowWithin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{new Rect(area.Left,area.Top,area.Width,1040),false});Layout(w);
   Check(w.Height<=1040&&w.Top+w.Height<=area.Top+1041,"Expanded log respects a 1080p-sized work area");
   w.Close();return 0;
  }catch(Exception ex){Console.WriteLine(ex);return 1;}
 }
}
