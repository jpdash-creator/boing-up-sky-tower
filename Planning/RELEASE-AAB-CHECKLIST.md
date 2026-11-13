# Release AAB checklist (R8 mapping and native debug symbols)

Owner decision 2026-11-12: the two Play Console warnings ("no deobfuscation file", "native code without debug symbols") are fixed in the RELEASE build only.
Development and APK builds stay unminified.

## What the release build does now
- `AndroidReleaseBuild` turns on `PlayerSettings.Android.minifyRelease` (R8), switches on "Custom Proguard File" (serialized field `useCustomProguardFile`) and sets
  `EditorUserBuildSettings.androidCreateSymbols = Public` for the build, then restores all three.
- Keep rules live in `Assets/Plugins/Android/proguard-user.txt` (Play Games v2, Play Billing and Unity IAP, AdMob, UMP, mediation, Firebase, protobuf/gRPC/OkHttp). Unity adds its own rules on top.
- Before the build number is used: the build fails if `proguard-user.txt` is missing or lost a rule group.
- After the build it opens the AAB and FAILS (with "Do NOT upload this file") if `BUNDLE-METADATA/com.android.tools.build.obfuscation/proguard.map` is missing, or if there are neither
  embedded debug symbols (`BUNDLE-METADATA/com.android.tools.build.debugsymbols/`) nor a fresh `*.symbols.zip` beside the AAB.
- It extracts the mapping to `E:/Unity/builds/fluffyclimbers_v<M.m.b>_mapping.txt` for upload.

## Run it
`E:\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe -batchmode -nographics -projectPath E:\Code\fluffy-climbers-sky-tower -executeMethod FluffyClimbers.Editor.Build.AndroidReleaseBuild.BuildReleaseFromCommandLine -logFile E:\Unity\logs\release.log`
(exit 0 = ok). Read the lines starting `[ReleaseBuild]`: "R8 minify release=True, custom proguard file=True", then "AAB check: mapping in bundle=True ... debug symbols embedded=... symbols zip ...".

## Upload to Play Console
1. Release > Production (or Internal testing) > create release > upload `fluffyclimbers_v<ver>.aab`.
2. If the log says symbols are embedded in the AAB, nothing more is needed for native symbols. Otherwise: App bundle explorer > select the version > Downloads tab > "Native debug symbols" > upload the `*.symbols.zip` from `E:/Unity/builds`.
3. Deobfuscation file: the same Downloads tab > "ReTrace mapping file" > upload `fluffyclimbers_v<ver>_mapping.txt` (the mapping inside the AAB is normally picked up automatically; the separate upload is the fallback).
4. The two warnings should be gone on the release summary page. Keep the mapping and symbols files of every shipped build (they cannot be regenerated).

## Verify on a device with a RELEASE build (R8 can break things only in release)
Install the release, play on a clean install, and watch `adb logcat -s Unity AndroidRuntime` for `ClassNotFoundException`, `NoSuchMethodError`, `NoClassDefFoundError`.
- Play Games: sign in, leaderboard opens, a score submits. (Locally signed builds use the UPLOAD key SHA-1: register it as a Play Games credential, or sign-in fails for reasons unrelated to R8. Builds installed from Play use the app signing key.)
- Ads: UMP consent prompt appears (EEA/test device), a rewarded ad loads, plays and grants the reward.
- IAP: gem pack purchase with a licence tester, subscription listing, and Restore Purchases.
- Normal play, pause/resume, Settings; no crash on start. If something fails, add a keep rule for the class in the stack trace to `proguard-user.txt` and rebuild.

## Device-installable APKs from the AAB (bundletool)
bundletool is not on PATH and not in `E:\tools`, but Unity ships `E:\Unity\Hub\Editor\6000.3.26f1\Editor\Data\PlaybackEngines\AndroidPlayer\Tools\bundletool-all-1.17.2.jar`. Nothing needs downloading. Java: `E:\dev-tools\jdk-17\bin\java.exe` (on PATH).
```
set BT=E:\Unity\Hub\Editor\6000.3.26f1\Editor\Data\PlaybackEngines\AndroidPlayer\Tools\bundletool-all-1.17.2.jar
java -jar %BT% build-apks --bundle=E:\Unity\builds\fluffyclimbers.aab --output=E:\Unity\builds\fc_release.apks --connected-device --mode=default ^
  --ks=E:\Unity\keys\fluffyclimbers-upload.keystore --ks-key-alias=fluffyclimbers-upload --overwrite
java -jar %BT% install-apks --apks=E:\Unity\builds\fc_release.apks
```
bundletool asks for the keystore passwords on the console (do not put them on the command line). Uninstall the old build first (clean install rule).
Without a device: `--mode=universal` gives one APK inside the .apks zip.

## Firebase and privacy checks of a release (added 2026-11-12)
- Firebase Analytics is OFF by the manifest flags in `Assets/Plugins/Android/FluffyPrivacyDefaults.androidlib` (`firebase_analytics_collection_deactivated`, `firebase_analytics_collection_enabled=false`, no ad-ID or SSAID collection, consent defaults denied) and the install-referrer permission is removed. Check the merged manifest of the AAB: `java -jar %BT% dump manifest --bundle=...aab` must show the flags and must NOT list `com.google.android.finsky.permission.BIND_GET_INSTALL_REFERRER_SERVICE`.
- The Firebase Unity packages 14.1.0 (`com.google.firebase.app`, `.auth`) are local tgz files in `E:/tools/firebase_unity` (see CLAUDE.md); a build on another machine fails at package resolve until they are there.
- Play Console: upload the mapping file and the symbols zip of every release; the Data safety form must carry the leaderboard answers (doc 49, last section).
