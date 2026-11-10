# 46. Timed gameplay: Time, Pace, records, medals, endless splits and Time Attack (2026-11-09)

**Source:** owner decisions, 2026-11-09 (a long question and answer). This file records them exactly; anything not decided is in section 12 (Open points). **Status:** built and updated to the code on 2026-11-09 after the audit: sections 5, 6, 8, 9, 11 and 12 describe what exists. The dotnet and EditMode tests and the PlayMode CriticalUX audits are written; the Unity suites were edited but not run in the Unity editor yet (section 11). Companion docs: 44 (hero physics, the 60 Hz simulation), 45 (levels and difficulty), 39 (Main, Daily and Weekly rewards), 26 (Rewards screen), 11 (critical UX rules).

Units: the simulation runs at 60 ticks per second. A "floor" is one platform row. Pace is written `plat/s` (platform rows per second).

## 1. Purpose

Today a run only produces stars, a score and (in endless) a best floor. The only clock is on speedrun-goal levels. This spec adds a second skill axis, speed, to every level and to endless: how fast can you climb. It gives the player a personal best to beat before every run, medals as target lines, a live comparison during the run, and new reward goals. It does not change hero physics, level layouts, star rules or the endless coin rules.

## 2. What is recorded per run

| Value | Definition |
|---|---|
| Floors cleared | Each new row reached counts once. It never decreases (falling back down does not lose floors). |
| Time | Elapsed run time, stored as simulation ticks (60 per second). |
| Pace | Floors per second = Floors cleared divided by Time in seconds. |

**Display formats**

| Value | Format | Example |
|---|---|---|
| Time | `mm:ss.t` (minutes, seconds, one decimal) | `01:12.4` |
| Pace | two decimals plus unit | `0.83 plat/s` |

**Layouts stay random per attempt.** Two attempts of the same level are not the same tower. So **Pace is the fair number** and Time carries a luck factor (a lucky layout is faster). Pace is the record that counts (section 4).

**Levels whose goal ends before the top row** (combo chain, hidden gem, no wall bounce): the run ends when the goal completes, and Pace = floors reached at completion divided by the time at completion. The level's top row is not needed.

## 3. The clock

| Rule | Decision |
|---|---|
| Starts | At the first jump of the run. |
| Stops | When the goal completes (a level) or when the run ends (endless). |
| Pauses | Excluded. Pause, an app switch or an interruption does not add time. |
| Revive | **Owner 2026-11-10:** a revive is paid with gems earned or bought, so a revived level run counts like any other: best Time and Pace records, NEW BEST ribbons, Bronze/Silver/Gold and the timed reward goals. The run clock is the simulation tick, which does not advance while the player is dead, so the time spent on the revive popup is never counted. Ranked leaderboards stay clean-run only. (Before this date a revived run earned nothing.) |

## 4. Which records are kept

**Per level, per Gameplay mode.** There are five records per level: Relaxed, Easy, Normal, Hard, Insane. The hero does not matter (any hero can set the record; the hero id is only stored for verification).

| Stored in each record | Purpose |
|---|---|
| Best Pace | The record that counts. Used for medals, ghosts, rewards and ranking. |
| Best Time | Shown next to the best Pace. Kept as its own best (it can come from a different run than the best Pace). |
| Layout seed | Verification. |
| Hero id | Verification. |
| Gameplay mode | Verification (also the key of the record). |
| Game version | Verification (the simulation must match the version). |
| Compressed per-tick inputs of the best run | Verification and replay. About 1 to 3 KB. |
| Floor-by-time curve of the best run | Floors reached at every second. Used by the live delta and the Pace Ghost. |

**Verification.** The simulation is deterministic, so a run can be re-played from the seed plus the inputs. The server must later decide leaderboard scores (rule in CLAUDE.md); the client never decides them. The stored best-run data is what the server will receive. No leaderboard exists yet; this spec only makes the data available.

**Update rule.** A record is replaced only when the new run improves it (higher Pace; a lower Time updates the best Time on its own). Revived level runs update it like any other (section 3, owner 2026-11-10).

## 5. UI

| Screen | What it shows (as built) |
|---|---|
| Level-start popup | `RecordBlock` (`TimedUi`): "Best in <Gameplay mode>" with the best Pace (`0.83 plat/s`) and the best Time (`mm:ss.t`) as two pills, or `<mode>: no record yet`. The medal strip (`MedalStrip`) sits under the stars. |
| Endless popup | Best floor pill, then the same record block (best Pace and the longest Time alive), then the **Time Attack line**: `3:00, best N floors` and, on a second line, the best Pace of that run plus `fell at m:ss` when the best run ended before the full 3:00 (nothing extra for a full run). The TIME ATTACK button sits under it; START is below. The popup is content-sized, so the second line adds height instead of overlapping. |
| Level map | Each node shows the medal of the **current Gameplay mode** (not the best of all five) at its top left; it redraws in place when the Gameplay mode is changed on the level screen. |
| Result screen | Time and Pace pills, each with a tick or cross pill for the difference to the previous best (never colour alone), the medal strip with the newly earned medal, and **separate ribbons**: `NEW BEST TIME` and `NEW BEST PACE` (both, stacked, when both were set). A record that is neither (endless floors or a split) shows one generic `NEW BEST` ribbon. In endless, a longer Time alive counts as the Time record. |
| Run HUD | A **third row in the reserved top strip** (`HudTimes`, `RunLayout.Row3H`) under the wallet row and the gauge row: the timer pill (the run clock, or the 3:00 countdown of a Time Attack), the best Pace pill (best floors in a Time Attack) and a delta pill. The delta shows faster or slower against the best run's curve every 10 floors (every 100 floors against the best split in endless), with a tick or cross icon. The timer text changes only when the shown tenth changes. |
| Pace Ghost | Settings switch, **off by default**. In a level it is a translucent **diamond on the tower gauge** (a shape that differs from the round player marker). In endless and Time Attack, which have no gauge, it is a **ghost floor pill** in the free second row showing the floor your best run had reached at this second. It never leaves the reserved strip, so it cannot cover the player, platforms or hazards. |

**Pace Ghost idea.** The ghost does not copy the best run's jumps, because the layouts differ; it only shows where you would be if you kept the pace of your best run.

All timed items obey doc 11: inside the reserved strips and the safe area on 9:16, 9:19.5, 9:21 and 3:4, numbers never clipped, readable at 130 percent text, and nothing allocates per frame (cached strings, nothing built per frame; test `TimedHudWithGhostAndDeltaDoesNotAllocate`).

## 6. Medals

Bronze, silver and gold, **per level and per Gameplay mode**.

| Medal | Target Pace (percent of the reference Pace of that level and mode) |
|---|---|
| Bronze | 55 percent |
| Silver | 70 percent |
| Gold | 85 percent |

The percentages are `TimeRecords.MedalPercents` {55, 70, 85}; the owner can change them and `MedalOf` follows the new targets for old records. **The targets are data:** `Content/config/medal_targets.json` (copy `Assets/Resources/Content/medal_targets.json`, loaded by `FileMedalTargets` and installed as `MedalTargets.Source` by `MedalTargetsBootstrap` before the first scene) holds the reference Pace per level and mode in milli plat/s (830 = 0.83) for the 125 levels of regions 1 to 5. It is written only by `MedalCalibrationTests.Calibrate` (`MEDAL_CAL=1 dotnet test Tests/Simulation --filter MedalCalibrationTests.Calibrate`), never by hand; the readable table is `Tests/Simulation/MEDAL_CALIBRATION.md`. Reference = Momo (default stats), the solvability bot's fastest line to the top row (speedrun best-first search), averaged over 5 layouts of each level and mode; a level and mode the bot solved on no layout has target 0 and no medal. A medal is earned when the best Pace reaches its line; a revived run cannot earn one, and a medal once earned is never lost.

**Medal icons:** drawn in code (`MedalIcons`): bronze = round disc with one pip, silver = diamond with two pips, gold = five-point star with three pips, so the tiers differ by shape and pip count, not only by colour. An unearned medal is the same shape as a pale hollow outline. OpenArt art only after the owner approves an exact batch (guardrail in `.claude/rules/openart-credit-guardrail.md`); the code-drawn set stays until then.

## 7. Endless

Per run: floors reached, time survived and floors per second.

| Record | Scope |
|---|---|
| Best floors (exists today as `EndlessBest`) | per region and Gameplay mode |
| Best Pace | per region and Gameplay mode |
| Best survived Time | per region and Gameplay mode |

**Splits every 100 floors** (the 100-floor stages of the tower, doc 45): the time to reach floor 100, 200, 300 and so on. The best split of each stage is kept per region and mode and is shown live as faster or slower than the best split.

**Time Attack variant**

| Rule | Decision |
|---|---|
| Length | 3:00 countdown. |
| Revive | None. |
| Hero and powers | Same hero and powers as normal endless. |
| Coins | Pays the normal endless coin rules and counts as **one paying run** in the existing daily cap. |
| Difficulty (owner Bug List 8 item 3) | **A ramp that starts hard**: floor 1 of the run has the complexity of campaign **Level 10** (layout difficulty, platform kinds, special share and scroll speed), the level rises by one every 100 floors up to **Level 25**, which is held for the rest of the run (never the endless cycle; doc 45 section 6). Normal endless is unchanged (levels 1 to 25 at 100 floors each, then the 1400-floor cycle). |
| Score | Floors reached in 3 minutes. |
| Record | Its own best per region and Gameplay mode. |

**Time Attack records and the ramp.** The tower is `EndlessTower` with `LevelSpec.TimeAttack` set (`RunSession` passes the flag; floor f plays like endless floor f + 900, clamped to level 25), so it stays prefix stable and a run replays from seed plus inputs. Floors reached in 3:00 are now harder-won than in the old flat start, so the **old Time Attack records are not comparable**: `TimeAttackRecord.RampStartLevel` (JSON `ramp`, additive) is stamped with 10 when a record is set; a record without the stamp (from the unreleased flat-start build, no migration needed) is treated as no record (`TimeAttackRamp.IsCurrent`) and the first counted run replaces it. Old saves still load. The Weekly goal "100 floors in a Time Attack" and its pace (about 0.56 floors per second) are unchanged; the owner may want to re-check that target once the ramp is played, because level 10 is harder than level 1 (open point 0 below).

**Timed captures** (best of a single run, per region): best **coins per minute** and best **gems per minute**.

## 8. Reward time goals

Computed from the recorded Pace, Time and medals (`ProgressService.TimedGoals.cs`, counters in `TimedCounters`, catalogue in `RewardCatalog`).

| Tab | Goal (id) | Counter | Reward as built |
|---|---|---|---|
| Daily | Clear 2 levels at a pace of at least 0.6 plat/s (`d_pace2`) | clears with Pace of 0.6 or more (revived runs count), `PaceClearsToday` | 15 daily points |
| Daily | Reach floor 100 in Endless within 3:00 (`d_floor100`) | split to floor 100 of 3:00 or less, `Split100TodayTicks` | 15 daily points |
| Weekly | Beat your best pace on 3 different levels (`w_pace3`) | distinct levels with a new best Pace this week, `BestPaceLevelsWeek` | 8 gems + 600 coins |
| Weekly | Earn 5 gold medals (`w_gold5`) | gold medals earned this week, `GoldWeek` | 800 coins + 1 power-up |
| Weekly | Reach 100 floors in a Time Attack (`w_ta100`) | best Time Attack floors this week, `TimeAttackFloorsWeek` | 500 coins |
| Main (region goals) | Medal tier line: gold medals on 5 / 10 / 15 / 20 / 25 levels of the region (`g_<region>_gold_<tier>`, token `gold`, type `GoldMedalLevels`) | gold medals in the region (levels and modes) | the star tier at the same position (200 coins, 3 gems, 400 coins, 4 gems, 6 gems + 1 power-up); **tier 5 also gives one random Common hero** like the star tier 5 |

Reward sizes (owner, 2026-11-09): NO new economy numbers. The Daily goals take two Daily slots at 15 points each, so a **full Daily day is now 130 points** (10 + 15 + 20 + 20 + 15 + 20 + 15 + 15); the five Daily milestones stay at 20 / 40 / 60 / 80 / 100 points with their old rewards (the extra 30 points of the day give no extra milestone). The Weekly goals reuse existing weekly packages and add 8 gems a week in total (the gem budget of doc 39 holds). Icons on the Rewards screen: the pace goals and Time Attack use the clock, the endless floor goal the tower, and every gold-medal goal (the weekly one and all region goals) shows the code-drawn gold medal.

## 9. Data model

New **additive** save fields only: no version bump (old saves simply have no entries). Pure rules in `TimeRecordData.cs` (`RecordRules`) and `TimeRecords.cs` (formatters, Pace maths, medal grading, splits, curve delta), no Unity types, covered by dotnet tests.

| Record type (class) | Lives in | Content |
|---|---|---|
| `ModeRecord` (one per level and Gameplay mode) | `LevelProgress.Records[mode]` (JSON `records`) | best-Pace run: Floors, Ticks, seed and attempt seed, hero id, mode, game version, run-length coded inputs, floor-by-time curve (one entry per second); its own best Time (`BestTimeTicks`); best medal earned (`Medal`, only goes up) |
| `EndlessModeRecord` (per region and mode) | `SaveData.EndlessRecords` (`endlessRec`) | best floors, best-Pace run (floors, ticks, seed, hero, version, inputs, curve), longest survived Time, best split per 100-floor stage |
| `TimeAttackRecord` (per region and mode) | `SaveData.TimeAttackRecords` (`taRec`) | best floors in 3:00 and the ticks of that run (fewer than 10800 when the run fell early), seed, hero, mode, version, inputs, curve |
| `RegionTimedRecord` (per region) | `SaveData.RegionTimed` (`timedRec`) | best coins per minute and best gems per minute of one run (x 100) |
| `TimedCounters` (one) | `SaveData.TimedCounters` (`timedCnt`) | day and week keyed counters of the timed reward goals (section 8); they restart when the Daily or Weekly key changes |
| Medal targets | `Content/config/medal_targets.json` | reference Pace per level and mode (section 6); not part of the save |

**Caps.** Stored inputs: at most **12 KB** per level record (`RecordRules.MaxInputBytes`; a normal level run is 1 to 3 KB) and **30 KB** per endless or Time Attack record (`MaxLongInputBytes`). Over the cap the inputs are dropped and the record still counts. The endless curve is capped at **900 s** (`MaxEndlessCurve`).

**Helpers.** `TimeRecords` (ticks to `mm:ss.t`, Pace maths and `0.83 plat/s`, medal grading, splits, delta against a curve), `InputCodec` (run-length coding of the per-tick inputs), `PaceValue` (exact comparison by cross-multiplication). The run reports its result (floors, ticks, revived flag, seed, hero, mode, inputs, curve) through `RunRecord` and `RunResult.Record`; `ProgressService.RecordRun` applies it and returns a `RecordResult` for the result screen and the reward goals.

## 10. Benchmark: how well-known mobile games handle best times

General patterns only, no figures.

| Pattern | How mobile games commonly use it | This spec |
|---|---|---|
| Personal best shown before a run | Racing and runner games show your best lap or time on the start screen | Yes: best Pace and Time in the level-start popup |
| Medals or stars for target times | Racing and puzzle games grant bronze, silver, gold for beating target times | Yes: three medals from percent of the bot's best Pace |
| Ghosts | Racing games replay your best run as a ghost car | Adapted: a Pace Ghost at your best height per second, because layouts are random (off by default) |
| Split deltas | Racers and speedrun tools show faster or slower against the best split | Yes: delta every 10 floors, and every 100 floors in endless |
| Timed modes | Many arcade games have a fixed-length score attack | Yes: 3:00 Time Attack |
| Weekly time challenges | Live-ops games run weekly time challenges and leaderboards | Weekly and Daily time goals now; a leaderboard later, decided by the server |

The spec follows the "personal best plus medals plus live delta" pattern, and replaces the fixed-track ghost with a pace ghost.

## 11. Test plan

**Done (written; the Unity ones were edited but not run in the editor yet)**

| Layer | Tests | Covers |
|---|---|---|
| dotnet (`Tests/Simulation`) | `TimeRecordsTests`, `TimedEndlessTests`, `MedalTargetFileTests`, `MedalCalibrationTests`, `RewardTablesTests`, `ActiveHeroesTests` | formats and rounding (also over 10 minutes, culture independent), Pace maths and exact comparison, zero floors or time safe, record update (improve only, Time kept apart from Pace, revived excluded, five modes separate), medal lines and edge values, input coding and caps, replay of seed plus inputs reproduces floors and ticks, endless records and splits, Time Attack score, per-minute captures, JSON round trips and old saves, the medal target file, reward packages including the gold medal tiers and their hero |
| Unity EditMode | `TimeRecordsEditTests`, `TimedGoalsEditTests`, `TimedUiTests`, `AuditGapTests`, `RegionGoalTests`, `HeroScreensTests` | save and reload, reward counters by day and week, gold medals counted once, run clock starts at the first jump, endless splits and Time Attack session, medals differ by shape, ghost off by default, top strip reserves the timer row, Rewards icons of the timed goals, the Time Attack line of the Endless popup, the hero tab counts |
| Unity PlayMode (`CriticalUX`) | `TimedGameplayScreensFitEveryAspect`, `TimedGameplayScreensSurvive130PercentTextAndLeftHandedLayout`, `LevelNodeMedalFollowsTheGameplayMode`, `TimedHudWithGhostAndDeltaDoesNotAllocate` | level-start and Endless popups (also with a Time Attack record), result screens (new best, slower, revived, both NEW BEST ribbons, Time Attack end, endless end), Run HUD with ghost and delta, no overlap and nothing outside the safe area at 9:16, 9:19.5, 9:21, 3:4 and 130 percent text, zero allocations per frame, the node medal follows the mode |

**Open (not done)**

- Run the Unity EditMode and PlayMode suites in the editor and look at screenshots at 9:16 and 9:21 (the Endless popup second line, the stacked NEW BEST ribbons and the hero tab badges were added after the last run).
- Device check of the third HUD row on the phone.

## 12. Decided open points and what is still open

Decided by the owner on 2026-11-09:

1. Reward sizes: reuse today's sizes (section 8).
2. Medal reference: the default hero Momo, the fastest Pace of the solvability bot averaged over 5 layouts of each level and Gameplay mode; a stronger hero has a small natural edge but medals stay reachable for everyone.
3. Time Attack is open from the start (as soon as endless is). Runs past the daily coin cap pay no coins but still set records. Speedrun-goal levels keep their own goal clock unchanged and the Pace and Time records are recorded on top.
4. The Weekly Time Attack goal is 100 floors (about 0.56 floors per second over 3:00).
5. The gold medal tier 5 gives the same random Common hero as the star tier 5 (section 8).

Resolved in the build:

- Exact HUD position of the best pace and the delta: the third row of the top strip (section 5).
- Time Attack coins use the same per-run table as normal endless and count as one paying run in the daily cap (section 7).

Decided on 2026-11-09 (Bug List 8 item 3): Time Attack difficulty ramps from Level 10 at floor 1 to Level 25 at floor 1501, then holds (section 7).

Still open (not blocking):

0. Re-check the Weekly Time Attack goal (100 floors) and the Daily/Weekly pace goals after a play test on the Level 10 start; the Pace Ghost and the delta need no change.

1. Medal icon art (code-drawn now; an OpenArt batch needs the owner's approval).
2. How the hero's physics affect the later leaderboard (records do not depend on the hero).
3. The leaderboard and the server check of the stored best-run data (none exists yet).

## 13. Changelog

- 2026-11-09: document created from the owner's timed gameplay decisions (Time, Pace, records per level and mode, Pace Ghost, medals, endless splits, Time Attack, reward time goals).
- 2026-11-09: open points decided: reward sizes reused, medal reference Momo over 5 layouts, Time Attack open from the start and records past the cap, Weekly Time Attack goal 100 floors.
- 2026-11-09 (Bug List 8 item 3): Time Attack uses a difficulty ramp from Level 10 (floor 1) to Level 25 (floor 1501, then held); `TimeAttackRecord.RampStartLevel` marks records of the new ramp, legacy flat-start records count as no record. Level lengths changed in doc 45 (section 4a); medal targets re-calibrated for the new rows.
- 2026-11-09: status and sections 5, 6, 8, 9, 11, 12 updated to the built code (third HUD row, ghost diamond and pill, medal target file and calibration report, actual reward packages, Daily full day 130 points, record class names and caps, tests done and open). Endless popup shows the Time Attack best pace; node medals follow the current Gameplay mode; separate NEW BEST TIME and NEW BEST PACE ribbons; gold medal tier 5 gives a random Common hero.
