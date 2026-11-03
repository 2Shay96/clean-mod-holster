# Original geometric CLEAN C mark. No source images, fonts, trademarks or stock assets.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$iconPath = Join-Path $PSScriptRoot '..\src\app.ico'
$iconBytes = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($iconBytes)
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::FromArgb(16, 9, 11))
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(254, 172, 1), ($size * .13))
    $pen.StartCap = $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawArc($pen, [single]($size*.22), [single]($size*.22), [single]($size*.56), [single]($size*.56), 48, 264)
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,($stream.ToArray())
    $stream.Dispose(); $pen.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i=0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -eq 256) {0} else {$sizes[$i]}
    $writer.Write([byte]$dim); $writer.Write([byte]$dim); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
[System.IO.File]::WriteAllBytes($iconPath, $iconBytes.ToArray())
$writer.Dispose(); $iconBytes.Dispose()
Write-Output "Created original CLEAN icon: $iconPath"
