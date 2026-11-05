# 39. Rewards economy: region goals (Main), Daily, Weekly (Bug List 5 F1, 2026-11-05)

Status: **data and economy layer done and tested; the Rewards UI is a separate task.** Owner spec: region-based Main rewards (30 regions, 1 to 5 playable, 6 to 30 "Coming Soon"), redone Daily (6 tasks) and Weekly (9 tasks), common heroes as rewards, everything reachable without money, gem policy TIGHT. This file replaces doc 26 section 4 (Main, Daily, Weekly tables) and doc 27 sections 11 to 13. Conversion rates and prices are from doc 27: 1 gem = 20 coins, a random power-up unit = 60.7 gems (50/75 gems or 1,000/1,500 coins to buy), bundles are worth 1.4 to 1.8 times their price (average 1.60), a level pays 70 + 60 per star coins (about 250 for 3 stars) and about 4,650 coins per hour of first-clear play.

Where the numbers live (one place each, retune there only):
- Main goals: `RegionGoalTable` in `Assets/_Game/Economy/RegionGoals.cs` (tiers per type, scaling per region). Generated report: `Tests/Simulation/REWARDS_TABLES.md` (written by `RewardTablesTests`).
- Daily and Weekly: `RewardCatalog.BuiltIn()` rows in `Assets/_Game/Economy/RewardCatalog.cs`, mirrored by `Content/economy/rewards.json` (a test keeps them identical).

---

## 1. Rules

1. **Per region.** Each of the 30 regions has its own goal list. Regions 1 to 5 have 44 goals each; regions 6 to 30 have **none** (the Main tab shows "Coming Soon", nothing can be claimed).
2. **Unlock.** A region's goals open when the region is unlocked (`IsRegionGoalsOpen`). Progress is shown for every goal at all times; a goal is **ready** only when its region is open, the counter reached the target and it is not claimed.
3. **Only play inside the region counts.** Jumps, combos, play time, coins and power-ups are counted per region (new save counters, section 6). Stars, cleared levels, 3-star levels, no-wall clears and the best endless floor come from the existing per-level and per-region save data.
4. **Retroactive, never auto-paid.** The existing save already holds stars, clears (`LevelProgress.Clears`, `Stars`) and `EndlessBest`, so those goals show their real progress and are **ready (claimable)** for existing players. The counters that were never stored (jumps, combos, play seconds, coins earned, power-ups used, hard clears per region) start at 0 with this build. Nothing is paid until the player claims.
5. **Claiming.** Per goal (`TryClaimRegionGoal`) or all ready goals of a region (`ClaimAllRegion`); one atomic save per claim. A goal is claimed once for the life of the save (no clock reset).
6. **No separate "region cleared" bonus.** "Clear all 15 levels" is simply the last tier of the Levels goal (`g_<r>_levels_5`) and also gives that region's Common hero (1 copy).
7. **Hero rewards.** `g_<r>_levels_5` = 1 copy of the region's Common hero (Momo, Capi, Otto, Bonbon, Bao for regions 1 to 5, via `progress.AddCopies`). The **top Weekly task** (clear 5 hard levels) = 1 copy of a **random Common hero of an open region**, drawn from the per-account bundle seed and the claim counter (repeatable, never `UnityEngine.Random`). Each copy is a new hero card on the Hero screen.

## 2. Main goals per region (all 5 playable regions, 44 goals each)

Goal id: `g_<region>_<type>_<tier>` (permanent), for example `g_2_jumps_3`. Rewards: gems / coins / random power-up units. Targets that scale by region use the percent rows below the table; coin rewards scale with the coin percent row; gems and power-ups never scale.

| Type (token) | Counts | Tiers: target -> reward (region 1) |
|---|---|---|
| Stars (`stars`) | stars earned in the region (max 45) | 9 -> 200c; 18 -> 3g; 27 -> 400c; 36 -> 4g; 45 -> 6g + 1 PU |
| Levels (`levels`) | different levels cleared (max 15) | 3 -> 150c; 6 -> 2g; 9 -> 300c; 12 -> 4g; **15 (clear all) -> 8g + 500c + Common hero** |
| 3-star levels (`stars3`) | levels with 3 stars | 3 -> 200c; 8 -> 4g; 15 -> 8g + 300c |
| Jumps (`jumps`) | take-offs made in the region | 400 -> 150c; 1,200 -> 2g + 200c; 3,000 -> 4g |
| Combos (`combos`) | chains of 3+ landings | 10 -> 150c; 40 -> 2g + 200c; 100 -> 4g |
| Play time (`time`) | minutes in runs in the region | 15 -> 150c; 45 -> 2g + 200c; 120 -> 4g |
| Coins (`coins`) | coins earned in the region (level and endless payouts) | 1,000 -> 100c; 3,500 -> 1g + 150c; 8,000 -> 3g |
| Power-ups (`powerups`) | units used in the region | 3 -> 150c; 10 -> 2g; 25 -> 300c + 1 PU |
| Hard clears (`hard`) | levels cleared on Hard or Insane | 1 -> 200c; 3 -> 2g; 6 -> 4g |
| No-wall (`nowall`) | clears of level 7 (the no-wall-bounce level) | 1 -> 2g + 100c; 3 -> 3g |
| Endless floors (`endless`) | best endless floor in the region | 100 -> 1g + 100c; 200 -> 1g + 150c; 500 -> 2g + 250c; 1,000 -> 2g + 350c; 1,500 -> 2g + 450c + 1 PU; 2,000 -> 2g + 550c; 3,000 -> 2g + 700c; 4,000 -> 2g + 850c; 5,000 -> 2g + 1,000c + 1 PU; 7,500 -> 3g + 1,300c; 10,000 -> 4g + 1,600c + 2 PU |

Scaling by region (regions 1 / 2 / 3 / 4 / 5):

| | R1 | R2 | R3 | R4 | R5 |
|---|---|---|---|---|---|
| Jumps targets | 400 / 1,200 / 3,000 | 500 / 1,500 / 3,800 | 600 / 1,800 / 4,500 | 720 / 2,200 / 5,400 | 880 / 2,600 / 6,600 |
| Combo targets | 10 / 40 / 100 | 12 / 50 / 130 | 15 / 60 / 150 | 18 / 70 / 180 | 20 / 90 / 220 |
| Play minutes | 15 / 45 / 120 | 18 / 55 / 150 | 20 / 65 / 180 | 25 / 80 / 220 | 35 / 100 / 260 |
| Coins earned | 1,000 / 3,500 / 8,000 | 1,300 / 4,400 / 10,000 | 1,500 / 5,300 / 12,000 | 1,800 / 6,300 / 14,400 | 2,200 / 7,700 / 17,600 |
| Power-ups used | 3 / 10 / 25 | 3 / 10 / 25 | 3 / 12 / 30 | 4 / 15 / 35 | 5 / 17 / 45 |
| Coin reward percent | 100 | 108 | 116 | 124 | 132 |

Endless floor targets (owner list) are the same in every region. Stars, levels, 3-star, hard and no-wall targets are the same in every region.

### Totals per region (from `REWARDS_TABLES.md`)

| Region | Gems core | Gems endless | Gems all | Coins core | Coins endless | PU core / endless | Hero | Value core (GE) | Value if everything incl. 10,000 floors (GE) |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 74 | 23 | 97 | 4,100 | 7,300 | 2 / 4 | 1 | 450 | 1,081 |
| 2 | 74 | 23 | 97 | 4,430 | 7,890 | 2 / 4 | 1 | 467 | 1,127 |
| 3 | 74 | 23 | 97 | 4,730 | 8,480 | 2 / 4 | 1 | 482 | 1,172 |
| 4 | 74 | 23 | 97 | 5,110 | 9,040 | 2 / 4 | 1 | 501 | 1,219 |
| 5 | 74 | 23 | 97 | 5,410 | 9,630 | 2 / 4 | 1 | 516 | 1,263 |
| **1 to 5** | **370** | **115** | **485** | **23,780** | **42,340** | **10 / 20** | **5** | **2,416** | **5,862** |

"Core" = every goal except the endless tiers. GE = gem equivalents (1 gem, 20 coins, 60.7 per PU, 50 per hero copy, an assumption: a third of an Uncommon at 150 gems). The owner policy "about 80 gems per region" is met: **74 gems core** (about 80 with the first endless tiers), 97 only if the player also reaches 10,000 floors in every region (practically nobody). A player who stops at floor 1,000 earns 6 endless gems and 850 endless coins per region.

Comparison with the old Main tab (docs/27 section 11): old 580 gems, 14,500 coins, 11 PU for 225 stars plus five region bonuses. New: 370 core gems (-36 percent), 485 with every endless tier, coins 23,780 core (+64 percent, the coin value carries the economy), PU 10, plus 5 hero copies.

## 3. Daily (resets at local midnight)

Six tasks, 100 points in total. The points track mechanism is unchanged (n/100, chests at 20 / 40 / 60 / 80 / 100).

| Task id | Task | Counter | Target | Points |
|---|---|---|---|---|
| `d_login` | Log in today | login | 1 | 10 |
| `d_play5` | Play for 5 minutes | play seconds | 300 | 15 |
| `d_play15` | Play for 15 minutes | play seconds | 900 | 20 |
| `d_level3` | Complete 3 levels | levels | 3 | 20 |
| `d_combo5` | Make 5 combos | combo chains | 5 | 15 |
| `d_hard` | Clear a hard level | hard clears | 1 | 20 |

| Chest | Points | Reward |
|---|---|---|
| `dp_20` | 20 | 300 coins |
| `dp_40` | 40 | 3 gems |
| `dp_60` | 60 | 500 coins |
| `dp_80` | 80 | 3 gems |
| `dp_100` | 100 | 4 gems + 700 coins |
| **Full day** | | **10 gems, 1,500 coins** |

Changes: the daily random power-up (60.7 gems a day, doc 27 finding 2) is gone; gems per full day drop from 45 to 10. "Hard" is now **cleared on Hard or Insane** (the old counter only counted a level start). Without a hard clear a day tops out at 80 points = 6 gems + 800 coins.

## 4. Weekly (resets Monday local time)

Nine tasks, ordered by difficulty. The last, hardest one gives the hero.

| Task id | Task | Counter | Target | Reward |
|---|---|---|---|---|
| `w_login5` | Log in on 5 days | login days | 5 | 8g + 400c |
| `w_play100` | Play for 100 minutes | play minutes | 100 | 8g + 500c |
| `w_powerups10` | Use 10 power-ups | power-ups used | 10 | 2 PU |
| `w_stars20` | Earn 20 stars | stars earned | 20 | 6g + 800c |
| `w_levels20` | Complete 20 levels | levels | 20 | 8g + 600c |
| `w_nowall2` | Clear the no-wall level 2 times | no-wall clears | 2 | 8g + 500c |
| `w_endless100` | Reach floor 100 in Endless | best endless floor | 100 | 8g + 500c |
| `w_combos150` | Make 150 combos | combo chains | 150 | 10g + 800c + 1 PU |
| `w_hard5` | Clear 5 hard levels | hard clears | 5 | 16g + 1,000c + 1 random Common hero (open region) |
| **Full week** | | | | **72 gems, 5,100 coins, 3 PU, 1 hero** |

Replays of cleared levels keep the existing rule (at most 3 per day count for level counters). The endless counter is the best floor of the week (`endlessFloor`).

## 5. Economic analysis

### 5.1 Free income per week (gems, coins, PU, hero)

Archetypes (Daily points per day, Weekly tasks done):

| Player | Daily days | Weekly tasks | Gems/week D + W | Coins/week | PU/week | Value (GE/week) |
|---|---|---|---|---|---|---|
| Casual | 3 days at 45 points | stars20, levels20 | 9 + 14 = **23** | 2,300 | 0 | 138 |
| Active | 5 days at 80 points, 2 login-only | all but combos150 and hard5 | 30 + 46 = **76** | 7,300 | 2 | 562 |
| Dedicated | 5 days at 80, 2 days at 100 | all but hard5 | 50 + 56 = **106** | 11,100 | 3 | 843 |
| Perfect | 7 days at 100 | all nine | 70 + 72 = **142** | 15,600 | 3 + hero | 1,154 |

Add the one-off Main goals (section 5.2) spread over the first weeks: **Dedicated about 125 to 161 gems per week while the five regions open (about 8 weeks), 106 afterwards; Perfect about 197 early, 142 steady.** That is the owner target of about 150 to 175 gems a week for an active player (Dedicated early weeks), and about 1 bundle (200 to 500 gems, average 364) every 2 weeks for the Active to Dedicated player (76 to 106 steady gems a week = 152 to 212 gems per 2 weeks; 250 to 320 with the Main weeks included).

Old values for comparison (docs/27 section 14, all claimed): 475 gems, 12,300 coins, 8 PU = 1,576 GE a week. New Perfect week: 142 gems (-70 percent), 15,600 coins (+27 percent), 3 PU (-62 percent) = 1,154 GE (-27 percent). Gems are cut hard, coins carry the value as asked.

### 5.2 Main per region, realistic

An Active player reaches about 65 percent of the core gem goals of a region within about two weeks of playing it (3 stars on every level and the long totals are the hard part), plus the endless tiers up to 500 floors (4 gems): about **52 gems per region**, 260 over the five regions; Dedicated about 67 per region (334 total), Casual about 37 (187 total). Coins per region about 3,400 to 4,700 plus 100 to 500 endless.

### 5.3 How long does a new player need for a 300-gem bundle?

A 300-gem bundle is worth about 480 GE (1.6 times the price, doc 27 section 9), so the free gems buy 1.6 times their value when spent there. Cumulative free gems from rewards only, and including the level gem pickups of doc 27 (about 52 per region at 60 percent pickup, one region a week):

| Player | After week 1 | Week 2 | Week 3 | Week 4 | 300 gems reached (rewards only) | 300 gems reached (with level pickups) |
|---|---|---|---|---|---|---|
| Casual | 39 | 87 | 135 | 183 | day 46 | day 25 |
| Active | 89 | 210 | 331 | 447 | day 19 | **day 13** |
| Dedicated | 125 | 286 | 447 | 603 | day 15 | day 11 |

So an Active new player buys the first 300-gem bundle in **about two weeks**, a casual player in about a month, nobody earns it in the first few days. Per week this is not enough to buy chests (100 to 300 gems each) as well; those stay a money or patience sink.

### 5.4 Share of gem-pack revenue value given away

Reference: gem value at the owner rate $0.01099 (1,000-gem pack, doc 27), the assumed average payer spends $18.40 a month (doc 27 A-5). Steady state, 4.345 weeks a month, **gems only**:

| Player | Free gems per month | In dollars at reference | Share of a payer's $18.40 monthly spend |
|---|---|---|---|
| Casual | 100 | $1.10 | 6 percent |
| Active | 330 | $3.63 | 20 percent |
| Dedicated | 461 | $5.06 | 28 percent |
| Perfect | 617 | $6.78 | 37 percent |

The first month adds the Main goals (about 190 to 310 gems over eight weeks). The old tables gave up to 2,065 gems a month (Perfect), $22.7 or 123 percent of a payer's spend. **The new gem faucet is about 70 percent smaller.** Counting everything at gem rates (coins at 20 per gem, PU 60.7, hero 50) an Active player receives about 2,440 GE a month ($26.8). That number overstates the loss: coins cannot be turned into gems and are only worth something as power-ups (1,000 coins each) and revives, and a player who would never have bought a 50-gem power-up costs nothing. The real exposure is the power-up gem sales, which the removal of the daily power-up (-7 PU a week) and the 3 PU a week of the Weekly table keep small. Spent in a bundle the free gems are worth 1.6 times more, but a bundle spent also needs the player to keep playing, which is the point of the rewards.

### 5.5 Conclusions and levers

- Gem budget holds: 74 core gems per region (policy about 80), 106 gems a week for the Dedicated player steady state.
- Coins are the main reward currency: 23,780 core coins over the five regions, 7,300 to 15,600 coins a week. If coin inflation shows up in soft launch, lower `CoinPercent` (region coin scaling) or the daily milestone coins first; they have no effect on gems.
- Endless tiers pay "slightly more" than the tight policy (23 gems = 31 percent on top of the core 74) but only in reach of top players; 7,500 and 10,000 floors need the 7,000-floor play cap removed (other task).
- Free gems fund about one bundle per two weeks for an active player; if the owner wants more, raise the Daily 100-point chest (+1 gem = +7 a week) or the weekly gem rows; to cut, lower `w_hard5` first.
- The hard-clear tasks (`d_hard`, `w_hard5`, `g_*_hard_*`) reward skill on Hard or Insane only; the 100-point day and the weekly hero need it, which is intended.

## 6. Data and hooks (for the UI agent and for maintenance)

Save (all additive, no version bump): `RewardsData.Regions` (`Dictionary<string, RegionStats>` keyed by region index; `Jumps`, `Combos`, `PlaySeconds`, `Coins`, `PowerUps`, `HardClears`) and `RewardsData.GoalsClaimed` (ids). Old `MainClaimed` ids (`s_25`, `r_1`) stay in the file and are ignored. Reset progress clears everything.

Where the counters are fed (no per-frame allocation: region keys are cached strings, counters are plain ints, the jump counter is one int increment in `RunSession.Step`):

| Counter | Hook |
|---|---|
| Jumps | `RunSession.Step` counts a take-off (grounded to airborne with upward speed); `BuildOutcome` reports the new ones in `RunOutcome.Jumps` with `RunOutcome.Region`; `RecordRunStats` / `ApplyRun` add them |
| Combos, power-ups | the existing `RunOutcome.ComboChains` / `PowerUpsUsed` deltas, now also added to the region (`ApplyRegionDelta`) |
| Play time | `ProgressService.FeedPlayTime(dt, save, regionIndex)` from `RunController` (runs only; menu time feeds Daily/Weekly but no region) |
| Coins | `ApplyRun` passes `reward.TotalCoins`; `ApplyEndlessRun` passes the endless coins (`TrackEndlessRun`) |
| Hard clears | `RecordLevelStarted(difficulty)` remembers the setting, a completed level counts a hard clear (also daily/weekly `hardClear`) when it was 3 or more |
| Endless floor | `EndlessBest` (existing) for the goals; `TrackEndlessRun` feeds the daily/weekly `endlessFloor` max |

### API (`ProgressService`, Economy assembly)

```
RewardCatalog RewardDefs
IReadOnlyList<RegionGoal> RegionGoals(int region)           // 1..30; empty for 6..30 and invalid
bool IsRegionComingSoon(int region)                         // no goals yet
bool IsRegionGoalsOpen(int region)                          // has goals AND region unlocked (or Admin Main ON)
int  RegionGoalProgress(RegionGoal goal)                    // also (int region, RegionGoal goal)
bool IsRegionGoalReady(RegionGoal goal)                     // complete, region open, unclaimed
bool IsRegionGoalClaimed(RegionGoal goal)
int  RegionGoalsReadyCount(int region), RegionGoalsClaimedCount(int region)
bool HasClaimableInRegion(int region)                       // red dot on the region circle
bool HasClaimableMain()                                     // red dot on the Main tab (= HasClaimable(RewardTab.Main))
RewardClaimOutcome TryClaimRegionGoal(string goalId)        // Gems, Coins, PowerUpIds, HeroIds (one entry per hero copy), Result
RewardClaimOutcome ClaimAllRegion(int region)               // one save; NotReady when nothing ready
RegionStats RegionStatsOf(int region)                       // copy: Jumps, Combos, PlaySeconds, Coins, PowerUps, HardClears
```

`RegionGoal : RewardEntry` has `Id, Title, Stat (type token), Target, Gems, Coins, PowerUps, HeroRegion, Region, Type (RegionGoalType), Tier (1-based), IsEndless`. `HeroRegion != 0` means the claim also gives a hero copy (`GivesHero`). The generic API still works: `ClaimableEntries(RewardTab.Main)` yields every goal, `TryClaimReward(RewardTab.Main, id)`, `ClaimAll(RewardTab.Main)` (all regions), `RewardProgress/RewardTarget/IsRewardReady/IsRewardClaimed(RewardTab.Main, goal)`. Daily/Weekly entries are unchanged `RewardEntry` rows (`RewardEntry.HeroRegion == -1` marks the random-hero weekly task; the outcome lists the hero in `HeroIds`).

Removed: `RewardCatalog.StarMilestones` and `RegionRewards` still exist but are empty and `[Obsolete]` (so the old Rewards screen compiles; its Main tab is empty until the UI is redone).

Admin "Main rewards ON" (`AdminSetMainRewardsReady`, docs/38): marks every goal of regions 1 to 5 ready and unclaimed without paying (progress reads as at least the target); OFF restores the real progress; System Test OFF drops it. Claiming while it is on pays like a normal claim.

## 7. Tests

- `Tests/Simulation/RewardTablesTests.cs` (xunit, pure data, run with `dotnet test --filter RewardTablesTests`): 44 goals for regions 1 to 5, none for 6 to 30, unique permanent ids, targets strictly increasing and never easier in later regions, caps (45 stars, 15 levels), the hero only on `levels_5`, gem budget per region (core 65 to 85, endless at most 30, total at most 105), value caps; writes `REWARDS_TABLES.md`.
- `Assets/_Game/Tests/EditMode/RegionGoalTests.cs` (Unity EditMode): goal lists and Coming Soon, closed-region gating, retroactive progress from a seeded save (ready, nothing paid, counters at 0), per-region counters (jumps, combos, play seconds, coins, power-ups, hard clears), endless coins and floors, claim once and no clock reset, `ClaimAllRegion`, exactly one hero copy per region clear, save round trip, failed claims change nothing, Admin switch ON/OFF/System Test off, reset.
- `RewardsTests.cs` (data tests rewritten for the six Daily and nine Weekly tasks, hard clears, endless weekly task, random weekly hero repeatable and open regions only) and `AdminSettingsTests.cs` (Main switch over goals). The Main-tab UI tests of `RewardsTests` are `[Ignore]`d until the Rewards UI task rebuilds the screen.
- Verified here without Unity: the Economy, Content and Simulation sources compile (net10, LangVersion 9) and all `RegionGoalTests` and the data tests of `RewardsTests` (48 tests) pass in a scratch NUnit-compatible runner; `RewardTablesTests` ran under dotnet (8 passed). The Unity EditMode run itself is still to be done in the Editor.

## 8. Open points

1. "Hard level" means the difficulty setting Hard or Insane when the level was started; an ordinary hard-class level on Normal does not count.
2. Jumps are take-offs from a platform (a wall jump or air jump is not counted); play time counts only run time with a region (menus add to Daily and Weekly but to no region).
3. The weekly hero sits on the hardest task (5 hard clears). If the owner wants it more reachable, move `HeroRegion = -1` to `w_combos150` in `RewardCatalog.WeeklyRows`.
4. `RegionStats.Coins` counts level and endless payouts (not shop or reward coins).
5. Only the Admin "complete all levels" tool runs `ApplyRun` for levels the player did not play (they count as 3-star clears for the stars, clears and Daily/Weekly counters, as before). A gem region skip only unlocks the region and clears no level.
