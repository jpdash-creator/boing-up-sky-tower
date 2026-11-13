# 08 - Rules Addendum (v2) - supersedes conflicting parts of docs 01, 02, 05

Source: the nine additional rules from the product owner, plus the clarifying answers.

> **2026-11-01 (economy and gear update):** all coin, gem, price, reward, chest and revive numbers in this document were re-checked against the code (build v0.38.0.40). The authoritative economy tables, with gems, coins and dollars for every item, are in [27-economy-tables-from-code.md](27-economy-tables-from-code.md); docs 20, 22 and 26 are the owner sources. Rules now in force: **1 gem = 20 coins (fixed)**, **no gear anywhere**, Iron and Gold chests, a Weekly/Monthly Subscription instead of VIP tiers, revive 5 then 10 gems. Sections 7 and 8 and the economy blocks at the end of this file were rewritten; leaderboards, scroll, jump numbers and the map are unchanged.

> **v2.1 patch:** chest USD value, VIP row and per-region coin totals corrected; docs 02-05 aligned to this doc. The decisions raised by this review (difficulty cap, endless gems and coins, scoring, module scope, etc.) are recorded in [09-decisions-needed.md](09-decisions-needed.md) and applied in this doc.

> **Update 2026-10-20:** the owner's device-test rounds (2026-10-19/20) changed several rules. They are recorded in the last sections of this document (device-test rules, scroll model, jump numbers, economy v2, world map, level screen) and **win over the older text above them**: section 4 gap numbers, section 6 star wording and section 7 in-level coins carry short "superseded" notes. Decision log: game repo `.claude/pendingdecisions/device-feedback-2026-10-19.md`.

## 0. Rule-to-decision map
| # | Rule | Decision |
|---|---|---|
| 1 | Leaderboards for campaign and endless | Section 1 |
| 2 | Endless gets faster | Section 2: +4 percent every 25 floors, capped at +150 percent (the campaign scroll model is separate, see "Scroll model") |
| 3 | Similar mechanics, brand-new feel, "dazzle" | Section 3: neon glow + juicy particle FX |
| 4 | Random platform generator; wider platforms on easy levels | Section 4 |
| 5 | Same mechanics across regions, regions are skins | Section 5 (with the owner's clarification: signature mechanics per region sit on top of the shared core) |
| 6 | All 30 regions, 25 levels each (doc 45; regions 6 to 30 still carry 15 levels until they are migrated), every 5th level a special goal, levels 16 to 25 complex | Section 6, doc 45 |
| 7 | Coins per level, bigger on 5th, biggest on 15th, some gems | Section 7 |
| 8 | Standard coins : gems : USD ratio | Section 8 (proposal) |
| 9 | Generic jumper, no reference to any existing game | Section 9 |

## 1. Leaderboards (rule 1)
> **Update 2026-11-12 (owner):** the leaderboard actually built is the weekly CROWN leaderboard: three boards (overall, levels, endless), crown = (floors x 10 + combo^2 x 5) x (1 + setting percent), best crown per slot per week, week = Monday 00:00 India time, Firebase + a replay-verifying Cloud Run service, prizes claimed in Rewards. Everything counts (revived runs, power-ups, the player's own rolled hero stats); there is no neutral Ranked mode. The table below (PlayFab, friends boards, Ranked-only) is the old plan and is history. Source of truth: `Planning/LEADERBOARD-DESIGN.md`.
| Board | Scope | Ranking |
|---|---|---|
| Campaign - per level (30 x 15 = 450) | **Friends** board per level (+ top-100 global for levels 5, 10, 15 only) | Score, then time |
| Campaign - per region | Global + friends | Sum of best level scores (and stars) |
| Campaign - total | Global + friends | Total stars, then total score |
| Endless - per region (30) | Global + friends, all-time and weekly | Highest floor, then score |
| Endless - overall | Global | Best floor across regions |
| Daily challenge | Global | Score |

Why per-level boards are friends-only: 450 global boards are costly and rarely read. The per-level best is stored in player data and compared with friends client-side; the three milestone levels per region get real global boards. **(Verify PlayFab statistic/leaderboard count limits before final design; fall back to a single keyed-score table if needed.)** Only Ranked runs (no consumables) count on global boards. Campaign runs are Casual by default (consumables allowed); they credit per-level friends boards (flagged by mode) but not the milestone global boards unless played Ranked (D6). Friends boards use server-validated stats, not client-side compares of other players' data (D18). **Score** = floors x 10 + combo bonus (combo^2 x 5) + coins x 1, and **floor** = platform row index (D5); both are prototype values to revisit after Phase 1.

## 2. Endless speed ramp (rule 2)
- Every **25 floors**, speed multiplier += **4 percent of base**, a "Speed up!" banner flashes.
- Cap: **+150 percent** (reached around floor 940), then constant.
- The multiplier scales the **tower scroll speed and hazard tempo fully, and the player's max run speed at half rate** so control stays fair. Jump physics (gravity, impulse formula) are not scaled.
- Constants live in Remote Config: `rampFloors=25`, `rampPercent=4`, `rampCap=150`, `playerSpeedShare=0.5`.
- Campaign levels do not ramp except where a level's definition says so (the special-goal levels 5, 10, 15 and 20 may ramp inside the level).

## 3. Look and feel: "neon dazzle" (rule 3)
Same fundamental mechanics as the genre, but a fresh, shareable presentation.
- **Visuals**: glowing neon rim-lights on character, platforms and pickups; bloom; gradient skies; colour-shifting platforms; clean cute chibi shapes stay readable.
- **Juice**: speed-streak trails that grow with combo, shockwave ring on big jumps, chromatic-aberration flash on combos 5/10/20, screen pulse to the music beat, confetti bursts at milestones, slow-motion beat on level finish, haptics.
- **Audio**: music that layers in stems as combo rises; pitch-rising pickup chimes.
- **UI**: glass/neon panels, animated rarity foils (Legendary items shimmer).
- **Signature visual**: a "light trail" ribbon following the player, with a colour of its own, that makes every screenshot and clip distinctive.
- Performance guard: bloom and particles scale by quality tier; low-end devices use baked glow sprites rather than post-processing.
- Accessibility guard: chromatic-aberration flashes, beat pulses and colour-shifting platforms must be disabled by the reduced-motion option, stay below photosensitivity thresholds, and platform types must keep a non-colour cue.
- Asset impact: STYLE LOCK in doc 04 becomes (doc 04 now carries this, with key colour #808080): `cute chibi cartoon with neon rim light and glow, thick dark-hue outline, saturated gradient colors, glossy sparkle accents, solid flat background`.

## 4. Random platform generator (rule 4)
Seeded, deterministic (same seed gives the same level on every device). Inputs: `difficulty d` in 0..1 and level type.

| Parameter | Easy (d=0.05) | Hard (d=1.0) | Notes |
|---|---|---|---|
| Platform width (player = 1 unit) | 6.0 - 8.0 | 1.6 - 2.4 | Linear blend with random jitter of +/- 15 percent |
| Vertical gap | 2.0 - 3.0 | 3.8 - 5.2 | Always below max jump height x 0.9 |
| Horizontal offset between rows | 0 - 40 percent of screen | 20 - 90 percent | Needs wall bounce at high d |
| Special platforms share | 0 percent | up to 40 percent | moving, crumble, bounce, vanishing, sticky, slippery |
| Platforms per row | 1 - 2 | 1 | |

*Update 2026-10-20: the vertical gap in the table is superseded. Normal gaps are capped at 4.2 u (owner jump retune, see "Jump numbers"), and gap and sideways reach shrink with platform width so that one jump always works (full gap from 3.2 wide, 55 percent at 1.5 wide). The row pattern of a level is a fixed easy ladder first, then a new pattern every 6 to 10 rows (ladder, dense patches of small platforms, zigzag).*

Rules: a **solvability check** guarantees that every platform is reachable from the previous one using the player's max speed and jump formula; bot validation in CI runs every level seed. Easy levels place wide platforms and coins on the path; hard levels narrow them and put coins on risky routes. Chunks (hand-authored patterns) are mixed in at L5/L10/L15 so complex levels feel designed, not random. Endless uses the same generator with `d = min(1, floor / 400)` (D16). Screen width is about 10 world units in portrait (D5), so easy platforms (6-8) span most of the screen. Specials are region-weighted in code (D12, `RegionPlatformSets` and `SpecialPlatforms.Options`; the old `platformWeights` data field was removed 2026-11-10): a region's signature platform is listed twice among the options so it is the most common, next to the generic kinds. Authored chunks (D15): one shared library of about 12 chunks per class (complex, highly complex), reused across regions with re-skinning, not 60 unique sets.

## 5. Regions as skins plus signature mechanics (rule 5, as clarified)
- **Shared core (never changes by region)**: movement, jump formula, wall bounce, combo, scroll rule, coin/gem system, power-up rules, random generator.
- **Skinned by region**: background, tileset/platform art, power-up icon skins, particle palette, music, props, UI accent colour.
- **Signature mechanic per region**: one extra rule layered on the core (table in section 6). It is implemented as a reusable **module** from a shared library (ice, wind, lava, etc.). Modules are written once and reused or recombined by later regions, so adding a region is mostly data plus art. **Scope note (D7):** regions 1-30 need about 28 distinct modules, and all 30 are built before v1.0 if the schedule allows, so locked regions unlock without an app update. If the schedule slips, ship the 20 live regions' modules and plan one app update before region 21. "No store update" applies from region 31 onward, and only when reusing existing modules.
- The owner's wording ("randomness, power-ups, background are skinned; mechanics are unique per region") is respected: skinning covers presentation, and the signature mechanic gives each region a distinct feel without changing core controls.

## 6. All 30 regions (rule 6)
25 levels each for the five open regions (doc 45, owner Bug List 6, 2026-11-09); regions 6 to 30 keep 15 levels until they are migrated with the same rules. **Levels 5, 10, 15 and 20 carry the special goals; levels 16 to 25 are the complex class and level 25 is the highly complex reach-the-top level.**

| # | Region | Skin / palette | Signature mechanic (module) | Status |
|---|---|---|---|---|
| 1 | Meadow Springs | Green, pink, sky blue | None (tutorial), bounce flowers | Live |
| 2 | Mushroom Grove | Purple, teal | Bounce-cap platforms (high launch) | Live |
| 3 | Sunny Beach | Gold, aqua | Drifting platforms (horizontal sway) | Live |
| 4 | Candy Canyon | Pink, mint | Sticky gum (reduced jump) | Live |
| 5 | Bamboo Temple | Jade, red | Swinging gongs (knockback) | Live |
| 6 | Frosty Peaks | Ice blue, white | Slippery platforms (low friction) | Live |
| 7 | Autumn Orchard | Orange, brown | Wind gusts (push left/right) | Live |
| 8 | Desert Oasis | Sand, turquoise | Crumbling sand platforms | Live |
| 9 | Pirate Cove | Navy, gold | Cannonballs from the sides | Live |
| 10 | Toy Factory | Primary colours | Conveyor platforms | Live |
| 11 | Haunted Manor | Violet, green glow | Vanishing/ghost platforms | Live |
| 12 | Crystal Caverns | Cyan, magenta | Reflective crystal bounce (angled rebound) | Live |
| 13 | Volcano Forge | Red, charcoal | Rising lava + fire jets | Live |
| 14 | Jungle Ruins | Emerald, amber | Vine swing (grab and release) | Live |
| 15 | Neon City | Electric blue, pink | Laser gates (timed) | Live |
| 16 | Cloud Kingdom | White, lavender | Dissolving clouds + lightning flash | Live |
| 17 | Steampunk Skyport | Brass, teal | Steam vents (upward lift) | Live |
| 18 | Deep Sea Reef | Deep blue, coral | Low-gravity water currents | Live |
| 19 | Space Station | Black, cyan | Zero-g zones (floaty jumps) | Live |
| 20 | Moon Carnival | Rainbow neon | Trampolines + rotating wheels (mix) | Live |
| 21 | Dragon Peak | Crimson, gold | Fire-breath sweeps | Locked |
| 22 | Pyramid Sands | Gold, lapis | Sliding stone doors | Locked |
| 23 | Viking Fjord | Steel blue, frost | Drifting ice floes + gusts | Locked |
| 24 | Wild West Mesa | Terracotta, sunset | Tumbleweeds + rolling barrels | Locked |
| 25 | Dino Valley | Lime, orange | Stomp quakes (platform shake) | Locked |
| 26 | Storybook Castle | Pastel royal | Drawbridge platforms (tilting) | Locked |
| 27 | Arcade Realm | Pixel neon | Pixel-grid snap platforms (blinking) | Locked |
| 28 | Samurai Dojo | Ink, cherry | Falling blades with warning marks | Locked |
| 29 | Alien Jungle | Glow green, violet | Gravity pods (flip pull) | Locked |
| 30 | Rift of Time | Chrono violet, white | Rewind zones (position echo) | Locked |

Locked regions appear on the map as silhouettes with a lock and "coming soon". Their signature modules can be built at any time, because the data model carries them already.

### Level structure (applies to every migrated region, doc 45 is the source)
The old 15-level template (d from 0.05 to 1.00 per level, the L5/L10 "complex" and L15 "highly complex" rows) is retired for regions 1 to 5. Every level now has a measured difficulty score from 0 to 100 (doc 45 section 2) and a target score:

| Level | 1 | 2 | 3 | 4 | 5 | 6-10 | 11-15 | 16-20 | 21-25 |
|---|---|---|---|---|---|---|---|---|---|
| Target score | 5 | 20 | 26.7 | 33.3 | 40 | 44 to 60 (+4 per level) | 63 to 75 (+3 per level) | 77.5 to 87.5 (+2.5 per level) | 90 to 100 (+2.5 per level) |
| Class | Simple (1-3), Medium (4-8), Hard (9-15), Complex (16-25) | | | | | | | | |

- **Special-goal levels:** 5, 10, 15 and 20 (combo chain, no wall bounce, hidden gem, speedrun, set per region). Level 25 is the highly complex reach-the-last-platform level.
- **Specials are planned from level 1.** The share of rows with a special platform ramps inside each level (`SimConfig.RampLowPercent/RampHighPercent`: level 1 from 10 to 30 percent of rows, rising in steps of 5 to level 25 at 50 to 70 percent); hazard-type specials (Thorn, Snap) are only left out of level 1. The old "level 1 and 2 have no specials, level 3 introduces them" rule is gone.
- **Rows per level** (60 / 75 / 90 / 105 / 120 / 150 by band) are in doc 45 section 4a. The builder difficulty d of each level is solved per region to meet its target score (`Content/config/difficulty_curve.json`).

Region difficulty offset: +0.0 (region 1) to +0.25 (region 30). It is used in **one place only**: the per-level calibrated builder difficulty d is turned into the effective difficulty `d_eff = d + offset * (1 - d)` (`LevelGenerator.EffectiveDifficulty`), which compresses toward 1.0 instead of clamping (decision D1), so later regions are harder at equal level number. The offset does **not** scale hazard tempo, gap jitter or scroll speed separately: those follow from d_eff through the generator and the scroll model only. The retired per-region `platformWeights` data field was removed from the region files on 2026-11-10. *Stars (superseded 2026-10-20): each level has 3 collectible stars, see "Economy v2".*

## 7. In-level coin and gem rules (rule 7)
> **Data note (2026-11-10):** the level fields `stars` (clear / coin target / no revive entries), `rewards.coinPayout`, the region field `platformWeights` and `remote_config` `economy.regionCoinScalePercent` were dead and are removed from the content, the schemas and the loader; stars are the 3 collectible pickups only and the payout is the Economy v3 formula.
> **Superseded 2026-10-20 by "Economy v2" (end of this document):** coins are no longer collected in a level (no landing coins, no floating coins, no jump-height coins). Coins are paid at the end; gems are pickups with rarity tiers. The tables below stay as history and for the endless rules, which are unchanged. **Updated 2026-11-01:** the end-of-level payout is now 70 + 60 per star + 5 per combo step (see "Economy v3" at the end of this document); the 60-900 coin payouts below are history. Endless gem drops (floors 250/500/1,000) are specified but **not built**.

**Coin sources inside a level**
- Landing on a new platform: +1 coin (+2 if the landing continues a combo).
- Floating coins on the path and risky side-routes (placed by the generator, more on hard routes).
- Jump-height bonus: +1 coin per extra floor of a multi-floor jump.
- End-of-level payout (base + star bonus), multiplied by region factor.

**Per-level coin payout (end-of-level base, region 1; scales +3 percent per region index)**
| Level | Base payout | Typical in-level pickups | Gem reward |
|---|---|---|---|
| 1-4, 6-9, 11-14 | 60 - 140 (rises with level) | 40 - 120 | 0 (rare 1-gem pickup, 3 percent chance per level) |
| **5, 10** | **300** | 120 - 200 | **25 percent chance of 1-3 gems** (guaranteed 1 on first-ever clear of L10) |
| **15** | **900** | 250 - 400 | **Guaranteed 5 gems on first clear, plus 0-5 bonus by stars; 2 on replays 50 percent** |

Replays (history, now 50 percent once the level has 3 stars) paid 40 percent of the coin payout; gem rewards are first-clear (milestone levels also give a small replay chance, as shown). Endless: +1 coin per 2 floors, +50 coin bonus every 100 floors (implemented 2026-10-20 as `floors/2 + 50 x (floors/100)` times the region coin factor, rounded half up; the cap counts paying runs per UTC day); the per-landing coin rule does **not** apply in endless (D3). Endless coin income is capped per day at about 3 runs' worth at full rate (cap value is a Remote Config constant), then halves. Gem drop at floors 250, 500, 1000 (once per run each): each gem threshold pays **once per account per day**, not per region (D2). This adds about 1-3 gems/day for skilled players on top of the income model above.

## 8. Currency ratio: coins : gems : USD (rule 8) - REWRITTEN 2026-11-01 from the code
> **2026-11-01:** this section was rewritten to match the shipped economy (build v0.38; the numbers, with coins, gems and dollars for every item, are in [27-economy-tables-from-code.md](27-economy-tables-from-code.md), which wins). Older text (100 coins : 1 gem, a 6-pack ladder from 80 gems, outfit/boots/hat prices, Bronze/Silver/Gold chests, VIP, battle pass, Remove Ads) is removed. There is **no gear** in the game (doc 20, 2026-10-24).

### Exchange anchors
- **1 gem = 20 coins, fixed** (owner, doc 22). The coin packs keep this rate for the four smallest packs (20 coins per gem) and give a small volume bonus above it (22.2, 22.7 and 25 coins per gem for the 20,000, 50,000 and 100,000 packs). Coins never buy gems.
- **Dollars:** $1 = 95 rupees (doc 22). A gem costs $0.0158 in the 50 pack, **$0.01099 in the 1,000 pack (the reference rate)** and $0.0080 in the 15,000 pack (list prices; the launch promo and the first-purchase double are in the code and OFF by default). One coin is worth $0.00055 at the reference rate.
- Equivalently at the reference rate: **$1 = 91 gems = 1,820 coins**.

### Gem packs (money, list prices; owner table doc 22 4.1)
| Gems | USD | INR | Gems per $ | Promo USD (off) | First-purchase double |
|---|---|---|---|---|---|
| 50 | 0.79 | 75 | 63.3 | 0.49 | yes |
| 100 | 1.49 | 142 | 67.1 | 0.79 | yes (badge Popular) |
| 200 | 2.49 | 237 | 80.3 | 1.29 | yes |
| 500 | 5.99 | 569 | 83.5 | 2.99 | yes |
| 1,000 | 10.99 | 1,044 | 91.0 | 5.49 | yes (badge Best value) |
| 2,000 | 20.99 | 1,994 | 95.3 | 10.49 | no |
| 5,000 | 49.99 | 4,749 | 100.0 | 24.99 | no |
| 10,000 | 89.99 | 8,549 | 111.1 | 44.99 | no |
| 15,000 | 119.99 | 11,399 | 125.0 | 59.99 | no |

### Coin packs (paid with gems; owner table doc 22 4.2)
| Coins | Gems (list) | Coins per gem | Gems (promo, off) |
|---|---|---|---|
| 1,000 | 50 | 20.0 | 25 |
| 2,000 | 100 | 20.0 | 50 |
| 5,000 | 250 | 20.0 | 125 |
| 10,000 | 500 | 20.0 | 250 |
| 20,000 | 900 | 22.2 | 450 |
| 50,000 | 2,200 | 22.7 | 1,100 |
| 100,000 | 4,000 | 25.0 | 2,000 |

### Free-player income (measured by `Tests/Simulation/ECONOMY_TABLES.md`, build v0.38)
- Coins per region, 15 levels at 3 stars, no combo: **3,750 / 3,870 / 3,975 / 4,095 / 4,200** (regions 1 to 5, factors 1.00 to 1.12); 5 regions **19,890 coins** (994.5 gem-equivalents).
- Gems per region: about 74 to 79 gem value placed on 40 pickups plus 6 to 7 milestone gems; at 60 percent pickup about **51 to 54 gems per region, 260 for the 5 regions** (146 at 30 percent, 413 at 100 percent). Replays of a 3-star level drop no gems.
- Rewards screen (doc 26 section 4, tables in doc 27 sections 11 to 13): Main (star milestones and region cleared) **580 gems, 14,500 coins, 11 random power-ups** once; Daily (100 points) **45 gems, 1,500 coins, 1 random power-up per day**; Weekly **160 gems, 1,800 coins, 1 random power-up per week**. A random power-up is worth 60.7 gems on average.
- Time: about 3.4 minutes a level (assumption), a region 41 to 56 minutes, the campaign about 4.3 hours: about **61 gems and 4,650 coins per hour** of first-clear play.

### Item prices (as shipped)
| Item | Coins | Gems | USD at reference rate |
|---|---|---|---|
| Double Jump, Wall Stick, Shield, Magnet | 1,000 | 50 | $0.55 |
| Flight, Rocket, Slow Time | 1,500 | 75 | $0.82 |
| 5 power-ups of one type | -10 percent | -10 percent | |
| 10 or more power-ups | -20 percent | -20 percent | |
| Iron chest (open 1 / open 10) | - | 100 / 1,000 | $1.10 / $10.99 |
| Gold chest (open 1 / open 10) | - | 300 / 3,000 | $3.30 / $32.97 |
| Revive | - | 5 (first), 10 (second), max 2 per run; the first can be a free rewarded-ad revive | $0.05 / $0.11 |
| Region skip (regions 2 to 5) | - | 100 / 116 / 133 / 150 | $1.10 to $1.65 |
| Bundle (1,000 generated bundles) | - | 200 to 500 (average 364; value 1.41 to 1.79 times, average 1.60) | $2.20 to $5.50 |
| Bundle Refresh | 100 | 5 | $0.05 |
| Characters (not for sale yet) | - | owner list 150 / 300 / 700 / 1,500 (Uncommon to Legendary) | |
| Weekly Subscription | - | - | **$1.49** (Rs 149), 7 days |
| Monthly Subscription | - | - | **$4.99** (Rs 449), 30 days |

Chests: Iron = 1 random power-up + 400 to 600 coins (average value 85.7 gems, 86 percent of the price); Gold = 3 random power-ups + 1,000 to 1,400 coins (242.1 gems, 81 percent). Characters in chests are switched off. **No chest is earned from play or rewards.**
Subscriptions: Weekly pays 100 gems at once, then 10 gems + 500 coins a day for 7 days; Monthly pays 200 gems at once, then 10 gems + 600 coins a day for 30 days and 1 random power-up on every 5th claim. There are no VIP tiers and no stat bonuses.
Not in the game: gear (hats, shirts, pants, boots, outfits), Bronze/Silver chests, battle pass, Remove Ads, interstitials, starter pack, daily login streak. Doc 22 point 7 describes a future starter pack ($0.99 = 100 gems + 2,000 coins).

### Targets to confirm in soft launch
- Payer conversion 2 percent and average spend of a payer $15-25 (assumptions). Money products are only the 9 gem packs and the 2 subscriptions.
- All prices are content/Remote Config values and can be re-tuned without an update. **These numbers must be validated with real playtest data.**

## 9. Generic-game / legal guardrails (rule 9)
- No mention, logo, character, level layout, UI or sound of any existing jumper game anywhere: code, art, prompts, store listing, keywords, screenshots, marketing copy, social posts.
- Do not use another game's name as an ASO keyword or in prompts to image tools.
- The shared mechanics (momentum jump, combo, scrolling tower) are generic genre mechanics; our implementation, physics numbers, art, theme, names and UI are original.
- Pre-launch checklist: trademark search for the final name and mascot, reverse image search on key art, review store listing text for competitor names, document the original design intent (this folder acts as the design record).
- Keep a prompt log with no competitor names, to demonstrate clean-room creation.

## Owner device-test rules (2026-10-19)
- Controls: three always-visible buttons (Left, Right bottom-left; Jump bottom-right; mirrored by the left-handed setting).
- Scrolling: see "Scroll model" below (the first version, steps of 5 to 25 percent, was replaced on 2026-10-20 by steps of 20 to 100 percent). Five difficulty levels in Settings (dropdown) also set the share of spots that carry a gem (2026-11-01: now 2.7, 2.7, 2.9, 3.0 and 3.2 percent per row, `SimConfig.DifficultyGemPermille` 27/27/29/30/32; the first version was 5 to 10 percent). In-app purchases may later sell gem chance. RISK: review the gem inflow against the economy (docs 05, 09, 27).
- No timed goals and no time star. Revive costs 5 gems, then 10 (2026-10-25, was 2 then 5), at most twice per run; the first can be a free rewarded-ad revive.
- Levels are longer (reach-floor goals doubled) and use patterns: the first 4 rows are a fixed easy ladder, then every 6 to 10 rows the pattern changes (ladder, dense patches of small platforms where about one in four extras crumbles, or a zigzag).
- The goal is a galaxy portal above the last platform; reaching it flies Momo into the swirl, then the results show.

## Economy v2 (owner, 2026-10-20)
- No coin pickups. Each level has 3 collectible stars (side routes and risky spots) and occasional gems (rarity tiers Common 1, Rare 2, Epic 5, Legendary 10 gems at 70/22/7/1 percent).
- Coins are paid when the level is cleared: **70 for the clear + 60 per star collected + 5 per combo step**, times the region coin factor (economy v3, 2026-10-25; v2 was 30 + 40).
- After a level has 3 stars, replays pay 50 percent of the coins and drop no gems. Levels below 3 stars pay in full on replay (risk: replay farming; review with the economy check).
- Narrow platforms get smaller gaps (full jump height from 3.2 wide, 55 percent at 1.5 wide).

## Scroll model (owner, 2026-10-19/20; supersedes the earlier "rising danger" wording)
- The screen scrolls up on its own **after the first jump**: platforms and background slide down, the camera never waits for the player. The bottom edge of the screen is the danger line; falling below it ends the run (revive offer, then retry). There is no red band, hazard or kill zone coming from below.
- **Base speed** is 120 percent of the difficulty-blended speed (`ScrollBasePercent` 120; the original "+20 percent" request).
- **Steps:** every 5 seconds the speed rises by the difficulty's step. The five difficulty levels (Settings dropdown) use steps of **20, 40, 60, 80 and 100 percent** per 5 s (owner, list 8: "20 percent factors instead of 8 percent"; the first version used 5/10/15/20/25). The total is capped at **+400 percent**.
- Every step shows a visible **"SPEED UP +N%"** cue (with the SpeedUp sound, doc 12; the cue is its own visual twin for CUX-6).
- Gem chance per difficulty level: 2.7, 2.7, 2.9, 3.0 and 3.2 percent of rows (2026-11-01, code values; history: 5 to 10 percent). Later in-app purchases may sell extra gem chance.
- Campaign boss-chase levels use the endless step formula clamped by `CampaignRampCapPercent` (60 percent); endless keeps its own ramp (section 2). Endless mode (an **Endless button on each region's level screen**, owner decision 2026-10-20) is being implemented by another work stream; its final rules will be recorded here and in `Assets/_Game/Simulation/NOTES.md`.
- **Update 2026-11-12 (owner): the step table is now 10, 20, 25, 40, 50 percent per 5 s for Relaxed, Easy, Normal, Hard, Insane (`SimConfig.DifficultyStepPercent`); the lines above that name 20/40/60/80/100, 20/22/25/28/30 or +400 percent are history. Owner 2026-11-12 (second decision): there is NO global percent cap; the speed-up stops after 8 steps (`SimConfig.DefaultScrollStepMaxSteps`, 40 s), so the top speed-up is step x 8 = 80, 160, 200, 320, 400 percent for Relaxed .. Insane (`SimConfig.TopSpeedUpPercent`, set as `ScrollStepCapPercent` by `GameSession`). The separate campaign speed ceiling `ScrollMaxSpeed` (4 u/s) still applies (doc 45 section 7).**
- Code: `SimConfig.ScrollBasePercent` (120) and `ScrollStepSeconds`; the step table `SimConfig.DifficultyStepPercent` = {10, 20, 25, 40, 50} (`GameSettings.DifficultyStepPercent` is the same array) and the cap `ScrollStepCapPercent` = `SimConfig.TopSpeedUpPercent(mode)` = step x 8 (80, 160, 200, 320, 400), set in `GameSession` (since 2026-11-12; the earlier values {20,40,60,80,100} and 400 are history; the `SimConfig` field defaults are only the unit-test baseline). The bot proves every level under the scroll.

## Jump numbers (owner retune, lists 6 and 7, 2026-10-20)
| Value | Number |
|---|---|
| `JumpBase` | 19 u/s (history: 14, then 21, then 19) |
| `JumpSpeedFactor` | 0.41 (history: 0.85, then 0.58) |
| Standing jump peak | 4.51 u = **1.3 median row gaps** (median normal gap 3.48 u) |
| Half-speed jump peak | 5.43 u |
| Full-speed jump peak (22.69 u/s launch) | 6.44 u |
| Normal vertical gap cap (`NormalGapMax`) | 4.2 u, so a standing jump always reaches the next platform with 0.3 u margin |

- Hero physics and the rarity rules: see docs/44-power-mechanics-bible.md. The jump numbers above are the default hero (Momo Common), see doc 44 section 3.
- Layouts are sized from a separate reference jump (`LayoutJumpBase` 14 u/s, `LayoutJumpSpeedFactor` 0.85) whose reach is never larger than the real one; see `Assets/_Game/Simulation/NOTES.md` for the contract and its tests.
- Narrow-platform caps, sticky platforms (next gap at most 2.2 u), bounce pads and the combo ladder keep their own caps.

## Pickups and feedback (lists 6 and 8, 2026-10-20)
- **Star pickup fix:** the view used to lift stars and gems onto nearby platforms, so the drawn star was up to 1.5 u away from where the simulation collects it and the player "had to cross the star twice". Now pickups are drawn at their simulation position, the builder keeps stars and gems clear of every platform slab, and collection is a **swept test** of the player's path between two ticks against the pickup circle (a fast pass cannot skip a pickup). Radius unchanged.
- Pickup effects are drawn **at the pickup position**: a gem shows a gem, a star shows a star, never a star at the character.
- Feet and pickups stand on the walkable surface line of a platform (top of the grass, not the neon rim); nothing collectible sits inside a platform, only on a platform top or in clear air.

## Economy v2 details (owner, 2026-10-20; extends the "Economy v2" section above)
- 3 collectible **stars** per level on side routes and risky spots; the stars earned are the level's grade (0 to 3). The old star types (clear, coin target, no revive, time) and the CollectCoins goal type are retired.
- End payout: **70 coins for the clear + 60 per star collected + 5 per combo step**, times the region coin factor 1.00, 1.03, 1.06, 1.09, 1.12 (a 3-star clear pays 250 coins in region 1 and 280 in region 5 before combo; 0 stars 70, 1 star 130, 2 stars 190).
- **Gem rarity:** Common 1, Rare 2, Epic 5, Legendary 10 gems at 70/22/7/1 percent per gem spot. The difficulty setting raises the share of gem spots (2.7 to 3.2 percent of rows). Milestone gems on first clears: L5 and L10 roll 1 to 3 gems (L10 guarantees 1), L15 pays 5 plus a 2-gem bonus, level 8 pays its hidden gem (up to 2).
- **Replay rules:** once a level has 3 stars, replays pay 50 percent of the coins and drop no gems. Levels with fewer than 3 stars keep full rewards on replay. A failed run pays nothing and loses the gems collected in it; a revive is the only way to keep them.
- **Revive:** 5 gems the first time, 10 gems the second time, at most 2 per run; one free revive by rewarded ad (first revive only, LevelPlay test ads until the owner supplies the key).
- Data mismatches fixed in RC 1 (2026-11-01): `Content/config/remote_config.json` `coinsPerGem` is now 20, `vipGemsPerDay` was deleted, and the generator and config both say `replayCoinPercent` 50 (the per-level field was deleted; docs 09, 27 section 17).

## Economy v3 (owner issue lists 24 to 28, 2026-10-25 to 2026-10-31; numbers in doc 27)
- Coins at the end of a level: clear 70 + 60 per star + 5 per combo step, times the region factor. Replays pay 50 percent only once the level has 3 stars (and drop no gems); below 3 stars full pay (replay farming is an open item).
- Shop: five tabs Bundles, Chests, Power-ups, Coins, Gems and a Subscription button (doc 26). Chests Iron/Gold for gems; power-ups for coins or gems; coin packs for gems; 9 gem packs for money.
- Rewards screen (home): Main (stars, regions), Daily (10 actions, 100 points), Weekly (11 tasks). No chests are granted (doc 22 open item 3).
- Characters (Momo, Sporeling) are free commons; the character shop is coming soon; **gear was removed on 2026-10-24 (doc 20)**.

## World map (owner, 2026-10-20)
- One scrollable vertical map with **all 30 regions**: **10 chained OpenArt segments of 3 regions each**, road-free, so the pins carry the route. Segments are not butted directly: a **procedural stream of clouds** (150 px band drawn in code, no OpenArt credits) separates them; joint positions are in `Assets/Resources/Map/world_map.json` (`cloudJoints`). The map is full screen behind a translucent top bar (about 20 percent opaque). Owner still to review the stitched look.
- The map screen scrolls and opens on the player's current region. Playable regions carry a **hand-placed pin** (regions 1, 2, 7 to 10 on the current art: 8 on the sand dune hump, 9 just above the red flag) plus a slim name tag beside it that never covers the art (a ribbon banner under each node was the first idea; translucent black boxes are not allowed). Regions 6 to 30 show a padlock and a "Coming soon" tag. Region numbers are not drawn on the nodes.
- Region order on the map follows the picture (for example Moon Carnival, Dragon Peak, Space Station from the bottom of that segment); content region order and the golden level hashes are updated after the chain is final.

## Level screen (owner, 2026-10-20)
- The title is centred in its own band. The wallet below it has two columns: **coins, gems, total stars | levels completed, region stars**. The 15-level grid fits the screen on every supported aspect without overlap (CUX-3). Each level opens an info popup (goal, stars, PLAY) with centred content and the display font.
- The level screen also hosts the region's **Endless button** (decision 2026-10-20, in implementation).
- Popups ("You fell!", level clear, pause) are compact cards: big display-font title, a round X icon (OpenArt, 15 credits, approved), a panel only as tall as its content, no big ribbon.

## Playable scope and testing switch (2026-10-20)
- The current build plays **5 regions x 15 levels** (Meadow Springs, Mushroom Grove, Sunny Beach, Candy Canyon, Bamboo Temple). The progress service has `playableRegionLimit` = 5; content still marks regions 6-20 live and 21-30 teaser.
- **"Test: all levels open"** (Settings, development builds only, default on) treats every level and region of the playable set as unlocked for testing. It is off in tests and release builds.
- Versioning: every build is vMAJOR.MINOR.BUILD (example v0.52.68; changed 2026-11-09 from vX.Y.Z.B, old build-log rows keep the old format) (game repo `.claude/rules/versioning.md`, `Tools/version.json`, build log `Content/builds/build_log.csv`).
- Fonts: Lilita One (display) + Nunito Bold (body).
