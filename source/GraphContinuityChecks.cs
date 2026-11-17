using System;
using System.Linq;
using unDTS;
class GraphContinuityChecks {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 [STAThread] static int Main(){try{
  var sampler=new BitrateSampler();
  Check(sampler.Add(1,1000)==8,"Initial sample uses output bytes and media duration");
  Check(!sampler.Add(1,2000).HasValue,"Repeated timestamp waits for usable data");
  Check(sampler.Add(2,3000)==16,"Repeated timestamp bytes included in next valid interval");
  Check(!sampler.Add(3,3000).HasValue,"Buffered output does not emit a false zero dip");
  Check(sampler.Add(4,5000)==8,"Buffered interval is included when writes resume");
  Check(!sampler.Add(4.01,6000).HasValue,"Tiny media intervals cannot produce huge instantaneous readings");
  Check(sampler.Add(5,7000)==16,"Tiny interval bytes are retained for next reading");
  var graph=new BitrateGraph();graph.Add(0,10000);graph.Add(0.5,null);graph.Add(1,10000);
  Check(graph.Plot(20000).All(p=>p.Rate.HasValue),"Brief missing update keeps one continuous trace");
  graph.Add(2,null);graph.Add(4,12000);
  Check(graph.Plot(20000).Count(p=>!p.Rate.HasValue)==1,"Longer missing interval keeps a visible gap");
  graph.MarkBoundary(4);graph.Add(4.1,5000);
  var plot=graph.Plot(20000);
  Check(plot.Count(p=>!p.Rate.HasValue)==2&&plot.Last().Rate==5000,"File boundary breaks line and resets smoothing");
  graph.Reset();graph.Add(0,10000);graph.Add(0.5,20000);
  Check(graph.Plot(25000).Last().Rate>10000&&graph.Plot(25000).Last().Rate<16000,"Gentle smoothing reduces sharp jumps");
  for(int i=2;i<=8;i++)graph.Add(i*0.5,20000);
  Check(graph.Plot(25000).Last().Rate>19800,"Sustained change settles promptly");
  graph.Reset();graph.Add(0,5000);graph.Add(0.5,5000);double range=graph.ScaleMaximum();
  graph.Add(1,1e9);graph.Add(1.5,5000);
  Check(graph.ScaleMaximum()==range,"Smoothing retains single-spike axis protection");
  Check(graph.Plot(range).Last().Rate<6000,"Isolated spike cannot leave a large smoothing tail");
  graph.BreakTrace(2);graph.Add(2.1,15000);
  Check(graph.Plot(20000).Last().Rate==15000,"Explicit stop/failure break resets smoothing");
  return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
