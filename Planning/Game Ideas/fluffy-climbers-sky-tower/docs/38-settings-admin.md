# 38 Settings: Admin tools, Reset Progress, Restore Purchases

Owner Issue 34, item 5; reworked by Bug List 1 B2 and B3 (2026-11-03). Code: `Gameplay/MetaController.Settings.cs`, `Gameplay/MetaController.Admin.cs`, `Gameplay/AdminAccess.cs`, `Economy/AdminGate.cs`, `Economy/ProgressService.Purchases.cs`.

## Who sees the Admin button
- Development builds (`Debug.isDebugBuild`, which is what the normal test builds are): always.
- Release builds: hidden. Tap the version label at the bottom right of the Settings popup 7 times (each tap within 2 s of the last), type the code in the small box, press OK. A wrong or empty code does nothing (the box stays). A right code turns admin mode on for this install (PlayerPref `settings.adminunlocked`) and opens the Admin popup.
- The code is `adminCode` in `Assets/Resources/Services/admin_config.json`. Default (documented): `0000-TEST`. Compare ignores spaces and letter case. **Change it before a public release.** "Lock admin again" in the popup turns the mode off.
- While admin mode was opened by code in a release build, System Test starts OFF (in development builds it still starts ON).

## Settings screen (Bug List 1 B2, B3.c)
Reference: SS 21 (small popup, title, a light inner panel with icon rows and green switches, then big buttons, version bottom right), but **only Google Play**: no mail, Facebook, Reddit, Discord, language, privacy or terms links. Code: `Gameplay/MetaController.Settings.cs` (`ShowSettings`), `MetaController.Admin.cs` (footer), `AccountPopup.cs` (labels, `SaveProgress`).

One compact popup (860 wide, height measured from its content, about 1450 units at 100 percent text instead of the old 2000+ scrolling card; it only scrolls when it would pass 1470 units, e.g. very high touch-target density). Close X top right (back to Home; no Back button). Sky background. Icons instead of text, all from the existing set in `Resources/Art/Ui` (no new OpenArt art):
| Row | Content |
|---|---|
| 1 | `icon_sound` + green switch, `icon_music` + green switch |
| 2 | `icon_vibrate` (Haptics) + switch, `icon_motion` (Reduced motion) + switch |
| 3 | `icon_hand` (Left-handed) + switch, `icon_size` + value button "100%" (cycles 100/115/130, rebuilds the popup) |
| 4 | `icon_dial` + value button with the difficulty name (opens the difficulty list) |
| Big button 1 | Guest: **Sign in with Google** (G badge pill). Signed in: **Unlink Google Account**. No sign-in in this build: a one-line notice instead. |
| Big button 2 | **Save Progress**: guest saves on this phone, Google player saves to Google Saved Games; the result replaces the label for 2.5 s ("Saved on this phone", "Saved to Google", "Saved on phone only") |
| Small row | **Reset** (red, `icon_restart`, confirm popup stays) and **Restore** (yellow, `icon_shop` bag, `RestorePurchases`) side by side, each with a short word because an icon alone would be ambiguous |
| Footer | small **Admin** icon button (`icon_lock`, blue, bottom left, only when allowed) and the version label (bottom right, 7 quick taps open the code box) |
Switch cells carry no visible label (the icon explains them); screen readers get "Sound, on" and so on. Rows are at least 48 dp tall (grown with the screen density) with 8 dp or more between neighbours; the two big buttons are 140 to 170 units high so the label can wrap to two lines at 130 percent text.

## Admin popup (Bug List 1 B3)
Opened by the Admin icon (still behind the code gate above). Height follows the content; it scrolls only when it does not fit (cap 1470 units). A status line under the title shows the result of the last action.

1. **Pill**: green **PROD READY** (System Test off) or orange **TEST MODE** (System Test on). The words differ, not only the colour.
2. **System Test** master switch (`GameSettings.SystemTest`). OFF = PROD READY: **turning it off sets Game Test, Shop Test and Character Test off too and stores that** (`PlayerPrefs`), so a release candidate is clean; turning it on again does not bring the three back by itself. It also gates Autopilot.
3. Below it, **enabled only while System Test is on** (otherwise the rows are dimmed to 45 percent and cannot be touched, and their tool buttons are hidden, which keeps the popup short):
| Switch | Flag | Tools shown while its switch is on |
|---|---|---|
| **Game Test** (the old Jump test, "All levels open") | `GameSettings.GameJumpTest` | Open regions (writes the real unlock list), Clear levels (75 levels, 3 stars) |
| **Game Test, Main rewards** (shown whenever System Test is on) | `ProgressService.AdminSetMainRewardsReady` (save field `rewards.testMainReady`, additive) | Toggle button "Main rewards ON/OFF": every Main reward (5 region rewards, all star cards) reads complete but unclaimed and unpaid so each can be reviewed or claimed in Rewards > Main. Real progress is untouched; OFF restores it. System Test OFF clears it. |
| **Shop Test** | `GameSettings.ShopTest` (test purchases, `ShopScreen.TestPurchasesEnabled`) | Big wallet (+1,000,000 coins, +100,000 gems), VIP monthly, +1,000 coins, +100 gems, Buy 50 gems (store stub, recorded like a real purchase), Rewarded ad |
| **Character Test** | `GameSettings.CharacterTest` (every hero owned, `ProgressService.TestAllCharacters`) | Pick a hero... (list of `CharacterDefs.All` with +1 / +2 copies) |
| **Cloud Test** (a heading, shown while System Test is on) | none | signed in or out, Sign in (Google), Sign out, Upload save |
4. **Admin mode** (only when opened by code): "Lock admin again".

Defaults (owner 2026-11-10): System Test, Game Test, Shop Test and Character Test all default to OFF everywhere, so a fresh development install reads PROD READY (the Admin button still shows in a development build; a release build shows it only after the code). They never count while System Test is off. Every switch change rebuilds the popup and keeps the scroll position. Not built: infinite revives and skipping a level goal (the run code has no hook for them yet).
## Reset Progress
Confirm popup: "This deletes levels, heroes, gems and coins on this device." plus "Your cloud save is reset too." for a Google player and "Purchases are kept.". Cancel or the red Reset. Reset calls `ProgressService.ResetAll` (fresh save, login choice and linked account kept, **purchases list kept**), selects Momo, uploads the fresh state to the Google account when signed in, and returns to the menu.

## Purchases record and Restore Purchases
- `SaveData.purchases` (additive): productId, kind (consumable / nonconsumable / subscription), transactionId, utcTicks, days. `ProgressService.RecordPurchase` stores a (product, transaction) pair once. Today every test or stub purchase in the shop, the subscription screen and the Admin popup is recorded; the real Google Play flow must call `RecordPurchase` in its purchase callback (and on redelivery). Google Play Billing stays the source of truth for signed-in players.
- Restore Purchases: `Services.Payments.Restore` (store redelivery), then `ProgressService.RestoreFromRecords`: a subscription whose paid days (stacked in purchase order) are not over is re-granted (no instant gems again). Gem packs are consumables and are never granted twice. Message: "Purchases restored: N" or "Nothing to restore"; a store error with nothing to restore shows "Store not reachable".

## Tests
EditMode `AdminSettingsTests` (gating by build and code, wrong code does nothing, config default, **grouping and pill texts, System Test off clears and persists every test switch, sub switches only count while System Test is on, each switch drives only its own area**, reset keeps purchases, restore re-grants and stacks, stub restore, product ids match `ProductCatalog`); EditMode `AccountPopupTests` (labels, **Save Progress: guest writes locally and never touches the cloud, signed in pushes to the cloud stub, failure still saves on the phone**); xunit `Tests/Simulation/AdminGateTests` (pure rules). PlayMode `SettingsPopupIsCompactAndEverythingStaysInside` (every control inside the popup, 48 dp, 8 dp gaps, under 1500 units, all four aspects), `SettingsGoogleButtonAndSaveProgress`, `AdminPopupSystemTestMasterSwitch`. CriticalUX layout audit screens (all aspects, notch, 100 and 130 percent text): `settings-guest`, `settings-signed-in`, `settings-unavailable`, `settings-guest-admin-icon`, `settings-guest-saved`, `admin-system-off`, `admin-system-on`, `admin-system-on-all-switches`, `admin-heroes`, `reset-popup`, `settings-reset-confirm`, `settings-restored`, `settings-nothing-to-restore`, `settings-code-box`.