param([string]$Version)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$versionFile = Join-Path $projectRoot 'VERSION'
if ([String]::IsNullOrWhiteSpace($Version)) { $Version = (Get-Content -Raw -LiteralPath $versionFile).Trim() }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid AppDeck version: $Version" }
$versionParts = $Version.Split('.')
$assemblyVersion = "$($versionParts[0]).$($versionParts[1]).$($versionParts[2]).0"
$generatedDir = Join-Path $projectRoot '.build'
New-Item -ItemType Directory -Force -Path $generatedDir | Out-Null
$generatedSource = Join-Path $generatedDir 'AppInfo.g.cs'
@"
using System.Reflection;
[assembly: AssemblyTitle("AppDeck")]
[assembly: AssemblyProduct("AppDeck")]
[assembly: AssemblyCompany("jaydenswork")]
[assembly: AssemblyCopyright("Copyright © jaydenswork 2026")]
[assembly: AssemblyVersion("$assemblyVersion")]
[assembly: AssemblyFileVersion("$assemblyVersion")]
[assembly: AssemblyInformationalVersion("$Version")]
namespace AppDeck { internal static class AppInfo { public const string Version = "$Version"; } }
"@ | Set-Content -LiteralPath $generatedSource -Encoding UTF8
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $projectRoot 'AppDeck.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName) + $generatedSource
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /debug:pdbonly ('/win32icon:' + (Join-Path $projectRoot 'assets\AppDeck.ico')) ('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')) /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:System.Xml.Linq.dll ('/out:' + $output) $sources
if ($LASTEXITCODE -ne 0) { throw 'AppDeck compilation failed' }
$test = Start-Process -FilePath $output -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if ($test.ExitCode -ne 0) { throw 'AppDeck self-test failed; see data\self-test.txt' }
Get-FileHash -LiteralPath $output -Algorithm SHA256
