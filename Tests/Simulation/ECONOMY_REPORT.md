# T29 Economy check: is gem inflow healthy? (regions 1 to 5, economy v2)

Analysis only: no rules, tuning or content were changed. Source: `Tests/Simulation/EconomyTests.cs` (category `Economy`,
run `dotnet test Tests/Simulation --filter "Category=Economy"`). It builds all 75 shipped levels with the real `LevelBuilder`,
sums the gem pickups placed, replays the `Rewards.cs` arithmetic (3-star coin payout, milestone gems), and lets the `LevelBot`
play every level for a time estimate. Full per-level and per-region tables are generated into `ECONOMY_TABLES.md`.

Assumptions: "c" is the share of placed gem value a player actually picks up (gems sit on risky spots). The bot picks up about
80 percent on its solving line (region 1: 127 of 158) without detours, so c = 60 percent is used as the realistic human case
(100 and 30 percent also shown). Human time = 2 x bot time (guess). Prices: revive 5 then 10 gems (the figures below were produced with the older 2 then 5 and 100 coins per gem; the next run of `EconomyTests` regenerates `ECONOMY_TABLES.md` with 5/10 and 20) (max 2 per run), region skip =
`unlock.gemSkip` of the next region (100, 116, 133, 150, 166). Gem tiers 1/2/5/10 gems at 70/22/7/1 percent (expected 1.59 per pickup).

## Headline numbers

| Region | Gem pickups | Gems placed | Per level | Milestone gems (1st clear, 3 stars) | Max first pass | Next skip price | Price / max | 3-star coins (no combo) | Bot minutes |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 102 | 158 | 10.5 | 11 | 169 | 100 | 0.59 | 2250 | 36.9 |
| 2 | 108 | 158 | 10.5 | 12 | 170 | 116 | 0.68 | 2325 | 33.6 |
| 3 | 121 | 204 | 13.6 | 12 | 216 | 133 | 0.62 | 2385 | 39.4 |
| 4 | 93 | 140 | 9.3 | 11 | 151 | 150 | 0.99 | 2460 | 36.8 |
| 5 | 106 | 172 | 11.5 | 11 | 183 | 166 | 0.91 | 2520 | 40.8 |

- Gems per level: mean 10.7, median 10, range 0 (lvl_002_01) to 35 (lvl_003_15). 11 of 75 levels pay 2 gems or less.
- Gems per region: 140 to 204 placed, plus about 11 milestone gems (L15 pays 10 on 3 stars; L5 and L10 add 0 to 2). Milestones are only about 6 percent of income.
- 3-star coin payout is flat per level: (30 + 120) x region factor = 150 (region 1) to 168 (region 5), so 2250 to 2520 coins per region before combo (combo adds 5 per step; the bot line adds 53 to 516 per region). At the current 20 coins per gem that is 113 to 126 gem-equivalents (the generated table used 100 coins per gem when this report was written) per region.

## Time to afford a region skip by playing

Gems earned when playing the preceding region once (first clears, 3 stars), and minutes of play until the total reaches the next skip price:

| Region played | Skip price of next | c=100% gems / min | c=60% gems / min | c=30% gems / min | Gems per minute at c=60% |
|---|---|---|---|---|---|
| 1 | 100 | 169 / 46.6 | 106 / 73.8 | 58 / never | 1.4 |
| 2 | 116 | 170 / 50.1 | 107 / never | 59 / never | 1.6 |
| 3 | 133 | 216 / 59.1 | 134 / 78.8 | 73 / never | 1.7 |
| 4 | 150 | 151 / 73.6 | 95 / never | 53 / never | 1.3 |
| 5 | 166 | 183 / 81.6 | 114 / never | 63 / never | 1.4 |

Cumulative wallet from region 1 with no spending (c=60 percent): 106 gems before region 2 (price 100), 213 before region 3 (116), 347 before region 4 (133), 442 before region 5 (150). So a player who clears everything ends with a growing surplus (6, 97, 214, 292), and a player at c=100 percent even more (169, 339, 555, 706).

Reading: buying the next region by play alone costs about the whole preceding region (roughly 75 minutes of play at c=60 percent, 1.3 to 1.7 gems per minute, so about 80 to 100 gems per hour). Skipping therefore saves no time for a player who is earning the price; it is a paid shortcut. In money: 80 gems cost $0.99 (`gem_packs.json`), so the skip prices are about $1.2 to $2.1, which is cheap against about an hour of play.

## Do revives drain more than a player earns?

Average income is 6.3 to 9.0 gems per level (c=60 percent, including milestones). A first revive (now 5 gems; this paragraph was computed with the old 2) is 22 to 32 percent of one level's income; a full revive pair (now 5 + 10 = 15 gems; computed with the old 2 + 5 = 7) is 0.8 to 1.1 levels of income. Occasional revives are sustainable (3.2 to 4.5 first revives per level of income), but a player who buys both revives on every run earns roughly nothing net. Gems collected in a failed run are lost (a failed run pays nothing), so a revive is the only way to keep them. A replay of a 3-star level pays no gems, so any revive there is pure cost. Conclusion: revives do not drain the typical player, they do drain a player who relies on them every run.

## Verdict

Inflow is **adequate and not inflated**: about 10 gems per level, about 160 to 220 per region, enough that a normal clear of regions 1 to 3 funds the next skip, and revives only bite a player who revives every run. It is thin for low collectors, and the numbers get worse in later regions (see below).

## Concerns and recommendations (not applied)

1. **Skip prices outgrow gem income.** Prices rise 100 to 166 by region 6 (400 by region 20 per `Economy/NOTES.md`) while gem income per region stays flat around 140 to 216 and does not scale with the region. Region 4 (0.99) and region 5 (0.91) are already near 100 percent of everything placed in the preceding region; at c=60 percent regions 2, 4 and 5 cannot be afforded from one pass of the region before them. Recommendation: either scale gem density or tier odds with region index, or accept that later skips are a purchase and not a play goal (and say so in the shop copy).
2. **Gem farming through a 2-star loop.** Pickup gems pay in full on every replay until the level has 3 stars (`Rewards.cs` line 112), and the layout is deterministic per seed. A player can take only the gems, skip the third star and replay forever. Best case lvl_003_15: 35 gems per about 5 bot minutes (about 3.4 gems per minute with the 2x human factor), roughly double the normal 1.3 to 1.7. Recommendation: pay pickup gems only on the first clear of a level (or only the gems not yet collected, tracked per level), or cap replay gems.
3. **Very uneven gem distribution.** 0 to 35 gems per level (median 10); the 1 percent legendary (10 gems) and 7 percent epic (5 gems) dominate variance: lvl_003_15 has 35 gems from 15 pickups, lvl_004_15 has 11 from 10. 11 of 75 levels give 2 or fewer gems, so the "gems per level" a player feels is lumpy and the L15 milestone (fixed 10) is the only guaranteed income. Recommendation: floor of about 5 placed gems per level, or move rare tiers to a fixed placement.
4. **Resolved (RC 1, 2026-11-01).** The unread per-level `rewards.replayCoinPercent` field was deleted from all level files, the model and the schema; `remote_config.json` `economy.replayCoinPercent` = 50 matches the rule `ReplayPercent` = 50. Still open: level `rewards.coinPayout` (60, 65, ...) is obsolete under v2 (the formula pays 150 at 3 stars). Remote Config would overwrite the rule if it is ever wired.
5. **Failed runs lose their gems.** Combined with revive prices this nudges players to buy revives to bank gems (good for monetisation, risky for fairness). Worth an explicit owner decision; no change made.
6. **Coins have no clear gem sink here.** One 3-star pass pays about 22 to 25 gem-equivalents at the old `coinsPerGem` 100 (now fixed to 20, RC 1), and a skip is 10,000 to 16,600 coin-equivalents, so if coins can ever be converted to gems the skip is trivially affordable. Confirm coins cannot buy gems.
7. **Minor.** `Economy/NOTES.md` item 6 says hidden gems pay no wallet gems, but `Rewards.cs` pays +1 gem on the first clear of a level with a hidden gem (not counted in the tables above); NOTES is stale. The human-time factor (2x) is a guess; real play-test timings would sharpen the time-to-afford figures.
