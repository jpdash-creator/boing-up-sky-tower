param([string]$Platform = "PlayMode", [int]$TimeoutSec = 1200)
# Runs the Unity tests headless (PlayMode with graphics so screenshots work) and prints a short summary.
$unity = "E:\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"
$xml = "E:\Unity\logs\$Platform.xml"
$log = "E:\Unity\logs\$Platform.log"
$args2 = @('-batchmode', '-projectPath', 'E:\Code\boing-up-sky-tower', '-runTests', '-testPlatform', $Platform, '-testResults', $xml, '-logFile', $log)
if ($Platform -eq "EditMode") { $args2 += '-nographics' }
$p = Start-Process -FilePath $unity -ArgumentList $args2 -PassThru
if (-not $p.WaitForExit($TimeoutSec * 1000)) { $p.Kill(); "TIMEOUT" } else { "exit " + $p.ExitCode }
Select-String -Path $log -Pattern 'error CS' | Select-Object -First 10 | ForEach-Object { $_.Line }
Select-String -Path $xml -Pattern '<test-run ' | ForEach-Object { $_.Line.Substring(0, [Math]::Min(190, $_.Line.Length)) }
python E:\Code\boing-up-sky-tower\Tools\unity_failures.py $xml
