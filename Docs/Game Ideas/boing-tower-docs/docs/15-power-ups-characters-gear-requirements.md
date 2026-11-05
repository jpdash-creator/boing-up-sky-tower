# 15 - Power-ups, chests, VIP, characters, inventory: requirements (gear removed 2026-11-01)

> The file name keeps "gear" so existing links keep working. **Gear (hats, shirts, pants, boots, Gear Chests, style shards, wardrobe layering) was dropped by the owner on 2026-11-01 and is removed from this document.** Requirement ids of the old gear section are kept as "removed" markers, not renumbered. **The coin to gem rate is fixed at 1 gem = 20 coins** (owner, 2026-11-01); every coin price below is 20 x the gem price for items sold in both currencies.

Status: requirements written 2026-10-20, revised 2026-11-01. **Nothing in this document is built.** It extends [14-shop-requirements.md](14-shop-requirements.md) (shop shell, Tab 4, billing flows), which stays the owner of the shop UI, currency tables and purchase state machine; this document defines what Tabs 1 to 3 sell and the screens around them. Docs 14 sections 7 to 9 are superseded in detail by this document (the structure there stays; their gear parts are dropped). Economy numbers are cross-checked in [16-economy-comparison.md](16-economy-comparison.md).
Sources: owner input and decisions of 2026-10-20 and 2026-11-01 (section 1), `Assets/_Game/Simulation/NOTES.md` (jump numbers, goals, gongs), `Content/regions/*.json`, `Content/config/modules.json`, doc 08, doc 11 / critical UX rules, doc 13, doc 14.
No game or studio is named in this project; patterns are described generically.

Legend. Priority (a **proposal** for the owner to reorder): **P0** needed for the first release of the group, **P1** soon after, **P2** later. **Min** = Claude minutes (+-40 percent). Tags: **[OWNER]** decided by the owner, **[PROPOSAL]** my suggestion, **[ASSUMPTION]** my default, **(verify)** depends on store policy or prices. Art: **no OpenArt credit is spent by anything here**; every generation needs the owner's explicit approval (guardrail `.claude/rules/openart-credit-guardrail.md`, 15 credits per image, 2 images per job = 30 credits per job).

---

## 1. Owner input and decisions (verbatim summary)

| ID | Item | Tag |
|---|---|---|
| O-1 | Power-ups: 1 Flight, 2 Rocket, 3 Double Jump, 4 Wall sticking, 5 many more. | [OWNER] |
| O-2 | Chests: Coin Chests, Power-up Chests. (Gear Chests were part of the first list and are dropped.) | [OWNER] |
| O-3 | ~~Gear (ornamental for now): Hats, Shirts, Pants, Boots.~~ **Dropped 2026-11-01.** | [OWNER] |
| O-4 | VIP: 6 levels, each a higher percent of features: larger jump, more percent of gems and coins. | [OWNER] |
| O-5 | Characters: each region 3 to 5 characters, 1 free, the rest bought; extra features (more gems, higher jump, flight and more). A Character screen and an Inventory screen. | [OWNER] |
| O-6 | **Everything must work in all 5 regions.** | [OWNER] |
| O-7 | **1 gem = 20 coins is fixed and not changed** (2026-11-01). | [OWNER] |
| O-8 | **No gear anywhere** (2026-11-01). | [OWNER] |
| D-1 | Power-ups are **owned stock** (consumables bought in the shop or from chests, kept per type with a count). Up to **3** are picked as a **loadout** before a run; in the run each has a button (or auto-trigger) with limited duration or uses and is used up on activation. Usable **everywhere** (campaign incl. boss levels, and endless), all 5 regions; they never change what a star needs; every level stays bot-proven solvable **without** power-ups; each power-up is tested against every region's rules. | [OWNER] |
| D-2 | Chests are sold in Tab 1 for gems or coins in tiers (Common, Rare, Epic, Legendary if recommended), also earned (level milestones, region completion, daily login, battle pass). Opened instantly by a tap with an animation. Odds table in the shop and the chest dialog. Pity counter guarantees rare-or-better within N chests. Server-authoritative odds are a later production topic. (Gear duplicate conversion removed with gear.) | [OWNER] |
| D-3 | ~~Gear: 4 slots, ornamental only, layered on the character.~~ **Dropped 2026-11-01.** | [OWNER] |
| D-4 | VIP: six subscription tiers VIP 1 to VIP 6 (weekly or monthly), one active at a time, higher price and bigger bonuses each (jump power percent, gem percent, coin percent, daily gems, ad-free from a tier I propose). A larger jump must never break the layouts. | [OWNER] |
| D-5 | Characters: **4 per region for the 5 regions (20)**: 1 free per region (unlocked when the region opens), 3 bought with coins or gems (price rises with rarity). Each has ONE signature ability plus small stat bonuses. Abilities are sim-deterministic. **Art scope: only the 4 characters of region 1 get real art now**; regions 2 to 5 are data plus placeholder recolours of Momo. | [OWNER] |
| D-6 | A Character screen and an Inventory screen (power-ups, chests), reachable from the main menu and the shop. (The Wardrobe/dressing part is dropped with gear.) | [OWNER] |

### 1.1 Conflicts with existing rules (the owner decides; nothing applied unless marked resolved)

| ID | Conflict | Proposal |
|---|---|---|
| X-1 | `CLAUDE.md`: "Cosmetics only: items never change gameplay stats. Ranked runs disallow consumables and permanent upgrades." D-1, D-4 and D-5 add consumables, VIP jump bonuses and character abilities that change gameplay. | With gear gone the cosmetic items are character looks, VIP badges and frames (no stats). Define **Ranked** runs as "neutral": no loadout, no character ability or stats, no VIP bonuses, one fixed reference character. Casual campaign and Casual endless allow everything. Ranked boards (milestone levels, global) stay fair. Update `CLAUDE.md` once approved. |
| X-2 | **Resolved 2026-11-01 [OWNER]:** doc 08 power-up prices assumed 100 coins per gem; doc 14 set 20 (promo) / 10 (list). | The rate is fixed at **1 gem = 20 coins** (O-7). Every coin price in this document = 20 x the gem price. No other rate is used anywhere. |
| X-3 | A Coin Doubler priced like other consumables can never pay back, and at 120 coins pays more than it costs (a 3-star level pays about 150 to 170 coins). | **Coin Doubler is removed from the shop (2026-11-01).** It stays only as a rewarded-ad reward and a chest item (section 3.2). |
| X-4 | Gongs only knock back and never kill (`GongKnockback`), hazards are few; "Shield absorbs one fall or hit" would overlap Safety Net. | Shield absorbs one hazard hit or knockback; Safety Net saves from a fall. |
| X-5 | Doc 14 FR-SHOP-53 says one VIP subscription with weekly and monthly prices; D-4 asks for six tiers. | Six tiers, each weekly and monthly (section 6). Doc 14 points here. |

---

## 2. Cross-cutting: it works in all 5 regions, deterministically (FR-REGION)

The five playable regions and their mechanics (`Content/regions`, `modules.json`): **1 Meadow Springs** (bounce flowers), **2 Mushroom Grove** (bounce caps, high launch), **3 Sunny Beach** (drifting platforms), **4 Candy Canyon** (sticky gum, reduces jump height), **5 Bamboo Temple** (swinging gongs, knockback, never kill). Platform kinds common to all: normal, narrow, moving, crumble, bounce; walls on both sides (wall bounce). Slippery and wind platforms do not exist yet (regions 6+): every item below must be re-tested when such a mechanic is built (FR-REGION-9).

**Hard numeric limits (proof basis).** Standing jump peak is 4.51 u; `NormalGapMax` is 4.2 u; layouts are sized from `LayoutMaxJumpHeight` 5.86 u; two median row gaps are about 6.96 u. Jump height scales with the square of the launch speed, so a bonus "+p percent jump power" is defined as **+p percent peak height** (launch speed multiplied by sqrt(1 + p/100), taken from an integer table of 0 to 25 so the sim stays fixed-point and deterministic). Total permanent jump bonus (VIP + character) is capped at **+25 percent peak = 5.64 u**, below the layout reference 5.86 u and below two median gaps (6.96 u). A temporary effect (Spring Boots) may add on top but the sim clamps any single jump to a hard peak of **6.5 u**. Consequence: a bigger jump only makes platforms easier to reach; no normal gap (at most 4.2 u) can become unreachable, and a standing jump can still not cross two median rows.

| ID | Requirement | Pri | Acceptance criteria | Min |
|---|---|---|---|---|
| FR-REGION-1 | **Run config.** Every run starts from a `RunConfig` = {level or endless seed, loadout[3], character id, jump bonus percent, gem percent, coin percent, ability parameters}. The config is part of the run record, is hashed (`runConfigHash`), and is passed to the sim, the reward calculator and the server. The level generator never reads it: layouts depend only on level id and seed. | P0 | Same seed with any config yields identical platform ids and positions (golden hashes `Content/golden/level_hashes_r1-5.txt` unchanged); config hash stored in the run result. | 90 |
| FR-REGION-2 | **Bonus pipeline.** One sim-side pipeline applies jump, pickup and payout bonuses in a fixed order: base x region factor x (1 + (VIP + character) percent) x temporary multipliers; integer math, rounding half up, caps: jump +25 percent peak, gem +40 percent, coin +60 percent (permanent sources). | P0 | Unit tests per cap; reward totals reproducible by the server from the config. | 120 |
| FR-REGION-3 | **Deterministic features.** Each power-up and each character ability is a Simulation feature with its own input bit or auto-trigger rule, integer/fixed-point only, no UnityEngine types, seeded RNG only. Replays contain the activation ticks. | P0 | `dotnet test Tests/Simulation`: replay of a recorded run with power-ups gives the same final state hash on two runs. | 60 |
| FR-REGION-4 | **Solvable without help.** All 450 levels (and regions 1 to 5 now: 75) stay solvable by the bot with an empty loadout and the neutral character. Additionally the bot solves all 75 levels with the **maximum permanent bonus** (+25 percent jump). | P0 | Existing 75/75 test plus a new max-bonus variant; both green. | 60 |
| FR-REGION-5 | **Stars unchanged.** Power-ups and abilities never alter goal definitions, star requirements, pickup positions or the number of stars. They may help collecting a star in play (that is the point), never replace it (rocket and flight cannot teleport through a star check). | P0 | Goal/star tests run unchanged; a rocket run that skips a star leaves the star uncollected. | 30 |
| FR-REGION-6 | **Per-region interaction matrix.** Each of the 11 power-ups and each of the 20 character abilities is tested against each of the 5 regions' mechanics on a simple level (L1), a complex level (L5) and a highly complex level (L15), plus one endless tower per region. | P0 | Section 3.3 matrix has zero failures; tests named `Power_<id>_<region>` and `Ability_<id>_<region>`. | 360 |
| FR-REGION-7 | **Boss/gauntlet levels (L15) and endless** allow loadout and abilities. Endless rewards (floors coins with the daily cap) apply bonus percentages after the cap rule. | P0 | Tests on `lvl_00x_15` and `EndlessTower` for each region. | 60 |
| FR-REGION-8 | **Region-scoped content.** Characters, chests earned and prices are data keyed by region index; a region's character becomes buyable only when that region is unlocked; owned content stays usable everywhere. Region 6+ adds data, no code. | P0 | Adding a fake region 6 in a test fixture exposes 4 characters without code changes. | 60 |
| FR-REGION-9 | **New-mechanic checklist.** When a region adds a mechanic (slippery, wind, others), the checklist re-runs FR-REGION-6 for that mechanic before the region goes live. | P1 | Checklist in `docs/` and in the content CLI. | 30 |
| FR-REGION-10 | **Ranked neutrality (X-1).** Ranked runs ignore loadout, ability, stats and VIP bonuses. | P0 | Ranked run config hash equals the neutral config; server rejects a ranked score with a non-neutral hash. | 45 |
| FR-REGION-11 | **Screens obey the critical UX rules** in all 5 regions' backgrounds (text over art has a backing, CUX-5) and at 4 aspects x 130 percent text. | P0 | PlayMode audit per new screen. | 0 (in FR-INV) |

---

## 3. Power-ups (FR-PWR)

### 3.1 Rules

- **Owned stock:** a count per type in the inventory (cap 99). Bought in Tab 2 or found in Power-up Chests.
- **Loadout:** up to 3 slots, chosen before the run in the **loadout picker** on the level popup (section 9.4) or in the Inventory. One type per slot. A slot carries up to the type's **carry cap** (below) from the stock. The same loadout is remembered per player and re-used for the next level.
- **In the run:** one round button per slot on the HUD (bottom, thumb zone; mirrored for left-handed play, section 9.6). Tapping activates it, uses up one unit, and shows a ring with the remaining time or uses. Types marked auto trigger on their own condition and show an icon, not a button.
- **Stacking:** two different types may be active together. The same type cannot be re-activated while it is active (button disabled with a visible "active" state). Temporary multipliers multiply once (never compound twice from the same type).
- **Unused units** return to the stock at the end of the run; units used are gone, including on a failed run (a revive keeps active effects running).
- **Ranked** ignores the loadout (FR-REGION-10).
- **Where:** campaign (all level classes including L15) and endless, in all 5 regions.

### 3.2 Roster of power-ups (first 4 are the owner's; 7 proposed to fill "many more")

Durations are in seconds of play (60 sim ticks per second). **Prices: gems / coins with coin price = 20 x gems (O-7)**; bundles of 5 (-10 percent) and 10 (-20 percent) [PROPOSAL].

| # | Power-up | Effect (sim feature) | Duration / uses | Carry cap per run | Stacking | Price (gems / coins) | Trigger |
|---|---|---|---|---|---|---|---|
| 1 | **Flight** [OWNER] | Gravity off; the player rises at 8 u/s and steers sideways at normal air speed; hazards cannot hit; no pickups skipped (collision stays on). At the end a 1 s grace: landing on any platform is safe. | 4 s | 1 | none while active | 20 / 400 | Button |
| 2 | **Rocket** [OWNER] | Straight upward launch at 14 u/s, no steering, hazards cannot hit; the player cannot pass the goal row or the last 3 rows of a level (stops there). Ends with 1 s slow fall. | 2 s (about 28 u, 8 rows) | 1 | none while active | 16 / 320 | Button |
| 3 | **Double Jump** [OWNER] | One extra mid-air jump with standing-jump strength (same sim jump routine, counts as a jump for combo). | 1 use | 3 | uses add up | 8 / 160 | Button |
| 4 | **Wall Stick** [OWNER] | For 10 s touching a wall holds the player for up to 1.5 s (no fall), then jump or release; wall-bounce counters still count. | 10 s | 2 | does not extend while active | 8 / 160 | Button |
| 5 | **Shield** [PROPOSAL] | Absorbs the next hazard hit or gong knockback (the hit is consumed, no knockback, short sparkle). | until hit, max 25 s | 2 | one shield at a time | 10 / 200 | Auto on hit after activation |
| 6 | **Magnet** [PROPOSAL] | Gems and stars within 4 u are pulled to the player (pickup collision unchanged, just a moving pickup position computed in fixed point). | 10 s | 2 | no stacking | 8 / 160 | Button |
| 7 | **Slow Time** [PROPOSAL] | The auto-scroll speed is reduced by 40 percent; the player speed is unchanged. | 6 s | 2 | not with itself | 10 / 200 | Button |
| 8 | **Spring Boots** [PROPOSAL] | The next 3 jumps are 20 percent higher (peak), subject to the 6.5 u hard clamp. (A temporary power-up, not gear.) | 3 jumps | 2 | refresh, not add | 10 / 200 | Button |
| 9 | **Coin Doubler** [PROPOSAL, X-3] | The end-of-level coin payout of this run is multiplied by 2 (after region factor and VIP/character percent). No effect on gems or stars. **Not sold in the shop**: rewarded-ad reward and chest item only. | whole run | 1 | once per run | not sold | Button (any time before the finish) |
| 10 | **Star Seeker** [PROPOSAL] | Shows an arrow at the screen edge towards each uncollected star within 12 u. A HUD hint only; no sim effect except reading pickup positions. | 12 s | 2 | no stacking | 8 / 160 | Button |
| 11 | **Safety Net** [PROPOSAL] | The first time the player would fall off the bottom edge, bounce back up to the last platform touched (once) and consume the net. | 1 use | 1 | one at a time | 14 / 280 | Auto |

"Many more" later: Ghost Step (pass through one crumbling platform), Gem Pulse (next 5 pickups count as one tier higher), Lucky Landing (one free combo step). Each new one needs the FR-PWR-8 template.

### 3.3 Interaction matrix with the region mechanics (must be tested, FR-REGION-6)

| Power-up | R1 Bounce flowers | R2 Bounce caps | R3 Drifting | R4 Sticky gum | R5 Gongs | Crumble / moving (all) |
|---|---|---|---|---|---|---|
| Flight | steering over flowers; no extra launch while flying | flying ignores caps | rises independent of drift | not stuck while flying | immune to gongs | passes over |
| Rocket | stops at row limit | same | same | same | immune | same |
| Double Jump | allowed after a flower launch | allowed after cap launch (peak clamp 6.5 u) | landing on a drifting platform keeps the use | sticky penalty applies to the second jump too | knockback does not cancel the use | works |
| Wall Stick | works | works | works | works | works | works |
| Shield | n/a (no hazards, kept) | n/a | n/a | n/a | absorbs one gong hit | absorbs moving hazards |
| Magnet | pulls | pulls | pulled pickups follow drift-independent fixed path | pulls | pulls | pulls |
| Slow Time | scroll only | scroll only | drift speed unchanged | unchanged | gong period unchanged | unchanged |
| Spring Boots | flower launch +20 percent clamped | cap launch clamped 6.5 u | applies | counters the sticky penalty partly (penalty first, boots after) | applies | applies |
| Coin Doubler | payout x2 | x2 | x2 | x2 | x2 | x2 |
| Star Seeker | hint | hint | hint follows drifting stars | hint | hint | hint |
| Safety Net | rescue to last platform | same | last platform may have drifted: rescue to its current position | same | same | if crumbled, to the nearest platform below |

### 3.4 Requirements

| ID | Requirement | Pri | Acceptance criteria | Min |
|---|---|---|---|---|
| FR-PWR-1 | Owned stock per type (cap 99) saved at once (section 8). | P0 | Buy/chest/use change the count; old save loads with 0. | 45 |
| FR-PWR-2 | Loadout of up to 3 slots, one type per slot, per-run carry caps of 3.2, remembered between runs. | P0 | Picker rejects a 4th slot; carry cap respected; loadout saved. | 60 |
| FR-PWR-3 | HUD power-up buttons: round, at least 48 dp, in the thumb zone, ring for remaining time/uses, label by icon plus a short text name (not colour only), left-handed mirror, no overlap with pause, jump, move pad or the next platforms; disabled state shown by shape (cross) and the accessibility label. | P0 | Layout audit of the run HUD with 3 buttons on 4 aspects, 130 percent text, left-handed. | 120 |
| FR-PWR-4 | Flight and Rocket sim features with the limits of 3.2 (row limit stopping at the goal, hazard immunity, grace). | P0 | Sim tests: cannot pass the goal row early; star/hidden gem not auto-collected; deterministic replay. | 180 |
| FR-PWR-5 | Double Jump, Wall Stick, Spring Boots sim features. | P0 | Sim tests incl. 6.5 u clamp and sticky interaction. | 150 |
| FR-PWR-6 | Shield, Safety Net, Slow Time, Magnet, Star Seeker, Coin Doubler features. | P1 | Sim tests per item; reward test for the doubler. | 240 |
| FR-PWR-7 | Auto-trigger rules (Shield, Safety Net) are deterministic and show a clear visual and audio cue; no flash above 3 per second; reduced motion replaces particles by a static icon. | P0 | Audit of cue frequency; reduced-motion test. | 45 |
| FR-PWR-8 | Template for every new power-up: effect, duration/uses, carry cap, stacking, price, trigger, sim input, region matrix, tests. | P1 | Template checked in; content CLI validates `powerups.json`. | 30 |
| FR-PWR-9 | Data file `Content/items/powerups.json` (section 8.2) and remote overrides for price, duration and cap (`powerups.*`). The validator fails the build if a priced power-up has `coins` different from 20 x `gems`. | P0 | Change of a value in config changes the run without code; validator test. | 60 |
| FR-PWR-10 | Power-ups appear in Tab 2 (shop), in Power-up Chests, and in the Inventory with counts; a power-up out of stock shows "Buy" in the loadout picker. Coin Doubler is not listed in the shop. | P0 | Flow test: picker > buy > back keeps the picker. | 90 |
| FR-PWR-11 | Accessibility: each power-up has a spoken label ("Flight, 4 seconds, 2 left"); activation feedback within the same frame (CUX-2). | P0 | Label test; latency check. | 30 |
| FR-PWR-12 | Analytics: `powerup_equipped`, `powerup_used` (type, tick, row, region), `powerup_bought`, `powerup_rewarded`. | P1 | Events in the log. | 30 |

---

## 4. Chests (FR-CHEST)

### 4.1 Kinds, tiers, prices, contents

Two kinds [OWNER, 2026-11-01]: **Coin Chest** (pays coins; **earn-only, never sold**) and **Power-up Chest** (pays power-ups; sold). ~~Gear Chest~~ is dropped with gear. Four tiers [PROPOSAL]: Common, Rare, Epic, **Legendary (recommended)**, because the ladder needs a top prize for gems and for battle pass rewards; Legendary is gem-only and never earned from milestones.

Sale prices of the **Power-up Chest** (1 gem = 20 coins; coin route only where shown; "-" = not sold for that currency) [PROPOSAL, price = about 70 percent of expected value, see doc 16]:

| Tier | Power-up Chest, gems | Power-up Chest, coins (20 x gems) |
|---|---|---|
| Common | 14 | 280 |
| Rare | 26 | 520 |
| Epic | 45 | - |
| Legendary | 85 | - |

Coin Chests have no price (earned only).

Contents (what one chest pays) [PROPOSAL]:

| Tier | Coin Chest coins (earn-only) | Power-up Chest (count x type) |
|---|---|---|
| Common | 400 to 900 | 2 to 3 units of 1 common type |
| Rare | 1,500 to 3,000 | 3 to 5 units, 2 types |
| Epic | 5,000 to 9,000 | 5 to 8 units, 3 types, one of Flight/Rocket possible |
| Legendary | 15,000 to 25,000 | 8 to 12 units, 4 types, Flight/Rocket guaranteed one |

Power-up types roll by weight: common (Double Jump, Wall Stick, Magnet, Star Seeker, Coin Doubler) 70 percent, uncommon (Slow Time, Shield, Spring Boots, Safety Net) 25, rare (Flight, Rocket) 5 in Common/Rare chests, 15 in Epic, 100 pool in Legendary. The result rarity of a Power-up Chest is the highest class it contains: **Common** (common types only), **Rare** (at least one uncommon type), **Epic** (at least one Flight/Rocket). Final tables live in remote config with a version.

Pity [PROPOSAL, **Power-up kind only**]: a counter of Power-up Chests opened since the last Rare-or-better result (guarantee within **10** chests) and since the last Epic-or-better result (guarantee within **30** chests); the counters are shown on the chest dialog; they reset when the guaranteed rarity drops. A Legendary chest counts as 3. Coin Chests have no pity (earn-only, no purchase). ~~Legendary pity within 90 (gear only)~~ removed.

~~Duplicates and Style Shards~~ removed with gear (power-ups stack, so there are no duplicates).

Earning chests [OWNER, values PROPOSAL, aligned with doc 16 section 8]:

| Source | Reward |
|---|---|
| First clear of level 5 / 10 of a region | Common Coin Chest / Common Power-up Chest |
| First clear of level 15 (region completion) | Rare Power-up Chest + Rare Coin Chest; at 3 stars on all 15 levels an Epic Power-up Chest |
| Daily login (7-day loop) | Day 1 to 6 small coins and a Common Power-up Chest on day 3, a Common Coin Chest on day 6; day 7 a Rare Power-up Chest |
| Endless | First time reaching floors 100 / 250 / 500 per region: Common / Rare / Epic Coin Chest |
| Battle pass (doc 14) | Power-up Chests on selected tiers |
| VIP | One Rare Power-up Chest per week at VIP 4 and above |
| Rewarded ad | One Common Coin Chest, cap 2 a day (doc 05, doc 16) |

Earned chests are stacked in the inventory.

### 4.2 Requirements

| ID | Requirement | Pri | Acceptance criteria | Min |
|---|---|---|---|---|
| FR-CHEST-1 | Chest data (`Content/items/chests.json`, section 8.2): kind (`coin`, `powerup`), tier, prices (Power-up only), contents, odds, pity group, art. | P0 | Validator checks odds sum to 100 per tier; Coin Chests have no price; Power-up coin price = 20 x gems. | 60 |
| FR-CHEST-2 | Tab 1 Chests lists the **4 Power-up Chest tiers** (4 cards) in a scroll list with price buttons (coins or gems where both exist), an "Odds" link, and the pity counter. Coin Chests appear only in the Inventory. Insufficient funds uses doc 14 FR-SHOP-21. | P0 | Layout audit; purchase flow tests; confirm above 50 gems (doc 14 FR-SHOP-20). | 150 |
| FR-CHEST-3 | **Opening by one tap** from the shop, the inventory or an earn popup; a 1.2 s opening animation, skippable by a second tap; reduced motion shows the result at once; no flash over 3 per second. | P0 | Animation skip test; reduced-motion test. | 120 |
| FR-CHEST-4 | Result dialog: shows the item(s) with class name (Common/Rare/Epic) + frame shape, an "Add to loadout" action for power-ups, and an "Open another" button when more chests are owned. | P0 | Layout audit; labels not colour-only. | 90 |
| FR-CHEST-5 | **Odds disclosure** (store compliance): the odds table of the exact chest (class percent and type weights) is shown in the shop card, in the confirm dialog and in the opening dialog; the displayed values come from the same table the roller uses. | P0 | Unit test: shown odds = roller table; text reachable before purchase. | 60 |
| FR-CHEST-6 | **Pity counters** for the Power-up kind, saved; guarantee holds (never more than N-1 chests without a Rare-or-better / Epic-or-better). | P0 | Simulation of 100,000 openings: max streak <= 9 and 29. | 90 |
| FR-CHEST-7 | **Removed 2026-11-01 (no gear).** (Was: duplicates convert to Style Shards.) | - | - | 0 |
| FR-CHEST-8 | **Earn rules** of 4.1 implemented as data (`chests.sources`), each granting exactly once per trigger and saved at once (no loss on app kill). | P0 | Idempotence test (restart after grant). | 120 |
| FR-CHEST-9 | Chests in the inventory stack by kind and tier with counts and "Open" buttons (section 9.3). | P0 | See FR-INV. | 0 (FR-INV) |
| FR-CHEST-10 | **Randomness:** development builds roll on the device with a seeded RNG stored per chest (`chestSeed`) so tests are reproducible; production rolls on the server (later topic): the client sends "open chest X", the server returns the result and updates the pity counters. Chest purchases with real gems must not be possible without the server in a release build. | P0 | Release guard like doc 14 FR-SHOP-90. | 60 |
| FR-CHEST-11 | Compliance: odds before purchase, age gate, spending limits and parental PIN apply to chest purchases (doc 14 section 12); regions where random paid items are restricted can be switched off by `chests.enabled` per country. | P0 | Flag test. | 45 |
| FR-CHEST-12 | Anti-abuse: chest purchases and openings are rate-limited and logged server-side. | P1 | Server tests (later). | 60 |
| FR-CHEST-13 | Analytics: `chest_earned`, `chest_bought`, `chest_opened` (kind, tier, result class, pity before/after), `chest_odds_viewed`. | P1 | Events in the log. | 30 |

---

## 5. Gear (FR-GEAR) - removed 2026-11-01

The owner dropped all gear (Hats, Shirts, Pants, Boots, rarity frames, style levels and shards, layering, rigs and anchors for gear, the placeholder gear generator, event gear). This section is intentionally empty. The ids below are kept only so older references resolve.

| ID | Status |
|---|---|
| FR-GEAR-1 to FR-GEAR-12 | **Removed 2026-11-01 (no gear).** |

What stays from the old section: characters keep their own art only (no layers, no anchors). A character has one sprite set (idle, jump, fall, portrait; section 7.2 FR-CHAR-7). Squash and stretch on the character root stay as before.

---

## 6. VIP (FR-VIP)

### 6.1 Six tiers [PROPOSAL; prices (verify) store price points]

One active tier at a time. Bonuses are **additive percent multipliers** into the bonus pipeline (FR-REGION-2). Jump is +% peak height (see section 2 limits). Gem % applies to gems picked up in runs and milestone rewards, never to purchased gems or prices. Coin % applies to end-of-level payouts and endless coins.

| Tier | Monthly $ | Weekly $ | Jump power (peak) | Gems | Coins | Daily gems | Ad-free | Extras (no gear) |
|---|---|---|---|---|---|---|---|---|
| VIP 1 | 3.99 | 1.49 | +2% | +5% | +5% | 0 | no | crown name badge |
| VIP 2 | 6.99 | 2.49 | +4% | +10% | +10% | 5 | no | |
| VIP 3 | 9.99 | 3.49 | +6% | +15% | +20% | 10 | yes (no prompts, optional rewarded ads stay) | |
| VIP 4 | 14.99 | 4.99 | +8% | +20% | +30% | 15 | yes | weekly Rare Power-up Chest |
| VIP 5 | 21.99 | 7.49 | +10% | +25% | +40% | 20 | yes | weekly Rare Power-up Chest, profile frame |
| VIP 6 | 32.99 | 10.99 | +12% | +30% | +50% | 25 | yes | weekly Rare Power-up Chest, exclusive victory animation, name badge |

Extras are profile items and effects only (name badge, profile frame, victory animation); none changes gameplay and none is wearable gear.

Checks. **Level solvability:** VIP 6 gives +12% and the best character +6% = +18%, inside the +25 percent cap (5.64 u). Gem income: about 10.7 gems per level placed, so +30% is +3 gems per level at most, a full 5-region clear adds about 50 to 60 gems (about $0.8 to $1.8 of value at the doc 14 rates). Coin income: +50% on 2,250 coins per region is 1,125 more. Daily gems: VIP 6 25 x 30 = 750 gems a month for $32.99 (23 gems per $, worse than the best pack's 67, so VIP never undercuts packs; this fixes doc 14 conflict C-5 by construction). Monthly price per tier is about 1.5 times the one below, weekly about 35 percent of monthly. The weekly Rare Power-up Chest is worth about 37 gem-equivalents (about 148 a month).

### 6.2 Requirements

| ID | Requirement | Pri | Acceptance criteria | Min |
|---|---|---|---|---|
| FR-VIP-1 | Six subscription products, each with a weekly and a monthly base plan (12 offers) in one subscription group; one tier active at a time. | P2 | Store console set-up checklist; test accounts. | 120 |
| FR-VIP-2 | Tier table in config (`vip.tiers`) and applied only through the bonus pipeline; hard caps of FR-REGION-2 enforced even if config is wrong. | P2 | Test with a 60 percent jump in config: sim clamps to +25. | 60 |
| FR-VIP-3 | **Upgrade/downgrade:** upgrade takes effect at once (store proration), downgrade at the next renewal; the screen shows "Current", "Starts on <date>". | P2 | State tests (store sandbox). | 120 |
| FR-VIP-4 | Auto-renew disclosure on the card: price, period, renews automatically until cancelled, how to cancel, links to terms and privacy (doc 14 FR-SHOP-55); text readable at 130 percent without scrolling away from the buy button. | P0 for VIP | Layout audit; wording review (verify). | 45 |
| FR-VIP-5 | Entitlement is server-side (tier, expiry); offline grace 3 days; states: active, grace, on hold, cancelled-but-active, expired, refunded; state shown in plain words. | P2 | Airplane-mode and state tests. | 150 |
| FR-VIP-6 | **Restore:** tier and expiry restored at start and by Settings > Restore purchases; a reinstall restores the tier. | P0 for VIP | Reinstall test. | 45 |
| FR-VIP-7 | Daily gems claimed once per UTC day by a Claim button (not automatic), accumulating up to 3 days if missed [ASSUMPTION]. | P2 | Date tests, server time. | 45 |
| FR-VIP-8 | Ad-free from VIP 3: no interstitial and no ad prompts; rewarded ads remain optional. | P2 | Ads module respects the flag. | 30 |
| FR-VIP-9 | VIP profile items (name badge, profile frame, victory animation) are granted while the tier is active and kept after expiry [ASSUMPTION]. No gear. | P2 | Ownership test. | 45 |
| FR-VIP-10 | VIP bonuses are excluded from Ranked (FR-REGION-10) and the bonus lines are shown on the results screen ("VIP +20% coins"). | P2 | Results layout audit. | 45 |
| FR-VIP-11 | VIP screen in Tab 3: tier carousel of 6 cards with the bonus list as text, weekly/monthly toggle, price from the store, Subscribe/Change/Manage buttons; accessible and left-hand friendly. | P2 | Layout audit at 130 percent. | 150 |
| FR-VIP-12 | Analytics: `vip_viewed`, `vip_started`, `vip_changed`, `vip_cancelled`, `vip_expired`, `vip_daily_claimed`. | P2 | Events. | 30 |

---

## 7. Characters (FR-CHAR)

### 7.1 Roster of 20 [PROPOSAL; names are suggestions in each region's theme]

Rules for all: one signature ability; stats are small and capped (jump <= +6%, gem find <= +5%, coin find <= +5%); every character is a **Casual-only** advantage (X-1); abilities are passive, reaction (auto) or **air-tap** (uses the jump button while airborne, with a cooldown ring). Region mechanic hooks are listed so each ability works with its own region and is still tested in the other four (FR-REGION-6). Prices rise with rarity and with the region; buyable only when the region is open; the free character unlocks with the region. **Rate: 1 gem = 20 coins.** Rare and Epic characters can be bought with coins or with the equal gem amount (coins / 20); Legendary characters are gem-only.

| # | Region | Character | Rarity | Signature ability (sim feature) | Stats | Price | Unlock rule |
|---|---|---|---|---|---|---|---|
| 1 | R1 Meadow Springs | **Momo** | Common | Flower Friend: bounce flowers launch +15% | - | free | owned from the start |
| 2 | R1 | **Pip** (bee) | Rare | Hover: holding jump while falling slows the fall to 40% for up to 0.5 s per jump | gem find +3% | 4,000 coins or 200 gems | region 1 open |
| 3 | R1 | **Clover** (rabbit) | Epic | Bunny Streak: after a 5-step combo the next jump is +20% height once | jump +4%, coin find +3% | 9,000 coins or 450 gems | region 1 open |
| 4 | R1 | **Petal** (dragonfly) | Legendary | Wing Rescue: once per run, falling off the bottom turns into a 2 s flight upward (like Safety Net but needs no stock) | jump +6%, gem find +5% | 800 gems | region 1 open |
| 5 | R2 Mushroom Grove | **Spora** (mushroom sprite) | Common | Cap Cushion: landing on a bounce cap adds +1 combo step | - | free | region 2 opens |
| 6 | R2 | **Truffle** (mole) | Rare | Burrow Grip: touching a wall holds for 0.4 s | coin find +3% | 5,000 coins or 250 gems | region 2 open |
| 7 | R2 | **Glim** (glow-worm) | Epic | Glow Reach: pickup radius +25% | gem find +4%, jump +3% | 11,000 coins or 550 gems | region 2 open |
| 8 | R2 | **Baron Cap** (stag beetle) | Legendary | Cap Surge: after a bounce-cap launch, 1.2 s of extra upward drift | jump +6%, gem find +5% | 950 gems | region 2 open |
| 9 | R3 Sunny Beach | **Shelly** (hermit crab) | Common | Sand Grip: landing on a drifting platform cancels its sideways push for 0.3 s | - | free | region 3 opens |
| 10 | R3 | **Gully** (gull) | Rare | Sea Breeze: drifting platforms within 2 u move 30% slower | coin find +3% | 6,000 coins or 300 gems | region 3 open |
| 11 | R3 | **Tortuga** (turtle) | Epic | Sandcastle: crumbling platforms last twice as long | coin find +5%, gem find +3% | 13,000 coins or 650 gems | region 3 open |
| 12 | R3 | **Inky** (octopus) | Legendary | Ink Cloud: once per run, near the bottom edge the scroll slows 50% for 4 s | jump +6%, gem find +5% | 1,100 gems | region 3 open |
| 13 | R4 Candy Canyon | **Gumdrop** (jelly bear) | Common | Gum Boots: the sticky-platform jump penalty is halved (an ability, not gear) | - | free | region 4 opens |
| 14 | R4 | **Toffee** (otter) | Rare | Peel Off: no delay after landing on a sticky platform | gem find +3% | 7,000 coins or 350 gems | region 4 open |
| 15 | R4 | **Mint** (pup) | Epic | Sugar Rush: every 10 pickups, 3 s of +15% air speed | jump +4%, gem find +4% | 15,000 coins or 750 gems | region 4 open |
| 16 | R4 | **Sprinkles** (cake-dragon) | Legendary | Sugar Wings: air-tap flight 1.5 s, cooldown 20 s | jump +6%, coin find +5% | 1,250 gems | region 4 open |
| 17 | R5 Bamboo Temple | **Bao** (panda) | Common | Gentle Thud: gong knockback -40% | - | free | region 5 opens |
| 18 | R5 | **Kiri** (crane) | Rare | Gong Dodge: a gong hit is ignored once every 15 s | coin find +3% | 8,000 coins or 400 gems | region 5 open |
| 19 | R5 | **Ronin** (red panda) | Epic | Wall Run: touching a wall holds for 0.8 s and the next jump from it is +10% | jump +4%, gem find +4% | 17,000 coins or 850 gems | region 5 open |
| 20 | R5 | **Longyi** (dragon) | Legendary | Gong Surge: a gong hit becomes an upward boost of about 1.5 rows instead of knockback | jump +6%, coin find +5%, gem find +5% | 1,450 gems | region 5 open |

Price sanity: a region pays 2,250 to 2,520 coins at 3 stars, so a Rare character costs about 2 regions, an Epic 4 to 6. Legendary gem prices are **1.7 times the Epic gem price of the same region** (800 / 950 / 1,100 / 1,250 / 1,450 against 450 / 550 / 650 / 750 / 850), so rarity order holds; they are $12 to $21.75 at the best promo rate ($0.015 per gem) and $24 to $43.50 at $0.03 (doc 14 table 5.1). All five Legendary characters together are 5,550 gems. Overlaps with power-ups are intended but weaker (a power-up is stock, an ability is permanent but small). Flight-like abilities (4, 16) are limited by once-per-run or a 20 s cooldown. Characters are the only cosmetic and status item left now that gear is dropped, so their art quality matters (section 11).

### 7.2 Requirements

| ID | Requirement | Pri | Acceptance criteria | Min |
|---|---|---|---|---|
| FR-CHAR-1 | Character data (`Content/characters/characters.json`, section 8.2): id, region, rarity, name, ability id and parameters, stats, price (coins and/or gems), unlock rule, art keys. 4 per region for regions 1 to 5. | P0 | Validator: exactly 1 free per region; 20 entries; Rare/Epic gem price = coins / 20; Legendary gem price = 1.7 x the Epic gem price of the region (+/- 50 gems). | 90 |
| FR-CHAR-2 | The free character of a region unlocks when that region opens (progress or gem skip); bought ones become buyable then. Ownership is permanent (IDs permanent, project rule). | P0 | Unlock tests incl. skip path. | 45 |
| FR-CHAR-3 | Ability features in the Simulation (20), each with parameters in data, a deterministic trigger and a unit test; stats go through the bonus pipeline. | P1 | `Ability_<id>` sim tests. | 600 |
| FR-CHAR-4 | The selected character and its stats are part of the RunConfig (FR-REGION-1). Switching characters is free and saved. | P0 | Config hash changes with the character. | 30 |
| FR-CHAR-5 | Character screen (section 9.2): carousel of 20 (filtered by region tabs), large preview, stats and ability text, Select/Buy, locked ones explain why ("Unlock Sunny Beach first", "4,000 coins or 200 gems"). No dressing or layering. | P0 | Layout audit; text for every locked state. | 240 |
| FR-CHAR-6 | Purchase with coins or gems through the common flow (doc 14 section 6): confirm above 50 gems, insufficient funds route. | P1 | Flow tests. | 60 |
| FR-CHAR-7 | **Art scope:** region 1's four characters (Momo + 3 new) get real art; characters of regions 2 to 5 use Momo recolours (hue shift + a small badge icon per region) as placeholders defined in data (`art.placeholder: "momo_recolor_<hue>"`). Real art for regions 2 to 5 is a later packet (section 11). A character has one sprite set (idle, jump, fall, portrait). | P0 | Placeholder generator makes 16 recolours; R1 real art after owner approval. | 120 |
| FR-CHAR-8 | Placeholder characters are labelled in dev builds only; in release they simply look like the recolour. | P1 | None. | 15 |
| FR-CHAR-9 | Every ability and stat is explained in plain text on the card and is not colour-only. | P0 | Text length at 130 percent fits. | 30 |
| FR-CHAR-10 | Ranked uses the neutral character (FR-REGION-10). | P0 | See FR-REGION-10. | 0 |
| FR-CHAR-11 | Analytics: `character_viewed`, `character_bought`, `character_selected`. | P1 | Events. | 20 |

---

## 8. Save data, data files, remote config, analytics

### 8.1 Save data (SaveData version 2; migration from 1 with safe defaults)

```json
{
  "version": 2,
  "coins": 0, "gems": 0, "levels": {}, "unlockedRegions": [], "endlessBest": {},
  "inventory": {
    "powerups": {"pw_flight": 2, "pw_doublejump": 5},
    "chests": {"coin_common": 1, "powerup_rare": 0}
  },
  "characters": {
    "owned": ["char_momo", "char_pip"],
    "selected": "char_momo"
  },
  "loadout": ["pw_doublejump", null, null],
  "chestPity": {"powerup": {"rare": 1, "epic": 9}},
  "vip": {"tier": 0, "expiresUtc": null, "source": "none", "lastDailyClaim": ""},
  "earned": {"granted": ["lvl_001_05:first", "region_001:complete"]}
}
```

Rules: all fields optional on load with defaults (an old save loads; a save that still has `inventory.gear`, `shards` or `characters.equipped` from an earlier build ignores and drops those fields); counts never negative; saved at once on every change (existing `Commit()`); `earned.granted` makes earn triggers idempotent; server wallet later wins for currencies and inventory counts (doc 03).

### 8.2 Data formats (JSON, schemas under `Content/schemas`)

`powerups.json` entry: `{"id":"pw_flight","name":"Flight","trigger":"button","durationSec":4,"uses":1,"carryCap":1,"stack":"none","price":{"coins":400,"gems":20},"simParams":{"riseSpeed":8.0,"graceSec":1.0},"icon":"pw_flight"}` (coin price is always 20 x gems; Coin Doubler has no `price`).
`chests.json` entry: `{"id":"powerup_rare","kind":"powerup","tier":"rare","price":{"coins":520,"gems":26},"typeWeights":{"common":70,"uncommon":25,"rare":5},"units":[3,5],"types":2,"pityGroup":"powerup","pityWeight":1,"sources":["shop","l15_complete"]}` and `{"id":"coin_rare","kind":"coin","tier":"rare","coinsMin":1500,"coinsMax":3000,"sources":["l15_complete"]}` (no `price` on coin chests).
`characters.json` entry: `{"id":"char_pip","region":1,"rarity":"rare","name":"Pip","ability":{"id":"ab_hover","params":{"fallFactor":0.4,"maxSec":0.5}},"stats":{"gemFind":3},"price":{"coins":4000,"gems":200},"unlock":{"region":1},"art":{"placeholder":false}}`.
`vip.json`: the table of 6.1 with `tier`, `monthlyUsd`, `weeklyUsd`, `jumpPct`, `gemPct`, `coinPct`, `dailyGems`, `adFree`, `extras`.
(`gear.json` and gear rig files are removed.)

### 8.3 Remote config (all optional, defaults in the data files)

`powerups.<id>.price|durationSec|carryCap`, `chests.enabled`, `chests.odds.<id>` (versioned: `chests.version`), `chests.pity.powerup`, `chests.sources.*`, `vip.tiers`, `vip.enabled`, `bonus.caps.jumpPct|gemPct|coinPct` (cannot exceed the compiled maximum), `characters.<id>.price`, `characters.hidden`, `ranked.neutral` (true). The coin to gem rate (20) is a compiled constant, not a remote value.

### 8.4 Analytics events (in addition to doc 14 section 15)

`inventory_opened`, `loadout_changed`, `character_screen_opened`, plus the group events listed in each section. All carry `region`, `app_version`, `build_type`.

---

## 9. Screens (FR-INV)

Entry points: main menu buttons **Characters** and **Inventory**, the Shop (link "Characters"), the level popup (loadout picker), the chest earn popups and the results screen (open new chest). Back returns to the caller.

### 9.1 Screen flow

```
Main menu --Characters--> Character screen
Main menu --Inventory--> Inventory [Power-ups | Chests]
Shop (Tab 1/2) --Inventory link--> Inventory ; Shop --Characters link--> Character screen
Map > Levels > Level popup --Loadout--> Loadout picker (modal) --Start--> Run
Run end > Results --New chest--> Chest dialog > Open > Result
Any "not enough" --> Shop Tab 4 (doc 14)
```

### 9.2 Character screen (9:16 wireframe)

```
+--------------------------------------------------+
| (<)  Characters                  [coins 12,345]       |
|                                  [gems     300]       |
|  [R1] [R2] [R3] [R4] [R5]   region filter tabs        |
|  ( <)   +---------------------------+   (> )          |
|         |      large preview        |                 |
|         |       (character)         |                 |
|         +---------------------------+                 |
|   Pip  - Rare            [rarity frame + text]        |
|   Ability: Hover - hold jump while falling...         |
|   Stats: Gem find +3%                                  |
|   [ Select / Buy: 4,000 coins  or  200 gems ]          |
|   (locked state: "Unlock Sunny Beach first")           |
+--------------------------------------------------+
```
Carousel arrows are 48 dp buttons plus swipe; dots show position; owned/locked is a text label plus a lock icon, not colour. The bottom buttons are full-width in the thumb zone.

### 9.3 Inventory (9:16 wireframe)

```
+--------------------------------------------------+
| (<)  Inventory                   [coins] [gems]       |
|  [ Power-ups ] [ Chests ]                             |
|  Power-ups tab:                                       |
|   rows: icon, name, effect line, count, [Buy]         |
|   Loadout: [slot 1][slot 2][slot 3] (tap to choose)   |
|  Chests tab:                                          |
|   rows: kind+tier, count, [Open] [Odds]               |
+--------------------------------------------------+
```

Empty states: Power-ups "None owned" + Buy; Chests "No chests. Earn them by playing" + Shop link; Character carousel always has Momo.

### 9.4 Loadout picker on the level popup

The level popup (goal, stars, PLAY) gets one row of 3 slot buttons "Loadout" above PLAY. Tap a slot: modal list of owned power-ups with counts (and "Buy" when 0). PLAY stays the largest, lowest button. Rows never overlap the stars row; the popup grows in height up to the existing limits at all aspects. The last loadout is pre-selected. Skipping the picker never blocks play (CUX-7).

### 9.5 Run HUD buttons

Three round buttons (at least 48 dp, 8 dp gaps) in a column above the jump button on the thumb side, mirrored by the left-handed option; each shows icon, a count and a ring; they sit outside the move pad, jump and pause areas and never cover the next platforms (placed in the lower third on the screen edge).

### 9.6 Requirements

| ID | Requirement | Pri | Acceptance criteria | Min |
|---|---|---|---|---|
| FR-INV-1 | SaveData v2 and `InventoryService` (counts, owned characters, loadout, pity, vip, earned) with immediate save and migration; EditMode tests. | P0 | Old save loads; every mutation persisted. | 150 |
| FR-INV-2 | Main menu gets Characters and Inventory buttons without moving PLAY and keeping Settings/Shop usable (layout audit on 4 aspects, 130 percent). Menu becomes two rows if needed. | P0 | Menu audit green. | 60 |
| FR-INV-3 | Inventory screen with **Power-ups and Chests** tabs as 9.3 (48 dp tab buttons, selected state not colour-only). The Gear tab is removed. | P0 | Layout audit both tabs and empty states. | 200 |
| FR-INV-4 | Character screen as 9.2. | P0 | See FR-CHAR-5. | 0 (FR-CHAR-5) |
| FR-INV-5 | Loadout picker on the level popup as 9.4 and in the inventory; remembered. | P0 | Layout audit of the level popup with the picker. | 150 |
| FR-INV-6 | Run HUD buttons as 9.5 (see FR-PWR-3). | P0 | See FR-PWR-3. | 0 (FR-PWR-3) |
| FR-INV-7 | All new screens meet the critical UX rules: 48 dp targets, 8 dp spacing, text >= 16 sp, 4.5:1 contrast, no overlap or clipping on 9:16, 9:19.5, 9:21, 3:4, 130 percent text, colour never the only cue, reduced motion honoured, accessibility labels on every button, left-handed mirror where side-specific. | P0 | `CriticalUX` PlayMode audits added for: character screen (3 states), inventory (2 tabs, empty and full), loadout picker, chest dialogs, run HUD with buttons. | 240 |
| FR-INV-8 | Screens are async-friendly, no hitch on open: sprites for the visible items only, pooled UI rows, no per-frame allocation. | P0 | Open-time and allocation checks. | 60 |
| FR-INV-9 | Missing art never crashes (placeholder sprite, CUX-10). | P0 | Delete-a-sprite test. | 20 |
| FR-INV-10 | Navigation: Android back closes a modal first, then the screen. | P0 | Device test. | 30 |
| FR-INV-11 | Analytics `inventory_opened`, `loadout_changed`, `character_screen_opened`. | P1 | Events. | 20 |

---

## 10. Tests

| Layer | What | Tool |
|---|---|---|
| EditMode (Unity) | inventory counts and caps, atomic buy/open, pity guarantee (100,000 simulated openings), odds sum = 100, earn idempotence, character unlock and buy, VIP tier table and caps, save migration v1 to v2 (and dropping old gear fields), bonus pipeline rounding, config clamps, coin price = 20 x gems | `Assets/_Game/Tests/EditMode` |
| dotnet Simulation | each of the 11 power-ups and 20 abilities: unit tests, determinism (same inputs same hash), goal/star unchanged, row-limit of Rocket/Flight, 6.5 u clamp, caps; bot solves 75/75 with an empty loadout, and 75/75 with +25% jump; matrix `Power_<id>_<region>` and `Ability_<id>_<region>` on L1/L5/L15 and an endless tower for each of the 5 regions (about 165 + 100 scenarios) | `dotnet test Tests/Simulation` |
| Content (Python) | schemas and validator for powerups, chests, characters, vip; exactly 1 free character per region; odds sums; price rules (coins = 20 x gems, Legendary = 1.7 x Epic) | `Tools/content_cli` |
| PlayMode CriticalUX | layout audits listed in FR-INV-7 on 4 aspects x 130 percent | `CriticalUxTests.cs` |
| Device (OnePlus 13 kit) | open each new screen; switch character; buy and open a chest of each kind; pick a loadout; use each power-up in region 1 to 5 once; see frame time (no hitch when a power-up starts), left-handed HUD, TalkBack labels | `Tools/android_test` |
| Compliance | odds screens present, age gate, subscription disclosure | release checklist |

---

## 11. Tasks (Claude minutes +-40 percent; blockers; OpenArt credits estimated, none spent)

Version milestones: each group is a minor bump (`python Tools/version.py bump minor`), build number added by the build script. Current release line v0.10.x. All T-GEAR tasks are removed (2026-11-01).

| ID | Task | Min | Owner blocker | OpenArt estimate |
|---|---|---|---|---|
| T-INV-1 | SaveData v2, `InventoryService`, migration, tests (FR-INV-1) | 150 | none | 0 |
| T-INV-2 | Main menu entries (FR-INV-2) | 60 | none | 0 |
| T-INV-3 | Inventory screen: Power-ups, Chests tabs (FR-INV-3) | 200 | none | 0 |
| T-INV-4 | Loadout picker on the level popup (FR-INV-5) | 150 | decision Q-3 | 0 |
| T-INV-5 | CriticalUX audits for all new screens, device pass (FR-INV-7..10) | 330 | none | 0 |
| T-REGION-1 | RunConfig, bonus pipeline, caps, ranked neutrality (FR-REGION-1, 2, 10) | 255 | owner decision X-1 | 0 |
| T-REGION-2 | Sim feature framework: inputs, auto-triggers, replay hash (FR-REGION-3) | 60 | none | 0 |
| T-REGION-3 | Max-bonus bot proof 75/75 (FR-REGION-4) | 60 | none | 0 |
| T-REGION-4 | Per-region interaction matrix harness (FR-REGION-6, 7, 8, 9) | 540 | none | 0 |
| T-GEAR-1 to 7 | **Removed 2026-11-01 (no gear)** (was 980 min, 480 credits) | 0 | - | 0 |
| T-CHAR-1 | Character data, validator, unlock rules, 16 recolour placeholders (FR-CHAR-1, 2, 7) | 255 | roster approval (names, prices) | 0 |
| T-CHAR-2 | Character screen (FR-CHAR-5, 9) | 270 | none | 0 |
| T-CHAR-3 | 20 ability sim features and tests (FR-CHAR-3) | 600 | ability balance sign-off | 0 |
| T-CHAR-4 | Buy flow, selection, analytics (FR-CHAR-4, 6, 11) | 110 | none | 0 |
| T-CHAR-5 | Real art region 1: Pip, Clover, Petal (Momo exists): per character idle, jump, fall, portrait = 4 images, plus one reroll set = 8 images each | 120 | owner approval of each packet | 24 images = 360 credits (12 jobs) |
| T-CHAR-6 | Real art region 2 (4 characters x 8 images) | 100 | later, owner approval | 32 images = 480 credits (16 jobs) |
| T-CHAR-7 | Real art region 3 | 100 | later | 480 credits |
| T-CHAR-8 | Real art region 4 | 100 | later | 480 credits |
| T-CHAR-9 | Real art region 5 | 100 | later | 480 credits |
| T-PWR-1 | Data, stock, loadout, shop/picker links (FR-PWR-1, 2, 9, 10) | 255 | none (rate fixed at 20, X-2 resolved) | 0 |
| T-PWR-2 | HUD buttons and cues (FR-PWR-3, 7, 11) | 195 | none | 0 |
| T-PWR-3 | Flight and Rocket (FR-PWR-4) | 180 | none | 0 |
| T-PWR-4 | Double Jump, Wall Stick, Spring Boots (FR-PWR-5) | 150 | none | 0 |
| T-PWR-5 | Shield, Safety Net, Slow Time, Magnet, Star Seeker, Coin Doubler (FR-PWR-6) | 240 | none | 0 |
| T-PWR-6 | Template, analytics, region matrix runs (FR-PWR-8, 12) | 60 | none | 0 |
| T-PWR-7 | Power-up icons real art (11 images) | 30 | owner approval | 165 credits (6 jobs) |
| T-CHEST-1 | Chest data, roller, pity, tests (FR-CHEST-1, 6) | 180 | odds table sign-off | 0 |
| T-CHEST-2 | Earn rules and idempotent grants (FR-CHEST-8) | 120 | none | 0 |
| T-CHEST-3 | Shop cards, odds screen, purchase flows (FR-CHEST-2, 5, 11) | 255 | none | 0 |
| T-CHEST-4 | Opening dialog and animation, result (FR-CHEST-3, 4) | 210 | none | 0 |
| T-CHEST-5 | Server roll, anti-abuse, analytics (FR-CHEST-10, 12, 13) | 150 | server stack (doc 14 T-SHOP-10) | 0 |
| T-CHEST-6 | Chest art: 2 kinds x 4 tiers closed + open = 16 images | 60 | owner approval | 240 credits (8 jobs) |
| T-VIP-1 | Tier data, bonus wiring, daily claim, ad-free flag (FR-VIP-2, 7, 8, 9, 10) | 225 | owner decision on the table | 0 |
| T-VIP-2 | VIP screen (FR-VIP-11, 4) | 195 | disclosure wording review | 0 |
| T-VIP-3 | Subscriptions: 6 products x 2 plans, upgrade/downgrade, entitlement, restore (FR-VIP-1, 3, 5, 6, 12) | 495 | billing (doc 14 T-SHOP-9/10), Play Console | 0 |
| T-VIP-4 | VIP name badge, profile frame, victory animation art | 30 | approval | 6 images = 90 credits |

OpenArt total if everything is approved (none spent): about 2,775 credits (185 images, about 93 jobs of 2 images; was 3,435 with gear); first-version needs only the free placeholder tools. Packets always go through the approval ask first.

### 11.1 Dependency order and suggested versions

```
T-INV-1 (save v2)  -->  T-REGION-1/2 (run config, bonus pipeline, sim framework)
T-INV-1  --> T-INV-2/3 (screens)  --> T-CHAR-1/2
T-REGION-2 --> T-PWR-3..5 --> T-REGION-4 (matrix) ; T-PWR-1 --> T-INV-4
T-PWR --> T-CHEST-1..4 ; T-CHAR-3 needs T-REGION-1/2
T-VIP-1 needs T-REGION-1 ; T-VIP-3 needs billing and server (doc 14)
```

| Version | Group | Content |
|---|---|---|
| v0.11 | INV + REGION foundation | save v2, inventory screen shell, menu entries, run config, bonus pipeline |
| v0.12 | ~~GEAR~~ | **removed 2026-11-01**; number kept free so later versions do not shift |
| v0.13 | CHAR | roster data, character screen, 16 recolours, 20 abilities |
| v0.14 | PWR | 11 power-ups, loadout picker, HUD buttons, matrix tests |
| v0.15 | CHEST | chests, odds, pity, earn rules |
| v0.16 | VIP | six tiers (after billing and server exist) |

### 11.2 Priority matrix (for the owner to reorder)

| Group | Player value | Revenue | Effort (min) | Depends on | Risk | Proposed order |
|---|---|---|---|---|---|---|
| INV + REGION foundation | enables all | none | about 1,805 | none | low | 1 |
| CHARACTERS | high (variety, region reward, the only cosmetic left) | high (coins/gems sink) | about 1,750 (500 of it art handling) | INV | balance, art | 2 |
| POWER-UPS | high (help, comfort) | high (consumable sink) | about 1,110 | REGION | sim complexity, ranked fairness | 3 |
| CHESTS | medium | medium-high (odds compliance) | about 975 | PWR | legal (odds), server | 4 |
| VIP | low-medium | high (recurring) | about 945 | billing, server | store policy, economy | 5 |

Total Claude minutes (code, tests, docs; art excluded): 6,585 (about 110 hours), +-40 percent (was 7,730 with gear).

---

## 12. Open questions for the owner

| ID | Question | My default |
|---|---|---|
| Q-1 | X-1: Ranked runs neutral (no loadout, ability or VIP bonus), Casual allows all? | Yes |
| Q-2 | Legendary chest tier included? | Yes (gems only) |
| Q-3 | Loadout picker on the level popup, 3 slots, remembered per player? | Yes |
| Q-4 | VIP price table and the 6 bonus rows of section 6.1 acceptable (extras are now badges, frame and animation)? Weekly and monthly both? | As proposed |
| Q-5 | Roster names, abilities and prices of section 7.1 acceptable (Legendary 800 to 1,450 gems), or a different mix of rarities? | As proposed |
| Q-6 | ~~Duplicates as Style Shards~~ | Closed 2026-11-01 (no gear) |
| Q-7 | Total permanent jump cap +25 percent and hard clamp 6.5 u OK? | Yes |
| Q-8 | ~~Coin Doubler at 120 coins or drop it?~~ | Closed 2026-11-01: not sold, ad/chest reward only |
| Q-9 | Is a downgrade of VIP at the next renewal acceptable? | Yes |
| Q-10 | ~~Separate equipped gear per character or one shared wardrobe?~~ | Closed 2026-11-01 (no gear) |
| Q-11 | Real-art order: region 1 characters first, then chests, power-up icons, VIP items? | Yes |
| Q-12 | New: with gear gone, add another cosmetic sink later (character skins, effects)? See doc 16 section 3.3. | Decide after soft launch |
