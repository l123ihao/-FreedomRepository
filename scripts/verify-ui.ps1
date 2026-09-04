$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]

$proc = Get-Process FormatConverter -ErrorAction Stop | Select-Object -First 1
$cond = New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $proc.Id)
$win = $A::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { throw 'window not found' }

$rect = $win.Current.BoundingRectangle
Write-Output ("WINDOW: {0}x{1} @ {2},{3}" -f [int]$rect.Width, [int]$rect.Height, [int]$rect.X, [int]$rect.Y)

$all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$names = @($all | ForEach-Object { $_.Current.Name } | Where-Object { $_ })

Write-Output ("banner title: " + [bool]($names -contains '万能格式转换器'))
Write-Output ("banner subtitle: " + [bool]($names -contains '把文件直接拖到目标格式上,即刻转换'))
Write-Output ("about button: " + [bool]($names -contains '关于'))
Write-Output ("grid header: " + [bool]($names -contains '目标格式 — 点选格式,或直接把文件拖到格式上'))
Write-Output ("queue header: " + [bool]($names -contains '转换队列'))
Write-Output ("empty-state: " + [bool]($names -contains '暂无文件 — 把文件拖到上方的目标格式上,或点「选择文件」'))
Write-Output ("init status: " + [bool]($names -contains '将文件拖到对应格式磁贴上即可开始转换;或先选格式再点「选择文件」。'))

$tiles = 'MP4','MKV','AVI','MOV','WEBM','MP3','WAV','FLAC','M4A','AAC','OGG','DOCX','TXT','PDF','MD','HTML','PNG','JPG','WEBP','BMP','GIF','ICO'
$missing = @()
foreach ($t in $tiles) { if ($names -notcontains $t) { $missing += $t } }
Write-Output ("missing tile texts: " + ($(if ($missing) { $missing -join ',' } else { '(none)' })))
Write-Output ("PPTX tile absent: " + [bool]($names -notcontains 'PPTX'))

foreach ($n in @('开始转换','重试失败项')) {
    $c = New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, $n)
    $b = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
    Write-Output ($n + " hidden initially: " + [bool]($null -eq $b))
}
$c = New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, '取消')
$cancel = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
if ($cancel) { Write-Output ("cancel enabled (should be False): " + $cancel.Current.IsEnabled) }

function Find-TileButton([string]$ext) {
    $c = New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, $ext)
    $t = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
    if (-not $t) { return $null }
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $el = $t
    while ($el -and $el.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button) {
        $el = $walker.GetParent($el)
    }
    return $el
}
function Get-ToggleState($el) {
    if (-not $el) { return 'no-element' }
    return $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState
}

$mp4 = Find-TileButton 'MP4'
$mkv = Find-TileButton 'MKV'
Write-Output ("before: MP4=" + (Get-ToggleState $mp4) + " MKV=" + (Get-ToggleState $mkv))
if ($mkv) {
    $mkv.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 500
    Write-Output ("after click MKV: MP4=" + (Get-ToggleState $mp4) + " MKV=" + (Get-ToggleState $mkv))
}
