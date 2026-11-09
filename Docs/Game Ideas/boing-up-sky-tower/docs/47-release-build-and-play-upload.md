# 47 Release build and Google Play upload

Status: written 2026-11-09 with the first release build entry. Code: `Assets/Editor/Build/AndroidReleaseBuild.cs`. (Doc number 36 is also used by `36-inventories.md`; this file is the release-build doc.)

## 1. What the release build is

A signed Android App Bundle (`.aab`) for Google Play. It is a NON-development build: no `DEVELOPMENT_BUILD` define, `Debug.isDebugBuild` is false, no profiler or script debugging, IL2CPP, arm64-v8a only, no AltTester, only the three game scenes (Boot, Meta, Run). All tests, admin settings and the admin icon are off (section 4).

It is signed with the UPLOAD key. Google Play App Signing keeps the real app signing key on Google's side; the upload key only proves to Google that the upload comes from us. If the upload key is lost it can be reset (section 7).

The normal dev build (`AndroidBuild`, a development APK for the phone) is unchanged and keeps working. The release build restores every editor setting it changes.

## 2. Where the keystore lives

Folder (outside the repository, never commit it): `E:\Unity\keys\`

- `fluffyclimbers-upload.keystore` (alias `fluffyclimbers-upload`)
- `fluffyclimbers-upload.properties` (keys `keystore`, `alias`, `storepass`, `keypass`; simple key=value lines)

The build reads the properties file at build time and passes the passwords to Unity in memory only (`PlayerSettings.Android.keystorePass` / `keyaliasPass`). They are cleared again after the build, never logged, never copied into the project, ProjectSettings or docs. `.gitignore` already excludes `*.keystore`, `*.jks`, `*.aab`. Do not put the properties file in the repo either.

After the first release build, check that `ProjectSettings/ProjectSettings.asset` still has an empty `AndroidKeystoreName:` and `AndroidKeyaliasName:`, `androidUseCustomKeystore: 0`, and contains no password. `git diff ProjectSettings` should show no keystore line.

## 3. How to build

Menu in the Unity Editor: `Tools > Fluffy Climbers: Tower Jump > Build Release AAB`.

Command line (the editor must not be open on the same project):

```
"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" -batchmode -nographics -quit ^
  -projectPath "E:\Code\boing-up-sky-tower" ^
  -executeMethod FluffyClimbers.Editor.Build.AndroidReleaseBuild.BuildReleaseFromCommandLine ^
  -logFile "E:\Unity\builds\release_build.log"
```

Exit code 0 = success, 1 = failure. Do not pass `-altTester` and do not set `BT_ALTTESTER` (the release build refuses and fails). Search the log for `[ReleaseBuild]`.

What it does, in order:
1. Preflight (before a build number is used): no AltTester request, no test scripting symbol, the three scenes exist, no test scene enabled in Build Settings, the properties file and keystore exist, the alias is `fluffyclimbers-upload`, and keytool (Unity's JDK) can open the keystore with that alias and store password. Any failure logs `[ReleaseBuild]` as an error and returns exit code 1.
2. Version: same rules as the APK build. `Tools/version.json` BUILD +1, version name `M.m.b`, Android version code = BUILD, one row in `Content/builds/build_log.csv` (last column holds the `.aab` path).
3. Settings: `buildAppBundle=true`, `development=false`, `allowDebugging=false`, `connectProfiler=false`, IL2CPP, ARM64, custom keystore on.
4. Build with `BuildOptions.None`, then a post-build assertion logs the effective flags (development, buildAppBundle, keystore path and alias, never the passwords) and fails if the report says Development or the file is missing.
5. Output: `E:\Unity\builds\fluffyclimbers_v<M.m.b>.aab` (kept) and a copy `E:\Unity\builds\fluffyclimbers.aab` (latest).
6. Restores the editor settings it changed.

Build hooks that run for every Android build apply as well: LevelPlay dependency (order 10), short label (20), no backup (30).

Stripping: the release uses the project's existing stripping settings (the same as the development APK that is tested on the phone), so Newtonsoft.Json and Unity IAP behave as in the tested APK. The log prints the effective level. If you ever raise stripping for release, test purchases, ads and save loading on a release build first.

## 4. What is OFF in a release build

Release = non-development player (`UNITY_EDITOR` and `DEVELOPMENT_BUILD` not defined, `Debug.isDebugBuild` false).

| Surface | Release behaviour |
|---|---|
| Admin unlock (`AdminAccess`, PlayerPrefs `settings.adminunlocked`) | Constant `AdminAccess.ToolsCompiledIn` is false: the stored value is ignored (Unlocked always false), the setter writes nothing, `TryUnlock` always returns false, `Available` is false |
| Admin icon in Settings | Not shown (`AdminAccess.Available` false) |
| 7 taps on the version label and the code box | Do nothing (`OnVersionTapped` and `ShowAdminCodeBox` return at once) |
| Settings dev rows, `ShowDevRowsForTests` | `ShowDevRowsForTests` is only set by tests; the Admin button depends on `AdminAccess.Available` (false) |
| SYSTEM_TEST, GAME_JUMP_TEST, SHOP_TEST, CHARACTER_TEST | Always false: `GameSettings.DevBuild` is false in release, so all four read false whatever the PlayerPrefs hold |
| Unlock all levels or characters, test purchases, Main rewards test override | Follow the flags above, so off |
| Autopilot, `AutopilotBootstrap`, autoplay.json, BTAUTO logs | `Autopilot.Allowed` is false (SystemTest false, ForceEnable needs editor or debug build), so the bootstrap is never installed |
| `TestBridge` (BTSTATE, BTUI logcat lines) | Not installed (`Debug.isDebugBuild` false) |
| `LevelSweepApi` (AltTester call API) | Mutating methods refuse; AltTester is not in the scene anyway |
| AltTester runner | Never added: the release refuses `-altTester` and the scene hook is inactive; no `ALTTESTER` symbol |
| Test ad provider | `AdService` uses `TestRewardedAds` only in the editor or a debug build; release uses LevelPlay |
| Stub account service | Editor and debug build only; release uses Google Play Games |
| Test scenes | Only Boot, Meta, Run are built; the build fails if a test scene is enabled |
| Debug build, profiler, script debugging | Off |

Notes: `LaunchPromo` and `FirstPurchaseDouble` are real game settings stored in PlayerPrefs (default off). They are not test tools, so they are not locked. The AltTester package code (`com.alttester.sdk`) has a `link.xml` that keeps its classes, so unused AltTester code still ships in the AAB (no runner is ever started). Removing the package for release is optional. `Debug.Log` lines still go to logcat in release.

## 5. Backing up the keystore

Losing the upload key is recoverable (section 7) but slow, so back it up:
1. Keep at least two copies of the whole `E:\Unity\keys\` folder on different devices, at least one OFFLINE (a USB stick kept somewhere safe, not plugged in).
2. Store the passwords (the values in the properties file) and the alias in the password manager, with a note naming the keystore file and the app.
3. After the first successful upload, test that a copy opens: `keytool -list -keystore <copy>`.
4. Never email it, never put it in the repo, a public cloud folder or a chat.

## 6. Play Console: upload to Internal testing

1. Open Play Console, select the app "Fluffy Climbers: Tower Jump" (package `com.jdlabs.fluffyclimbers`). First time only: create the app and accept Play App Signing (Google holds the app signing key, we upload with the upload key).
2. Left menu: Test and release > Testing > Internal testing.
3. Testers tab: create or pick an email list of testers and save.
4. Releases tab: Create new release.
5. Upload `E:\Unity\builds\fluffyclimbers_v<M.m.b>.aab`. The version code is the BUILD number and must be higher than any earlier upload.
6. Release name (suggested `v<M.m.b>`), release notes, then Next. Resolve any errors or warnings (data safety form, ads declaration, content rating and target audience must be complete before the first rollout), then Save and publish to Internal testing.
7. Testers: use the opt-in link on the Testers tab, install from Google Play, and check that the Settings screen shows no Admin icon.
8. Later: promote the same release to Closed or Production from the release page.

## 7. If the upload key is lost

With Play App Signing enabled the app is not lost. In Play Console go to Test and release > App integrity > App signing, choose "Request upload key reset", create a NEW keystore (`keytool -genkeypair`), export its public certificate (`keytool -export -rfc`), and upload that `.pem` in the form. Google needs a day or two to approve; then sign uploads with the new key. Store it as in section 5 and update the properties file (keep the alias and file names, or update the constants in `AndroidReleaseBuild`).
If Play App Signing is not on, a lost key means the app can no longer be updated, so confirm Play App Signing is on before the first upload.
