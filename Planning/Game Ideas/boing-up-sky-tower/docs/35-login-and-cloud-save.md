# 35. Login (Google Play Games or guest) and cloud save

Owner Issue 34 item 1. Decisions: both ways to play; progress in **Google Play Games Saved Games**, purchases in **Google Play Billing**; **no own server**.
Package id: `com.jdlabs.fluffyclimbers` (Android only; the Play Games plugin no longer supports iOS).

## 1. What the player sees

| Moment | What happens |
|---|---|
| First launch (`loginChosen` false) | Full-screen login over the menu: **Sign in with Google Play** (white Google-style button) and **Play as guest**, note "Guests save only on this device." |
| Guest | `accountMode = guest`. Nothing is uploaded. Settings shows "Guest: progress is saved on this device only" and a Sign in button. |
| Google, account has no save | The device progress is uploaded (a fresh device with no progress uploads nothing). |
| Google, device has no progress, account has a save | The account save is loaded silently. |
| Google, both have progress and differ | **Choose your save** popup: two cards "This device" and "Google account" (levels cleared, gems, coins, hero copies, last played), each with its own button (**Keep this device** / **Use Google save**). The save not picked is replaced. |
| Google, same progress on both | Nothing to choose; linked and marked synced. |
| Returning Google player | Silent sign-in at start, the cloud save is read, `SaveSync.Decide` applies the rules below. No UI unless a choice is needed. |
| Offline / sign-in fails at start | The game plays on the local save. Uploads wait; the check is repeated when the app regains focus (max 6 tries per launch). |
| Sign out (Settings) | Back to guest on this device: the progress stays, uploads stop, the cloud save is left untouched. |
| Reset progress (Settings) | Wipes progress but keeps `accountMode`, `accountId`, `lastCloudSyncUtc` and `loginChosen` (no login screen again). With a Google account the next upload replaces the cloud save with the empty one: the reset confirmation should say so (Settings owner). |

## 2. Rules (`Economy/SaveSync.cs`, pure C#, unit tested)

`SaveSync.Decide(local, cloud, accountId)` returns `Nothing`, `UploadLocal`, `UseCloud` or `AskPlayer`:

1. No readable cloud save: upload the device save if it has progress, else nothing.
2. Cloud save written by a newer app version (`Data.Version` above `SaveData.CurrentVersion`): nothing, and uploads are blocked for the session so the newer save is never overwritten.
3. Same summary (levels, gems, coins, hero copies): nothing.
4. Device has no progress: use the cloud save. Cloud save empty: upload the device save.
5. Not linked to this account yet (guest, or another Google account than last time): **ask**. Never overwrite either side silently.
6. Linked (`accountId` equal): compare with `lastCloudSyncUtc`. Only the cloud changed since: use cloud. Only the device changed: upload. Both changed and differ: ask.

Whole saves are chosen, never merged field by field (a mixed wallet could duplicate or lose gems).
`lastPlayedUtc` is stamped by `ProgressService.Commit` on every change; `SaveEnvelope.savedUtc` is stamped when the bytes are made.

### Cloud bytes
UTF-8 JSON `SaveEnvelope { format, savedUtc, saveVersion, summary{levels,gems,coins,heroCopies,lastPlayed}, data = SaveData }`, file name `fluffy_save`.
The header lets the popup show a summary without understanding the save. Size is small (a few KB, the limit is 3 MB).

### Upload timing (`Gameplay/AccountSync.cs`)
- Every `ProgressService.Changed` marks the save dirty (a flag and one float). A tiny runner object checks the deadline once per frame (a float compare, no allocation); the upload happens `DebounceSeconds` (6 s) after the first change of a burst, and at once when the app is paused or quit.
- Allocation happens only when an upload is made (serialising), never per frame.
- A failed upload is ignored and retried after 45 s. Uploads only run when: Google mode, signed in, and the start-up check has finished (`syncReady`).
- Level results, purchases and merges all go through `ProgressService.Commit`, so they are covered without any call at the call sites.

## 3. Code map

| File | Role |
|---|---|
| `Services/Account/IAccountService.cs` | Interface: `SignIn`, `SilentSignIn`, `SignOut`, `IsSignedIn`, `DisplayName`, `PlayerId`, `IsAvailable`, `LoadCloudSave`, `LastLoadFailed`, `SaveCloud`. |
| `Services/Account/StubAccountService.cs` | Editor and development builds: signs in as "Test Player", fake cloud blob in PlayerPrefs (`fluffy.stub.cloud`). Test switches: `FailSignIn`, `CancelSignIn`, `FailCloud`, `UseMemoryOnly`, `SeedCloud`. |
| `Services/Account/UnavailableAccountService.cs` | Release build without the SDK: login screen shows only "Play as guest". |
| `Services/GooglePlay/GooglePlayGamesAccountService.cs` + asmdef | Real service, compiled only with `FLUFFY_GPGS`. |
| `Services/Account/PlayGamesConfig.cs`, `Resources/Services/play_games_config.json` | Project ID (placeholder `REPLACE_WITH_PLAY_GAMES_PROJECT_ID`); `IsConfigured` gates the SDK. |
| `Editor/Build/PlayGamesAndroidSetup.cs` | Build step: APP_ID `@string` reference, `strings.xml`, plugin settings, dependency resolve, release preflight (`StoreConfigPreflight`). |
| `Services/Services.cs` | `Services.Account` (stub in the editor / dev build, Play Games with `FLUFFY_GPGS` on a device, else unavailable). |
| `Economy/SaveSync.cs` | Summary, bytes, decision table, account field helpers. |
| `Economy/ProgressService.Account.cs` | `ReplaceData`, `SetAccount`, `MarkCloudSynced`. |
| `Economy/SaveData.cs` | New fields `accountMode`, `accountId`, `lastCloudSyncUtc`, `loginChosen`, `lastPlayedUtc` (additive, no version bump). |
| `Gameplay/LoginScreen.cs` | The two screens (built in code, same look as the menu). |
| `Gameplay/AccountSync.cs` | Sync glue, `LoginFlow`. |
| `Tests/EditMode/AccountTests.cs`, `Tests/PlayMode/CriticalUxTests.cs` | Rules, stub, glue; layout audit screens `login`, `login-guest-only`, `login-conflict`. |

### Hooks for other screens
- **MetaController.Start** (one line, at the end of `Start()`): `LoginFlow.Begin(canvas.transform, ShowMenu);` (the second argument redraws the open screen after a cloud save was applied; any `Action` works).
- **Settings**: `AccountSync.AccountLine` (text), `AccountSync.IsGoogleMode`, `AccountSync.SignInAndLink((ok, message) => ...)` for a "Sign in with Google Play" button (shows the conflict popup itself when needed), `AccountSync.SignOut()` for "Sign out". Both buttons need the 48 dp / 16 sp rules.
- **Restore Purchases** (`IPaymentService.Restore`): see section 5.

## 4. Play Games Services setup (verified against the Google guide)

Source of truth: developer.android.com/games/pgs/android/android-signin ("New client integration": Games v2 SDK `play-services-games-v2`, minSdk 19 or higher, compileSdk 28 or higher; this project has minSdk 25) and the Play Console pages under **Grow users > Play Games Services**. The plugin is `com.google.play.games` (package manager, OpenUPM scoped registry, listed in `Packages/manifest.json`; the C# plugin identifies itself as 2.1.0 in `PluginVersion.cs`, the package as 2.3.0). The Unity side of every step below is done by the repository (config file plus the build step `Assets/Editor/Build/PlayGamesAndroidSetup.cs`); only the console values come from the owner.

### 4.1 What the guide requires, and where it is in this project

| Guide requirement | Where it lives here |
|---|---|
| Games v2 SDK dependency `com.google.android.gms:play-services-games-v2` | Declared by the plugin (`Editor/GooglePlayGamesPluginDependencies.xml`, version 22.1.0, also `play-services-nearby` 18.5.0) and resolved by the External Dependency Manager into `Assets/Plugins/Android` (section 4.4). |
| `<meta-data android:name="com.google.android.gms.games.APP_ID" android:value="@string/game_services_project_id"/>` inside `<application>` | `Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml` (the plugin's generated Android library). `PlayGamesAndroidSetup` rewrites only this entry to the `@string` reference before every Android build and leaves the plugin's other entries (`unityVersion`, `NativeBridgeActivity`) as generated. |
| `<string translatable="false" name="game_services_project_id">PROJECT_ID</string>` in `res/values/strings.xml` | `GooglePlayGamesManifest.androidlib/res/values/strings.xml`, written by the same build step from the config file. |
| The value is the **Project ID** (the leading digits of the OAuth client id, 10 or more digits, shown under the game name on the Play Console Configuration page) | `Assets/Resources/Services/play_games_config.json`, key `projectId` (placeholder `REPLACE_WITH_PLAY_GAMES_PROJECT_ID`). |
| `PlayGamesSdk.initialize(context)` at start | Done by the plugin: the first access of `PlayGamesPlatform.Instance` builds `AndroidClient`, whose constructor calls `com.google.android.gms.games.PlayGamesSdk.initialize(activity)` (checked in `Runtime/Scripts/Platforms/Android/AndroidClient.cs`). `PlayGamesPlatform.Activate()` is NOT needed (it only sets `Social.Active`). `GooglePlayGamesAccountService` touches `Instance` only on Android with a valid project id. |
| Automatic sign-in at launch, no mandatory button, retry | `GooglePlayGamesAccountService.AutoSignInAtLaunch` (AfterSceneLoad) calls `PlayGamesPlatform.Authenticate` = the plugin's `GamesSignInClient.isAuthenticated()` check (no UI). A failure is silent; `AccountSync` repeats the check when the app regains focus (max 6 per launch). The manual button (`ManuallyAuthenticate` = `signIn()`) stays optional on the login screen and in Settings. |
| Never block play; handle SIGN_IN_REQUIRED and other failures quietly | Every sign-in and Saved Games call is wrapped; a failure becomes `SignInOutcome.Failed` / `LastLoadFailed`, never an exception or a popup. The game is fully playable as a guest and offline. A cloud error never overwrites or wipes local progress (`AccountSync` only applies a successfully read cloud save). |
| Do not keep the Play Games player id in an own backend | There is no own backend. The player id is stored only inside the local save and the cloud blob (`accountId`, used to detect a different Google account). |

### 4.2 Owner steps in Google Play Console and Google Cloud

Values: package name `com.jdlabs.fluffyclimbers`.

1. Create the app `com.jdlabs.fluffyclimbers` in Play Console (once). **Grow users > Play Games Services > Setup and management > Configuration**: create the Play Games Services project (it creates the linked Google Cloud project). Name, short description, category.
2. Copy the **Project ID** shown under the game name on the Configuration page (10 or more digits). Put it in `Assets/Resources/Services/play_games_config.json` as `"projectId"`. A release build refuses a placeholder or non-digit value.
3. **OAuth consent screen** (Google Cloud console, the project linked in step 1): User type External, app name, support email, scopes **`.../auth/games`**, **`.../auth/games_lite`** and **`.../auth/drive.appdata`**, then **Publish app** (a consent screen left in Testing blocks real players).
4. **Credentials > Add credentials > Android** (Play Console, Configuration page: "Add credential > Android"; it creates an OAuth client in the linked Cloud project). Create ONE credential per signing key, all with package name `com.jdlabs.fluffyclimbers`:
   - **Upload key** SHA-1 `E3:0D:AE:F1:AF:2A:29:EC:78:D5:40:1E:F4:6D:A9:66:81:11:5F:6A`. Used by the APKs and AABs signed with `E:\Unity\keys\fluffyclimbers-upload.keystore` (internal-testing sideloads and the release AAB before Google re-signs it).
   - **Debug key** SHA-1 `DE:D2:01:A4:59:B2:16:96:16:59:A9:B0:20:53:74:19:E7:C4:AF:85` (development builds signed with the Unity debug keystore).
   - **Play App Signing key** SHA-1: the owner copies it from Play Console > Test and release > Setup > **App signing** ("App signing key certificate") after the FIRST upload. Builds installed from Google Play are re-signed with this key, so without it sign-in fails for every Play-installed copy (`DeveloperError` in logcat).
   Then link the client ids: Configuration page > **Credentials**, select each credential and **Save** (the guide says the OAuth client must be linked to the game).
5. **Saved games**: Configuration page > enable **Saved games** (needed by the cloud save; it uses the Drive app-data scope from step 3).
6. **Testers**: Play Games Services > **Testers**: add your own Google account (and any tester accounts). While the Play Games project is unpublished only listed testers can sign in. Put them on the internal testing track too.
7. **Publish game** in the Play Games Services settings (Setup and management > Publish) BEFORE publishing the app to production; an unpublished game works for testers only.

### 4.3 Unity side (done in the repository)

- `Assets/Resources/Services/play_games_config.json` holds the Project ID (step 2). Nothing else needs hand editing: the Plugin's setup window is not used.
- `Assets/Editor/Build/PlayGamesAndroidSetup.cs` (`IPreprocessBuildWithReport`, order 0, Android only) runs before every Android build: it points the library manifest APP_ID at `@string/game_services_project_id`, writes `res/values/strings.xml` from the config, sets `PlayGamesSettings.asset` `mAppId` (the runtime asset of the plugin) and the plugin's "setup done" flag, and makes sure the Games SDK is resolved. Menu: Tools > Fluffy Climbers: Tower Jump > Apply Play Games setup.
- With the placeholder a development APK still builds; the game then never initialises the Play Games SDK (`PlayGamesConfig.IsConfigured` is false, the login screen offers guest play only). `AndroidReleaseBuild` fails a release build when the id is the placeholder or not at least 10 digits, when `iap_config.json` still has the placeholder licence key, or when the Games SDK is not resolved (`StoreConfigPreflight`).
- Scripting define `FLUFFY_GPGS` (Android) activates `GooglePlayGamesAccountService`; the other agent sets it with `FLUFFY_ADMOB;FLUFFY_UNITY_IAP`.

### 4.4 External Dependency Manager (how the Games v2 SDK reaches Gradle)

What was verified by reading: no custom `mainTemplate.gradle` (`useCustomMainGradleTemplate: 0`), so the Google Android Resolver (EDM4U 1.2.182, package `com.google.external-dependency-manager`) copies the resolved AARs into `Assets/Plugins/Android` and Unity's Gradle build picks them up there; its setting "Resolve Automatically On Build" exists (`AutoResolveOnBuild`, default on) and `PlayServicesResolver.ResolveSync` is public in the installed `Google.JarResolver.dll`. `PlayGamesAndroidSetup.EnsureDependenciesResolved` calls `ResolveSync` by reflection if no `play-services-games` artifact is present, so a headless build needs no Force Resolve click; the release preflight calls it and fails the build if the artifact is still missing. NOT verified (needs Unity, JDK/Gradle and network): the first real resolve, the exact file names it produces, and that the batch-mode resolver finishes inside the build. The Unity EDM package (`com.unity.external-dependency-manager` 2.1.0, pulled by AdMob) is installed too, with its own switch `edmEnabled=false` in `ProjectSettings/ExternalDependencyManagerSettings.json`; the Google resolver is the one that runs. If the first release build fails with a missing `com.google.android.gms.games` class, run **Assets > External Dependency Manager > Android Resolver > Force Resolve** once and check the `Assets/Plugins/Android` folder.
`play-services-nearby` is pulled by the same plugin file although the game does not use Nearby; it may add Bluetooth/location permissions to the merged manifest (check the merged manifest and the Play Console permissions page).

### 4.5 Test checklist on a device

1. Clean install (uninstall first). Internal-testing build from Play, or the sideloaded APK signed with a key whose SHA-1 you added in step 4.
2. First launch: automatic sign-in runs silently. Settings > "Sign in with Google" opens the Google sheet if it did not sign in by itself.
3. Play a level, Settings > Save Progress ("Saved to Google"). Uninstall, install, sign in: the progress returns.
4. Logcat tags: `GamesSDK`, `Unity`. `DeveloperError` or status 10 means a missing or wrong SHA-1 credential.

## 5. Purchases

Google Play Billing is the record of purchases. Non-consumables and subscriptions are tied to the Google account of the Play Store, not to our login. **Restore Purchases** (`IPaymentService.Restore`, called by the Settings owner) re-delivers non-consumables and active subscriptions from Play on any device signed in to that Google account; the game grants them again on `Restore` success. Nothing else is built here. Consumables (gems) are only in the save, so they travel with the cloud save, not through Billing: a guest who loses the phone loses gems (this is why the login screen offers Google first).

## 6. Test checklist (editor with the stub, then device with the SDK)

- Editor: first run shows the login. "Sign in with Google Play" signs in as Test Player (stub). Delete `fluffy.stub.cloud` and `fluffy.stub.signedin` in PlayerPrefs to reset the fake cloud.
- Guest then Settings sign-in with a non-empty fake cloud: popup with both summaries, each button works, the other save is replaced.
- Kill the app mid-upload / airplane mode: the game keeps playing, the next upload happens later.
- Two devices, same Google account: play on A, open B: B takes A's save silently; play on both while offline: B shows the popup.

## 7. Risks and open points

- **Plugin API checked against the sources, not compiled.** `GooglePlayGamesAccountService` was checked name by name against `Library/PackageCache/com.google.play.games` (`PlayGamesPlatform.Instance/Authenticate/ManuallyAuthenticate/IsAuthenticated/GetUserId/GetUserDisplayName`, `SavedGame.OpenWithManualConflictResolution/ReadBinaryData/CommitUpdate`, `SignInStatus.Success/Canceled/InternalError`, assembly `Google.Play.Games`); it compiles only inside Unity with `FLUFFY_GPGS`. Conflicts between two devices that wrote at once are resolved by the game (`SaveSync.PickGreater`: readable, more levels cleared, more hero copies, newer, bigger wallet), not by the plugin.
- Play Games has no sign-out; "Sign out" only makes the game stop using the account. The next "Sign in" shows the sheet or returns at once.
- Play Games player ids are per game; a player who changes the Google account on the phone is asked before anything is overwritten (rule 5).
- A guest who never signs in has no recovery after uninstall (Android Auto Backup of `save.json` may help, not relied on).
- Cheating: the save is plain JSON and trusted. A later server (docs/13) can validate; out of scope (no own server by owner decision).
- Existing installs with progress and `loginChosen` false see the login screen once; "Play as guest" keeps their save untouched.
- The conflict popup needs the Meta canvas. If a conflict is found while another scene is up, the check is repeated the next time Meta starts.
- Data safety: consent and privacy policy must mention Google Play Games Services and the cloud save (store listing).

## 8. Home screen, Google connect pop-up and Account pop-up (owner issue 35.3, 2026-11-03; reworked by Bug List 1 B1.a to B1.e)

- **Home (Bug List 1)**: no bottom tab bar any more, no Shop / Heroes / Rewards buttons, no coin / gem / star pills, no round Account "G" button and no gear in the top row. What stays: the title, Momo, the background, the version label and the bundle timer button. Code: `MetaController.ShowMenu` and `MetaController.Home.cs`.
  - **Play** (B1.a): one green icon-only button (the `icon_play` triangle in an Image named `Icon`, centred both ways, no text), full safe width minus 16 units on each side, 170 units high (never below 48 dp), 24 units above the bottom of the safe area (so above the gesture inset). Accessibility label "Play". Opens the world map.
  - **Settings** (B1.b): a blue plate (150 x 150, never below 48 dp) with the gear (`icon_settings`, Image named `Icon`, so the final OpenArt gear can replace the sprite) directly above Play at the right, where the Daily Rewards button used to be. Label "Settings". The version label sits at the bottom centre at the same height.
  - Daily Rewards now lives on the world map bottom bar; the Shop and Heroes are reached from the map bar.
- **Google connect pop-up** (B1.c, `GoogleConnectPrompt.cs`, shown by `MetaController.MaybeShowGooglePrompt` at the end of `ShowMenu`): a compact card with a Google-style "G" badge, the one line "Keep your progress safe: connect Google in Settings." and two icon buttons: the gear "Open Settings" (green, closes the pop-up and opens Settings) and the cross "Later" (closes). The back key (Escape) acts as Later. It is dismissible and has no other action. Google sign-in itself is only in Settings.
  - Rules (pure, `GoogleConnectPrompt.ShouldShow`, owner answers): only after the player completed a level (first test run: any level with `Clears > 0`), only while a guest (`AccountPopup.State.Guest`; not when signed in with Google, not in a build without sign-in), at most once every 3 calendar days (UTC date difference of `SaveData.lastGooglePromptUtc`, an additive string field, no version bump; an unreadable stamp counts as never shown, a clock set back does not block it), at most once per app session (`GoogleConnectPrompt.ShownThisSession`), and only on Home (so never during a run, and not while a hero reveal pop-up is open). The stamp is written when the pop-up opens (`ProgressService.MarkGooglePromptShown`).
- **Account pop-up** (`AccountPopup.cs`, `MetaController.ShowAccount`): no longer opened from Home. The pop-up and the `AccountPopup` state logic stay because Settings uses them (the account block in Settings and `ShowAccount`).
  - Guest: **Sign in with Google Play** (`AccountSync.SignInAndLink`; it shows the choose-your-save popup itself when needed). The status line shows "Signing in...", then the failure message, or the popup rebuilds into the signed-in state.
  - Signed in: **Sync now** (`AccountSync.SyncNow`: uploads at once when in step, otherwise signs in quietly and compares the saves again) and **Sign out** (`AccountSync.SignOut`: back to guest, progress stays on the device, cloud untouched).
  - Unavailable (release build without the Google plugin and no linked account): "Google sign-in is not available in this build." and only the close X.
- **Settings** (Bug List 1 B2, reworked 2026-11-03, see doc 38): the account block moved into the compact Settings popup as the first of two big buttons: **Sign in with Google** (white Google-style button with the G badge; guest) or **Unlink Google Account** (same button, signed in; AccountPopup.RunPrimary, which signs out of the link: progress stays on the device, cloud untouched). Nothing but a one-line notice when this build cannot sign in. The screen rebuilds after a success, a failure opens a message popup. No account line any more (the button label says the state).
- **Save Progress** (Settings, second big button, AccountPopup.SaveProgress): always writes the save to the phone first (ProgressService.SaveLocalNow). Guest: the button label shows **Saved on this phone** for 2.5 s. Signed in with Google: also uploads to Google Saved Games (AccountSync.SyncNow, the SaveSync upload), label **Saved to Google**, or **Saved on phone only** when the upload failed (it is retried later). The result is shown on the button itself (a short toast, no extra layer, no extra height).
- **Tests**: `GoogleConnectPromptTests` (EditMode: first completed level, guest only, once per session, 3 calendar days, unreadable stamp, clock set back, stamp saved); `AccountPopupTests` (EditMode: state, letter, labels, sign-in/out, Sync now, Save Progress guest = local write, signed in = cloud stub receives the save, failure keeps the phone copy); PlayMode `HomeHasOneBigPlayButtonAndSettingsAboveIt` (no bar / wallet / Account / Rewards on Home, Play end to end with a centred icon and no text, Settings above Play, 48 dp, labels), `GooglePromptHasIconButtonsThatOpenSettingsOrClose`, `GooglePromptShowsOnHomeOnlyAfterAFirstCompletedLevelAndOncePerSession`, `AccountPopupStatesAndActions`; CriticalUX / TextOnArt audit screens `menu`, `menu-signed-in`, `google-prompt`, `account-popup-*`, `settings-*` (guest, signed in, unavailable, Admin icon, Save result on the button; all aspects, notch, 130 percent text). Test set-ups switch the automatic pop-up off (`GoogleConnectPrompt.AutoShow = false`) so the plain Home is audited.
- **Open points**: the Play and Settings icons use the existing kit sprites; the final OpenArt gear is a sprite swap on the `Icon` image. The pop-up G is the same text G as the Google sign-in button, not the official Google logo.

## Rename note (2026-11-09)
The game is now "Fluffy Climbers: Tower Jump", package `com.jdlabs.fluffyclimbers` (old id never uploaded, no migration). The Google Play Games Services project, the OAuth client (package name + signing SHA-1) and the Firebase app must be created / registered with the NEW package name; the old id has no entries to move.
