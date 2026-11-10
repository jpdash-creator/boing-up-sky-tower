# 07 - Roadmap, Risks and Open Items

> **2026-11-01 update (economy re-check against the code, build v0.38.0.40):** the status, phases and risk rows that touch the economy were corrected. Economy numbers are in [27-economy-tables-from-code.md](27-economy-tables-from-code.md) (source of truth, with docs 20, 22, 26). Facts: 1 gem = 20 coins (fixed), **no gear** (removed 2026-10-24, doc 20), Iron/Gold chests, a Weekly/Monthly Subscription instead of VIP tiers, 2 characters (free commons), only the revive ad built, billing and server not started.

Solo / tiny team, lean budget. Estimates are ranges for one full-time developer with heavy AI assistance; adjust once you start.

## Status (2026-11-01)
Build **v0.38.0.40** (Android). Since the 2026-10-20 status below the owner issue lists 23 to 28 added: the Treasure Chest screen (Iron/Gold, gems only), the shop with five tabs (Bundles, Chests, Power-ups, Coins, Gems) and a Subscription button, 1,000 generated gem bundles with a refresh and a timer bundle, the Rewards screen (Main, Daily, Weekly), seven power-ups with stacking durations, two characters (Momo, Sporeling) with the character screen, the free ad revive (LevelPlay test ads), random layouts per attempt, the level structure of doc 25 and new region 1 platforms (doc 24). Gear, the inventory screen and the wardrobe were removed (doc 20). Still not started: real billing, server wallet (PlayFab), analytics layer, leaderboards, regions 6+, iOS, characters in chests and the character shop.

## Status (2026-10-20, history)
The Phase 1 prototype and a large part of the Phase 2 meta layer exist as a **pre-release Android build (v0.9.x, currently v0.9.3.x)** on the owner's OnePlus 13: **5 regions x 15 levels (75 levels) are playable end to end** (Boot, menu, scrollable world map, level screen, run, results, stars and rewards, settings, pause), offline, with all 75 levels proven solvable by the bot. Done in the 5-region milestone: deterministic simulation and generator, three-button controls, auto-scroll model, economy v2, local save and wallet, 2 fonts, placeholder audio (14 CC0 sound effects + 1 loop), the Android test kit and build versioning. Two days of phone testing (ten feedback lists, 2026-10-19/20) drove the changes recorded in doc 08 and doc 09. Test counts at the time of writing: dotnet 97, Unity EditMode 72 (84 once the endless tests land), Unity PlayMode 13.
In progress: endless mode (Endless button on each region's level screen), AltTester on top of the Android kit, the IAP and ads plan, the screen-by-screen polish pass (T20), platform art for regions 2-5 (owner picks). Shop: requirements written (doc 14), power-ups/chests/characters requirements written (doc 15) and the first gem/coin screen with test purchases added on 2026-10-20 (later replaced by the docs 22/26 shop). Not started then: backend (PlayFab), real IAP and ads, leaderboards, regions 6+, iOS. Open owner items: doc 09 section F.

## Phase 0 - Foundations (2-3 weeks)
- Names, trademark search (game name, mascot), bundle IDs, Apple/Google accounts, Unity licence.
- Style bible + Momo concept; final art direction sign-off.
- Unity project, repo + LFS, CI skeleton, `CLAUDE.md`, content schemas.
- **Exit**: a blank build runs on a real Android device and via TestFlight/iOS. *(Android done 2026-10-20; iOS needs a Mac, still open.)*

## Phase 1 - Core prototype (4-5 weeks)
- First: choose physics baselines (gravity, accel, maxSpeed, jumpBase, jumpSpeedFactor, wallBoost) in a spreadsheet, tune in the prototype, then freeze them for the generator and solvability bot (doc 09 D4).
- Deterministic controller (fixed-point or integer sim), platforms, scroll, combo, camera, juice, placeholder art.
- Shared authored-chunk library (about 12 chunks per class) for L5/L10/L15.
- Chunk generator + 1 region (Meadow Springs) with 15 levels and endless mode.
- Character rig with Momo (parts drawn back to front; no gear, doc 20).
- **Gate**: playtest 10 people; "just one more run" test (median 5+ runs voluntary). *(Update 2026-10-20: the first owner device tests ran on 2026-10-19/20; the 10-person playtest is still pending. Physics tuning T9 is blocked on hands-on playtests.)*

## Phase 2 - Meta and economy (5-6 weeks)
- Meta UI: map, shop, Rewards screen (Main, Daily, Weekly), characters.
- Coins/gems wallet (server-authoritative), PlayFab integration, auth, cloud save.
- Leaderboards (plausibility validation), analytics funnels.
- IAP (9 gem packs, Weekly and Monthly Subscription), rewarded ads (revive built with test ads), consent flows.

## Phase 3 - Content factory (6-8 weeks, overlaps)
- Asset pipeline scripts, AI recipes, normalizer, atlas + Addressables.
- Produce regions 2-8 first, with characters (one free common per region, others by gems; staged), power-ups.
- Content CLI and CI validation; bot solvability checks.

## Phase 4 - Content to launch scope (6-8 weeks)
- **Checkpoint:** decide here whether to keep 20 live regions or fall back to 8-10 (see MVP section). Build the signature modules for all 30 regions before v1.0 if the schedule allows (doc 08 section 5).
- Regions 9-20, teaser art for 21-30, remaining characters, audio, localization (first 5 languages).
- Chest odds UI (before characters join the chests), starter pack, limited offers. (Battle pass and Remove Ads are not planned; the Subscription is built against a test purchase.)
- Polish, accessibility, performance pass, low-end device tuning.

## Phase 5 - Soft launch (4-6 weeks)
- Closed testing -> soft launch in a few countries; measure KPIs (doc 05).
- Fix funnel leaks; tune economy using Remote Config; add anti-cheat re-simulation.
- Go/no-go for global launch: KPIs hit and crash-free 99.5 percent.

## Phase 6 - Global launch & live-ops (ongoing)
- Marketing push (creators, ASO, social clips), referral loop.
- Region 21 unlock event at ~week 3; then one per 2-3 weeks.
- Expand: friends/ghost races, more languages, tablet polish, new hazard modules.

**Total to global launch (solo, lean): about 7-10 months.** A small team of 3-6 could compress this to 5-7 months. If you want faster validation, **cut launch scope to 8 live regions** and unlock the rest as live-ops, since the data model supports it.

## MVP vs. full scope suggestion
**Decision (D11):** 20 live regions is the launch target. The 8-10 region launch below is the fallback, decided at the Phase 4 checkpoint.

Because the budget is lean, I recommend launching with fewer *playable* regions (8 to 10) while keeping the 30-region map visible. This gets real retention data months earlier and lets sales fund the remaining regions. The architecture does not change if you keep 20; it just adds about 3 months of content work.

## Risk register
| Risk | Impact | Mitigation |
|---|---|---|
| AI art inconsistency across hundreds of items | Brand and quality | Style bible, locked model + reference, template alignment checks, human curation, hand-polish hero assets |
| Trademark/IP closeness to existing jumper games | Takedown | Original name, mascot, world; legal check before launch |
| Copyright status of AI-generated art | Ownership | Check generator terms; keep prompt logs; hand-modify key assets |
| Gacha regulation | Store rejection, fines | Odds display, pity, per-country toggle, legal review |
| Economy imbalance / pay-to-win perception | Retention, reviews | Ranked mode stays neutral (no power-ups), no gear exists, soft-launch tuning with the gem ledger by source and sink |
| Cheating on leaderboards | Trust | Deterministic replays, server validation, flags, shadow bans |
| iOS build needs a Mac | Schedule | Cloud Mac or CI macOS runners from Phase 0 |
| Scope creep (30 regions x 15 levels) | Delays | Procedural levels + bot validation; launch with fewer live regions |
| Single-developer bus factor | Continuity | Documentation, CLI automation, CI |
| Discoverability (viral not guaranteed) | Growth | Soft launch gates, creator seeding, share cards, ASO |
| Policy changes (stores, SDKs, MCP availability) | Rework | Abstraction layers around IAP, ads and backend |
| App size growth | Installs | On-demand region bundles, ASTC, audio compression |
| Free gem and coin flow too high: Rewards screen gives Daily up to 45 gems + 1,500 coins + 1 random power-up a day and Weekly 160 gems; pickups give only about 260 gems in the campaign (doc 27 sections 2, 11 to 14); coin and gem farming through 1 and 2 star replays (full pay below 3 stars) | Gem IAP sales, scarcity | Doc 27 recommendations RC 2 to RC 4 and RC 9; `Tests/Simulation/ECONOMY_TABLES.md`; owner decision pending (doc 09 section G) |
| Stale economy constants fixed in RC 1 (2026-11-01: `coinsPerGem` 20, `vipGemsPerDay` and per-level `replayCoinPercent` deleted; the Unity manifest hash rebuild is pending) and the promo "list price" reference-price rules | Wrong tooling numbers, store or consumer-law complaint | Doc 27 RC 1 and RC 11; launch promo and first-purchase double are OFF by default; legal read before they are switched on |
| Power-ups (7 types, stackable durations) and later character powers change gameplay in Casual runs | Ranked fairness, level solvability | Ranked stays neutral; every level is bot-proven without help; the 7 power-ups are tested per region (`PowerUpTests`, `PowerUpStackTests`); doc 15 section 1.1 X-1 |
| Placeholder or unverified art (platform sets for regions 2-5, map segments, popup frame) | Visual quality | Owner verifies picks; every OpenArt batch needs an approval packet (credit guardrail) |
| Subscription cheaper per gem than any pack (232 to 354 gem-equivalents per $ vs 63 to 125) | Most payers pick the $4.99 plan, low ARPPU | Doc 27 RC 6; watch subscription share of payers |
| Ads limited to the revive ad (about $0.003 to $0.007 ARPDAU); LevelPlay app key and ad unit are placeholders | Ad revenue far below the 50-60 percent mix of the old plan | Doc 27 RC 10; owner supplies the keys (doc 22 open item 10) |

## Open items I could still clarify later (not blockers)
1. Mascot approval (placeholder: Momo). Game name is **Fluffy Climbers: Tower Jump** (chosen); still run trademark and app-store availability checks.
2. Target launch countries and languages beyond the first five.
3. ~~Whether to start with 20 live regions or the leaner 8-10 launch~~ Decided: 20, with 8-10 as fallback at the Phase 4 checkpoint.
4. Mac access for iOS builds (own, cloud or CI).
5. Monthly tooling budget (Unity, PlayFab free tier limits, CDN, image generation credits, ad mediation).
6. Whether subscriptions use RevenueCat or native + PlayFab validation.
7. Economy questions from doc 27 section 18 (`coinsPerGem` fixed in RC 1, Daily/Weekly reward size, subscription price vs packs, power-up repricing by duration, replay farming below 3 stars, promo and first-purchase double, Main rewards scope, which old docs to archive): see doc 09 section G.

## Next steps (when you are ready to build)
1. Approve or edit the decisions in `00-README.md`.
2. I generate the style bible and Momo concepts with the OpenArt MCP.
3. I scaffold the Unity project structure, `CLAUDE.md`, content schemas and the content CLI.
4. Then Phase 1: the playable prototype.
