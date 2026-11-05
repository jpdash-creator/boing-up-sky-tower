# 07 - Roadmap, Risks and Open Items

Solo / tiny team, lean budget. Estimates are ranges for one full-time developer with heavy AI assistance; adjust once you start.

## Status (2026-10-20)
The Phase 1 prototype and a large part of the Phase 2 meta layer exist as a **pre-release Android build (v0.9.x, currently v0.9.3.x)** on the owner's OnePlus 13: **5 regions x 15 levels (75 levels) are playable end to end** (Boot, menu, scrollable world map, level screen, run, results, stars and rewards, settings, pause), offline, with all 75 levels proven solvable by the bot. Done in the 5-region milestone: deterministic simulation and generator, three-button controls, auto-scroll model, economy v2, local save and wallet, 2 fonts, placeholder audio (14 CC0 sound effects + 1 loop), the Android test kit and build versioning. Two days of phone testing (ten feedback lists, 2026-10-19/20) drove the changes recorded in doc 08 and doc 09. Test counts at the time of writing: dotnet 97, Unity EditMode 72 (84 once the endless tests land), Unity PlayMode 13.
In progress: endless mode (Endless button on each region's level screen), AltTester on top of the Android kit, the IAP and ads plan, the screen-by-screen polish pass (T20), platform art for regions 2-5 (owner picks). Shop: requirements written (doc 14), power-ups/chests/VIP/characters/inventory requirements written (doc 15, nothing built) and the Tab 4 screen (gems and coins, test purchases in development builds) added on 2026-10-20; tabs 1 to 3 and real billing are not started. Not started: backend (PlayFab), real IAP and ads, leaderboards, regions 6+, iOS. Open owner items: doc 09 section F.

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
- Character rig with Momo (no gear layers).
- **Gate**: playtest 10 people; "just one more run" test (median 5+ runs voluntary). *(Update 2026-10-20: the first owner device tests ran on 2026-10-19/20; the 10-person playtest is still pending. Physics tuning T9 is blocked on hands-on playtests.)*

## Phase 2 - Meta and economy (5-6 weeks)
- Meta UI: map, characters, inventory, shop, daily rewards, quests.
- Coins/gems wallet (server-authoritative), PlayFab integration, auth, cloud save.
- Leaderboards (plausibility validation), analytics funnels.
- IAP (gem packs, remove ads), rewarded ads, consent flows.

## Phase 3 - Content factory (6-8 weeks, overlaps)
- Asset pipeline scripts, AI recipes, normalizer, atlas + Addressables.
- Produce regions 2-8 first, with cosmetics for 16 characters (staged), power-ups.
- Content CLI and CI validation; bot solvability checks.

## Phase 4 - Content to launch scope (6-8 weeks)
- **Checkpoint:** decide here whether to keep 20 live regions or fall back to 8-10 (see MVP section). Build the signature modules for all 30 regions before v1.0 if the schedule allows (doc 08 section 5).
- Regions 9-20, teaser art for 21-30, remaining characters, audio, localization (first 5 languages).
- Battle pass, VIP subscription, chests + odds UI, starter pack, limited offers.
- Polish, accessibility, performance pass, low-end device tuning.

## Phase 5 - Soft launch (4-6 weeks)
- Closed testing -> soft launch in a few countries; measure KPIs (doc 05).
- Fix funnel leaks; tune economy using Remote Config; add anti-cheat re-simulation.
- Go/no-go for global launch: KPIs hit and crash-free 99.5 percent.

## Phase 6 - Global launch & live-ops (ongoing)
- Marketing push (creators, ASO, social clips), referral loop.
- Region 21 unlock event at ~week 3; then one per 2-3 weeks. Seasonal battle pass.
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
| Economy imbalance / pay-to-win perception | Retention, reviews | Cosmetic-only items, Ranked mode, soft-launch tuning |
| Cheating on leaderboards | Trust | Deterministic replays, server validation, flags, shadow bans |
| iOS build needs a Mac | Schedule | Cloud Mac or CI macOS runners from Phase 0 |
| Scope creep (30 regions x 15 levels) | Delays | Procedural levels + bot validation; launch with fewer live regions |
| Single-developer bus factor | Continuity | Documentation, CLI automation, CI |
| Discoverability (viral not guaranteed) | Growth | Soft launch gates, creator seeding, share cards, ASO |
| Policy changes (stores, SDKs, MCP availability) | Rework | Abstraction layers around IAP, ads and backend |
| App size growth | Installs | On-demand region bundles, ASTC, audio compression |
| Gem inflow too high under economy v2 (about 10 gems per level), gem farming through 2-star replays | Gem IAP sales, scarcity | Economy check in the game repo `Tests/Simulation/ECONOMY_REPORT.md`; owner decision pending (doc 09 section F) |
| Shop economy conflicts (coin packs before a coin sink, gem price rate vs doc 08 anchor, promo reference-price rules): see doc 14 section 5.5 | Players waste gems, items cost more real money than planned, store or consumer-law complaint | Owner decisions Q1 to Q3, Q8 in doc 14; remote flags for coins section and promo |
| Power-ups, characters, VIP tiers change gameplay in Casual runs (rule "cosmetics only" vs owner decisions), bigger jump could break layouts | Ranked fairness, level solvability | Doc 15 section 1.1 X-1 (ranked neutral) and section 2 (jump cap +25 percent = 5.64 u, hard clamp 6.5 u, bot proof at max bonus, 5-region interaction matrix) |
| Placeholder or unverified art (platform sets for regions 2-5, map segments, popup frame) | Visual quality | Owner verifies picks; every OpenArt batch needs an approval packet (credit guardrail) |

## Open items I could still clarify later (not blockers)
1. Mascot approval (placeholder: Momo). Game name is **Boing Up: Sky Tower** (chosen); still run trademark and app-store availability checks.
2. Target launch countries and languages beyond the first five.
3. ~~Whether to start with 20 live regions or the leaner 8-10 launch~~ Decided: 20, with 8-10 as fallback at the Phase 4 checkpoint.
4. Mac access for iOS builds (own, cloud or CI).
5. Monthly tooling budget (Unity, PlayFab free tier limits, CDN, image generation credits, ad mediation).
6. Whether subscriptions use RevenueCat or native + PlayFab validation.

## Next steps (when you are ready to build)
1. Approve or edit the decisions in `00-README.md`.
2. I generate the style bible and Momo concepts with the OpenArt MCP.
3. I scaffold the Unity project structure, `CLAUDE.md`, content schemas and the content CLI.
4. Then Phase 1: the playable prototype.
