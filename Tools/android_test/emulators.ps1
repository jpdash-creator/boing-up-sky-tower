<#
Android emulator helper for Boing Up: Sky Tower (see EMULATORS.md).

  .\emulators.ps1 list
  .\emulators.ps1 create                      # (re)creates the standard AVDs
  .\emulators.ps1 start  bu_9x16_a34 [-Headless]   # boots and waits until Android is ready
  .\emulators.ps1 install bu_9x16_a34 [-Apk path]
  .\emulators.ps1 launch bu_9x16_a34 [-Clear]
  .\emulators.ps1 shot   bu_9x16_a34 [-Out E:\Unity\shots\emu_x.png]
  .\emulators.ps1 stop   bu_9x16_a34
  .\emulators.ps1 run    bu_9x16_a34          # start + install + launch + shot + stop
  .\emulators.ps1 matrix                      # run on every AVD in turn
#>
param(
    [Parameter(Position = 0)][ValidateSet('list', 'create', 'start', 'install', 'launch', 'shot', 'stop', 'run', 'matrix')][string]$Cmd = 'list',
    [Parameter(Position = 1)][string]$Avd,
    [string]$Apk = 'E:\Unity\builds\boingupskytower.apk',
    [string]$Out,
    [switch]$Headless,
    [switch]$Clear,
    [int]$Port = 0,
    [int]$BootTimeoutSec = 240
)
$ErrorActionPreference = 'Continue'
$Sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { 'E:\dev-tools\android-sdk' }
$env:ANDROID_HOME = $Sdk
$env:ANDROID_SDK_ROOT = $Sdk
$env:ANDROID_AVD_HOME = 'E:\dev-tools\avd'
$Adb = Join-Path $Sdk 'platform-tools\adb.exe'
$Emu = Join-Path $Sdk 'emulator\emulator.exe'
$Avdm = Join-Path $Sdk 'cmdline-tools\latest\bin\avdmanager.bat'
$Pkg = 'com.jdlabs.boingupskytower'
$ShotDir = 'E:\Unity\shots'

# name -> @(system image api, width, height, density, extra config)
$Avds = [ordered]@{
    'bu_9x16_a34'   = @(34, 1080, 1920, 420, @{})
    'bu_9x195_a34'  = @(34, 1080, 2340, 440, @{})
    'bu_9x21_a34'   = @(34, 1080, 2520, 440, @{})
    'bu_3x4_a34'    = @(34, 1536, 2048, 320, @{})
    'bu_9x195_a30'  = @(30, 1080, 2340, 440, @{})
}

# fixed console ports so the serial is predictable: emulator-<port>
function Get-Port([string]$name) {
    if ($Port -gt 0) { return $Port }
    $i = [array]::IndexOf(@($Avds.Keys), $name)
    if ($i -lt 0) { $i = 5 }
    return 5554 + 2 * $i
}
function Serial([string]$name) { "emulator-$(Get-Port $name)" }
function Adb-S([string]$name, [string[]]$a) { & $Adb -s (Serial $name) @a }

function Cmd-Create {
    foreach ($n in $Avds.Keys) {
        $api, $w, $h, $dpi, $x = $Avds[$n]
        $img = "system-images;android-$api;google_apis;x86_64"
        'no' | & $Avdm create avd -n $n -k $img -d pixel_6 --force | Out-Null
        $ini = Join-Path $env:ANDROID_AVD_HOME "$n.avd\config.ini"
        $cfg = Get-Content $ini
        $set = @{
            'hw.lcd.width' = $w; 'hw.lcd.height' = $h; 'hw.lcd.density' = $dpi
            'hw.keyboard' = 'yes'; 'hw.ramSize' = 2048; 'vm.heapSize' = 256
            'disk.dataPartition.size' = '4096M'; 'showDeviceFrame' = 'no'
            'hw.gpu.enabled' = 'yes'; 'hw.gpu.mode' = 'auto'
        }
        foreach ($k in $set.Keys) { $cfg = @($cfg | Where-Object { $_ -notmatch "^$([regex]::Escape($k))\s*=" }) + "$k=$($set[$k])" }
        Set-Content $ini $cfg
        "created $n  ${w}x${h} @${dpi}dpi  API $api"
    }
}

function Cmd-Start([string]$name) {
    if (& $Adb devices | Select-String (Serial $name)) { "$name already running"; return }
    $a = @('-avd', $name, '-port', (Get-Port $name), '-no-snapshot-save', '-no-audio', '-no-boot-anim', '-netdelay', 'none', '-netspeed', 'full')
    if ($Headless) { $a += @('-no-window', '-gpu', 'swiftshader_indirect') }
    Start-Process -FilePath $Emu -ArgumentList $a -WindowStyle $(if ($Headless) { 'Hidden' } else { 'Normal' }) | Out-Null
    & $Adb -s (Serial $name) wait-for-device
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $BootTimeoutSec) {
        $b = (Adb-S $name @('shell', 'getprop', 'sys.boot_completed') 2>$null) -join ''
        if ($b.Trim() -eq '1') { break }
        Start-Sleep -Seconds 3
    }
    if ($b.Trim() -ne '1') { throw "$name did not finish booting in $BootTimeoutSec s" }
    # keep screen on, no animations delay, no lock
    Adb-S $name @('shell', 'settings', 'put', 'global', 'stay_on_while_plugged_in', '7') | Out-Null
    Adb-S $name @('shell', 'input', 'keyevent', '82') | Out-Null
    Adb-S $name @('reverse', 'tcp:13000', 'tcp:13000') | Out-Null   # AltTester Desktop on the PC
    "$name ready ($(Serial $name)) after $([int]$sw.Elapsed.TotalSeconds) s"
}

function Cmd-Install([string]$name) {
    if (-not (Test-Path $Apk)) { throw "APK not found: $Apk" }
    Adb-S $name @('install', '-r', $Apk)
}

function Cmd-Launch([string]$name) {
    Adb-S $name @('shell', 'am', 'force-stop', $Pkg) | Out-Null
    if ($Clear) { Adb-S $name @('shell', 'pm', 'clear', $Pkg) | Out-Null }
    Adb-S $name @('shell', 'monkey', '-p', $Pkg, '-c', 'android.intent.category.LAUNCHER', '1') | Out-Null
}

function Cmd-Shot([string]$name, [string]$file) {
    if (-not $file) { $file = Join-Path $ShotDir "emu_$name.png" }
    New-Item -ItemType Directory -Force (Split-Path $file) | Out-Null
    # exec-out gives raw PNG bytes (no CRLF mangling)
    $p = Start-Process -FilePath $Adb -ArgumentList @('-s', (Serial $name), 'exec-out', 'screencap', '-p') `
        -RedirectStandardOutput $file -NoNewWindow -Wait -PassThru
    "saved $file ($((Get-Item $file).Length) bytes)"
}

function Cmd-Stop([string]$name) {
    & $Adb -s (Serial $name) emu kill 2>$null | Out-Null
    "$name stopped"
}

function Cmd-Run([string]$name) {
    Cmd-Start $name
    try {
        Cmd-Install $name
        $script:Clear = [switch]$true
        Cmd-Launch $name
        Start-Sleep -Seconds 20          # splash + first scene
        Cmd-Shot $name $null
    } finally { Cmd-Stop $name }
}

switch ($Cmd) {
    'list'    { & $Emu -list-avds }
    'create'  { Cmd-Create }
    'matrix'  { foreach ($n in $Avds.Keys) { Cmd-Run $n; Start-Sleep -Seconds 5 } }
    default   {
        if (-not $Avd) { throw "AVD name required: $($Avds.Keys -join ', ')" }
        switch ($Cmd) {
            'start'   { Cmd-Start $Avd }
            'install' { Cmd-Install $Avd }
            'launch'  { Cmd-Launch $Avd }
            'shot'    { Cmd-Shot $Avd $Out }
            'stop'    { Cmd-Stop $Avd }
            'run'     { Cmd-Run $Avd }
        }
    }
}
