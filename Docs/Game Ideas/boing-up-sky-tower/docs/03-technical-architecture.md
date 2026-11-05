# 03 - Technical Architecture

## 1. Stack
| Layer | Choice | Notes |
|---|---|---|
| Engine | Unity 6 LTS **(verify current LTS)**, 2D URP | Single codebase, IL2CPP builds for Android (ARM64) and iOS |
| Language | C# 9+ | Assembly definitions per module |
| Input | Unity Input System | Three on-screen buttons (Left, Right, Jump; mirrored when left-handed), haptics |
| Physics | Custom kinematic controller (not Rigidbody) | Deterministic, fixed timestep, replayable |
| Animation | Unity 2D Animation (PSD Importer, bones) + sprite swap | Paper-doll rig (see asset plan) |
| Content delivery | Addressables | Per-region bundles hosted on CDN |
| Backend | PlayFab (Economy v2, Leaderboards, Player Data, Title Data, CloudScript/Azure Functions, receipt validation) | Alternative: Firebase + Cloud Functions |
| Analytics/crash | Firebase Analytics + Crashlytics (or Unity Analytics) | Funnels for FTUE/IAP |
| IAP | Unity IAP, or RevenueCat for subscriptions/entitlements **(verify)** | Server validation via PlayFab |
| Ads | AppLovin MAX or Unity LevelPlay mediation | Rewarded first |
| Push | Firebase Cloud Messaging + APNs | Local notifications too |
| CI/CD | GitHub Actions or Unity Build Automation + fastlane | Automated store uploads |
| Source control | Git + Git LFS | Large binary assets |

## 2. Project structure
```
Assets/
  _Game/
    Core/           (bootstrap, DI container, event bus, logging)
    Gameplay/       (player controller, platforms, scroller, combo, hazards)
    Simulation/     (deterministic sim, replay recorder/validator)
    Content/        (definitions, ContentRepository, runtime catalog)
    Economy/        (wallet, shop, bundles, chests, subscription, rewards, power-ups)
    Backend/        (PlayFab client, auth, leaderboards, sync)
    Monetization/   (IAP, ads, consent)
    UI/             (screens, widgets, theming, localization)
    Audio/
    Tools/          (editor scripts, validators, importers)
  Content/          (ScriptableObjects authored in editor, exported to JSON)
  Art/              (source) -> built into Addressables groups
Packages/
Tests/              (EditMode, PlayMode, Simulation)
```
Use a lightweight DI approach (VContainer or hand-rolled service locator) and an event bus. Avoid singletons spread across scenes.

## 3. Scenes
- `Boot` (init services, consent, load config, check version)
- `Meta` (menu, map, heroes, shop, chests, rewards) - UI-driven, one scene with screens
- `Run` (gameplay) - loaded with the region's Addressable theme prefab

Update 2026-10-20: the `Meta` scene's world map is a scrollable screen over 10 chained OpenArt segments (5 stitched chunk images plus `world_map.json` with the cloud joints), with hand-placed pins for the playable regions and "Coming soon" tags for the rest (doc 08, world map). Dev builds carry the `TestBridge` that prints `BTSTATE` and `BTUI` lines to logcat (section 9).

## 4. Data-driven content model (the key to infinite growth)

All content types are plain data with stable string IDs. IDs never change once shipped.

```jsonc
// RegionDef
{ "id": "region_021_dragon_peak", "index": 21, "nameKey": "region.dragon_peak",
  "status": "live|teaser|hidden", "unlock": {"requires":"region_020", "gemSkip": 416},
  "theme": {"bundle": "region_021", "tileset": "...", "parallax": ["..."], "music": "..."},
  "rules": {"platformWeights": {...}, "hazards": ["fire_jet","gust"], "difficultyOffset": 0.17},
  "levels": ["lvl_021_01", "...", "lvl_021_15"], "availableFrom": "2026-12-01T00:00:00Z" }

// LevelDef
{ "id":"lvl_021_01", "region":"region_021_dragon_peak", "index":1, "seed":93211,
  "goal": {"type":"reachFloor","value":30}, "stars":[{"type":"clear"},{"type":"coins","value":10},{"type":"noRevive"}],  // stars[] is a legacy schema field: since 2026-10-20 every level has 3 collectible star pickups (doc 08, economy v2)
  "difficulty":0.45, "overrides":{"fixedChunks":["intro_a"]}, "rewards":{"firstClear":{"gems":2,"coins":100}} }

// ItemDef (character|powerup; gear slots were removed 2026-11-01, doc 27)
{ "id":"pw_flight", "slot":"powerup", "price":{"coins":1500,"gems":75}, "tags":["season1"], "availableFrom":"...", "availableTo":null }

// GemPackDef (maps to store SKU, usdList/usdPromo/inrList), CoinPackDef (coins, gemsList, gemsPromo), PowerUpDef, BundleDef (1,000 generated),
// SubscriptionDef (weekly, monthly), RewardCatalog (main, daily, weekly). Chests (Iron, Gold) are code constants (ChestRolls), doc 22.
```
- Authoring in Unity ScriptableObjects or spreadsheets (Google Sheets to JSON by a script) -> validated by CI -> published to PlayFab Title Data and CDN. The client downloads a **content manifest** (versioned) at boot.
- The client has a built-in fallback manifest so the game works offline and on first run.
- Sprite bundles are referenced by Addressable labels, e.g. `region_021`, `items_characters_01`.

### Procedural level generation
Chunk-based generator driven by seed + region rules + difficulty:
1. Library of hand-authored **chunks** (pattern of platforms) tagged by difficulty and type.
2. Generator picks chunks by weighted random using a seeded RNG (`xoshiro`) so output is identical on every device.
3. Verify solvability with a **headless bot** (a script that plays the sim) in CI for each LevelDef; fail the build if unwinnable.
4. Endless mode uses the same generator with increasing difficulty as a function of floor.

## 5. Deterministic simulation and anti-cheat
- Fixed timestep 1/60 s simulation, separate from rendering (Unity `FixedUpdate` or own loop).
- **Determinism rule:** the sim and the level generator must use fixed-point or integer math (and the seeded RNG), not floats, because IL2CPP on ARM and the server's .NET on x86 are not bit-identical for floats. Rendering may use floats. (Decision D9 in doc 09.)
- Inputs are recorded per tick (left/right/jump bitfield, run-length encoded). A run = `{seed, region, level, version, inputs[], score, floor}`.
- On submit, the server (CloudScript/Azure Function) **re-simulates** the replay with the same C# sim code (shared library compiled to .NET Standard) or validates plausibility (max score per second, max floor vs. time) for cheaper start.
- Plan: P1 plausibility checks + rate limit + shadow-ban flags; P2 full re-simulation for top-N scores before they appear on a global board.
- Client integrity: Play Integrity API (Android) and App Attest/DeviceCheck (iOS) **(verify)** attached to score submits for Ranked.
- Never trust client currency: balances live in PlayFab Economy; the client requests operations, the server decides.

## 6. Economy and purchases flow
```
Client taps buy -> store SDK purchase -> receipt -> PlayFab ValidateIOSReceipt / ValidateGooglePlayPurchase (CloudScript)
 -> server grants gems (idempotent by transactionId) -> client refreshes inventory -> acknowledge/finish transaction
```
- Idempotency key = store transaction ID; handle duplicates, pending, refunds (Google RTDN + Apple server notifications -> revoke).
- In-game item purchase with gems: server `PurchaseItem` call, atomic debit + grant.
- Chests (Iron, Gold): the roll is seeded by the chest kind and the running open count, so a server can recompute it; once a server wallet exists the roll is evaluated **server-side**. The contents are published in the chest (i) text (doc 22). No pity counter in the shipped version.
- Subscriptions: validate and store entitlement + expiry server-side; check on every launch.

## 7. Backend modules
| Module | PlayFab feature |
|---|---|
| Auth | Anonymous custom ID, link Google/Apple/email |
| Player data | Player Data/Inventory (progress, stars, loadout) |
| Catalog | Economy v2 catalog items + virtual currencies (coins, gems) |
| Leaderboards | Statistics v2 / Leaderboards (per region, weekly resets). About 180 boards (doc 08): verify PlayFab limits, fall back to a single keyed-score table. Friends boards use server-validated stats, not client-side compares of other players' data |
| Config | Title Data + Remote Config (tuning, flags, A/B) |
| Validation | CloudScript functions (receipts, scores, chests) |
| Events | Player events to analytics warehouse (optional) |

Weekly leaderboard resets use PlayFab's reset frequency setting; archive winners and grant rewards via scheduled task.

## 8. Performance plan
- Sprite atlases per region (SpriteAtlas V2), 2048 max, ASTC compression (Android/iOS).
- Object pooling for platforms/particles; platforms recycled in a ring buffer.
- No allocations in gameplay loops; profile with Unity Profiler and Memory Profiler.
- Resolution scaler, quality tiers, 30/60 fps toggle.
- Audio: compressed Vorbis/AAC, streaming for music. Placeholder set and import rules: doc 12.
- Target device matrix: low (Android Go, 2 GB), mid (Galaxy A-series), high; iPhone 8 and up.

## 9. Testing and quality
- Unit tests for sim, economy, content validation.
- Replay regression: golden replays must produce identical scores after code changes.
- Bot playthrough of every level in CI.
- **Update 2026-10-20, Android test kit:** `Tools/android_test/bt.py` (a small adb driver: install, launch, find by text, tap, hold, screenshot) and `Tools/android_test/smoke_test.py` (walks every screen, plays, checks layout and frame time). Both read the `TestBridge` logcat lines (development builds only): `BTSTATE {...}` (version, scene, fps, run state) and `BTUI {...}` (visible UI elements with screen rectangles). The smoke test passed 7/7 steps on the owner's OnePlus 13 on 2026-10-20.
- **AltTester** (in preparation, roadmap T26): SDK 2.3.2 in `Packages/manifest.json`, Python driver on E:, AltTester Desktop extracted under `E:/tools`. It complements, not replaces, the lightweight kit above.
- **Build versioning:** every APK is vMAJOR.MINOR.HOTFIX.BUILD from `Tools/version.json`, logged in `Content/builds/build_log.csv` (rules: game repo `.claude/rules/versioning.md`). Reports always name the installed version.
- Test counts at the time of writing (2026-10-20): dotnet simulation tests 97 (plays all 75 shipped levels with the bot), Unity EditMode 72 (84 with the endless tests being added), Unity PlayMode 13.
- Device farm smoke tests (Firebase Test Lab, BrowserStack) **(verify)**.
- Store sandbox/test tracks for IAP: Google Play license testers, Apple sandbox accounts.

## 10. Security & privacy checklist
No secrets in client; TLS only; PlayFab secret key only server-side; obfuscate (IL2CPP + code stripping); GDPR delete/export endpoints; ATT prompt only when ads need it; UMP consent for EEA/UK; Data Safety form and App Privacy nutrition labels filled accurately.
