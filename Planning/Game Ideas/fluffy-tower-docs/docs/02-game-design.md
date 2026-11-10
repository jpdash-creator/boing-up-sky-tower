# 02 - Game Design

> **Superseded in part by [08-rules-addendum.md](08-rules-addendum.md)**: level complexity, the region list with signature mechanics, endless speed ramp, coin/gem rewards and all currency prices/ratios are now defined there.

## 1. Pillars
1. **One more try**: runs last 30-90 s, restart in under 1 s.
2. **Skill expression**: momentum and combos reward mastery, so leaderboards matter.
3. **Collect and show off**: cute, bold cosmetics that are visible mid-air and shareable.
4. **Always something new**: a region every ~2-3 weeks after launch.

## 2. Core mechanics (generic momentum-jumper feel, original implementation)
- Horizontal speed builds while a direction is held (acceleration curve + friction by surface).
- Jump impulse = base + k * |horizontal speed|; fast runners jump much higher.
- Wall bounce reverses horizontal velocity with a small speed boost.
- Combo: a jump that passes 2 or more floors starts or extends a combo. The combo ends on landing without enough height, and the bonus score is combo^2 style. Visual feedback: screen shake, shout text ("Sweet!", "Super!", "Wow!") with cute-themed words, trail intensity.
- Scroll: after the first jump the screen scrolls up on its own (platforms and background slide down); the bottom edge of the screen is the danger line, so falling below it ends the run. No hazard comes from below. Speed is a 120 percent base that steps up every 5 s by the chosen difficulty step (20/40/60/80/100 percent), capped at +400 percent, with a visible "SPEED UP +N%" cue at each step (details in doc 08, scroll model). *(Update 2026-10-20: replaces the earlier "ratcheting camera and rising kill zone".)*
- Jump numbers (T9 baselines, owner retune 2026-10-20): `JumpBase` 19 u/s, `JumpSpeedFactor` 0.41 (docs aligned to the code 2026-11-01). A standing jump peaks at 4.51 u (1.3 median row gaps), a full-speed jump (22.69 u/s launch) at 6.44 u (1.85 gaps). Hero physics and the rarity rules (independent stat sets from the Forgiveness Index rule engine, not a percent chain): see docs/44-power-mechanics-bible.md in the fluffy-climbers-sky-tower docs folder. Normal vertical gaps are capped at 4.2 u so a standing jump always reaches the next platform. The layout code sizes rows from a separate reference jump (`LayoutJumpBase`, see `Assets/_Game/Simulation/NOTES.md`).
- Floor numbering shown on platforms; milestone every 100 floors (confetti + background shift inside the region).

### Definitions and scoring (decision D5)
- **Floor** = platform row index. **Screen width** about 10 world units in portrait (player = 1 unit).
- **Score** = floors x 10 + combo bonus (combo^2 x 5) + coins x 1. Prototype values, revisit after Phase 1.
- **Physics baselines (D4)** are chosen in a spreadsheet, tuned in the prototype, then frozen before the generator and solvability bot are built.
- Each character's unique trail is fixed to that character and not equippable (no gear, owner 2026-11-01).

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
Region 31+ continue as "Season Regions" tied to the battle pass theme. The data model has no upper bound.

## 7. Characters (collectible)
Default mascot: **Momo**, a round chibi cloud-fox with big eyes and a tiny scarf. This is the game icon.

Launch roster (suggested 16): Momo (free), Biscuit the Bunny, Captain Waddle (penguin), Zippy (robot), Luna (cat witch), Bolt (dino), Sir Nibbles (knight hamster), Pixel (cyber frog), Coco (monkey), Frost (yeti cub), Blaze (dragon pup), Nova (astro-panda), Boo (ghost), Rex (T-rex in a hoodie), Sakura (fox maiden), Ziggy (alien).
- Prices are in doc 15 section 7 (Rare and Epic in coins or gems at 1 gem = 20 coins; Legendary in gems).
- Each character has idle, run, jump, fall, land, win, fail animations plus a unique trail and a unique shout line.

### No equipment (owner, 2026-11-01)
There are no outfit, boots, hat, accessory or trail slots. Characters are the only collectible look. Silhouette variations (tail, ears) are handled by character-specific "head" and "tail" layers.

### Item catalog goals at launch
- 20 characters (4 per region, doc 15), 10 power-ups, no gear.

## 8. Power-ups
| Power-up | Effect | Duration | Cost |
|---|---|---|---|
| Spring Boots | Next 5 jumps +30 percent height | 5 jumps | 15 gems (or 300 coins) |
| Rocket Pack | Auto-boost 15 floors | 4 s | 30 gems |
| Coin Magnet | Pulls coins/gems | 10 s | 10 gems (or 200 coins) |
| Bubble Shield | Absorbs one hazard hit or one fall | 1 hit | 20 gems |
| Slow Time | Slows scroll and hazards | 6 s | 15 gems |
| Double Coins | x2 coins this run | run | 8 gems (or 160 coins) |
| Safety Net | Auto-bounce once from bottom | 1 use | 25 gems |
| Lucky Clover | +chance for gem drop | run | 25 gems |
| Feather Fall | Reduced gravity | 6 s | 15 gems |
| Revive (Heart) | Continue after fail | 1 use | 2 gems (first), 5 gems (second), or ad |

*Update 2026-10-20 (economy v2): coins are no longer picked up in a level, so Coin Magnet pulls stars and gems, and Double Coins doubles the end-of-level coin payout.*
Prices: 8-30 gems / 160-600 coins at 1 gem = 20 coins, tunable via Remote Config (final values in docs 15 and 16).
Stackable up to 2 equipped per run. In-run orbs give free temporary versions of the first six.
Permanent upgrades (coins): magnet duration, shield bubble size, starting coin bonus; not applied in Ranked.

## 9. Economy (gems are scarce)

> The faucet/sink numbers and IAP ladder that used to be here were replaced by doc 08 section 8 (superseded: the rate is now fixed at 1 gem = 20 coins, owner 2026-11-01). Summary below; doc 08 is authoritative.

### Faucets (free gem income; deliberately scarce)
- Campaign: **superseded 2026-10-20 by economy v2.** Gems are collectible pickups with rarity tiers (Common 1, Rare 2, Epic 5, Legendary 10 gems at 70/22/7/1 percent per gem spot), about 10 per level and 140 to 220 per region in regions 1-5, plus milestone gems on L5/L10/L15 (about 11 per region). The old figure of 7-12 gems per region no longer holds; the owner-review item on gem scarcity is in doc 09.
- Daily login + quests + one rewarded ad: about 5 gems and 400 coins per day.
- Endless gem drops at floors 250/500/1000, once per account per day (doc 08 section 7).
- Battle pass: free and premium tracks per doc 05 (gem totals to be re-balanced against the doc 08 income model).

### Sinks
- Region skip: 100 gems (region 2) rising to 400 (region 20).
- Characters: see doc 15 section 7 (1 gem = 20 coins).
- Consumable power-ups 8-30 gems (or 160-600 coins).
- Chests: Power-up Chests for gems or coins (1 gem = 20 coins); Coin Chests are earned, not sold (docs 15, 16).
- Revive 2 gems the first time, 5 the second, max 2 per run (or ad). Owner decision 2026-10-19/20.

### Gem IAP packs (prices are store tiers, adjust per locale)
| Pack | Price (USD) | Gems | Bonus |
|---|---|---|---|
| Handful | 0.99 | 80 | base (superseded by doc 14) |
| Pouch | 4.99 | 440 | +9 percent |
| Sack | 9.99 | 920 | +14 percent |
| Chest | 19.99 | 1,900 | +18 percent |
| Vault | 49.99 | 5,000 | +24 percent |
| Treasury | 99.99 | 10,500 | +30 percent |

Tuning goal: a free player can earn roughly one rare item per week; spending accelerates collecting, never gates leaderboard skill.

## 10. Game feel & juice
Typography: Lilita One for titles and numbers, Nunito Bold for body text (free OFL fonts, owner decision 2026-10-20). Pickups show feedback at the pickup position: a gem shows a gem, a star shows a star, never a star at the character.
Squash and stretch, particle dust on landing, screen shake on big combos, parallax backgrounds (4-6 layers per region), day-night shift every 100 floors, haptics, sfx pitch rising with combo, short adaptive music loops per region.

## 11. Onboarding / FTUE
60-second tutorial in Meadow Springs level 1: auto-highlight movement zones, first jump, first wall bounce, first combo. A free chest at the end shows the loadout screen to seed customization desire. First session gives ~30 gems so players feel the currency.
