# 43. Region 5 heroes (Bamboo Temple, Bug List 5 F4.5, 2026-11-06)

**Source of truth: the code.** Region 5 (Bamboo Temple) has the same shape as Regions 1 to 4 (docs/40 to docs/42): 5 characters x 5 rarity tiers = 25 heroes. Bao (`monk`, the region starter with the reveal; Common and Uncommon existed) gets Rare, Elite and Legendary, and four new Commons come as hero cards (not starters, no reveal): **Sprout the bamboo sprout** (`sprout`), **Tora the tiger cub** (`tora`), **Rusu the red panda** (`rusu`) and **Ryu the wingless dragon pup** (`ryu`). No hero has wings (Ryu is a wingless dragon). Rig ids: Common = line id, tiers `{line}_u`, `_r`, `_e`, `_l`.

Code: `Economy/Characters.cs` (`CharacterDefs.Sprout / Tora / Rusu / Ryu`, `Bases`), `Economy/MergeTierDefs.cs`, `Economy/TierRules.cs` (`Lines`, `Pools`), `Economy/RegionGoals.cs` (`Region5Extra`). Art and tools: `Tools/art_pipeline/cut_region1_heroes.py` (Region 5 entries), `qa_hero_parts.py`, `Tools/tier_rows.py`, `Tools/art_pipeline/previews/region5_*`. Tests: `Region5HeroTests`.

## 1. Roster, stats, powers

Hero physics (Jump, Speed, Control, Wall Bounce) and the rarity rules: see docs/44-power-mechanics-bible.md (section 4 the rules, section 8 the numbers of every hero); the old "above Region 4" ordering is retired.

| Hero | Identity | Identity power (first of the pool) | Run bonus |
|---|---|---|---|
| Bao (existing) | calm focus, wall mastery | Wall Stick (Wall Stick, Slow Time, Shield, Flight) | 60 coins, 5 gems |
| Sprout | defence, wall bounce | Shield (Shield, Wall Stick, Slow Time, Double Jump) | 70 coins, 5 gems |
| Tora | jump | Double Jump (Double Jump, Wall Stick, Rocket, Shield) | 60 coins, 4 gems |
| Rusu | coin finder | Magnet (Magnet, Double Jump, Slow Time, Rocket), coin find +40 per level | 80 coins, 5 gems |
| Ryu | speed | Rocket (Rocket, Shield, Magnet, Double Jump) | 90 coins, 5 gems |

Tier physics follow doc 44 sections 4 and 8 (no percent chain). Run add-ons drawn by `Tools/tier_rows.py` (line index continues after Cookie: Sprout 21, Tora 22, Rusu 23, Ryu 24; Bao is line 4). Rusu takes the better of two draws for run coins and gems. Bao's Uncommon is the existing constant.

| Id | Name | Tier | Run coins add | Run gems add |
|---|---|---|---|---|
| monk_r | Lotusrobe Bao | Rare | +280 | +8 |
| monk_e | Dragonsash Bao | Elite | +570 | +7 |
| monk_l | Sunmonk Bao | Legendary | +1070 | +9 |
| sprout_u | Leafwrap Sprout | Uncommon | +80 | +5 |
| sprout_r | Bamboomail Sprout | Rare | +500 | +5 |
| sprout_e | Jadecloak Sprout | Elite | +950 | +8 |
| sprout_l | Groveking Sprout | Legendary | +1710 | +6 |
| tora_u | Stripeband Tora | Uncommon | +50 | +3 |
| tora_r | Clawguard Tora | Rare | +340 | +8 |
| tora_e | Tigercape Tora | Elite | +680 | +10 |
| tora_l | Tigerlord Tora | Legendary | +1770 | +8 |
| rusu_u | Scoutwrap Rusu | Uncommon | +60 | +3 |
| rusu_r | Goldfur Rusu | Rare | +390 | +8 |
| rusu_e | Coinmantle Rusu | Elite | +570 | +9 |
| rusu_l | Treasurelord Rusu | Legendary | +1600 | +6 |
| ryu_u | Scalevest Ryu | Uncommon | +50 | +5 |
| ryu_r | Jadeguard Ryu | Rare | +390 | +5 |
| ryu_e | Dragonplate Ryu | Elite | +580 | +5 |
| ryu_l | Skylord Ryu | Legendary | +1020 | +10 |

(Bao's Uncommon "Ironbamboo Bao" is unchanged.)

## 2. How a player gets them

Region 5 progress hands out the four Commons: `g_5_levels_2` = Sprout, `g_5_levels_3` = Tora, `g_5_levels_4` = Rusu, `g_5_stars_5` = Ryu, `g_5_levels_5` = Bao (as before). Bao stays the only free Common of the region. Higher tiers come from merging, bundles, chests and the shop; Region 5 heroes only enter draws once Region 5 is open.

## 3. Art

Approved paintings in `Art/_candidates`: Bao r2 / e1 / l2, Sprout c1 / u1 / r2 / e1 / l1, Tora c2 / u1 / r2 / e1 / l1, Rusu c2 / u1 / r2 / e1 / l1, Ryu c2 / u1 / r2 / e2 / l1. Cut like Regions 1 to 4 (sticker key). QA: `Tools/art_pipeline/previews/region5_heroes_qa.txt` and `region5_qa_*.png`.

## 4. Open points for the owner

- Ryu Elite and Legendary and Tora Elite and Legendary keep a thin pale sticker or glow rim after the key (see the QA sheets).
- Rusu, Cookie, Coco and Nib are coin finders; Rusu's flat find is +40, the highest.
- All eight Region 4 and 5 lines now reach Legendary; no placeholder card is left in All Heroes.
