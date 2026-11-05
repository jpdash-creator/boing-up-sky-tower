# 29. Character stats: data model and gameplay behaviour (2026-11-01)

**Source of truth: the code.** Where this file and the code differ, the code wins and this file is updated in the same change. This file is the living reference for how a character's stats are stored and how they change play. It does not copy numbers from older docs (doc 02 and doc 08 still list `JumpSpeedFactor` 0.58; the code has 0.41 and this file follows the code).

Code: `Assets/_Game/Simulation/CharacterStats.cs` (data, validation, derived sheet), `Simulation/SimConfig.cs` (global physics), `Economy/Characters.cs` (`CharacterSpec.Stats`), `Gameplay/GameSession.BuildRunCfg` (applies the chosen character to a run), `Economy/ProgressService.ApplyRun` (finds). Tests: `Tests/Simulation/CharacterStatsTests.cs`.

Units: `u` = world unit (player width 1, screen width 10). Speeds `u/s`, accelerations `u/s2`. The sim runs at 60 ticks per second. No percent is stored or shown; bonuses are plain numbers.

## 1. Four tiers

| Tier | What it is | Edited per character? |
|---|---|---|
| Identity | id, name, region, rarity, price, art, powers (`CharacterSpec`) | Yes |
| Main stats | the six numbers shown on the character card | Yes (stored: 6 fields) |
| Derived | calculated from main stats and global values, never stored | No |
| Global | shared physics and rules, the same for every character | No |

## 2. Main stats (stored per character: `CharacterStats`)

| Field | Card label | Momo (baseline) | Unit | Meaning |
|---|---|---|---|---|
| `JumpLaunch` | Jump (shown as the height it gives) | 19 | u/s | Upward launch speed of a standing jump. Card value is derived: launch² ÷ (2 × gravity) = 4.5 u |
| `MaxSpeed` | Speed | 9 | u/s | Top sideways speed |
| `Control` | Control | 30 | u/s2 | Ground acceleration. Air steering, braking and turn-around follow it (section 3) |
| `WallBoostHundredths` | Wall Bounce (shown as the speed after a bounce at top speed) | 77 (= 0.77) | hundredths | Share of horizontal speed kept after a wall bounce. Card value is derived: Speed × 0.77 = 6.9 u/s |
| `CoinFind` | Coins | 0 | coins | Flat extra coins added to the payout of every cleared level |
| `GemFind` | Gems | 0 | gems | Flat extra gems added to the reward of every cleared level |
| `RunBonusCoins` | Run bonus (coin icon) | 80 | coins | Paid at the end of every completed run, on top of the level reward (a multiple of 10, 50 to 100 for roster heroes; 0 on the bare default) |
| `RunBonusGems` | Run bonus (gem icon) | 2 | gems | Paid at the end of every completed run (1 to 5 for roster heroes; 0 on the bare default) |

Card (six lines, plus the rarity): Jump 4.5, Speed 9, Control 30, Wall Bounce 6.9, Coins +0, Gems +0.

## 3. Derived values (calculated, Momo shown)

| Value | Formula | Momo |
|---|---|---|
| Jump height (card) | `JumpLaunch`² ÷ (2 × `Gravity`) | 4.5 u |
| Wall Bounce (card) | `MaxSpeed` × wall boost | 6.9 u/s |
| Air steering | `Control` × `AirControl` (0.6) | 18 u/s2 |
| Braking | `Control` × `BrakeFactor` (1.5) | 45 u/s2 |
| Turn-around, ground | `Control` × 2 (pushing the opposite way) | 60 u/s2 |
| Turn-around, air | air steering × 2 | 36 u/s2 |
| Time to stop from top speed | `MaxSpeed` ÷ braking | 0.2 s |
| Stopping distance | `MaxSpeed`² ÷ (2 × braking) | 0.9 u |
| Time to top speed | `MaxSpeed` ÷ `Control` | 0.3 s |
| Full-speed launch | `JumpLaunch` + `JumpSpeedFactor` (0.41) × `MaxSpeed` | 22.69 u/s |
| Half-speed jump height | (`JumpLaunch` + 0.41 × `MaxSpeed` ÷ 2)² ÷ (2 × `Gravity`) | 5.43 u |
| Full-speed jump height | full-speed launch² ÷ (2 × `Gravity`) | 6.44 u (1.85 median row gaps) |
| Sticky-platform jump height | (`JumpLaunch` × 0.7)² ÷ (2 × `Gravity`) | 2.21 u |
| Normal bounce launch and height | `JumpLaunch` × `BounceMultiplier` (1.4), then ² ÷ (2 × `Gravity`) | 26.6 u/s, 8.8 u |

## 4. Global values (`SimConfig`, same for every character)

| Field | Value | Meaning |
|---|---|---|
| `Gravity` | 40 u/s2 | Fall pull; with launch speed it sets jump height |
| `JumpSpeedFactor` | 0.41 | Extra launch speed per unit of sideways speed |
| `AirControl` | 0.6 | Share of Control available in the air |
| `BrakeFactor` | 1.5 | Braking = Control × this (replaced the old fixed `Friction` 45; Momo is unchanged) |
| turn-around factor | 2 (fixed in `Sim.cs`) | Pushing against your motion accelerates twice as hard |
| `CoyoteTicks` | 5 (80 ms) | Late-jump window: a jump just after leaving a ledge still works |
| `JumpBufferTicks` | 6 (100 ms) | Early-jump window: a tap just before landing is remembered |
| `BounceMultiplier` | 1.4 | Bounce platform launch |
| `StickyJumpMultiplier` | 0.7 | Launch on sticky platforms |
| `SlipperyFriction`, `SlipperyAccel` | 0.15, 0.6 | Multiplies braking and Control on ice |
| `NormalGapMax` | 4.2 u | Largest normal vertical gap; a standing jump (4.51 u) always reaches the next platform |
| `LayoutJumpBase`, `LayoutJumpSpeedFactor` | 14 u/s, 0.85 | The reference jump the level layouts are sized from (independent of a character's `JumpLaunch`) |

## 5. How the stats change play

- **Jump and gravity.** Height = launch² ÷ (2 × gravity). Both the standing and the moving jump scale with `JumpLaunch`. Platform and zone effects stack on the character's number at run time (sticky 0.7 launch, bounce 1.4 launch); they never change the stored stat.
- **Control, braking, turn-around.** One number drives starting, steering in the air, stopping and reversing. Braking applies only on the ground with no direction held; in the air with no input nothing slows you. Ice multiplies braking by 0.15 and Control by 0.6.
- **Speed and wall bounce.** `MaxSpeed` caps sideways speed and enters the moving jump. A wall bounce keeps `WallBoost` of the speed (0.77 for Momo, so bounces lose speed).
- **Forgiveness windows** (late and early jump) are global and are not character stats. A jump uses up both windows, so a wider window never gives a double jump (double jump is a separate power).
- **Finds.** `CoinFind` and `GemFind` are flat per cleared level, applied when the level result is saved. They are 0 for every current character.
- **Run bonus.** `RunBonusCoins` and `RunBonusGems` (owner 2026-11-02) are paid by `ProgressService.ApplyRun` when a level run is completed, added to the payout and gem reward of that result. Meta reward only: the simulation never sees it. Every roster hero has its own distinct pair. A failed run and an endless run pay no bonus (it would reward failing on purpose); change that only on the owner's word.
- **Levels.** Layouts are sized from the layout reference jump, so a stronger character stays inside the proven solvable envelope. Bot proofs and golden hashes run on the baseline.

## 6. Roster (current code)

Momo keeps the baseline. Capi, Otto, Bonbon and Bao were drawn ONCE (2026-11-02, owner: random once, then fixed forever) with a fixed-seed script (Python `random.Random(20261102)`, one uniform draw in -10..+10 percent per stat and hero, order Jump, Speed, Control, Wall Bounce, rounded to the whole numbers the stats are stored in; then 5 distinct run-bonus coin values from {50..100 step 10} and a shuffle of gems 1..5). The numbers are plain constants in `CharacterDefs`, nothing is random at run time. Because the stats are whole numbers, Speed moves in steps of 1 (about 11 percent), so the rounded value can sit just past the drawn percent.

Stored values (what the code holds):

| Id | Name | Region | Rarity | JumpLaunch | MaxSpeed | Control | WallBoost | Run bonus coins | Run bonus gems |
|---|---|---|---|---|---|---|---|---|---|
| momo | Momo | 1 | Common | 19 | 9 | 30 | 77 | 80 | 2 |
| forestgirl | Capi | 2 | Common | 19 | 10 | 29 | 79 | 90 | 1 |
| beachotter | Otto | 3 | Common | 18 | 8 | 32 | 72 | 100 | 3 |
| candygirl | Bonbon | 4 | Common | 21 | 10 | 31 | 84 | 70 | 4 |
| monk | Bao | 5 | Common | 20 | 10 | 31 | 80 | 60 | 5 |

Hero sheet values (what the player sees; Jump = launch squared / 80, Wall Bounce = Speed x wall share):

| Id | Name | Jump | Speed | Control | Wall Bounce | Coins | Gems | Run bonus |
|---|---|---|---|---|---|---|---|---|
| momo | Momo | 4.5 | 9 | 30 | 6.9 | +0 | +0 | +80 coins, +2 gems |
| forestgirl | Capi | 4.5 | 10 | 29 | 7.9 | +0 | +0 | +90 coins, +1 gem |
| beachotter | Otto | 4 | 8 | 32 | 5.8 | +0 | +0 | +100 coins, +3 gems |
| candygirl | Bonbon | 5.5 | 10 | 31 | 8.4 | +0 | +0 | +70 coins, +4 gems |
| monk | Bao | 5 | 10 | 31 | 8 | +0 | +0 | +60 coins, +5 gems |

(Otto's Jump shows 4 because 18 x 18 / 80 = 4.05.) Common region heroes carry no power (doc 21). Powers of Uncommon and above stay in `CharacterSpec.Powers` and are not stat fields.

## 6b. Uncommon merge tiers (docs/34 section 2b, redone 2026-11-03)

The five Uncommon heroes are made by merging 3 copies of their Common hero. Rule (owner): exactly 2 of {Jump, Speed, Control, Wall Bounce} get +10 to +25 percent over the Common hero, measured on the numbers the hero sheet SHOWS (one decimal); run bonus coins +50 to +100 (steps of 10) and gems +1 to +5 added to the Common hero's own bonus; 1 hero power tied to the hero's region. Rare, Elite and Legendary continue the chain (+10 to +25 percent over the previous tier, 3/3/4 stats; rules in `TierRules`, docs/34 section 2b) but have no characters yet. Rows are constants in `MergeTierDefs.Rows`, drawn once with `TierRules.Generate` (seed 20261104 + line index 0..4).

Shown values (hero sheet), Common then Uncommon, with the exact percent:

| Id | Name | Jump | Speed | Control | Wall Bounce | Run bonus coins (total) | Run bonus gems (total) | Power |
|---|---|---|---|---|---|---|---|---|
| momo_u | Cloudguard Momo | 4.5 | 9 to 10.5 (+17%) | 30 | 6.9 to 7.8 (+13%) | +80 (160) | +4 (6) | Double Jump |
| forestgirl_u | Thornveil Capi | 4.5 to 5.4 (+20%) | 10 | 28.9 to 32.7 (+13%) | 7.9 | +90 (180) | +1 (2) | Wall Stick |
| beachotter_u | Tidecrest Otto | 4.1 to 4.7 (+15%) | 8 | 32 | 5.8 to 6.5 (+12%) | +50 (150) | +2 (5) | Magnet |
| candygirl_u | Sugarplate Bonbon | 5.5 to 6.8 (+24%) | 10 | 31 to 35.3 (+14%) | 8.4 | +90 (160) | +5 (9) | Double Jump |
| monk_u | Ironbamboo Bao | 5 to 5.8 (+16%) | 10 | 31 to 36.9 (+19%) | 8 | +80 (140) | +4 (9) | Wall Stick |

(Capi's Control shows 28.9 although 29 is stored: the fixed-point acceleration is 28.95.) The merge result card prints these percents (`Jump +20%`, `Control +13%`, `Run coins +90`, ...). Unboosted stats are unchanged.

Effective (played) values, whole numbers, after rounding and the section 7 caps: momo_u launch 19, speed 11, control 30, wall share 74; forestgirl_u 21 / 10 / 33 / 79; beachotter_u 19 / 8 / 32 / 81; candygirl_u 23 (capped, shown 6.8 would be 23.3) / 10 / 35 / 84; monk_u 22 / 10 / 37 / 80. The wall share is derived from Wall Bounce over Speed, so a boosted Speed with an unboosted Wall Bounce lowers the share (Momo 77 to 74).

**Shown versus effective.** The hero sheet shows the chained number (`CharacterSpec.Shown`, `Simulation/ShownStats.cs`). The simulation plays `CharacterSpec.Stats`, derived from it with `ShownStats.ToPhysics` and clamped to the limits of section 7. Where the chain passes a limit, the sheet shows the chained number and the character plays at the limit (cap table in docs/34 section 2b: Rare and above will often be capped). Levels, bot proofs and golden hashes stay on the baseline Momo config. The run bonus is meta only and not capped by these limits. The tiers do not use flat coin or gem finds.

## 7. Limits a character must respect (validated by `CharacterStats.Validate`)

Since 2026-11-02 a character may sit up to 10 percent under Momo's baseline (floors below are the baseline minus 10 percent, rounded). Upper limits keep layouts and the 4.2 u gap cap sensible. **The upper limits are proposals until the owner signs them off.** Solvability note: bot proofs and golden hashes run on the baseline; a standing jump of Jump 17 or 18 (3.6 to 4.05 u) is below the 4.2 u largest normal gap, so such a hero relies on moving jumps (the floor's full-speed jump is 5.1 u; covered by a test).

| Field | Minimum | Maximum (proposed) |
|---|---|---|
| `JumpLaunch` | 17 | 23 |
| `MaxSpeed` | 8 | 12 |
| `Control` | 27 | 45 |
| `WallBoostHundredths` | 69 | 100 |
| `CoinFind` | 0 | 50 |
| `GemFind` | 0 | 5 |
| `RunBonusCoins` | 0 or 50 | 100 (multiples of 10); merge tiers add on top, up to 2100 (`ValidateTier`) |
| `RunBonusGems` | 0 or 1 | 5; merge tiers add on top, up to 15 (`ValidateTier`) |

## 8. Changing or adding a character

1. Add or edit its `CharacterSpec` row in `CharacterDefs.All` and set `Stats` (omit it for the baseline).
2. Run `CharacterStatsTests` (validation of every roster entry, derived sheet) and the full Simulation suite.
3. Update the roster table in section 6 and the changelog below in the same change.

## 9. Status and open items

- Done: data model, validation, derived sheet, braking as a factor of Control, per-run application of the chosen character's stats, flat finds, tests.
- Not done: the stat card on the Characters screen (main card with an Advanced expander). It needs on-device screenshots at 9:16 and 9:21 and the layout test (CUX-3 and CUX-5), and the Characters screen is icon-only today, so adding text is a design decision.
- Open: sign-off of the maximum values in section 7; whether the card shows Jump as a height (4.5) or in platform rows (1.3).

## Changelog
- 2026-11-03: Uncommon rows redone again (seed 20261104): percent is over the SHOWN sheet numbers (one decimal) and exact, chain Rare / Elite / Legendary as rules (docs/34 section 2b); `ShownStats` derives the physics, caps stay.
- 2026-11-02: Uncommon rows redone to the owner rule (2 stats, run bonus added on top, 1 region power); `ValidatePhysics` / `ValidateTier`; tier rule tables in docs/34 section 2b.
- 2026-11-02: five Uncommon merge tiers added as data (section 6b, docs/34).
- 2026-11-02: roster stats drawn once (Capi, Otto, Bonbon, Bao within -10..+10 percent of Momo), new stats `RunBonusCoins` / `RunBonusGems` for all five heroes (shown as 'Run bonus' on the hero sheet, paid by `ApplyRun`), floors lowered by 10 percent. Hero sheet: green previous / next arrows step through the owned heroes.
- 2026-11-01: document created from the code. `Friction` 45 replaced by `BrakeFactor` 1.5 × Control (identical for Momo). Docs 02, 08 and 09 still list `JumpSpeedFactor` 0.58 and are to be corrected from this file.
