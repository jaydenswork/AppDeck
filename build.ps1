$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $projectRoot 'AppDeck.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /debug:pdbonly ('/win32icon:' + (Join-Path $projectRoot 'assets\AppDeck.ico')) ('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')) /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:System.Xml.Linq.dll ('/out:' + $output) $sources
if ($LASTEXITCODE -ne 0) { throw 'AppDeck compilation failed' }
$test = Start-Process -FilePath $output -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if ($test.ExitCode -ne 0) { throw 'AppDeck self-test failed; see data\self-test.txt' }
Get-FileHash -LiteralPath $output -Algorithm SHA256
