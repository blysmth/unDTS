using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace unDTS {
public sealed class FilenameDisplay : Grid {
 readonly TextBlock original=new TextBlock(), output=new TextBlock();
 public bool IsPulsing {get;private set;}
 public string OriginalName {get;private set;}
 public string OutputName {get;private set;}
 public FilenameDisplay() {
  Height=62;ClipToBounds=true;
  foreach(var text in new[]{original,output}) {text.FontSize=16;text.TextWrapping=TextWrapping.Wrap;text.TextTrimming=TextTrimming.CharacterEllipsis;text.MaxHeight=62;Children.Add(text);}
  ShowIdle();
 }
 public void ShowIdle() {Stop();original.Text="Waiting for your first file";original.Opacity=1;output.Opacity=0;}
 static void Format(TextBlock text,string name,IEnumerable<Tuple<int,int>> ranges,Color color) {
  text.Inlines.Clear();int offset=0;
  foreach(var range in ranges) {if(range.Item1>offset)text.Inlines.Add(new Run(name.Substring(offset,range.Item1-offset)));text.Inlines.Add(new Run(name.Substring(range.Item1,range.Item2)){Foreground=new SolidColorBrush(color),FontWeight=FontWeights.SemiBold});offset=range.Item1+range.Item2;}
  if(offset<name.Length)text.Inlines.Add(new Run(name.Substring(offset)));text.ToolTip=name;
 }
 static List<Tuple<int,int>> OriginalRanges(string name) {
  var ranges=new List<Tuple<int,int>>();foreach(Match m in Converter.CodecLabels.Matches(Path.GetFileNameWithoutExtension(name)))ranges.Add(Tuple.Create(m.Index,m.Length));return ranges;
 }
 static List<Tuple<int,int>> OutputRanges(string source) {
  var ranges=new List<Tuple<int,int>>();int shift=0;
  foreach(var range in OriginalRanges(source)){ranges.Add(Tuple.Create(range.Item1+shift,3));shift+=3-range.Item2;}
  if(ranges.Count==0)ranges.Add(Tuple.Create(Path.GetFileNameWithoutExtension(source).Length+1,3));return ranges;
 }
 static DoubleAnimationUsingKeyFrames Pulse(bool isOutput) {
  var animation=new DoubleAnimationUsingKeyFrames {Duration=TimeSpan.FromSeconds(4),RepeatBehavior=RepeatBehavior.Forever};
  double[] times={0,1,1.6,2.8,3.4,4};double[] values={1,1,0,0,1,1};
  for(int i=0;i<times.Length;i++)animation.KeyFrames.Add(new LinearDoubleKeyFrame(isOutput?1-values[i]:values[i],KeyTime.FromTimeSpan(TimeSpan.FromSeconds(times[i]))));return animation;
 }
 void Stop() {original.BeginAnimation(OpacityProperty,null);output.BeginAnimation(OpacityProperty,null);IsPulsing=false;}
 public void Start(string source,string destination) {
  Stop();OriginalName=Path.GetFileName(source);OutputName=Path.GetFileName(destination);
  Format(original,OriginalName,OriginalRanges(OriginalName),Color.FromRgb(255,102,126));Format(output,OutputName,OutputRanges(OriginalName),Color.FromRgb(100,245,175));
  ToolTip=OriginalName+Environment.NewLine+"to "+OutputName;original.Opacity=1;output.Opacity=0;original.BeginAnimation(OpacityProperty,Pulse(false));output.BeginAnimation(OpacityProperty,Pulse(true));IsPulsing=true;
 }
 public void Complete(string destination) {
  Stop();OutputName=Path.GetFileName(destination);Format(output,OutputName,OutputRanges(OriginalName),Color.FromRgb(100,245,175));original.Opacity=0;output.Opacity=1;ToolTip=OutputName;
 }
 public void Abort() {Stop();original.Opacity=1;output.Opacity=0;ToolTip=OriginalName;}
}
}
