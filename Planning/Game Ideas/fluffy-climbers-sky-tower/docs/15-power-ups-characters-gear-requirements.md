# 15 - Power-ups, chests, subscription, characters: requirements (gear removed)

> **Corrected 2026-11-01 to match the code (build v0.38.0.40).** Authoritative now: **doc 27** (economy tables), **doc 26** (shop, Bundles, Subscription, Rewards), **doc 22** (chests, prices, revive, characters), **doc 21** and **doc 20** (characters and chests; gear removed 2026-10-24). Where this file differs, those win. The filename keeps "gear" only so links do not break; **there is no gear** (hats, shirts, pants, boots, gear chests, style shards, wardrobe layering, inventory gear tab).
> Requirement ids are kept. An id that no longer applies is marked "removed or superseded 2026-11-01 (see doc N)" and is not renumbered.
> Rules applied: **1 gem = 20 coins (fixed)**; power-ups are 1,000 coins / 50 gems or 1,500 coins / 75 gems; **no VIP tiers** (Weekly and Monthly Subscription instead); **no loadout, no carry cap, no stock cap** (rework 2026-10-31); **7 power-ups are implemented**; chests are **Iron and Gold**; characters are **2 Commons** and the character shop is "coming soon".

Legend. Priority **P0/P1/P2**. Tags: **[OWNER]**, **[CODE]** read from the code, **[ASSUMPTION]**, **(verify)**. State: **Built**, **Planned**, **Removed**.

---

## 1. Owner input and decisions

| ID | Item | Status 2026-11-01 |
|---|---|---|
| O-1 | Power-ups: Flight, Rocket, Double Jump, Wall Stick, many more. | Built: 7 types (Flight, Rocket, Double Jump, Wall Stick, Shield, Magnet, Slow Time). Four more rows exist hidden (Spring Boots, Coin Doubler, Star Seeker, Safety Net, not implemented). |
| O-2 | Chests: Coin, Gear, Power-up. | **Superseded (doc 20, doc 22):** two chests, **Iron** and **Gold**, each paying coins and power-ups. |
| O-3 | Gear (Hats, Shirts, Pants, Boots). | **Removed 2026-10-24 (doc 20) and again 2026-11-01: no gear.** |
| O-4 | VIP with 6 levels (jump, gems, coins percent). | **Superseded (doc 26 section 2):** Weekly and Monthly Subscription, no stat bonuses. |
| O-5 | Characters: 3 to 5 per region. | **Superseded (doc 21, doc 22):** rarities Common to Legendary; today **Momo (region 1) and Sporeling (region 2)**, both Common and free. Others are "coming soon". |
| O-6 | Everything works in all 5 regions. | Kept (section 2). |
| O-7 | Rate: 1 gem = 20 coins, fixed. | Applied everywhere (doc 22 conventions). |
| O-8 | No gear anywhere. | Applied (owner 2026-11-01). |
| D-1 | Power-ups are owned stock, picked as a 3-slot loadout. | **Superseded 2026-10-31:** stock has no cap, there is **no loadout**; every owned type shows a round button in the run (`PowerUpBar`); a tap while active adds the full duration. |
| D-2 | Chests sold in tiers for gems or coins, pity, duplicates to shards, earned chests. | **Superseded (doc 22 point 3):** gems only, Iron 100 / Gold 300 gems, open 1 or 10; no pity, no shards, **no earned chests**. |
| D-3 | Gear layered on the character. | **Removed.** |
| D-4 | VIP six subscription tiers. | **Superseded** by the Subscription (doc 26). |
| D-5 | 4 characters per region, 20 in total, abilities. | **Not planned as written.** Characters have rarity and powers per doc 21 (Uncommon 1 power, Rare 1, Epic 2, Legendary 3; cooldown 30/22/15/10 s); data model exists, no Uncommon+ character exists yet. |
| D-6 | Character screen and Inventory/Wardrobe screen. | Character ("Heroes") screen built; **no Inventory screen** (doc 20). |

### 1.1 Conflicts (original), status

| ID | Conflict | Status |
|---|---|---|
| X-1 | Cosmetics-only rule vs consumables and abilities. | Kept: Ranked is neutral (no power-ups, no character powers). Ranked is not built yet. |
| X-2 | Doc 08 prices assume 100 coins per gem. | **Resolved: rate fixed at 20 coins per gem**; prices 1,000 / 50 and 1,500 / 75 (doc 22 4.3). |
| X-3 | Coin Doubler priced like other consumables. | Coin Doubler is a hidden, unimplemented row (120 coins in the table). It is not sold. |
| X-4 | Shield overlap with Safety Net. | Shield is a pure 60 s gong block (doc 24 P1); Safety Net not built. |
| X-5 | One VIP vs six tiers. | **Resolved:** neither; Subscription (weekly, monthly). |

---

## 2. Cross-cutting: it works in all 5 regions, deterministically (FR-REGION)

The 5 playable regions and their mechanics: 1 Meadow Springs (bounce flowers plus 7 new special platforms from level 3, doc 24), 2 Mushroom Grove (bounce caps), 3 Sunny Beach (drifting platforms), 4 Candy Canyon (sticky gum), 5 Bamboo Temple (swinging gongs; knock back, never kill). Gongs are the only hazard Shield, Flight and Rocket must handle.

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-REGION-1 | Run config hashed into the run record. | P1 | **Superseded/Planned:** a `RunConfig` with character and loadout is not built; the run uses the selected character and the owned power-ups. | Planned |
| FR-REGION-2 | Bonus pipeline (jump, gem, coin percent from VIP and characters). | - | **Removed or superseded 2026-11-01:** no VIP bonuses, no character stat bonuses today. | Removed |
| FR-REGION-3 | Deterministic features: each power-up is a Simulation feature, integer math, no `UnityEngine` types. | P0 | Replay gives the same state hash. | Built |
| FR-REGION-4 | Levels stay solvable without help. | P0 | The bot solves all 75 levels (random layouts, 20+ seeds each) with an empty stock. | Built |
| FR-REGION-5 | Stars unchanged by power-ups (a rocket skipping a star leaves it uncollected). | P0 | Goal and star tests. | Built |
| FR-REGION-6 | Per-region interaction test for each implemented power-up. | P0 | Tests `Power_<Name>_<Region>`. | Built for the 7 types |
| FR-REGION-7 | Level 15 and endless allow power-ups. | P0 | Chaos run covers level 15 and endless. | Built |
| FR-REGION-8 | Region-scoped content (characters per region). | P1 | The Common character of a region is owned when the region opens. | Built (2 characters) |
| FR-REGION-9 | New-mechanic checklist when a region adds a mechanic. | P1 | Checklist re-runs the power-up tests. | Planned |
| FR-REGION-10 | Ranked neutrality. | P0 | Ranked ignores power-ups and character powers. | Planned |
| FR-REGION-11 | New screens obey the critical UX rules in all backgrounds. | P0 | PlayMode audit. | Built |

---

## 3. Power-ups (FR-PWR)

### 3.1 Rules [CODE `PowerUpCatalog.cs`, `RunSession.cs`, `PowerUpBar.cs`]

- **Owned stock, no cap.** Counts per type are saved at once; stored counts above the old 99 are kept. Bought in the shop (coins or gems), won from Iron and Gold chests, Bundles, Rewards and the monthly Subscription (random type, equal chance).
- **No loadout, no carry cap.** Every owned type has a round button in a compact side block above the jump button (mirrored for left-handed play). The old `loadout` field stays in the save and is ignored.
- **A tap while active adds the full duration** onto the remaining time (Flight +10 s, Rocket +20 floors, Double Jump / Wall Stick / Magnet / Slow Time +30 s, Shield +60 s).
- Bundle prices: 5 units -10 percent, 10 or more -20 percent.
- Ranked runs ignore power-ups. They never change what a star needs; every level stays bot-proven solvable with an empty stock.

### 3.2 Roster [CODE `powerups.json`, doc 27 section 7]

| # | Power-up | Effect | Duration per tap | Coins | Gems | $ ref (gems x $0.01099) | State |
|---|---|---|---|---|---|---|---|
| 1 | **Flight** | Fly upward and steer; gongs cannot hurt you; stops 3 rows below the top | 10 s | 1,500 | 75 | 0.82 | Built |
| 2 | **Rocket** | Auto-climb 20 floors in about 4 s; gongs harmless; stops before the last floors | about 4 s (20 floors) | 1,500 | 75 | 0.82 | Built |
| 3 | **Double Jump** | One extra air jump per air phase | 30 s | 1,000 | 50 | 0.55 | Built |
| 4 | **Wall Stick** | Stick to side walls up to 1.5 s, jump to leap off | 30 s | 1,000 | 50 | 0.55 | Built |
| 5 | **Shield** | Gongs cannot hurt you | 60 s | 1,000 | 50 | 0.55 | Built |
| 6 | **Magnet** | Gems and stars come from farther (+2.0 u) | 30 s | 1,000 | 50 | 0.55 | Built |
| 7 | **Slow Time** | The rising danger zone moves 50 percent slower | 30 s | 1,500 | 75 | 0.82 | Built |
| 8 | Spring Boots | next 3 jumps 20 percent higher | 3 uses | 1,000 | - | - | Hidden, not implemented |
| 9 | Coin Doubler | doubles the end-of-level coins | 1 use | 120 | - | - | Hidden, not implemented; not planned (pays more than it costs) |
| 10 | Star Seeker | arrows to uncollected stars | 12 s | 800 | - | - | Hidden, not implemented |
| 11 | Safety Net | catches the first fall | 1 use | 1,500 | - | - | Hidden, not implemented |

Average value of a random power-up unit: **60.7 gems** (4 types at 50, 3 at 75). Cost per second of effect: Flight 7.5 gems, Rocket 18.75, Slow Time 2.5, Double Jump / Wall Stick / Magnet 1.67, Shield 0.83 (doc 27 RC 5).

### 3.3 Interaction with region mechanics (tested per region)

| Power-up | Bounce flowers / caps | Drifting | Sticky gum | Gongs | Moving / crumbling |
|---|---|---|---|---|---|
| Flight | steers over, no extra launch | rises independent of drift | not stuck while flying | immune | passes over |
| Rocket | stops at the row limit | same | same | immune | same |
| Double Jump | allowed after a launch | keeps use on a drifting platform | sticky penalty applies | knockback keeps the use | works |
| Wall Stick | works | works | works | works | works |
| Shield | n/a | n/a | n/a | absorbs gong hits for 60 s | n/a |
| Magnet | pulls | pulls | pulls | pulls | pulls |
| Slow Time | scroll only | drift unchanged | unchanged | gong period unchanged | unchanged |

### 3.4 Requirements

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-PWR-1 | Owned stock per type saved at once. **Cap 99 removed 2026-10-31.** | P0 | Old saves load; counts above 99 are kept. | Built |
| FR-PWR-2 | **Removed or superseded 2026-10-31 (see docs 20, 27):** 3-slot loadout and carry caps. | - | - | Removed |
| FR-PWR-3 | HUD buttons: one round button per owned type, at least 48 dp, thumb zone, ring for remaining time, text name (not colour only), left-handed mirror, no overlap with pause, jump or move pad. | P0 | `PowerUpButtonsFitEveryAspect`. | Built |
| FR-PWR-4 | Flight and Rocket (row limit at the goal, no star auto-collect, gongs harmless). | P0 | Sim tests, deterministic replay. | Built |
| FR-PWR-5 | Double Jump and Wall Stick. Spring Boots not built. | P0 | Sim tests. | Built (2 of 3) |
| FR-PWR-6 | Shield, Magnet, Slow Time built. Safety Net, Star Seeker, Coin Doubler not built and hidden. | P1 | Sim tests per item. | Built (3 of 6) |
| FR-PWR-7 | Cues without flashing; reduced motion shows a static icon. | P0 | Cue frequency audit. | Built |
| FR-PWR-8 | Template for every new power-up. | P1 | `implemented` flag and `simParams` in `powerups.json`. | Built (format) |
| FR-PWR-9 | Data file `Content/economy/powerups.json` (not `Content/items/`) and the built-in table are kept equal by a test. | P0 | `PowerUpTests`. | Built |
| FR-PWR-10 | Power-ups appear in the Power-ups tab, in chests, bundles, rewards and the monthly subscription. | P0 | Flow tests. | Built |
| FR-PWR-11 | Spoken labels on buttons ("Flight, 10 seconds"). | P0 | Label test. | Built |
| FR-PWR-12 | Analytics `powerup_used`, `powerup_bought`. | P1 | No analytics layer yet. | Planned |

---

## 4. Chests (FR-CHEST): Iron and Gold

> Coin Chests, Gear Chests, Common to Legendary tiers, pity, duplicate shards and earned chests are **removed or superseded 2026-11-01 (see doc 22 point 3 and doc 27 section 10)**.

### 4.1 Rules and values [CODE `Chests.cs`, doc 27 section 10]

| Chest | Opens | Price (gems) | Price (coins equal) | Contents | Average value | Return | $ ref price |
|---|---|---|---|---|---|---|---|
| Iron | 1 | 100 | 2,000 | 1 power-up + 400 to 600 coins | 85.7 gems | 85.7% | 1.10 |
| Iron | 10 | 1,000 | 20,000 | ten independent openings | 857 | 85.7% | 10.99 |
| Gold | 1 | 300 | 6,000 | 3 power-ups + 1,000 to 1,400 coins | 242.1 gems | 80.7% | 3.30 |
| Gold | 10 | 3,000 | 60,000 | ten independent openings | 2,421 | 80.7% | 32.97 |

Each power-up is one of the 7 types with equal chance (1 in 7); the same type can repeat. Characters switched off (`ChestRolls.CharactersEnabled = false`): when on, Iron drops Uncommon 67 / Rare 33 percent and Gold Rare 57 / Epic 29 / Legendary 14, one character per opening, a duplicate refunds 25 percent of its gem price in gems. **Open item (doc 22 item 1): at those odds a character per chest returns 276 to 286 percent of the price; fix before enabling.**

### 4.2 Requirements

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-CHEST-1 | Chest data: constants in `ChestRolls` (prices, ranges, counts); `chests.json` is not used. | P0 | Values equal doc 22 point 3. | Built |
| FR-CHEST-2 | Shop Chests tab: two cards (Iron, Gold) with Open 1 and Open 10. | P0 | Layout audit; confirm above 50 gems. | Built |
| FR-CHEST-3 | Opening by one tap with a 1.2 s skippable animation; reduced motion shows the result at once. | P0 | Skip test. | Built |
| FR-CHEST-4 | Result dialog: coins and power-ups with names, Collect / Collect all (open 10 shows totals). | P0 | Layout audit. | Built |
| FR-CHEST-5 | Contents text and (i) info before purchase; text matches the roller. | P0 | Text test. | Built |
| FR-CHEST-6 | **Removed or superseded 2026-11-01 (see doc 22):** pity counters. | - | - | Removed |
| FR-CHEST-7 | **Removed (no gear):** duplicates to Style Shards. | - | - | Removed |
| FR-CHEST-8 | **Removed or superseded:** earn rules for chests. "No earned chests are granted for now" (doc 22 point 5). | - | - | Removed |
| FR-CHEST-9 | **Removed:** chest stacks in an inventory. Chests are opened on purchase. | - | - | Removed |
| FR-CHEST-10 | Randomness: seeded per opening (`SeedOf(kind, openIndex)`), recomputable by a server; server-side roll later. | P0 | `ChestTests` (100,000 openings). | Built (seeded), server Planned |
| FR-CHEST-11 | Compliance: contents shown before purchase, age gate, regional switch for paid random items (verify). | P0 | Flag test. | Planned |
| FR-CHEST-12 | Anti-abuse: rate limits, server logs. | P1 | - | Planned |
| FR-CHEST-13 | Analytics `chest_open`, `chest_odds_viewed`. | P1 | No analytics layer yet. | Planned |

---

## 5. Gear (FR-GEAR): REMOVED

**Removed 2026-11-01 (and in the game 2026-10-24, doc 20): no gear.** This section used to define hats, shirts, pants and boots, rarity frames, layered drawing, rig anchors, a placeholder generator and style levels. FR-GEAR-1 to FR-GEAR-12 are **removed or superseded 2026-11-01 (see doc 20)**. Leftovers to delete from content: `Content/items/items.json` (boots_cloud_runner), the gear slots of `item.schema.json`, gear lines in the asset catalog; old saves ignore their gear fields.

---

## 6. Subscription (replaces VIP, FR-VIP)

> The six VIP tiers, weekly and monthly VIP plans, jump/gem/coin percentage bonuses, crown hats and exclusive gear are **removed or superseded 2026-11-01 (see doc 26 section 2 and doc 27 section 8)**.

| Plan | Price | Length | On purchase | Daily claim | Free power-ups |
|---|---|---|---|---|---|
| Weekly Subscription | $1.49 (Rs 149) | 7 days | 100 gems | 10 gems + 500 coins | none |
| Monthly Subscription | $4.99 (Rs 449) | 30 days | 200 gems | 10 gems + 600 coins | 1 random on every 5th claim (6 in 30 days) |

Value: weekly 345 GE (2.5 times its price), monthly 1,400 GE without and 1,764 with the power-ups (3.1 and 3.9 times); doc 27 section 8.

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-VIP-1 | Six VIP products x 2 plans. | - | **Superseded:** two products, weekly and monthly. | Removed |
| FR-VIP-2..3 | Tier table, bonus pipeline, upgrade/downgrade. | - | **Removed.** Buying a plan while one is active extends it. | Removed |
| FR-VIP-4 | Auto-renew disclosure on the card. | P0 | Layout audit. | Planned |
| FR-VIP-5 | Entitlement server-side, grace 3 days. | P2 | Airplane-mode test. | Planned |
| FR-VIP-6 | Restore at start and in Settings. | P0 | Reinstall test. | Planned |
| FR-VIP-7 | Daily claim once per local day; missed days are not stacked. | P1 | Date tests. | Built |
| FR-VIP-8 | Ad-free from VIP 3. | - | **Removed:** no ad-free perk exists. | Removed |
| FR-VIP-9 | Crown hat and exclusive gear. | - | **Removed (no gear).** | Removed |
| FR-VIP-10 | VIP bonuses excluded from Ranked. | - | **Removed:** no bonuses exist. | Removed |
| FR-VIP-11 | Subscription page with two cards (ticket icon, purchase chips, daily chips, privilege lines, price button, days left, Claim). | P2 | Layout audit. | Built |
| FR-VIP-12 | Analytics `subscription_started`, `subscription_claimed`. | P2 | - | Planned |

---

## 7. Characters (FR-CHAR)

### 7.1 Current state [CODE `Characters.cs`, docs 21, 22]

| Id | Name | Rarity | Region | Gem price | Powers | State |
|---|---|---|---|---|---|---|
| momo | Momo | Common | 1 | free (owned) | none | Built |
| sporeling | Sporeling | Common | 2 | free (owned when region 2 opens) | none | Built |
| - | Uncommon / Rare / Epic / Legendary | - | - | owner price list 150 / 300 / 700 / 1,500 gems (`GemPrice` is 0 until set) | 1 / 1 / 2 / 3 powers, cooldown 30 / 22 / 15 / 10 s | **Not built** (data model only) |

Rules: Common characters belong to a region and are owned when it unlocks; Uncommon and above come from chests (off) and the shop (coming soon); a duplicate refunds 25 percent of its gem price in gems (chest) or coins per doc 21 (Common 100, Uncommon 200, Rare 500, Epic 1,000, Legendary 2,000: superseded by doc 22). The selected character is shown on the home screen and in the run (doc 23).

> **Not planned / historical:** the original roster of 20 characters (4 per region) with signature abilities, stat bonuses and coin prices (Rare 4,000 to 8,000, Epic 9,000 to 17,000, Legendary 250 to 450 gems) is **dropped**; it is replaced by the rarity model above. Do not use the old names or prices.

### 7.2 Requirements

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-CHAR-1 | Character data: `CharacterDefs` in code (id, name, rarity, region, gem price, powers); `characters.json` not used yet. | P1 | - | Built (code table) |
| FR-CHAR-2 | The Common character of a region unlocks with the region (progress or gem skip). | P0 | Unlock tests. | Built |
| FR-CHAR-3 | **Superseded:** 20 ability features. Powers per rarity (doc 21) are not built. | P2 | - | Planned |
| FR-CHAR-4 | The selected character is used on the home screen and in the run. | P0 | Selected one shown. | Built |
| FR-CHAR-5 | Heroes screen: the chosen character on the land stage, a scrolling roster of rarity boxes, owned in colour, others dark silhouettes (no lock icon, doc 23). | P0 | Layout audit. | Built |
| FR-CHAR-6 | Purchase with gems through the common flow. | P1 | Flow tests. | Planned (coming soon) |
| FR-CHAR-7 | Art: Momo (nine parts) and Sporeling (polygon parts) exist. | P0 | - | Built |
| FR-CHAR-8 | Placeholder labels in dev builds only. | P1 | - | Not needed |
| FR-CHAR-9 | Powers explained in plain text on the card. | P0 | - | Planned |
| FR-CHAR-10 | Ranked uses the neutral character. | P0 | - | Planned |
| FR-CHAR-11 | Analytics `character_selected`. | P1 | - | Planned |

---

## 8. Save data, data files, remote config

### 8.1 Save data [CODE `SaveData.cs`]

Additive fields, old saves load with defaults: `coins`, `gems`, per-level progress, `unlockedRegions`, `endlessBest` / `endlessDay` / `endlessRunsToday`, `inventory.powerups` (id to count, no cap), `loadout` (obsolete, ignored), chest counter `ChestOpens` (save version 2), `characters` (owned ids, selected id), bundle state (6 offer ids, seed, draw counter, timer state), subscription state (type, expiry UTC, last claim day, monthly claim counter), reward counters (per day and per week), first-purchase flags per pack. Gear fields of older saves are ignored on load.

### 8.2 Data files [CODE]

`Content/economy/`: `powerups.json`, `gem_packs.json`, `coin_packs.json`, `subscriptions.json`, `bundles.json`, `rewards.json`. Chest numbers are constants in `Chests.cs`. No `gear.json`, `chests.json` or `characters.json`.

### 8.3 Remote config

Planned keys for prices and ranges; today `remote_config.json` carries `economy.coinsPerGem` (**20**), `replayCoinPercent` 50, `regionCoinScalePercent` 3 (`vipGemsPerDay` was deleted in RC 1), the endless coin cap values.

### 8.4 Analytics

No analytics layer exists (doc 22 item 16). Planned events: `powerup_used`, `powerup_bought`, `chest_open`, `character_selected`, `subscription_claimed`, `reward_claimed`.

---

## 9. Screens (FR-INV)

Screen flow now: Home -> Shop (5 tabs), Subscription button, Heroes (characters), Rewards (Main, Daily, Weekly), chest button (Chests tab); Map > Levels > level popup (goal, stars, PLAY; **no loadout row**) > Run. **No Inventory screen, no Wardrobe, no Dress button** (doc 20).

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-INV-1 | SaveData and power-up stock services with immediate save and migration. | P0 | Old saves load. | Built |
| FR-INV-2 | Home buttons: Shop, Heroes, Rewards, Subscription, chest; layout audit. | P0 | Four aspects at 130 percent. | Built |
| FR-INV-3 | **Removed 2026-11-01:** Inventory screen with Gear, Power-ups, Chests tabs. | - | - | Removed |
| FR-INV-4 | Character (Heroes) screen. | P0 | See FR-CHAR-5. | Built |
| FR-INV-5 | **Removed or superseded 2026-10-31:** loadout picker on the level popup. | - | - | Removed |
| FR-INV-6 | Run HUD power-up buttons (FR-PWR-3). | P0 | Layout audit. | Built |
| FR-INV-7 | All screens meet the critical UX rules. | P0 | CriticalUX audits. | Built |
| FR-INV-8 | Screens open without hitch; pooled rows. | P0 | Open-time checks. | Built |
| FR-INV-9 | Missing art never crashes. | P0 | Delete-a-sprite test. | Built |
| FR-INV-10 | Android back closes a modal first, then the screen. | P0 | Device test. | Built |
| FR-INV-11 | Analytics `character_screen_opened`. | P1 | - | Planned |

---

## 10. Tests

Built: `PowerUpTests`, `ChestTests`, `BundleTests`, `SubscriptionTests`, `RewardsTests`, `ShopEconomyTests`, `ReviveTests`, `CharacterRigTests` (Momo and Sporeling rigs; gear parts removed from tests), simulation per-region power-up tests (`Power_<Name>_<Region>`), `GemCoinEconomyTests`, chaos/autopilot runs. Removed: all gear tests (T-GEAR-*). Planned: max-bonus bot proof is moot (no bonuses).

## 11. Tasks (status)

| Group | Tasks | State |
|---|---|---|
| T-PWR-1..6 | Data, HUD, Flight/Rocket, Double Jump/Wall Stick, Shield/Magnet/Slow Time, analytics | Done except analytics and the 4 hidden power-ups |
| T-PWR-7 | Power-up icons real art | Done for the 7 sold types |
| T-CHEST-1..6 | Chest data, roller, screen, animation, art | Done (Iron, Gold); pity, shards, earn rules **removed**, server roll Planned |
| T-GEAR-1..7 | All gear tasks | **Removed (no gear)** |
| T-VIP-1..4 | VIP tiers, screen, subscriptions, art | **Superseded** by the Subscription (screen Done, billing Planned); crown/gear art removed |
| T-CHAR-1..9 | 20-character data, abilities, regional art | **Dropped**; 2 characters Done; Uncommon+ characters Planned |
| T-INV-1..5 | Inventory service, screens, loadout picker | SaveData Done; inventory screen and loadout picker **Removed** |
| T-REGION-1..4 | Run config, sim framework, max-bonus proof, matrix | Sim framework and matrix Done; run config and bonus pipeline **Removed** |

OpenArt credits: nothing here spends any; every generation needs the owner's approval packet.

## 12. Open questions

Tracked in **doc 27 section 18** and doc 22's open items: when characters go live (chest odds, prices), whether to rework power-up prices by duration, whether the four hidden power-ups are built, Ranked rules, and when to delete the gear leftovers from content and old docs 17 to 19.
