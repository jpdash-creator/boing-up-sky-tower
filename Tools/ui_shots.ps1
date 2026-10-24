# Visual validation pass: runs the CriticalUX PlayMode tests with screenshots of every screen saved to E:\Unity\logs\shots
# (9x16 and 9x21). Review the PNGs, then fix; the layout audit in the same run fails on overlaps, clipping, small targets.
$unity = "E:\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"
$env:BU_SHOTS = "E:\Unity\logs\shots"
Remove-Item "E:\Unity\logs\shots\*" -ErrorAction SilentlyContinue
Remove-Item "E:\Unity\logs\Shots.xml" -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $unity -ArgumentList @('-batchmode','-projectPath','E:\Code\boing-up-sky-tower','-runTests','-testPlatform','PlayMode','-testCategory','CriticalUX','-testResults','E:\Unity\logs\Shots.xml','-logFile','E:\Unity\logs\Shots.log') -PassThru
if (-not $p.WaitForExit(1500000)) { $p.Kill(); "TIMEOUT" } else { "exit " + $p.ExitCode }
(Get-ChildItem E:\Unity\logs\shots -ErrorAction SilentlyContinue | Measure-Object).Count.ToString() + " screenshots"
python E:\Code\boing-up-sky-tower\Tools\unity_failures.py E:\Unity\logs\Shots.xml
