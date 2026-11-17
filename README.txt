unDTS 1.8 — Windows x64
Convert you mkv DTS audio to AC3
================================

Extract the ZIP and run unDTS.exe. Keep unDTS.exe.config beside it.
Drop MKV files/folders into the app and click Convert queue.
Uses your existing FFmpeg: beside the app, in PATH, or selected when prompted.
Windows 10/11 x64 with .NET Framework 4.8. No Python or installation required.

CONVERSION
Default: Audio track marked default (otherwise the most channels), plus the first subtitle track.
Original FFmpeg automatic stream selection is restored, including video.
Video/subtitles are copied; audio is encoded to AC3 at 640 kb/s.

The all-tracks checkbox instead includes every stream, copies video/subtitles/
attachments, and converts every audio track to AC3 at 640 kb/s.
Audio is converted regardless of its source codec, not only DTS.

FILENAMES AND ANIMATION
DTS, DTS.5.1, DTS-HD, DTS-HD.5.1 and DTS-HD.MA.5.1 labels are replaced with
AC3, case-insensitively. Other separators before 5.1 are accepted too.
The longest label is replaced; DTS embedded inside another word is ignored.
If no label matches, _AC3 is appended to the filename stem.
Existing outputs are preserved with numbered names such as Movie.AC3 (2).mkv.

During conversion, the progress panel crossfades every four seconds between
the original filename with matched DTS labels in red, and the expected output
filename with replacement AC3 labels in green. When no DTS label matches,
the added AC3 suffix is green. Hover to see the full names.
On success, the animation stops on the actual final output filename, including
any number added to avoid a collision. On failure/cancellation, the original
filename remains visible.

PROGRESS AND WINDOW
The neon graph shows the last 60 seconds of total output bitrate in Mb/s:
video + audio + container overhead, measured per interval of media time.
This varies independently of the AC3 audio setting of 640 kb/s.
Percentage, elapsed time, estimated remaining time, speed and size remain visible.

The window fits its contents at startup. Expanding the FFmpeg log grows it;
collapsing restores the prior height/position. Growth is capped at the current
monitor's work area. Short screens retain scrolling. Maximized stays maximized.

SAFETY AND BATCHES
Originals are never modified. A unique temporary MKV is renamed only after
successful conversion. Stopping removes the incomplete result. Crashes or
power loss may leave .unDTS-*.partial.mkv files in the source folder.
Folders add their immediate MKVs, not subfolders. Files added while running
wait for the next batch. Remove and re-add a failed/stopped item to retry it.
Save full log exports the session log; the on-screen log is capped.

SOURCE
Source and integration/UI checks are included. Run source\build.ps1 in
PowerShell to rebuild with the Windows .NET Framework compiler. No NuGet needed.
The application is unsigned. FFmpeg is separate and is not distributed here.
Official FFmpeg download information: https://ffmpeg.org/download.html

Permission is granted to use, modify and redistribute the included unDTS
source and executable. Provided without warranty.

GRAPH COMPLETION MARKERS (1.5)
A red vertical bar marks each completed conversion. History is retained across
files and batches, with a break between traces for different files. Samples and
markers scroll out together after 60 seconds of graph history.

DTS AUDIO CHECK (1.6)
Files are inspected as they are added. Only files containing at least one DTS
audio stream are queued for conversion; DTS-HD profiles are included. Codec
detection uses the actual stream information, not words in the filename.
Files without DTS are marked Rejected: no DTS audio, with the explanation
Does not contain any DTS audio to convert. in the tooltip and session log.
Rejected inputs stay visible for review but produce no output. Unreadable
files get a separate rejection reason. Clear finished removes rejected rows.
If FFmpeg has not been located, Convert queue prompts for it before checking.

SPIKE-RESISTANT GRAPH (1.7)
An isolated excessive bitrate reading no longer expands the vertical axis.
Higher ranges require two consecutive valid readings within the same file.
Single spikes are capped at the top with an amber mark. The live numeric
bitrate still shows the actual measurement. Sustained increases still expand
the range. At startup the first reading waits for a second reading before
establishing a range above 1 Mb/s. Completion bars remain red.

The approved graffiti logo is embedded in the executable; no external image is required.

The embedded logo is stored at 320 x 107 pixels (60,019 bytes), matching its
header display size instead of bundling the full-resolution artwork.

WINDOWS ICON
Matching graffiti U/audio-bars icon is embedded for Explorer, the title bar
and taskbar, with 16/24/32/48/64/128/256-pixel frames. No external icon needed.

GRAPH CONTINUITY AND SMOOTHING (1.8)
Repeated media timestamps and buffered container writes wait for a usable
bitrate interval rather than creating zero-value dips. The graph bridges
brief missing updates up to two seconds between valid readings; longer gaps
and explicit file boundaries stay separate. Gentle time-based smoothing
(0.75-second response) applies to the line only. The numeric bitrate remains
the measured interval value. Extreme spikes remain capped before smoothing
so they cannot leave a large tail or expand the axis on their own.
