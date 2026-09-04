$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]

$proc = Get-Process FormatConverter -ErrorAction Stop | Select-Object -First 1
$cond = New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $proc.Id)
$win = $A::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { throw 'window not found' }

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

# 每次读取都重新查找,排除 UIA 元素缓存(peers 可能随视觉树重建而失效)
$mp4 = Find-TileButton 'MP4'
$mkv = Find-TileButton 'MKV'
Write-Output ("before: MP4=" + (Get-ToggleState $mp4) + " MKV=" + (Get-ToggleState $mkv))

$mkv.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
Start-Sleep -Milliseconds 600
$mp4 = Find-TileButton 'MP4'
$mkv = Find-TileButton 'MKV'
Write-Output ("after click MKV (fresh find): MP4=" + (Get-ToggleState $mp4) + " MKV=" + (Get-ToggleState $mkv))

# 再点回 MP4
$mp4.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
Start-Sleep -Milliseconds 600
$mp4 = Find-TileButton 'MP4'
$mkv = Find-TileButton 'MKV'
Write-Output ("after click MP4 (fresh find): MP4=" + (Get-ToggleState $mp4) + " MKV=" + (Get-ToggleState $mkv))
