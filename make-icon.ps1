Add-Type -AssemblyName System.Drawing
$assetDir = Join-Path $PSScriptRoot 'assets'
New-Item -ItemType Directory -Path $assetDir -Force | Out-Null
function New-RoundedPath([float]$x,[float]$y,[float]$width,[float]$height,[float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $radius
    $path.AddArc($x,$y,$d,$d,180,90)
    $path.AddArc(($x+$width-$d),$y,$d,$d,270,90)
    $path.AddArc(($x+$width-$d),($y+$height-$d),$d,$d,0,90)
    $path.AddArc($x,($y+$height-$d),$d,$d,90,90)
    $path.CloseFigure()
    return $path
}
$frames = @()
foreach ($iconSize in @(32,48,128,256)) {
    $bitmap = New-Object System.Drawing.Bitmap($iconSize,$iconSize)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = 'AntiAlias'
    $graphics.ScaleTransform(($iconSize/64.0),($iconSize/64.0))
    $bg = New-RoundedPath 1 1 62 62 16
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(113,85,232))
    $graphics.FillPath($brush,$bg)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White,2.6)
    $one = New-RoundedPath 16 12 23 34 5
    $two = New-RoundedPath 25 20 23 34 5
    $graphics.DrawPath($pen,$one)
    $graphics.DrawPath($pen,$two)
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
    $frames += @{ Size=$iconSize; Data=$stream.ToArray() }
    $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $brush.Dispose(); $pen.Dispose(); $bg.Dispose(); $one.Dispose(); $two.Dispose()
}
$iconStream = [System.IO.File]::Create((Join-Path $assetDir 'AppDeck.ico'))
$writer = New-Object System.IO.BinaryWriter($iconStream)
$writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$frames.Count)
$offset = 6 + $frames.Count * 16
foreach ($frame in $frames) {
    $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([UInt16]1); $writer.Write([UInt16]32); $writer.Write([UInt32]$frame.Data.Length); $writer.Write([UInt32]$offset)
    $offset += $frame.Data.Length
}
foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Data) }
$writer.Dispose(); $iconStream.Dispose()
