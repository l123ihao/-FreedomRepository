# Download ffmpeg/ffprobe (win32-x64, gyan.dev GPL 6.1.1 essentials) from the
# npmmirror binary mirror (fast in mainland China; the gyan.dev origin is also
# listed in THIRD-PARTY-LICENSES.txt). The binaries are NOT committed to the
# repository - run this script once after cloning.
#
# Usage: powershell -ExecutionPolicy Bypass -File scripts\fetch-ffmpeg.ps1
$ErrorActionPreference = "Stop"

$Root = Split-Path $PSScriptRoot -Parent
$Base = "https://registry.npmmirror.com/-/binary/ffmpeg-static/b6.1.1"
$Targets = @(
    @{ Name = "ffmpeg.exe";  Url = "$Base/ffmpeg-win32-x64.gz" },
    @{ Name = "ffprobe.exe"; Url = "$Base/ffprobe-win32-x64.gz" }
)

# 1. Download and gunzip into the App folder (csproj copies them to output/publish).
$AppDir = Join-Path $Root "src\FormatConverter.App\ffmpeg"
New-Item -ItemType Directory -Force -Path $AppDir | Out-Null

foreach ($t in $Targets) {
    $gz = Join-Path $env:TEMP ($t.Name + ".gz")
    $out = Join-Path $AppDir $t.Name
    if (Test-Path $out) {
        Write-Host ("exists, skip: " + $out)
        continue
    }
    Write-Host ("Downloading " + $t.Name + " ...")
    Invoke-WebRequest -Uri $t.Url -OutFile $gz
    $in = [System.IO.File]::OpenRead($gz)
    $fs = [System.IO.File]::Create($out)
    try {
        $g = New-Object System.IO.Compression.GZipStream($in, [System.IO.Compression.CompressionMode]::Decompress)
        $g.CopyTo($fs)
        $g.Dispose()
    } finally {
        $fs.Dispose()
        $in.Dispose()
    }
    Remove-Item $gz -Force
    Write-Host ("  -> " + $out)
}

# 2. Integration tests resolve ffmpeg from the test bin folder (BaseDirectory\ffmpeg).
$TestBin = Join-Path $Root "src\Tests\FormatConverter.Core.Tests\bin\Release\net10.0\ffmpeg"
New-Item -ItemType Directory -Force -Path $TestBin | Out-Null
Copy-Item (Join-Path $AppDir "ffmpeg.exe") $TestBin -Force
Copy-Item (Join-Path $AppDir "ffprobe.exe") $TestBin -Force
Write-Host ("Copied to test bin: " + $TestBin)
Write-Host "Done."
