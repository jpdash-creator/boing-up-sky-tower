> **Replaced for Regions 1 to 5 (and for all future regions) by doc 45 (45-levels-and-difficulty-curve.md): the 15-level structure and the old difficulty numbers no longer apply.**

# 24. Difficulty and new special platforms (owner issue list 26, 2026-10-31)

Owner decisions so far (Q and A):
- Levels 1 and 2 of EVERY region stay gentle; from level 3 to 15 the game is clearly harder, in every region.
- Region 1 (Meadow Springs) gets 7 NEW special platforms, ALL available from level 3, mixed freely. Today region 1 only has Normal plus the signature bouncy flowers (and the generic Moving/Crumble/Vanishing/Sticky/Slippery/Bounce kinds the builder can mix).
- Platform art is made with OpenArt, region by region; region 1 first, implement, test; then region 2, and so on one by one (each region gets its own questions).
- The same difficulty and randomness also apply to Endless mode.
- The speed-up percentage shown on screen must match the real speed (stream S1 verifies it).
- Random layouts per attempt stay (docs/23); every layout stays bot-proven beatable (CUX-7: no unavoidable damage; hazards are not telegraphed, see the owner decision below).

Owner decision 2026-11-01: platform behaviour is discovered by the player; no code-drawn cues; hazards still never kill, never unavoidable, bot-proven solvable. The cue column below is historical: none of those code-drawn overlays exist any more. Only the painted platform sprites and the platform motion that is the mechanic (bobbing, moving, tilting, Snap Daisy closing/dimming/squashing) remain.

## Region 1: the 7 new platforms (lead's proposed numbers, base values live in SimConfig)
| # | Name | What it does | Telegraph (REMOVED 2026-11-01, historical) |
|---|---|---|---|
| 1 | Wind Petal | A petal platform. While you stand on it a gust pushes you sideways at about 2.5 units/s; the direction flips every 2.5 s | Animated arrows on the petal; arrows flip 0.5 s before the gust flips |
| 2 | Bee Hive Lift | A hive platform that bobs up and down by about 0.8 row, period 3 s | Bees circling it, a small up/down arrow |
| 3 | Snap Daisy | A daisy that closes 1.5 s after you land (you fall through), and reopens 2 s later | Petals visibly closing, a 3-step tick countdown ring |
| 4 | Dandelion Puff | Launches you diagonally (about 1.3x a normal jump height and 4 units sideways) in the direction of its arrow | A big arrow on the puff, seeds blowing in that direction |
| 5 | Thorn Bush | A platform whose left OR right half is thorny; landing on thorns knocks you sideways off the platform (never kills) | Spiky half with a different SHAPE and a warning icon; the safe half is smooth |
| 6 | Ladybug Rider | Moves along a short horizontal path and moves 1.5x faster while you stand on it | A ladybug riding on it, speed lines when boosted |
| 7 | Seesaw Leaf | Tilts toward the side you land on and slides you toward that edge | The leaf visibly tilts, small slope arrows |

Rules for the generator: levels 1 and 2 of region 1 use none of them. From level 3 the share of rows with a new special rises with the level (about 15 percent at level 3 to about 55 percent at level 15, each row picks one of the 7 or the flower), gaps and speed rise per the difficulty curve; no two hazard-type platforms (Thorn Bush, Snap Daisy) in adjacent rows; the last 3 rows before the goal and the 2 rows around the hidden gem avoid launch and hazard kinds (existing rules). The bot must solve every random layout of levels 3 to 15 (20 seeds each plus more).

## Difficulty curve (all regions, levels 3 to 15)
- Levels 1, 2: as today.
- Levels 3 to 15: platform width, gap range, special share, scroll speed step and hazard density all ramp up; target: level 15 of region 1 about as hard as level 15 of region 3 today; every later region adds its own signature on top. Exact numbers are tuned with the bot's survival statistics and the owner's playtest.
- Endless: uses the same generator, the new platforms of the current region and the level cycle below, with a new random seed per run.

## Endless cycle (owner 2026-11-05, "Endless is endless: go till floor 100000 via code")
Endless difficulty is stepped by ONE pure function, `EndlessTower.LevelForFloor(floor)` -> campaign level 1..15 (floor n = row n, the start platform is floor 0). It replaces the old ramps (layout offset to 1.0 at floor 400, special/hazard share saturating at floor 150, scroll +4 percent per 25 floors capped at 150 percent, kinds unlocking every 10 floors, player-speed bonus).

| Floors | Level |
|---|---|
| 1-1500 | ceil(floor / 100): L1 at 1-100, L2 at 101-200 ... L14 at 1301-1400, L15 at 1401-1500 |
| 1501-1600 / 1601-1700 / 1701-1800 | L9 / L10 / L11 |
| 1801-1900 / 1901-2000 / 2001-2100 | L12 / L13 / L14 |
| 2101-2500 | L15 held (400 floors) |
| 2501-3500, 3501-4500 ... | the same 1000-floor cycle again (L9 ... L14 for 100 floors each, L15 for 400), without end; 99999 and 100000 are L13, 100001 is L14 |

A floor at level N uses everything the region's campaign level N uses, through the same code: layout difficulty d (`LevelSpec.DifficultyForLevel`: content difficulty with the region offset, then the level 3..15 ramp floor; a test pins it to every shipped level of all 5 regions), special-platform share (`SpecialPlatforms.RowPercent` over the 100 floors of the step, i.e. the level's own ramp), hazard spacing (`SpecialPlatforms.HazardSpacing`: 6 rows up to level 6, 2 from level 7), the kind list (`SpecialPlatforms.Options` of level N: only level 1 differs, it has no hazard types), gong density (region 5, none below level 3, half at level 3) and the scroll speed (`Sim`: base speed of level N's d, with the campaign time step-ups and the 4 u/s ceiling; no floor ramp and no player-speed bonus). Each reset to L9 therefore also eases the speed.

Generation: the tower is prefix stable and the PLAYED tower has no floor limit (owner 2026-11-05, "Fix the limit now": endless is playable to floor 100000 and beyond; the old cap `MaxRows` = 7000, about 2 hours of perfect play, now only limits a FLAT tower, i.e. golden hashes and tests). Heights are Q16.16 (about 32000 u), so the played tower is an `EndlessWorld` (Stream mode) that RE-BASES while the run goes on: when the top of the stored heights passes 2500 u (about every 500 floors, 200 times to floor 100000) it drops the rows far below the player (never within 30 rows of his platform) and shifts EVERY height and the platform / gong ids down by one exact amount; `Sim.ShiftState` moves the running state with it (Y, KillY, MaxY, rocket target, GroundId, BestGroundId, the per-platform clocks). Floors are never re-based: `Platform.Row`, `SimState.Row`, score, `LevelForFloor` and the generator row index stay absolute; `EndlessWorld.OriginYRaw` = height removed so far (absolute height = origin + height). The Sim only compares heights with heights, so a re-base is invisible: the per-tick state traces of a run with re-bases (also forced in mid-air and during Rocket / Flight) equal the run on a tower that never re-bases, bit for bit, in all 5 regions (`EndlessRebaseTests`). Rows are generated lazily in chunks of 40, 60 rows above the player; the window stays at 300 to 950 platforms (under 40 KB per array), a growth allocates at most 64 KB, and all 100000 floors cost under a second of generation and under 0.5 MB retained (tests). The view follows a re-base in the same frame (`RunView.SyncRebase`): index caches move down, stored heights move by the same amount, and the background / wall patterns use the absolute height so nothing pops. Found on the way: gong placement computed (prevY + y) / 2, which wrapped past 16384 u (about floor 4500); fixed without changing any earlier tower. Score additions saturate at int.MaxValue (a combo chain of about 1100 landings overflowed it before).

## Open design questions for later regions (asked one region at a time)
Regions 2 to 5 (signatures: bouncy caps, drifting platforms, sticky gum, swinging gongs): new special platforms, hazards and art, one region per round.

## Art plan (OpenArt, region 1)
One width sheet per new platform kind (thin to long rows) in the style bible, sliced by Tools/art_pipeline/slice_width_sheet.py into baked widths (1.5 to 8) as the existing platforms; plus platform motion (tilt, squash) in code; no drawn cue overlays (owner decision 2026-11-01). Approval packet with the exact prompts and credits is sent before any generation.

## Findings and questions from helpers (S1, speed percentage)
- The run HUD speed-up banner was wrong: it ignored the 6-step limit, the +150% cap, the campaign ceiling of 4.0 u/s and the endless row ramp. It now shows the real speed (round(current / starting - 1)) read from the simulation.
- Question: change the difficulty label "(+20% / 5 s)" in Settings to a true text such as "up to +150%"?
- Question: show a separate cue when the 4.0 u/s campaign ceiling holds the speed below the nominal speed-up?

## Helper questions (P1 power-ups, R1 platforms)
- P1: Shield is now a pure 60 s gong block (no rescue below the kill line): keep, or bring the rescue back as a bonus?
- P1: Double Jump = one extra air jump per air phase during the 30 s window (refilled on every landing); confirm (unlimited air jumps would be flight).
- P1: Rocket counts 20 floors from the platform last stood on and stops 3 rows under the finish row, so near the top it climbs less than 20 floors.
- P1: longer durations may call for repricing Shield, Slow Time, Magnet.
- R1: level 2 of region 1 got a light touch (2 safe + 1 hazard new platforms) per the owner's answer; doc text said none. (Implemented the light touch.)
- R1: the 15 to 55 percent share applies to free generated rows; chunk levels (5, 10, 15) and ladder level 11 have fewer free rows, so level 15 is about 31 percent of all rows (50 percent counting authored chunk rows). Want 55 percent of all rows?
- R1: Bee Hive Lift bob is +-1.2 u (about 0.8 of a row) peak to peak 2.4 u: intended size?
- R1: thorn hop pushes about 3 u away, no row credit, never kills: strong enough, or should it always throw the player off the platform?
- R1: ramp floor 0.30 at level 3 to 1.00 at level 15 (level 3 base scroll 1.14 u/s instead of 0.87): right jump from level 2 to 3?
- R1: gems per region now about 56 to 74 (30 percent fewer).
- (A1 art) Bee Hive Lift: plain honeycomb for all widths (the honey-drip variant unused): fine?
- (A1 art) The Seesaw Leaf sparkle glint is baked in and static: acceptable?
- (A1 art) Snap Daisy: add a closing squash animation? (squash and dim only; the countdown pips were removed 2026-11-01)
