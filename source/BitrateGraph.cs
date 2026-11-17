using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace unDTS {
// Native vector graph; values are sampled output bits per second of media time.
public sealed class BitrateGraph : FrameworkElement {
 sealed class Sample {public double Time; public double? Rate; public bool Break;}
 internal sealed class PlotPoint {public double Time; public double? Rate;}
 readonly List<Sample> samples = new List<Sample>();
 readonly List<double> boundaries = new List<double>();
 public void Reset() {samples.Clear();boundaries.Clear();InvalidateVisual();}
 public void MarkBoundary(double seconds) {
  if(double.IsNaN(seconds)||double.IsInfinity(seconds))return;
  boundaries.Add(seconds);
  // A missing-value sample breaks the trace between different files.
  BreakTrace(seconds);
 }
 public void BreakTrace(double seconds) {
  if(double.IsNaN(seconds)||double.IsInfinity(seconds))return;
  Add(seconds,null);
  if(samples.Count>0)samples[samples.Count-1].Break=true;
 }
 public void Add(double seconds, double? kbps) {
  if(double.IsNaN(seconds)||double.IsInfinity(seconds))return;
  if(kbps.HasValue && (double.IsNaN(kbps.Value)||double.IsInfinity(kbps.Value)||kbps.Value<0))kbps=null;
  samples.Add(new Sample {Time=seconds, Rate=kbps});
  while(samples.Count>1 && samples[1].Time<seconds-60) samples.RemoveAt(0);
  boundaries.RemoveAll(delegate(double time) {return time<seconds-60;});
  InvalidateVisual();
 }
 static SolidColorBrush Brush(byte r,byte g,byte b,byte a) {return new SolidColorBrush(Color.FromArgb(a,r,g,b));}
 // Require two neighbouring readings to support a higher range. A lone spike,
 // including at startup or either edge of the rolling window, cannot set it.
 // Missing samples break support between files. Raw rates remain unchanged.
 internal double ScaleMaximum() {
  double supported=1000/1.15;double? previous=null;double previousTime=0;
  foreach(var s in samples) {
   if(s.Break){previous=null;continue;}
   if(!s.Rate.HasValue)continue;
   if(previous.HasValue && s.Time-previousTime<=2)
    supported=Math.Max(supported,Math.Min(previous.Value,s.Rate.Value));
   previous=s.Rate;previousTime=s.Time;
  }
  double max=supported*1.15;
  double step=Math.Pow(10,Math.Floor(Math.Log10(max)));
  return Math.Ceiling(max/step)*step;
 }
 // Smooth only the visual trace. The numeric readout and stored samples stay
 // measured values. Time-based weighting behaves consistently at varying
 // update rates. Capping before filtering prevents one outlier from bleeding
 // into subsequent normal readings. Brief missing updates may be bridged;
 // explicit file boundaries and gaps longer than two seconds never are.
 internal List<PlotPoint> Plot(double maximum) {
  var result=new List<PlotPoint>();double? value=null;double last=0;
  foreach(var s in samples) {
   if(s.Break){result.Add(new PlotPoint {Time=s.Time});value=null;continue;}
   if(!s.Rate.HasValue)continue;
   double elapsed=s.Time-last;
   if(value.HasValue && (elapsed>2||elapsed<0)) {result.Add(new PlotPoint {Time=s.Time});value=null;}
   double target=Math.Min(s.Rate.Value,maximum);
   value=value.HasValue?value.Value+(1-Math.Exp(-Math.Max(0,elapsed)/0.75))*(target-value.Value):target;
   last=s.Time;result.Add(new PlotPoint {Time=s.Time,Rate=value});
  }
  return result;
 }
 void Label(DrawingContext dc,string text,double x,double y) {
  dc.DrawText(new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Consolas"),10,Brush(126,151,172,255)),new Point(x,y));
 }
 protected override void OnRender(DrawingContext dc) {
  base.OnRender(dc);double width=ActualWidth,height=ActualHeight;if(width<100||height<60)return;
  const double left=62,top=10,bottom=22;double right=width-8,h=height-top-bottom,pw=right-left;
  dc.DrawRoundedRectangle(Brush(10,18,27,255),null,new Rect(0,0,width,height),5,5);
  double max=ScaleMaximum();
  var grid=new Pen(Brush(62,97,111,90),1);
  for(int i=0;i<=4;i++) {double y=top+h*i/4;dc.DrawLine(grid,new Point(left,y),new Point(right,y));Label(dc,(max*(4-i)/4000).ToString("0.##"),4,y-6);}
  for(int i=0;i<=6;i++) {double x=left+pw*i/6;dc.DrawLine(grid,new Point(x,top),new Point(x,top+h));}
  Label(dc,"Mb/s",4,top+h+6);Label(dc,"-60s",left,top+h+6);Label(dc,"LIVE",right-28,top+h+6);
  double now=samples.Count==0?0:samples[samples.Count-1].Time;
  dc.PushClip(new RectangleGeometry(new Rect(left,top,pw,h)));
  var paths=new List<StreamGeometry>();StreamGeometry geo=null;StreamGeometryContext ctx=null;
  var plotted=Plot(max);
  foreach(var sample in plotted) {
   if(!sample.Rate.HasValue) {if(ctx!=null){ctx.Close();ctx=null;}continue;}
   var p=new Point(right-(now-sample.Time)/60*pw,top+h-Math.Min(sample.Rate.Value/max,1)*h);
   if(ctx==null){geo=new StreamGeometry();paths.Add(geo);ctx=geo.Open();ctx.BeginFigure(p,false,false);}else ctx.LineTo(p,true,false);
  }
  if(ctx!=null)ctx.Close();
  foreach(var path in paths){dc.DrawGeometry(null,new Pen(Brush(36,255,190,18),13),path);dc.DrawGeometry(null,new Pen(Brush(36,255,190,45),7),path);dc.DrawGeometry(null,new Pen(Brush(82,255,212,255),2),path);}
  // Amber caps identify out-of-range samples without flattening the rest of the graph.
  foreach(var sample in samples)if(sample.Rate.HasValue && sample.Rate.Value>max) {
   double x=right-(now-sample.Time)/60*pw;
   dc.DrawLine(new Pen(Brush(255,193,87,255),3),new Point(x-3,top+2),new Point(x+3,top+2));
  }
  if(plotted.Count>0 && plotted[plotted.Count-1].Rate.HasValue) {var tip=plotted[plotted.Count-1];var p=new Point(right-(now-tip.Time)/60*pw,top+h-tip.Rate.Value/max*h);dc.DrawEllipse(Brush(64,255,200,40),null,p,8,8);dc.DrawEllipse(Brush(191,255,237,255),null,p,3,3);}
  foreach(double boundary in boundaries) {
   double x=right-(now-boundary)/60*pw;
   dc.DrawLine(new Pen(Brush(255,55,85,40),9),new Point(x,top),new Point(x,top+h));
   dc.DrawLine(new Pen(Brush(255,72,94,255),2.5),new Point(x,top),new Point(x,top+h));
  }
  dc.Pop();
  if(samples.Count==0)Label(dc,"Waiting for bitrate samples",left+14,top+12);
 }
}
}
