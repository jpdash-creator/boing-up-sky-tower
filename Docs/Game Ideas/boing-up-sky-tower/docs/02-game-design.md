# 02 - Game Design

> **Superseded in part by [08-rules-addendum.md](08-rules-addendum.md)**: level complexity, the region list with signature mechanics, endless speed ramp, coin/gem rewards and all currency prices/ratios are now defined there.

## 1. Pillars
1. **One more try**: runs last 30-90 s, restart in under 1 s.
2. **Skill expression**: momentum and combos reward mastery, so leaderboards matter.
3. **Collect and show off**: cute, bold characters that are visible mid-air and shareable.
4. **Always something new**: a region every ~2-3 weeks after launch.

## 2. Core mechanics (generic momentum-jumper feel, original implementation)
- Horizontal speed builds while a direction is held (acceleration curve + friction by surface).
- Jump impulse = base + k * |horizontal speed|; fast runners jump much higher.
- Wall bounce reverses horizontal velocity with a small speed boost.
- Combo: a jump that passes 2 or more floors starts or extends a combo. The combo ends on landing without enough height, and the bonus score is combo^2 style. Visual feedback: screen shake, shout text ("Sweet!", "Super!", "Wow!") with cute-themed words, trail intensity.
- Scroll: after the first jump the screen scrolls up on its own (platforms and background slide down); the bottom edge of the screen is the danger line, so falling below it ends the run. No hazard comes from below. Speed is a 120 percent base that steps up every 5 s by the chosen difficulty step (20/40/60/80/100 percent), capped at +400 percent, with a visible "SPEED UP +N%" cue at each step (details in doc 08, scroll model). *(Update 2026-10-20: replaces the earlier "ratcheting camera and rising kill zone".)*
- Jump numbers (T9 baselines, owner retune 2026-10-20): `JumpBase` 19 u/s, `JumpSpeedFactor` 0.58. A standing jump peaks at 4.51 u (1.3 median row gaps), a full-speed jump at 7.33 u (2.1 gaps). Normal vertical gaps are capped at 4.2 u so a standing jump always reaches the next platform. The layout code sizes rows from a separate reference jump (`LayoutJumpBase`, see `Assets/_Game/Simulation/NOTES.md`).
- Floor numbering shown on platforms; milestone every 100 floors (confetti + background shift inside the region).

### Definitions and scoring (decision D5)
- **Floor** = platform row index. **Screen width** about 10 world units in portrait (player = 1 unit).
- **Score** = floors x 10 + combo bonus (combo^2 x 5) + coins x 1. Prototype values, revisit after Phase 1.
- **Physics baselines (D4)** are chosen in a spreadsheet, tuned in the prototype, then frozen before the generator and solvability bot are built.
- Each character's unique trail is its default equipped trail; the trail slot sets the ribbon colour (doc 08 section 3).

### Tunable constants (live-tunable via Remote Config)
`gravity`, `accel`, `maxSpeed`, `jumpBase`, `jumpSpeedFactor`, `wallBoost`, `comboMinFloors`, `scrollBasePercent` (120), `scrollStepPercent` (by difficulty), `scrollStepSeconds` (5), `scrollStepCap` (400), `coyoteTimeMs` (80), `jumpBufferMs` (100).

## 3. Controls
- **Default (owner decision 2026-10-19)**: three always-visible on-screen buttons: Left and Right at the bottom-left, Jump at the bottom-right. The earlier touch-zone scheme was removed because the owner could not tell how to play. Tilt stays an optional idea.
- Left-handed mirror option swaps the sides. Haptic ticks on jump and landing.

## 4. Modes
| Mode | Purpose | Rules | Leaderboard |
|---|---|---|---|
| Campaign | Progression | 15 goal-based levels per region, 1-3 stars | Total stars |
| Endless (per region) | Skill + competition | Infinite tower, region theme; entered with an **Endless button on each region's level screen** (owner decision 2026-10-20, being implemented) | All-time + weekly, per region |
| Daily Challenge | Retention | One seed per day, same for all | Daily board |
| Ranked toggle | Fairness | No consumables/permanent upgrades | Only Ranked scores count on global boards |
| Events (P2) | Live-ops | Limited-time modifiers (low gravity, ice-only...) | Event board |

## 5. Level structure (15 per region)
Levels are data (`LevelDef`), generated from region rules plus authored overrides.

Difficulty `d`, class (Simple/Medium/Hard/Complex/Highly complex) and rewards per level are defined in doc 08 section 6; the column below only suggests a **goal type** per level (the goal types remain the `LevelDef.goal` vocabulary).

| Level | Type | Typical goal | d (doc 08) |
|---|---|---|---|
| 1-3 | Intro | Reach floor 30/45/60 | 0.05-0.15 |
| 4 | Reach (was Collect) | Reach floor 100 (the CollectCoins goal is retired, see below) | 0.22 |
| 5 | Complex: combo | Chain a 3-floor combo twice | 0.40 |
| 6 | Hazard | Reach floor 80 with region hazard | 0.35 |
| 7 | Mini-challenge | No-wall-bounce climb | 0.42 |
| 8 | Collect | Find the hidden gem platform | 0.50 |
| 9 | Reach (was Speedrun) | Reach floor 180, no timer | 0.58 |
| 10 | Complex: mid-boss | Survive a scripted hazard wave | 0.70 |
| 11 | Combo | A 6-floor combo | 0.62 |
| 12 | Narrow | Only narrow/moving platforms | 0.70 |
| 13 | Hazard storm | Reach floor 120 | 0.78 |
| 14 | Pre-finale | Mix of all mechanics | 0.86 |
| 15 | **Highly complex: finale / boss** | Climb while the boss hazard chases you | 1.00 |

Region difficulty offset rises from +0.0 (region 1) to +0.25 (region 30), capped at d = 1.0 (doc 08).

**Update 2026-10-20 (owner device-test decisions):** goal values are doubled or more (levels are longer; example floors shown above are the original plan, shipped values are in `Content/levels`). Timed goals and the time star were removed (list 1, point 9), so Speedrun levels became reach-floor goals; the CollectCoins goal is retired with economy v2. The goal of every level is a galaxy portal above the last platform: Momo jumps into the swirl and the results show. Levels use patterns instead of pure randomness: the first rows are a fixed easy ladder, then every 6 to 10 rows the pattern changes (ladder, dense patches of small platforms where about one in four extras crumbles, or a zigzag). Stars are no longer clear/coins/no-revive: each level has **3 collectible stars** placed on side routes and risky spots (doc 08, economy v2). The `stars` array in the level JSON is kept only to satisfy the schema.

## 6. Region catalog (30)

> **Playable scope (2026-10-20):** the current build ships regions 1-5 (Meadow Springs, Mushroom Grove, Sunny Beach, Candy Canyon, Bamboo Temple), 15 levels each. Regions 6-30 appear on the scrollable world map with a padlock and a "Coming soon" tag. On the map the art order of the later regions follows the picture (for example Moon Carnival, Dragon Peak, Space Station from the bottom of that segment), so the content region order and golden hashes are updated after the map chain is final.

### Live at launch (20)
| # | Region | Theme / signature mechanic |
|---|---|---|
| 1 | Meadow Springs | Tutorial, flowers, bouncy mushrooms |
| 2 | Mushroom Grove | Bounce caps, spore clouds that slow |
| 3 | Sunny Beach | Moving driftwood, crabs |
| 4 | Candy Canyon | Sticky gum platforms, sugar crumble |
| 5 | Bamboo Temple | Narrow bamboo, swinging gongs |
| 6 | Frosty Peaks | Ice (low friction), snowballs |
| 7 | Autumn Orchard | Falling leaves, wind gusts |
| 8 | Desert Oasis | Sand crumble, cactus spikes |
| 9 | Pirate Cove | Cannonballs, rolling barrels |
| 10 | Toy Factory | Conveyor platforms, spring toys |
| 11 | Haunted Manor | Ghosts, vanishing platforms |
| 12 | Crystal Caverns | Reflective bounce, falling crystals |
| 13 | Volcano Forge | Rising lava, fire jets |
| 14 | Jungle Ruins | Vine swings, arrow traps |
| 15 | Neon City | Laser gates, teleport pads |
| 16 | Cloud Kingdom | Cloud platforms that dissolve, lightning |
| 17 | Steampunk Skyport | Gears, steam vents |
| 18 | Deep Sea Reef | Low-gravity water zone, currents |
| 19 | Space Station | Zero-g patches, meteors |
| 20 | Moon Carnival | Trampolines, rotating wheels, all-mix |

> The signature mechanic per region is the single one listed in doc 08 section 6. The extra hazards in the table above (spore clouds, crabs, snowballs, cactus spikes, arrow traps, teleport pads, meteors, falling crystals) are optional decoration/hazard props, not signature mechanics, and may be cut.

### Locked teasers (10, visible but not playable)
21 Dragon Peak, 22 Pyramid Sands, 23 Viking Fjord, 24 Wild West Mesa, 25 Dino Valley, 26 Storybook Castle, 27 Arcade Realm, 28 Samurai Dojo, 29 Alien Jungle, 30 Rift of Time.

Teasers show a dark silhouette backdrop, name and a lock icon. Release them one at a time with a hype event.

### Infinite roadmap
Region 31+ continue as "Season Regions" (a season theme is later). The data model has no upper bound.

## 7. Characters (collectible)
Default mascot: **Momo**, a round chibi cloud-fox with big eyes and a tiny scarf. This is the game icon.

**Current state (docs 20, 21, 22, 27):** two characters exist, both Common and free: Momo (region 1) and the Sporeling (region 2). Each region unlock gives that region's Common character. Uncommon, Rare, Epic and Legendary characters (with powers and cooldowns, doc 21) are planned and are **Coming Soon** in the shop and in chests (switched off in code). Owner price list for when they launch: Uncommon 150, Rare 300, Epic 700, Legendary 1,500 gems; a duplicate from a chest refunds 25 percent of the gem price in gems (doc 22).
- A character is a set of full-canvas part pictures listed in a `rig.json` (docs 19 and 20). Each character has idle, run, jump, fall, land, win and fail animations.
- The original 16-character launch roster idea (Biscuit the Bunny, Captain Waddle and so on) stays a naming pool for later characters.

**No gear (owner 2026-10-24 and 2026-11-01):** there are no outfits, boots, hats, accessories, trail items, wardrobe or inventory-gear screens. The cosmetic layer is the character itself.

## 8. Power-ups
Seven power-ups are implemented and sold (doc 22 point 4.3, doc 27 section 7). Stock per type has no cap; a tap while one is active adds its full duration. Chests, rewards, bundles and the monthly subscription give random units (equal chance across the 7 types).

| Power-up | Effect | Duration | Coins | Gems |
|---|---|---|---|---|
| Flight | Fly upward and steer; gongs cannot hurt | 10 s | 1,500 | 75 |
| Rocket | Auto-climb 20 floors (stops before the last floors) | about 4 s | 1,500 | 75 |
| Double Jump | One extra air jump per air phase | 30 s | 1,000 | 50 |
| Wall Stick | Stick to side walls up to 1.5 s | 30 s | 1,000 | 50 |
| Shield | Gongs cannot hurt | 60 s | 1,000 | 50 |
| Magnet | Gems and stars come from farther away | 30 s | 1,000 | 50 |
| Slow Time | Rising danger zone 50 percent slower | 30 s | 1,500 | 75 |
| Revive | Continue after a fall | 1 use | - | 5 gems (first), 10 (second), max 2 per run; the first can be free by rewarded ad |

Not built (hidden rows in the data): Spring Boots, Coin Doubler, Star Seeker, Safety Net. Buying 5 units is -10 percent, 10 or more -20 percent. Ranked runs stay neutral.

## 9. Economy

> **Authoritative numbers: [27-economy-tables-from-code.md](27-economy-tables-from-code.md)** (read from the code) and docs 22 and 26. The older "100 coins : 1 gem : about $0.011" model and the gear/VIP ladders that were here are void. **Fixed rate: 1 gem = 20 coins.**

### Faucets (summary)
- Level payout (economy v3): clear 70 + 60 per star + 5 per combo step, times the region factor (1.00, 1.03, 1.06, 1.09, 1.12). A 3-star level pays 250 coins before combo. Replays pay 50 percent once the level has 3 stars and drop no gems.
- Gems: collectible pickups in rarity tiers (Common 1, Rare 2, Epic 5, Legendary 10 gems at 70/22/7/1 percent), about 75 placed per region (2.7 to 3.2 percent of rows), plus milestone gems (L5, L10, L15 first clear).
- Rewards screen (Main, Daily, Weekly): stars and region milestones, 10 daily actions, 11 weekly tasks (doc 26 section 4).
- Subscription (Weekly, Monthly): instant gems and a daily claim (doc 26 section 2).
- Endless: floors/2 + 50 per 100 floors in coins, 3 full-rate runs a day (endless gem drops are specified but not built).

### Sinks (summary)
- Region skip: 100, 116, 133, 150 gems for regions 2 to 5.
- Power-ups: 1,000 coins / 50 gems or 1,500 coins / 75 gems.
- Treasure chests: Iron 100 gems (1 power-up + 400 to 600 coins), Gold 300 gems (3 power-ups + 1,000 to 1,400 coins); open 10 costs 10 times.
- Bundles: 1,000 generated bundles priced 200 to 500 gems, worth 1.4 to 1.8 times their price; Refresh 100 coins or 5 gems; a timer bundle at 30 percent off.
- Coin packs: 1,000 to 100,000 coins for 50 to 4,000 gems.
- Revive: 5 gems, then 10 (max 2 per run); the first can be free by rewarded ad.
- Characters (Coming Soon): 150 to 1,500 gems.

### Money
Nine gem packs (50 to 15,000 gems, $0.79 to $119.99, rupee prices too) and two subscriptions (Weekly $1.49, Monthly $4.99). Launch promo and first-purchase double are in the code, OFF by default. Pack table: doc 27 section 6.

Tuning goal: gems come mostly from rewards and spending is driven by chests, bundles, skips and power-ups; spending accelerates collecting, never gates leaderboard skill.

## 10. Game feel & juice
Typography: Lilita One for titles and numbers, Nunito Bold for body text (free OFL fonts, owner decision 2026-10-20). Pickups show feedback at the pickup position: a gem shows a gem, a star shows a star, never a star at the character.
Squash and stretch, particle dust on landing, screen shake on big combos, parallax backgrounds (4-6 layers per region), day-night shift every 100 floors, haptics, sfx pitch rising with combo, short adaptive music loops per region.

## 11. Onboarding / FTUE
60-second tutorial in Meadow Springs level 1: auto-highlight movement zones, first jump, first wall bounce, first combo. The first clears and the Rewards screen give the first gems so players feel the currency (no chest is earned from play).
