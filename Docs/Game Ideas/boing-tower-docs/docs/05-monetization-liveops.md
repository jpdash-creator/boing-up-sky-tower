# 05 - Monetization and Live-Ops

> Gem pack amounts, item prices and the coins : gems : USD ratio are defined in [08-rules-addendum.md](08-rules-addendum.md) section 8, which supersedes the numbers in doc 02.

> **Update 2026-11-01 (owner):** no gear anywhere (starter pack, VIP extras, live-ops calendar and collection loop rewritten); 1 gem = 20 coins, fixed. Prices in doc 08 section 8 and doc 14.

> **Update 2026-10-20 (economy v2, doc 08):** gems now come from collectible gem pickups with rarity tiers (1/2/5/10 gems) instead of the near-zero campaign income assumed below, so gems are less scarce than this document and doc 08 section 8 assume. The owner review of gem scarcity vs. gem pack sales is open (doc 09). Revive costs 2 gems, then 5 (max 2 per run). The scroll-difficulty setting raises the number of gem spots from 5 to 10 percent of former coin spots; selling extra gem chance as an in-app purchase is an idea recorded for later. The IAP and ads implementation plan lives in the game repo docs (doc 13, in preparation).

## 1. Revenue streams
| Stream | Role | Notes |
|---|---|---|
| Gem packs (IAP, consumable) | Core | 7 tiers (10 to 1000 gems, doc 14), localized price; coin packs are bought with gems at the fixed rate 1 gem = 20 coins |
| Starter pack | Conversion | One-time ~$1.99-4.99: 100 gems + 3,000 coins + 2 Rare + 1 Epic Power-up Chest + ad-free day (no gear); shown after first session ends |
| Limited offers | Spike | Timed (24-72 h) bundles tied to events or new regions |
| Battle pass | Recurring | Premium $4.99 per 8-week season at launch (test $9.99 with a richer track), 50 tiers, free track too |
| VIP subscription | Recurring | Weekly ~$2.99 / monthly ~$7.99 (auto-renewing): 25 gems daily at launch (A/B test up to 40, per doc 08), no interstitials, +50 percent coins, VIP badge / profile frame / victory animation (no gear) |
| Remove Ads | One-time | ~$3.99, non-consumable; included in VIP |
| Mystery chests | Sink + chance | Gems or ad-watch (limited free) |
| Rewarded ads | Free-user monetization | Revive, double rewards, free chest |
| Interstitials | Optional | Only after level end, cap 1 per 3 min, none in first 3 sessions |

Target mix (typical casual-arcade): ads 50-60 percent, IAP 40-50 percent early. Tune using soft launch data.

## 2. Gacha / mystery chest compliance (important)
- Publish odds in-app and in the store listing where required (Apple guideline 3.1.1, Google Play policy).
- Use server-side RNG, pity counter (e.g. guaranteed Epic every 10 pulls), no paid-only chance mechanic for leaderboard-affecting items (characters and consumables only; no gear).
- Region restrictions: some jurisdictions restrict paid random items (e.g. Belgium, Netherlands, China rules). Use a Remote Config flag to disable gem-chests per country; offer direct-purchase alternatives. **(verify with a lawyer before launch)**
- Rating: expect 12+ / Teen for in-app purchases and simulated random rewards. Do not target children under 13.

## 3. Pricing strategy
- Use platform price tiers; test anchoring (best-value highlight on Sack or Chest).
- A/B test via Remote Config: gem amounts, starter pack contents, region skip prices, chest odds.
- Do NOT sell leaderboard-affecting stats. Consumables are limited to Casual mode.
- "Gems not easily available": the faucets in the design doc keep gems scarce, but never freeze progression. Coins and skill alone must be able to complete all 15 levels in a region.

## 4. Store setup checklist
- Google Play Console: app, in-app products (consumable gem packs, non-consumable Remove Ads), subscriptions (base plans/offers), licence testers, Data Safety, content rating, Play Integrity, closed -> open testing -> production.
- App Store Connect: app, IAP (consumable, non-consumable, auto-renewable subscription group), StoreKit testing config, sandbox users, privacy nutrition labels, Sign in with Apple if other social logins are offered, App Tracking Transparency text, age rating, TestFlight.
- Developer accounts: Google ~one-time $25, Apple ~$99 per year **(verify)**. Platform fees typically 15 percent for small developers' first $1M and 30 percent otherwise **(verify current programs)**.
- Required pages: Privacy Policy, Terms, Support URL, account-deletion URL, licence attribution.

## 5. Retention loops
- Session loop (60 s): play, earn coins, see progress bar to next unlock, replay.
- Daily loop: login reward, 3 quests, daily challenge, free chest timer (4-8 h).
- Weekly loop: weekly leaderboard reset with rewards (Power-up Chests + gems for top N percent).
- Season loop (8 weeks): battle pass, themed regions, limited characters and effects.
- Collection loop: characters (20), rarity, character completion percent, "missing piece" nudges. (No gear: removed 2026-11-01.)

## 6. Virality features
- Share card: score + character + region background rendered to an image with a deep link and referral code.
- Referral reward: both parties get gems (cap per account; fraud checks).
- Short replay clips (last 10 s) exported as video/GIF (P2).
- Friend challenge links: "beat my 312 floors in Candy Canyon".
- Creator codes / UGC friendly: cosmetic Hall of Fame for top players.
- Launch tactics: TikTok/Reels/Shorts clips, creator seeding, ASO (keywords, localized screenshots), soft launch in a few countries, waitlist for locked region 21+ ("notify me" feeds push list).

## 7. Live-ops calendar (example, per 8-week season)
| Week | Content |
|---|---|
| 1 | Season start: new battle pass, new region unlock (from the teaser list), launch offer |
| 2 | Weekend event: double coins; daily challenge variants |
| 3 | New characters via chest |
| 4 | Mid-season limited offer; community leaderboard tournament |
| 5 | New character / event character; Power-up Chest drop |
| 6 | Event region with unique modifier |
| 7 | "Last chance" pass catch-up offer |
| 8 | Season finale tournament; teaser for the next season |

Cadence target after launch: 1 region every 2-3 weeks (stretching to a season-aligned cadence), a new character or effect every 1-2 weeks. The asset factory (doc 04) is built to sustain this with a solo team.

## 8. KPIs and soft-launch gates
| KPI | Target to scale marketing |
|---|---|
| D1 retention | 38 percent or better |
| D7 retention | 15 percent or better |
| D30 retention | 5 percent or better |
| Session length | 4-8 min across 3-5 sessions per day |
| ARPDAU | $0.10 or better |
| Payer conversion | 2 percent or better |
| Ad rewarded opt-in | 30 percent or better |
| Crash-free | 99.5 percent or better |
| CPI vs LTV | LTV at D90 > 1.3 x CPI |
(Benchmarks are rule-of-thumb; confirm for the genre.) Soft launch in e.g. Philippines/Canada/Australia/Poland **(choose based on cost and similarity)**, 4-6 weeks, then iterate before global launch.

## 9. Analytics event plan
`app_open, ftue_step, run_start, run_end(region, level, floor, score, duration), combo_peak, revive_offer, ad_impression, iap_view, iap_start, iap_success, shop_view, item_equip, chest_open, quest_complete, region_unlock(method), leaderboard_view, share`. Every monetization surface logs view -> click -> purchase.
