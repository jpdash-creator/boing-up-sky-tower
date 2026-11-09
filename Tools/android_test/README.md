# Android test kit

- `bt.py` - adb driver (reads BTUI/BTSTATE lines the development build prints to logcat; find, tap, screenshot, layout asserts).
- `smoke_test.py` - on-device smoke test (`ANDROID_SERIAL`-style: edit `Device(serial)` or use the only connected device).
- `emulator_matrix.py` - boots each emulator AVD one at a time, installs an APK, runs a screenshot tour and writes a report.

## Emulator matrix (one command)

    python Tools/android_test/emulator_matrix.py [--avd NAME]... [--apk PATH] [--skip-boot-wait]

- Default: the 5 `emu_*` AVDs (720x1280, 1080x1920, 1080x2400, 1080x2520, 1536x2048) and `E:/Unity/builds/fluffyclimbers_v0.42.0.48_x86.apk`.
- Needs a development build that includes x86_64 (the normal arm64-only APK crashes with SIGILL) and the Android SDK at
  `E:\dev-tools\android-sdk` (override with `ANDROID_SDK_ROOT`, `ANDROID_AVD_HOME`).
- Each AVD: boot (port 5554, swiftshader), install, launch with cleared data, tour (home, settings, shop + all 5 tabs, VIP, Heroes,
  Rewards main/daily/weekly, map, level select, level popup, run), logcat scan, `emu kill`. About 5-8 min per AVD.
- `--skip-boot-wait`: use an emulator already running on `emulator-5554` (it is not started or killed).
- Only `emulator-5554` is ever addressed; the real phone (96355505) is never touched.
- Output: `E:/Unity/device_shots/emulators/<avd>/*.png`, `_tour.log`, `_logcat.txt` and `E:/Unity/qa/emulator_report_<timestamp>.md`
  (BTUI rule violations, failed steps, crashes, logcat errors). Clipping/overlap still needs a visual look at the screenshots.

### Disk-safe AVD handling (only one AVD on disk at a time)

Each AVD is about 5 GB, so the matrix runs strictly sequentially: create the AVD, boot, test, kill the emulator, then delete the AVD
before the next one is created.

- Built-in profiles (android-34 google_apis x86_64, `hw.ramSize=3072`, `showDeviceFrame=no`): `emu_720x1280_9x16` (720x1280, 320 dpi),
  `emu_1080x1920_9x16` (1080x1920, 420), `emu_1080x2400_9x20` (1080x2400, 420), `emu_1080x2520_9x21` (1080x2520, 420),
  `emu_1536x2048_3x4` (1536x2048, 320). A missing AVD is created with `avdmanager` (its devices.xml error is ignored, the `.avd` folder is what
  counts) and `config.ini` is then rewritten (`hw.lcd.width/height/density`, `hw.ramSize`, `showDeviceFrame`).
  Env used: `ANDROID_SDK_ROOT=E:\dev-tools\android-sdk`, `ANDROID_AVD_HOME=E:\dev-tools\avd`, `JAVA_HOME` = the Unity OpenJDK folder.
- Before each creation `E:` must have >= 8 GB free, otherwise the script stops with a clear message.
- Default: the AVD is deleted after its test. `--keep-first` keeps only `emu_720x1280_9x16`; `--keep` keeps everything.
- Delete is exact-name only (folder `<name>.avd` plus `<name>.ini`, full names compared), only for the 5 profile names, and refused outside
  `E:\dev-tools\avd`. `--avd` names that are not profiles are never created or deleted. `--skip-boot-wait` never creates or deletes.
- `--dry-run` prints the create/delete commands and runs nothing:
  `python Tools/android_test/emulator_matrix.py --dry-run [--keep-first] [--avd NAME]...`
