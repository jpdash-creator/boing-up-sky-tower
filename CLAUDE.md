# Boing Up: Sky Tower - project guide for Claude

2D vertical-jumper for Android + iOS. Unity 6 LTS (C#), 2D URP. Data-driven: regions, levels, items and offers are JSON plus asset bundles, never hardcoded in scenes.

**Design docs** (source of truth, read before changing rules or numbers): `E:\Source\Game Ideas\boing-tower-docs\docs\`. Doc 08 (rules addendum) wins over docs 01, 02, 05. Doc 09 records the accepted review decisions.

## CRITICAL REQUIREMENTS (owner, 2026-10-19): seamless gameplay, no improper overlap, accessibility
User experience and accessibility are critical. Gameplay must be seamless and never choppy; screen items must never overlap improperly. The full mandatory rule set (CUX-1 to CUX-10) is in `.claude/rules/ux-gameplay-critical.md` (always loaded) and `docs/11-critical-ux-gameplay-requirements.md`. Headlines: 60 fps with fixed-step sim + interpolation, zero per-frame allocations, instant input, no overlapping or clipped UI on any aspect, 48 dp touch targets, readable contrast, no colour-only cues, reduced motion/haptics/left-handed options, clean sliced art. CUX tests (category `CriticalUX`) must stay green; a failing CUX test blocks a commit.

## Hard rules
- **OpenArt credits (MANDATORY GUARDRAIL):** never call a credit-consuming OpenArt tool (`openart_generate_image`, `openart_generate_video`, edits/upscales) without the owner's explicit chat approval for that exact batch. Ask first with full details: purpose, MCP and tool, model and settings, the full prompts, credits per job and total, balance before and after, alternatives. Full rule: `.claude/rules/openart-credit-guardrail.md`. Read-only OpenArt tools (account, model list/cost/form, creation list) are free to use.
- **Originality:** never mention, name or imitate any existing jumper game in code, art, prompts, store text or comments. Generic genre mechanics only.
- **Determinism:** the simulation and the level generator use fixed-point/integer math and the seeded RNG (xoshiro). No `float`, `UnityEngine.Random`, `Time.deltaTime` or `Physics` in `Simulation/` or generator code. Same seed gives the same level on every device and on the server.
- **Server authority:** currencies, purchases, chests and leaderboard scores are decided server-side. No secrets in the client.
- **Cosmetics only:** items never change gameplay stats. Ranked runs disallow consumables and permanent upgrades.
- **IDs are permanent:** content IDs (`region_021_dragon_peak`, `lvl_021_01`, `boots_cloud_runner`) never change once shipped.
- **No allocations** in gameplay loops. Pool platforms and particles.
- Behaviour is code (rare app updates); content is data + bundles (frequent). Only assets and data are downloaded, never code.

## Layout
```
Assets/_Game/{Core,Gameplay,Simulation,Content,Economy,Backend,Monetization,UI,Audio,Tools}  one asmdef per module (add when code appears)
Assets/Art/                        source art, built into Addressables groups
Content/schemas/*.schema.json      JSON schemas (RegionDef, LevelDef, ItemDef, GemPackDef, SignatureModuleDef)
Content/{regions,levels,items,economy,config}  shipped content data
Tools/content_cli/                 gen_content.py (baseline generator), content.py (validator)
Tests/{content,EditMode,PlayMode,Simulation}
```

## Commands (run from repo root; needs Python 3.10+, stdlib only)
```
python Tools/content_cli/content.py validate         # schemas + doc 08 rule checks; run before every commit
python -m unittest discover -s Tests/content -v      # CLI and shipped-content tests
dotnet test Tests/Simulation                         # .NET 10 SDK; sim, generator and bot (plays all 450 shipped levels, about 20 s)
python Tools/content_cli/gen_content.py              # regenerate baseline content from doc 08 (overwrites Content/regions, levels, economy, items, config)
```
`gen_content.py` regenerates generated files; if you hand-edit content, change the generator or stop using it for that file.

## Unity headless (Editor 6000.3.26f1 on E:)
Run: `E:\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe -batchmode -nographics -projectPath E:\Code\boing-tower-game -executeMethod <Class.Method> -logFile E:\Unity\logs\<name>.log` (add `-quit` unless the method exits itself). Tests: add `-runTests -testPlatform EditMode -testResults <xml>`. Needs an active Unity Personal license (Unity Hub signed in); exit code 198 means no license. Setup scripts are in `Assets/Editor/ProjectBootstrap/` (PackageInstaller, ProjectSetup, ArtImportPostprocessor). Final art lives under `Assets/Art/`; the 195-image candidate archive stays in `Art/_candidates/` (outside Unity, so it is not imported). Roadmap and status: `.claude/roadmap-5-regions.json`, `python Tools/status_ticker.py`.

## Assets
Full asset request catalog (367 requests, prompts, batches, credit estimates): `Content/assets/CATALOG.md`; workflow and roles: `Content/assets/README.md`. Regenerate with `python Tools/asset_cli/gen_asset_manifest.py`. All generation is gated by the OpenArt credit guardrail above.

## Pending owner decisions
Open items waiting on the owner are logged in `.claude/pendingdecisions/` (asset pick verification, other open items). Read it at session start; update it when an item closes. Default art picks (image 1) are placeholders until the owner verifies them.

## Key numbers (doc 08)
- 30 regions x 15 levels; regions 1-20 `live`, 21-30 `teaser`. **Playable now: regions 1-5 (75 levels)**; the world map shows 6-30 as "Coming soon". Level classes: L5/L10 complex, L15 highly complex.
- Scroll (owner, 2026-10-20): the screen scrolls after the first jump, the bottom edge kills, base 120 percent, steps 20/40/60/80/100 percent per 5 s by difficulty (Settings), cap +400 percent, "SPEED UP +N%" cue. Jump: `JumpBase` 19, `JumpSpeedFactor` 0.58, standing peak 4.51 u = 1.3 median row gaps, full speed 7.33 u = 2.1 gaps, normal gaps capped at 4.2 u (layout uses `LayoutJumpBase`, see `Assets/_Game/Simulation/NOTES.md`).
- Economy v2: no coin pickups; 3 collectible stars per level; gems in rarity tiers 1/2/5/10 (70/22/7/1 percent); coins paid at the end = 30 + 40 per star + 5 per combo step (times region factor); 3-star replays pay 50 percent and drop no gems; revive 2 gems then 5 (max 2 per run).
- Fonts: Lilita One (display) + Nunito Bold (body), `Assets/Resources/Fonts`.
- Region offset `d_eff = d + offset * (1 - d)`, offset 0.0 to 0.25 across regions 1-30.
- Endless speed: +4% per 25 floors, cap +150%. Constants in `Content/config/remote_config.json`.
- Endless mode (owner decision 2026-10-20): "Endless" pill at the bottom of each region's level screen (shows `best N`). `EndlessTower` (Simulation) grows in 40-row chunks from a per-run seed (`GameSession.EndlessSeed`), `RunSession(repo, cfg, region, seed)`, `RunView` creates/recycles platform views on demand, HUD shows Floor N / Best N, game over has no revive. Coins: `floors/2 + 50 x (floors/100)` x region factor, 3 full-rate runs per UTC day then x0.5; best floor per region in the save. Details: `Assets/_Game/Simulation/NOTES.md`, `Assets/_Game/Economy/NOTES.md`.
- Coins : gems : USD = 100 : 1 : about $0.011. Gem packs in `Content/economy/gem_packs.json` and coin packs (bought with gems) in `coin_packs.json`; the owner shop tables of docs/14 replace the older ladder (see the conflict list there).
- Score = floors x 10 + combo^2 x 5 (the sim score has no coins term since economy v2; doc 08 still lists "+ coins"). Floor = platform row index. Screen about 10 units wide. These are prototype values.

## Open / next
- **Milestone state (2026-10-20): pre-release v0.9.x on Android (OnePlus 13), 5 regions x 15 levels playable end to end**, bot-proven solvable. In progress: endless mode (Endless button on each region's level screen), AltTester (T26), IAP plan (`docs/13-iap-ads-implementation-plan.md`), polish pass (T20), platform art for regions 2-5 (owner picks). Docs were synced to the owner decisions of lists 1 to 10 on 2026-10-20 (docs 00 to 09, 12).
- Status and task list: `.claude/roadmap-5-regions.json` (`python Tools/status_ticker.py`). Done: T0 Unity setup, T2 runtime content, T3 gameplay completeness, T4 bot solvability (75 of 75 star-1 goals for regions 1-5), T8 save/wallet/rewards/unlocks (`Assets/_Game/Economy`, notes in its NOTES.md), T1 art pipeline (`Tools/art_pipeline/process_art.py`, notes in `Assets/Art/NOTES.md`).
- Simulation (`Assets/_Game/Simulation`, plain C# 9, no floats): Fx, Rng, SimConfig, Sim, LevelGenerator, LevelBuilder/LevelSpec/LevelData, Chunks, Goals (GoalEvaluator, StarEvaluator), LevelBot. API summary: `Assets/_Game/Simulation/NOTES.md`. Content side: `Assets/_Game/Content/NOTES.md` (ContentRepository, LevelSpecFactory).
- Determinism contract: `Content/golden/level_hashes_r1-5.txt` (dotnet writes it; the Unity test `LevelSpecFactoryTests` must match). Regenerate only for an intended generator change: set `UPDATE_GOLDEN=1` and run `dotnet test Tests/Simulation`.
- Test counts to keep green (2026-10-20): dotnet 97 (`dotnet test Tests/Simulation`, ~2 min), Unity EditMode 72 (7 `CriticalUX` accessibility; the endless tests add more), Unity PlayMode 13 (5 `CriticalUX`; run `Tools/unity_tests.ps1 -Platform PlayMode`, needs graphics for screenshots in `E:/Unity/shots`), art QA `python Tools/art_pipeline/qa_art.py`.
- Physics constants are owner-retuned from phone tests (lists 6 and 7, 2026-10-20: jump numbers above) but still need the T9 playtest before they are frozen (doc 09 D4); builder caps follow `NormalGapMax`.
- Owner questions from T3/T4 were answered on 2026-10-19 (`.claude/pendingdecisions/decisions-2026-10-19.md`); coin stars and the time goal were then retired by economy v2. Open items (gem inflow vs. scarcity, replay rate in data, art picks): doc 09 section F and `.claude/pendingdecisions/`.
- Signature modules (`Content/config/modules.json`) are all `planned`; regions 1-5 now have working signatures (bounce flowers, bounce caps, drifting, sticky gum, gongs); track `buildStatus` there.

## Conventions
C# 9+, assembly definitions per module, DI via VContainer or a small service locator plus an event bus, no scattered singletons. Tests: EditMode for pure logic, a Simulation suite with golden replays. Art follows `Art/<Class>/<id>_<layer>.png`, lowercase snake_case, prompts and seeds logged in `assets_log.csv`.

## Device testing (Android)
`Tools/unity_tests.ps1 -Platform PlayMode|EditMode` runs the Unity tests and prints failures. `Assets/Editor/Build/AndroidBuild.cs` builds the development APK (`-executeMethod BoingUpSkyTower.Editor.Build.AndroidBuild.BuildFromCommandLine`, output `E:/Unity/builds/boingupskytower.apk`). `python Tools/android_test/smoke_test.py` installs it on the connected phone (the phone asks to confirm each install), walks every screen, plays, and checks layout and frame time through the in-game TestBridge (development builds only). `Tools/android_test/bt.py` is the reusable adb driver (find by text, tap, hold, screenshot); `TestBridge` prints `BTSTATE {...}` (version, scene, fps, run state) and `BTUI {...}` (visible UI elements with screen rectangles) to logcat. AltTester (SDK 2.3.2 in `Packages/manifest.json`, Python driver and Desktop on E:) is being added by a separate task (T26) next to this kit. Settings has "Test: all levels open" (development builds only, default on) so every level of the 5 regions can be reached while testing. Owner feedback and decisions: `.claude/pendingdecisions/device-feedback-2026-10-19.md`.


## Build versioning
Every build uses the semantic version vMAJOR.MINOR.HOTFIX.BUILD (file Tools/version.json, rules in .claude/rules/versioning.md). Bump minor (feature or rule change) or hotfix (fix only) with `python Tools/version.py bump ...` before the build; BUILD is incremented by the build script. Name the installed version in every report.
