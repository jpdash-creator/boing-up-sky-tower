# Visual validation pass: runs the CriticalUX PlayMode tests with screenshots of every screen saved to <logs>\shots
# (9x16 and 9x21). Review the PNGs, then fix; the layout audit in the same run fails on overlaps, clipping, small targets.
# The project is the checkout this script lives in; logs go to $env:BU_LOGS when set, else E:\Unity\logs.
$root = Split-Path $PSScriptRoot -Parent
$logs = if ($env:BU_LOGS) { $env:BU_LOGS } else { "E:\Unity\logs" }
$unity = "E:\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"
$env:BU_SHOTS = "$logs\shots"
New-Item -ItemType Directory -Force $env:BU_SHOTS | Out-Null
Remove-Item "$logs\shots\*" -ErrorAction SilentlyContinue
Remove-Item "$logs\Shots.xml" -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $unity -ArgumentList @('-batchmode','-projectPath',$root,'-runTests','-testPlatform','PlayMode','-testCategory','CriticalUX','-testResults',"$logs\Shots.xml",'-logFile',"$logs\Shots.log") -PassThru
if (-not $p.WaitForExit(1500000)) { $p.Kill(); "TIMEOUT" } else { "exit " + $p.ExitCode }
(Get-ChildItem "$logs\shots" -ErrorAction SilentlyContinue | Measure-Object).Count.ToString() + " screenshots"
python "$root\Tools\unity_failures.py" "$logs\Shots.xml"
