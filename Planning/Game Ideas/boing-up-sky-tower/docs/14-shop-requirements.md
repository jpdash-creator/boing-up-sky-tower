# 14 - In-app store (Shop): requirements

> **Corrected 2026-11-01 to match the code (build v0.38.0.40).** Authoritative now: **doc 27** (all economy numbers and tables), **doc 26** (shop look, 5 tabs, Bundles, Subscription, Rewards), **doc 22** (packs, chests, prices, revive) and **doc 20** (characters and chests, gear removed). Where this file differs, those win. This file keeps the shop shell, purchase flows, compliance and billing requirements. Requirement ids are kept; an id that no longer applies is marked "removed or superseded 2026-11-01 (see doc N)" and is not renumbered.
> Owner rules applied: **1 gem = 20 coins (fixed)**; **no gear** (hats, shirts, pants, boots, gear chests, shards, wardrobe are gone); **no VIP tiers** (replaced by the Weekly and Monthly Subscription); **no battle pass** (not planned).
> Everything described below as "Built" was checked against `Assets/_Game/Economy/*.cs`, `Gameplay/ShopScreen*.cs` and `Content/economy/*.json`.

Legend. Priority: **P0** needed for the first store release, **P1** soon after, **P2** later. Tags: **[OWNER]** decided by the owner, **[ASSUMPTION]** my default, **(verify)** depends on store policy or law. State: **Built**, **Planned**, **Removed**.

---

## 1. Scope and phasing (current)

| Phase | What | Billing | State |
|---|---|---|---|
| Built (v0.38) | Shop with 5 tabs, 9 gem packs, 7 coin packs (gems to coins), 7 power-ups, Iron and Gold chests, 1,000 generated Bundles with Refresh and timer offer, Subscription page, Rewards screen (Main, Daily, Weekly), revive with a free ad (test ads) | none: gem packs and subscriptions are TEST purchases in development builds; release shows "Not available yet" | Built |
| Release 1.0 (P0) | Real gem and subscription purchases on Google Play, server validation, restore, age gate, parental and spending safeguards, analytics, remote config | Play Billing | Planned |
| Later (P2) | Starter pack and money bundles (doc 22 point 7), iOS StoreKit, characters in the shop and in chests | Play Billing + StoreKit | Planned |
| Not planned | Gear, VIP tiers, battle pass, daily deal, limited-time offers, interstitials | - | see section 21 |

Out of scope here: ads (doc 13), leaderboards, the server design (doc 03), art production (doc 04; the OpenArt credit guardrail applies to every generated icon).

## 2. Owner decisions

| ID | Decision | Tag | Status 2026-11-01 |
|---|---|---|---|
| D-SHOP-1 | The shop is part of the game. | [OWNER] | Kept. **Tabs superseded (doc 26): five tabs Bundles, Chests, Power-ups, Coins, Gems; a Subscription button in the shop header and on the home screen.** Characters are a "coming soon" card (doc 22 4.5). |
| D-SHOP-2 | Gem packs. | [OWNER] | **Superseded by doc 22 4.1 and `gem_packs.json`: 9 packs, 50 to 15,000 gems, $0.79 to $119.99** (section 5.1). |
| D-SHOP-3 | Coin packs bought with gems. | [OWNER] | **Superseded by doc 22 4.2 and `coin_packs.json`: 7 packs, 1,000 to 100,000 coins for 50 to 4,000 gems** (section 5.2). |
| D-SHOP-4 | Tables replace the earlier plan in `gem_packs.json` and the docs. | [OWNER] | Done with the new tables. |
| D-SHOP-5 | Launch promo with the list price struck through; switch off remotely. | [OWNER] | Code exists (`ProgressService.LaunchPromoActive`, `GameSettings.LaunchPromo`) but **OFF by default** (doc 22 owner answer 5). Promo prices in the data are placeholders (about half of list). |
| D-SHOP-6 | Requirements document and a first shop slice with TEST purchases. | [OWNER] | Done; the shop is now far beyond that slice. |
| D-SHOP-7 | First-purchase double: the first purchase of each pack of 50 to 1,000 gems credits double (once per pack and account). | [OWNER] doc 22 | Built, **OFF by default** (`FirstPurchaseDoubleActive`, `GameSettings.FirstPurchaseDouble`). |

## 3. Patterns used (generic)

The original table of patterns from other games was historical. The patterns that are actually in the product are: a single shop reachable from the home screen, the map and every "not enough gems" prompt; a wallet always visible; badges (Popular, Best value); a confirm above 50 gems; routing from a shortfall to the smallest pack that covers it; contents text on chests; a subscription with a daily claim. Patterns for a starter pack, daily deal, limited-time offers, tiered chests with pity and gear upgrades are **not built and not planned** (section 21).

## 4. Shop shell and navigation

Entry points: home "Shop" button, the chest button (opens the Chests tab directly; home, map and level screens), the Subscription button, every "Not enough gems" prompt (revive, coin packs, chests). The run scene never shows a shop; during a run only the revive popup spends gems.

Layout (doc 26 section 1): the Momo shopkeeper scene across the top, wooden background below, a ribbon title per page, a bottom tab bar of **5 tabs: Bundles | Chests | Power-ups | Coins | Gems** (selected tab yellow with its name, others icon-only), a blue back arrow at the bottom left, the wallet pills (coins, gems, stars) on top.

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-1 | The home screen has a "Shop" button; PLAY keeps its place. | P0 | Menu layout audit passes on 9:16, 9:19.5, 9:21, 3:4 at 100 and 130 percent text. | Built |
| FR-SHOP-2 | The shop is reachable from the world map and from every insufficient-gems prompt; the prompt opens the Gems tab. | P1 | Revive and coin-pack shortfalls open the Gems tab (the smallest covering pack is chosen by `ShopCatalog.SmallestGemPackCovering`; highlighting is an open item, doc 22 item 14). | Built (tab), highlight Planned |
| FR-SHOP-3 | **Superseded 2026-11-01 (see doc 26):** five tabs in the order Bundles, Chests, Power-ups, Coins, Gems; the selected tab is larger and carries the accessibility label "selected". | P0 | Tab buttons at least 48 dp, 8 dp apart; selection visible without colour. | Built |
| FR-SHOP-4 | A section whose content is not built shows a "Coming soon" card. | P0 | Characters card; no buy buttons. A remote flag per tab can hide it. | Built (card), flag Planned |
| FR-SHOP-5 | The wallet (coins, gems) is visible on every tab and updates immediately after a purchase. | P0 | Numbers not clipped up to 999,999 at 130 percent text. | Built |
| FR-SHOP-6 | Back returns to the caller; the Android back key does the same; an open popup closes first. Back from the Chests tab goes home (doc 26 B1). | P0 | Device test. | Built |
| FR-SHOP-7 | The shop opens on Power-ups (doc 23); a caller can ask for another tab. | P1 | Re-entry shows the requested tab. | Built |
| FR-SHOP-8 | Menu screen rules: no per-frame allocations, scrolled lists, shared modal popups. | P0 | CriticalUX layout audit covers the shop screens and popups. | Built |

## 5. Gems and Coins

### 5.1 Gem packs (doc 22 4.1, `Content/economy/gem_packs.json`), list prices

| Pack id | Gems | $ list | INR | $ promo (off) | $ per gem | Gems per $ | Badge | First-purchase double |
|---|---|---|---|---|---|---|---|---|
| gempack_50 | 50 | 0.79 | 75 | 0.49 | 0.0158 | 63.3 | - | yes |
| gempack_100 | 100 | 1.49 | 142 | 0.79 | 0.0149 | 67.1 | Popular | yes |
| gempack_200 | 200 | 2.49 | 237 | 1.29 | 0.01245 | 80.3 | - | yes |
| gempack_500 | 500 | 5.99 | 569 | 2.99 | 0.01198 | 83.5 | - | yes |
| gempack_1000 | 1,000 | 10.99 | 1,044 | 5.49 | 0.01099 | 91.0 | Best value | yes |
| gempack_2000 | 2,000 | 20.99 | 1,994 | 10.49 | 0.01049 | 95.3 | - | no |
| gempack_5000 | 5,000 | 49.99 | 4,749 | 24.99 | 0.01000 | 100.0 | - | no |
| gempack_10000 | 10,000 | 89.99 | 8,549 | 44.99 | 0.00900 | 111.1 | - | no |
| gempack_15000 | 15,000 | 119.99 | 11,399 | 59.99 | 0.00800 | 125.0 | - | no |

Gems per dollar rise at every step from the 100 pack up (validator test). $1 = 95 rupees. The 1,000-gem list rate ($0.01099) is the owner's reference gem value. The promo and double are off by default; with the promo on, gems per $ become 102 to 250.

### 5.2 Coin packs (doc 22 4.2, `Content/economy/coin_packs.json`), paid with gems

**Fixed rate: 1 gem = 20 coins.** The four smallest packs pay exactly 20 coins per gem, the larger ones 22.2, 22.7 and 25.

| Pack id | Coins | Gems (list) | Gems (promo, off) | Coins per gem | $ ref (gems x $0.01099) |
|---|---|---|---|---|---|
| coinpack_1000 | 1,000 | 50 | 25 | 20.0 | 0.55 |
| coinpack_2000 | 2,000 | 100 | 50 | 20.0 | 1.10 |
| coinpack_5000 | 5,000 | 250 | 125 | 20.0 | 2.75 |
| coinpack_10000 | 10,000 | 500 | 250 | 20.0 | 5.50 |
| coinpack_20000 | 20,000 | 900 | 450 | 22.2 | 9.89 |
| coinpack_50000 | 50,000 | 2,200 | 1,100 | 22.7 | 24.18 |
| coinpack_100000 | 100,000 | 4,000 | 2,000 | 25.0 | 43.96 |

### 5.3 Requirements

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-9 | Gems and Coins are two tabs of the bottom bar. | P0 | Each reachable with one tap. | Built |
| FR-SHOP-10 | A pack card shows icon, amount, badge text, price. Gem packs: three per row, polished gold cards, price button in green (doc 26). Coin packs: three per row, price button green with the gem icon and count. Badge "Popular" or "Best value" is small green italic text below the icon, never covering the card border. | P0 | Nothing overlaps or is clipped on four aspects at 130 percent; Buy at least 48 dp. | Built |
| FR-SHOP-11 | Pack lists are data driven from `gem_packs.json` and `coin_packs.json`; a built-in copy (`ShopCatalog`) is kept identical by tests (`ShopEconomyTests`). | P0 | Adding a pack to the JSON shows it. | Built |
| FR-SHOP-12 | Launch promo display (list struck through, "Launch offer"). | P0 | Only when the promo switch is on. | Built, **switch OFF by default** |
| FR-SHOP-13 | Promo off switch (flag). | P0 | Off: list price shown and charged. | Built (local switch), remote Planned |
| FR-SHOP-14 | Coin packs are paid with gems at the promo or list gem price of `coin_packs.json`. The price shown is the price charged. | P0 | `ShopCatalog.CoinPackGems(pack, promo)`; tests cover promo on and off. | Built |
| FR-SHOP-15 | A coin purchase debits gems and credits coins in one step and saves once. | P0 | Failed purchase changes nothing; coin total saturates at int max. | Built |
| FR-SHOP-16 | Development builds: gem packs are TEST purchases (confirm popup, no money, wallet saved at once). | P0 | Only when `GameSettings.DevBuild`; `SYSTEM_TEST` flag (doc 23). | Built |
| FR-SHOP-17 | Release builds without billing: the gem Buy button reads "Not available yet". | P0 | No grant path exists for gems in release except billing. | Built |
| FR-SHOP-18 | Badges come from pack data (Popular on 100, Best value on 1,000). | P1 | Text is a real word, never covers price or Buy. | Built |
| FR-SHOP-19 | With real billing every gem price is the store-localised price string; rupee and dollar numbers in JSON are fallback and design reference. | P0 | Unavailable price shows "Price unavailable" and disables Buy. | Planned |

### 5.4 Economy cross-check

All cross-checks (value per dollar, dollars per gem, time to afford) are in **doc 27 sections 4 to 6 and 15**. Facts used: gems per 5 regions about 260 at 60 percent pickup; rewards give far more (doc 27 section 14); a level pays 250 coins at 3 stars (region 1).

### 5.5 Conflicts of the original document, status

| ID | Original conflict | Status 2026-11-01 |
|---|---|---|
| C-1 | Coin packs live but no coin sink. | **Resolved:** power-ups (1,000 / 1,500 coins), bundle Refresh (100 coins) and later characters take coins. |
| C-2 | 100 coins per gem anchor vs the owner tables. | **Resolved: rate fixed at 20 coins per gem** (doc 22). `remote_config.json` `coinsPerGem` is now 20 (RC 1, 2026-11-01). |
| C-3 | Item prices set at $0.011 per gem. | **Resolved:** item gem prices (50 / 75) follow the 20:1 rate (1,000 / 1,500 coins). |
| C-4 | No volume bonus between 50 and 100 gems. | **Resolved:** the new ladder improves at every step. |
| C-5 | VIP gems undercut packs. | **Superseded:** VIP removed; the subscription is cheaper per gem than packs on purpose (doc 27 RC 6). |
| C-6 | Free gem inflow vs packs. | Open: rewards, not pickups, are the faucet (doc 27 findings 1 and 2). |
| C-7 | A list price never charged may mislead. | Open (verify): the promo is off, so list is the real price. |
| C-8 | One store product has one price (promo SKU). | Open for billing: SKUs `fluffyclimbers.gems.<n>` exist; promo SKU `.launch` is Planned. |
| C-9 | Skips and revives sold to someone who can buy a large pack. | Accepted. |

## 6. Purchase flows

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-20 | Confirmation threshold: a spend of more than 50 gems asks for confirmation (price and contents); 50 or less does not. Coin packs always show the confirm popup (doc 23 H2b). | P0 | Cancel changes nothing. | Built |
| FR-SHOP-21 | Insufficient funds: "Not enough gems" and the shortfall; a coin pack or chest the player cannot afford goes straight to the Gems tab. | P0 | Wallet unchanged. | Built |
| FR-SHOP-22 | Every successful purchase ends with a result popup naming what was added. | P0 | OK closes it. | Built |
| FR-SHOP-23 | Real-money purchases use only the store sheet. | P0 | No payment fields in the game UI. | Planned |
| FR-SHOP-24 | Double-tap protection while a purchase runs. | P0 | Two taps within 500 ms create one transaction. | Planned |
| FR-SHOP-25 | Pending purchases survive a restart. | P0 | No grant before the store says purchased. | Planned |
| FR-SHOP-26 | Wallet operations are atomic and saved immediately (`Commit()`); the server wallet wins later. | P0 | Kill between grant and save: no double or lost grant. | Built (local), server Planned |
| FR-SHOP-27 | Soft purchases (coins, power-ups, chests, bundles with gems) are final. | P1 | Policy text in Settings. | Planned |

The state machine of a real-money purchase (Idle, ProductsLoaded, StoreSheet, Validating, Granting, Consuming, Done / Failed / Pending / Revoked) stays as in the original design and is implemented with the billing task T-SHOP-9.

## 7. Tab "Chests" (Iron and Gold)

> Gear, Gear Chests, rarity frames, upgrade levels, preview and event gear are **removed 2026-11-01 (no gear)**. Chest specification: doc 22 point 3, doc 26 section 1, doc 27 section 10.

Rules [CODE `Chests.cs`]: two chests, gems only, **Iron 100 gems (open 10: 1,000)**: 1 power-up + 400 to 600 coins. **Gold 300 gems (open 10: 3,000)**: 3 power-ups + 1,000 to 1,400 coins. Each power-up is one of the 7 types with equal chance. Characters switched off (`CharactersEnabled = false`; duplicate refund 25 percent of the character's gem price when on). No chests are earned from play.

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-28 | Two chests, Iron and Gold, paid in gems, open 1 or open 10 (10 times the price). Page titled CHEST; each card shows the chest picture, name, direct text of the contents (coins gold, power-ups green), Open 1 and Open 10 buttons. | P0 | Opening pays exactly one result; skippable 1.2 s animation; none under reduced motion. | Built |
| FR-SHOP-29 | Odds disclosure: contents text before purchase and the info text; power-ups are 1 in 7 each. | P0 | Text matches the roller. | Built |
| FR-SHOP-30 | **Removed or superseded 2026-11-01 (see doc 22 point 3):** pity timer. Not in the code; chests have fixed ranges. | - | - | Removed |
| FR-SHOP-31 | **Removed (no gear):** duplicate protection for gear. Character duplicates refund 25 percent gems when characters are on. | - | - | Removed |
| FR-SHOP-32 | Chest ranges and prices are config (`ChestRolls` constants today; remote config later). | P2 | Changing a value never changes past results. | Planned |
| FR-SHOP-33..38 | **Removed 2026-11-01 (no gear):** gear slots, rarity frames, upgrade levels, preview, owned/equip states, event gear. | - | - | Removed |

## 8. Tab "Power-ups" and Characters

Power-up rules [CODE `PowerUpCatalog.cs`, doc 27 section 7]: 7 types are sold (Flight, Rocket, Double Jump, Wall Stick, Shield, Magnet, Slow Time); **1,000 coins / 50 gems** (Magnet, Shield, Wall Stick, Double Jump) or **1,500 coins / 75 gems** (Slow Time, Rocket, Flight); 5 units -10 percent, 10 or more -20 percent. No stock cap, no loadout, no carry cap (rework 2026-10-31); every owned type is available in a run; a tap while active adds the full duration. Spring Boots, Coin Doubler, Star Seeker, Safety Net are hidden rows (not implemented).

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-39 | Seven consumable power-ups with the prices above, bought with coins or gems. | P0 | Prices from `powerups.json` (equal to the built-in table by test). | Built |
| FR-SHOP-40 | **Superseded 2026-11-01 (see docs 20, 27):** per-run loadout. There is no loadout; all owned power-ups are available. | - | - | Removed |
| FR-SHOP-41 | Power-ups disabled in Ranked. | P0 | Ranked run has no power-up buttons. | Planned (Ranked not built) |
| FR-SHOP-42 | **Superseded 2026-11-01:** stack caps. There is no stock cap; an "Owned: N" label shows the count. | P1 | - | Built (label) |
| FR-SHOP-43 | Post-run power-up offers. | P2 | - | Not planned |
| FR-SHOP-44 | Characters: Momo (region 1) and Sporeling (region 2) are Commons owned when their region unlocks; the shop shows a single "Coming soon" card (doc 22 4.5). Owner price list for later: Uncommon 150, Rare 300, Epic 700, Legendary 1,500 gems. | P1 | No buy buttons while coming soon. | Built (coming soon), purchases Planned |
| FR-SHOP-45 | Character preview in the shop. | P2 | - | Not planned (preview is the Heroes screen) |
| FR-SHOP-46 | Characters are cosmetic in Ranked (neutral). | P0 | - | Planned |
| FR-SHOP-47 | Locked characters explain how to unlock. | P1 | "Unlock Mushroom Grove first" style text. | Planned |
| FR-SHOP-48 | One selected character, switching is free and saved. | P1 | The home screen and the run draw the selected one (doc 23). | Built |
| FR-SHOP-49 | Bundles may contain a character (schema supports it, none yet). | P2 | - | Planned |
| FR-SHOP-50 | Event characters. | P2 | - | Not planned |
| FR-SHOP-51 | **Removed 2026-11-01:** inventory screen with gear. The Heroes screen lists characters; owned power-up counts show on the power-up cards and HUD. | - | - | Removed |
| FR-SHOP-52 | Every inventory change is saved at once. | P1 | Same atomic rules as FR-SHOP-15. | Built |

## 9. Subscription and Rewards (replace "VIP and Battle Pass")

> VIP tiers, jump/gem/coin percentage bonuses and the battle pass are **removed or superseded 2026-11-01 (see doc 26 and doc 27 section 8)**. Subscription (doc 26 section 2): **Weekly Subscription $1.49 (Rs 149)**: 100 gems on purchase, then 7 daily claims of 10 gems + 500 coins. **Monthly Subscription $4.99 (Rs 449)**: 200 gems, then 30 daily claims of 10 gems + 600 coins, and one random power-up on every 5th claim. Claim once per local day; missed days are not stacked; buying while active extends the time. Test purchase in development builds with `SYSTEM_TEST`; release shows "Not available yet".

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-53 | Two auto-renewing subscriptions, weekly and monthly. | P2 | Both products exist in the store; the screen shows the store price. | Test purchase Built, billing Planned |
| FR-SHOP-54 | Perks: instant gems, daily gems and coins, free power-ups (monthly), "Worth about 2.5x / 3.1x" text from list rates (`SubscriptionCatalog.WorthText`). No stat bonuses, no ad-free yet. | P2 | Daily claim once per local day. | Built |
| FR-SHOP-55 | Auto-renew disclosure on the card: price, period, renews until cancelled, how to cancel, links. | P0 for subscriptions | Visible without scrolling at 9:16 and 130 percent text. | Planned |
| FR-SHOP-56 | Entitlement is server-side; offline grace 3 days. | P2 | Airplane-mode test. | Planned |
| FR-SHOP-57 | States handled: active, grace, on hold, paused, cancelled-but-active, expired, refunded. | P2 | Store test accounts. | Planned |
| FR-SHOP-58 | Manage/Cancel link opens the store page. | P0 for subscriptions | - | Planned |
| FR-SHOP-59..65 | **Removed 2026-11-01 (see doc 26 section 4):** battle pass (tiers, XP, premium unlock, catch-up, claim rules, rollover). Replaced by the Rewards screen: Main (star milestones, region cleared), Daily (10 actions, 100 points), Weekly (11 tasks); tables in doc 27 sections 11 to 13. | - | - | Removed |
| FR-SHOP-66 | Subscription purchases show a receipt in Settings > Purchases. | P1 | Loads offline. | Planned |

## 10. Bundles and offers

Built [doc 26 section 3, doc 27 section 9]: **1,000 generated Bundles** priced in gems (200 to 500), each worth 1.4 to 1.8 times its price (average 1.60); the Bundles page shows 6 random bundles (stable until refreshed); **Refresh** costs 100 coins or 5 gems; a **timer bundle** (30 percent off, 5 minutes, about every 45 minutes of play, never twice in a row, saved across restarts) sits in a corner button on the home screen (hidden on the world map).

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-67 | **Starter pack** (doc 22 point 7): once per account, $0.99 = 100 gems + 2,000 coins; hidden after purchase. (The older 50 gems / 3,000 coins / gear contents are removed.) | P1 | One purchase per account (server check). | Planned, not built |
| FR-SHOP-68 | **Removed or superseded 2026-11-01 (see doc 26 section 4):** daily deal. The Daily rewards on the Rewards screen replace it. | - | - | Removed |
| FR-SHOP-69 | **Removed 2026-11-01:** limited-time offers. The timer bundle (30 percent off) is the only timed offer. | - | - | Superseded |
| FR-SHOP-70 | Offers never block retry or the results screen and are never shown during a run. | P0 | Layout audit; the timer button is hidden in a run. | Built |

## 11. Currency rules

| ID | Requirement | Pri | Acceptance criteria | State |
|---|---|---|---|---|
| FR-SHOP-71 | Only gems buy coins. Coins never convert to gems. | P0 | No code path credits gems from coins. | Built |
| FR-SHOP-72 | **Rate fixed: 1 gem = 20 coins** (doc 22 shared conventions). Coin packs pay 20, 20, 20, 20, 22.2, 22.7, 25 coins per gem; power-up prices are 1,000 coins = 50 gems and 1,500 coins = 75 gems. The old 100:1, 10:1 (list) and bonus-pack rules are void. | P0 | `ShopEconomyTests` (coins per gem never drop as packs grow). | Built |
| FR-SHOP-73 | Gems and coins are integers, never negative, saturate at int max, thousands separator. | P0 | Wallet tests. | Built |
| FR-SHOP-74 | Free and bought gems are one balance, no expiry; analytics distinguish the source. | P0 | Event param `source`. | Planned (no analytics layer yet) |

## 12. Compliance, safety and regional rules

| ID | Requirement | Pri | State |
|---|---|---|---|
| FR-SHOP-75 | Digital goods use the store billing system only (verify current rules per region). | P0 | Planned |
| FR-SHOP-76 | Odds disclosure for random paid items (chests) before purchase; chest contents text and the (i) info satisfy this today. | P0 | Built (text), store policy check Planned |
| FR-SHOP-77 | Parental controls: optional PIN guarding real-money and large gem spends. | P1 | Planned |
| FR-SHOP-78 | Spending limits in the player's settings. | P1 | Planned |
| FR-SHOP-79 | Age gating at first start; under-age profiles see no real-money purchases and no personalised ads (verify). | P0 | Planned (ad consent wording is open item 10, doc 22) |
| FR-SHOP-80 | Regional prices come from the store; USD and INR numbers are the base. | P0 | Planned |
| FR-SHOP-81 | Tax display: the store string only. | P0 | Planned |
| FR-SHOP-82 | Minimum price points: the promo prices ($0.49 for 50 gems, $0.79 for 100) must exist as store price points if the promo is ever turned on (verify). | P0 | Planned |
| FR-SHOP-83 | Legal texts reachable from Settings > Purchases and the subscription card. | P0 | Planned |
| FR-SHOP-84 | Accidental-purchase protection: confirm above 50 gems; no one-tap real-money purchase. | P0 | Built (gems), store sheet Planned |

## 13. Receipts, restore, refunds, anti-fraud

FR-SHOP-85 server receipt validation (P0, Planned); FR-SHOP-86 refunds and revokes (P1, Planned); FR-SHOP-87 restore purchases, required for subscriptions (P0, Planned); FR-SHOP-88 unfinished purchase recovery (P0, Planned); FR-SHOP-89 anti-fraud (P1, Planned); FR-SHOP-90 test purchases impossible in release (P0, **Built** guard, server Planned); FR-SHOP-91 purchase history (P1, Planned); FR-SHOP-92 support path with order id (P1, Planned). Details as in doc 13.

## 14. Data formats and remote config

Shipped data (all under `Content/economy/`): `gem_packs.json` (id, gems, usdList, usdPromo, inrList, firstPurchaseDouble, sku, badge), `coin_packs.json` (id, coins, gemsList, gemsPromo), `powerups.json`, `subscriptions.json`, `bundles.json` (1,000 entries, also copied to `Assets/Resources/Content/bundles.json`), `rewards.json`. Chest numbers are constants in `ChestRolls`.

Remote config keys (planned; missing key = default): `shop.launchPromo.enabled` [false], `shop.firstPurchaseDouble.enabled` [false], `shop.confirmGemThreshold` [50], `shop.tabs.enabled`, `shop.coins.enabled`, `shop.gemPacks.badges`, `shop.gemPacks.hidden`, `ads.*`. Current `remote_config.json` economy block: `coinsPerGem` **20**, `replayCoinPercent` 50, `regionCoinScalePercent` 3 (`vipGemsPerDay` was deleted in RC 1, 2026-11-01), `endlessCoinDailyFullRateRuns` 3, `endlessCoinOverCapMultiplier` 0.5.

## 15. Analytics events

Not built: the code has no analytics layer (doc 22 open item 16). Planned events: `shop_opened`, `shop_tab_viewed`, `shop_purchase_started / confirmed / cancelled / failed / pending / completed`, `shop_insufficient_funds`, `shop_routed_to_gems`, `shop_restore`, `shop_refund_received`, `chest_open`, `chest_odds_viewed`, `gem_pack_bought` (with first-purchase flag), `coin_pack_bought`, `bundle_view`, `bundle_bought`, `subscription_started`, `subscription_claimed`, `reward_claimed`, `revive_offer`, `revive_taken`. All carry `session_id`, `app_version`, `build_type`, `test`.

## 16. Offline behaviour

FR-SHOP-93 the shop opens offline and soft-currency purchases (coins, power-ups, chests, bundles) work offline and are saved locally (P0, Built). FR-SHOP-94 real-money purchases need the network ("No connection", P0, Planned). FR-SHOP-95 store prices cached; "Price unavailable" otherwise (P0, Planned). FR-SHOP-96 queued grants retried (P0, Planned). FR-SHOP-97 remote config cached; defaults apply with no cache (promo off, threshold 50) (P0, Planned).

## 17. Accessibility, reduced motion, localisation

FR-SHOP-98 no overlap or clipping on four aspects (CUX-3, Built, audited); FR-SHOP-99 touch targets 48 dp with 8 dp gaps, Buy buttons full width, one-handed (Built); FR-SHOP-100 text at least 16 sp, 4.5:1 contrast, 130 percent text (Built); FR-SHOP-101 colour never the only cue (badge text, strike line, selected-tab label) (Built); FR-SHOP-102 accessibility labels on all buttons (Built for shop buttons); FR-SHOP-103 reduced motion removes chest animation and badge pulses (Built); FR-SHOP-104 countdowns read as text (timer bundle) (Built); FR-SHOP-105 shop strings through the string table (Planned); FR-SHOP-106 currency symbols from the store (Planned; the font lacks the rupee sign, so prices show "Rs", doc 26 B1).

## 18. Test plan

Built tests: `ShopEconomyTests` (packs equal JSON, coins per gem never drop, dollars per gem improve, purchase flows incl. first-purchase double and promo on/off), `ShopTests`, `ChestTests` (100,000 openings, ranges, equal power-up chance, 10x = 10 openings), `BundleTests` (+ `Tests/content/test_bundles.py`), `SubscriptionTests`, `RewardsTests`, `PowerUpTests`, `ReviveTests`, `GemCoinEconomyTests` (dotnet). Planned: billing sandbox tests, restore, refund, age gate. Note: `ContentRepositoryTests` still asserts `CoinsPerGem == 100` (stale, doc 27 RC 1).

## 19. Tasks (status)

| ID | Task | State |
|---|---|---|
| T-SHOP-1 | Requirements document and pointers | Done (this file, corrected 2026-11-01) |
| T-SHOP-2 | Pack data, schemas, generator, validator | Done |
| T-SHOP-3 | Shop screen (5 tabs), test purchases | Done |
| T-SHOP-4 | Manifest regeneration, compile, tests | Ongoing per build |
| T-SHOP-5 | Remote config wiring | Planned |
| T-SHOP-6 | Analytics events | Planned |
| T-SHOP-7 | Map entry and "Get gems" routes | Done (Gems tab); highlight Planned |
| T-SHOP-8 | Settings > Purchases | Planned |
| T-SHOP-9 | Real billing, Google Play (9 gem SKUs, 2 subscriptions) | Planned |
| T-SHOP-10 | Server validation, refund revoke, anti-fraud | Planned |
| T-SHOP-11 | Power-ups tab | Done (7 types) |
| T-SHOP-12 | Characters in the shop | Coming soon |
| T-SHOP-13 | Gear tab | **Removed (no gear)** |
| T-SHOP-14 | Chests (Iron, Gold) | Done (no pity, no server roll yet) |
| T-SHOP-15 | VIP subscription | **Superseded by the Subscription page (Done, test purchase)** |
| T-SHOP-16 | Battle pass | **Removed** |
| T-SHOP-17 | Starter pack, daily deal, limited offers | Starter Planned; the rest Removed; Bundles Done |
| T-SHOP-18 | Age gate, parental controls, compliance | Planned |
| T-SHOP-19 | Localisation | Planned |
| T-SHOP-20 | iOS StoreKit | Planned |

## 20. Risks

Free rewards out-earn the packs (doc 27 findings 1 to 3); subscription undercuts packs per gem; release has no ads or billing until keys and products exist; paid random items (chests) need regional checks; stale values in `remote_config.json` and tests (doc 27 RC 1).

## 21. Open questions

Tracked in **doc 27 section 18** and doc 22's open items (promo prices, badge assignment, first-purchase flag server side, tab behaviour). Not planned unless the owner asks: starter pack beyond doc 22 point 7, daily deal, limited-time offers, battle pass, VIP tiers, gear.
