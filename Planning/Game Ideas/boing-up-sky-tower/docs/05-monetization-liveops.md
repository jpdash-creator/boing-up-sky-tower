# 05 - Monetization and Live-Ops

> **2026-11-01 update (economy re-check against the code, build v0.38.0.40):** this document was rewritten to match what the game sells today. The numbers (gems, coins and dollars for every item) are in [27-economy-tables-from-code.md](27-economy-tables-from-code.md); docs 20, 22 and 26 are the owner sources and win over this file; [08-rules-addendum.md](08-rules-addendum.md) section 8 holds the rate card. Rules now in force: **1 gem = 20 coins (fixed)**, **no gear** (removed 2026-10-24, doc 20), **no VIP tiers** (a Weekly and a Monthly Subscription instead), Iron and Gold chests, revive 5 then 10 gems. Earlier plans for a battle pass, Remove Ads, a starter pack, interstitials and a daily login streak are kept below only as **planned, not built**.

## 1. Revenue streams
| Stream | Role | State | Notes |
|---|---|---|---|
| Gem packs (IAP, consumable) | Core | **Built (test purchase in dev builds; release shows "Not available yet" until billing)** | 9 packs, 50 to 15,000 gems, $0.79 to $119.99 (INR prices too); launch promo (about half price) and first-purchase double exist but are **OFF** by default |
| Subscription | Recurring | **Built (same billing state)** | Weekly $1.49 (Rs 149) and Monthly $4.99 (Rs 449); instant gems, daily gems and coins, free power-ups on the monthly plan; replaces the VIP tiers |
| Gem spending: bundles, chests, power-ups, coin packs, skips, revives | Sinks | **Built** | Everything except the packs and subscriptions is bought with gems or coins; bundles give 1.4 to 1.8 times their gem price in value, chests 0.81 to 0.86 times |
| Rewarded ads | Free-user monetization | **Revive only (first revive of a run)**; LevelPlay test ads, app key and ad unit are placeholders | Double result, daily power-up ad and gem ads are planned, not built |
| Starter pack | Conversion | Planned, not built | Doc 22 point 7: $0.99 = 100 gems + 2,000 coins, once per account |
| Limited-time offers | Spike | Planned, not built | The timer bundle (30 percent off, 5 minutes, gems) is the only timed offer in the game |
| Battle pass, Remove Ads, interstitials, daily login streak | - | **Not built, not in docs 20 to 26** | Re-open only by owner decision |

Realistic revenue mix with only the revive ad built: ads about 5 to 12 percent of ARPDAU (roughly $0.003 to $0.007 of about $0.06), IAP the rest. The old target of "ads 50-60 percent" needs 2.5 rewarded views per player per day at $10 eCPM, or high-eCPM traffic, and more placements than exist today (doc 27 section 16). Tune using soft launch data.

## 2. Chest compliance (important)
- Two chests only: **Iron (100 gems) and Gold (300 gems), gems only, open 1 or open 10**. Contents are shown as text on the card and in the (i) popup (Iron: 1 power-up and 400 to 600 coins; Gold: 3 power-ups and 1,000 to 1,400 coins; each power-up one of the 7 types with equal chance). Characters in chests are switched off.
- No rarity tiers, pity counter or duplicate system exists while characters are off. Before characters are switched on, the odds list (doc 22: Iron Uncommon 67 / Rare 33, Gold Rare 57 / Epic 29 / Legendary 14, duplicates refund 25 percent as gems) must be shown before purchase (Apple guideline 3.1.1, Google Play policy) and the price/odds must be fixed (an average character would return 276 to 286 percent of the chest price, doc 22 open item 1).
- Server-side RNG later (the roll is already seeded by the open index and recomputable). No paid-only chance mechanic for leaderboard-affecting items; power-ups are Casual only, Ranked stays neutral.
- Region restrictions: some jurisdictions restrict paid random items (for example Belgium, Netherlands, China rules). Use a Remote Config flag to disable gem chests per country; bundles and power-ups are direct purchases. **(verify with a lawyer before launch)**
- Rating: expect 12+ / Teen for in-app purchases and simulated random rewards. Do not target children under 13.

## 3. Pricing strategy
- Use platform price tiers; the gem pack ladder improves in gems per dollar at every step from the 100 pack up (63 to 125 gems per $); badges Popular on the 100 pack and Best value on the 1,000 pack.
- A/B test via Remote Config: pack amounts, promo and first-purchase double switches, chest contents, bundle value band, region skip prices, daily and weekly reward sizes.
- Do NOT sell leaderboard-affecting stats. Power-ups are limited to Casual mode.
- Gem scarcity: free gem pickups are modest (about 51 to 54 gems per region at 60 percent pickup) but the Rewards screen adds Main 580 gems once, Daily up to 45 gems a day and Weekly 160 gems a week; the Subscription is the cheapest gem source per dollar (232 to 354 gem-equivalents per $ against 63 to 125 in the packs). Never freeze progression: coins and skill alone complete all 15 levels in a region.

## 4. Store setup checklist
- Google Play Console: app, in-app products (9 consumable gem packs; product ids = `sku` in `gem_packs.json`), subscriptions (Weekly and Monthly base plans), licence testers, Data Safety, content rating, Play Integrity, closed -> open testing -> production.
- App Store Connect (iOS later): app, IAP (consumable, auto-renewable subscription group), StoreKit testing config, sandbox users, privacy nutrition labels, Sign in with Apple if other social logins are offered, App Tracking Transparency text, age rating, TestFlight.
- Developer accounts: Google ~one-time $25, Apple ~$99 per year **(verify)**. Platform fees typically 15 percent for small developers' first $1M and 30 percent otherwise **(verify current programs)**.
- Required pages: Privacy Policy, Terms, Support URL, account-deletion URL, licence attribution.

## 5. Retention loops
- Session loop (60 s): play, earn coins, see progress bar to next unlock, replay.
- **Daily loop (built, Rewards screen, Daily tab):** 10 actions of 10 points (log in, play 5 minutes, complete 1 or 3 levels, combo of 3, a hard level, the no-wall level, a gem level, 2 stars in a level, use a power-up); milestones at 20 / 40 / 60 / 80 / 100 points pay 10 gems, 500 coins, 15 gems, 1,000 coins, 20 gems + 1 random power-up. Resets at local midnight. Subscribers also claim their daily gems and coins.
- **Weekly loop (built, Weekly tab):** 11 tasks (log in 7 days, play 100 / 150 minutes, combos 5 / 10 / 25 / 50, clear 15 levels, earn 20 stars, no-wall level, 3 stars on 5 levels) worth 160 gems, 1,800 coins and 1 random power-up. Resets Monday local time.
- **Main loop (built, Main tab):** star milestones at 25 to 225 stars and one reward per region cleared (50 gems + 2,000 coins).
- Planned, not built: weekly leaderboard rewards, season loop / battle pass, collection loop for characters (characters are coming soon).

## 6. Virality features
- Share card: score + character + region background rendered to an image with a deep link and referral code.
- Referral reward: both parties get gems (cap per account; fraud checks).
- Short replay clips (last 10 s) exported as video/GIF (P2).
- Friend challenge links: "beat my 312 floors in Candy Canyon".
- Creator codes / UGC friendly: Hall of Fame for top players.
- Launch tactics: TikTok/Reels/Shorts clips, creator seeding, ASO (keywords, localized screenshots), soft launch in a few countries, waitlist for locked region 21+ ("notify me" feeds push list).

## 7. Live-ops calendar (example, per 8-week season; planned, nothing here is built)
| Week | Content |
|---|---|
| 1 | New region unlock (from the teaser list), launch offer (timer bundles, promo switch) |
| 2 | Weekend event: double coins; daily challenge variants |
| 3 | New characters (when characters launch; chest odds list first) |
| 4 | Mid-season limited offer; community leaderboard tournament |
| 5 | New power-up or platform kind; bundle refresh |
| 6 | Event region with unique modifier |
| 7 | "Last chance" Subscription or bundle offer |
| 8 | Season finale tournament; teaser for the next season |

Cadence target after launch: 1 region every 2-3 weeks (stretching to a season-aligned cadence). The asset factory (doc 04) is built to sustain this with a solo team.

## 8. KPIs and soft-launch gates
| KPI | Target to scale marketing |
|---|---|
| D1 retention | 38 percent or better |
| D7 retention | 15 percent or better |
| D30 retention | 5 percent or better |
| Session length | 4-8 min across 3-5 sessions per day |
| ARPDAU | $0.10 or better (base model with only the revive ad: about $0.06; about $0.08 with 2.5 rewarded views a day; doc 27 section 16.3) |
| Ads share of ARPDAU | realistic 5 to 12 percent today, up to about 30 percent with more placements; do not plan on 50 percent |
| Payer conversion | 2 percent or better |
| Ad rewarded opt-in | 30 percent or better (of revive offers) |
| Median gem balance after day 7 | 100 to 400 (too high: Daily/Weekly rewards too rich; too low: faucet too low) |
| Subscription share of payers | below 50 percent (the Subscription is the best value per dollar) |
| Crash-free | 99.5 percent or better |
| CPI vs LTV | LTV at D90 > 1.3 x CPI |
(Benchmarks are rule-of-thumb; confirm for the genre.) Soft launch in e.g. Philippines/Canada/Australia/Poland **(choose based on cost and similarity)**, 4-6 weeks, then iterate before global launch.

## 9. Analytics event plan
`app_open, ftue_step, run_start, run_end(region, level, floor, score, duration), combo_peak, revive_offer, revive_taken(type, price), ad_impression, iap_view, iap_start, iap_success, shop_view, chest_open(type, count, gems spent, contents), chest_odds_viewed, gem_pack_bought(pack, first-purchase flag), coin_pack_bought, bundle_view, bundle_bought, subscription_view, subscription_started, subscription_claim, reward_claimed(tab, id), powerup_bought, powerup_used, region_unlock(method), leaderboard_view, share`. Every monetization surface logs view -> click -> purchase. **No analytics layer exists in the code yet** (doc 22 open item 16), so none of these events is emitted today.
