# 发布打包脚本:自包含 win-x64 文件夹 + zip
# 用法: powershell -ExecutionPolicy Bypass -File publish.ps1
$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
# 优先用本机固定 SDK;找不到则退回 PATH 里的 dotnet
$dotnet = "D:\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }
$outDir = Join-Path $root "publish"
$appDir = Join-Path $outDir "FormatConverter-win-x64"
$zipPath = Join-Path $outDir "万能格式转换器-win-x64.zip"

# 1. 发布(自包含,WPF 不支持裁剪,保持 PublishTrimmed=false)
#    国内网络:走华为云 NuGet 镜像并关闭 NuGetAudit(直连 nuget.org 会卡死)
& $dotnet publish (Join-Path $root "src\FormatConverter.App\FormatConverter.App.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:PublishTrimmed=false `
    -p:NuGetAudit=false `
    --source "https://repo.huaweicloud.com/repository/nuget/v3/index.json" `
    -o $appDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败 (exit $LASTEXITCODE)" }

# 2. 附带许可证与说明
Copy-Item (Join-Path $root "THIRD-PARTY-LICENSES.txt") $appDir -Force
Copy-Item (Join-Path $root "README.md") $appDir -Force

# 3. 打包 zip(覆盖旧包)
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $appDir "*") -DestinationPath $zipPath

Write-Host ""
Write-Host "完成:"
Write-Host "  发布目录: $appDir"
Write-Host "  压缩包:   $zipPath"
Write-Host "  大小:     $([math]::Round((Get-Item $zipPath).Length / 1MB, 1)) MB"
