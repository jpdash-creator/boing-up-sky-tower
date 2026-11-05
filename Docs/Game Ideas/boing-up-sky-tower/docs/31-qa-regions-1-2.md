# 31. QA pass: Region 1 and Region 2 (owner issue list 31, item 3, 2026-11-02)

Scope: "look at the game plan for Region 1 and 2 and do complete testing from your side to see if there are any bugs." Read-only review plus my own measurements. No code was changed, Unity was never run, no OpenArt tool was used.

Read: docs 24, 25, 30 (plan), 22, 26 (economy, rewards), CLAUDE.md; `Assets/_Game/Simulation` (Platform, SimConfig, Sim, SpecialPlatforms, LevelBuilder, LevelSpec, EndlessTower, LevelBot, Goals, Level), `Assets/_Game/Gameplay` (RunView, SpecialPlatformFx, ArtCatalog, RunSession, Autopilot*), `Assets/_Game/Economy/ProgressService.Rewards.cs`, the Region 1/2 platform art folders and the existing tests.

How I measured (nothing in the repo was touched): a scratch console project that compiles the Simulation sources, `Tests/Simulation/ContentLoader.cs` and `AutopilotPlanner.cs` straight from `Assets/`. It lives in `E:/Unity/qa/qa_harness_regions12/` (copy of the scratch folder; `dotnet build -o <own dir>` with the usual env vars, then `qa.dll layout|bot|auto|revive|misc|endless|pick|probe|thorn`). All numbers below come from it unless a file is named. Build under test: working tree of 2026-11-02 (LevelBuilder.cs last changed 00:39). Installed phone build is not part of this review.

## 1. Result in one screen

| Rank | ID | Severity | Finding |
|---|---|---|---|
| 1 | B1 | HIGH | A revive puts the player on the best platform even when that platform is not solid at the death tick: a closing or shut **Snap Daisy** (90 to 209 ticks after the landing), a crumbled **Crumble** log (Region 2 keeps Crumble) or a hidden **Vanishing** platform. The player falls again at once, the second (10 gem) revive is burnt for nothing. |
| 2 | B2 | MEDIUM | Revive onto a **Spore Pop** in its swell phase: the clock is not reset, the revived player is flung 2 ticks later. |
| 3 | B3 | MEDIUM | There is NO autoplay result for the current build. The Nov 1 phone run produced no data (CSV holds only an adb error), and "4 of 15" is the Oct 23 build (v0.14.1.12) before the 14 new kinds existed. 3 of 4 phone suites also stalled silently. |
| 4 | B4 | MEDIUM | Root cause of "platform K outside the walls" (lvl_002_xx): `Platform.Amplitude` means "sideways swing" for Moving, Ladybug, Orbit Cap but "hole half width" for the **Fairy Ring**. The validator and the builder treat it as a swing for every kind. The ring is inside the walls; the check was wrong. The current tree clamps rings by the hole too (0 violations now) but that moves rings and neighbours for no physical reason. |
| 5 | B5 | LOW | Revive onto a **Ladybug** places the player on the unboosted path while the platform runs on the boosted one. |
| 6 | B6 | LOW | Endless dense patches ignore the swing of a Ladybug or Orbit Cap spine platform, so extras touch it (17 + 8 cases in 25 seeds x 400 rows). |
| 7 | B7 | LOW | Run view: platforms are drawn at the current tick while the player is interpolated (jitter on moving platforms at more than 60 Hz); Squish cap and Seesaw snap back in one frame. |
| 8 | B8 | LOW | Rule breach: `double` / `Math.Round` in `Simulation/CoinPayoutRules.cs` (payout maths) and `CharacterStats.cs`; the simulation proper is clean. |
| 9 | B9 | LOW | Bot visited-key aliasing for periods that do not divide 480 ticks; dead art and flags; doc drift (section 6). |

Autoplay question: **not a game bug.** Same planner and brain in my harness, current code: skill 0 (perfect) clears 27 of 30 Region 1 runs and 30 of 30 Region 2 runs; skill 0.2 clears 13 of 30; skill 0.5 clears 7 of 30, and Region 3 (which has none of the new platforms) clears the same 7 of 30 at skill 0.5. Details in section 4.

## 2. Ranked bug list

### B1 (HIGH) Revive onto a platform that is not solid
- Evidence: `Sim.cs:244-272` `Revive` sets `GroundId`, `X`, `Y`, `Grounded` and never looks at the platform's clock. `IsSolid` (`Sim.cs:488`) reads `CrumbleTick[id]` and the tick: Snap Daisy shut = `tick - landed` in [90, 210), Crumble gone = `tick - landed >= CrumbleTicks`, Vanishing hidden by `(tick + Phase) % cycle`. The sim freezes at death (`Step` returns when `Dead`), so `s.Tick` at revive is the death tick; the popup delay does not help.
- Repro (scratch `revive` mode, hand-built 2-platform world, best ground = the kind under test, state set to the clock value at the death tick, then `Revive` and 200 idle steps):
  - Snap Daisy, 60 ticks after landing: falls at tick 32 (it closes at 90).
  - Snap Daisy, 100 and 200 ticks after landing (shut): falls 3 ticks after the revive.
  - Crumble, crumbled: falls 3 ticks after the revive.
  - Vanishing, hidden: falls 49 ticks after the revive.
  - Snap Daisy 220 ticks after landing (open again): fine.
  Realistic trigger: stand on a daisy as the best row, it closes at 1.5 s, the player drops and dies within the 3.5 s shut window, revives, drops again. Each revive costs 5 then 10 gems (or the free ad).
- Fix: in `Sim.Revive`, after choosing `BestGroundId`, make that platform safe for a fresh start: Snap Daisy `CrumbleTick = s.Tick` (a fresh 1.5 s countdown), Crumble `CrumbleTick = -1`, Vanishing: if `!IsSolid` move the revive to the nearest solid platform of the same or lower row or hold it solid for the invulnerability window (needs a tiny new per-state field), Spore Pop see B2. Add one test per kind (section 7).

### B2 (MEDIUM) Revive onto a swelling Spore Pop
- Evidence: `Sim.cs:1180` pops when `tick - landed == SporePopTicks` while the player is grounded on it; `Revive` leaves `CrumbleTick[id]` as it was.
- Repro: Spore Pop 70 ticks after its landing, death, revive: `SporePops` becomes 1 two ticks later, the player is flung 2 u up and about 3 u sideways and ends on a lower platform (harness `sporepop_e70`). 100 ticks (flat phase) is harmless.
- Fix: in `Revive` set `CrumbleTick[id] = -1` (or `s.Tick`) for a Spore Pop.

### B3 (MEDIUM) No autoplay data for the current build; the phone suite stalls
- Evidence: `E:/Unity/qa/autoplay_results_20261101T153529.csv` and `autoplay_results_last.csv` are 59 bytes: `cat: files/autoplay_results.csv: No such file or directory`. `E:/Unity/qa/autoplay_report.md` (2026-11-01 22:01) says "0 of 0 runs cleared, No level result was received". The only data are `E:/Unity/logs/autoplay_r1*.out` of 2026-10-21 and 2026-10-23 (v0.14.0.11 and v0.14.1.12), i.e. before the Region 1 and 2 kinds. "4 of 15" is `autoplay_r1c.out` (skill 0.5, 2026-10-23).
- Three of the four phone suites ended with "no BTAUTO output for 400 s: the game stopped reporting" (after level 1, after level 6, after level 2), each time right after a result. `Autopilot.SuiteActive` is written in `AutopilotSuite.cs:82,203` and never read anywhere, so the normal run-end flow (results popup, first-clear rewards, hero reveal, ads) is not suppressed during the suite. Cause not provable from the files kept (no logcat of a stall).
- Fix: rerun the Region 1 suite on the current build with logcat saved (`adb logcat -d` right after a stall); make `RunController` skip popups, ads and hero reveals while `Autopilot.SuiteActive`; make the report script fail loudly when the CSV pull returns the adb error text.

### B4 (MEDIUM) Root cause of "platform K outside the walls" (lvl_002_01 to _03 and others, all Region 2)
- Evidence: `RandomLayoutTests.cs:98-100` (`Validate`) fails a platform when `X - (Width/2 + Amplitude) < 0` or `> screen`, for every kind with width below 9. `Platform.cs:38` and `SpecialPlatforms.Params` (case `FairyRing`) store the hole HALF WIDTH (0.7 u) in `Amplitude`; the ring is always 6.0 u wide (`ConstrainWidth`, `SimConfig.FairyRingWidth`). The builder's final pass `LevelBuilder.SnapPlatformWidths` (line 576) added `Amplitude` to the wall margin only for Moving, Ladybug and Orbit Cap (before the helper's fix), so a ring legally flush at x = 3.0 (left edge at the wall) but placed between 3.0 and 3.7 gave an "extent" past the wall. Fairy Ring is the only Region 1/2 kind that puts a non-swing value in `Amplitude`, which is why only `lvl_002_*` failed (the earlier run, `E:/Unity/logs/simout_qa/run1.txt`, lists 293 problems, all lvl_002).
- Status of the current tree: the clamp at line 576 now includes `FairyRing` and my scratch check finds 0 violations in 93,459 platforms (Regions 1 and 2, 20 attempt seeds each, test semantics and swing-only semantics). So the failure is gone, but:
  - the fix is the wrong direction: a 6 u ring at x = 3.0 is physically inside the walls; the hole width is not a swing. Now every ring is pushed up to 0.7 u inward and `SeparateSameRowPlatforms` (line 631-634), `DropTouchingExtras` (line 600) and `TryAddRowMate` keep neighbours 0.7 u further away from a ring (and may drop more row mates), which changes the layouts and the golden hashes for nothing;
  - `GongConflicts` (line 1109) and any future region 3 to 5 code that reads `Amplitude` as a swing will repeat the problem.
- Suggested fix: make the validator swing-aware (`Moving`, `Ladybug`, `OrbitCap` only; add a helper `PlatformKinds.SwingsSideways`), keep `FairyRing` out of the three builder clamps, and give the hole its own field (`Platform.Hole` or reuse `Phase`) so `Amplitude` always means swing.

### B5 (LOW) Revive onto a Ladybug uses the unboosted path
- Evidence: `Sim.cs:254` `PlatformX(BestGroundId, s.Tick)` (no `aux`), while the ground check and carry use `PlatformX(id, tick, s.CrumbleTick)` which adds the boost shift in `CrumbleTick[id]`.
- Repro: Ladybug with boost clock 160: player placed at x = 4.50, platform at 4.72 (offset 0.22 u, up to twice the amplitude in general). With a 2 u wide bug and 1.4 amplitude the player can start outside the landing margin and drop.
- Fix: use `PlatformXAt` / the `aux` overload in `Revive`.

### B6 (LOW) Endless dense patch extras ignore a swinging spine platform
- Evidence: `EndlessTower.cs:271-285` tests `Abs(x2 - usedX[u]) < (w2 + usedW[u]) / 2 + 0.6` with no `Amplitude`; the campaign row mate (`LevelBuilder.TryAddRowMate`, line 531) adds `amp`.
- Repro (scratch `endless`): 25 seeds x 400 rows: Region 1 Ladybug/extra pairs touching = 17, Region 2 Orbit Cap/extra = 8. Walls, adjacency rules, widths all clean.
- Fix: add the spine `amp` to `usedW[0]` (or to the distance) like the campaign.

### B7 (LOW) View details
- `RunView.cs:1124` interpolates the player (`Mathf.Lerp(prev, cur, alpha)`), `RunView.cs:1152-1171` places every platform at the current tick without alpha. On a 90 or 120 Hz display the player and an Orbit Cap, Ladybug, Hive, Sprout or Squish platform move out of phase by up to one tick (Orbit about 0.022 u per tick, Hive 0.027). Small, but CUX-1 promises interpolation.
- The Squish cap (`y = Y - SquishSink`) returns 0.35 u in one frame when the player jumps; the Seesaw leaf snaps flat when the player leaves. Cosmetic; a short ease out would remove the pop.
- Checked and fine: Orbit, Ladybug (boosted), Hive, Sprout, Squish positions come from the same Sim calls the physics uses; Thorn Bush art side matches `Dir` (art "left" has the thorns on the left, `Dir -1` = left, viewed); Fairy Ring painted hole is 1.41 u wide and centred = sim hole 1.4 u; Spore Pop poses are swapped from `SporePopPhase`; `SpecialPlatformFx` and `Render` allocate nothing per frame by reading (structs, no strings, no LINQ); every (kind, width) the builder can produce has a painted piece (no unpainted special width, no special width of 9 or more).

### B8 (LOW) Determinism rule: `double` inside `Simulation/`
- `CoinPayoutRules.cs:32-54` (`Scale(long, double)`, `Math.Round` on a double, `MilestoneGems` with `chance * 100.0`) feeds the server-authoritative payout; `CharacterStats.cs` (double, display only); `Sim.cs:567` (`float` getter for the HUD). The gameplay simulation, the builder and the endless tower use no float, `Random`, `DateTime` or `Math.*` (grep clean). IEEE doubles agree across devices in practice, but CLAUDE.md says no float in `Simulation/`: move the payout maths to integers (permille factor) or out of the folder.

### B9 (LOW) Smaller items
- `LevelBot.cs:366`: visited key uses `(Tick / 20) % 24` (480 tick cycle) for time dependent kinds; Orbit (252), Hive (180), Wind (300) and Ladybug periods do not divide it, so different states can share a key and the bot may under-report solvability (never over-report).
- Dead assets and flags: Fairy Ring pieces `w4_4` and `w8` are never used (the ring is always 6 u); `Autopilot.SuiteActive` is never read.
- Doc drift: doc 24 still says levels 1 and 2 use no new platform and "light touch" level 2 (doc 25 ramps 10 to 30 percent from level 1 and wins); CLAUDE.md still says the platforms are drawn as tinted placeholders although all 14 painted kinds are in `Resources/ArtCatalog.asset` (checked by GUID); doc 30 section 12 gives the Fairy Ring hole as 0.6 to 0.8 u, config is a fixed 0.7 u.

## 3. Wall root cause, short version for the other helper
`Amplitude` is overloaded. Fairy Ring = hole half width, so `width / 2 + Amplitude` is a wrong "extent" for it (6 / 2 + 0.7 = 3.7 against a legal clamp of 3.0). Everything else with an `Amplitude` (Moving, Ladybug, Orbit Cap) really swings. Fix the check, not the layout (B4).

## 4. Region 1 autopilot: game or player?
Method: `AutopilotPlanner` (depth 2) and the `AutopilotBrain` loop copied unchanged into the harness (reaction lag 5 + round(4 x skill) ticks, timing jitter 1 + round(2 x skill) ticks), the real `Sim` and `LevelBuilder` of today's tree, attempt seeds 1 and 2 of each level, no power-ups, default speed, goal check = `GoalEvaluator`.

| Run | Cleared |
|---|---|
| Region 1, skill 0 (perfect) | 27 of 30 |
| Region 1, skill 0.2 | 13 of 30 |
| Region 1, skill 0.5 (the phone setting) | 7 of 30 |
| Region 1, skill 1.0 (one seed) | 4 of 15 |
| Region 3 (no new kinds), skill 0.5 | 7 of 30 |
| Region 2, skill 0 | 30 of 30 |

- The three Region 1 skill 0 misses: level 10 (combo chain: the planner only climbs, it does not look for 4-floor jumps, both seeds fell at row 150) and level 13 seed 1 (fell at 104, lead 0.1 u). The bot (`LevelBot`) solves the same layouts: `Bt` mode, 30 runs, all solved; the planner is greedy, the bot searches.
- Skill 0.5 fails equally in Region 3, so the cause is the lag and jitter against a tight kill line, not the Region 1 kinds. Deaths at skill 0.5 and 0.2 are spread over normal landings, Dandelion, Wind Petal, Hive, Thorn Bush, Snap Daisy; no kind dominates (Dandelion appears in 6 of 17 deaths at skill 0.2 against about 12 percent of special rows: worth a look when a human playtest happens, not a proven fault).
- Margin of a perfect run (bot, 2 seeds x 15 levels): climb 1.0 to 1.35 floors per second, kill line lead at the finish 3.1 to 9.4 u (1.3 to 9 s); levels 10 to 15 sit at 1.3 to 3.8 s of lead. A player that is 5 to 10 percent slower than the bot dies. Deaths that look "early" (floor 2 to 10 at 2 to 10 s) are a missed landing after a hop of 3 rows: the kill line follows 10 u below the highest point (`SimConfig.KillDistance`) and scrolls up 2 to 3 u/s, so the ground is lethal about 1 s after a 3 row climb. `StartKillDistance` is irrelevant for that (I set it to 12 and 40: identical results).
- Per kind difficulty (bot homing jump options that land alive on a higher row, standing on the kind, levels 6 to 15; Normal = 37 percent): Thorn Bush 29, Fairy Ring 34, Snap Daisy 36, Seesaw 37, Ladybug 37, Wind Petal 38, Sprout 38, Puff Float 37, Dizzy 38, Hive 39, Spore Pop 40, Crumble 41, Slippery 42, Orbit 42, Dandelion 42, Squish 47, Bounce 51, Bounce Cap 52. No kind is an outlier below 29 percent; the Thorn Bush is the hardest to leave (centre starts included), as designed.
- Verdict: levels are solvable (bot proofs, skill 0 autopilot) and no unfair spot showed up. The levels from level 6 are, however, very tight for an imprecise player. That is a balance decision for the owner, not a bug: options are a lower scroll base for levels 6 to 15, a larger `KillDistance`, or accepting it as the "hard" promise. Re-run the phone autoplay at skill 0 first (it should clear what the harness clears) and then at 0.2.

## 5. Checked and found correct (no bug)
- Placement rules on 12 layouts x 30 levels (Regions 1 and 2): no hazard after hazard or launcher, no launcher after launcher, demanding kinds never adjacent to demanding, hazard or launcher; Hive neighbour gaps in [2.6, 3.0], Orbit gaps in [2.0, 3.7], no gap above 4.2; kinds are evenly mixed (about 900 to 1,300 of each new kind). Dandelion next platform: 6 of about 1,200 sit closer than 2.2 u because of the wall clamp, direction always right.
- Stars and gems: 2,040 pickups, none inside the swept area of any platform (Hive +-1.2, Orbit +-0.5, Sprout +1.6, swing), all within 2.76 u above a platform they can be reached from. There are no gong hazards in Regions 1 and 2, so nothing can sit under one.
- Endless Regions 1 and 2 (25 seeds x 400 rows, about 12,000 platforms each): no platform outside the walls, no adjacency violations, no unpainted width, all 7 new kinds plus generics present.
- Level goals: level 5 and 10 ladders are locked rows (no specials; Hive, Sticky, Sprout, Orbit and launchers excluded beside them); level 7 loses the goal permanently on an air wall touch (`GoalEvaluator.IsFailed`), Wind Petal pushes only on the ground (no wall bounce counted), Thorn Bush knock reaches a wall only on a platform less than 2.2 u from it (never counted while grounded); levels 8 and 15 gems, doubles and hidden bonus gem match doc 25 (existing `LevelStructureTests`).
- Rewards: `ProgressService.Rewards.cs` keys on the region-local `level.Index` (validated 1..15 in `ContentRepository.cs:138`), so Region 2 level 7 counts as the no-wall level and levels 8 and 15 as gem levels; replay cap of 3 per day, stars counters and combos work for any region. Only Region 1 ids appear in `RewardsTests` (section 7).
- Power-ups against the new kinds: nothing conflicts in the code. Shield, Magnet, Slow Time are independent of platform kinds (time based kinds keep running on ticks, Slow Time only slows the scroll); Flight and Rocket set `Grounded = false` every tick so no platform effect applies while they run; Double Jump after a Thorn Bush hop or a Spore Pop fling gives one extra air jump (harmless); Wall Stick is unaffected by the new kinds; Puff Float and Dizzy end at the next landing; Rocket ends 3 rows below the finish at the base Y of the target platform (up to 1.6 u off a Sprout, Hive or Orbit target, harmless).
- Determinism: no `float`, `Random`, `DateTime` or `Math.Sin/Cos` in the sim, builder, endless tower or bot (the Orbit lap uses the integer table); the per-platform clocks live in `CrumbleTick`; `ComputeHash` includes `Dir`.

## 6. Documentation drift found (fix when convenient)
Doc 24 vs doc 25 (levels 1 and 2), CLAUDE.md placeholder sentence, doc 30 section 12 hole size, `Region1`/`Region2` tests counts in CLAUDE.md (97 dotnet tests is stale). The "level 2 light touch" paragraph in doc 24 is superseded by doc 25.

## 7. Test gaps (behaviours with no test)
Existing coverage is large (hand-built world per kind, placement on 20 seeds x 15 levels, bot proofs 24 seeds, endless bot, determinism, golden hashes). Missing:
1. Revive on Snap Daisy (closing and shut), Crumble, Vanishing, Ladybug (boosted) and Spore Pop (swell phase): only Sprout Lift and Fairy Ring are tested (`Region2PlatformTests.cs:188, 297`) plus the generic `T3Tests.ReviveReturnsToHighestPlatformAndCounts`. Suggested test: for every `PlatformKind`, build a world, put the best ground on it at several clock values, die, revive, run 120 idle ticks and assert the player is still grounded on it or on an equal or higher platform.
2. Power-ups x new kinds: the `Power_*_Region` theories only use the first rows of one level per region; nothing runs Rocket or Flight ending over a Fairy Ring hole or a Snap Daisy, Double Jump with Squish or Puff, Wall Stick next to a Wind Petal, Slow Time with Snap Daisy or Spore Pop clocks.
3. Level 7 (no wall touch) with Thorn Bush, Dizzy Spores, Spore Pop, Dandelion: only "the bot solves it". An assert that a standing player on any kind can never be forced into a wall by the kind alone would document the rule.
4. Sim to view agreement for the new kinds: no EditMode or PlayMode test compares the drawn position with `PlatformX/PlatformY` for Orbit, Ladybug, Hive, Sprout, Squish; none checks that the Fairy Ring art hole (`platform_set.json` `holeWidth`, 1.4) equals `2 * SimConfig.FairyHole*`; none checks that every (kind, snapped width) the builder can emit has a painted piece (my harness found none missing; make it a test so the next width change is caught). `NoPlatformCueTests` covers the no-cue rule only.
5. Frame budget and allocation: `CriticalUxTests.HardestLevelStaysInTheFrameBudgetWithoutAllocations` uses `lvl_005_15` (gongs) only. Nothing measures `lvl_001_15` or `lvl_002_15` with Snap, Spore Pop, Sprout, Orbit in view, nor endless growth (`EndlessTower.GenerateTo` allocates new arrays every 40 rows, a hitch if the frame is already heavy).
6. Human-like margin: the only autopilot test is skill 0 on level 1 (`AutopilotTests.PerfectAutopilotClearsRegionOneLevelOne`). There is no regression guard for the kill line margin (for example: bot lead at the finish of every Region 1 and 2 level stays above 2 s) and no clear-rate floor at skill 0.2 on a few levels. Without them a balance change that makes levels 10 to 15 slightly harder is invisible until a phone run.
7. Rewards: only `lvl_001_*` ids in `RewardsTests`; add `lvl_002_07`, `lvl_002_08`, `lvl_002_15` to show the region-local index rules and that Region 2 clears count for the daily and weekly counters.
8. Determinism: the golden file covers base layouts and one endless tower (Region 3, seed 12345, 100 rows). No golden for an endless Region 1 or 2 tower (new kinds), none for attempt-seed layouts, none for a scripted Sim run that stands on each new kind (state hash), so a platform difference between .NET and IL2CPP would not be caught.
9. Pickups against moving platforms: `PickupOverlapTests` and `PickupAndJumpTests` cover static slabs and star overlap; no test sweeps the Hive, Sprout, Orbit and Ladybug motion against pickups (my sweep found none; the margins are constants, so a change to `HiveBobAmplitude` or `SproutRise` would not be caught).
10. Validator: `RandomLayoutTests.Validate` should know which kinds swing (B4); and add a test that rows containing a Fairy Ring keep row mates at the same distances as any other 6 u platform.
11. Autoplay tooling: nothing tests the report pipeline end to end (adb pull failure, empty CSV, stall detection) and `AutopilotSuite` has no test for the clear to next level transition.

## 8. What I could not do
- No device, no Unity: view findings are from reading `RunView` and `SpecialPlatformFx` and from the art files; the Run scene itself was not played.
- I started `dotnet test` filtered on `RandomLayoutTests` (own output `E:/Unity/logs/simout_qa`); it is the heavy class (bot on 75 levels x 20 seeds) and was still running after 45 minutes on the busy machine, and the tool blocked me from stopping it. Its output goes to `E:/Unity/logs/simout_qa/qa_run2.txt`; whoever owns the machine can stop it (`testhost.exe` of `simout_qa`). My own direct check of the same wall rule gave 0 violations.
