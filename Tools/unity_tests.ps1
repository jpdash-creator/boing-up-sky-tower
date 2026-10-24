param([string]$Platform = "PlayMode", [int]$TimeoutSec = 1200)
# Runs the Unity tests headless (PlayMode with graphics so screenshots work) and prints a short summary.
# The project is the checkout this script lives in (so it works from any worktree); logs go to $env:BU_LOGS when set, else E:\Unity\logs.
$root = Split-Path $PSScriptRoot -Parent
$logs = if ($env:BU_LOGS) { $env:BU_LOGS } else { "E:\Unity\logs" }
New-Item -ItemType Directory -Force $logs | Out-Null
$unity = "E:\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"
$xml = "$logs\$Platform.xml"
$log = "$logs\$Platform.log"
$args2 = @('-batchmode', '-projectPath', $root, '-runTests', '-testPlatform', $Platform, '-testResults', $xml, '-logFile', $log)
if ($Platform -eq "EditMode") { $args2 += '-nographics' }
$p = Start-Process -FilePath $unity -ArgumentList $args2 -PassThru
if (-not $p.WaitForExit($TimeoutSec * 1000)) { $p.Kill(); "TIMEOUT" } else { "exit " + $p.ExitCode }
Select-String -Path $log -Pattern 'error CS' | Select-Object -First 10 | ForEach-Object { $_.Line }
Select-String -Path $xml -Pattern '<test-run ' | ForEach-Object { $_.Line.Substring(0, [Math]::Min(190, $_.Line.Length)) }
python "$root\Tools\unity_failures.py" $xml
