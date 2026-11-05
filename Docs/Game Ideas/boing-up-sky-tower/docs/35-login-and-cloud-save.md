# 35. Login (Google Play Games or guest) and cloud save

Owner Issue 34 item 1. Decisions: both ways to play; progress in **Google Play Games Saved Games**, purchases in **Google Play Billing**; **no own server**.
Package id: `com.jdlabs.boingupskytower` (Android only; the Play Games plugin no longer supports iOS).

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
UTF-8 JSON `SaveEnvelope { format, savedUtc, saveVersion, summary{levels,gems,coins,heroCopies,lastPlayed}, data = SaveData }`, file name `boing_save`.
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
| `Services/Account/StubAccountService.cs` | Editor and development builds: signs in as "Test Player", fake cloud blob in PlayerPrefs (`boing.stub.cloud`). Test switches: `FailSignIn`, `CancelSignIn`, `FailCloud`, `UseMemoryOnly`, `SeedCloud`. |
| `Services/Account/UnavailableAccountService.cs` | Release build without the SDK: login screen shows only "Play as guest". |
| `Services/GooglePlay/GooglePlayGamesAccountService.cs` + asmdef | Real service, compiled only with `BOING_GPGS`. |
| `Services/Services.cs` | `Services.Account` (stub in the editor / dev build, Play Games with `BOING_GPGS` on a device, else unavailable). |
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

## 4. Owner setup (Google Play Console and Unity)

### 4.1 Play Console
1. Play Console, open the game, **Grow users > Play Games Services > Setup and management > Configuration**. Create the Play Games Services project (it creates a linked Google Cloud project). Name, category, description.
2. **Credentials > Add credential > Android**: an **OAuth client** for package `com.jdlabs.boingupskytower`. It needs the **SHA-1 of the signing key**. Add **one credential per key**:
   - the **Play App Signing** key (Play Console > Test and release > Setup > App signing: "App signing key certificate" SHA-1) for builds installed from Play,
   - the **upload key** SHA-1 and/or the **debug / local keystore** SHA-1 (Unity Keystore Manager; `keytool -list -v -keystore <file>`) for sideloaded test builds. A wrong or missing SHA-1 gives sign-in failure with `DeveloperError`.
3. **Enable features > Saved games** (Setup and management > Enable features). Without it `OpenWithAutomaticConflictResolution` fails.
4. **Testers**: while the Play Games project is unpublished only listed accounts can sign in. Add the tester Google accounts (Play Games Services > Testers) and put them on an internal testing track. Publish the Play Games Services project before the public release.
5. **Get resources** (Configuration page): copy the **Android (XML)** resources.
6. Billing (existing, see docs/33): products live in the same Play Console app; nothing new for login.

### 4.2 Unity
1. Plugin: **Google Play Games plugin for Unity**, github.com/playgameservices/play-games-plugin-for-unity. Latest release seen on its Releases page: **v2.3.0** (29 Sep). Download the `.unitypackage` from the repository folder `current-build` (`GooglePlayGamesPluginForUnity-2.3.0.unitypackage`; use the newest file there). It is not on the Unity registry, and no OpenUPM entry was confirmed, so use the `.unitypackage` (do not edit `Packages/manifest.json`).
2. **Assets > Import Package > Custom Package** and import everything. Platform must be **Android**, scripting backend **IL2CPP**.
3. **Window > Google Play Games > Setup > Android Setup**: paste the Android resources XML from step 5 above, set the constants folder/class, **Setup**. Then **Nearby Connections Setup** with the package id (the dialog needs it even though the game does not use Nearby).
4. Check the plugin's assembly definition name. The code in `Services/GooglePlay` references **`Google.Play.Games`** (`BoingUpSkyTower.Services.GooglePlay.asmdef`). Find the plugin's `.asmdef` under `Assets/GooglePlayGames/...` and, if its `name` differs, edit the reference in our asmdef. If the plugin has no asmdef, move our asmdef folder content to a folder compiled with the plugin or add the plugin to an asmdef (decide then).
5. **Project Settings > Player > Other Settings > Scripting Define Symbols** (Android): add **`BOING_GPGS`**. The project still compiles without it (all SDK code is behind the define and the asmdef has `defineConstraints`).
6. Optional: manifest flag `com.google.android.gms.games.SUPPRESS_GAME_PROFILE_CREATION` is **not** set, so Play Games offers to create a profile itself on first use.
7. Clean install rule (owner memory): uninstall then install on the phone, never keep an old save. To test the conflict popup: play as guest, progress a level, then sign in with a Google account that already has a save from another install.

## 5. Purchases

Google Play Billing is the record of purchases. Non-consumables and subscriptions are tied to the Google account of the Play Store, not to our login. **Restore Purchases** (`IPaymentService.Restore`, called by the Settings owner) re-delivers non-consumables and active subscriptions from Play on any device signed in to that Google account; the game grants them again on `Restore` success. Nothing else is built here. Consumables (gems) are only in the save, so they travel with the cloud save, not through Billing: a guest who loses the phone loses gems (this is why the login screen offers Google first).

## 6. Test checklist (editor with the stub, then device with the SDK)

- Editor: first run shows the login. "Sign in with Google Play" signs in as Test Player (stub). Delete `boing.stub.cloud` and `boing.stub.signedin` in PlayerPrefs to reset the fake cloud.
- Guest then Settings sign-in with a non-empty fake cloud: popup with both summaries, each button works, the other save is replaced.
- Kill the app mid-upload / airplane mode: the game keeps playing, the next upload happens later.
- Two devices, same Google account: play on A, open B: B takes A's save silently; play on both while offline: B shows the popup.

## 7. Risks and open points

- **Plugin API not compiled here.** `GooglePlayGamesAccountService` was written from the plugin documentation (Authenticate, ManuallyAuthenticate, `ISavedGameClient.OpenWithAutomaticConflictResolution / ReadBinaryData / CommitUpdate`). Unverified names to check at the first compile with the plugin: `GetUserId()`, `GetUserDisplayName()`, `ConflictResolutionStrategy.UseMostRecentlySaved` (the documented example uses `UseLongestPlaytime`), `SignInStatus.Canceled`, and the asmdef name `Google.Play.Games`.
- Play Games has no sign-out; "Sign out" only makes the game stop using the account. The next "Sign in" shows the sheet or returns at once.
- Play Games player ids are per game; a player who changes the Google account on the phone is asked before anything is overwritten (rule 5).
- Cloud conflicts inside the plugin (two devices writing at once) resolve to the most recently saved file; our rules then run on the next start.
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
