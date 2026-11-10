# 14 - In-app store (Shop): requirements

Status: requirements written 2026-10-20. First slice built the same day: the Shop screen with Tab 4 (Gems and Coins), TEST gem purchases in development builds, coin packs paid with gems. Tabs 1 to 3 and real billing are not built.
Sources: owner brief of 2026-10-20 (section 2), doc 13 (IAP and ads implementation plan: billing stack, server validation, ads), doc 08 sections 8 and 10 to 12 (economy), doc 11 (critical UX), doc 05 (monetisation), game code (`ProgressService`, `Rewards.cs`, `Content/regions/*.json`).
**This document supersedes the pack numbers of doc 13 and doc 08 section 8 (old ladder of 6 gem packs 80 / 440 / ... gems).** Everything else in doc 13 (Play Billing first, validation, restore, ads) stays valid.
Patterns in section 3 are described generically from how successful casual and arcade games work. No game or studio is named, and none must be named anywhere in the project.

> **Owner decisions 2026-11-01 (applied in this document):** (1) **No gear anywhere**: no hats, shirts, pants, boots, accessories, gear chests, rarity frames for gear, gear upgrades or shards; Tab 1 sells **Chests** only (Power-up Chests sold; Coin Chests earn-only). (2) **1 gem = 20 coins, fixed, never changes.** There is no list/promo difference for coin packs and no bulk bonus; the launch promo (D-SHOP-5) applies to **gem packs in USD only**. Conflict C-2 is resolved by this rate. The dev slice data (`coin_packs.json` with `gemsList`/`gemsPromo`, `ProgressService.CoinPackPrice`, `ShopCatalog`, schema `coin_pack.schema.json`, their tests) must be changed by the game fork to a single `gems` per pack; this document does not change code.

Legend. Priority: **P0** = needed for the first store release, **P1** = soon after, **P2** = later live-ops. Decision tags: **[OWNER]** decided by the owner, **[PROPOSAL]** my suggestion, the owner decides, nothing changed yet, **[ASSUMPTION]** my default until told otherwise, **(verify)** depends on store policy or law that changes: check the official page when the task starts. State: **Built** = in the dev slice (v0.10), **Planned** = not built.

---

## 1. Scope and phasing

| Phase | What | Billing | Where |
|---|---|---|---|
| Now (v0.10, dev slice) | Shop shell, Tab 4 with gems and coins, tabs 1 to 3 as "Coming soon", TEST gem purchases (development builds only), coin packs for gems (real, local save; the dev slice still has the old list/promo coin prices, to be changed to the fixed 20 coins per gem) | none | `ShopScreen.cs`, `ShopCatalog.cs`, `ProgressService` |
| Release 1.0 (P0) | Real gem purchases on Google Play, server validation, restore, parental and spending safeguards, analytics, remote config, localisation of the shop | Play Billing | doc 13 phase plan |
| After 1.0 (P1) | Tab 2 (power-ups, characters), Tab 1 Chests (Power-up Chests; Coin Chests earn-only), starter pack, daily deal, limited-time offers | Play Billing + soft currency | |
| Live-ops (P2) | Chests with odds, VIP subscription, battle pass, seasonal offers, iOS | Play Billing + StoreKit | doc 13 phase 3 |

Out of scope here: ads (doc 13), leaderboards, the server design (doc 03), art production (doc 04, OpenArt credit guardrail applies to every generated icon).

## 2. Owner decisions (verbatim sources: brief of 2026-10-20 and the three answers)

| ID | Decision | Tag |
|---|---|---|
| D-SHOP-1 | The shop is part of the game. Four tabs: **1 Chests (renamed 2026-11-01, no gear), 2 Power-ups and Characters, 3 VIP and Battle Pass, 4 Gems and Coins.** | [OWNER] |
| D-SHOP-2 | Gem packs (list price, promotional offer): 10 gems $0.99 / $0.49; 20 gems $1.99 / $0.99; 50 gems $3.99 / $1.99; 100 gems $7.99 / $3.99; 200 gems $11.99 / $5.99; 500 gems $19.99 / $9.99; 1000 gems $29.99 / $14.99. | [OWNER] |
| D-SHOP-3 | Coin packs, bought with gems at the **fixed rate 1 gem = 20 coins** (owner, 2026-11-01; replaces the earlier list/promo table): 100 coins 5 gems; 200 coins 10; 500 coins 25; 1000 coins 50; 2000 coins 100; 5000 coins 250; 10000 coins 500. No list/promo split, no bulk bonus. | [OWNER] |
| D-SHOP-4 | These two tables **replace** the earlier plan (80 gems for $0.99 and so on) in `Content/economy/gem_packs.json` and in the docs. | [OWNER] |
| D-SHOP-5 | The promotional price is a **launch promo**: the player pays the promotional price, the list price is shown crossed out with a "Launch offer" badge. The promo can be switched off remotely (remote config flag); afterwards the list price applies. | [OWNER] |
| D-SHOP-6 | Deliverable now: this requirements document plus a first Shop screen for Tab 4 with TEST purchases in development builds. Real billing comes later. | [OWNER] |

## 3. Patterns from successful casual and arcade games (generic)

| Pattern | What good games do | How Fluffy Climbers: Tower Jump uses it | Tab / FR | Pri |
|---|---|---|---|---|
| Shop as a destination | One shop reachable from the menu, the map and every "not enough" prompt; tab bar with 3 to 5 tabs; the currency wallet always visible | 4 tabs (D-SHOP-1), wallet at top, entries from menu now, map and prompts later | FR-SHOP-1..8 | P0 |
| Featured and best-value badges | One or two cards carry a badge ("Popular", "Best value"); badges never cover price or amount | Badge per pack from data (`badge` field) | FR-SHOP-18 | P1 |
| Crossed-out launch price | Price anchoring: list price crossed out next to the real price, a time-limited promo tag | D-SHOP-5, remote flag, optional end time | FR-SHOP-12..13 | P0 |
| Starter pack | One-time bundle at a very low price for new players, shown after the first sessions, disappears after purchase or after a few days | Bundle of 100 gems, 3,000 coins, 2 Rare + 1 Epic Power-up Chest and an ad-free day; one SKU, once per account | FR-SHOP-67 | P1 |
| Daily deal | One free item or a discounted item that changes every 24 h, resets at a fixed UTC time | One free daily gift (small coins) plus one discounted item | FR-SHOP-68 | P1 |
| Limited-time offers | Bundles with a visible countdown, 1 to 7 days, remote scheduled | Remote scheduled offer list, countdown text, never more than 2 active | FR-SHOP-69..70 | P2 |
| Chest tiers with odds and pity | Several chest tiers (bronze, silver, gold), contents hidden until opened, odds shown before purchase, a pity counter that guarantees a rare item after N opens | 3 tiers, odds screen, pity timers, duplicate protection | FR-SHOP-28..32 | P2 |
| ~~Gear slots with rarity and upgrades~~ | Removed 2026-11-01 (no gear in the game) | - | FR-SHOP-33..38 (removed) | - |
| Consumable power-ups | Small stackable consumables, equipped in a pre-run loadout, used once | shield, magnet, extra jump, slow time; loadout of 2 before the run; Casual only, never Ranked (project rule) | FR-SHOP-39..43, 51..52 | P1 |
| Characters | Unlock by gems, coins or events, preview before buy, one equipped | Momo plus bought characters, 3D-free preview in a popup | FR-SHOP-44..48 | P1 |
| VIP subscription | Weekly or monthly auto-renew: daily gems, ad-free, coin multiplier, VIP badge/frame/animation (no gear) | VIP weekly/monthly, clear renew disclosure | FR-SHOP-53..58 | P2 |
| Battle pass | Season with free and premium tracks, tiers unlocked by XP, catch-up purchase of tiers, claim rules | 30 tiers, 60 days | FR-SHOP-59..66 | P2 |
| Currency conversion | Soft currency can be bought with hard currency at a fixed rate; the reverse is never offered | Gems to coins only (D-SHOP-3) | FR-SHOP-14..15, FR-SHOP-71 | P0 |
| Purchase confirmation | Confirm spends of hard currency above a threshold; no confirm for tiny spends | More than 50 gems asks first | FR-SHOP-20 | P0 |
| Insufficient funds routing | "Not enough gems: get more" jumps to the gem tab with the missing amount | Popup with button to the Gems section | FR-SHOP-21 | P0 |
| Trust and safety | Restore purchases, clear receipts, parental controls, spending limits, regional prices, odds disclosure | Section 12 and 13 | FR-SHOP-75..92 | P0/P1 |

## 4. Shop shell and navigation

Entry points: main menu button "Shop" (built), map top bar button (P1), every "Not enough gems" prompt (revive, region skip, shop purchases), post-run results "Offers" strip (P2). The run scene never shows a shop; during a run only the revive popup spends gems (doc 08).

Wireframe, 9:16 portrait (1080 x 1920 reference, content inside the safe area; back arrow and wallet in the top bar):

```
+--------------------------------------------------+
| (<)  Shop                         [coin icon  12,345] |
|                                   [gem  icon     300] |
|  [ Chests ] [ Power-ups and Characters ]              |
|  [ VIP and Battle Pass ] [ Gems and Coins  (sel) ]    |
|  [      Gems  (sel)      ] [      Coins         ]     |
|  +-------------------------------------------------+ |
|  | [gem] 10        [Launch offer]                  | |
|  |                                                 | |
|  | $0.99 (struck)  $0.49                           | |
|  | [            Buy (test)                       ] | |
|  +-------------------------------------------------+ |
|  | next card ...  (list scrolls)                   | |
+--------------------------------------------------+
```

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-1 | The main menu has a "Shop" button next to Settings; PLAY keeps its place. | P0 | Menu layout audit passes on 9:16, 9:19.5, 9:21, 3:4 at 100 and 130 percent text; Shop opens the shop screen. | Built |
| FR-SHOP-2 | The shop is also reachable from the world map top bar and from every insufficient-gems prompt. | P1 | Map and revive/skip popups carry a "Get gems" action that opens Tab 4, Gems section, and returns to the caller on back. | Planned |
| FR-SHOP-3 | Four tabs in the order of D-SHOP-1, drawn as a 2 x 2 grid of large buttons (long names wrap to two lines, 130 percent text must fit). The selected tab is larger and carries the accessibility label "selected". | P0 | Tab buttons at least 48 x 48 dp, 8 dp apart; selection visible without colour; layout audit green. | Built |
| FR-SHOP-4 | A tab whose content is not built shows a "Coming soon" panel with one sentence of what is coming. A remote flag per tab can hide or disable it. | P0 | Tabs 1 to 3 show "Coming soon"; the flag `shop.tabs.enabled` (section 14) hides a tab without an app update. | Built (panel), flag Planned |
| FR-SHOP-5 | The wallet (coins, gems) is visible on every tab and updates immediately after any purchase. | P0 | After a purchase the number changes within the same frame, no flicker; numbers are never clipped up to 999,999 at 130 percent text. | Built |
| FR-SHOP-6 | The back arrow returns to the menu (or to the caller of FR-SHOP-2). The Android back key does the same; an open popup closes first. | P0 | Back key behaviour covered by a device test; no state is lost. | Built (arrow) |
| FR-SHOP-7 | The shop opens on Tab 4, Gems, unless a caller asks for another tab. The last selected tab and section are remembered for the session. | P1 | Re-entering the shop shows the last selection; a call with a target tab overrides it. | Built (default) |
| FR-SHOP-8 | The shop screen is a menu screen: no per-frame allocations, lists are scrolled by a masked scroll view, popups are the shared modal popups of the meta scene. | P0 | No `Update` in shop code; the CriticalUX layout audit covers shop screens and popups. | Built |

## 5. Tab 4 - Gems and Coins

### 5.1 Gem packs (owner table D-SHOP-2) with computed value

Gems per US dollar and dollars per gem, at list price and at launch promo price. Source: `Content/economy/gem_packs.json`.

| Pack id | Gems | List | Promo | Gems per $ (list) | Gems per $ (promo) | $ per gem (list) | $ per gem (promo) | Discount | Badge |
|---|---|---|---|---|---|---|---|---|---|
| gempack_pinch | 10 | $0.99 | $0.49 | 10.10 | 20.41 | 0.0990 | 0.0490 | 50.5% | - |
| gempack_handful | 20 | $1.99 | $0.99 | 10.05 | 20.20 | 0.0995 | 0.0495 | 50.3% | - |
| gempack_pouch | 50 | $3.99 | $1.99 | 12.53 | 25.13 | 0.0798 | 0.0398 | 50.1% | - |
| gempack_sack | 100 | $7.99 | $3.99 | 12.52 | 25.06 | 0.0799 | 0.0399 | 50.1% | popular [PROPOSAL] |
| gempack_chest | 200 | $11.99 | $5.99 | 16.68 | 33.39 | 0.0600 | 0.0300 | 50.0% | - |
| gempack_vault | 500 | $19.99 | $9.99 | 25.01 | 50.05 | 0.0400 | 0.0200 | 50.0% | - |
| gempack_treasury | 1000 | $29.99 | $14.99 | 33.34 | 66.71 | 0.0300 | 0.0150 | 50.0% | best_value [PROPOSAL] |

Reading: the promo is almost exactly half price on every pack. The price ladder is steep (10 to 33 gems per $ at list, 20 to 67 at promo): the largest pack gives 3.3 times the gems per dollar of the smallest. Two small inversions exist in the owner's numbers (the 20-gem pack gives 0.5 percent (list) to 1 percent (promo) fewer gems per $ than the 10-gem pack, and the 100-gem pack 0.1 percent (list) to 0.3 percent (promo) fewer than the 50-gem pack). Invisible to players, but there is no volume bonus between 50 and 100 gems (see conflict C-4).

### 5.2 Coin packs (owner table D-SHOP-3, fixed rate 1 gem = 20 coins) with computed value

Source: `Content/economy/coin_packs.json` (single `gems` per pack since 2026-11-01). **The rate is fixed at 20 coins per gem for every pack, always: no list/promo split, no bulk bonus** (the earlier table had 10 coins per gem at list and 20 at promo, and 25 for the 5000 pack; all dropped). The dollar columns use the gem prices of section 5.1: best case = buying the gems in the 1000-gem pack at promo ($0.015 per gem) or list ($0.030), worst case = the 10-gem pack at promo ($0.049) or list ($0.099).

| Pack id | Coins | Gems | Coins per gem | Real cost, promo best | Real cost, promo worst | Real cost, list best | Real cost, list worst |
|---|---|---|---|---|---|---|---|
| coinpack_100 | 100 | 5 | 20 | $0.08 | $0.25 | $0.15 | $0.50 |
| coinpack_200 | 200 | 10 | 20 | $0.15 | $0.49 | $0.30 | $0.99 |
| coinpack_500 | 500 | 25 | 20 | $0.38 | $1.23 | $0.75 | $2.48 |
| coinpack_1000 | 1,000 | 50 | 20 | $0.75 | $2.45 | $1.50 | $4.95 |
| coinpack_2000 | 2,000 | 100 | 20 | $1.50 | $4.90 | $3.00 | $9.90 |
| coinpack_5000 | 5,000 | 250 | 20 | $3.75 | $12.25 | $7.50 | $24.75 |
| coinpack_10000 | 10,000 | 500 | 20 | $7.50 | $24.50 | $15.00 | $49.50 |

Reading: every coin pack is exactly 20 coins per gem, so a coin costs 0.05 gem. Coins per US dollar when bought through gems: about 1,333 (1000-gem pack at promo) down to 202 (10-gem pack at list). Large coin packs have no discount, which keeps the gem price of every coin item simple (coin price = 20 x gems).

### 5.3 Requirements

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-9 | Tab 4 has two sections, **Gems** and **Coins**, chosen by two large buttons under the tab bar. Gems is the default. | P0 | Both sections reachable with one tap; selection visible without colour (larger button + accessibility label). | Built |
| FR-SHOP-10 | A pack card shows: icon (gem or coin), the amount (thousands separator), optional badge(s), the list price crossed out and the price to pay, and a full-width Buy button in the lower half of the card. | P0 | Nothing overlaps or leaves the card at 9:16, 9:19.5, 9:21, 3:4 and 130 percent text; Buy at least 48 dp high; text at least 16 sp; contrast at least 4.5:1 (card backing is dark, text light). | Built |
| FR-SHOP-11 | The pack list is data driven from `gem_packs.json` and `coin_packs.json` (section 14), in file order, in a masked scroll view. | P0 | Adding an 8th pack to the JSON shows it without code changes; ordering follows the file; a stale manifest without the new format falls back to the built-in tables. | Built |
| FR-SHOP-12 | **Launch promo display [OWNER], gem packs only.** While the promo is on, the card shows the list price struck through, the promotional price in a highlighted colour, and the badge "Launch offer". The player pays the promotional price. The strike-through is a drawn line (shape), not only colour. | P0 | Promo on: three cues visible (strike line, "Launch offer" text, green price). A card never shows a promotional price above its list price. | Built |
| FR-SHOP-13 | **Promo off switch [OWNER].** A remote config flag turns the promo off: cards show only the list price, no strike-through, no badge, and the list price is charged. The flag is read when the shop opens and again on app resume; an open shop keeps the state it opened with. An optional end timestamp turns the promo off by itself. | P0 | Flag off: list price shown and charged (the matching store SKU for gem packs later, see 5.5). The flag never affects coin packs (fixed 20 coins per gem). Local default on; `GameSettings.LaunchPromo` stands in for the remote value until config is wired. | Built (local flag), remote Planned |
| FR-SHOP-14 | Coin packs are paid with gems at the **fixed rate of 20 coins per gem** (D-SHOP-3, 2026-11-01). The price shown is the price charged; it does not depend on the launch promo. | P0 | `ProgressService.CoinPackPrice` returns `coins / 20` for every pack and ignores `LaunchPromoActive`; EditMode tests updated (game fork: the current dev slice still has the old list/promo prices and must be changed). | Built (old prices), change Planned |
| FR-SHOP-15 | A coin purchase debits gems and credits coins in one step and saves once (atomic): no state where only one half happened, also after an app kill. | P0 | Test: second `ProgressService` on the same store sees both changes; failed purchase changes nothing; coin total saturates at int max. | Built |
| FR-SHOP-16 | **Development builds: gem packs are TEST purchases [OWNER].** Confirm popup titled "Confirm test buy" with the text "TEST PURCHASE ... No money is charged", then the gems are granted at once and the result popup says "This was a test purchase". The Buy button label is "Buy (test)". | P0 | Only when `GameSettings.DevBuild`; wallet increases and is saved immediately; test purchases never reach analytics as revenue (flag `test=true`). | Built |
| FR-SHOP-17 | **Release builds without billing: the gem Buy button reads "Not available yet"** and opens a popup that says so. No gems are granted, no billing call is made. | P0 | Release build: no grant path exists for gems except billing; covered by a unit test that the grant method is only called from the dev branch. | Built |
| FR-SHOP-18 | Badges ("Popular", "Best value") come from the pack data; at most one special badge plus "Launch offer" per card; coin packs carry no badge (fixed rate, no best-value pack). | P1 | Badge text is a real word on a light pill (contrast above 10:1); never covers price, amount or Buy. | Built |
| FR-SHOP-19 | With real billing every shown gem price is the **store-localised price string** (currency, tax included as the store reports it). The USD numbers in JSON are the fallback and the design reference only. | P0 | Price text comes from the store query (`ProductDetails`); if it is unavailable the card shows "Price unavailable" and Buy is disabled, never a guessed price. | Planned |

### 5.4 Economy cross-check (what the owner's tables imply)

Facts used: gems are earned by play at about 10.7 per level placed (median 10; 6.3 to 9 per level realised at 60 percent pickup), about 80 to 100 per hour (`Tests/Simulation/ECONOMY_REPORT.md`). A 3-star level pays 150 coins (region 1) to 168 (region 5) plus combo, 2,250 to 2,520 coins per region. Region skip price = `unlock.gemSkip` = 100 + (n - 2) x 300 / 18 gems: 100, 116, 133, 150, 166 for regions 2 to 6, 400 for region 20, 566 for region 30. Revive costs 2 gems then 5 (max 2 per run). **Fixed rate (owner, 2026-11-01): 1 gem = 20 coins**; gem price $0.015 to $0.099 by pack (reference $0.03), so $1 buys about 10 to 67 gems. The old anchor (100 coins per gem, $0.011 per gem, $1 = 90 gems) is retired.

| Item | Cost in gems | Real cost, promo best ($0.015/gem) | Real cost, promo worst ($0.049/gem) | Real cost, list best ($0.030/gem) | Real cost, list worst ($0.099/gem) | Old plan (doc 08) |
|---|---|---|---|---|---|---|
| Revive 1 | 2 | $0.03 | $0.10 | $0.06 | $0.20 | $0.02 |
| Revive 2 | 5 | $0.08 | $0.25 | $0.15 | $0.50 | $0.06 |
| Skip to region 2 | 100 | $1.50 | $4.90 | $3.00 | $9.90 | $1.10 |
| Skip to region 5 | 150 | $2.25 | $7.35 | $4.50 | $14.85 | $1.65 |
| Skip to region 20 | 400 | $6.00 | $19.60 | $12.00 | $39.60 | $4.40 |
| Skip to region 30 | 566 | $8.49 | $27.73 | $16.98 | $56.03 | - |
| Skip regions 2 to 5 in a row | 499 | $7.49 | $24.45 | $14.97 | $49.40 | $5.50 |
| One coin (0.05 gem, fixed rate) | 0.05 | $0.00075 | $0.00245 | $0.0015 | $0.00495 | $0.00011 |
| One 3-star level in coins (150 coins) | 7.5 | $0.11 | $0.37 | $0.23 | $0.74 | $0.017 |
| Flight power-up (400 coins = 20 gems) | 20 | $0.30 | $0.98 | $0.60 | $1.98 | - |
| Legendary character R1 / R5 (800 / 1,450 gems) | 800 / 1,450 | $12.00 / $21.75 | $39.20 / $71.05 | $24.00 / $43.50 | $79.20 / $143.55 | $16.50 (1,500 gems) |

What this means in plain words:
1. **A region skip of 100 to 400 gems costs about $1.50 to $6 at the best promo price, and $3 to $12 at list (best pack).** It costs up to $4.90 to $19.60 for a player who only buys the small packs. The best promo price is close to the old plan ($1.10 to $4.40), so skips stay cheap; at list they are about 2.7 times dearer.
2. **The rate between the currencies is fixed: 20 coins = 1 gem, for every purchase, every pack, promo or not.** The old economy valued a gem at 100 coins (`CLAUDE.md`, doc 08 section 8 before 2026-11-01); a gem is now worth 5 times fewer coins, so every coin price from doc 08/15 written at 100:1 must be divided by 5, or the gem price multiplied by 5 (section 5.5, C-3).
3. **In dollars, a gem is $0.015 to $0.099** instead of $0.011: 1.4 to 9 times dearer. Any gem price in doc 08 (legendary character, chest, VIP daily gems) now means more real money; the repriced Legendary characters (800, 950, 1,100, 1,250, 1,450 gems) cost $12 to $21.75 at the best promo rate and $24 to $43.50 at list best.
4. **Gems stay hard to buy relative to play:** a pack of 100 gems at promo ($3.99) equals about one hour of play. The largest pack (1000 gems, $14.99 promo) equals about 10 hours of play and 100 levels of gem income, and covers every region skip from region 2 to 6 (665 gems) with change. The small packs (10, 20, 50 gems) are worth 5 to 30 minutes of play and are unlikely to sell except as an impulse top-up.
5. **Coins have no sink yet.** Until tabs 1 and 2 exist nothing can be bought with coins (power-up and character coin prices are the plan; there is no gear). The coin section works, but a player can spend gems on coins that buy nothing (see conflict C-1).

### 5.5 Conflicts with the existing economy and proposals (C-2 resolved by the owner on 2026-11-01; C-3 updated to the fixed rate; others open)

| ID | Conflict | Proposal [PROPOSAL] | Effect |
|---|---|---|---|
| C-1 | Coin packs are live (D-SHOP-3) but the game has no coin sink yet; in a release build a player could spend gems on coins that buy nothing. | Hide the Coins section behind remote flag `shop.coins.enabled` (default off until tab 1 or 2 ships) in release builds; keep it on in development builds. | Protects players and reviews; one flag, no layout change. |
| C-2 | **RESOLVED 2026-11-01 (owner): the rate is fixed at 1 gem = 20 coins, never changed.** The old anchor "1 gem = 100 coins, $0.011 per gem" (doc 08 section 8, `CLAUDE.md`, `remote_config.json` `coinsPerGem`) is retired. | Set `coinsPerGem` to 20 in `remote_config.json`, update `CLAUDE.md` and `Rewards.cs` comments, doc 08 section 8 is already updated; coin pack data uses one `gems` value per pack (game fork). | One source of truth: coin price = 20 x gem price. No code reads `coinsPerGem` for a conversion today. |
| C-3 | Item, chest, skip and VIP prices in doc 08 were set at $0.011 per gem and 100 coins per gem. With the fixed rate of 20 coins per gem and $0.015 to $0.099 per gem, coin prices written at 100:1 are 5 times too high and every gem price costs 1.4 to 9 times more real money. | **Item gem prices stay as in docs 08 and 15; every coin price = 20 x the gem price** (power-ups: Flight 20 gems / 400 coins, Rocket 16 / 320, 8-gem items 160 coins, 10-gem items 200, Safety Net 14 / 280). Chests: Power-up Common/Rare/Epic/Legendary 14/26/45/85 gems; Coin Chests are earn-only. Legendary characters 800 / 950 / 1,100 / 1,250 / 1,450 gems. | Coin and gem routes cost the same value; no arbitrage. |
| C-4 | No volume bonus between 50 and 100 gems (12.53 vs 12.52 gems per $ at list); 10 and 20 gems are also level. | Accept as is (difference is 0.1 to 1 percent), or give the 100-gem pack +10 bonus gems ("110"). Needs the owner's call because the table is the owner's. | Cleaner ladder, optional. |
| C-5 | VIP (doc 08): 25 gems a day = 750 gems a month for $7.99. At the new prices those gems are worth $11.25 (promo best) to $22.50 (list best); VIP is cheaper per gem (94 gems per $) than the best pack (67 per $ at promo), so it undercuts every pack. | Lower VIP to 15 gems a day (450 a month, 56 gems per $, worth $6.75 to $13.50) and sell the remaining value as ad-free, coin multiplier and exclusive cosmetic. Re-test after soft launch. | Keeps VIP attractive without undercutting packs. |
| C-6 | Free gem inflow (about 10 per level, 160 to 220 per region, economy report concerns 1 to 3) makes the small packs nearly worthless and lets a normal player afford the next region skip by play. | Do not change inflow now. After the first soft-launch data, decide between lowering gem density (revenue up, friction up) and keeping it generous (retention up). | Open economy decision (doc 09 section F). |
| C-7 | A "list price" that was never charged can count as a misleading reference price in some jurisdictions (rules on reference prices and "lowest price in the last 30 days") (verify). | Call it "Launch offer", give the promo an end date in remote config, keep the list price low enough to be a price you would really charge, get a legal read before launch. | Avoids a compliance blocker. |
| C-8 | A store product has one price. Switching the promo off remotely cannot change what the store charges for an existing SKU. | Two products per gem pack: `fluffyclimbers.gems.<name>` (list) and `fluffyclimbers.gems.<name>.launch` (promo). The flag chooses which product the Buy button starts; the displayed price always comes from the product that will be charged. | Display and charge can never disagree. The data file keeps one `sku` (the list one); the promo SKU is derived by the suffix `.launch`. |
| C-9 | Region skip and revive are sold to the same player who can buy 1000 gems for $14.99: all five skips (499 gems) cost $7.49 at best. | Optional: price skips on the real-money rate by capping purchases (for example one skip per 24 h) or leave. | Pacing of the 75 levels is protected. |

## 6. Purchase flows

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-20 | **Confirmation threshold.** A spend of more than 50 gems asks for confirmation ("Buy 2,000 coins for 100 gems? You have 250 gems."); 50 gems or less does not. The threshold is a remote config value (`shop.confirmGemThreshold`, default 50). | P0 | 50-gem spend runs without a confirm popup, 51-gem spend shows it; Cancel changes nothing. | Built |
| FR-SHOP-21 | **Insufficient funds.** If the player has fewer gems than the price, show "Not enough gems: You need N gems and have M" with two buttons, "Get more gems" (opens Tab 4, Gems) and "Close". Nothing is spent. | P0 | Popup text shows both numbers; button routes to Gems section; wallet unchanged. | Built |
| FR-SHOP-22 | Every successful purchase ends with a result popup naming what was added (+N gems / +N coins), and the wallet shows the new value behind it. | P0 | Unmistakable success state (CUX-8); OK closes it. | Built |
| FR-SHOP-23 | Real-money purchases use only the store's own purchase sheet; the game never collects card data. The in-game confirmation for real money is the store sheet. | P0 | No payment fields in the game UI (code review). | Planned |
| FR-SHOP-24 | Double-tap protection: while a purchase is in progress the Buy buttons of the shop are disabled and a "Processing" indicator shows. | P0 | Two taps within 500 ms create one transaction. | Planned |
| FR-SHOP-25 | **Pending** purchases (slow payment methods) show "Payment pending"; gems arrive when the store reports completion, also after an app restart. | P0 | Pending state survives restart; no grant before the store says purchased. | Planned |
| FR-SHOP-26 | Wallet operations are atomic and saved immediately (existing `Commit()` path). With the server wallet (doc 03) the server balance wins; the client shows a pending change until the server confirms. | P0 | Kill the app between grant and save: no double grant and no lost grant after restart. | Built (local), server Planned |
| FR-SHOP-27 | Soft currency purchases (coins for gems) are final: no in-game refund. The confirm popup states the price. Store purchases follow store refund rules (FR-SHOP-86). | P1 | Policy text in Settings > Purchases. | Planned |

State machine of a real-money gem purchase (planned; the dev slice implements only Idle > Confirm > Granted for TEST purchases):

```
Idle -> ProductsLoaded (prices from the store)
ProductsLoaded -> ConfirmShown (test builds only; real builds go straight to StoreSheet)
ConfirmShown --cancel--> Idle
ProductsLoaded --tap Buy--> StoreSheet (store UI, buttons disabled)
StoreSheet --user cancels--> Idle           [event: shop_purchase_cancelled]
StoreSheet --error--> Failed (message, retry) [event: shop_purchase_failed]
StoreSheet --payment pending--> Pending      (survives restart)
StoreSheet / Pending --purchased--> Validating (receipt token sent to the server)
Validating --server ok--> Granting (server credits gems; client shows pending +N)
Validating --rejected--> Rejected (no grant, support text)  [event: shop_purchase_rejected]
Validating --no network--> Queued (kept on device, retried at next start)
Granting --balance confirmed--> Consuming (consume/acknowledge with the store)
Consuming --ok--> Done (result popup, event shop_purchase_completed)
Done --refund notice later--> Revoked (server removes gems if unspent, else flags the account; FR-SHOP-86)
Coin purchase (soft): Idle -> [Confirm if price > threshold] -> AtomicDebitCredit -> Done | InsufficientFunds -> Routed to Gems
```

## 7. Tab 1 - Chests

> **2026-11-01 update (owner): there is no gear in the game.** Tab 1 sells **Chests** only. Chest kinds: **Power-up Chest** (sold for gems or, at Common and Rare, coins) and **Coin Chest** (earn-only, never sold). Gear slots, gear rarity frames, gear upgrades, Style Shards and duplicate protection are removed (FR-SHOP-33 to 38 marked removed). Chest details live in [15-power-ups-characters-gear-requirements.md](15-power-ups-characters-gear-requirements.md) (FR-CHEST); the economy ladder is in [16-economy-comparison.md](16-economy-comparison.md). This document keeps the shop shell, Tab 4, billing and compliance.

Numbers in sections 7 to 10 (counts, durations, prices not given by the owner) are [PROPOSAL] defaults for the owner to confirm; the structure is the requirement. **Coin price = 20 x gem price** wherever an item is sold in both currencies (fixed rate, C-2).

Wireframe (9:16): sub-tabs row `[Power-up Chests] [My Coin Chests]`, below a scroll list. Chest card: chest art, tier name, price (gems; coins for Common and Rare), "Odds" link, pity counter ("Rare guaranteed in 7 opens"). Coin Chests are listed as owned stock with an "Open" button, never with a price.

Power-up Chest prices [PROPOSAL, 70 percent of expected value]: Common 14 gems / 280 coins; Rare 26 gems / 520 coins; Epic 45 gems; Legendary 85 gems.

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-28 | **Two chest kinds:** Power-up Chest (sold, tiers Common, Rare, Epic, Legendary; prices above) and Coin Chest (earn-only, not in the shop). Each opens with a short animation (skippable, none under reduced motion). | P2 | Opening a chest pays exactly one result; the result is decided on the server; animation can be skipped with one tap; the shop lists no Coin Chest price. | Planned |
| FR-SHOP-29 | **Odds disclosure.** Every chest shows its drop odds (percent per rarity, and per item pool) before purchase, one tap from the chest card, and in the chest result screen. | P0 for any chest | Odds screen lists all rarities summing to 100 percent; text matches the live server table; reachable without buying. | Planned |
| FR-SHOP-30 | **Pity timer:** a guaranteed rare-or-better after N opens without one (default 10), epic-or-better after M (default 30). The counter is shown on the card. | P2 | Server tests: never more than N-1 opens without a rare; counter persists across devices. | Planned |
| FR-SHOP-31 | ~~Duplicate protection~~ removed 2026-11-01 (no gear): power-ups stack, there are no duplicates to convert. | - | - | Removed |
| FR-SHOP-32 | Chest odds and pity values are remote config (`shop.chests.*`) and versioned; the version is stored with every opened chest. | P2 | Changing odds never changes past results. | Planned |
| FR-SHOP-33 | ~~Gear slots~~ removed 2026-11-01 (no gear in the game). | - | - | Removed |
| FR-SHOP-34 | ~~Gear rarity frames~~ removed 2026-11-01 (no gear). Chest and character rarity is still shown by frame shape plus a text label, never by colour only (CUX-6). | - | Colour-blind check on chest and character cards. | Removed (rule kept for chests and characters) |
| FR-SHOP-35 | ~~Gear upgrade levels~~ removed 2026-11-01 (no gear). | - | - | Removed |
| FR-SHOP-36 | ~~Gear try-on preview~~ removed 2026-11-01 (no gear). Character preview stays (FR-SHOP-45). | - | - | Removed |
| FR-SHOP-37 | ~~Gear prices and Owned/Equip state~~ removed 2026-11-01 (no gear). Owned characters show "Owned"/"Equip", never Buy (FR-SHOP-44). | - | - | Removed |
| FR-SHOP-38 | ~~Event gear with countdown~~ removed 2026-11-01 (no gear). Event characters keep their countdown (FR-SHOP-50). | - | - | Removed |

## 8. Tab 2 - Power-ups and Characters

Wireframe: sub-tabs `[Power-ups] [Characters]`. Power-up card: icon, name, one-line effect, owned count, price (coins or gems), `-` count `+` stepper or fixed bundles (1, 5, 10). Character card: portrait, name, rarity, unlock source (gems, coins, event), "Preview", "Buy" or "Equip".

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-39 | Four consumable power-ups: **shield** (absorbs one hazard hit; the bottom edge still kills), **magnet** (pulls pickups for 10 s), **extra jump** (one mid-air jump), **slow time** (scroll speed -30 percent for 8 s). Each is bought in bundles (1, 5, 10) with coins or gems. | P1 | Prices from config; effects are deterministic sim features and covered by bot/sim tests before shipping. | Planned |
| FR-SHOP-40 | Per-run loadout: before a Casual run the player picks up to 2 power-ups from the owned stack; they are consumed when used (or at run start, to be decided: [ASSUMPTION] consumed on use). | P1 | Loadout screen at most 1 extra tap; unused ones are returned. | Planned |
| FR-SHOP-41 | Power-ups are disabled in Ranked runs and on global boards (project rule: Ranked disallows consumables). The shop says so. | P0 for power-ups | Ranked run shows no loadout; server rejects ranked scores with power-up flags. | Planned |
| FR-SHOP-42 | Stack caps (for example 99 per type) and a clear "Owned: N" label. | P1 | Buy button disabled with "Full" at the cap. | Planned |
| FR-SHOP-43 | Power-up bundles can be offered in the post-run strip ("Out of shields? Buy 5"), at most once per session. | P2 | Strip is dismissible and never covers the retry button (CUX-7). | Planned |
| FR-SHOP-44 | Characters: Momo is owned from the start; others unlock by gems (rare and epic by coins or gems at 20 coins per gem; legendary 800, 950, 1,100, 1,250 and 1,450 gems for regions 1 to 5, doc 15), coins, or events/season rewards. | P1 | Character data in `items.json` with `slot: character`; unlock source shown on the card. | Planned |
| FR-SHOP-45 | Character preview: animated idle/jump on a plain background inside a popup; static pose under reduced motion. | P1 | Preview never allocates per frame beyond the animator; closes with the back key. | Planned |
| FR-SHOP-46 | Characters are cosmetic (identical physics) so Ranked boards stay fair. | P0 for characters | Sim ignores the character id; test. | Planned |
| FR-SHOP-47 | Locked characters explain how to unlock them (CUX-8). | P1 | Text like "Win the Dragon Peak event" or "700 gems". | Planned |
| FR-SHOP-48 | One equipped character; switching is free and saved at once. | P1 | Equip persists; run uses it. | Planned |
| FR-SHOP-49 | Bundles that mix power-ups and a character (for example a starter bundle) are allowed only as offers (section 10), priced in real money. | P2 | Offer rules apply. | Planned |
| FR-SHOP-50 | Out-of-stock handling for event characters: "Event ended" and no Buy. | P2 | Date test. | Planned |
| FR-SHOP-51 | Inventory screen (what do I own) reachable from the shop and the run loadout. | P1 | Lists power-ups with counts, chests and owned characters. | Planned |
| FR-SHOP-52 | Every inventory change is saved at once and mirrored to the server wallet when it exists. | P1 | Same atomic rules as FR-SHOP-15. | Planned |

## 9. Tab 3 - VIP and Battle Pass

Wireframe: two big cards. VIP card: crown art, perks list (daily gems, no ads, coin bonus, VIP badge), price buttons `[Weekly $2.99] [Monthly $7.99]`, a line of disclosure text. Battle pass card: season name, countdown, track preview, `[Unlock premium $4.99]`.

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-53 | VIP is an auto-renewing subscription with weekly and monthly price tiers (doc 08: $2.99 week, $7.99 month; final prices open). | P2 | Both products exist in the store; the screen shows the store price. | Planned |
| FR-SHOP-54 | VIP perks: daily gems (doc 08 25 a day, proposal C-5: 15), no interstitial/rewarded-ad prompts except opt-in bonuses, coin multiplier x1.2 on level payouts, and non-gear extras by tier (VIP 1 crown name badge, VIP 5 profile frame, VIP 6 victory animation + name badge; see doc 15 section 6). | P2 | Perks apply while the entitlement is active and stop when it lapses; the daily gems are claimed once per UTC day. | Planned |
| FR-SHOP-55 | **Auto-renew disclosure** right on the purchase card: price, period, "renews automatically until cancelled", how to cancel (store subscription settings), links to terms and privacy. | P0 for subscriptions | Text visible without scrolling at 9:16 and 130 percent text; wording reviewed for each store (verify). | Planned |
| FR-SHOP-56 | Entitlement is server-side: the client asks the server and the store for the active state at start and on resume; offline the last known state is used for up to 3 days (grace). | P2 | Airplane-mode test; expiry honoured after grace. | Planned |
| FR-SHOP-57 | Subscription states handled: active, in grace period, on hold, paused, cancelled-but-active, expired, refunded. The screen shows the current state in plain words. | P2 | State table test with store test accounts. | Planned |
| FR-SHOP-58 | Manage/Cancel link opens the store's subscription page. | P0 for subscriptions | Link works on the device. | Planned |
| FR-SHOP-59 | Battle pass season: 30 tiers, 60 days (proposal), one free track and one premium track; both tracks are visible, premium rewards are visibly locked. | P2 | Track view scrolls horizontally with a text label on each locked reward (not colour only). | Planned |
| FR-SHOP-60 | Season XP sources: finishing levels (10 XP, 3-star bonus 5), endless floors (1 XP per 10 floors, daily cap), daily and weekly missions. Tier needs 100 XP, flat. | P2 | XP table in remote config; server awards XP from validated runs. | Planned |
| FR-SHOP-61 | Premium unlock: one purchase ($4.99 or $9.99 with 10 tiers skipped, open question Q6) unlocks all premium rewards retroactively up to the current tier. | P2 | Buying at tier 12 grants premium rewards of tiers 1 to 12 at once. | Planned |
| FR-SHOP-62 | Catch-up purchase of tiers with gems (price per tier, capped at the end of the season); never sells a tier the player could not otherwise reach. | P2 | Gem price per tier in config; shows the total before buy. | Planned |
| FR-SHOP-63 | Claim rules: rewards are claimed manually or with "Claim all"; unclaimed rewards stay claimable for 7 days after the season ends, then convert to coins. | P2 | Date tests; no reward is lost silently. | Planned |
| FR-SHOP-64 | Season rollover: a new season starts at a fixed UTC time; XP does not carry over; exclusive rewards of ended seasons are never sold again for the same price [ASSUMPTION]. | P2 | Rollover test with a fake clock. | Planned |
| FR-SHOP-65 | Season reward items are cosmetic or soft currency only. | P2 | No power-up or stat reward. | Planned |
| FR-SHOP-66 | Pass and VIP purchases show a receipt in Settings > Purchases (name, date, store price, order id). | P1 | List loads offline from the local cache. | Planned |

## 10. Offers (starter pack, daily deal, limited-time offers)

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-67 | **Starter pack:** one-time real-money bundle (100 gems, 3,000 coins, 2 Rare Power-up Chests, 1 Epic Power-up Chest and 1 day ad-free) shown after the player finishes region 1 or after 3 sessions, whichever first; disappears when bought or after 7 days. | P1 | Store SKU `fluffyclimbers.starter`; one purchase per account (server check); shown at most once per session; closes with one tap. | Planned |
| FR-SHOP-68 | **Daily deal:** one free daily gift (coins) and one discounted item that changes at 00:00 UTC. | P1 | Claim button; claimed state persists; clock manipulation does not give a second claim (server time). | Planned |
| FR-SHOP-69 | **Limited-time offers:** remote scheduled bundles with a countdown ("2d 4h"); never more than 2 active; ends are enforced by the server. | P2 | Countdown text updates once per second only while visible (no per-frame string building: change-only updates); no flashing under reduced motion. | Planned |
| FR-SHOP-70 | Offers never block retry or the results screen (CUX-7) and are never shown during a run. | P0 | Layout audit on results screen with an offer strip. | Planned |

## 11. Currency rules

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-71 | Only gems buy coins (D-SHOP-3). Coins can never be converted into gems. | P0 | No code path credits gems from coins (unit test on `ProgressService`). | Built |
| FR-SHOP-72 | **Rate: 1 gem = 20 coins, fixed (owner, 2026-11-01)**, for every coin pack and every item; no list/promo variant, no bulk bonus. The in-game valuation text everywhere uses 20:1, not the old 100:1 rate (conflict C-2, resolved). | P0 | Doc 08 updated; `CLAUDE.md`, `Rewards.cs` and `remote_config.json` (`coinsPerGem` = 20) updated by the game fork. | Doc done, code Planned |
| FR-SHOP-73 | Gems and coins are integers, never negative, saturate at int max, and display with a thousands separator. | P0 | Existing wallet tests plus shop tests. | Built |
| FR-SHOP-74 | Free and bought gems are one balance (no expiry) [ASSUMPTION]; analytics distinguish the source. | P0 | Event params `source`. | Planned |

## 12. Compliance, safety and regional rules

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-75 | **Store policy:** digital goods use the store billing system only (Google Play Billing; StoreKit on iOS); no external payment links in the app (verify current rules per region). | P0 | Policy checklist signed before submission. | Planned |
| FR-SHOP-76 | **Loot box odds disclosure:** any random paid item (chests) shows its odds before purchase in all regions (both stores require it) (verify). | P0 for chests | See FR-SHOP-29. | Planned |
| FR-SHOP-77 | **Parental controls:** a "Purchases" settings section with an optional 4-digit PIN that guards every real-money and every gem spend above the threshold; off by default; the PIN is never sent to analytics. | P1 | PIN prompt before the store sheet; wrong PIN 5 times locks for 15 minutes. | Planned |
| FR-SHOP-78 | **Spending limits:** optional daily/monthly caps in the player's own settings; reaching a cap shows "Limit reached" and links to the setting. | P1 | Cap is checked before the store sheet; resets at UTC day/month. | Planned |
| FR-SHOP-79 | **Age gating:** an age screen at first start (neutral age question). Under-13 (and under local digital-consent age) players see no real-money purchases, no personalised ads, and no chests; the store family settings are honoured (verify). | P0 | Under-age profile: Buy buttons for money hidden; test with a fake age. | Planned |
| FR-SHOP-80 | **Regional pricing:** prices come from the store per region; the USD table is the base. Price points per region are set in the store console from the base. | P0 | No hard-coded currency symbol in code; formatted by store string. | Planned |
| FR-SHOP-81 | **Tax display:** prices shown are what the store charges (tax inclusive or exclusive as the store reports); the game adds no tax text of its own. | P0 | Screens show the store string only. | Planned |
| FR-SHOP-82 | **Minimum price points:** the $0.49 launch price of the 10-gem pack must be a valid price point in the store console in the launch regions (verify; fall back to $0.99 if not). | P0 | Console check before T-SHOP-9. | Planned |
| FR-SHOP-83 | Legal texts: terms of sale, privacy policy and refund policy reachable from Settings > Purchases and from the subscription card. | P0 | Links open in the system browser. | Planned |
| FR-SHOP-84 | Cooling-off for accidental purchases: a gem spend above the threshold needs the confirm popup (FR-SHOP-20); no one-tap purchase of real-money items with a stored payment method inside the game. | P0 | The store sheet always shows. | Planned |

## 13. Receipts, restore, refunds, anti-fraud

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-85 | **Receipt validation on the server** before gems are granted (doc 13 section 3); the client never decides a grant. | P0 | Forged/replayed token test is rejected; same token credits once. | Planned |
| FR-SHOP-86 | **Refunds and revokes:** a refund/chargeback notice from the store removes the matching gems if still owned, otherwise records a negative balance limit and flags the account; repeated abuse blocks purchases. | P1 | Notice-handling test with store test tools. | Planned |
| FR-SHOP-87 | **Restore purchases:** non-consumables and subscriptions are re-queried at start; Settings has a "Restore purchases" row (required on iOS). | P0 | Reinstall test restores ad-free/VIP; gems (consumable) are restored from the server wallet. | Planned |
| FR-SHOP-88 | **Unfinished purchase recovery:** a purchase completed in the store but not yet credited is credited at next start. | P0 | Kill-app test between payment and grant. | Planned |
| FR-SHOP-89 | **Anti-fraud:** server rate limits per account/device on grant requests, receipt de-duplication, root/modified-client signals (best effort), and anomaly alerts (many purchases per hour). | P1 | Alert rules documented and tested with synthetic traffic. | Planned |
| FR-SHOP-90 | Test purchases are impossible in release builds (compile-time/DevBuild guard) and the server rejects test receipts in production. | P0 | Release build contains no path that credits gems without a validated receipt. | Built (guard), server Planned |
| FR-SHOP-91 | Purchase history (local cache + server) in Settings > Purchases with date, item, store price, order id. | P1 | Available offline. | Planned |
| FR-SHOP-92 | Support path: "Report a problem" with order id prefilled; no personal data in the text. | P1 | Mail/intent opens. | Planned |

## 14. Data formats and remote config

`Content/economy/gem_packs.json` (array, file order = display order; schema `Content/schemas/gem_pack.schema.json`):

```json
[
  {"id": "gempack_pinch", "gems": 10, "usdList": 0.99, "usdPromo": 0.49, "sku": "fluffyclimbers.gems.pinch", "badge": ""},
  {"id": "gempack_sack", "gems": 100, "usdList": 7.99, "usdPromo": 3.99, "sku": "fluffyclimbers.gems.sack", "badge": "popular"},
  {"id": "gempack_treasury", "gems": 1000, "usdList": 29.99, "usdPromo": 14.99, "sku": "fluffyclimbers.gems.treasury", "badge": "best_value"}
]
```
(all 7 tiers are in the file; `badge` is one of `""`, `popular`, `best_value`; `sku` is the list-price product, the promo product is `sku + ".launch"`, conflict C-8.)

`Content/economy/coin_packs.json` (schema `coin_pack.schema.json`):

```json
[
  {"id": "coinpack_100", "coins": 100, "gems": 5},
  {"id": "coinpack_5000", "coins": 5000, "gems": 250}
]
```

Remote config keys (planned; defaults in brackets; all optional, missing key = default):

| Key | Type | Meaning |
|---|---|---|
| `shop.launchPromo.enabled` | bool [true] | Launch promo on/off (D-SHOP-5). Off: list prices charged and shown. |
| `shop.launchPromo.endsAtUtc` | ISO time or null [null] | Promo ends by itself at this time; the shop shows "Launch offer ends in 3d" (P1). |
| `shop.confirmGemThreshold` | int [50] | Gem spends above this ask for confirmation. |
| `shop.tabs.enabled` | array of 4 bool [true,true,true,true] | Show or hide a tab; tabs without content show "Coming soon". |
| `shop.coins.enabled` | bool [true in dev, false in release until a coin sink exists, conflict C-1] | Coins section visible. |
| `shop.gemPacks.badges` | map id to badge | Overrides the `badge` field. |
| `shop.gemPacks.hidden` | array of ids | Hide a pack (for example a regional test). |
| `shop.starterPack.enabled`, `shop.dailyDeal.*`, `shop.offers[]` | structures | Offers (section 10). |
| `shop.chests.version`, `shop.chests.odds`, `shop.chests.pity` | structures | Odds and pity (section 7). |
| `shop.vip.gemsPerDay` | int [25 in doc 08, proposal 15] | VIP perk size. |
| `shop.battlePass.season` | structure | Season id, start/end, tiers, XP table. |
| `shop.parental.pinRequired` | bool [false] | Default for the PIN guard. |

Local stand-ins today: `ProgressService.LaunchPromoActive` (default true), `GameSettings.LaunchPromo` (PlayerPref, default true), `ShopScreen.ConfirmGemThreshold` (50), `ShopScreen.TestPurchasesOverride` (tests only).

## 15. Analytics events (every step)

All events carry `session_id`, `app_version`, `build_type` (`dev`/`release`), `test` (true for TEST purchases, excluded from revenue), `promo_active`. Events are queued offline and sent later (doc 03).

| Event | When | Params |
|---|---|---|
| `shop_opened` | shop screen shown | `entry` (menu, map, prompt:revive, prompt:skip, offer), `tab`, `section` |
| `shop_tab_viewed` | tab/section selected | `tab`, `section` |
| `shop_item_viewed` | card scrolled into view (once per session per card) | `item_id`, `tab`, `position` |
| `shop_purchase_started` | Buy tapped | `item_id`, `price_type` (usd, gems, coins), `price_value`, `list_value`, `promo_active` |
| `shop_purchase_confirmed` | confirm popup accepted | same |
| `shop_purchase_cancelled` | confirm popup or store sheet cancelled | `stage` |
| `shop_purchase_failed` | store/server error | `stage`, `code` |
| `shop_purchase_pending` | payment pending | `item_id` |
| `shop_purchase_completed` | grant confirmed | `item_id`, `amount`, `currency`, `price`, `localized_price`, `order_id_hash` |
| `shop_insufficient_funds` | Not enough gems popup | `item_id`, `need`, `have` |
| `shop_routed_to_gems` | "Get more gems" tapped | `from_item_id` |
| `shop_restore` | restore tapped | `result`, `count` |
| `shop_refund_received` | refund notice | `item_id` |
| `shop_odds_viewed` | odds screen opened | `chest_id`, `version` |
| `shop_offer_shown` / `shop_offer_dismissed` | offers | `offer_id`, `placement` |
| `shop_parental_pin_result` | PIN prompt | `result` (never the PIN) |

## 16. Offline behaviour

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-93 | The shop opens offline. Soft-currency purchases (coins for gems) work offline and are saved locally. | P0 | Airplane-mode test: coin purchase works; wallet saved. | Built |
| FR-SHOP-94 | Real-money purchases need a network and the store; offline the Buy button of a gem pack shows "No connection" and does nothing. | P0 | Message and state tested with the network off. | Planned |
| FR-SHOP-95 | Prices from the store are cached for the session; if never loaded the card shows "Price unavailable" (FR-SHOP-19). | P0 | See FR-SHOP-19. | Planned |
| FR-SHOP-96 | Queued purchases/grants are retried at the next start with network. | P0 | See FR-SHOP-88. | Planned |
| FR-SHOP-97 | Remote config is cached; with no cache the defaults of section 14 apply (promo on, threshold 50). | P0 | Cold start offline shows promo on. | Planned |

## 17. Accessibility, reduced motion, localisation (critical UX rules apply to every shop screen)

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-98 | **No overlap, nothing clipped (CUX-3).** Shop screens, cards, badges, popups fit inside the safe area on 9:16, 9:19.5, 9:21, 3:4. | P0 | PlayMode audit cases: shop tab 4 gems, coins, tab 1, 2, 3, list prices, six popups; at 100 and 130 percent text. | Built |
| FR-SHOP-99 | **Touch targets (CUX-4):** every button at least 48 x 48 dp with 8 dp spacing; Buy in the lower half of the card; the shop is one-handed: Buy buttons are full card width. Left-handed mirror: the shop has no side-specific controls, the back arrow stays at the top-left (the same as other menus). | P0 | Audit; manual left-hand check. | Built |
| FR-SHOP-100 | **Legible text (CUX-5):** at least 16 sp, 4.5:1 contrast (3:1 above 24 sp); price and amount text on the dark card backing; 130 percent text works. | P0 | Audit and contrast helper checks. | Built |
| FR-SHOP-101 | **Colour is never the only cue (CUX-6):** promo = strike line + "Launch offer" text + colour; selected tab = size + label; chest and character rarity = frame shape + text; locked = text. | P0 | Greyscale screenshot review. | Built |
| FR-SHOP-102 | Accessibility labels on all buttons ("Buy 100 gems for $3.99, test purchase"). | P0 | Every `Button` has an `A11yLabel` text. | Built |
| FR-SHOP-103 | **Reduced motion:** sale animations (pulse on badges, countdown pulse, chest opening, preview animation) are replaced by static versions when reduced motion is on; nothing flashes more than 3 times a second; no full-screen flash. | P0 for any animation | Setting test; the dev slice has no sale animation. | Built (none) |
| FR-SHOP-104 | Countdowns and prices read out as text, not images; screen-reader order follows visual order. | P1 | Manual TalkBack pass. | Planned |
| FR-SHOP-105 | **Localisation:** all shop strings go through the string table (doc 03 localisation plan), no concatenated sentences that break in other languages (use format strings), plurals handled, right-to-left tested at least once, number formats from the locale (the dev slice uses an invariant format). Launch languages: first 5 of doc 07. | P1 | Pseudo-localisation test (+40 percent length) passes the layout audit. | Planned |
| FR-SHOP-106 | Currency symbols and decimal separators come from the store string. | P0 | See FR-SHOP-80. | Planned |

## 18. Test plan

| Layer | What | Where |
|---|---|---|
| Content (Python) | Both JSON files validate; promo never above list; unique SKUs; tables equal the owner brief | `python Tools/content_cli/content.py validate`, `Tests/content/test_content.py` |
| EditMode (C#) | `ShopTests`: owner tables, built-in tables equal the JSON files, test gem grant saves at once, coin pack debit/credit atomic, promo on/off price, insufficient gems change nothing, exact balance, saturation, invalid packs, old save loads, value table figures | `Assets/_Game/Tests/EditMode/ShopTests.cs` |
| PlayMode layout (CriticalUX) | `ShopScreenHasNoOverlapAndFitsEveryAspect`, `ShopScreenSurvives130PercentText`: tab 4 gems (release and dev labels), coins, tabs 1 to 3, promo off, popups (not available, test buy, confirm coins, not enough gems, coins done), 4 aspects x 2 safe areas | `CriticalUxTests.cs` |
| Device (manual, dev APK) | Menu > Shop; buy a test pack, wallet grows; buy a coin pack with gems; 51+ gems asks first; too few gems routes to Gems; scroll with a thumb; 130 percent text; TalkBack labels; reduced motion; kill the app after a purchase and reopen | checklist in `.claude/pendingdecisions` after the first run |
| Store sandbox (later) | Play test tracks, licence testers: purchase, cancel, pending, refund, restore, subscription states | T-SHOP-9, T-SHOP-10 |
| Server (later) | Receipt replay, forged token, double grant, refund revoke, rate limits | server tests (doc 03) |
| Compliance (later) | Odds screen present, subscription disclosure, age gate, policy checklist, privacy labels | release checklist |
| Performance | Opening the shop and switching tabs does not hitch the next run; no `Update` in shop code; no allocation in the run scene from the shop | profiler pass |

## 19. Tasks (Claude minutes, +-40 percent; owner blockers named)

| ID | Task | Claude min | Status | Owner blocker |
|---|---|---|---|---|
| T-SHOP-1 | This requirements document, doc 01/07/13 pointers, roadmap entries | 90 | done 2026-10-20 | - |
| T-SHOP-2 | Pack data (`gem_packs.json`, `coin_packs.json`, schemas, generator, validator, `ShopCatalog`, `ProgressService` shop methods, tests) | 90 | done 2026-10-20 (not compiled) | - |
| T-SHOP-3 | Shop screen, Tab 4 with test purchases, menu button, layout audit cases | 150 | done 2026-10-20 (not compiled) | - |
| T-SHOP-4 | Main session: regenerate the content manifest, compile, run EditMode and PlayMode, fix findings, device check of the screen | 60 | next | - |
| T-SHOP-5 | Wire remote config (`shop.launchPromo.*`, `shop.confirmGemThreshold`, `shop.tabs.enabled`, `shop.coins.enabled`), promo end countdown | 90 | pending | remote config service decision (doc 03) |
| T-SHOP-6 | Analytics events of section 15 | 90 | pending | analytics provider |
| T-SHOP-7 | Map top bar entry and "Get gems" routes from revive and region-skip prompts; session memory of tab | 60 | pending | - |
| T-SHOP-8 | Settings > Purchases (history, restore row, legal links, PIN, limits) | 150 | pending | terms, privacy policy text, refund policy text |
| T-SHOP-9 | Real billing, Google Play: products (7 gem SKUs + 7 `.launch` SKUs), purchase flow, state machine of section 6, price strings | 480 | pending | Play Console app + merchant account, tax profile, product creation, licence testers, $0.49 price point check |
| T-SHOP-10 | Server receipt validation, wallet grant, refund revoke, anti-fraud | 480 | pending | server stack decision (PlayFab or own), Play service account |
| T-SHOP-11 | Tab 2: power-ups (4 sim features + bot tests, inventory, loadout) | 480 | pending | power-up art (OpenArt credit approval), rule decisions Q4, Q5 |
| T-SHOP-12 | Tab 2: characters (data, preview, equip) | 240 | pending | character list and art |
| T-SHOP-13 | ~~Tab 1: gear slots, rarity frames, preview, buy/equip, upgrades~~ removed 2026-11-01 (no gear) | 0 | removed | - |
| T-SHOP-14 | Tab 1: Power-up Chests with odds and pity (server side); Coin Chests as earn-only stock | 360 | pending | odds table, legal odds text, server |
| T-SHOP-15 | Tab 3: VIP subscription | 360 | pending | prices, perks decision (conflict C-5), subscription products |
| T-SHOP-16 | Tab 3: battle pass (season data, XP, tracks, claim) | 600 | pending | season length, reward list, premium price |
| T-SHOP-17 | Offers: starter pack, daily deal, limited-time offers | 300 | pending | offer contents |
| T-SHOP-18 | Age gate, parental controls, regional price checks, compliance checklist | 240 | pending | legal review, age-rating questionnaire |
| T-SHOP-19 | Localisation of the shop strings (5 languages) and pseudo-localisation audit | 150 | pending | translations |
| T-SHOP-20 | iOS StoreKit path | 360 | pending | Apple developer account, Mac |

## 20. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Coins are sellable before anything can be bought with them (C-1) | Players waste gems; bad reviews | Flag `shop.coins.enabled`; ship coin sinks first |
| Crossed-out list price judged misleading (C-7) | Store or consumer-law complaint | Legal read, promo end date, wording |
| Promo switch disagrees with the store price (C-8) | Charging the wrong amount | Two SKUs per pack; price text always from the product being bought |
| Coin prices written at 100:1 in older docs (C-3) | Coin items 5 times too expensive | Coin price = 20 x gem price before items ship |
| VIP undercuts packs (C-5) | Lower pack revenue | Reduce daily gems |
| Free gem inflow too generous (C-6) | Low IAP revenue | Soft-launch data, then decide |
| Chests without odds | Store rejection, fines | FR-SHOP-29 is P0 for chests |
| Minors buying | Legal and trust damage | Age gate, PIN, limits |
| Shop UI too dense at 130 percent text | Clipped prices | Cards are single column, audit at 130 percent |
| Test purchase path leaks into a release build | Free gems | `DevBuild` guard, server rejects test receipts |

## 21. Open questions for the owner

| ID | Question | My default [ASSUMPTION] |
|---|---|---|
| Q1 | ~~Approve conflict C-2~~ resolved 2026-11-01: 1 gem = 20 coins, fixed. | Done in docs; game fork updates code |
| Q2 | C-1: hide the Coins section in release builds until a coin sink exists? | Yes, flag default off in release |
| Q3 | C-4: leave the 50 and 100 gem packs as they are, or add +10 bonus gems to the 100 pack? | Leave as is |
| Q4 | Which power-ups at launch and how many in a loadout (2 proposed)? | shield, magnet, extra jump, slow time; 2 |
| Q5 | Are power-ups consumed on use or at run start? | On use |
| Q6 | Battle pass: season length (60 days), tiers (30), premium price ($4.99 or $9.99), tier skip on purchase? | 60 days, 30 tiers, $4.99, no skip |
| Q7 | VIP: daily gems 25 or 15 (C-5), weekly $2.99 and monthly $7.99 final? | 15 per day, prices as doc 08 |
| Q8 | How long does the launch promo run (end date) and in which regions? | 30 days, all regions |
| Q9 | Is $0.49 allowed as the 10-gem promo price in all launch regions? | Check the console; else $0.99 |
| Q10 | Starter pack contents and price? | 100 gems + 3,000 coins + 2 Rare + 1 Epic Power-up Chest + 1 day ad-free, $2.99 |
| Q11 | Spending cap default for players under 18 (per month)? | $50, changeable |
| Q12 | One shop entry on the map top bar OK (small button between title and wallet)? | Yes, 48 dp |
| Q13 | Should gem packs 10 and 20 exist at launch given 100 gems/hour of free play? | Keep (owner table) |
