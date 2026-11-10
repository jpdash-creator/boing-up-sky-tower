# 42. Region 4 heroes (Candy Canyon, Bug List 5 F4.4, 2026-11-06)

**Source of truth: the code.** Region 4 (Candy Canyon) has the same shape as Regions 1 to 3 (docs/40, docs/41): 5 characters x 5 rarity tiers = 25 heroes. Bonbon (`candygirl`, the region starter with the reveal; Common and Uncommon existed) gets Rare, Elite and Legendary, and four new Commons come as hero cards (not starters, no reveal): **Gummi the gummy bear** (`gummi`), **Choco the chocolate bunny** (`choco`), **Marsh the marshmallow kid** (`marsh`) and **Cookie the cookie kid** (`cookie`). No hero has wings. Rig ids: Common = line id, tiers `{line}_u`, `_r`, `_e`, `_l` (`Assets/Resources/Characters/{id}/`). Bonbon's Uncommon keeps its existing rig (with the candy-cane `back` piece).

Code: `Economy/Characters.cs` (`CharacterDefs.Gummi / Choco / Marsh / Cookie`, `Bases`), `Economy/MergeTierDefs.cs` (Region 4 `TierRow`s), `Economy/TierRules.cs` (`Lines`, `Pools`), `Economy/RegionGoals.cs` (`Region4Extra`). Art and tools: `Tools/art_pipeline/cut_region1_heroes.py` (Region 4 entries), `Tools/art_pipeline/qa_hero_parts.py`, `Tools/tier_rows.py`, `Tools/art_pipeline/previews/region4_*`. Tests: `Region4HeroTests`.

## 1. Roster, stats, powers

Hero physics (Jump, Speed, Control, Wall Bounce) and the rarity rules: see docs/44-power-mechanics-bible.md (section 4 the rules, section 8 the numbers of every hero); the old "slightly above Region 3" lift and the Momo +-10 percent band (Jump 17..21, Speed 8..10, Control 27..33, Wall 69..85) are retired. Every (run coins, run gems) pair stays distinct among the 25 Common heroes.

| Hero | Identity | Identity power (first of the pool) | Run bonus |
|---|---|---|---|
| Bonbon (existing) | jump, wall bounce | Double Jump (Double Jump, Slow Time, Magnet, Rocket) | 70 coins, 4 gems |
| Gummi | defence, wall bounce | Shield (Shield, Wall Stick, Slow Time, Magnet) | 80 coins, 3 gems |
| Choco | jump | Double Jump (Double Jump, Rocket, Shield, Slow Time) | 100 coins, 1 gem |
| Marsh | calm control | Slow Time (Slow Time, Shield, Double Jump, Magnet) | 90 coins, 4 gems |
| Cookie | coin finder | Magnet (Magnet, Double Jump, Rocket, Shield), coin find +35 per level | 80 coins, 4 gems |

Tier physics follow doc 44 sections 4 and 8 (no percent chain). Run add-ons drawn by `Tools/tier_rows.py` (seed BaseSeed + line index + 1000 x tier; line index continues after Coco: Gummi 17, Choco 18, Marsh 19, Cookie 20; Bonbon is line 3). Cookie takes the better of two draws for run coins and gems. Bonbon's Uncommon is the existing constant.

| Id | Name | Tier | Run coins add | Run gems add |
|---|---|---|---|---|
| candygirl_r | Caramelplate Bonbon | Rare | +360 | +9 |
| candygirl_e | Sprinklecloak Bonbon | Elite | +930 | +5 |
| candygirl_l | Sugarqueen Bonbon | Legendary | +1620 | +6 |
| gummi_u | Starsash Gummi | Uncommon | +90 | +1 |
| gummi_r | Gemguard Gummi | Rare | +330 | +5 |
| gummi_e | Heartplate Gummi | Elite | +680 | +8 |
| gummi_l | Gummyking Gummi | Legendary | +1530 | +5 |
| choco_u | Wafflewrap Choco | Uncommon | +50 | +4 |
| choco_r | Fudgeplate Choco | Rare | +400 | +7 |
| choco_e | Cocoacape Choco | Elite | +760 | +7 |
| choco_l | Chocoking Choco | Legendary | +1990 | +7 |
| marsh_u | Grahambelt Marsh | Uncommon | +80 | +1 |
| marsh_r | Toastplate Marsh | Rare | +450 | +5 |
| marsh_e | Crackerplate Marsh | Elite | +530 | +9 |
| marsh_l | Roastking Marsh | Legendary | +1720 | +7 |
| cookie_u | Crumbscarf Cookie | Uncommon | +90 | +4 |
| cookie_r | Chipguard Cookie | Rare | +470 | +6 |
| cookie_e | Doughcape Cookie | Elite | +930 | +9 |
| cookie_l | Bakeking Cookie | Legendary | +1420 | +10 |

(Bonbon's Uncommon "Sugarplate Bonbon" is unchanged.)

## 2. How a player gets them

Region 4 progress hands out the four Commons (`RegionGoals.HeroGoalId`): `g_4_levels_2` = Gummi, `g_4_levels_3` = Choco, `g_4_levels_4` = Marsh, `g_4_stars_5` = Cookie, `g_4_levels_5` = Bonbon (as before). Bonbon stays the only free Common of the region (`RegionStarter`). Higher tiers come from merging 3 of the tier below, bundles, chests and the shop. Region 4 heroes only enter chest, bundle, shop and weekly draws once Region 4 is open (`BundleCards.RegionOf`).

## 3. Art

Approved paintings (1728 x 2304, flat grey) in `Art/_candidates`: Bonbon r2 / e2 / l2, Gummi pink / u1 / r2 / e2 / l1, Choco c4 / u2 / r1 / e2 / l2, Marsh c2 / u1 / r1 / e2 / l2, Cookie c2 / u1 / r2 / e2 / l2. Cut by `cut_region1_heroes.py` (sticker key) into `legL, legR, body, handL, handR, head`. QA in `Tools/art_pipeline/previews/region4_heroes_qa.txt` and the `region4_qa_*.png` sheets: no part overlap and nothing touches the canvas edge. The high "grey edge" share on Gummi is the translucent pale gummy rim of the painting, not key fringe.

## 4. Open points for the owner

- Legendary paintings (Bonbon, Choco, Marsh) keep a thin pale glow rim around the silhouette; sparkles are dropped.
- Cookie and Nib and Coco are all coin finders; Cookie's flat find is +35.
- Legendary run bonus pays up to 2,000 extra coins per run (docs/44 section 7b).
- Region 5 is covered by docs/43.
