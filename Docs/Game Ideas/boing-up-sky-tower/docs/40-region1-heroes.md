# 40. Region 1 and Region 2 heroes (Bug List 5 F4.1 and F4.2, 2026-11-06)

**Source of truth: the code.** Region 1 (Meadow Springs) now has **5 characters x 5 rarity tiers = 25 heroes**: Momo (Common and Uncommon already existed; Rare, Elite and Legendary are new) and four new characters, each with Common, Uncommon, Rare, Elite and Legendary. No gear, no wings on any hero (Flight is a power-up). Characters and power-ups only.

Code: `Economy/Characters.cs` (`CharacterDefs.Bumble / Ribbi / Dandy / Pip`, `CharacterSpec.RegionStarter`), `Economy/MergeTierDefs.cs` (the 19 new `TierRow`s), `Economy/TierRules.cs` (`Lines`, `Pools`), `Economy/RegionGoals.cs` (`RegionGoalTable.HeroGoalId`), `Economy/RewardEntry.cs` (`HeroId`), `Economy/ProgressService.Merge.cs` / `ProgressService.Rewards.cs`, `Gameplay/AllHeroesScreen.cs` (one hero line per row block). Art and tools: `Tools/art_pipeline/cut_region1_heroes.py`, `Tools/tier_rows.py`, `Assets/Resources/Characters/{id}/`. Tests: EditMode `Region1HeroTests`, `HeroScreensTests`, `MergeTests`, `RegionGoalTests`, `ChestTests`, `HeroPurchaseTests`, `BundleTests`; dotnet `TierRulesTests`, `HeroCardsTests`, `ChestCardTests`, `BundleCardTests`, `HeroPurchaseRulesTests` (updated, see the report of the change); CriticalUX `AllHeroesScreen*`, `MergeScreen*`.

## 1. The roster

| Line id (Common id) | Name | Identity | Identity power (first power of the pool) |
|---|---|---|---|
| `momo` | Momo | all-rounder (existing) | Double Jump |
| `bumble` | Bumble the bee, mage | speed | Flight |
| `ribbi` | Ribbi the frog | jump | Double Jump |
| `dandy` | Dandy the dandelion puff | floaty air control ("slow fall") | Slow Time |
| `pip` | Pip the ladybug | wall bounce | Shield |

Ids: the Common hero keeps the line id (`bumble`, `ribbi`, `dandy`, `pip`, like `momo`), the tiers are `{line}_u`, `{line}_r`, `{line}_e`, `{line}_l` (Elite = code enum `Epic`). Merge chain (unchanged rule): 3 Common -> 1 Uncommon, 3 Uncommon -> 1 Rare, 3 Rare -> 1 Elite, 3 Elite -> 1 Legendary; Legendary does not merge.

Names: Momo (Cloudguard, Cloudknight, Skyplate, Sunlord), Bumble (Honeycloak, Hivemage, Combweaver, Archmage), Ribbi (Leafcrown, Barkplate, Mossknight, Jadecrown), Dandy (Seedling, Blossom, Petalcloak, Bloomqueen), Pip (Redcape, Crownguard, Thornplate, Goldshell). A tier hero belongs to Region 1 through its line (`BundleCards.RegionOf`).

There is **no gravity or slow-fall stat** (and the simulation is not touched), so Dandy's "slow fall" is expressed only by the Slow Time power (her Control and Speed are drawn like any hero's under doc 44 section 4; the roster identities are themes of art, power and run bonus, not a promise of one top stat). If the owner wants a real slow fall it needs a new physics stat (simulation change, golden hashes).

## 2. Stats

Hero physics (Jump, Speed, Control, Wall Bounce), the rarity rules and the Forgiveness Index: see docs/44-power-mechanics-bible.md (section 4 the rules, section 8 the numbers of every hero). Only the run bonus of the Common heroes is kept here (a meta reward outside the Forgiveness Index, rule in doc 44 section 7b).

| Hero | Common run bonus |
|---|---|
| Momo | 80 coins, 2 gems |
| Bumble | 70 coins, 3 gems |
| Ribbi | 60 coins, 2 gems |
| Dandy | 100 coins, 4 gems |
| Pip | 50 coins, 1 gem |

Tier heroes are independent hero physics sets under the rarity rules of docs/44-power-mechanics-bible.md section 4 (numbers: section 8); there is no percent chain over the previous tier any more. What a tier adds besides physics: run coins +50..100 / +200..500 / +500..1000 / +1000..2000; run gems +1..5 / +5..10 / +5..10 / +5..10 (doc 44 section 7b); powers 1 / 2 / 3 / 4; cooldown 30 / 22 / 15 / 10 s. The run add-ons per hero are fixed constants in the table below (drawn once by `Tools/tier_rows.py`; the percent boosts and SHOWN stats of that script are retired). Momo's Uncommon row is the existing constant.

| Id | Name | Tier | Run coins add | Run gems add |
|---|---|---|---|---|
| momo_r | Cloudknight Momo | Rare | +350 | +6 |
| momo_e | Skyplate Momo | Elite | +690 | +7 |
| momo_l | Sunlord Momo | Legendary | +1470 | +8 |
| bumble_u | Honeycloak Bumble | Uncommon | +60 | +2 |
| bumble_r | Hivemage Bumble | Rare | +410 | +8 |
| bumble_e | Combweaver Bumble | Elite | +690 | +5 |
| bumble_l | Archmage Bumble | Legendary | +1390 | +6 |
| ribbi_u | Leafcrown Ribbi | Uncommon | +100 | +1 |
| ribbi_r | Barkplate Ribbi | Rare | +330 | +10 |
| ribbi_e | Mossknight Ribbi | Elite | +530 | +7 |
| ribbi_l | Jadecrown Ribbi | Legendary | +1350 | +9 |
| dandy_u | Seedling Dandy | Uncommon | +70 | +5 |
| dandy_r | Blossom Dandy | Rare | +210 | +8 |
| dandy_e | Petalcloak Dandy | Elite | +560 | +5 |
| dandy_l | Bloomqueen Dandy | Legendary | +1550 | +6 |
| pip_u | Redcape Pip | Uncommon | +50 | +4 |
| pip_r | Crownguard Pip | Rare | +390 | +10 |
| pip_e | Thornplate Pip | Elite | +980 | +5 |
| pip_l | Goldshell Pip | Legendary | +2000 | +9 |

Shown versus played stats and physics caps no longer exist as a mechanic: every hero plays exactly the numbers of the hero table, see docs/44 sections 4 and 8.

Hero power pools (`TierRules.Pools`; tier n uses the first n): Bumble Flight, Magnet, Slow Time, Shield. Ribbi Double Jump, Wall Stick, Magnet, Rocket. Dandy Slow Time, Shield, Magnet, Double Jump. Pip Shield, Wall Stick, Slow Time, Magnet. Momo (existing): Double Jump, Magnet, Shield, Rocket.

## 3. How a player gets them (all heroes tied in)

- **Ownership.** `CharacterSpec.RegionStarter` marks the ONE Common hero a region hands over by itself (Momo, Capi, Otto, Bonbon, Bao). Bumble, Ribbi, Dandy and Pip are Region 1 Commons but NOT starters: nobody owns them until a copy arrives (they never need the "New hero joins you!" reveal). Existing saves are unaffected (the copies table has no key for them).
- **Region 1 progress (Main goals, `RegionGoalTable.HeroGoalId`)**: clear 6 levels = Bumble, clear 9 levels = Ribbi, clear 12 levels = Dandy, earn 45 stars = Pip, clear all 15 levels = Momo (as before). One copy each, claimed like any goal (`RewardEntry.HeroId`).
- **Weekly top task** = one random Common hero of an OPEN region (so in Region 1 any of the five).
- **Iron chest** 70 Common / 20 Uncommon / 10 Rare, **Golden chest** 50 Uncommon / 40 Rare / 10 Elite; **bundle hero card** 70 Uncommon / 20 Rare / 10 Elite. A drawn tier picks uniformly among the heroes of that tier whose region is open (Region 1 is always open, so 5 heroes per tier). A tier with no hero in an open region falls back to the next lower tier. No chest or bundle gives Legendary (by merge or shop only).
- **Shop hero cards** Uncommon 150, Rare 300, Elite 700, Legendary 1,500 gems: a random hero of that tier from open regions (Region 1: one of 5; Regions 2 to 5 only have Uncommon today).
- **Merge** 3 -> 1 up the whole chain.
- **Character Test** (admin) owns only the Common heroes (now 9, including the four new ones); never the tiers.

## 4. Screens

- **All Heroes**: groups by region; Region 1 shows 25 cards. Each hero line (Common to Legendary) starts on its own row (3 cards, then 2) so a ladder is not split across other heroes. Owned = colour, the rest = locked silhouette with "i" details and how to get it.
- **Heroes (main)**: owned heroes only, one card per copy (unchanged logic).
- **Merge**: lists every owned type with a next tier and at least 3 copies (now also Uncommon, Rare, Elite of Region 1).
- Layout rules unchanged (CUX-3/4/5): 48 dp targets, no overlap at 9:16, 9:19.5, 9:21, 3:4 with notch, 130 percent text; the grid just scrolls longer.

## 5. Art

Approved paintings (full body, front, 1728 x 2304 on flat grey) in `Art/_candidates`: Momo r1 / e3 / l1, Bumble c3 / u1 / r1 / e2 / l1, Ribbi c1 / u1 / r2 / e2 / l2, Dandy c4 / u1 / r1 / e1 / l2, Pip c1 / u2 / r2 / e2 / l2. Cut by `Tools/art_pipeline/cut_region1_heroes.py` (key the background and the white sticker rim, register to the 1365 x 1649 canvas with the whole silhouette kept inside the canvas width, partition into `legL, legR, body, handL, handR, head` by plain lines, `back` only where there is a glow: Momo Legendary light rays and Bumble Legendary honey glow). The parts stack to the painting. QA (`qa_hero_parts.py`, CUX-9): overlap 0 px and 0 px touching the canvas edge on all 23 cuts; the grey-edge metric is 0.2 to 1.2 percent for Ribbi, Pip (except Pip Legendary 5.5), Bumble (except Elite 4.1 and Legendary 3.2), Momo Elite 4.5 and Legendary 3.9; it reads high (10 to 27 percent) on Momo Rare and on all five Dandy because the metric flags the soft shaded fibres of the white puff and the mauve fringe of Momo Rare, which were checked visually on sky blue and dark backgrounds and look clean. Results: `Tools/art_pipeline/previews/region1_heroes_qa.txt` and the contact sheets next to it.
No whole-body fallback was needed.

## 6. Open points for the owner

- Bumble's Common painting (`bumble_c3`) has a small wing on the body. The rule is "no wings on any hero (Flight is a power-up)". It was an approved pick, so it was imported as is.
- Dandy "slow fall" has no stat behind it (section 1).
- Legendary run bonus pays up to 2,000 extra coins per run (the owner rule, docs/44 section 7b); with five heroes per tier this is easier to reach in Region 1.
- Regions 3 to 5 still have only Common and Uncommon (F4.3 to F4.5).


## 7. Region 2 (Mushroom Grove, Bug List 5 F4.2, 2026-11-06)

Region 2 has the same shape as Region 1: 5 characters x 5 tiers = 25 heroes. Capi (`forestgirl`, the region starter with the reveal; Common and Uncommon existed) gets Rare, Elite, Legendary, and four new Commons come as hero cards (not starters, no reveal): **Nib the squirrel** (`nib`), **Spike the hedgehog** (`spike`), **Moss the snail kid** (`moss`), **Fenn the fox kit** (`fenn`). Ids `{line}_u/_r/_e/_l`, merge chain and drops exactly as Region 1 (section 3). The weekly random Common hero, chests, bundles and shop cards include Region 2 heroes only once Region 2 is open.

| Hero | Identity | Identity power (first of the pool) | Run bonus |
|---|---|---|---|
| Capi (existing) | wall and safe footing | Wall Stick (pool: Wall Stick, Shield, Magnet, Slow Time) | 90 coins, 1 gem |
| Nib | coin finder | Magnet (Magnet, Double Jump, Slow Time, Rocket) | 100 coins, 2 gems |
| Spike | defence | Shield (Shield, Magnet, Slow Time, Rocket) | 50 coins, 3 gems |
| Moss | wall bounce | Wall Stick (Wall Stick, Shield, Slow Time, Double Jump) | 90 coins, 3 gems |
| Fenn | run bonus | Rocket (Rocket, Magnet, Double Jump, Shield) | 100 coins, 5 gems |

All Commons follow the rarity rules of docs/44 section 4 (numbers: section 8); every (run coins, run gems) pair of the 13 Common heroes is distinct. Tier physics follow doc 44 sections 4 and 8 (no percent chain); run add-ons drawn by `Tools/tier_rows.py` (seed BaseSeed + line index + 1000 x tier, line index continues after Pip: Nib 9, Spike 10, Moss 11, Fenn 12; Capi is line 1). Nib and Fenn take the better of two draws for run coins and gems (their identity is the run bonus). Capi's Uncommon row is the existing constant. Run add-ons per tier:

| Id | Name | Tier | Run coins add | Run gems add |
|---|---|---|---|---|
| forestgirl_r | Acornplate Capi | Rare | +270 | +8 |
| forestgirl_e | Oakmantle Capi | Elite | +980 | +5 |
| forestgirl_l | Grovequeen Capi | Legendary | +1910 | +7 |
| nib_u | Leafscarf Nib | Uncommon | +90 | +3 |
| nib_r | Barkguard Nib | Rare | +230 | +9 |
| nib_e | Oakknight Nib | Elite | +810 | +5 |
| nib_l | Acornking Nib | Legendary | +1940 | +6 |
| spike_u | Logplate Spike | Uncommon | +50 | +3 |
| spike_r | Thornguard Spike | Rare | +220 | +8 |
| spike_e | Oakbrace Spike | Elite | +870 | +9 |
| spike_l | Quillking Spike | Legendary | +1970 | +8 |
| moss_u | Vinebelt Moss | Uncommon | +70 | +2 |
| moss_r | Shellplate Moss | Rare | +260 | +5 |
| moss_e | Ferncrown Moss | Elite | +850 | +6 |
| moss_l | Shellking Moss | Legendary | +1040 | +6 |
| fenn_u | Scoutvest Fenn | Uncommon | +100 | +4 |
| fenn_r | Coinguard Fenn | Rare | +290 | +10 |
| fenn_e | Goldcape Fenn | Elite | +960 | +10 |
| fenn_l | Foxking Fenn | Legendary | +1960 | +10 |

Nib's flat coin find (+20 coins per cleared level) is kept by every tier (not boosted by the chain). Hero physics of every tier: docs/44 section 8.

Region 2 progress hands out the four Commons (`RegionGoalTable.HeroGoalId`): `g_2_levels_2` = Nib, `g_2_levels_3` = Spike, `g_2_levels_4` = Moss, `g_2_stars_5` = Fenn, `g_2_levels_5` = Capi (as before). Art: Capi r2 / e1 / l1, Nib c1 / u2 / r1 / e1 / l2, Spike c2 / u1 / r2 / e1 / l1, Moss c3 / u2 / r1 / e1 / l1, Fenn c1 / u2 / r1 / e2 / l1, cut by the same `cut_region1_heroes.py` (table entries for Region 2; Fenn Rare and Elite use the rim trim mode because the white tail tip is the colour of the sticker rim, Fenn Legendary trim plus glow back piece, Capi Elite and Legendary and the Legendary tiers of the others the glow mode). QA in `Tools/art_pipeline/previews/region2_heroes_qa.txt`: overlap 0 px and 0 px touching the canvas edge on all 23; grey edge 0.06 to 3.6 percent, Nib Legendary 8.0 (soft glow, checked visually) and Fenn Legendary 5.2. No whole-body fallback; the tails (Nib, Fenn), Moss's shell and Spike's quills stay in the normal parts (the parts stack to the painting exactly and are static, so no separate back part was needed except the glows).
