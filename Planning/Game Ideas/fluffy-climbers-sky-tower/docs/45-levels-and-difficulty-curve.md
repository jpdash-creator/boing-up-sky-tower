# 45. Levels and the difficulty curve: 25 levels per region, the 0..100 difficulty score (2026-11-09)

**Source:** owner Bug List 6, 2026-11-09. **Replaces:** the 15-level structure of doc 25 and the old difficulty numbers of doc 24 for Regions 1 to 5 (doc 24 still describes the special platform kinds; doc 25 still describes rows, platforms per row and goal details until the code is changed). It also applies to Regions 6 to 30 when they are migrated, and to every future region.

Units: `u` = world unit (player width 1, screen width 10), `u/s` = units per second.

## 1. The rule in one paragraph

Every region has **25 levels instead of 15**. Difficulty is no longer a loose "d from 0.05 to 1.00"; every level has a measured score from 0 to 100 and a target score. The curve rises gently over levels 1 to 15 (score 5 to 75) and then faster over levels 16 to 25 (75 to 100), so the new Level 25 is as hard as the old Level 15, and the new Level 15 is about as hard as the old Levels 9 to 10. The first 15 levels become easier to climb into, and the extra 10 levels carry the player to the old peak.

## 2. The difficulty score (0..100)

A level's score is built from five measured parts. Each part is first turned into a number between 0 (as easy as the easiest old level) and 1 (as hard as the hardest old level) using the OLD Region 1 levels 1 and 15 as the two ends.

| Part | Easiest end (old L1) = 0 | Hardest end (old L15) = 1 | Direction | Weight |
|---|---|---|---|---|
| Precision: share of jump options that land safely on a higher row | 61.8 percent | 32.5 percent | inverted (fewer safe options = harder) | 0.35 |
| Platform width (average, u) | 4.13 | 2.65 | inverted (narrower = harder) | 0.20 |
| Scroll speed at the start (u/s) | 0.69 | 2.40 | higher = harder | 0.20 |
| Share of rows with a non-normal platform | 20 percent | 59 percent | higher = harder | 0.15 |
| Average row gap (u) | 2.62 | 3.73 | higher = harder | 0.10 |

The weights add up to 1.00. With c the weighted sum of the five normalised parts, c_L1 the sum of the old Region 1 level 1 and c_L15 that of the old Region 1 level 15:

**score = 5 + 95 x (c - c_L1) / (c_L15 - c_L1)**

So the old Level 1 scores exactly 5 and the old Level 15 exactly 100. A level harder than the old Level 15 can score above 100 (a warning, not an error). A part outside its two ends is not clamped, so one odd part does not hide in the sum.

**How it is measured.** With the default hero (Momo Common), on 3 or more random layouts per level (attempt seeds as in the old difficulty report), using the same probe as the old report (`Tests/Simulation/DIFFICULTY_REPORT.md`). The mean over the layouts is the level's score.

**The old Region 1 levels 1 to 15 on this scale (reference table):**

| Old level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Score | 5 | 22 | 40 | 44 | 39 | 61 | 66 | 73 | 75 | 77 | 86 | 88 | 93 | 96 | 100 |

The old curve was not smooth: levels 4 to 5 dip (44 then 39) and levels 10 to 11 jump by 9 points. The new targets below are smooth.

## 3. The target for levels 1 to 25

Tolerance: plus or minus 3 points on the measured score of every level (mean of 3 or more layouts).

| Level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Target | 5 | 20 | 26.7 | 33.3 | 40 | 44 | 48 | 52 | 56 | 60 | 63 | 66 | 69 |

| Level | 14 | 15 | 16 | 17 | 18 | 19 | 20 | 21 | 22 | 23 | 24 | 25 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Target | 72 | 75 | 77.5 | 80 | 82.5 | 85 | 87.5 | 90 | 92.5 | 95 | 97.5 | 100 |

Shape: 5, then 20, then +6.67 per level to 40 (level 5), +4 per level to 60 (level 10), +3 per level to 75 (level 15), then +2.5 per level to 100 (level 25).

## 4. Classes, special goals, ids

| Class | Score | Levels (by target) |
|---|---|---|
| Simple | up to 30 | 1 to 3 |
| Medium | 30 to 55 | 4 to 8 |
| Hard | 55 to 75 | 9 to 15 |
| Complex | 75 and above | 16 to 25 |

Level 15 (target exactly 75) sits on the border: it counts as Hard (the top of the hard band).

- **Special-goal levels** (combo chain, no wall bounce, hidden gem, speedrun) are levels **5, 10, 15 and 20**. Which goal sits on which level is set per region in the level table; the goal rules themselves stay as in doc 25 (combo chain has no gems, no wall bounce fails on a wall touch, and so on). The speedrun goal is new and is specified when it is built.
- **Level 25 is the highly complex reach-the-top level** of the region (plain reach-the-last-platform goal, the hardest layout of the region).
- Level ids are `lvl_RRR_01` to `lvl_RRR_25` (RRR = region number, three digits). The existing ids 1 to 15 **keep their ids and their seeds**, so saves, stars and bot proofs stay valid; ids 16 to 25 are new.
- Platforms per row and special share are set so the measured score meets the target of section 3; the doc 25 values are a starting point, not a rule. **Rows per level are fixed by the owner (section 4a).**

### 4a. Rows (floors) per level, Regions 1 to 5 (owner Bug List 8 item 3, 2026-11-09)

The level size is the number of platform rows (floors) from the start platform to the top row. It is the goal value of every row goal (reach the top, no wall bounce, hidden gem, speedrun) and `LevelSpec.RowsFor()` (the combo chain level 5 has no row goal and uses 75). It is the same in all five open regions; Regions 6 to 30 keep their legacy tables until they are migrated.

| Levels | Rows (floors) | Was (old table) |
|---|---|---|
| 1 | 60 | 60 |
| 2 to 5 | 75 | 90 |
| 6 to 10 | 90 | 120 (6 to 9), 150 (10) |
| 11 to 15 | 105 | 150 |
| 16 to 20 | 120 | 150 (16 to 19), 90 (20, speedrun) |
| 21 to 25 | 150 | 150 |

Rows per region fall from 3,240 to 2,685 (a level is on average 17 percent shorter). The special levels follow their band: level 5 combo chain 75 rows, level 10 no wall bounce 90, level 15 hidden gem 105, level 20 speedrun 120 rows. **The speedrun time limit scales with its rows: 75 s for 90 rows became 100 s for 120 rows** (same margin over the bot as before). Goals, stars and star tiers, rewards (coin payout per level) and the difficulty scores of section 3 are unchanged; the builder difficulty d of each level was re-solved for the new lengths (section 10).

## 5. Region goal tiers and stars

| Tier | Clear this many levels of the region | Reward |
|---|---|---|
| 1 | 5 | same as the old tier 1 (doc 39) |
| 2 | 10 | same as the old tier 2 |
| 3 | 15 | same as the old tier 3 |
| 4 | 20 | same as the old tier 4 |
| 5 | 25 (all) | same as before, and the region's **Common hero** |

Stars per region: 25 levels x 3 = **75**. Star thresholds that unlock regions or rewards are scaled to 75 per region in the code, and doc 39 is updated in the same change.

## 6. The endless tower

The endless tower has **25 stages of 100 floors** (floors 1 to 2500). Stage N follows the difficulty of level N (stage 1 = level 1, up to stage 25 = level 25). After floor 2500 the tower **cycles** (`EndlessTower`, `FirstCycleFloors` 2500, `CycleFloors` 1400): levels **15 to 24** for 100 floors each, then **level 25 is held for 400 floors** (`HoldFloors`), and that 1400-floor cycle repeats without end (floors 2501 to 3900 are the first cycle, and so on). The difficulty never rises above level 25. The endless generator stays separate from the level generator; only its difficulty input changes. Each 100-floor stage of the endless tower uses the **complexity** of its campaign level (difficulty, platform kinds, scroll speed), not that level's row count (section 4a); the endless floors per stage stay 100 (owner Bug List 8 item 3).

**Time Attack ramp (owner Bug List 8 item 3).** Time Attack (doc 46, section 7) does not start at level 1. Floor 1 of a Time Attack run has the complexity of **level 10**; every further 100 floors the level rises by one up to **level 25**, which is then held for the rest of the run (it never enters the endless cycle). Floors 1 to 100 are level 10, 101 to 200 level 11, and so on to floors 1501 and above at level 25. Implemented as `EndlessTower.LevelForFloorTimeAttack`: the plain first climb read at `floor + 900` (`TimeAttackFloorOffset`), clamped to the top level, so the tower stays prefix stable and replayable from the seed (`LevelSpec.TimeAttack`, `LevelData.EndlessTimeAttack`; the Sim scroll speed follows the same level).

**Curve note.** The Region 4 (Candy Canyon) difficulty curve overshoots its target by 7.07 points on levels 23 to 25 (its sticky platforms limit how hard it measures); the owner accepted this for now (section 10).

## 7. The Gameplay setting (replaces the old Difficulty setting)

The old Difficulty setting is removed from Settings. The new **Gameplay** setting lives on the **Level screen**: a centred row of five icon buttons directly under the region name (each at least 48 x 48 dp, with icon or shape differences, not colour alone, per CUX-4 and CUX-6).

| Button | Relaxed | Easy | Normal | Hard | Insane |
|---|---|---|---|---|---|
| Scroll speed-up step per 5 s (owner 2026-11-12; was 20/22/25/28/30 as 20 percent x 1.00/1.10/1.25/1.40/1.50) | 10 percent | 20 percent | 25 percent | 40 percent | 50 percent |
| Top speed-up after 8 steps (40 s), no global cap (owner 2026-11-12; was 6 steps and a 150 percent cap) | 80 percent | 160 percent | 200 percent | 320 percent | 400 percent |
| Crown bonus (same percents, doc 46 section 6a) | +10 percent | +20 percent | +25 percent | +40 percent | +50 percent |
| Medal target scale (doc 46 section 6) | x0.85 | x0.92 | x1.00 | x1.12 | x1.25 |

- It only changes the **size of the scroll speed-up steps**. The campaign speed ceiling `SimConfig.ScrollMaxSpeed` (4 u/s) stays as before, while the stepped speed-up itself tops out at step x 8 per setting (row above); start speed, layouts, goals, stars and gems are unchanged (gems are not scaled by Gameplay).
- Hard and Insane still count as **hard clears** (as the old Hard did).
- The choice is saved at once and applies to the next run.
- The difficulty scores of sections 2 and 3 do not depend on this setting: the score uses the scroll speed at the start of a level, which Gameplay does not change.

## 8. Heroes

Only the five primary hero lines (Momo, Capi, Otto, Bonbon, Bao) with their five tiers each are active: 25 heroes. All shops and rewards roll only active heroes. The rules for hero physics, the Forgiveness Index and the rosters stay in doc 44, the only source for them.

## 9. What is NOT migrated yet

- **Regions 6 to 30** still have 15 levels each and keep working unchanged. They are migrated later with the same rules (sections 1 to 6); until then doc 25 describes them.
- Star and reward tables of Regions 6 to 30 stay on the 15-level numbers until their migration.
- The speedrun goal is not specified yet.

## 10. Results

<!-- RESULTS START -->
Run of 2026-11-09 (Regions 1 to 5, 25 levels each, default hero Momo).

**1. Difficulty curve (`Tests/Simulation/DIFFICULTY_CURVE_REPORT.md`, 3 layouts per level).** The score formula reproduces the old Region 1 numbers exactly (5, 22, 40, 44, 39, 61, 66, 73, 75, 77, 86, 88, 93, 96, 100). The builder difficulty d of every level is solved per region and stored in `Content/config/difficulty_curve.json` (applied to the level files by `Tools/content_cli/migrate_levels_25.py`).

| Region | Largest error against the target score (points) |
|---|---|
| 1 Meadow Springs | 0.21 |
| 2 Mushroom Grove | 1.78 |
| 3 Sunny Beach | 1.03 |
| 4 Candy Canyon | 7.07 (levels 23 to 25 measure 91.9, 93.7 and 92.9 against 95, 97.5 and 100; its sticky platforms limit how hard it measures; accepted by the owner for now) |
| 5 Bamboo Temple | 0.41 |

Region 1 builder difficulty, levels 1 to 25: .05 .202 .233 .28 .49 .375 .398 .42 .491 .518 .55 .565 .574 .642 .645 .694 .701 .746 .772 .801 .815 .854 .897 .905 .937 (the special-goal levels 5, 10, 15 and 20 carry their own solved d).

**2. Goals and classes.** Level 5 combo chain, 10 no wall bounce, 15 hidden gem, 20 speedrun (90 rows, 75 s), 25 reach the top (highly complex, 5 guaranteed gems). Classes follow the score: simple under 30, medium under 55, hard under 75, complex from 75 (levels 15 to 24), highly complex at 25.

**3. Bot proof.** All 125 of 125 levels of Regions 1 to 5 are solved (`SolvabilityT4Tests`, `SOLVABILITY_REPORT.md`) and every random layout of all levels passes (`RandomLayoutTests`); the special levels also pass with 12 extra seeds each.

**4. Region goals and endless.** Region goals are 25 levels and 75 stars per region; tiers clear 5 / 10 / 15 / 20 / 25 levels with the same rewards, the last tier still gives the Common hero; star tiers 15 / 30 / 45 / 60 / 75 and three-star tiers 5 / 13 / 25. The endless tower has 25 stages of 100 floors (floors 1 to 2500), then a 1400-floor cycle (levels 15 to 24 for 100 floors each, level 25 held for 400 floors). Regions 6 to 30 keep the old 15-level rules until they are migrated.

**5. Economy after the move to 25 levels.** Perfect-play coins per region rise from about 3,750 to about 6,250. Gem income per region doubled (115 to 150 gems) and was brought back by scaling the gem drop chance by about 0.52 (permille per Gameplay mode from 27, 27, 29, 30, 32 to 14, 14, 15, 15, 15; 0.55 exactly overshoots the band in Region 3). Placed gem value per region on all 25 levels is now 61 to 70 on every mode (band 55 to 75, hard cap 91); the gem tier odds 70 / 22 / 7 / 1 percent are unchanged. The golden hashes (`Content/golden/level_hashes_r1-5.txt`) are regenerated and the curve scores did not move.

**6. Level sizes in rows (owner Bug List 8 item 3, section 4a).** Regions 1 to 5 now use 60 / 75 / 90 / 105 / 120 / 150 rows for levels 1 / 2 to 5 / 6 to 10 / 11 to 15 / 16 to 20 / 21 to 25 (speedrun level 20: 120 rows in 100 s). The builder difficulty d of all 125 levels was re-solved (`Content/config/difficulty_curve.json`, `migrate_levels_25.py`): largest score error per region R1 0.62, R2 1.78, R3 1.03, R4 7.07 (the accepted Candy Canyon cap), R5 0.84. Golden hashes regenerated. Gem income: the shorter levels carry fewer gem spots, so the drop shares went from 14 / 14 / 15 / 15 / 15 to 16 / 16 / 17 / 17 / 17 permille (placed value back inside 55 to 75 per region). Medal targets and the full bot proof of all heroes on the new rows are re-run separately (see the build report).
<!-- RESULTS END -->

## 11. Changelog

- 2026-11-09 (Bug List 8 item 3): level sizes fixed in rows (section 4a: 60 / 75 / 90 / 105 / 120 / 150), speedrun time 100 s for 120 rows, difficulty curve re-solved, golden hashes, medal targets and gem income re-checked; endless unchanged; Time Attack ramps from level 10 (section 6).
- 2026-11-09: document created from owner Bug List 6 (25 levels per region, the 0..100 difficulty score, the target curve, region goal tiers, the endless tower of 25 stages, the Gameplay setting, the active hero rule).
- 2026-11-12 (owner): Gameplay settings now speed up 10/20/25/40/50 percent per 5 s, top step x 8 (80/160/200/320/400 percent); the ceiling `ScrollMaxSpeed` 4 u/s still applies to the campaign; crown and medal target scale per setting (doc 46).
