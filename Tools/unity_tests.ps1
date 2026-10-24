param([string]$Platform = "PlayMode", [int]$TimeoutSec = 1200, [string]$Project = "E:\Code\boing-up-sky-tower", [string]$LogDir = "E:\Unity\logs")
# Runs the Unity tests headless (PlayMode with graphics so screenshots work) and prints a short summary.
# -Project and -LogDir let a second checkout (a git worktree) run its own tests without touching the main checkout's logs.
$unity = "E:\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force $LogDir | Out-Null
$xml = "$LogDir\$Platform.xml"
$log = "$LogDir\$Platform.log"
$args2 = @('-batchmode', '-projectPath', $Project, '-runTests', '-testPlatform', $Platform, '-testResults', $xml, '-logFile', $log)
if ($Platform -eq "EditMode") { $args2 += '-nographics' }
$p = Start-Process -FilePath $unity -ArgumentList $args2 -PassThru
if (-not $p.WaitForExit($TimeoutSec * 1000)) { $p.Kill(); "TIMEOUT" } else { "exit " + $p.ExitCode }
Select-String -Path $log -Pattern 'error CS' | Select-Object -First 10 | ForEach-Object { $_.Line }
Select-String -Path $xml -Pattern '<test-run ' | ForEach-Object { $_.Line.Substring(0, [Math]::Min(190, $_.Line.Length)) }
python "$Project\Tools\unity_failures.py" $xml
