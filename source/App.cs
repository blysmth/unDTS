using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Win32;

namespace unDTS {
public class Job : INotifyPropertyChanged {
 public string Path {get;set;}
 public string Output {get;set;}
 private string detail;
 public string Detail {get {return detail;} set {detail=value;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("Detail"));}}
 public string Name { get { return System.IO.Path.GetFileName(Path); } }
 public string Size { get { return Main.SizeText(new FileInfo(Path).Length); } }
 private string status="Queued";
 public string Status {get {return status;} set {status=value; if(PropertyChanged!=null) PropertyChanged(this,new PropertyChangedEventArgs("Status"));}}
 public event PropertyChangedEventHandler PropertyChanged;
}
public class Main {
 Window w; ObservableCollection<Job> jobs=new ObservableCollection<Job>();
 CancellationTokenSource cancel; bool running, closing;
 bool preparing;
 string ffmpegOverride;
 Task inspection=Task.FromResult(0);
 readonly CancellationTokenSource inspectionCancel=new CancellationTokenSource();
 Stopwatch graphClock=new Stopwatch();
 Stopwatch clock=new Stopwatch(); string lastOutput;
 BitrateGraph graph=new BitrateGraph();
 FilenameDisplay filename=new FilenameDisplay();
 readonly StringBuilder fullLog=new StringBuilder(); readonly object logGate=new object();
 readonly List<string> pendingLog=new List<string>();
 DispatcherTimer timer;
 double collapsedHeight, collapsedTop;
 [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)] struct MonitorInfo {public int Size;public NativeRect Monitor,Work;public uint Flags;}
 [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
 [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 Rect WorkArea() {
  var source=PresentationSource.FromVisual(w);var hwnd=new WindowInteropHelper(w).Handle;
  var info=new MonitorInfo {Size=Marshal.SizeOf(typeof(MonitorInfo))};
  if(hwnd!=IntPtr.Zero&&source!=null&&GetMonitorInfo(MonitorFromWindow(hwnd,2),ref info)) {
   var transform=source.CompositionTarget.TransformFromDevice;
   return new Rect(transform.Transform(new Point(info.Work.Left,info.Work.Top)),transform.Transform(new Point(info.Work.Right,info.Work.Bottom)));
  }
  return SystemParameters.WorkArea;
 }
 void FitWindow(bool restore) {
  FitWindowWithin(WorkArea(),restore);
 }
 void FitWindowWithin(Rect area,bool restore) {
  if(w.WindowState!=WindowState.Normal)return;
  w.MaxHeight=area.Height;w.MinHeight=Math.Min(720,area.Height);
  w.MaxWidth=area.Width;w.MinWidth=Math.Min(880,area.Width);
  w.Width=Math.Min(w.Width,area.Width);
  var viewer=Get<ScrollViewer>("MainScroll");
  double chrome=w.IsLoaded&&viewer.ActualHeight>0?Math.Max(0,w.ActualHeight-viewer.ActualHeight):SystemParameters.WindowCaptionHeight+2*SystemParameters.ResizeFrameHorizontalBorderHeight+8;
  double clientWidth=viewer.ActualWidth>0?viewer.ActualWidth:Math.Max(0,w.Width-20);
  var root=Get<Grid>("LayoutRoot");root.Measure(new Size(clientWidth,double.PositiveInfinity));
  double desired=root.DesiredSize.Height+chrome+2;
  double target=restore&&collapsedHeight>0?Math.Max(desired,collapsedHeight):desired;
  if(!restore&&Get<Expander>("LogExpander").IsExpanded)target=Math.Max(target,w.Height);
  w.Height=Math.Max(w.MinHeight,Math.Min(area.Height,Math.Ceiling(target)));
  double top=restore&&!double.IsNaN(collapsedTop)&&collapsedHeight>0?collapsedTop:w.Top;
  if(double.IsNaN(top))top=area.Top+(area.Height-w.Height)/2;
  w.Top=Math.Max(area.Top,Math.Min(top,area.Bottom-w.Height));
  if(!double.IsNaN(w.Left))w.Left=Math.Max(area.Left,Math.Min(w.Left,area.Right-w.Width));
 }
 T Get<T>(string name) where T:class {return w.FindName(name) as T;}
 void Text(string name,string value) {Get<TextBlock>(name).Text=value;}
 static string Base { get {return AppDomain.CurrentDomain.BaseDirectory;} }
 [STAThread] public static void MainEntry(string[] args) {
  try {
   var app=new Application();
   var main=new Main();
   main.Init(args);
   app.Run(main.w);
  } catch(Exception ex) {MessageBox.Show(ex.ToString(),"unDTS could not start",MessageBoxButton.OK,MessageBoxImage.Error);}
 }
 public static string SizeText(long bytes) {return bytes>=1073741824 ? (bytes/1073741824d).ToString("0.00")+" GB" : (bytes/1048576d).ToString("0.0")+" MB";}
 static string TimeText(double seconds) {if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0) return "—"; var t=TimeSpan.FromSeconds(Math.Min(seconds,315360000)); return ((int)t.TotalHours).ToString("00")+":"+t.Minutes.ToString("00")+":"+t.Seconds.ToString("00");}
 void Init(string[] args) {
  using(var stream=typeof(Main).Assembly.GetManifestResourceStream("MainWindow.xaml")) w=(Window)XamlReader.Load(stream);
  using(var stream=typeof(Main).Assembly.GetManifestResourceStream("unDTS.ico")) {
   var icon=System.Windows.Media.Imaging.BitmapFrame.Create(stream,System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
   icon.Freeze();w.Icon=icon;
  }
  using(var stream=typeof(Main).Assembly.GetManifestResourceStream("Logo.png")) {
   var logo=System.Windows.Media.Imaging.BitmapFrame.Create(stream,System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
   logo.Freeze();Get<Image>("Logo").Source=logo;
  }
  Get<ListView>("Queue").ItemsSource=jobs;
  Get<Border>("GraphHost").Child=graph;
  Get<Border>("FilenameHost").Child=filename;
  w.Closed+=delegate {inspectionCancel.Cancel();if(timer!=null)timer.Stop();};
  w.Loaded+=delegate {FitWindow(false);};
  Get<Expander>("LogExpander").Expanded+=delegate {
   collapsedHeight=w.Height;collapsedTop=w.Top;
   w.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(delegate {FitWindow(false);}));
  };
  Get<Expander>("LogExpander").Collapsed+=delegate {
   w.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(delegate {FitWindow(true);}));
  };
  FitWindow(false);
  Get<Button>("Add").Click+=delegate {var dialog=new OpenFileDialog {Filter="Matroska video (*.mkv)|*.mkv",Multiselect=true};if(dialog.ShowDialog(w)==true) AddFiles(dialog.FileNames);};
  w.PreviewDragOver+=delegate(object s,DragEventArgs e) {e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
  w.Drop+=delegate(object s,DragEventArgs e) {if(e.Data.GetDataPresent(DataFormats.FileDrop))AddFiles((string[])e.Data.GetData(DataFormats.FileDrop));e.Handled=true;};
  Get<Button>("Remove").Click+=delegate {foreach(Job j in Get<ListView>("Queue").SelectedItems.Cast<Job>().ToArray()) jobs.Remove(j);Count();};
  Get<Button>("Clear").Click+=delegate {foreach(var j in jobs.Where(j=>j.Status=="Completed"||j.Status=="Stopped"||j.Status.StartsWith("Failed")||j.Status.StartsWith("Rejected")).ToArray())jobs.Remove(j);Count();};
  Get<Button>("Start").Click+=async delegate {await RunBatch();};
  Get<Button>("Cancel").Click+=delegate {if(cancel!=null)cancel.Cancel(); Get<Button>("Cancel").IsEnabled=false;Text("Badge","●  STOPPING");};
  Get<Button>("OpenFolder").Click+=delegate {if(lastOutput!=null) Process.Start(new ProcessStartInfo("explorer.exe","/select,"+Converter.Quote(lastOutput)) {UseShellExecute=true});};
  Get<Button>("SaveLog").Click+=delegate {var dlg=new SaveFileDialog {Filter="Text log (*.txt)|*.txt",FileName="unDTS-log.txt"}; if(dlg.ShowDialog(w)==true)try {lock(logGate) File.WriteAllText(dlg.FileName,fullLog.ToString(),Encoding.UTF8);}catch(Exception ex){MessageBox.Show(w,ex.Message,"Could not save log");}};
  w.Closing+=delegate(object s,CancelEventArgs e) {if(running) {e.Cancel=true;if(MessageBox.Show(w,"Stop the current conversion and close? The incomplete output will be removed.","unDTS",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){closing=true;cancel.Cancel();}}};
  timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(200)};
  timer.Tick+=delegate {if(running)Text("Elapsed",TimeText(clock.Elapsed.TotalSeconds));FlushLog();};timer.Start();
  Log("unDTS 1.8 | AC3 640 kb/s | video and subtitle stream copy");
  AddFiles(args);
 }
 void Count() {Text("QueueCount","  /  "+jobs.Count+" file"+(jobs.Count==1?"":"s"));}
 void AddFiles(IEnumerable<string> paths) {
  var addedJobs=new List<Job>();int added=0; foreach(string path in paths) {
   try {
    IEnumerable<string> files=Directory.Exists(path)?Directory.EnumerateFiles(path,"*.mkv",SearchOption.TopDirectoryOnly):new[]{path};
    foreach(string file in files) {if(!File.Exists(file)||!string.Equals(Path.GetExtension(file),".mkv",StringComparison.OrdinalIgnoreCase))continue;string full=Path.GetFullPath(file);if(jobs.Any(j=>string.Equals(j.Path,full,StringComparison.OrdinalIgnoreCase)))continue;var job=new Job {Path=full,Status="Awaiting DTS check"};jobs.Add(job);addedJobs.Add(job);added++;}
   } catch(Exception ex) {Log("Could not add "+path+": "+ex.Message);}
  }
  Count();if(added>0){string ffmpeg=FindFFmpeg();if(ffmpeg!=null)ScheduleInspection(addedJobs.ToArray(),ffmpeg);Text("Summary",added+" file"+(added==1?"":"s")+" added. "+(ffmpeg!=null?"Checking for DTS audio...":"Locate FFmpeg using Convert queue to check audio."));}
 }
 void ScheduleInspection(Job[] added,string ffmpeg) {
  foreach(var job in added)job.Status="Checking DTS audio...";
  inspection=InspectAfter(inspection,added,ffmpeg);
 }
 async Task InspectAfter(Task previous,Job[] added,string ffmpeg) {
  await previous;
  foreach(var job in added) {
   if(inspectionCancel.IsCancellationRequested)return;if(!jobs.Contains(job))continue;
   try {
    var result=await Converter.InspectAudio(ffmpeg,job.Path,inspectionCancel.Token);
    job.Detail=result.Detail;
    if(!result.Readable){job.Status="Rejected: unreadable file";Log("Rejected "+job.Name+": Could not inspect media. "+result.Detail);}
    else if(!result.HasDts){job.Status="Rejected: no DTS audio";Log("Rejected "+job.Name+": Does not contain any DTS audio to convert.");if(!running)Text("Summary","Does not contain any DTS audio to convert.");}
    else {job.Status="Queued";if(!running&&!preparing)Text("Summary",jobs.Count(j=>j.Status=="Queued")+" file(s) contain DTS audio and are ready to convert.");}
   }catch(OperationCanceledException){return;}
   catch(Exception ex){job.Detail=ex.Message;job.Status="Rejected: check failed";Log("Could not check "+job.Name+": "+ex.Message);}
  }
 }
 void Log(string line) {lock(logGate){fullLog.AppendLine(line);pendingLog.Add(line);}}
 void FlushLog() {string chunk;lock(logGate) {if(pendingLog.Count==0)return;chunk=string.Join(Environment.NewLine,pendingLog)+Environment.NewLine;pendingLog.Clear();}var box=Get<TextBox>("Log");if(box.Text.Length>90000)box.Text=box.Text.Substring(box.Text.Length-60000);box.AppendText(chunk);box.ScrollToEnd();}
 async Task RunBatch() {
  if(running||preparing)return;
  string ffmpeg=FindFFmpeg();
  if(ffmpeg==null){MessageBox.Show(w,"Select the ffmpeg.exe used by your batch script. You can also place it beside unDTS.exe.","Locate FFmpeg",MessageBoxButton.OK,MessageBoxImage.Information);var locate=new OpenFileDialog {Title="Locate ffmpeg.exe",Filter="FFmpeg executable (ffmpeg.exe)|ffmpeg.exe"};if(locate.ShowDialog(w)!=true)return;ffmpeg=locate.FileName;ffmpegOverride=ffmpeg;}
  preparing=true;Get<Button>("Start").IsEnabled=false;
  try {
   var uncheckedJobs=jobs.Where(j=>j.Status=="Awaiting DTS check").ToArray();
   if(uncheckedJobs.Length>0)ScheduleInspection(uncheckedJobs,ffmpeg);
   await inspection;
  }finally {preparing=false;Get<Button>("Start").IsEnabled=true;}
  if(inspectionCancel.IsCancellationRequested)return;
  var batch=jobs.Where(j=>j.Status=="Queued").ToArray();
  if(batch.Length==0){Text("Summary",jobs.Any(j=>j.Status=="Rejected: no DTS audio")?"Nothing queued: no DTS audio to convert.":"Add files, or remove and re-add a file to convert it again.");return;}
  bool all=Get<CheckBox>("AllTracks").IsChecked==true;
  running=true;cancel=new CancellationTokenSource();foreach(string name in new[]{"Start","Remove","Clear"})Get<Button>(name).IsEnabled=false;Get<CheckBox>("AllTracks").IsEnabled=false;Get<Button>("Cancel").IsEnabled=true;
  int done=0, failed=0, stopped=0;
  try {
   foreach(Job job in batch) {
    if(cancel.IsCancellationRequested)break;
    clock.Restart();job.Status="Converting";Text("Badge","●  CONVERTING");filename.Start(job.Path,Converter.OutputName(job.Path));Text("Summary","File "+(done+failed+stopped+1)+" of "+batch.Length);Text("Remaining","—");Text("Speed","—");Text("Size","—");Text("Percent","0.0%");if(!graphClock.IsRunning)graphClock.Start();Text("Bitrate","-- Mb/s");Get<ProgressBar>("UnknownProgress").Visibility=Visibility.Visible;
    Log(Environment.NewLine+"=== "+job.Path+" ===");
    try {
     var result=await Converter.Run(ffmpeg,job.Path,all,cancel.Token,Log,delegate(Telemetry data) {w.Dispatcher.BeginInvoke(new Action(delegate {if(!running||job.Status!="Converting")return;Get<ProgressBar>("UnknownProgress").Visibility=data.Duration>0?Visibility.Collapsed:Visibility.Visible;Text("Percent",data.Duration>0?data.Percent.ToString("0.0")+"%":"—");graph.Add(graphClock.Elapsed.TotalSeconds,data.BitrateKbps);Text("Bitrate",data.BitrateKbps.HasValue?(data.BitrateKbps.Value/1000).ToString("0.00")+" Mb/s":"-- Mb/s");Text("Speed",data.Speed>0?data.Speed.ToString("0.0")+"×":"—");Text("Size",SizeText(data.Bytes));Text("Remaining",data.Duration>0&&data.Speed>0?TimeText(Math.Max(0,data.Duration-data.Position)/data.Speed):"—");}));});
     if(result.Cancelled){filename.Abort();job.Status="Stopped";stopped++;Log("Stopped. Incomplete output removed.");}
     else if(result.Success){graph.MarkBoundary(graphClock.Elapsed.TotalSeconds);filename.Complete(result.Output);job.Status="Completed";job.Output=result.Output;lastOutput=result.Output;Get<Button>("OpenFolder").IsEnabled=true;done++;Text("Percent","100.0%");Text("Size",SizeText(new FileInfo(result.Output).Length));Text("Remaining","00:00:00");}
     else {filename.Abort();job.Status="Failed (FFmpeg "+result.ExitCode+")";failed++;Log("Conversion failed. See FFmpeg details above.");Get<Expander>("LogExpander").IsExpanded=true;}
    }catch(OperationCanceledException){filename.Abort();job.Status="Stopped";stopped++;}
    catch(Exception ex){filename.Abort();job.Status="Failed";failed++;Log(ex.ToString());Get<Expander>("LogExpander").IsExpanded=true;}
    if(job.Status!="Completed")graph.BreakTrace(graphClock.Elapsed.TotalSeconds);clock.Stop();Text("Elapsed",TimeText(clock.Elapsed.TotalSeconds));
   }
  } finally {
   bool wasCancelled=cancel.IsCancellationRequested;cancel.Dispose();cancel=null;running=false;Get<ProgressBar>("UnknownProgress").Visibility=Visibility.Collapsed;foreach(string name in new[]{"Start","Remove","Clear"})Get<Button>(name).IsEnabled=true;Get<CheckBox>("AllTracks").IsEnabled=true;Get<Button>("Cancel").IsEnabled=false;
   Text("Badge",wasCancelled?"●  STOPPED":failed>0?"●  CHECK LOG":"●  COMPLETE");Text("Summary",done+" completed · "+failed+" failed · "+jobs.Count(j=>j.Status=="Queued")+" queued"+(stopped>0?" · "+stopped+" stopped":""));FlushLog();if(closing)w.Close();
  }
 }
 string FindFFmpeg() {
  if(!string.IsNullOrEmpty(ffmpegOverride)&&File.Exists(ffmpegOverride))return ffmpegOverride;
  string local=Path.Combine(Base,"ffmpeg.exe");if(File.Exists(local))return local;
  foreach(string folder in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator)) {
   try {string candidate=Path.Combine(folder.Trim().Trim('"'),"ffmpeg.exe");if(Path.IsPathRooted(candidate)&&File.Exists(candidate))return candidate;}catch(ArgumentException){}catch(NotSupportedException){}
  }
  return null;
 }
}
}
