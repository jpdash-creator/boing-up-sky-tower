# 08 - Rules Addendum (v2) - supersedes conflicting parts of docs 01, 02, 05

Source: the nine additional rules from the product owner, plus the clarifying answers.

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
| 6 | All 30 regions, 15 levels each, every 5th complex, 15th highly complex | Section 6 |
| 7 | Coins per level, bigger on 5th, biggest on 15th, some gems | Section 7 |
| 8 | Standard coins : gems : USD ratio | Section 8 (proposal) |
| 9 | Generic jumper, no reference to any existing game | Section 9 |

## 1. Leaderboards (rule 1)
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
- Campaign levels do not ramp except where a level's definition says so (L5/L10/L15 may ramp inside the level).

## 3. Look and feel: "neon dazzle" (rule 3)
Same fundamental mechanics as the genre, but a fresh, shareable presentation.
- **Visuals**: glowing neon rim-lights on character, platforms and pickups; bloom; gradient skies; colour-shifting platforms; clean cute chibi shapes stay readable.
- **Juice**: speed-streak trails that grow with combo, shockwave ring on big jumps, chromatic-aberration flash on combos 5/10/20, screen pulse to the music beat, confetti bursts at milestones, slow-motion beat on level finish, haptics.
- **Audio**: music that layers in stems as combo rises; pitch-rising pickup chimes.
- **UI**: glass/neon panels, animated rarity foils (Legendary characters shimmer).
- **Signature visual**: a "light trail" ribbon following the player, coloured by the character's own trail (no equip slot; no gear), that makes every screenshot and clip distinctive.
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

Rules: a **solvability check** guarantees that every platform is reachable from the previous one using the player's max speed and jump formula; bot validation in CI runs every level seed. Easy levels place wide platforms and coins on the path; hard levels narrow them and put coins on risky routes. Chunks (hand-authored patterns) are mixed in at L5/L10/L15 so complex levels feel designed, not random. Endless uses the same generator with `d = min(1, floor / 400)` (D16). Screen width is about 10 world units in portrait (D5), so easy platforms (6-8) span most of the screen. Specials are region-weighted through `platformWeights` (D12): a region's signature platform appears at its full share, others stay at 0 until the region's difficulty tier allows them. Authored chunks (D15): one shared library of about 12 chunks per class (complex, highly complex), reused across regions with re-skinning, not 60 unique sets.

## 5. Regions as skins plus signature mechanics (rule 5, as clarified)
- **Shared core (never changes by region)**: movement, jump formula, wall bounce, combo, scroll rule, coin/gem system, power-up rules, random generator.
- **Skinned by region**: background, tileset/platform art, power-up icon skins, particle palette, music, props, UI accent colour.
- **Signature mechanic per region**: one extra rule layered on the core (table in section 6). It is implemented as a reusable **module** from a shared library (ice, wind, lava, etc.). Modules are written once and reused or recombined by later regions, so adding a region is mostly data plus art. **Scope note (D7):** regions 1-30 need about 28 distinct modules, and all 30 are built before v1.0 if the schedule allows, so locked regions unlock without an app update. If the schedule slips, ship the 20 live regions' modules and plan one app update before region 21. "No store update" applies from region 31 onward, and only when reusing existing modules.
- The owner's wording ("randomness, power-ups, background are skinned; mechanics are unique per region") is respected: skinning covers presentation, and the signature mechanic gives each region a distinct feel without changing core controls.

## 6. All 30 regions (rule 6)
15 levels each. **Levels 5 and 10 are complex. Level 15 is highly complex.**

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

### Level complexity template (applies to every region)
| Level | Class | d (difficulty) | Platform width | Hazards | Notes |
|---|---|---|---|---|---|
| 1 | Simple | 0.05 | very wide | none | Tutorial feel; region skin shown |
| 2 | Simple | 0.10 | very wide | none | |
| 3 | Simple | 0.15 | wide | signature mechanic introduced gently | |
| 4 | Simple | 0.22 | wide | signature mechanic | |
| **5** | **Complex** | 0.40 | medium | signature + hazard wave | Authored chunks mixed in; better rewards; gem chance |
| 6 | Medium | 0.35 | wide-medium | signature | Breather after L5 |
| 7 | Medium | 0.42 | medium | signature | |
| 8 | Medium | 0.50 | medium | signature + hazard | |
| 9 | Medium | 0.58 | medium-narrow | signature + hazard | |
| **10** | **Complex** | 0.70 | narrow | signature + waves | Better rewards; gem chance |
| 11 | Hard | 0.62 | narrow | all signature | Breather after L10 |
| 12 | Hard | 0.70 | narrow | | |
| 13 | Hard | 0.78 | very narrow | | |
| 14 | Hard | 0.86 | very narrow | | |
| **15** | **Highly complex** | 1.00 | tight, moving/vanishing mix | everything, timed gauntlet or boss chase | Many coins and gems |

Region difficulty offset: +0.0 (region 1) to +0.25 (region 30). It is applied as `d_eff = d + offset * (1 - d)` so it compresses toward 1.0 instead of clamping (decision D1): later regions are harder at equal level number, L14 never collapses onto L15, and L15 (d = 1.0) differs across regions through other levers (hazard tempo, gap jitter, scroll speed scaled by the same offset). *Stars (superseded 2026-10-20): each level has 3 collectible stars, see "Economy v2".*

## 7. In-level coin and gem rules (rule 7)
> **Superseded 2026-10-20 by "Economy v2" (end of this document):** coins are no longer collected in a level (no landing coins, no floating coins, no jump-height coins). Coins are paid at the end; gems are pickups with rarity tiers. The tables below stay as history and for the endless rules, which are unchanged.

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

Replays pay 40 percent of the coin payout; gem rewards are first-clear (milestone levels also give a small replay chance, as shown). Endless: +1 coin per 2 floors, +50 coin bonus every 100 floors (implemented 2026-10-20 as `floors/2 + 50 x (floors/100)` times the region coin factor, rounded half up; the cap counts paying runs per UTC day); the per-landing coin rule does **not** apply in endless (D3). Endless coin income is capped per day at about 3 runs' worth at full rate (cap value is a Remote Config constant), then halves. Gem drop at floors 250, 500, 1000 (once per run each): each gem threshold pays **once per account per day**, not per region (D2). This adds about 1-3 gems/day for skilled players on top of the income model above.

## 8. Currency ratio: coins : gems : USD (rule 8) - UPDATED by owner 2026-11-01
**Fixed rate: 20 coins : 1 gem, never changed** (owner, 2026-11-01; the earlier "100 coins : 1 gem : about $0.011" is retired). The gem price in dollars depends on the pack: **$0.015 to $0.099 per gem** (reference $0.03, the 200-gem pack at promo); $1 buys about 10 to 67 gems. Pack ladder and item prices below are proposals, tunable via Remote Config after soft launch. **There is no gear in the game (owner, 2026-11-01)**: gems and coins buy characters, power-ups, Power-up Chests, revives and region skips only.
Baseline: **USD**. Industry-typical casual-game structure: soft currency is plentiful and cheap, premium currency is scarce, and gem packs give volume discounts.

### Exchange anchors
- **1 gem = 20 coins**, for every coin pack and every item (coin price = 20 x gem price). The shop offers gems to coins only (doc 14 D-SHOP-3: 100 coins = 5 gems up to 10,000 coins = 500 gems); coins never convert to gems.
- **Gem price by pack (doc 14 section 5.1):** $0.015 (1000 gems promo) to $0.099 (10 gems list); reference **$0.03**. One coin is worth $0.00075 to $0.005 (reference $0.0015).
- **Ratio target: 20 coins : 1 gem : about $0.03 (reference).** Equivalently, **$1 : about 33 gems : 667 coins** at the reference price.

### Gem pack ladder (USD baseline)
> **Superseded 2026-10-20 by the owner tables in [14-shop-requirements.md](14-shop-requirements.md) section 5** (7 gem packs 10 to 1000 gems, launch promo, coin packs for gems at the fixed rate). The old ladder (80 to 10,500 gems for $0.99 to $99.99) is history only and removed here; read doc 14 for the current numbers. Summary: 10 gems $0.99 list / $0.49 promo, 20 / $1.99 / $0.99, 50 / $3.99 / $1.99, 100 / $7.99 / $3.99, 200 / $11.99 / $5.99, 500 / $19.99 / $9.99, 1000 / $29.99 / $14.99.

### Free-player income (why gems feel scarce)
> **Update 2026-10-20:** the campaign figures in this block were written before economy v2 and no longer match the shipped game. In regions 1-5 a level places about 10 gems (140 to 220 per region) and a 3-star clear pays a flat 150 to 168 coins (2,250 to 2,520 coins per region before combo). Gems are therefore far less scarce than "7-12 per region". Open owner decision (doc 09 section F). **2026-11-01:** coin values below are restated at 20 coins per gem.

- Campaign gems per region (one full clear, no replays): L5 and L10 about 0.25 x 2 x 2 = 1.0 expected, L15 = 5 to 10, rare pickups about 0.4, so about **7-12 gems per region** (old model; economy v2 gives about 120 realised).
- Coins per region (region 1, old model): about **4,300 coins**, which is **215 gems** at 20:1 (about $6.45 at the $0.03 reference): about 2,700 end-of-level payouts plus about 1,600 in-level pickups. Later regions scale +3 percent per region index.
- Daily login + quests + one rewarded ad: about 5 gems and 400 coins per day (= 20 gems), so about **35 gems and 2,800 coins per week** (2,800 coins = 140 gems; together about $5.25 at the reference price).
- Whole 20-region campaign gives roughly **200 gems and 110,000 coins** (= 5,500 gems at 20:1) to a full-clear free player, excluding endless income.

### Item price proposal (coins for commons, gems for premium; coin price = 20 x gem price)
No gear: the old outfit, boots and hat rows and the Bronze/Silver/Gold chest row are removed.
| Item | Coins | Gems | USD-equivalent (reference $0.03 per gem; range $0.015 - 0.099) |
|---|---|---|---|
| Common character (1 per region) | free | - | free |
| Rare character R1 to R5 | 4,000 - 8,000 | 200 - 400 (= coins / 20) | $6 - 12 ($3 - 40) |
| Epic character R1 to R5 | 9,000 - 17,000 | 450 - 850 | $13.50 - 25.50 ($6.75 - 84) |
| Legendary character R1 to R5 | - | 800 / 950 / 1,100 / 1,250 / 1,450 | $24 - 43.50 ($12 - 144) |
| Consumable power-up: Double Jump, Wall Stick, Magnet, Star Seeker | 160 | 8 | $0.24 |
| Shield, Slow Time, Spring Boots | 200 | 10 | $0.30 |
| Safety Net | 280 | 14 | $0.42 |
| Rocket | 320 | 16 | $0.48 |
| Flight | 400 | 20 | $0.60 |
| Power-up Chest Common | 280 | 14 | $0.42 |
| Power-up Chest Rare | 520 | 26 | $0.78 |
| Power-up Chest Epic | - | 45 | $1.35 |
| Power-up Chest Legendary | - | 85 | $2.55 |
| Coin Chests (all tiers) | not sold, earn-only | - | reward |
| Revive | - | 2 (first), 5 (second), max 2 per run (owner, 2026-10-19; was 20) | $0.06 - 0.15 |
| Region early unlock | - | 100 (region 2) to 400 (region 20) | $3 - 12 |
| Battle pass (premium) | - | - | $4.99 or $9.99 |
| VIP (six tiers, doc 15) | - | 0 to 25 gems per day by tier | $3.99 - 32.99 per month; weekly $1.49 - 10.99 |
| Remove Ads | - | - | $3.99 |

Check on VIP: the top tier's 25 gems a day is 750 gems a month, worth $22.50 at the reference price for $32.99; every tier gives 23 to 30 gems per dollar, below the 200-gem pack (33) and the 1000-gem pack (67), so VIP never undercuts packs. VIP extras are non-gear (crown name badge, profile frame, victory animation).

### Targets to confirm in soft launch
- Payer conversion 2 percent, ARPPU $15-25, and spend skewed to gems for characters and power-ups (about 60 percent) and pass/VIP (about 25 percent). With no gear, characters and consumables are the spend.
- Free player can buy about one Rare character per 1-2 weeks using coins (4,000 - 8,000 coins = 2 - 4 hours of first-clear play); Legendary characters only via gems (mostly paid).
- All prices are Remote Config values, so prices can be re-tuned without an update. **These numbers are a proposal and must be validated with real playtest data.**

## 9. Generic-game / legal guardrails (rule 9)
- No mention, logo, character, level layout, UI or sound of any existing jumper game anywhere: code, art, prompts, store listing, keywords, screenshots, marketing copy, social posts.
- Do not use another game's name as an ASO keyword or in prompts to image tools.
- The shared mechanics (momentum jump, combo, scrolling tower) are generic genre mechanics; our implementation, physics numbers, art, theme, names and UI are original.
- Pre-launch checklist: trademark search for the final name and mascot, reverse image search on key art, review store listing text for competitor names, document the original design intent (this folder acts as the design record).
- Keep a prompt log with no competitor names, to demonstrate clean-room creation.

## Owner device-test rules (2026-10-19)
- Controls: three always-visible buttons (Left, Right bottom-left; Jump bottom-right; mirrored by the left-handed setting).
- Scrolling: see "Scroll model" below (the first version, steps of 5 to 25 percent, was replaced on 2026-10-20 by steps of 20 to 100 percent). Five difficulty levels in Settings (dropdown) also set the share of spots that carry a gem: 5, 5, 7, 9 and 10 percent. In-app purchases may later sell gem chance. RISK: review the gem inflow against the economy (docs 05, 09).
- No timed goals and no time star. Revive costs 2 gems, then 5, at most twice per run.
- Levels are longer (reach-floor goals doubled) and use patterns: the first 4 rows are a fixed easy ladder, then every 6 to 10 rows the pattern changes (ladder, dense patches of small platforms where about one in four extras crumbles, or a zigzag).
- The goal is a galaxy portal above the last platform; reaching it flies Momo into the swirl, then the results show.

## Economy v2 (owner, 2026-10-20)
- No coin pickups. Each level has 3 collectible stars (side routes and risky spots) and occasional gems (rarity tiers Common 1, Rare 2, Epic 5, Legendary 10 gems at 70/22/7/1 percent).
- Coins are paid when the level is cleared: 30 for the clear + 40 per star collected + 5 per combo step, times the region coin factor.
- After a level has 3 stars, replays pay 50 percent of the coins and drop no gems. Levels below 3 stars pay in full on replay (risk: replay farming; review with the economy check).
- Narrow platforms get smaller gaps (full jump height from 3.2 wide, 55 percent at 1.5 wide).

## Scroll model (owner, 2026-10-19/20; supersedes the earlier "rising danger" wording)
- The screen scrolls up on its own **after the first jump**: platforms and background slide down, the camera never waits for the player. The bottom edge of the screen is the danger line; falling below it ends the run (revive offer, then retry). There is no red band, hazard or kill zone coming from below.
- **Base speed** is 120 percent of the difficulty-blended speed (`ScrollBasePercent` 120; the original "+20 percent" request).
- **Steps:** every 5 seconds the speed rises by the difficulty's step. The five difficulty levels (Settings dropdown) use steps of **20, 40, 60, 80 and 100 percent** per 5 s (owner, list 8: "20 percent factors instead of 8 percent"; the first version used 5/10/15/20/25). The total is capped at **+400 percent**.
- Every step shows a visible **"SPEED UP +N%"** cue (with the SpeedUp sound, doc 12; the cue is its own visual twin for CUX-6).
- Gem chance per difficulty level: 5, 5, 7, 9 and 10 percent of spots (max 10). Later in-app purchases may sell extra gem chance.
- Campaign boss-chase levels use the endless step formula clamped by `CampaignRampCapPercent` (60 percent); endless keeps its own ramp (section 2). Endless mode (an **Endless button on each region's level screen**, owner decision 2026-10-20) is being implemented by another work stream; its final rules will be recorded here and in `Assets/_Game/Simulation/NOTES.md`.
- Code: `SimConfig.ScrollBasePercent` (120) and `ScrollStepSeconds`; the step table `GameSettings.DifficultyStepPercent` = {20, 40, 60, 80, 100} and the cap `ScrollStepCapPercent` = 400 (set in `GameSession`; the `SimConfig` field defaults 4 and 50 are only the unit-test baseline). The bot proves every level under the scroll.

## Jump numbers (owner retune, lists 6 and 7, 2026-10-20)
| Value | Number |
|---|---|
| `JumpBase` | 19 u/s (history: 14, then 21, then 19) |
| `JumpSpeedFactor` | 0.41 (history: 0.85, then 0.58; docs aligned to the code 2026-11-01) |
| Standing jump peak | 4.51 u = **1.3 median row gaps** (median normal gap 3.48 u) |
| Half-speed jump peak | 5.43 u |
| Full-speed jump peak (22.69 u/s launch) | 6.44 u = **1.85 median row gaps** |
| Normal vertical gap cap (`NormalGapMax`) | 4.2 u, so a standing jump always reaches the next platform with 0.3 u margin |

- Hero physics and the rarity rules: see docs/44-power-mechanics-bible.md in the fluffy-climbers-sky-tower docs folder (section 3). The jump numbers above are the default hero (Momo Common).
- Layouts are sized from a separate reference jump (`LayoutJumpBase` 14 u/s, `LayoutJumpSpeedFactor` 0.85) whose reach is never larger than the real one; see `Assets/_Game/Simulation/NOTES.md` for the contract and its tests.
- Narrow-platform caps, sticky platforms (next gap at most 2.2 u), bounce pads and the combo ladder keep their own caps.

## Pickups and feedback (lists 6 and 8, 2026-10-20)
- **Star pickup fix:** the view used to lift stars and gems onto nearby platforms, so the drawn star was up to 1.5 u away from where the simulation collects it and the player "had to cross the star twice". Now pickups are drawn at their simulation position, the builder keeps stars and gems clear of every platform slab, and collection is a **swept test** of the player's path between two ticks against the pickup circle (a fast pass cannot skip a pickup). Radius unchanged.
- Pickup effects are drawn **at the pickup position**: a gem shows a gem, a star shows a star, never a star at the character.
- Feet and pickups stand on the walkable surface line of a platform (top of the grass, not the neon rim); nothing collectible sits inside a platform, only on a platform top or in clear air.

## Economy v2 details (owner, 2026-10-20; extends the "Economy v2" section above)
- 3 collectible **stars** per level on side routes and risky spots; the stars earned are the level's grade (0 to 3). The old star types (clear, coin target, no revive, time) and the CollectCoins goal type are retired.
- End payout: **30 coins for the clear + 40 per star collected + 5 per combo step**, times the region coin factor (a 3-star clear pays 150 coins in region 1 and 168 in region 5, plus combo).
- **Gem rarity:** Common 1, Rare 2, Epic 5, Legendary 10 gems at 70/22/7/1 percent per gem spot. The difficulty setting raises the share of gem spots (5 to 10 percent). Milestone gems on L5, L10 and L15 are unchanged.
- **Replay rules:** once a level has 3 stars, replays pay 50 percent of the coins and drop no gems. Levels with fewer than 3 stars keep full rewards on replay. A failed run pays nothing and loses the gems collected in it; a revive is the only way to keep them.
- **Revive:** 2 gems the first time, 5 gems the second time, at most 2 per run (or a rewarded ad later).
- Known data mismatch: `Content/config/remote_config.json` and the level JSON still say `replayCoinPercent` 40 while the rule is 50 (doc 09 section F).

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
- Versioning: every build is vMAJOR.MINOR.HOTFIX.BUILD (game repo `.claude/rules/versioning.md`, `Tools/version.json`, build log `Content/builds/build_log.csv`).
- Fonts: Lilita One (display) + Nunito Bold (body).
