# 46. Timed gameplay: Time, Pace, records, medals, endless splits and Time Attack (2026-11-09)

**Source:** owner decisions, 2026-11-09 (a long question and answer). This file records them exactly; anything not decided is in section 12 (Open points). **Status:** specification only, no code yet. Companion docs: 44 (hero physics, the 60 Hz simulation), 45 (levels and difficulty), 39 (Main, Daily and Weekly rewards), 26 (Rewards screen), 11 (critical UX rules).

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
| Revive | A run that used a revive still pays its rewards and stars, but it **never sets a best Time or best Pace and never goes to the leaderboard**. |

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

**Update rule.** A record is replaced only when the new run improves it (higher Pace; a lower Time updates the best Time on its own). Revived runs never update it (section 3).

## 5. UI

| Screen | What it shows |
|---|---|
| Level-start popup | Best Pace and Best Time of the **current Gameplay mode**, for example `Pace 0.83 plat/s   Time 01:12.4`, or `no record yet`. Medals (section 6) next to the stars. |
| Result screen | Time, Floors, Pace, a **NEW BEST** badge when a record was set, and the delta against the previous best (plus or minus seconds, and the pace difference). |
| Run HUD | An always-visible run timer (`mm:ss.t`) with the best pace beside it, on **every level and in endless**. A live **faster / slower delta every 10 floors** against the best run (from its floor-by-time curve). |
| Pace Ghost | See below. |

**Pace Ghost.** A translucent ghost marker (or silhouette) at the floor height your best run had reached at this second of the clock. It does not copy the best run's jumps, because the layouts differ; it only shows where you would be if you kept the pace of your best run. **Disabled by default.** An on/off toggle sits in Settings. The Settings screen must stay non-scrolling and keep 48 dp touch targets and 16 sp text (doc 11, CUX-4 and CUX-5), so the toggle replaces or shares a row rather than adding a scroll.

All new items obey doc 11: no overlap with the player, the next platforms or hazards, inside the safe area on 9:16, 9:19.5, 9:21 and 3:4, numbers never clipped, readable at 130 percent text, and nothing allocates per frame (the timer text updates only when the shown tenth changes).

## 6. Medals

Bronze, silver and gold, **per level and per Gameplay mode**.

| Medal | Target Pace (percent of the fastest Pace the solvability bot reaches on that level and mode) |
|---|---|
| Bronze | 55 percent |
| Silver | 70 percent |
| Gold | 85 percent |

The three percentages are a table the owner can change; a test computes the bot's fastest Pace per level and mode and writes the resulting target Paces into the level data. Medals show **next to the stars** on the level map and in the level-start popup. A medal is earned when the best Pace reaches its target (a revived run cannot earn one).

**Medal icons:** drawn in code first. OpenArt art only after the owner approves an exact batch (guardrail in `.claude/rules/openart-credit-guardrail.md`: purpose, tool, model, full prompts, cost and balance, then wait for a clear yes).

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
| Score | Floors reached in 3 minutes. |
| Record | Its own best per region and Gameplay mode. |

**Timed captures** (best of a single run, per region): best **coins per minute** and best **gems per minute**.

## 8. Reward time goals

Computed from the recorded Pace, Time and medals.

| Tab | New goal | Counter |
|---|---|---|
| Daily | Clear 2 levels at a pace of at least 0.6 plat/s | clears with Pace of 0.6 or more (not revived) |
| Daily | Reach floor 100 in Endless within 3:00 | split to floor 100 of 3:00 or less |
| Weekly | Beat your best pace on 3 different levels | distinct levels with a new best Pace this week |
| Weekly | Earn 5 gold medals | gold medals earned this week |
| Weekly | Reach 100 floors in a Time Attack | best Time Attack floors this week |
| Main (region goals) | New **medal tier line**: earn gold medals on 5 / 10 / 15 / 20 / 25 levels of the region | gold medals in the region |

Reward sizes (owner, 2026-11-09): NO new economy numbers. Main: each medal tier pays the same as the star tier at the same position (doc 39). Daily: each new goal takes a Daily slot and pays like today's Daily actions. Weekly: each new goal takes a Weekly slot and pays like today's Weekly goals.

## 9. Data model

New **additive** save fields only: no version bump (the owner plays a new game; old saves simply have no entries).

| Record type | Lives in |
|---|---|
| Per-level, per-mode record (5 per level: best Pace, best Time, seed, hero id, mode, version, inputs, floor-by-time curve) | inside the level progress (`LevelProgress`), keyed by Gameplay mode |
| Medal per level and mode (derived from the best Pace; the count per region is cached for the Main goal) | derived from the record; region counters in the rewards region stats |
| Endless records (best floors, best Pace, best Time, best splits per stage) | per region and Gameplay mode, next to `EndlessBest` |
| Time Attack records (best floors) | per region and Gameplay mode |
| Timed captures (best coins per minute, best gems per minute) | per region |
| Medal targets (bronze, silver, gold Pace per level and mode) | level data, written by the bot test |

**Helper.** A pure C# `TimeRecords` and formatter helper (ticks to `mm:ss.t`, Pace maths and `0.83 plat/s`, the improve-only update rule, medal grading, split handling and the delta against a curve). It lives in the Economy or Simulation layer with no Unity types, so dotnet tests can cover it. The run reports its result (floors, ticks, revived flag, seed, hero, mode, inputs, curve) through the existing run result (`RunResult`), and `ProgressService` applies it.

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

**dotnet tests (pure C#)**

| Area | Checks |
|---|---|
| Maths and format | ticks to Time `mm:ss.t` including rounding and over 10 minutes; Pace maths and `0.83 plat/s` rounding; zero floors or zero time safe |
| Floors | each row counts once; never decreases |
| Record update | only improves; a better Time alone updates only Time; revived runs excluded; the five modes are separate |
| Medals | thresholds from the three percentages; edge values exactly on a threshold; recomputing after the owner changes a percentage |
| Endless | splits at every 100 floors; best split per stage; Pace and survived Time records |
| Time Attack | 180 s end, score = floors, no revive, own record, counts once in the daily cap |
| Rewards | progress of every goal in section 8, including revived and non-qualifying runs |
| Determinism | replay of seed plus inputs reproduces floors and ticks |

**Unity EditMode and PlayMode (category `CriticalUX`)**

| Screen | Checks |
|---|---|
| Level-start popup, result screen, Run HUD timer and delta, Settings toggle | no overlap, nothing outside the safe area, 48 dp targets and 8 dp gaps, 16 sp text, readable at 130 percent text, at 9:16, 9:19.5, 9:21 and 3:4 |
| Settings | still non-scrolling with the new toggle |
| Run HUD | zero managed allocations per frame with the timer, delta and ghost on |
| Pace Ghost | off by default; toggle works; ghost never covers the player, platforms or hazards |

## 12. Decided open points and what is still open

Decided by the owner on 2026-11-09:

1. Reward sizes: reuse today's sizes (section 8).
2. Medal reference: the default hero Momo, the fastest Pace of the solvability bot averaged over 5 layouts of each level and Gameplay mode; a stronger hero has a small natural edge but medals stay reachable for everyone.
3. Time Attack is open from the start (as soon as endless is). Runs past the daily coin cap pay no coins but still set records. Speedrun-goal levels keep their own goal clock unchanged and the Pace and Time records are recorded on top.
4. The Weekly Time Attack goal is 100 floors (about 0.56 floors per second over 3:00).

Still open (not blocking):

1. Medal icon art (code-drawn first; an OpenArt batch needs the owner's approval).
2. Exact HUD position of the best pace and the delta.
3. Whether Time Attack coins use the same per-run table as normal endless (default: the same table).
4. How the hero's physics affect the later leaderboard (records do not depend on the hero).

## 13. Changelog

- 2026-11-09: document created from the owner's timed gameplay decisions (Time, Pace, records per level and mode, Pace Ghost, medals, endless splits, Time Attack, reward time goals).
- 2026-11-09: open points decided: reward sizes reused, medal reference Momo over 5 layouts, Time Attack open from the start and records past the cap, Weekly Time Attack goal 100 floors.
