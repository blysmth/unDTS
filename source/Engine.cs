using System;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace unDTS {
public sealed class Telemetry {
    public double Duration, Position, Speed;
    public long Bytes;
    public double? BitrateKbps;
    public double ElapsedSeconds;
    public double Percent { get { return Duration > 0 ? Math.Min(99.9, Math.Max(0, Position / Duration * 100)) : 0; } }
}
public sealed class ConversionResult {
    public string Output;
    public bool Success, Cancelled;
    public int ExitCode;
}
public sealed class AudioInspection {
    public bool Readable, HasDts;
    public string Detail;
}
// FFmpeg can repeat timestamps or buffer container writes. Keep the baseline
// until both counters advance so those updates neither divide by tiny/zero
// intervals nor turn temporarily unchanged file sizes into zero-bitrate dips.
internal sealed class BitrateSampler {
    double position;
    long bytes;
    internal double? Add(double currentPosition,long currentBytes) {
        if(double.IsNaN(currentPosition)||double.IsInfinity(currentPosition)||currentPosition<0||currentBytes<0)return null;
        if(currentPosition<position||currentBytes<bytes){position=currentPosition;bytes=currentBytes;return null;}
        double delta=currentPosition-position;
        if(delta<0.25||currentBytes==bytes)return null;
        double rate=8.0*(currentBytes-bytes)/delta/1000.0;
        position=currentPosition;bytes=currentBytes;
        return rate;
    }
}
public static class Converter {
    public static async Task<AudioInspection> InspectAudio(string ffmpeg,string input,CancellationToken token) {
        token.ThrowIfCancellationRequested();
        var start=new ProcessStartInfo(ffmpeg,"-hide_banner -nostdin -i "+Quote(input)) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,StandardErrorEncoding=Encoding.UTF8};
        using(var process=new Process {StartInfo=start}) {
            process.Start();
            using(token.Register(delegate {try {if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}})) {
                var stderr=process.StandardError.ReadToEndAsync();var stdout=process.StandardOutput.ReadToEndAsync();
                await Task.WhenAll(stderr,stdout,Task.Run(delegate {process.WaitForExit();}));
                token.ThrowIfCancellationRequested();string report=stderr.Result;
                // An input-only invocation deliberately ends with 'no output specified'.
                // Inspect codec descriptors, never the filename/title/metadata tags.
                bool readable=Regex.IsMatch(report,@"(?m)^Input #0,");
                bool dts=Regex.IsMatch(report,@"(?m)^\s*Stream #0:\d+[^\r\n]*\bAudio:\s*dts(?:\s|,|$)",RegexOptions.IgnoreCase);
                return new AudioInspection {Readable=readable,HasDts=dts,Detail=readable?(dts?"DTS audio detected":"Does not contain any DTS audio to convert."):report.Trim()};
            }
        }
    }
    public static readonly Regex CodecLabels = new Regex(@"(?<![A-Za-z0-9])DTS(?:[-._ ]HD(?:[-._ ]MA)?)?(?:[^A-Za-z0-9]?5\.1)?(?![A-Za-z0-9])", RegexOptions.IgnoreCase);
    public static string Quote(string value) {
        // Windows CommandLineToArgvW rules; never invoke cmd.exe.
        return "\"" + Regex.Replace(value, "(\\\\*)\"", "$1$1\\\"") .TrimEnd('\\') + new string('\\', value.Length - value.TrimEnd('\\').Length) + new string('\\', value.Length - value.TrimEnd('\\').Length) + "\"";
    }
    public static string OutputName(string input) {
        string name = Path.GetFileNameWithoutExtension(input);
        // Longest codec label first; accept a punctuation/space separator before 5.1.
        string renamed = CodecLabels.Replace(name, "AC3");
        if (renamed == name) renamed = name + "_AC3";
        string stem = Path.Combine(Path.GetDirectoryName(input), renamed);
        string output = stem + ".mkv";
        int n = 2;
        while (File.Exists(output) || Directory.Exists(output)) output = stem + " (" + n++ + ").mkv";
        return output;
    }
    public static async Task<ConversionResult> Run(string ffmpeg, string input, bool allTracks, CancellationToken token, Action<string> log, Action<Telemetry> progress) {
        token.ThrowIfCancellationRequested();
        string output = OutputName(input);
        string temp = Path.Combine(Path.GetDirectoryName(input), ".unDTS-" + Guid.NewGuid().ToString("N") + ".partial.mkv");
        string args = "-hide_banner -nostdin -n -i " + Quote(input) + (allTracks ? " -map 0 -c copy -c:a ac3" : " -c:v copy -c:a ac3 -c:s copy") + " -b:a 640k -progress pipe:1 -nostats " + Quote(temp);
        log("Output: " + output);
        log("ffmpeg " + args);
        Telemetry state = new Telemetry();
        Stopwatch elapsed = Stopwatch.StartNew();
        var bitrateSampler = new BitrateSampler();
        object gate = new object();
        var start = new ProcessStartInfo(ffmpeg, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardErrorEncoding = Encoding.UTF8, StandardOutputEncoding = Encoding.UTF8 };
        bool committed = false;
        try {
            using (var process = new Process { StartInfo = start }) {
                process.Start();
                using (token.Register(delegate { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) {} catch (System.ComponentModel.Win32Exception) {} })) {
                    Task err = Task.Run(async delegate {
                        string line;
                        while ((line = await process.StandardError.ReadLineAsync()) != null) {
                            log(line);
                            var m = Regex.Match(line, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
                            if (m.Success) lock(gate) state.Duration = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 3600 + double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) * 60 + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                        }
                    });
                    Task stdout = Task.Run(async delegate {
                        string line;
                        while ((line = await process.StandardOutput.ReadLineAsync()) != null) {
                            int i = line.IndexOf('='); if (i < 0) continue;
                            string key = line.Substring(0,i), val = line.Substring(i+1);
                            lock (gate) {
                                double number;
                                if (key == "out_time_us" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) state.Position = number / 1000000;
                                if (key == "speed" && double.TryParse(val.TrimEnd('x'), NumberStyles.Float, CultureInfo.InvariantCulture, out number)) state.Speed = number;
                                long bytes; if (key == "total_size" && long.TryParse(val, out bytes)) state.Bytes = bytes;
                                if (key == "progress") {
                                    double? bitrate = bitrateSampler.Add(state.Position,state.Bytes);
                                    progress(new Telemetry { Duration=state.Duration, Position=state.Position, Speed=state.Speed, Bytes=state.Bytes, BitrateKbps=bitrate, ElapsedSeconds=elapsed.Elapsed.TotalSeconds });
                                }
                            }
                        }
                    });
                    await Task.WhenAll(err, stdout, Task.Run(delegate { process.WaitForExit(); }));
                    if (token.IsCancellationRequested) return new ConversionResult { Cancelled=true, ExitCode=process.ExitCode };
                    if (process.ExitCode != 0) return new ConversionResult { ExitCode=process.ExitCode };
                    if (!File.Exists(temp) || new FileInfo(temp).Length == 0) throw new IOException("FFmpeg returned no output file.");
                    token.ThrowIfCancellationRequested();
                    // Resolve conflicts again immediately before committing; Move never overwrites.
                    while (true) {
                        output = OutputName(input);
                        try { File.Move(temp, output); break; }
                        catch (IOException) { if (!File.Exists(output) && !Directory.Exists(output)) throw; }
                    }
                    committed=true;
                    log("Completed: " + output);
                    return new ConversionResult { Success=true, Output=output, ExitCode=0 };
                }
            }
        } finally {
            if (!committed && File.Exists(temp)) {
                try { File.Delete(temp); }
                catch (Exception ex) { log("Could not remove incomplete output: " + temp + " (" + ex.Message + ")"); }
            }
        }
    }
}
}
