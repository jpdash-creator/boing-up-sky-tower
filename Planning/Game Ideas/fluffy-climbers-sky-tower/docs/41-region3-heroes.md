# 41. Region 3 heroes (Sunny Beach, Bug List 5 F4.3, 2026-11-06)

**Source of truth: the code.** Region 3 (Sunny Beach) has the same shape as Regions 1 and 2 (docs/40): 5 characters x 5 rarity tiers = 25 heroes. Otto (`beachotter`, the region starter with the reveal; Common and Uncommon existed) gets Rare, Elite and Legendary, and four new Commons come as hero cards (not starters, no reveal): **Pinch the crab** (`pinch`), **Shelly the sea turtle** (`shelly`), **Bloop the pufferfish kid** (`bloop`), **Coco the monkey** (`coco`). Ids `{line}_u/_r/_e/_l`, merge chain, chests, bundles, shop cards and weekly hero exactly as docs/40 section 3; they only appear once Region 3 is open. No gear, no wings.

Code: `Economy/Characters.cs` (`CharacterDefs.Pinch / Shelly / Bloop / Coco`, `Bases`), `Economy/MergeTierDefs.cs` (19 new `TierRow`s), `Economy/TierRules.cs` (`Lines`, `Pools`), `Economy/RegionGoals.cs` (`Region3Extra`). Art and tools: `Tools/art_pipeline/cut_region1_heroes.py` (table entries for Region 3), `Tools/art_pipeline/qa_hero_parts.py`, `Tools/tier_rows.py`, `Assets/Resources/Characters/{id}/`. Tests: EditMode `Region3HeroTests` plus the updated `CharacterRigTests`, `HeroScreensTests`, `MergeTests`, `BundleTests`, `HeroPurchaseTests`, `HeroInventoryTests`, `RewardsTests`, `Region1HeroTests`; dotnet `TierRulesTests`, `HeroCardsTests`, `ChestCardTests`, `BundleCardTests`; CriticalUX `Region1Full` now owns all 15 heroes of Regions 1 to 3.

## 1. Roster, stats, powers

| Hero | Identity | Identity power (first of the pool) | Run bonus |
|---|---|---|---|
| Otto (existing) | calm swimmer | Magnet (Magnet, Double Jump, Flight, Shield) | 100 coins, 3 gems |
| Pinch | defence, wall grip | Shield (Shield, Wall Stick, Slow Time, Magnet) | 60 coins, 3 gems |
| Shelly | calm control | Slow Time (Slow Time, Shield, Magnet, Double Jump) | 70 coins, 2 gems |
| Bloop | jump | Double Jump (Double Jump, Slow Time, Rocket, Shield) | 90 coins, 2 gems |
| Coco | coin finder | Magnet (Magnet, Double Jump, Rocket, Slow Time), coin find +30 per level | 80 coins, 1 gem |

Hero physics and the rarity rules: see docs/44-power-mechanics-bible.md (section 4 the rules, section 8 the numbers of every hero); every (run coins, run gems) pair of the 17 Common heroes is distinct. Run add-ons drawn by `Tools/tier_rows.py` (seed BaseSeed + line index + 1000 x tier; line index continues after Fenn: Pinch 13, Shelly 14, Bloop 15, Coco 16; Otto is line 2). Coco takes the better of two draws for run coins and gems. Otto's Uncommon row is the existing constant. The script's rounding now follows .NET ("0.0" formats from 15 significant digits); the rows of Regions 1 and 2 reproduce unchanged. Coco's flat coin find (+30 coins per cleared level) is kept by every tier.

| Id | Name | Tier | Run coins add | Run gems add |
|---|---|---|---|---|
| beachotter_r | Shellplate Otto | Rare | +220 | +8 |
| beachotter_e | Wavecloak Otto | Elite | +900 | +5 |
| beachotter_l | Pearlking Otto | Legendary | +1070 | +9 |
| pinch_u | Shellvest Pinch | Uncommon | +100 | +3 |
| pinch_r | Clawguard Pinch | Rare | +360 | +9 |
| pinch_e | Tidecrown Pinch | Elite | +710 | +8 |
| pinch_l | Reefking Pinch | Legendary | +1040 | +6 |
| shelly_u | Coralstrap Shelly | Uncommon | +100 | +3 |
| shelly_r | Reefplate Shelly | Rare | +470 | +9 |
| shelly_e | Pearltiara Shelly | Elite | +660 | +6 |
| shelly_l | Coralqueen Shelly | Legendary | +1880 | +9 |
| bloop_u | Coralband Bloop | Uncommon | +70 | +3 |
| bloop_r | Reefmail Bloop | Rare | +260 | +5 |
| bloop_e | Bubblecrown Bloop | Elite | +530 | +7 |
| bloop_l | Bubbleking Bloop | Legendary | +1140 | +10 |
| coco_u | Palmwrap Coco | Uncommon | +100 | +3 |
| coco_r | Leafmail Coco | Rare | +330 | +8 |
| coco_e | Junglecape Coco | Elite | +900 | +8 |
| coco_l | Palmking Coco | Legendary | +1670 | +5 |

(Otto's Rare, Elite and Legendary rows are the first three lines; his Uncommon "Tidecrest Otto" is unchanged.)

## 2. How a player gets them

Region 3 progress hands out the four Commons (`RegionGoalTable.HeroGoalId`): `g_3_levels_2` = Pinch, `g_3_levels_3` = Shelly, `g_3_levels_4` = Bloop, `g_3_stars_5` = Coco, `g_3_levels_5` = Otto (as before). Otto stays the only free Common of the region (`RegionStarter`). Region 3 heroes only enter chest, bundle, shop and weekly draws once Region 3 is open (`BundleCards.RegionOf`).

## 3. Art

Approved paintings (1728 x 2304, flat grey) in `Art/_candidates`: Otto r1 / e2 / l1, Pinch c1 / u1 / r2 / e2 / l2, Shelly c2 / u1 / r2 / e2 / l2, Bloop c4 / u1 / r2 / e2 / l2, Coco c2 / u1 / r3 / e2 / l1. Cut by `cut_region1_heroes.py` into `legL, legR, body, handL, handR, head` (the stack equals the painting; no `back` piece was needed). QA in `Tools/art_pipeline/previews/region3_heroes_qa.txt` and the `region3_qa_*.png` contact sheets: overlap 0 px and 0 px touching the canvas edge on all 23. Key modes: the white sticker rim key for all Commons to Elite and for Otto and Bloop Legendary; the Legendary paintings of Pinch, Shelly and Coco use a plain silhouette trim (`trim40`, `trim40`, `trim50`) because their bright golden glow made the grey-edge metric read 12 to 27 percent with the glow modes (and the glow modes cut Otto's pearl and left stray specks). Grey-edge metric: 0.1 to 1.6 percent for 16 cuts, Otto Rare 3.6, Elite 5.9 and Legendary 6.9 (soft pearl and shell shine, checked visually), Pinch Legendary 2.9, Shelly Legendary 3.5, Coco Legendary 12.7 (golden cape edge, checked visually on dark and light backgrounds).

## 4. Open points for the owner

- Coco Legendary: the trim removes part of the glow and part of the tail near the cape. The sparkles and bubbles around the Legendary and Elite paintings are dropped (they would need separate back pieces).
- Coco and Nib are both coin finders (Magnet + coin find); Coco's flat find is +30 (Nib +20).
- Legendary run bonus pays up to 2,000 extra coins per run (docs/44 section 7b).
- Regions 4 and 5 still have only Common and Uncommon (F4.4 and F4.5).
