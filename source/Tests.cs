using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using unDTS;
class Tests {
 static string root, ffmpeg;
 static int Run(string args) {var p=Process.Start(new ProcessStartInfo(ffmpeg,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true});string output=p.StandardError.ReadToEnd();p.WaitForExit();File.AppendAllText(Path.Combine(root,"fixture.log"),output);return p.ExitCode;}
 static void Check(bool yes,string name){if(!yes)throw new Exception("FAILED: "+name);Console.WriteLine("PASS: "+name);}
 static string Inspect(string path){var p=Process.Start(new ProcessStartInfo(ffmpeg,"-hide_banner -i "+Converter.Quote(path)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true});string result=p.StandardError.ReadToEnd();p.WaitForExit();return result;}
 static void Main(string[] args) {try{RunTests(args).GetAwaiter().GetResult();}catch(Exception ex){Console.WriteLine(ex);Environment.ExitCode=1;}}
 static async Task RunTests(string[] args) {
  root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);ffmpeg=Path.GetFullPath(args[1]);
  foreach(string label in new[]{"DTS","DTS.5.1","DTS 5.1","DTS_5.1","DTS-HD","DTS-HD.5.1","DTS-HD 5.1","DTS-HD.MA.5.1","DTS-HD.MA 5.1","dts-hd.ma.5.1","DTS-HD.MA?5.1","DTS?5.1"}) {
   Check(Path.GetFileName(Converter.OutputName(Path.Combine(root,"Movie."+label+".1080p.mkv")))=="Movie.AC3.1080p.mkv","Replace filename label "+label);
  }
  Check(Path.GetFileName(Converter.OutputName(Path.Combine(root,"Movie.AAC.mkv")))=="Movie.AAC_AC3.mkv","Fallback suffix when no DTS label");
  Check(Path.GetFileName(Converter.OutputName(Path.Combine(root,"ADTS.Movie.mkv")))=="ADTS.Movie_AC3.mkv","Do not replace DTS inside another word");
  Check(Path.GetFileName(Converter.OutputName(Path.Combine(root,"Movie.DTS-HD.MA.5.1.DTS.mkv")))=="Movie.AC3.AC3.mkv","Replace all labels and consume longest match");
  string source=Path.Combine(root,"Test & film ! café.mkv");
  string sub=Path.Combine(root,"caption.srt");File.WriteAllText(sub,"1\r\n00:00:00,100 --> 00:00:02,000\r\nTest subtitle\r\n");
  Check(Run("-y -f lavfi -i testsrc2=size=160x90:rate=10 -f lavfi -i sine=frequency=440:sample_rate=48000 -f lavfi -i sine=frequency=880:sample_rate=48000 -i "+Converter.Quote(sub)+" -map 0:v -map 1:a -map 2:a -map 3:s -t 4 -c:v mpeg4 -c:a dca -metadata:s:a:0 title=FirstAudio -metadata:s:a:1 title=SecondAudio -disposition:a:0 0 -disposition:a:1 default -strict -2 -c:s srt "+Converter.Quote(source))==0,"Create two DTS tracks, video and subtitle fixture");
  byte[] original=File.ReadAllBytes(source);int progress=0;double position=0;bool bitrateSeen=false;
  var dtsInspection=await Converter.InspectAudio(ffmpeg,source,CancellationToken.None);
  Check(dtsInspection.Readable&&dtsInspection.HasDts,"Audio inspection detects actual DTS codec");
  string fakeDts=Path.Combine(root,"NoDTS.DTS-HD.MA.5.1.mkv");
  Check(Run("-y -f lavfi -i sine=sample_rate=48000 -t 1 -c:a aac "+Converter.Quote(fakeDts))==0,"Create AAC-only file with misleading DTS filename");
  var aacInspection=await Converter.InspectAudio(ffmpeg,fakeDts,CancellationToken.None);
  Check(aacInspection.Readable&&!aacInspection.HasDts,"Reject AAC-only file even when its filename says DTS");
  string silent=Path.Combine(root,"NoAudio.mkv");
  Check(Run("-y -f lavfi -i testsrc2=size=160x90:rate=10 -t 1 -c:v mpeg4 -an "+Converter.Quote(silent))==0,"Create video with no audio track");
  var silentInspection=await Converter.InspectAudio(ffmpeg,silent,CancellationToken.None);
  Check(silentInspection.Readable&&!silentInspection.HasDts,"Reject file with no audio tracks");
  Action<Telemetry> update=delegate(Telemetry t){progress++;position=Math.Max(position,t.Position);if(t.BitrateKbps.HasValue&&t.BitrateKbps.Value>0)bitrateSeen=true;};
  var basic=await Converter.Run(ffmpeg,source,false,CancellationToken.None,delegate{},update);
  Check(basic.Success&&File.Exists(basic.Output),"Script-compatible conversion completes");
  string info=Inspect(basic.Output);
  Check(info.Contains("Audio: ac3")&&info.Contains("640 kb/s")&&!info.Contains("Audio: dts"),"AC3 at 640 kb/s");
  Check(info.Contains("Video: mpeg4")&&info.Contains("Subtitle: subrip"),"Video and subtitles retained");
  Check(System.Text.RegularExpressions.Regex.Matches(info,"Audio:").Count==1,"Default automatic selection matches batch script");
  Check(info.Contains("SecondAudio")&&!info.Contains("FirstAudio"),"Automatic selection prefers the second audio track marked default");
  Check(progress>0&&position>0,"Machine-readable progress updates arrive");
  Check(bitrateSeen,"Positive measured output bitrate arrives from real FFmpeg conversion");
  var all=await Converter.Run(ffmpeg,source,true,CancellationToken.None,delegate{},delegate{});
  Check(all.Success&&all.Output!=basic.Output&&File.Exists(basic.Output),"Existing output is never overwritten");
  Check(System.Text.RegularExpressions.Regex.Matches(Inspect(all.Output),"Audio: ac3").Count==2,"All-tracks option converts both audio tracks");
  Check(File.ReadAllBytes(source).SequenceEqual(original),"Source file remains byte-identical");
  string bad=Path.Combine(root,"corrupt.mkv");File.WriteAllText(bad,"not a matroska file");
  Check(!(await Converter.InspectAudio(ffmpeg,bad,CancellationToken.None)).Readable,"Unreadable media is distinguished from missing DTS audio");
  var failed=await Converter.Run(ffmpeg,bad,false,CancellationToken.None,delegate{},delegate{});
  Check(!failed.Success&&!File.Exists(Converter.OutputName(bad)),"Corrupt input fails without final output");
  string slow=Path.Combine(root,"cancel.mkv");
  Check(Run("-y -f lavfi -i anullsrc=r=48000:cl=5.1 -t 180 -c:a dca -strict -2 "+Converter.Quote(slow))==0,"Create cancellation fixture");
  using(var cts=new CancellationTokenSource()) {
   var cancelled=await Converter.Run(ffmpeg,slow,false,cts.Token,delegate(string line){if(line.Contains("Duration:"))cts.Cancel();},delegate{});
   Check(cancelled.Cancelled,"Cancellation terminates active conversion");
  }
  Check(Directory.GetFiles(root,".unDTS-*.partial.mkv").Length==0,"Incomplete temporary outputs cleaned up");
  Check(!File.Exists(Path.Combine(root,"cancel_AC3.mkv")),"Cancellation leaves no final output");
  Check(Converter.Quote("C:\\folder\\")=="\"C:\\folder\\\\\"","Windows argument trailing backslash quoting");
  Console.WriteLine("All integration checks passed.");
 }
}
