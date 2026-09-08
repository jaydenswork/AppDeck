param([string]$Version)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([String]::IsNullOrWhiteSpace($Version)) { $Version = (Get-Content -Raw -LiteralPath (Join-Path $root 'VERSION')).Trim() }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid AppDeck version: $Version" }

& (Join-Path $root 'build.ps1') -Version $Version
if (-not $?) { throw 'AppDeck build failed' }

$isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
$iscc = if ($isccCommand) { $isccCommand.Source } else { Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe' }
if (-not (Test-Path -LiteralPath $iscc)) { throw 'Inno Setup 6 was not found. Install it from https://jrsoftware.org/isinfo.php' }

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
& $iscc "-dMyAppVersion=$Version" (Join-Path $PSScriptRoot 'AppDeck.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }

$setup = Join-Path $dist "AppDeck-Setup-$Version.exe"
if (-not (Test-Path -LiteralPath $setup)) { throw "Installer was not created: $setup" }
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $setup).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($setup))" | Set-Content -LiteralPath "$setup.sha256" -Encoding ASCII
Get-Item -LiteralPath $setup, "$setup.sha256"
