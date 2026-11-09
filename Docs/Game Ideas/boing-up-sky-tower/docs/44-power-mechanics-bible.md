# 44. Power mechanics bible: hero physics, the Forgiveness Index and the rarity rules (2026-11-07)

**Replaces:** this bible replaces docs 29 (character stats), 34 section 2b (merge tier percent rules, the old "+10 to +25 percent chain") and the stat numbers in docs 02, 08 and 21 (`JumpSpeedFactor` 0.58 and the old hero stat lines) as the ONLY description of hero physics and tier rules. Doc 34 still owns merging itself (3 copies make the next tier), the Merge screen, hero powers in a run (section 2c) and the power pools; the run bonus and finds rules of doc 29 now live in section 7b of this bible.

**Source of truth: the code.** Where this file and the code differ, the code wins and this file is updated in the same change. The numbers come from `Assets/_Game/Simulation/HeroIndex.cs` (the engine: limits, bands, Ladder Final, the model, the seeded draw), `Simulation/CharacterStats.cs` (what a hero stores and how the sheet is derived), `Tests/Simulation/HeroFiHarness.cs` (the FI measurement) and `Tests/Simulation/HeroGeneratorTests.cs` (the generator and its gates).

**Status today (2026-11-07):** the engine, the harness and the generator exist. The constants file `HeroTable.cs` (section 6) and the roster of 125 heroes (section 8) are produced by the generator run that follows this document; until then the five Ladder Final rows are the only fixed heroes and the other lines still carry their old stats.

Units: `u` = world unit (player width 1, screen width 10). Speeds `u/s`, accelerations `u/s2`. The simulation runs at 60 ticks per second.

## 1. Purpose and the one-line rule

Every hero must feel different but no hero may make a level easier or harder to survive than the default hero does, except through its hero powers. Without a number to compare, "fair" is a guess, so one number decides it.

**The rule: a hero's physics are legal only when its measured Forgiveness Index matches the approved value of its rarity.** Jump is the only stat that grows by a percent band per rarity; Speed, Control and Wall Bounce are drawn at random inside fixed limits; the draw is accepted only if the measured FI passes the gates in section 4.

## 2. The Forgiveness Index (FI)

**What it measures.** On a level, a hero has some share of safe jump options: out of all the jumps a player could try from a platform, the share that lands alive on a higher row. FI is that share divided by the share Momo Common has on the same layouts. FI 1.000 = exactly as forgiving as Momo Common; 1.050 = 5 percent more of the jump options are safe; 0.950 = 5 percent fewer.

**The probe.** Test code (`HeroFi.OkLayout`) puts the hero on the main route platforms of a level layout, one platform per row, and lets the level bot (`LevelBot.MeasureOptions`) try the homing jump options: the next 3 rows, a range of back-off distances and a range of run-up lengths. An option counts as safe when the hero lands alive on a higher row. It is a probe only (no search), so it is fast.

| Part | Fast probe (generator, 75-level reference) | Full probe (first ladder tests, approved values) |
|---|---|---|
| Platforms probed | every 3rd row of the main route | every row |
| Clock offsets (moving platform phase) | 1 (tick 101) | 3 (ticks 0, 101, 233) |
| Cost | about 9 times cheaper | reference |
| Layouts per level | 24 for the final gate (8 and 4 for the two screens) | 24 |

Layouts are random layouts of the shipped levels (attempt seed base 300 plus the layout number); they do not depend on the hero, so each layout is built once and measured for every hero. Momo is always measured with the same probe, so FI stays comparable. The level 15 of Region 1 gate uses level `lvl_001_15`.

**How to read it.**

| FI | Meaning |
|---|---|
| 0.97 to 1.10 | The legal band for every tier (physics only). |
| about 1.00 | Plays like Momo Common on safety. |
| above 1.10 | Too forgiving: the levels trivialise. |
| below 0.97 | Too punishing: a hero the player pays for would be worse than the free one. |

The approved values of the five tiers are not equal (1.000 to 1.048), they are what Momo's tiers measured. A higher rarity may sit a little higher inside the band, never outside it.

## 3. Momo is the default hero: Ladder Final

Momo Common is the reference (FI 1.000 by definition) and the default hero (always owned, always selectable). His five tiers were measured and are fixed forever; every other hero is compared with the Momo row of its tier (`HeroIndex.LadderFinal`). Measured on level 15 of Region 1.

**The four stored numbers:**

| Tier | Launch (u/s) | Jump over 19 | Speed (u/s) | Control (u/s2) | Wall bounce (share kept) | FI |
|---|---|---|---|---|---|---|
| Common | 19.0 | 0.0% | 9 | 30 | 77 | 1.000 |
| Uncommon | 20.9 | 10.0% | 9 | 34 | 82 | 1.002 |
| Rare | 21.6 | 13.7% | 9 | 38 | 88 | 1.040 |
| Elite | 21.9 | 15.3% | 10 | 41 | 94 | 1.012 |
| Legendary | 22.8 | 20.0% | 11 | 41 | 100 | 1.048 |

Written as launch / speed / control / wall. Launch is stored in tenths of u/s (190 = 19.0), wall in hundredths (77 = 0.77).

**Derived values of each row** (never stored; formulas: gravity 40; jump height = launch squared / 80; air steering = 0.6 x control; braking = 1.5 x control; turn-around ground = 2 x control; turn-around air = 2 x air steering; time to stop = speed / braking; stop distance = speed squared / (2 x braking); time to top speed = speed / control; full-speed launch = launch + 0.41 x speed; half-speed jump = (launch + 0.41 x speed / 2) squared / 80; full-speed jump = full-speed launch squared / 80; sticky jump = (0.7 x launch) squared / 80; bounce launch = 1.4 x launch; bounce height = bounce launch squared / 80; wall bounce speed = speed x wall share):

| Value | Unit | Common | Uncommon | Rare | Elite | Legendary |
|---|---|---|---|---|---|---|
| Jump height (card) | u | 4.51 | 5.46 | 5.83 | 6.00 | 6.50 |
| Air steering | u/s2 | 18.0 | 20.4 | 22.8 | 24.6 | 24.6 |
| Braking | u/s2 | 45.0 | 51.0 | 57.0 | 61.5 | 61.5 |
| Turn-around, ground | u/s2 | 60 | 68 | 76 | 82 | 82 |
| Turn-around, air | u/s2 | 36.0 | 40.8 | 45.6 | 49.2 | 49.2 |
| Time to stop | s | 0.20 | 0.18 | 0.16 | 0.16 | 0.18 |
| Stop distance | u | 0.90 | 0.79 | 0.71 | 0.81 | 0.98 |
| Time to top speed | s | 0.30 | 0.26 | 0.24 | 0.24 | 0.27 |
| Full-speed launch | u/s | 22.69 | 24.59 | 25.29 | 26.00 | 27.31 |
| Half-speed jump | u | 5.43 | 6.47 | 6.87 | 7.17 | 7.85 |
| Full-speed jump | u | 6.44 | 7.56 | 7.99 | 8.45 | 9.32 |
| Sticky jump | u | 2.21 | 2.68 | 2.86 | 2.94 | 3.18 |
| Bounce launch | u/s | 26.60 | 29.26 | 30.24 | 30.66 | 31.92 |
| Bounce height | u | 8.84 | 10.70 | 11.43 | 11.75 | 12.74 |
| Wall bounce speed (card) | u/s | 6.93 | 7.38 | 7.92 | 9.40 | 11.00 |
| FI (level 15, Region 1) | | 1.000 | 1.002 | 1.040 | 1.012 | 1.048 |

Global values that do not change per hero (`SimConfig`): gravity 40 u/s2, `JumpSpeedFactor` 0.41 (the old 0.58 in docs 02, 08 and 09 is out of date), `AirControl` 0.6, `BrakeFactor` 1.5, turn-around factor 2, coyote 5 ticks, jump buffer 6 ticks, `BounceMultiplier` 1.4, `StickyJumpMultiplier` 0.7, `NormalGapMax` 4.2 u. Levels are sized from a separate layout reference jump (`LayoutJumpBase` 14 u/s), so a stronger hero never leaves the proven solvable envelope.

## 4. The rarity rules

| Rule | Common | Uncommon | Rare | Elite | Legendary |
|---|---|---|---|---|---|
| Jump band (percent of launch speed over the default 19.0, whole percent, both ends included) | 0 to 5 | 6 to 10 | 10 to 15 | 15 to 20 | 20 to 25 |
| Resulting launch range (u/s) | 19.0 to 19.9 | 20.2 to 20.9 | 20.9 to 21.8 | 21.9 to 22.8 | 22.8 to 23.7 |
| Wall bounce range (share kept, hundredths) | 69 to 85 | 72 to 90 | 76 to 94 | 80 to 97 | 85 to 100 |
| Approved FI | 1.000 | 1.002 | 1.040 | 1.012 | 1.048 |

The band edges touch (10, 15 and 20 percent belong to both neighbours; the tier decides which one applies).

**Limits that hold for every hero** (`HeroIndex`): Launch 19.0 to 23.7 u/s (25 percent of 19 is 23.75, so 23.7 is the highest legal launch; the band edges 10, 15 and 20 percent belong to both neighbouring tiers), Speed 8 to 12, Control 27 to 45, Wall bounce 69 to 100. Speed, Control and Wall bounce have no percent rule: any whole number inside the limits (and the wall range of the tier) is legal.

**The FI band:** every tier stays inside 0.97 to 1.10 (physics only).

**The gates** (a drawn hero is accepted only when all three hold, measured with the fast probe, 75 levels = 5 regions x 15 levels, 24 layouts each):

| Gate | Tolerance |
|---|---|
| Level 15 of Region 1 | FI within 0.02 of the approved FI of the tier (1.000 / 1.002 / 1.040 / 1.012 / 1.048) |
| 75-level mean | within 0.02 of Momo's mean FI of the same tier |
| Worst single level | no level more than 0.08 from Momo's FI of the same tier on that level |

## 5. What the measurements taught

All numbers are FI on level 15 of Region 1 unless stated.

| Finding | Measured | What it means for the rules |
|---|---|---|
| Jump is by far the strongest lever | Launch 17 gives FI 0.70, 19 gives 1.00, 21 gives 1.12, 23 gives 1.33 | Jump is the only stat that is percent-banded per rarity, and the bands are narrow (5 percent wide). |
| Control above 30 costs about 11 percent at once | Control 30 gives 1.00; 31 to 41 stays flat at about 0.895 | Any Control above 30 needs an offsetting Jump gain. |
| Control above 41 falls further | 42 gives 0.86, 45 gives 0.81 | Control 42 to 45 is legal but expensive. |
| Speed 10 or more costs 4 percent | Speed 8 gives 1.03, 9 gives 1.00, 10 gives 0.96, 11 and 12 about 0.96 | Fast heroes need Jump to compensate. |
| Speed 11 or 12 together with Control above 40 costs a further 5 to 10 percent | Speed 10 with Control 44 broke the floor at 0.956 | The model adds an interaction factor (0.95, or 0.90 for Control 43 or more with Speed 10 or more). |
| Wall bounce had no effect on level 15 | Changing wall bounce did not move the FI | Wall bounce is free to draw; the rarity only raises its floor. It still matters on other levels, so the 75-level gates check it. |
| The prediction model is not accurate enough | Error up to 0.10 against the measurement | The model is only a screen; the measured FI is the only gate. |
| Common heroes cannot have Control above 30 | Control above 30 costs about 11 percent and the Common Jump band is only 0 to 5 percent (about +4 percent FI at most) | High-Control identities are not possible at Common; they start at Uncommon, where the Jump band is wide enough to pay for them. |

## 6. How heroes are generated

**Principle.** Random per hero, then fixed forever: a hero's physics are drawn once by a tool, written down as constants and never random at run time.

1. **Seed.** Deterministic from the hero's line id, the tier and the attempt number (`HeroIndex.Draw`, FNV-1a hash of the line id mixed with the tier and the attempt; the same on every runtime). Same line, tier and attempt always give the same candidate.
2. **Draw inside the legal limits.** Launch inside the Jump band of the tier, Speed 8 to 12, Control 27 to 45, Wall bounce inside the wall range of the tier.
3. **Model screen.** The draw repeats (up to 4000 times) until the predicted FI is within 0.03 of the approved FI of the tier. This only keeps hopeless candidates away from the expensive measurements.
4. **Three measured stages** (`HeroGeneratorTests`, up to 40 attempts per hero):

| Stage | Measures | Passes when |
|---|---|---|
| 1 screen | level 15 of Region 1, 8 layouts | FI within 0.04 of the target of the tier |
| 2 screen | all 75 levels, 4 layouts | mean FI within 0.03 of Momo's mean of the tier |
| 3 final | all 75 levels, 24 layouts | the three gates of section 4 |

5. **Store.** The accepted physics (launch in tenths, speed, control, wall) go into the constants file `HeroTable.cs`, together with the measured FI at level 15, the 75-level mean and the worst level deviation. Nothing is random at run time. A hero that fails 40 attempts is reported as FAILED and needs a different seed (more attempts) or a decision.

Momo's five tiers are not drawn: they are the reference.

**Target note.** The approved values were measured with the full probe. The generator keeps them as the target on level 15 when the fast probe of Momo agrees within 0.015; otherwise the target is Momo's fast-probe value.

**Adding a new hero:**

1. Choose the line id (lowercase, one word) and add it to `TierRules.Lines`.
2. Add its power pool (four powers, ordered Uncommon to Legendary) to `TierRules.Pools`, themed to its region (rules in doc 34).
3. Add the character row (name, region, rarity names per tier) and the art folder `Resources/Characters/{id}/` for each of the five tiers (placeholder art is allowed; a missing picture must show a visible fallback).
4. Run the generator for that line only: set `HERO_GEN=1` and `HERO_LINES=<line id>`, run `HeroGeneratorTests.GenerateHeroes`. Results are appended to `Tests/Simulation/hero_gen/heroes.csv` (resumable; a line already in the file is skipped).
5. Check that all five tiers say OK. If one says FAILED, rerun with a higher `HERO_MAXATT`.
6. Copy the five accepted rows into `HeroTable.cs`.
7. Run the bot proof of the new heroes on all 75 levels (section 9) and the Simulation suite; update the roster table in section 8 and the changelog.

## 7. What is NOT in the index yet

| Part | Today | Plan |
|---|---|---|
| **Hero powers** (Double Jump, Wall Stick, Magnet, Shield, Rocket, Slow Time, Flight; doc 34 section 2c) | Not measured. FI headroom is reserved for them: the gap from the approved Ladder Final FI to the 1.10 ceiling is 0.060 for Rare (1.040) and 0.052 for Legendary (1.048) (an earlier note said about 0.10 and 0.04; that was wrong). Measured with the fast probe, Legendary on level 15 of Region 1 reads 1.100 and the 75-level mean is 1.096, so the real headroom there is close to zero and must be settled before powers are measured. | The next phase measures each power on the same probe and sets how much of the headroom it may use. |
| **Run bonuses and finds** (run bonus coins and gems, flat coin and gem finds) | Neutral: paid after the run, never seen by the simulation. | Stay outside the index. |

## 7b. Run bonus and finds (moved here from doc 29)

These are meta rewards. The simulation never sees them, so they are not part of the FI.

| Stat | What it does | Range |
|---|---|---|
| Coin find | Flat extra coins added to the payout of every cleared level, applied when the level result is saved | 0 to 50; 0 for most heroes (Nib, Coco, Cookie and Rusu carry one) |
| Gem find | Flat extra gems added to the reward of every cleared level | 0 to 5; 0 for every current hero |
| Run bonus coins | Paid at the end of every completed level run, on top of the level reward (`ProgressService.ApplyRun`) | Common hero: a multiple of 10 from 50 to 100. Each higher tier ADDS to the Common hero's own bonus: Uncommon 50 to 100, Rare 200 to 500, Elite 500 to 1000, Legendary 1000 to 2000 (multiples of 10) |
| Run bonus gems | Paid at the end of every completed level run | Common hero: 1 to 5. Tier add-on: Uncommon 1 to 5, Rare 5 to 10, Elite 5 to 10, Legendary 5 to 10 |

A failed run and an endless run pay no run bonus (it would reward failing on purpose); change that only on the owner's word. Momo's default run bonus is 80 coins and 2 gems. The tier add-ons are the existing `TierRules` ranges and are unchanged by the Forgiveness Index.

## 8. Roster of all 125 heroes (25 lines x 5 tiers)

<!-- HERO TABLE START -->
All 125 heroes (Momo's five tiers are fixed from Ladder Final; the other 120 were generated and measured on 2026-11-07 with the fast probe). FI level 15 = Region 1 level 15; FI mean = mean over all 75 levels; largest level gap = the biggest distance from Momo's FI of the same tier on any single level.

| Region | Hero | Rarity | Launch | Jump over default | Speed | Control | Wall | FI level 15 | FI mean (75 levels) | Largest level gap |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Momo | Common | 19.0 | +0.0% | 9 | 30 | 77 | 1.000 | 1.000 | reference |
| 1 | Cloudguard Momo | Uncommon | 20.9 | +10.0% | 9 | 34 | 82 | 0.987 | 1.010 | reference |
| 1 | Cloudknight Momo | Rare | 21.6 | +13.7% | 9 | 38 | 88 | 1.018 | 1.051 | reference |
| 1 | Skyplate Momo | Elite | 21.9 | +15.3% | 10 | 41 | 94 | 1.022 | 1.049 | reference |
| 1 | Sunlord Momo | Legendary | 22.8 | +20.0% | 11 | 41 | 100 | 1.100 | 1.096 | reference |
| 1 | Bumble | Common | 19.4 | +2.1% | 9 | 27 | 73 | 1.015 | 1.012 | 0.022 |
| 1 | Honeycloak Bumble | Uncommon | 20.7 | +8.9% | 9 | 35 | 83 | 0.969 | 1.001 | 0.024 |
| 1 | Hivemage Bumble | Rare | 21.6 | +13.7% | 9 | 39 | 78 | 1.021 | 1.052 | 0.008 |
| 1 | Combweaver Bumble | Elite | 21.9 | +15.3% | 12 | 39 | 94 | 1.033 | 1.037 | 0.039 |
| 1 | Archmage Bumble | Legendary | 22.8 | +20.0% | 11 | 41 | 95 | 1.099 | 1.095 | 0.003 |
| 1 | Ribbi | Common | 19.4 | +2.1% | 10 | 30 | 85 | 1.003 | 0.981 | 0.047 |
| 1 | Leafcrown Ribbi | Uncommon | 20.8 | +9.5% | 9 | 35 | 84 | 0.971 | 1.005 | 0.017 |
| 1 | Barkplate Ribbi | Rare | 21.5 | +13.2% | 9 | 32 | 78 | 1.014 | 1.037 | 0.036 |
| 1 | Mossknight Ribbi | Elite | 22.0 | +15.8% | 11 | 40 | 92 | 1.041 | 1.050 | 0.025 |
| 1 | Jadecrown Ribbi | Legendary | 22.8 | +20.0% | 11 | 42 | 86 | 1.100 | 1.097 | 0.008 |
| 1 | Dandy | Common | 19.1 | +0.5% | 9 | 27 | 84 | 0.998 | 1.002 | 0.013 |
| 1 | Seedling Dandy | Uncommon | 20.8 | +9.5% | 9 | 33 | 75 | 0.968 | 0.996 | 0.035 |
| 1 | Blossom Dandy | Rare | 21.2 | +11.6% | 8 | 38 | 80 | 1.001 | 1.032 | 0.042 |
| 1 | Petalcloak Dandy | Elite | 21.9 | +15.3% | 12 | 39 | 83 | 1.037 | 1.036 | 0.041 |
| 1 | Bloomqueen Dandy | Legendary | 22.8 | +20.0% | 11 | 42 | 86 | 1.100 | 1.097 | 0.008 |
| 1 | Pip | Common | 19.2 | +1.1% | 12 | 27 | 72 | 1.005 | 1.005 | 0.022 |
| 1 | Redcape Pip | Uncommon | 20.8 | +9.5% | 9 | 35 | 76 | 0.972 | 1.005 | 0.017 |
| 1 | Crownguard Pip | Rare | 21.7 | +14.2% | 8 | 42 | 94 | 1.015 | 1.063 | 0.033 |
| 1 | Thornplate Pip | Elite | 21.9 | +15.3% | 9 | 42 | 83 | 1.033 | 1.065 | 0.031 |
| 1 | Goldshell Pip | Legendary | 22.8 | +20.0% | 11 | 42 | 86 | 1.100 | 1.097 | 0.008 |
| 2 | Capi | Common | 19.7 | +3.7% | 10 | 30 | 84 | 1.019 | 0.992 | 0.036 |
| 2 | Thornveil Capi | Uncommon | 20.9 | +10.0% | 9 | 31 | 86 | 0.970 | 0.999 | 0.030 |
| 2 | Acornplate Capi | Rare | 21.6 | +13.7% | 9 | 38 | 76 | 1.020 | 1.051 | 0.006 |
| 2 | Oakmantle Capi | Elite | 22.0 | +15.8% | 10 | 38 | 92 | 1.041 | 1.064 | 0.034 |
| 2 | Grovequeen Capi | Legendary | 22.8 | +20.0% | 10 | 42 | 87 | 1.105 | 1.103 | 0.028 |
| 2 | Nib | Common | 19.7 | +3.7% | 12 | 28 | 71 | 1.017 | 0.990 | 0.037 |
| 2 | Leafscarf Nib | Uncommon | 20.8 | +9.5% | 9 | 36 | 80 | 0.971 | 1.005 | 0.021 |
| 2 | Barkguard Nib | Rare | 21.4 | +12.6% | 9 | 35 | 76 | 1.002 | 1.036 | 0.036 |
| 2 | Oakknight Nib | Elite | 21.9 | +15.3% | 10 | 38 | 84 | 1.041 | 1.059 | 0.025 |
| 2 | Acornking Nib | Legendary | 22.8 | +20.0% | 12 | 42 | 99 | 1.099 | 1.089 | 0.028 |
| 2 | Spike | Common | 19.6 | +3.2% | 11 | 30 | 69 | 1.018 | 0.988 | 0.033 |
| 2 | Logplate Spike | Uncommon | 20.9 | +10.0% | 9 | 37 | 81 | 0.972 | 1.009 | 0.015 |
| 2 | Thornguard Spike | Rare | 21.5 | +13.2% | 9 | 41 | 87 | 1.005 | 1.038 | 0.033 |
| 2 | Oakbrace Spike | Elite | 21.9 | +15.3% | 10 | 38 | 90 | 1.035 | 1.058 | 0.023 |
| 2 | Quillking Spike | Legendary | 22.8 | +20.0% | 11 | 42 | 86 | 1.100 | 1.097 | 0.008 |
| 2 | Moss | Common | 19.2 | +1.1% | 12 | 27 | 70 | 1.005 | 1.005 | 0.021 |
| 2 | Vinebelt Moss | Uncommon | 20.9 | +10.0% | 9 | 37 | 84 | 0.972 | 1.010 | 0.015 |
| 2 | Shellplate Moss | Rare | 21.8 | +14.7% | 9 | 39 | 89 | 1.030 | 1.063 | 0.020 |
| 2 | Ferncrown Moss | Elite | 21.9 | +15.3% | 12 | 41 | 87 | 1.028 | 1.038 | 0.041 |
| 2 | Shellking Moss | Legendary | 22.8 | +20.0% | 11 | 41 | 93 | 1.102 | 1.096 | 0.003 |
| 2 | Fenn | Common | 19.6 | +3.2% | 10 | 29 | 74 | 1.017 | 0.988 | 0.043 |
| 2 | Scoutvest Fenn | Uncommon | 20.9 | +10.0% | 9 | 37 | 74 | 0.968 | 1.009 | 0.020 |
| 2 | Coinguard Fenn | Rare | 21.5 | +13.2% | 9 | 39 | 78 | 1.016 | 1.047 | 0.013 |
| 2 | Goldcape Fenn | Elite | 22.0 | +15.8% | 11 | 39 | 95 | 1.040 | 1.049 | 0.024 |
| 2 | Foxking Fenn | Legendary | 22.8 | +20.0% | 11 | 42 | 95 | 1.098 | 1.096 | 0.005 |
| 3 | Otto | Common | 19.6 | +3.2% | 10 | 29 | 75 | 1.017 | 0.988 | 0.041 |
| 3 | Tidecrest Otto | Uncommon | 20.7 | +8.9% | 9 | 35 | 77 | 0.969 | 1.001 | 0.024 |
| 3 | Shellplate Otto | Rare | 21.4 | +12.6% | 9 | 35 | 89 | 1.001 | 1.036 | 0.038 |
| 3 | Wavecloak Otto | Elite | 21.9 | +15.3% | 10 | 37 | 89 | 1.038 | 1.057 | 0.022 |
| 3 | Pearlking Otto | Legendary | 22.8 | +20.0% | 11 | 42 | 89 | 1.100 | 1.096 | 0.005 |
| 3 | Pinch | Common | 19.0 | +0.0% | 9 | 28 | 81 | 0.995 | 0.997 | 0.013 |
| 3 | Shellvest Pinch | Uncommon | 20.9 | +10.0% | 9 | 31 | 72 | 0.968 | 0.998 | 0.033 |
| 3 | Clawguard Pinch | Rare | 21.4 | +12.6% | 9 | 41 | 93 | 1.003 | 1.033 | 0.037 |
| 3 | Tidecrown Pinch | Elite | 22.0 | +15.8% | 10 | 42 | 89 | 1.040 | 1.057 | 0.020 |
| 3 | Reefking Pinch | Legendary | 22.8 | +20.0% | 12 | 42 | 88 | 1.097 | 1.088 | 0.029 |
| 3 | Shelly | Common | 19.2 | +1.1% | 9 | 27 | 85 | 1.006 | 1.006 | 0.016 |
| 3 | Coralstrap Shelly | Uncommon | 20.8 | +9.5% | 9 | 36 | 85 | 0.971 | 1.005 | 0.020 |
| 3 | Reefplate Shelly | Rare | 21.3 | +12.1% | 8 | 37 | 91 | 1.010 | 1.036 | 0.039 |
| 3 | Pearltiara Shelly | Elite | 21.9 | +15.3% | 10 | 38 | 87 | 1.036 | 1.058 | 0.022 |
| 3 | Coralqueen Shelly | Legendary | 22.8 | +20.0% | 12 | 41 | 88 | 1.101 | 1.088 | 0.026 |
| 3 | Bloop | Common | 19.0 | +0.0% | 10 | 27 | 72 | 0.997 | 0.998 | 0.012 |
| 3 | Coralband Bloop | Uncommon | 20.8 | +9.5% | 9 | 33 | 78 | 0.967 | 0.996 | 0.035 |
| 3 | Reefmail Bloop | Rare | 21.4 | +12.6% | 8 | 42 | 85 | 0.998 | 1.045 | 0.034 |
| 3 | Bubblecrown Bloop | Elite | 21.9 | +15.3% | 10 | 39 | 87 | 1.037 | 1.060 | 0.024 |
| 3 | Bubbleking Bloop | Legendary | 22.8 | +20.0% | 11 | 41 | 95 | 1.099 | 1.095 | 0.003 |
| 3 | Coco | Common | 19.7 | +3.7% | 12 | 29 | 75 | 1.019 | 0.991 | 0.035 |
| 3 | Palmwrap Coco | Uncommon | 20.8 | +9.5% | 9 | 35 | 76 | 0.972 | 1.005 | 0.017 |
| 3 | Leafmail Coco | Rare | 21.4 | +12.6% | 9 | 37 | 82 | 1.019 | 1.043 | 0.019 |
| 3 | Junglecape Coco | Elite | 22.0 | +15.8% | 12 | 41 | 89 | 1.041 | 1.044 | 0.036 |
| 3 | Palmking Coco | Legendary | 22.8 | +20.0% | 12 | 42 | 88 | 1.097 | 1.088 | 0.029 |
| 4 | Bonbon | Common | 19.2 | +1.1% | 9 | 29 | 71 | 1.011 | 1.006 | 0.011 |
| 4 | Sugarplate Bonbon | Uncommon | 20.8 | +9.5% | 9 | 32 | 88 | 0.967 | 0.996 | 0.033 |
| 4 | Caramelplate Bonbon | Rare | 21.4 | +12.6% | 9 | 32 | 81 | 1.012 | 1.032 | 0.044 |
| 4 | Sprinklecloak Bonbon | Elite | 21.9 | +15.3% | 9 | 42 | 86 | 1.033 | 1.065 | 0.033 |
| 4 | Sugarqueen Bonbon | Legendary | 22.8 | +20.0% | 12 | 41 | 91 | 1.099 | 1.088 | 0.026 |
| 4 | Gummi | Common | 19.0 | +0.0% | 12 | 27 | 70 | 0.992 | 0.997 | 0.021 |
| 4 | Starsash Gummi | Uncommon | 20.9 | +10.0% | 9 | 36 | 84 | 0.977 | 1.010 | 0.013 |
| 4 | Gemguard Gummi | Rare | 21.5 | +13.2% | 9 | 35 | 84 | 1.025 | 1.048 | 0.022 |
| 4 | Heartplate Gummi | Elite | 22.0 | +15.8% | 11 | 40 | 92 | 1.041 | 1.050 | 0.025 |
| 4 | Gummyking Gummi | Legendary | 22.8 | +20.0% | 12 | 42 | 91 | 1.098 | 1.088 | 0.028 |
| 4 | Choco | Common | 19.7 | +3.7% | 11 | 30 | 85 | 1.020 | 0.992 | 0.032 |
| 4 | Wafflewrap Choco | Uncommon | 20.9 | +10.0% | 9 | 33 | 81 | 0.970 | 1.002 | 0.028 |
| 4 | Fudgeplate Choco | Rare | 21.5 | +13.2% | 8 | 42 | 89 | 1.002 | 1.050 | 0.028 |
| 4 | Cocoacape Choco | Elite | 22.0 | +15.8% | 12 | 40 | 92 | 1.041 | 1.043 | 0.036 |
| 4 | Chocoking Choco | Legendary | 22.8 | +20.0% | 12 | 41 | 94 | 1.099 | 1.088 | 0.026 |
| 4 | Marsh | Common | 19.4 | +2.1% | 10 | 29 | 70 | 1.006 | 0.980 | 0.045 |
| 4 | Grahambelt Marsh | Uncommon | 20.8 | +9.5% | 9 | 35 | 90 | 0.971 | 1.005 | 0.018 |
| 4 | Toastplate Marsh | Rare | 21.5 | +13.2% | 9 | 38 | 76 | 1.015 | 1.046 | 0.012 |
| 4 | Crackerplate Marsh | Elite | 21.9 | +15.3% | 12 | 39 | 83 | 1.037 | 1.036 | 0.041 |
| 4 | Roastking Marsh | Legendary | 22.8 | +20.0% | 11 | 42 | 97 | 1.098 | 1.096 | 0.004 |
| 4 | Cookie | Common | 19.0 | +0.0% | 12 | 27 | 78 | 0.991 | 0.997 | 0.022 |
| 4 | Crumbscarf Cookie | Uncommon | 20.9 | +10.0% | 9 | 38 | 84 | 0.970 | 1.009 | 0.028 |
| 4 | Chipguard Cookie | Rare | 21.5 | +13.2% | 9 | 33 | 87 | 1.019 | 1.040 | 0.033 |
| 4 | Doughcape Cookie | Elite | 21.9 | +15.3% | 10 | 40 | 83 | 1.039 | 1.061 | 0.026 |
| 4 | Bakeking Cookie | Legendary | 22.8 | +20.0% | 12 | 42 | 95 | 1.099 | 1.088 | 0.028 |
| 5 | Bao | Common | 19.6 | +3.2% | 11 | 29 | 85 | 1.013 | 0.988 | 0.035 |
| 5 | Ironbamboo Bao | Uncommon | 20.8 | +9.5% | 9 | 34 | 89 | 0.967 | 0.999 | 0.032 |
| 5 | Lotusrobe Bao | Rare | 21.4 | +12.6% | 9 | 37 | 76 | 1.015 | 1.042 | 0.022 |
| 5 | Dragonsash Bao | Elite | 21.9 | +15.3% | 9 | 43 | 81 | 1.034 | 1.067 | 0.034 |
| 5 | Sunmonk Bao | Legendary | 22.8 | +20.0% | 11 | 42 | 89 | 1.100 | 1.096 | 0.005 |
| 5 | Sprout | Common | 19.5 | +2.6% | 10 | 30 | 76 | 1.015 | 0.986 | 0.042 |
| 5 | Leafwrap Sprout | Uncommon | 20.9 | +10.0% | 9 | 37 | 73 | 0.968 | 1.009 | 0.019 |
| 5 | Bamboomail Sprout | Rare | 21.6 | +13.7% | 9 | 31 | 77 | 1.017 | 1.040 | 0.033 |
| 5 | Jadecloak Sprout | Elite | 22.1 | +16.3% | 10 | 42 | 91 | 1.042 | 1.062 | 0.025 |
| 5 | Groveking Sprout | Legendary | 22.8 | +20.0% | 12 | 42 | 98 | 1.099 | 1.089 | 0.029 |
| 5 | Tora | Common | 19.6 | +3.2% | 12 | 29 | 80 | 1.015 | 0.988 | 0.037 |
| 5 | Stripeband Tora | Uncommon | 20.9 | +10.0% | 9 | 34 | 75 | 0.988 | 1.010 | 0.001 |
| 5 | Clawguard Tora | Rare | 21.4 | +12.6% | 9 | 33 | 84 | 1.016 | 1.035 | 0.039 |
| 5 | Tigercape Tora | Elite | 22.0 | +15.8% | 12 | 41 | 84 | 1.042 | 1.044 | 0.037 |
| 5 | Tigerlord Tora | Legendary | 22.8 | +20.0% | 12 | 42 | 92 | 1.098 | 1.088 | 0.028 |
| 5 | Rusu | Common | 19.4 | +2.1% | 11 | 29 | 79 | 1.005 | 0.981 | 0.043 |
| 5 | Scoutwrap Rusu | Uncommon | 20.9 | +10.0% | 9 | 33 | 83 | 0.970 | 1.002 | 0.028 |
| 5 | Goldfur Rusu | Rare | 21.4 | +12.6% | 8 | 39 | 87 | 1.007 | 1.044 | 0.029 |
| 5 | Coinmantle Rusu | Elite | 22.0 | +15.8% | 12 | 40 | 93 | 1.041 | 1.043 | 0.036 |
| 5 | Treasurelord Rusu | Legendary | 22.8 | +20.0% | 12 | 41 | 97 | 1.100 | 1.088 | 0.026 |
| 5 | Ryu | Common | 19.1 | +0.5% | 9 | 28 | 73 | 1.004 | 1.002 | 0.007 |
| 5 | Scalevest Ryu | Uncommon | 20.9 | +10.0% | 9 | 33 | 87 | 0.970 | 1.002 | 0.028 |
| 5 | Jadeguard Ryu | Rare | 21.7 | +14.2% | 9 | 38 | 88 | 1.028 | 1.058 | 0.016 |
| 5 | Dragonplate Ryu | Elite | 21.9 | +15.3% | 12 | 40 | 88 | 1.034 | 1.037 | 0.041 |
| 5 | Skylord Ryu | Legendary | 22.8 | +20.0% | 12 | 42 | 93 | 1.098 | 1.088 | 0.028 |
<!-- HERO TABLE END -->

## 9. Test plan

1. **Bot proof of every hero.** Every hero of the roster (125) is played by the level bot on all 75 levels (5 regions x 15 levels) and must clear each one (the existing solvability proof, run with the hero's physics instead of the baseline).
2. **FI measurement.** Every hero is measured with the fast probe, 24 layouts per level, on all 75 levels: FI on level 15 of Region 1, the 75-level mean, the worst level deviation from Momo's tier, and the FI band 0.97 to 1.10.
3. **Regression.** Golden hashes and bot proofs of the baseline Momo stay unchanged; the Ladder Final rows in `HeroIndex` and in the harness stay identical.
4. Fast probe run commands: `HERO_REF=1` builds the Momo reference (`Tests/Simulation/FI_REFERENCE_FAST.csv`); `HERO_GEN=1` runs the generator.

<!-- RESULTS START -->
Run of 2026-11-07 to 2026-11-08.

**1. Generation (FI gate).** 120 heroes (24 lines x 5 tiers; Momo's five tiers are fixed) were generated and verified: 120 accepted, 0 failed, with 892 logged attempts after the screening fix (`Tests/Simulation/hero_gen/attempts.csv`). Per tier:

| Tier | Heroes | Attempts min / median / max | FI level 15 range | FI 75-level mean range | Largest level gap (max) |
|---|---|---|---|---|---|
| Common | 24 | 1 / 3 / 16 | 0.991 to 1.020 | 0.980 to 1.012 | 0.047 |
| Uncommon | 24 | 1 / 9 / 58 | 0.967 to 0.988 | 0.996 to 1.010 | 0.035 |
| Rare | 24 | 1 / 2 / 5 | 0.998 to 1.030 | 1.032 to 1.063 | 0.044 |
| Elite | 24 | 1 / 4 / 39 | 1.028 to 1.042 | 1.036 to 1.067 | 0.041 |
| Legendary | 24 | 1 / 4 / 21 | 1.097 to 1.105 | 1.088 to 1.103 | 0.029 |

The level 15 target is Momo's own value of the tier measured with the same fast probe (1.000, 0.987, 1.018, 1.022, 1.100), because the approved values (1.000, 1.002, 1.040, 1.012, 1.048) came from the slower full probe. The FI ceiling 1.10 is enforced with 0.005 of slack, so Legendary heroes sit at about 1.10 (Momo's own Legendary reads 1.100 on level 15).

**2. Bot proof of every hero on every level.** 125 heroes x 75 levels (5 regions x 15) = 9,375 runs: 9,375 solved, 0 failed (`Tests/Simulation/HERO_BOT_REPORT.csv`, `HERO_BOT_SUMMARY.md`). The first pass used a 3,000-node budget per solve and left 21 runs unsolved, all Common heroes (including Momo) on Region 5 level 10, a complex combo-chain level that needs about 7,500 nodes even for the default hero (SOLVABILITY_REPORT.md); those 21 were rerun with the full budget and all solved (`HERO_BOT_RERUN_SUMMARY.md`).

**3. Active heroes (owner Bug List 6, 2026-11-09).** Only five lines are ACTIVE: Momo, Capi (forestgirl), Otto (beachotter), Bonbon (candygirl) and Bao (monk), five tiers each = **25 active heroes**. The other 20 lines (100 heroes) are inactive ALWAYS, a permanent compile-time rule with no setting: their generated rows stay in `HeroTable` as data (all 125 rows, `Active` flag, `CharacterDefs.AllIncludingInactive` for tools and the bot proof) but they never exist at runtime. They are not in `CharacterDefs.All`, so no pool, shop, chest, bundle, reward, region goal, All Heroes screen or save ever sees them, and an old save naming one falls back to Momo. Region goals that used to name an extra hero (and the top weekly task) now give one random ACTIVE Common hero of an open region. The generation and bot proof above cover all 125 rows; the runtime roster is the 25.

**4. Unit tests (counts of 2026-11-09).** Unity EditMode **605 tests** and PlayMode **77 tests**, plus the dotnet Simulation suite (tier rules, character stats, golden hashes, hero cards, inventory, bundles, chests, hero powers, the active hero rule). The Unity suites were edited but not run in the editor since the last additions.
<!-- RESULTS END -->

## 10. Changelog

- 2026-11-07: document created from `HeroIndex.cs`, `CharacterStats.cs`, `HeroFiHarness.cs` and `HeroGeneratorTests.cs`. Replaces docs 29 and 34 section 2b for hero physics and tier rules (the old +10 to +25 percent chain is retired). Ladder Final (Momo, five tiers) fixed; HeroTable and the roster table follow the generator run.
