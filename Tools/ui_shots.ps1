param([string]$Project = "E:\Code\boing-up-sky-tower", [string]$LogDir = "E:\Unity\logs")
# Visual validation pass: runs the CriticalUX PlayMode tests with screenshots of every screen saved to $LogDir\shots
# (9x16 and 9x21). Review the PNGs, then fix; the layout audit in the same run fails on overlaps, clipping, small targets.
# -Project and -LogDir let a second checkout (a git worktree) run its own audit.
$unity = "E:\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force "$LogDir\shots" | Out-Null
$env:BU_SHOTS = "$LogDir\shots"
Remove-Item "$LogDir\shots\*" -ErrorAction SilentlyContinue
Remove-Item "$LogDir\Shots.xml" -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $unity -ArgumentList @('-batchmode','-projectPath',$Project,'-runTests','-testPlatform','PlayMode','-testCategory','CriticalUX','-testResults',"$LogDir\Shots.xml",'-logFile',"$LogDir\Shots.log") -PassThru
if (-not $p.WaitForExit(1500000)) { $p.Kill(); "TIMEOUT" } else { "exit " + $p.ExitCode }
(Get-ChildItem "$LogDir\shots" -ErrorAction SilentlyContinue | Measure-Object).Count.ToString() + " screenshots"
python "$Project\Tools\unity_failures.py" "$LogDir\Shots.xml"
