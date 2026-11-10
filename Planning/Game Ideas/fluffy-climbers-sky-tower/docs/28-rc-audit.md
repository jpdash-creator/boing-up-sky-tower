# 28. Audit of the change proposals RC 1 to RC 14 (read-only, build v0.38.0.40, 2026-11-01)

Scope: `E:\Source\Game Ideas\boing-tower-docs\RC-1-to-14-change-details.txt` (the proposal) checked against the real code in `E:\Code\boing-up-sky-tower`, the content files, and the owner's decisions in docs 22, 23, 24, 26 (plus 09, 13, 27 where they restate decisions). Nothing in the repository was changed. All numbers below were recomputed from `Content/economy/*.json`, `Assets/Resources/Content/bundles.json`, `Assets/_Game/Economy/*.cs`, `CoinPayoutRules.cs`, `Tests/Simulation/ECONOMY_TABLES.md` (generated from the real level data) and the unit tests.

Reading guide. **OK** = claim matches code or recomputes. **WRONG** = claim or number is incorrect. **MISSED** = something the RC left out. **CONFLICT (i)** = conflicts with code already written; **CONFLICT (ii)** = conflicts with an owner decision. Reference rate everywhere: 1 gem = 20 coins, $0.01099 per gem, one random power-up = 60.714 gems (4 x 50 and 3 x 75, /7).

## 0. Findings that cut across several RCs

1. **Several "owner decisions" the RCs would change were typed in by the owner as explicit tables**: Daily milestones (doc 26 section 4), Main star and region rewards (doc 26 section 4), subscription benefits and prices (doc 26 section 2), chest contents (doc 22 point 3, owner answer 1), power-up prices (doc 22 4.3), power-up durations (PowerUpTests.cs line 80: "owner durations of 2026-10-31"), the replay rule (doc 22 owner answer 3 and point 6). RC 2, 4, 5, 6, 7, 9 each reverse one of them. They are proposals, but each needs an explicit owner "yes", not a default.
2. **The "daily player" income in RC 2 and RC 3 is a ceiling, not a steady state.** `d_allgems` ("collect all gems in a level", 10 points) needs `GemPickupsTotal > 0`. `RunController.cs:51` strips every gem pickup from a level that was ever cleared (`IsLevelCleared` = Clears > 0), so after the campaign is cleared (or on any cleared level) this action cannot be done again. The 100-point milestone (20 gems + the random power-up) is then unreachable, and the day tops out at 90 points = 25 gems + 1,500 coins (100 GE, 3,000 GE a month, no power-up). The 45 gems, 1,350 gems and 5,421 GE per month figures hold only while the player still has uncleared levels with gems. Same for the weekly "collect 30 gems in levels" task. (Doc 26 helper question B4 already hints at it; it was never answered.) This lowers the real cost of RC 2 and RC 3, and the 30 power-ups a month in RC 3 would in practice fall to about 0 after the first pass of the campaign.
3. **Doc 22 point 8 (daily power-up after a rewarded ad) was superseded by doc 26 section 4** (Rewards screen replaces the Daily button; the power-up is the 100-point milestone, no ad). RC 3-C and RC 10 cite doc 22 point 8 as if it were current.
4. **The generator `Tools/content_cli/gen_content.py` must never be run**: its own header in `REGIONS` says not to regenerate levels without checking seeds, it overwrites `config/remote_config.json` (live value `replayCoinPercent` 50, generator writes 40), regions, levels and items. All RC 1 / RC 13 edits to it are text-only.
5. **Content loading is strict**: `ContentRepository.cs:60` uses `MissingMemberHandling.Error`, and `EconomyConfigDef` fields are `Required.Always`. A key removed from the JSON but kept in `Models.cs` fails to load, and a key kept in the manifest but removed from `Models.cs` also fails. Any key removal is an all-or-nothing change across `remote_config.json`, `Models.cs` and the regenerated manifest.

---

## RC 1: Fix the stale constants

### (a) Claims
| Claim | Result | Evidence |
|---|---|---|
| `remote_config.json` line 18 `coinsPerGem` 100 | OK | line 18: `"coinsPerGem": 100,` |
| line 21 `vipGemsPerDay` 25 | OK | line 21: `"vipGemsPerDay": 25,` |
| `Models.cs` lines 189, 192 both `Required.Always` | OK | 189 `CoinsPerGem`, 192 `VipGemsPerDay` (also `replayCoinPercent` line 190, `regionCoinScalePercent` 191) |
| `gen_content.py` lines 214-215 (100, 40, 25) | OK | 214 `"coinsPerGem": 100, "replayCoinPercent": 40, ...`, 215 `"vipGemsPerDay": 25` |
| `content_manifest.json` lines 16925-16928 (100 and 25) | OK | 16925 `coinsPerGem` 100, 16926 `replayCoinPercent` 50, 16928 `vipGemsPerDay` 25 |
| `ContentRepositoryTests.cs` line 43 `AreEqual(100, CoinsPerGem)` | OK | line 43 |
| `EconomyTests.cs` lines 85-228 use coinsPerGem for the table | OK | lines 85-89 read it, 137, 223, 228 print/multiply |
| Only tests and reports read `coinsPerGem`, no runtime conversion | OK | grep: `Models.cs`, `ContentRepositoryTests.cs`, `Tests/Simulation/EconomyTests.cs` only. `VipGemsPerDay`, `ReplayCoinPercent`, `RegionCoinScalePercent` have no reader at all (only `EndlessCoinDailyFullRateRuns` and `EndlessCoinOverCapMultiplier` are read, `ProgressService.cs:449-453`) |
| Region 1 becomes 187.5 gems instead of 37.5 | OK | 3,750 / 20 = 187.5; / 100 = 37.5 |
| "Fix the '2 then 5' wording" | WRONG (it is numeric, not wording) | `Tests/Simulation/EconomyTests.cs` lines 22-23: `const int Revive1 = 2, Revive2 = 5;` drive the printed table (line 210, 217: "7 for both", sustainable revives per level). Real values are `RunSession.FirstReviveGems = 5, SecondReviveGems = 10` (`RunSession.cs:16`). Fixing means changing constants, so the generated table values change (15 gems for both, not 7) |
| "Also check Content/schemas and test_content.py for hard-coded values (not verified)" | Verified: nothing | No schema for `remote_config`; `Tests/content/test_content.py` has no coinsPerGem, vip or replay reference; `Tools/content_cli/content.py` does not validate the economy block |

### MISSED
- **`replayCoinPercent` 40 is in every level.** All 450 level files carry `rewards.replayCoinPercent: 40` (`gen_content.py:164`; `Content/levels/*.json`; `Models.cs:80` `Required.Always`; `level.schema.json:49-53`). Nothing reads it. `Tests/Simulation/ECONOMY_REPORT.md` item 4 already notes it. Three layers disagree: levels 40, `remote_config` 50, generator 40, code constant 50.
- `regionCoinScalePercent` 3 is also dead (the region factor comes from region data).
- `ECONOMY_REPORT.md` has more stale items than item 6: item 2 (gems on replays, `Rewards.cs` line 112) is wrong now; item 1 and 4 mention `EconomyRules.ThreeStarReplayPercent` (now `ReplayPercent`).
- `Assets/_Game/Economy/NOTES.md` line 23 says replay percent 40; `CLAUDE.md` line 56 and docs 07 (L78, L91), 08 (L268), 09 (G section), 14 (L112), 15 (L233), 27 mention the stale values and must be reworded after the fix.

### (b) Conflicts
- (i) Code: removing `VipGemsPerDay` is safe only in one commit across JSON, `Models.cs` and manifest (see finding 5). `ContentManifestMerger` rebuilds the manifest from ALL sources and changes `contentHash`; it is plain C# (`Assets/_Game/Content/ContentManifestMerger.cs`), run by the Unity menu "Tools/Fluffy Climbers: Tower Jump/Build Content Manifest" or `ContentManifestBuilder.BuildFromCommandLine`. Needs a Unity run (or a dotnet harness).
- (ii) None. The owner asked in doc 27 section 18 question 1 and doc 09 G whether this is an error.
Conflicts: 0 with decisions, 1 with code mechanics.

### (c) Files and lines for the recommended option
`Content/config/remote_config.json` 18 -> 20, delete 21; `Assets/_Game/Content/Models.cs` delete line 192 (keep or delete 189-191, see question 2); `Tools/content_cli/gen_content.py` 214-215 (and 164 if per-level field changes); `Assets/Resources/Content/content_manifest.json` via merger; `ContentRepositoryTests.cs:43`; `Tests/Simulation/EconomyTests.cs` 22-23, 85-228; `ECONOMY_REPORT.md` items 1, 2, 4, 6; `ECONOMY_TABLES.md` is regenerated by the test; `Economy/NOTES.md:23`; docs listed above; the 450 level files and manifest if the per-level field is set to 50 or removed (`Models.cs:80`, `level.schema.json:49-53`).

### (d) Questions for the owner
1. Set `coinsPerGem` to 20 and delete `vipGemsPerDay`? Recommend yes (both, one commit, no behaviour change).
2. Dead economy keys (`coinsPerGem`, `replayCoinPercent`, `regionCoinScalePercent`): keep them with correct values (recommended, lowest risk, future Remote Config), or delete the unused ones too?
3. Per-level `rewards.replayCoinPercent` (450 files): set to 50, or delete the field from schema, model and levels? Recommend set to 50 only in the generator and leave the 450 files for a later content pass (they are unread), or delete in the same manifest rebuild. Need a choice.
4. Also correct the test constants `Revive1/Revive2` to 5/10 so the generated economy report is right? Recommend yes.

---

## RC 2: Review the Daily reward size

### (a) Claims
All numbers recompute: Now 45 gems + 1,500 coins + 1 PU = 45 + 75 + 60.71 = **180.7 GE, $1.99 ref, 5,421/30 days** OK. A: 45 + 100 = 145.0, $1.59, 4,350 OK. B: gems 5+8+10 = 23, 23 + 75 + 60.7 = 158.7, $1.74, 4,761 OK. C: 23 + 100 = 123.0, $1.35, 3,690 OK. 690 gems a month OK. "37 minutes of campaign income (293 GE/h)": 180.7 / 293.5 x 60 = 36.9 min OK (293.5 = 61 gems + 4,650 coins/20; 4.28 h is the bot time x2 assumption, ECONOMY_TABLES bot seconds = 7,708 s x 2 = 4.28 h). Files: `rewards.json` `dailyMilestones`, `RewardCatalog.cs` built-in table (rows at line 125-129) and "RewardsTests requires identical" OK (`RewardsTests.cs:63-85`). "Doc 26 section 4" OK.
- **Caveat on "1,350 gems a month" and "37 minutes"**: it is a ceiling (finding 2).
- **MISSED test impact**: `RewardsTests.cs` hard-codes the milestone payouts: line 274 `10 + 15 + 20` gems, 275 `500 + 1000`, 276 `1 power-up` at 100 points, 363-368 `gems + 10`. These break, not only the identity test.

### (b) Conflicts
- (ii) **The Daily milestones (20 = 10 gems, 40 = 500 coins, 60 = 15 gems, 80 = 1,000 coins, 100 = 20 gems + a random power-up) are the owner's own list in doc 26 section 4.** RC 2 changes all three gem values and removes the power-up. Needs an explicit decision.
- (ii) Doc 26 helper question B4 "Random power-ups pick any of the 7 types equally: ok?" is unanswered.
- (i) None in code beyond tests above.
Conflicts: 1 (decision) plus test updates.

### (c) Files
`Content/economy/rewards.json` (dailyMilestones, ids dp_20/60/100), `Assets/_Game/Economy/RewardCatalog.cs:125-129` (`DailyMilestoneRows`), `Assets/_Game/Tests/EditMode/RewardsTests.cs` 272-276, 363-368, docs 26 section 4, 27 section 12, `CLAUDE.md` line 56 no change needed. Rewards UI is data-driven (no hard-coded amounts found).

### (d) Questions
1. Do you want any cut at all, given the ceiling finding (real steady state is 90 points = 100 GE/day)? Options: leave as is (recommended until soft launch data), A, B, C. If you cut, recommend **A only** (swap the 100-point power-up for 500 coins): it keeps your gem numbers, removes the item that is unreachable after the campaign anyway, and costs 36 GE a day.
2. Should "collect all gems in a level" be changed (count replays, or replace it with an always-possible action) so the 100-point milestone stays reachable? Recommend replace the action, e.g. "collect 5 gems" via a different always-possible task, or accept the ceiling.

---

## RC 3: Review the random power-ups

### (a) Claims
| Claim | Result | Evidence |
|---|---|---|
| Unit value 60.7 gems | OK | (4 x 50 + 3 x 75) / 7 = 60.714 |
| ~34 units a month to a daily player (30 daily + ~4 weekly) | OK as a ceiling | 30 + 30/7 = 34.3. Weekly has only `w_combos50` = 1 PU (`RewardCatalog.cs:131-142`) |
| "plus 6 from the monthly subscription: about 2,082 GE ($22.9)" | WRONG | 34.3 x 60.714 = 2,082 GE ($22.88) **excludes** the subscription; with the 6 sub units it is 40.3 units = 2,446 GE ($26.9) |
| Option A: 50-gem types only = 50.0, ~1,715 GE (-18%) | OK | 34.3 x 50 = 1,714; 1 - 50/60.71 = 17.6% |
| Option B: weekly 50-combo power-up -> 1,000 coins, -10.7 GE/week | OK | 60.71 - 50 = 10.71 |
| Option C: expected ~1,041 GE, ~15 ad views, +$0.15 | WRONG (GE), OK (views, $) | 1,041 = 34.3 x 60.7 x 0.5 halves the weekly units too. Only the daily unit goes behind the ad, so expected = 15 x 60.71 + 4.29 x 60.71 = **1,171 GE**. Views 15 x $0.01 = $0.15 OK |
| Option C ad views vs RC 10 | INCONSISTENT | RC 3 assumes taken on ~50% of days (15 views/month); RC 10 table assumes 0.3 views per player-day (9/month) |
| "Option A is a one-line pool change" in `ProgressService.Rewards.cs` | PARTLY | The rewards pick is `ProgressService.Rewards.cs:427` (`PowerUpDefs.Implemented`). The monthly subscription uses a separate `PickFreePowerUp()` (`ProgressService.Subscription.cs:232`), chests another (`Chests.cs`), bundles are explicit. A pool change is 2 places to make rewards and subscription consistent, plus tests |
| "Doc 22 flagged this as open item 2" | OK | doc 22 open item 2 (1,820 gems a month, ~$20) |
| Note: decide RC 2 and RC 3 together | OK | RC 2 A/C remove the 100-point power-up, which is most of the daily power-up |

### (b) Conflicts
- (ii) **Equal chance across the 7 types is an owner rule** (doc 22 header: "Power-ups roll with equal chance across the 7 types"; doc 22 point 8 and doc 26 B4 repeat it). Option A changes it for rewards.
- (ii) Option C moves the daily power-up behind an ad "per doc 22 point 8", but **doc 26 section 4 replaced that design**: the Daily tab has no ad and the power-up is the 100-point milestone. Doc 22 point 8 also said ad-free VIP gets it without the ad; the subscription would need the same rule (not in RC 10).
- (i) Finding 2: the 30 daily power-ups a month are not reachable after the campaign, so the real saving of A/B/C is smaller than stated.
Conflicts: 3.

### (c) Files
A: `ProgressService.Rewards.cs:427`, `ProgressService.Subscription.cs:232-`, `RewardsTests`/`SubscriptionTests` (pool assertions), docs 22/26 text "any of the 7". B: `rewards.json` weekly `w_combos50`, `RewardCatalog.cs:131-142`, `RewardsTests.cs:63-85` identity. C: ad flow, see RC 10 (button on the Rewards screen Daily tab, not a "Daily tile").

### (d) Questions
1. Keep the equal 7-type pool (your rule), or limit rewards to the four 50-gem types? Recommend **keep equal pool**; if you want cheaper rewards, use B (one weekly item to 1,000 coins) instead.
2. Do you still want the ad-gated daily power-up (doc 22 point 8), now that the Rewards screen (doc 26) exists? If yes, which claim is gated: the 100-point power-up, or an extra one? Recommend do not gate the owner-built 100-point reward; add the ad power-up only as an extra if wanted.
3. Subscribers skip the ad?

---

## RC 4: Check main rewards

### (a) Claims
Star gems 20+30+40+60+80+100 = 330 OK; region gems 5 x 50 = 250 OK; total 580 OK; coins 4,500 + 10,000 = 14,500 OK; 11 power-ups OK (1+2+3+5). GE 580 + 725 + 11 x 60.71 = **1,972.9** OK; $21.68 OK. 580 / 260 = 2.23x OK (the 260 includes the 32 milestone gems of the levels). Halve: stars 10/15/20/30/40/50 = 165, regions 30 each = 150, total 315, GE 1,707.9, 1.2x OK. Note the label "halve" is only true for the star gems; the region gems go 50 -> 30 (60%).
Files: `rewards.json` (stars, regions), `RewardCatalog.cs` `StarRows` (line 101-105) and line 150 (`50, 2000`) OK.

### (b) Conflicts
- (ii) Star milestone and region rewards are the owner's list (doc 26 section 4, including "each 50 gems + 2,000 coins").
- (i) `RewardsTests.cs:92-103` and 108-111 hard-code the star and region values; the identity test as well.
- MISSED: pickups 260 depends on assumption A-2 (60%) and the current gem density (ECONOMY_TABLES: 381 placed + 32 milestone gems = 413 at 100%). The comparison "2.2x" moves with it.
Conflicts: 1.

### (c) Files
`rewards.json`; `RewardCatalog.cs:101-105, 150`; `RewardsTests.cs:92-111`; doc 26 section 4; doc 27 section 11.

### (d) Questions
1. Should Main (a one-time reward) lead the gem faucet? Recommend **keep as is**: it is one-time, bounded (580 gems = $6.4 at ref, not $21.7: gems only; the rest are coins and power-ups) and the owner's list. Revisit with soft-launch day-7 balances.

---

## RC 5: Reprice power-ups by effect time

### (a) Claims
Gems per second from `powerups.json`: Flight 75/10 = 7.50, Rocket 75/4 = 18.75, Double Jump 50/30 = 1.667, Wall Stick 1.667, Shield 50/60 = 0.833, Magnet 1.667, Slow Time 75/30 = 2.50 OK. Option A: all at 50: Rocket 12.5, Flight 5.0, Slow Time 1.667 OK. Option B: Flight 15 s = 5.0, Rocket 30 floors in 6 s = 12.5, Slow Time 45 s = 1.667 OK. Knock-on: Iron 50 + 25 = 75 (75%), Gold 150 + 60 = 210 (70%), sub with 6 PU at 50 = 1,700 GE = 3.75x OK.
- **MISSED, bundles (recomputed from the real file)**: with all units at 50 gems and the bundles unchanged, **206 of 1,000 bundles fall below the 1.4x floor** (minimum 1.15, average 1.51), so `BundleTests.cs:56-68` and `Tests/content/test_bundles.py:53-57` fail until `gen_bundles.py` is rerun (RC says this correctly).
- **MISSED, tests for A**: `PowerUpTests.cs:73-79` asserts the 1,500 coin prices; `ChestTests.cs:11` reference values 86/242 (+-1%) fail (75/210); Gold at exactly 70.0% is on the doc 22 band edge.
- **Files for B are incomplete**: RC lists "SimConfig durations and powerups.json descriptions". Real set: `SimConfig.cs:304` (`FlightTicks = 600`), `:308-309` (`RocketTicks = 240`, `RocketFloors = 20`), `:330` (`SlowTimeTicks = 1800`); `PowerUpCatalog.cs:83-89` (BuiltIn rows hold `DurationSec` and the descriptions; `PowerUpTests.cs:49,71` require it to equal the JSON and SimConfig); `powerups.json` (durations, descriptions, `simParams.floors`); `PowerUpTests.cs:80-90` hard-codes the owner durations and `RocketFloors == 20`; `Tests/Simulation/PowerUpStackTests.cs:57` (RocketFloors 20), `:127` ("8 s in total"), `:295` (`FlightRise * 600`), `:275, :320`; `PowerUpTests.cs` (Simulation) loops over `FlightTicks`. UI text is data-driven (`PowerUpUi.cs:67`).
- **Design check on B**: Rocket 30 floors is a third to half of a level (levels have 60 to 150 rows, ECONOMY_TABLES), Flight 15 s at 8 u/s rise = 120 units. The "stops 3 rows below the finish" rule limits it, but it is a balance and difficulty change, not just a price change.

### (b) Conflicts
- (ii) **Durations are the owner's** (`PowerUpTests.cs:80`: "owner durations of 2026-10-31": Magnet 30, Shield 60, Wall Stick 30, Slow Time 30, Double Jump 30, Rocket 4 s / 20 floors, Flight 10). Doc 24 only has the lead's helper question P1 "longer durations may call for repricing Shield, Slow Time, Magnet", which is unanswered (and points the other way: reprice, not lengthen).
- (ii) Option A contradicts doc 22 4.3 (the owner price list 1,000/50 and 1,500/75) and changes the chest return the owner computed in doc 22 point 3 (Iron 86%, Gold 81%).
- (i) Hard-coded tests above; `PowerUpTests.cs:68` requires `GemPrice == CoinPrice / 20` (A keeps that).
Conflicts: 3.

### (c) Files
A: `PowerUpCatalog.cs:83-89` (price columns), `powerups.json`, `Tools/content_cli/gen_bundles.py:30-31` (`POWERUPS`) + regenerate `Content/economy/bundles.json` and `Assets/Resources/Content/bundles.json` (the test `test_bundles.py:85-90` requires both identical), `PowerUpTests.cs:73-79`, `ChestTests.cs:11`, `SubscriptionTests` (worth text uses coins only, unaffected), docs 22 4.3, 26 section 3, 27 sections 7-10. B: list above.

### (d) Questions
1. Reprice (A), lengthen (B), or keep? Recommend **keep prices and durations** until soft launch; the owner chose both. If something must change, the least invasive is only the Rocket price (75 -> 50 gems, 12.5 g/s), since it is the outlier at 18.75 g/s and does not touch durations.
2. If B: which of Flight/Rocket/Slow Time should be longer, and is a playtest round acceptable?

---

## RC 6: Decide on the subscription

### (a) Claims
All ratios recompute. Weekly: 100 + 7 x 10 = 170 gems, 3,500 coins = 175 gems, 345 GE, **232 GE/$**. Monthly: 200 + 300 = 500 gems, 18,000 coins = 900 gems, 1,400 GE (**281/$**), +6 PU x 60.71 = 1,764 GE (**354/$**). Worth x at $0.011: monthly 3.1 / 3.9, weekly 2.5 OK (code: `SubscriptionTests.cs:84-85` "2.5x", "3.1x"). A ($6.99): 200 (252 with PU), 2.2x (2.8x), net $5.94 OK. B (300 coins): 950 GE (1,314), 190 (263), 2.1 (2.9) OK. C ($5.99, 400 coins): 1,100 (1,464), 184 (244), 2.0 (2.7), net $5.09 OK. Weekly 300 coins = 275 GE, 185/$, 2.0x OK. $0.213 vs $0.166 per day, monthly 22% cheaper OK. Dev net $4.24 OK.
- **WRONG / MISSED, currency**: the owner priced the plans in **rupees: Rs 149 weekly, Rs 449 monthly** (doc 26 section 2; code comment `ProgressService.Subscription.cs:52`). The code and all dollar figures use **$1.49 and $4.99** (`SubscriptionCatalog.cs:55-56`, `subscriptions.json`). At the owner's own rate ($1 = Rs 95, doc 22) Rs 149 = $1.57 and Rs 449 = $4.73. So the real GE/$ is 220 weekly and 296 / 373 monthly (without / with PU), not 232 / 281 / 354. There is no INR field in `SubscriptionDef`. The UI shows dollars ("$1.49 / 7 Days"), doc 26 says "Rs 149 / 7 days".
- Doc 26 says the card text is "Worth about 2.4x" (weekly) and "Worth about 3.2x" (monthly); the code computes 2.5x and 3.1x with $0.011 per gem (doc 26 used Rs 1.04 per gem). Not an error, but two different results exist.
- **MISSED files**: `SubscriptionTests.cs:48-71` (`TheTablesAreTheOwnerNumbers`, exact prices and coins) and `:84-85` (worth text); `SubscriptionScreen.cs:303` (shows the worth text); `SubscriptionCatalog.cs:55-56` (BuiltIn) and `:91` comment; CLAUDE.md line 56; doc 13 and 09 text with the prices.

### (b) Conflicts
- (ii) **Subscription price and benefits are the owner's list (doc 26 section 2)**: instant 100/200 gems, daily 10 gems + 500/600 coins, PU every 5th claim, Rs 149 / Rs 449. All three options change them.
- (ii) RC 6's "View: keep it as it is" is consistent with the owner list.
- (i) Tests above.
Conflicts: 2 (for A, B, C), 0 for "keep".

### (c) Files
`Content/economy/subscriptions.json`, `SubscriptionCatalog.BuiltIn()` (`SubscriptionCatalog.cs:55-56`), `SubscriptionTests.cs:48-71, 84-85`, store price points when billing exists, docs 26 section 2, 27 section 8.

### (d) Questions
1. Keep both plans unchanged for now (recommended; watch pack-vs-subscription mix in soft launch, RC 14)? 
2. Is the real price in rupees (Rs 149 / Rs 449) or dollars ($1.49 / $4.99)? The app should store one source of truth (add `priceInr`), otherwise every "GE per dollar" figure is off by about 5%.
3. Should "Worth about" count the free power-ups (doc 26 helper question B3, unanswered)? Today it does not.

---

## RC 7: Keep chests below 1.0 and bundles at 1.6

### (a) Claims
| Claim | Result | Evidence |
|---|---|---|
| Iron 85.7%, Gold 80.7% | OK | Iron 25 + 60.71 = 85.71 GE; Gold 60 + 182.14 = 242.14 GE / 300 |
| Bundles 1.41 to 1.79, average 1.60 | OK | recomputed from the file: min 1.414, mean 1.605, max 1.786; tiers 1.596 / 1.613 / 1.605 |
| "tests enforce 70 to 105% (chests)" | **WRONG** | No test enforces a chest return band. `ChestTests.cs:11` only pins average value to 86.0 and 242.0 (+-1%) and `Assert.Less(average, price)` (line 68). Doc 22 line 72 *asks* for a "chest return 70 to 105%" validator; it was never written |
| "tests enforce 1.4 to 1.8 (bundles)" | OK | `BundleTests.cs:56-68`, `Tests/content/test_bundles.py:53-57` |
| Iron 500-700 coins -> 90.7% | OK | 600/20 + 60.71 = 90.71 |
| **Gold 1,200-1,600 coins -> 86.7%** | **WRONG** | avg 1,400 coins = 70 GE + 182.14 = 252.14 -> **84.0%**. 86.7% would need an average of 1,558 coins |
- **MISSED, breaks an owner rule**: Gold maximum becomes 80 + 225 = **305 GE, above its 300 price**; doc 22 point 3 states "A Golden chest can never beat its price" (current max 295). Iron max becomes 110 (already 105 > 100 today, so the owner accepted that for Iron, not for Gold).
- UI is data-driven (`TreasureChestScreen.cs:97,107,245` read `ChestRolls.MinCoins/MaxCoins`); no hard-coded text found.

### (b) Conflicts
- (ii) Chest contents are the owner's (doc 22 owner answer 1 and point 3; "Gold 1,000 to 1,400"); the "never beat its price" line is violated for Gold.
- (i) `ChestTests.cs:11,40,46` (ranges and reference averages), `CharacterRigTests.cs:58-59,85` (`Is.InRange(400, 600)`, `1000, 1400`) fail.
Conflicts: 2 (only if contents change).

### (c) Files
Change contents: `Chests.cs:48` constants; `ChestTests.cs:11,40,46`; `CharacterRigTests.cs:58,59,85`; docs 22 point 3, 26, 27 section 10, CLAUDE.md line 56. Doc work: the rule text is already in doc 22 line 72 and doc 27; what is missing is a real test (add `ChestReturnStaysInBand`).

### (d) Questions
1. Keep chest contents (recommended, zero work)? If you want more value, use Iron 500-700 only (90.7%, max 110) and leave Gold, or accept that Gold can beat its price at the extreme (305 vs 300).
2. Add the missing chest-return test (average between 70% and 105%, Gold max below price)? Recommend yes.

---

## RC 8: Do not enable characters in chests yet

### (a) Claims
| Claim | Result | Evidence |
|---|---|---|
| `ChestRolls.CharactersEnabled` is false | OK | `Chests.cs:43` `public const bool` (a const, so no test can flip it) |
| At doc 22's odds one character returns 286% (Iron), 276% (Gold) | OK | Iron: 85.7 + (0.67 x 150 + 0.33 x 300 = 199.5) = 285.2%; Gold: 242.1 + (0.57 x 300 + 0.29 x 700 + 0.14 x 1,500 = 584) = 275.4% |
| Option A: Iron 4.5% (+9 GE), coins 355-555; Gold 7% (+41 GE), coins 590-990; return "about 86% / 81%" | **WRONG** | Iron: coins 455 avg = 22.75 + 60.71 + 0.045 x 199.5 (= 8.98) = 92.4%. Gold: 39.5 + 182.14 + 0.07 x 584 (= 40.9) = 87.5%. To stay at 86%/81% the coins must drop by ~180 (Iron) and ~820 (Gold), i.e. Iron 220-420, Gold 190-580 |
| Option B: Iron 110, Gold 350 -> "about 86% / 81%" | **WRONG / ambiguous** | with no character the return is 77.9% and **69.2%** (Gold falls under the 70% band). It reaches 86%/81% only together with a character chance of ~4.3% (Iron) and ~6.9% (Gold) |
| Option C: 105 and 2.5%, 325 and 4% | OK | (85.7 + 4.99)/105 = 86.4%; (242.1 + 23.4)/325 = 81.7% |
| `CharacterSpec.GemPrice` is 0, so the 25% refund is 0 | OK | `Characters.cs` (Momo/Sporeling have no price); owner list 150/300/700/1,500 is not in code |
- **MISSED, the code does not use doc 22's odds**: `Chests.cs:49-50` has `IronPercent = {100, 50, 25}` and `GoldenPercent = {100, 50, 25}` as **independent per-tier chances** (best tier first, capped by `IronMaxCharacters = 1`, `GoldenMaxCharacters = 3`). Doc 22 wants weighted exclusive odds (Iron Uncommon 67 / Rare 33, Gold Rare 57 / Epic 29 / Legendary 14, one character per chest). Flipping the switch today would give: Iron always a character (Common 37.5%, Uncommon 37.5%, Rare 25%); Gold a Rare always, an Epic half the time, a Legendary a quarter, up to 3 characters. At the owner's price list that is a return of about 217% (Iron) and about 422% (Gold), not 286/276. There is **no single "character chance" variable**: Option A and C need a rewritten `Roll` (a one-character gate with a chance, then a weighted pick), not a constant change.
- **MISSED**: only two Common characters exist (`CharacterDefs.All = {Momo, Sporeling}`), so the Uncommon+ pools are empty; today's roll would only ever return Commons (refund 0). The character roll is untested because the switch is a `const`.

### (b) Conflicts
- (ii) Doc 22 open item 1 is exactly this question and is still open; the RC does not contradict a decision.
- (ii) Doc 22 point 3 says duplicates refund 25% as gems and "one character per chest" (code allows 3 in Gold).
- (i) `ChestTests.cs:34`, `CharacterRigTests.cs:65` assert the switch is off; chest info text `TreasureChestScreen.cs:108` prints "Characters: coming soon".
Conflicts: 1 (code vs doc 22 odds), others informational.

### (c) Files
`Chests.cs:43-52, 72-100` (Roll), `Characters.cs` (GemPrice, roster), `TreasureChestScreen.cs:108, 169, 245` (info text must list the odds, doc 22 line 40), `ChestTests.cs`, `CharacterRigTests.cs:56-66, 76-92`, doc 22 point 3 / open item 1. Effort is more than 60 min once the roll is rewritten and tested (100,000-opening simulation with characters on).

### (d) Questions
1. Agree to "do not enable until characters exist" (recommended: no Uncommon+ characters exist anyway)?
2. When they ship: which of A (low chance + less coin), B (higher prices + chance), C (both, small steps, recommended)? Numbers to use: Iron 105 gems with 2.5% character chance; Gold 325 with 4%. Note the doc 22 weights (67/33, 57/29/14) need new roll code.
3. Is "one character per chest" (doc 22) final, replacing the code's up-to-3 for Gold?

---

## RC 9: Cap replay farming below 3 stars

### (a) Claims
| Claim | Result | Evidence |
|---|---|---|
| Full pay below 3 stars, 50% only at 3 stars | OK | `CoinPayoutRules.cs:19-22` `IsReducedReplay(starsBefore) => starsBefore >= 3`; `Rewards.cs:107-118` |
| Rewards.cs lines 108-118 | OK | 108 `reducedReplay`, 113-118 scaling |
| 130 / 190 / 125 coins per replay (region 1) | OK | 70+60 = 130, 190, 250/2 = 125 |
| ~17.6 levels an hour | OK | 60 / 3.43 min (7,708 bot s x2 / 75 levels) = 17.5 |
| 2,290 / 3,344 / 2,200 per hour, 49% / 72% / 47% of 4,650 | OK within rounding | 130 x 17.5 = 2,278 (49%), 190 x 17.5 = 3,329 (72%), 125 x 17.5 = 2,190 (47%) |
| Proposed table (190 / 95 / 65) | OK | 190/2, 130/2 |
| "Check first: gem pickups on replays; doc 26 says cleared levels strip pickups" | OK, and stronger | `RunController.cs:51` strips for every level with Clears > 0; `RunSession.cs:74,80`. So a 1- or 2-star replay already drops no gems |
- **WRONG test file**: RC says `Tests/Simulation/GemCoinEconomyTests.cs and RewardsTests`. The payout tests are `Assets/_Game/Tests/EditMode/EconomyTests.cs` lines 259-321 (`ClearPaysSeventyPlusSixtyPerStarAndOnlyThreeStarReplaysPayHalf`, `ReplayBelowThreeStarsPaysInFullAndKeepsTheGems` at 314-321, `ThreeStarReplay...` 302-310, 346) and `Tests/Simulation/GemCoinEconomyTests.cs:272-278`. `RewardsTests.cs:200-215` is the separate replay *counter* cap (3 per day), unrelated.
- **MISSED, side effect**: `Rewards.cs:125` uses the same `reducedReplay` flag to zero `PickupGems`, and `:106`/test 314-321 assume pickups exist on <3-star replays. If `IsReducedReplay` becomes "stars <= previous", gems would also be zero for non-improving replays (harmless in practice because pickups are stripped, but the test `ReplayBelowThreeStarsPaysInFullAndKeepsTheGems` asserts `PickupGems == 2`). Use a separate flag for coins.
- **MISSED, UI**: `MetaController.cs:1596-1610` shows a "50%" pill only when the level has 3 stars; the new rule needs new text ("beat your stars for full pay").
- **MISSED, the problem is not removed**: after RC 9 the best repeatable farm is the 3-star replay, which already pays 125 coins (2,190/h, 47% of first-clear income). RC 9 lowers the ceiling from 3,344 to 2,190 coins an hour (-34%), it does not make the faucet dry up. 1-star replays drop to 65 (1,140/h), 2-star to 95 (1,665/h).
- Another existing guard: reward *counters* count only 3 replays a day (`RewardCatalog.cs:38` `MaxCountedReplaysPerDay = 3`), payouts are not limited.

### (b) Conflicts
- (ii) **Owner answer 3 (doc 22): "replays once a level has 3 stars pay 50% and no gems; below 3 stars full pay"**, repeated in doc 22 point 6, doc 08 Economy v3, doc 09 G (2026-10-25 row) and `CLAUDE.md`. RC 9 reverses it.
- (ii) Doc 22 open item 8 asked a *different* fix (full payout for the first 3 replays a day per level), unanswered; RC 9 does not mention it.
- (i) The 3 tests above; `Rewards.cs` comment line 105-106.
Conflicts: 2 decisions, 2 code items.

### (c) Files
`CoinPayoutRules.cs:18-22` (+ `Payout()` line 39), `Rewards.cs:107-118` (+ keep `:125` on the old rule), `MetaController.cs:1596-1610`, `EditMode/EconomyTests.cs:259-321, 346`, `Tests/Simulation/GemCoinEconomyTests.cs:272-278`, docs 22 point 3/6, 08 Economy v3, 09, 27 sections 4 and 17, `CLAUDE.md`.

### (d) Questions
1. Reverse your rule "below 3 stars full pay"? Options: (a) keep as is and accept farm up to 3,300 coins/h (recommended until soft launch shows farming); (b) RC 9 (full pay only if stars improve, else 50%); (c) your doc 22 item 8 idea (first 3 replays a day per level full, then 50%). Recommend (b) if you want a fix, because it needs no new save field.
2. If (b): should a replay that equals the best stars on a 1- or 2-star level also pay 50%? (That is what the proposal does.)

---

## RC 10: Add ad placements, with caps

### (a) Claims
| Claim | Result | Evidence |
|---|---|---|
| Only the revive ad exists | OK | `RunController.cs:262` `AdService.ShowRewarded(granted => ...)`; no other caller |
| Revive ad adds ~$0.007/player/day | OK | 0.7 x $0.01 (assumptions A-4, doc 27) |
| Double result: +250 coins = 12.5 GE ($0.14) | OK | 250/20 x 0.01099 = $0.137 |
| 20 a day cap, 0.6 views, $0.006 | OK arithmetic | |
| Total 1.6 views, $0.016 | OK | |
| 2.5 views at $10 eCPM = $0.025, $25 = $0.063 | OK | |
| Needs: placement enum and `AdService.ShowRewarded(placement, callback)` | OK it is missing | `AdService.cs:59` `ShowRewarded(Action<bool>)`; `IRewardedAds` has no placement |
| Ledger key `ad:<placement>:<run id>`; per-day counters `adDay`, `adCounts` in the save | MISSED: none exists | no ledger anywhere in the code (grep); `SaveData.cs` has no ad fields. Doc 13 line 124/185 plans the key; it is new work |
| AdConfig holds placeholder keys; consent/age gate before first request | OK | `AdConfig.cs:15-16` placeholders; no consent code (doc 22 open item 15 unanswered) |
| Buttons on the results screen and the "Daily tile" | MISSED | There is no Daily tile; doc 26 replaced the Daily button with the Rewards screen (tabs Main/Daily/Weekly) |
- **Cap check, not in the RC**: 20 double-result views a day at 250 coins = 5,000 coins = **250 GE a day ($2.75)**, more than the whole Daily reward (180.7 GE) that RC 2 wants to trim, plus it stacks on RC 9's replay farm (doubling a replay doubles the farm). The cap should be far lower (3 to 5) or limited to first clears.
- RC 3-C uses 15 daily-power-up views a month; this table uses 0.3 per day (9).
- "Ad pays no gems": consistent with doc 27 section 16.4.

### (b) Conflicts
- (ii) Doc 22 point 9 says the ad revive is offered only with the first revive (kept). Doc 22 point 8 daily power-up ad is superseded (finding 3). Double result is **not in any owner doc** (doc 27: "not specified"); doc 13 section 4.2 lists it as the assistant's plan.
- (ii) Open owner items not answered: consent/age gate wording (doc 22 item 10, 15), LevelPlay key.
- (i) Revive flow must stay untouched (`RunController.cs:256-275`, `ReviveTests.cs`).
Conflicts: 2 (superseded design, unapproved placement).

### (c) Files
`Gameplay/Ads/AdService.cs` (placement + cap), `IRewardedAds.cs`, `TestRewardedAds.cs`, `AdConfig.cs`, `RunController.cs:256-275` (call site), results UI in `RunHud.cs`, Rewards screen (Daily tab), `SaveData.cs` (new additive fields, no version bump), a reward ledger in `ProgressService`, `ReviveTests.cs` + new ad tests, doc 13 section 4.2.

### (d) Questions
1. Do you want a double-result ad at all? If yes: cap per day (recommend 5, first clears only) and coins only.
2. Daily power-up ad: gate the 100-point reward or add a second one? Recommend no gating of existing rewards.
3. Provide the LevelPlay app key/ad unit and approve the consent and age-gate flow wording (doc 22 items 10, 15) before any new placement.

---

## RC 11: Launch promo and first-purchase double

### (a) Claims
| Claim | Result | Evidence |
|---|---|---|
| Both exist and are off | OK | `GameSettings.cs:63-77` PlayerPrefs default 0; `ShopEconomyTests.cs:158-162` assert off |
| Gems per $ columns | OK | List 63.3 / 67.1 / 80.3 / 83.5 / 91.0 / 95.3 / 100.0 / 111.1 / 125.0; double 126.6 / 134.2 / 160.6 / 166.9 / 182.0; promo 102.0 / 126.6 / 155.0 / 167.2 / 182.1 / 190.7 / 200.1 / 222.3 / 250.0 (doc 27 shows 167.0 for the 500 double, 1,000/5.99 = 166.9 is right) |
| "halves every gem pack price (a gem is worth $0.004 to $0.0079)" | **WRONG upper end** | promo $/gem: 50 pack $0.0098, 100 pack $0.0079, 15,000 pack $0.0040. Range is $0.0040 to $0.0098. "Halves" holds for packs of 200 and up (49.9% to 52%); the 50 pack is 62% and the 100 pack 53% |
| Halves coin pack gem prices (40 to 50 coins per gem) | OK | `CoinRows` 25/50/125/250/450/1,100/2,000; 1,000/25 = 40 ... 100,000/2,000 = 50 |
| Needs two store SKUs per pack (list and .launch) | OK as a plan | `GemPackDef` has one `Sku` (`Models.cs:125`) |
| First-purchase double beats the promo on small packs, applies once | OK | 126.6 vs 102; 134.2 vs 126.6; 160.6 vs 155.0; tie at 500 |
| Files: GameSettings, `ProgressService.Shop.cs` | PARTLY | also `ProgressService.cs:109-114` (`LaunchPromoActive`), `ShopScreen.cs:389,391,448,589`, `MetaController.cs:500`, `RunHud.cs:421`, `TreasureChestScreen.cs:275` |
- **MISSED**: the promo prices are **invented placeholders** (doc 22 open item 22: "Promo prices are invented because the doc has none ... Accept, or use list price?", unanswered). Turning the promo on would ship prices the owner never chose. Doc 22 open items 24, 26 (flag storage, no settings UI) also unanswered.

### (b) Conflicts
- (ii) Doc 22 owner answer 5: both stay in code, OFF by default, switched on later by the owner, both tested end to end. RC 11's "keep off" is consistent.
- (i) None.
Conflicts: 0.

### (c) Files
None for "keep off". For a flip: `GameSettings.cs:63-77`, `gem_packs.json`/`coin_packs.json` promo columns, store SKUs, `ShopEconomyTests.cs:292-340`, docs 27 (all dollar columns).

### (d) Questions
1. Confirm both stay OFF until billing exists (recommended).
2. Accept the placeholder promo prices (doc 22 item 22) or replace with owner-set prices before they can ever be switched on?
3. At launch, use the first-purchase double (recommended) rather than the promo?

---

## RC 12: Starter pack when billing exists

### (a) Claims
Source doc 22 point 7: $0.99 = 100 gems + 2,000 coins OK. Value: 100 + 100 = 200 GE = **$2.20 ref, 2.22x, 202 GE/$** OK; 100-gem pack 100 GE, $1.10, 0.74x, 67 OK. Position between packs (63 to 125) and subscriptions (232 to 354) OK in GE/$ (202 is below weekly 232 and above every pack).
- Doc 27 section 17 says "about 2.4 times value"; the correct figure is 2.2x (RC 12 is right, doc 27 wrong).
- Not built: no `starter` anywhere in code, no billing package (`Packages/manifest.json` has no `com.unity.purchasing`), `SaveData` has no `starterBought`. OK.
- "SKU fluffyclimbers.starter": consistent with `fluffyclimbers.gems.<n>` naming.
- "Rs 99": $0.99 x 95 = Rs 94; doc 22 uses Rs 49 for the India entry bundle (40 gems + 500 coins), which RC 12 omits.
- **Observation**: for $0.99 the starter gives 200 GE, while the $1.49 pack gives 100 GE and the $0.79 pack 50 GE, so for a first-time buyer it strictly dominates the 50- and 100-gem packs (as intended for a converter, but the 100-pack then looks pointless on first view).

### (b) Conflicts
- (ii) Owner doc 22 point 7 lists it as "Future spec", so adding later fits. Open items 4 (India price points) and 7 (expiry, gems-only or money) are unanswered.
- (i) None.
Conflicts: 0.

### (c) Files (when built)
`Content/economy/` new starter file (or `gem_packs.json` entry), `SaveData.cs` flag, `ProgressService.Shop.cs`, `ShopScreen.cs` placement, tests (once per account, atomic grant, restore), doc 13.

### (d) Questions
1. Build the starter with billing and not before (recommended)? 2. Expire after N days or never (doc 22 item 7)? 3. Include the Rs 49 India entry bundle? 4. Price in dollars or rupees?

---

## RC 13: Remove gear leftovers

### (a) Claims
| Claim | Result | Evidence |
|---|---|---|
| `boots_cloud_runner` in `Content/items/items.json`; `gen_content.py` 205-207 | OK (lines 205-209) | the write call spans 205-209 |
| `content.py` 139-140 validates items, works on empty | OK | lines 139-141, loop over `load(...)` |
| `item.schema.json` slots | OK | enum has `character, outfit, boots, hat, accessory, trail, powerup` |
| `ContentRepositoryTests.cs` line 39 `Assert.IsNotEmpty(Repo.Items)` | OK | line 39 |
| `SimConfig.cs` line 13 "applies gear boosts" | OK | line 13 |
| `MetaController.cs` line 1784 "gear and chest overlays" | OK | line 1784 |
| `CharacterRigTests.cs` line 98 old-save test | OK | lines 96-98 |
| `PowerGearArt.cs` is power-up art, keep | OK | `RunView.cs:926-930,1002` use it for Flight wings / Rocket pack |
| `ReadList` works with an empty array | OK | `ContentRepository.cs:101-113` |
| `Repo.Items` has no runtime consumer | OK | only the test |
- **MISSED**: the asset catalog gear classes are five, not two: `Content/assets/catalog/` has `boots.md` (484 lines), `outfit.md` (604), `hat.md` (454), `accessory.md` (304), `trail.md` (184). `Content/assets/manifest.json` has 132 gear assets of 367 (outfit 40, boots 30, hat 30, accessory 20, trail 12). `Tools/asset_cli/gen_asset_manifest.py` lines 118 and 160 (gear entries such as `gear_boots`) and `Tools/art_pipeline/make_polygon_templates.py` lines 6, 39, 90 (gear templates), and `Art/_candidates/` gear folders (`gear_pilot`, `polygons/gear_*`, `after_gear_removal.png`).
- **MISSED**: `trail` is not named in your "no outfits, boots, hats or accessories" rule (`CLAUDE.md` line 15); the code's "trail" is a motion effect (`RunView.cs`, `SimConfig.cs`). Ask before removing it from the catalog and schema.
- **MISSED**: emptying `items.json` changes `contentHash`, so `content_manifest.json` must be rebuilt (Unity merger), same step as RC 1. Also `Models.cs:153-165` `ItemDef` and `ContentRepository.cs:34,72` `Items` become unused (keep, harmless).
- Old approvals `Content/assets/approvals/B01.md, P1.md, T52 to T59`: exist (confirmed), keep as history.

### (b) Conflicts
- (ii) Doc 20 (2026-10-24) removed gear; consistent.
- (i) The old-save test must stay.
Conflicts: 0.

### (c) Files
`Content/items/items.json`, `Tools/content_cli/gen_content.py:205-209`, `Content/schemas/item.schema.json` (enum), `Assets/_Game/Tests/EditMode/ContentRepositoryTests.cs:39`, `SimConfig.cs:13`, `MetaController.cs:1784`, manifest rebuild, `Content/assets/catalog/{boots,outfit,hat,accessory}.md` (+ `trail.md` if confirmed), `Content/assets/manifest.json`, `Tools/asset_cli/gen_asset_manifest.py`, doc 10 totals.

### (d) Questions
1. Remove the catalog classes hat, accessory (and trail?) as well as boots and outfit? Recommend yes for the four gear classes, and ask about trails.
2. Delete `Art/_candidates` gear images or archive?

---

## RC 14: Validate in soft launch

### (a) Claims
- "No analytics layer exists (doc 22 open item 16)": OK. No analytics SDK in `Packages/manifest.json` (only the built-in `com.unity.modules.unityanalytics` module); no event calls in code. Doc 13 section 6 and doc 05 section 9 already plan **Firebase** events and UMP consent; so "choose a service first" is partly decided already (doc 13 says Firebase) and not confirmed by the owner.
- Event list vs existing plans: RC 14's `gem_source/gem_sink/coin_source/coin_sink/ad_reward/revive_taken/chest_open/bundle_bought/subscription_started/gem_pack_bought` partly differ from doc 05 section 9 and doc 22 (`revive_offer`, `coin_pack_bought`, `chest_odds_viewed`, `bundle_view`, `daily_reward_view`, `ad_offer`, `ad_complete`, `iap_success`). Two naming schemes would exist.
- Metric thresholds (balance 100 to 400 after day 7, 30 to 50% bundles, 15 to 25% chests, 10 to 20% revives, 1.2 to 1.6% pack buyers, 30% ad opt-in, 20,000 coins at day 30): none comes from an owner doc; doc 27 section 17.1 has the same table except the day-30 coin row (new). All are assumptions.
- Assumptions at the end (60% pickup, 3.4 min per level, $0.01 per ad, 2% payers, $18.40): OK, match doc 27 A-1..A-5.
- Effort and duration: not verifiable.
- Gem sink shares do not sum to a closed list (power-ups, skips, coin packs are missing).

### (b) Conflicts
- (ii) None with owner decisions; overlaps doc 05/13 event plan.
- (i) None.
Conflicts: 0 (1 naming overlap).

### (c) Files
New: analytics wrapper (e.g. `Gameplay/Analytics/IAnalytics`), call sites in `ProgressService*.cs` (every currency change goes through `Data.Gems`/`Data.Coins` writes: `Commit()` sites), `RunController.cs` (revive), `AdService.cs`; consent dependency from RC 10; docs 05 section 9 and 13 section 6 (merge the event lists).

### (d) Questions
1. Which analytics service (doc 13 assumes Firebase)? Recommend Firebase Analytics + UMP, behind an interface.
2. Use one event list: doc 05/13 names plus `gem_source/gem_sink`. OK?
3. Treat the thresholds as starting guesses only?

---

## Summary table

"Claims OK / wrong" counts the factual claims checked in section (a); partial counts as wrong.

| RC | Claims OK / wrong | Conflicts (decision / code) | Risk | Recommended decision |
|---|---|---|---|---|
| 1 Stale constants | 9 / 1 (plus 3 missed items: 450 level files, revive constants, strict loader) | 0 / 1 | Low-medium (one-commit rule; Unity manifest rebuild) | Do it. Set `coinsPerGem` 20, delete `vipGemsPerDay` across JSON + Models + manifest in one commit; fix revive constants 5/10; leave per-level replay field or set 50; never run `gen_content.py` |
| 2 Daily size | 8 / 0 (ceiling caveat) | 1 / 1 | Low | Do not cut yet. If you cut, option A (power-up to 500 coins) only. First decide the unreachable `d_allgems` problem |
| 3 Random power-ups | 5 / 3 (2,082 GE, 1,041 GE, pool "one line") | 3 / 1 | Medium (reverses equal-chance rule, superseded design) | Keep equal 7-type pool; if anything, B. Drop C as written (cites superseded doc 22 point 8) |
| 4 Main rewards | 6 / 0 | 1 / 1 | Low | Keep as is; revisit after day-7 balances |
| 5 Reprice by time | 5 / 1 (file list for B incomplete; bundles 206 below floor) | 2 / 2 | High for A (bundles regen, chest return, owner prices); medium for B (balance, tests) | Keep. If one change: Rocket price 75 -> 50 only |
| 6 Subscription | 9 / 2 (USD vs owner Rs prices; worth text) | 2 / 1 | Medium (owner list, pricing currency) | Keep as is; settle rupee vs dollar price source |
| 7 Chests / bundles | 4 / 2 (Gold 86.7% is 84.0%; "tests enforce 70-105%" false) | 2 / 1 | Low if unchanged; medium if Gold raised (max 305 > price) | Keep contents; add the missing chest-return test |
| 8 Characters in chests | 3 / 2 (A returns 92%/88%; B incoherent), 2 missed (code odds differ, no chance knob) | 1 / 2 | Low now (switch off), high at enabling | Do not enable; when ready use C and rewrite the roll to doc 22 weights |
| 9 Replay cap | 6 / 2 (wrong test file, gem flag coupling) | 2 / 2 | Medium (reverses owner rule; does not end the farm) | Owner decides; if yes use the stars-improve rule with a separate coin flag and a UI text change |
| 10 Ad placements | 8 / 2 (no ledger/counters exist; "Daily tile" gone) | 2 / 1 | Medium-high (consent/age gate, 20/day cap = 250 GE a day) | Build only after key + consent decisions; cap double result at ~5 a day; do not gate owner rewards |
| 11 Promo / first-purchase | 6 / 1 ($0.004-0.0079 should be $0.0040-0.0098) | 0 / 0 | Low | Keep both off; first-purchase double as the launch lever; replace invented promo prices |
| 12 Starter pack | 7 / 0 (doc 27 says 2.4x, RC correct 2.2x) | 0 / 0 | Low | Build with billing; ask expiry and Rs 49 bundle |
| 13 Gear leftovers | 9 / 1 (catalog classes 2 of 5 named) | 0 / 0 | Low | Do it, with the five catalog classes (ask about trails) and the manifest rebuild |
| 14 Soft launch | 3 / 1 (service choice/events already planned in docs 05, 13) | 0 / 0 | Low | Pick Firebase behind an interface; merge event names |

## Questions the lead should ask the owner (shortest useful set)

1. RC 1: OK to fix constants in one commit (including a Unity manifest rebuild), and what to do with the 450 unread per-level `replayCoinPercent` fields?
2. RC 2/3/10: the 100-point Daily reward cannot be earned once all levels are cleared ("collect all gems" needs an uncleared level). Change that task, or accept 90 points? Then decide whether any Daily cut or ad gating is still wanted.
3. RC 3: keep the equal 7-type power-up pool?
4. RC 5/6/7/4: confirm that your explicit tables (durations, power-up prices, subscription, chest contents, Main rewards) stay unchanged for soft launch (recommended).
5. RC 6: is the subscription price Rs 149 / Rs 449 (doc 26) or $1.49 / $4.99 (code)?
6. RC 9: reverse "below 3 stars full pay"? (a) keep, (b) pay 50% unless stars improve, (c) first 3 replays a day full.
7. RC 10: want a double-result ad (cap?), and who supplies the LevelPlay key and consent/age wording?
8. RC 11: keep the promo and first-purchase double off, and replace the invented promo prices?
9. RC 13: remove hat/accessory/(trail?) catalog classes too?
10. RC 14: Firebase Analytics?
