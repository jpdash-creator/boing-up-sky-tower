# Phone test checklist for the final build (2026-11-12)

For the owner's phone (OnePlus 13). Work top to bottom, tick each line, note results with the bug template at the end. Facts come from the code and docs of 2026-11-12; items marked (expected) are owner decisions I could not check on a device.

## 0. Versions and clean install
- `Tools/version.json` says v0.59.92; `Content/builds/build_log.csv` last line: v0.59.92 AAB succeeded 2026-11-12 21:17. The development APK for the phone comes from `AndroidBuild.BuildFromCommandLine` (`E:/Unity/builds/fluffyclimbers.apk`); the Play build is the AAB from `AndroidReleaseBuild`. Version shown in the app / logcat `BTSTATE`: ______
- Clean install (owner rule): `adb uninstall com.jdlabs.fluffyclimbers`, then install. Never keep the old save.
- For the release-build checks (section 3) install APKs made from the AAB with bundletool (last section of `RELEASE-AAB-CHECKLIST.md`); an AAB cannot be installed directly.

## 1. Meta screens and settings (leaderboard flag OFF, as shipped)
1. **Music.** On start the menu music "Sunny Skip" (`Assets/Resources/Audio/music/loop.wav`, original composition) plays on the home screen. Visit World map, Level screen, Heroes, Shop, Rewards, Settings, Merge, Hero sheet: music continues without gaps or restarts where it should continue. Music off in Settings stops it at once.
2. **Home and login layout (expected, owner 2026-11-12).** Banner/logo centred or slightly above centre; no hero figure on home; no version label. The login screen uses the same painted meadow as home. Nothing overlaps or leaves the notch-safe area, also at 130 percent font size.
3. **Guest vs Google Play sign-in.** Start as guest: play works; the leaderboard says sign-in is needed (a guest is never on a board). Sign in with Google Play Games: your name shows, the save uploads (Saved Games). Sign out: back to guest, progress stays on the device.
4. **Settings speed-ups and crown numbers.** Level screen buttons: Relaxed, Easy, Normal, Hard, Insane. Expected speed-up per 5 s and top speed-up (`SimConfig.DifficultyStepPercent`, `TopSpeedUpPercent`): 10 / 80 percent, 20 / 160, 25 / 200, 40 / 320, 50 / 400. In a run the "SPEED UP +N%" cue climbs in those steps and stops after 8 steps (40 s). Crown = (floors x 10 + combo^2 x 5) x (1 + percent), half up: play one run, note floors, combo and setting, check with a calculator. Example: base 1,000 on Hard = 1,400.
5. **Medals per setting.** Target = bot pace x {0.85, 0.92, 1.00, 1.12, 1.25} for Relaxed..Insane; Bronze/Silver/Gold at 55/70/85 percent of that target. Play one level on Relaxed and Insane: the same pace earns an equal or better medal on Relaxed. The level node shows the medal of the CURRENT setting and redraws when you change it.
6. **Revive medal.** On a level, die, revive (gems or Watch Ad), finish: since 2026-11-10 the run counts (best Time and Pace, NEW BEST ribbons, medal, timed goals). Code detail: Time Attack and per-minute captures still skip revived runs. Note anything that contradicts this.
7. **Insane and Hard feel.** Play levels 1, 10 and 25 of Region 1 on Hard and Insane. Is the speed-up readable, is the top speed survivable (4 u/s campaign ceiling still applies), any unfair death? Note level, setting and floor of each fail.
8. **Hero stats sheet after a merge or chest.** Get an Uncommon or better hero (chest or merge). Open its sheet (the (i) button on the Heroes screen): Jump, Speed, Control and Wall bounce are the numbers rolled for you (`HeroRolls`); a second clean install can show slightly different numbers for the same hero. A Common hero always shows the fixed numbers. Screenshot the sheet.

## 2. Leaderboard (TEST BUILD ONLY, flag ON)
The shipped config has the leaderboard OFF. Do NOT flip it in a release. For one test build:
1. In `Tools/content_cli/gen_content.py`, inside the `write("config/remote_config.json", {...})` dictionary, add `"leaderboard": {"enabled": True, "baseUrl": "https://fluffy-leaderboard-763742589585.asia-south1.run.app"},` (URL from `LEADERBOARD-DEPLOY-LOG.md`, no trailing slash). The client reads `leaderboard.enabled` and `leaderboard.baseUrl` (`LeaderboardConfigDef`); a missing section or unusable URL means OFF.
2. Run `python Tools/content_cli/gen_content.py` (it overwrites generated content, see CLAUDE.md), rebuild the offline manifest (`ContentManifestBuilder.BuildFromCommandLine`), build the dev APK. Afterwards revert the generator change (git checkout of the file) so a release cannot ship it ON.
3. Outside the app (`LEADERBOARD-DEPLOY-LOG.md`, "Not done"): the Firebase Auth Play Games provider must be enabled and the signing key SHA-1 registered, or sign-in fails for unrelated reasons; the weekly tick and the `config/leaderboard` seed are not set up (defaults come from `config.default.json`).
4. On the phone: open the Leaderboard screen (4th icon of the bottom bar: Shop, Leaderboard, Heroes, Daily Rewards) as guest (asks to sign in), sign in, and see the **consent popup** once (name and picture shown to others, runs sent for checking). Decline: nothing is sent (asked again the next day, `DeclinedDay`). Accept: Overall, Levels and Endless boards load; your rank shows after a first run.
5. **A run submitting.** Complete a campaign level (only COMPLETED levels count) and finish an Endless run. The board total grows by the slot crown. Cloud Run has no custom log lines (none found in `Tools/leaderboard_service`); look for the request lines of service `fluffy-leaderboard`: `POST /start-run` 200, `POST /submit-run` 200 (body `accepted:true, counted:true`; an uncompleted level answers `counted:false, reason:"level_not_completed"`), `GET /my-week` 200. 401 means the Firebase token was refused. Read-only command: `gcloud run services logs read fluffy-leaderboard --region=asia-south1 --project=fluffy-climbers-tower-jump --limit=50`.
6. Revived runs, power-up runs and your own rolled hero all count (owner 2026-11-12): do one run with a revive and one with a power-up; both should be accepted.
7. **Offline.** Airplane mode, finish a run: it is queued and sent when the network returns.
8. **Weekly prize claim.** Needs a finished week and a top-10 rank above the entry limit (200k overall, 150k levels, 50k endless; ranks 1 to 3 need 1.5 x). Unlikely on one phone: check that Rewards shows the leaderboard prize section without error, and test the real claim on the first real week (arrives once; a second claim gives nothing).

## 3. Release-build checks (R8, symbols, Firebase)
Use the AAB from `AndroidReleaseBuild` and `Planning/RELEASE-AAB-CHECKLIST.md`.
1. Build log `[ReleaseBuild]` lines: "R8 minify release=True, custom proguard file=True" and "AAB check: mapping in bundle=True ...", symbols embedded or a `*.symbols.zip`.
2. `E:/Unity/builds/fluffyclimbers_v<ver>_mapping.txt` exists. Upload it (App bundle explorer > Downloads > ReTrace mapping file) and the symbols zip if not embedded. Both Play warnings must be gone.
3. Install through bundletool (`build-apks --connected-device`, `install-apks`; uninstall first). Play with `adb logcat -s Unity AndroidRuntime`: no `ClassNotFoundException`, `NoSuchMethodError`, `NoClassDefFoundError`. Test Play Games sign-in (needs the UPLOAD key SHA-1 registered), a rewarded ad, the UMP consent, a licence-tester purchase and Restore Purchases.
4. **Permission list.** `java -jar bundletool-all-1.17.2.jar dump manifest --bundle=E:\Unity\builds\fluffyclimbers.aab` (jar path in the checklist). `com.google.android.finsky.permission.BIND_GET_INSTALL_REFERRER_SERVICE` must NOT appear (removed by `FluffyPrivacyDefaults.androidlib`). Look at the rest of the list for anything new.
5. **Firebase Analytics flags** in the same dump: `firebase_analytics_collection_deactivated=true`, `firebase_analytics_collection_enabled=false`, `google_analytics_adid_collection_enabled=false`, `google_analytics_ssaid_collection_enabled=false` and the four `google_analytics_default_allow_*` = false. On the device logcat should show no Firebase Analytics event upload lines.
6. The Data safety form (doc 49, last section) is entered in Play Console before the release is submitted.

## 4. Bug report template
```
Title:
Build version (vM.m.b) and install type (dev APK / bundletool release / Play):
Device, Android version, language, font size:
Checklist section and step:
Account: guest / Google Play Games; leaderboard flag ON or OFF:
Gameplay setting, level, hero (and tier):
What I did (numbered):
What I expected:
What happened (include the numbers I saw):
How often (always / sometimes / once):
Screenshot or screen recording: yes / no
logcat (adb logcat -d -s Unity AndroidRuntime > file) attached: yes / no
Severity: blocks release / annoying / cosmetic
```
