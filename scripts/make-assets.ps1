# Generate app.ico from a source image (center-crop square -> rounded corners -> PNG sizes -> ICO container).
# Pure ASCII: PowerShell 5.1 reads no-BOM scripts as GBK, so no non-ASCII text in this file.
# Author-local tool only: the generated Assets are committed, so cloners do not need to run this.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File D:/test/scripts/make-assets.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = 'C:\Users\123456\Desktop\598955baeab2f433b4c8d8507203ad1e.jpg'
$icoOut = 'D:\test\src\FormatConverter.App\Assets\app.ico'

function New-RoundedPath([int]$size, [int]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
    $path.AddArc(0, $size - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

$srcImage = [System.Drawing.Image]::FromFile($source)
$side = [Math]::Min($srcImage.Width, $srcImage.Height)
$x = [int](($srcImage.Width - $side) / 2)
$y = [int](($srcImage.Height - $side) / 2)

# 1) center-crop to square
$crop = New-Object System.Drawing.Bitmap($side, $side)
$g = [System.Drawing.Graphics]::FromImage($crop)
$g.DrawImage($srcImage,
    (New-Object System.Drawing.Rectangle(0, 0, $side, $side)),
    (New-Object System.Drawing.Rectangle($x, $y, $side, $side)),
    [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$srcImage.Dispose()

# 2) resize to each size with rounded corners (radius = 12.5% of side) -> PNG bytes
$sizes = @(256, 64, 48, 32, 16)
$pngs = New-Object System.Collections.ArrayList
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g2 = [System.Drawing.Graphics]::FromImage($bmp)
    $g2.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.Clear([System.Drawing.Color]::Transparent)
    $g2.SetClip((New-RoundedPath $s ([int]($s * 0.125))))
    $g2.DrawImage($crop, 0, 0, $s, $s)
    $g2.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    [void]$pngs.Add(@($s, $ms.ToArray()))
    $bmp.Dispose()
}
$crop.Dispose()

# 3) assemble ICO container (PNG entries: width/height byte 0 means 256)
$count = $pngs.Count
$offset = 6 + 16 * $count
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([uint16]0)   # reserved
$bw.Write([uint16]1)   # type: icon
$bw.Write([uint16]$count)
foreach ($entry in $pngs) {
    $s = $entry[0]; $data = $entry[1]
    $wh = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$wh)    # width
    $bw.Write([byte]$wh)    # height
    $bw.Write([byte]0)      # palette count
    $bw.Write([byte]0)      # reserved
    $bw.Write([uint16]1)    # planes
    $bw.Write([uint16]32)   # bpp
    $bw.Write([uint32]$data.Length)
    $bw.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($entry in $pngs) { $bw.Write($entry[1]) }
$bw.Flush()
$total = $out.Length
[System.IO.File]::WriteAllBytes($icoOut, $out.ToArray())
$bw.Dispose()
Write-Output ("app.ico written: {0} bytes, {1} PNG entries" -f $total, $count)
