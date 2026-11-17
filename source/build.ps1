$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$framework = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$compiler = Join-Path $framework 'csc.exe'
$refs = @('PresentationCore','PresentationFramework','WindowsBase') | ForEach-Object { '/r:' + (Join-Path $framework ('WPF\' + $_ + '.dll')) }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /out:unDTS.exe /win32manifest:app.manifest /win32icon:unDTS.ico /resource:unDTS.ico,unDTS.ico /resource:MainWindow.xaml,MainWindow.xaml /resource:Logo.png,Logo.png @refs /r:System.Xaml.dll App.cs Engine.cs BitrateGraph.cs FilenameDisplay.cs Entry.cs
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Write-Host 'Built unDTS.exe'
