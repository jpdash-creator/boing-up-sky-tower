# 01 - Requirements

Priority: **P0** = launch blocker, **P1** = needed for soft launch, **P2** = post-launch live-ops.

## 1. Functional requirements

### 1.1 Core gameplay (P0)
- FR-G1 Three always-visible on-screen buttons: Left and Right (bottom-left), Jump (bottom-right); the left-handed option swaps the sides. Jump height scales with horizontal speed. *(Update 2026-10-19: replaces the original touch zones, which the owner could not understand on device.)*
- FR-G2 Wall bounce that preserves momentum; ceiling-free vertical tower.
- FR-G3 Combo system: a jump that passes 2 or more floors starts or extends a combo; the combo ends on a landing without enough height (no timer). Floor = platform row index.
- FR-G4 Auto-scroll: after the first jump the screen scrolls up (platforms and background slide down) and the speed steps up every 5 s; falling below the bottom edge of the screen = fail. No hazard comes from below (doc 08, scroll model). *(Update 2026-10-20: replaces the earlier rising-danger wording.)*
- FR-G5 Platforms: normal, narrow, moving, crumbling, bouncy, icy/slippery, one-way; per-region special platforms.
- FR-G6 Pickups: stars (3 per level), gems (rarity tiers), power-up orbs. Coins are no longer collected in a level; they are paid at the end (economy v2, doc 08). *(Update 2026-10-20.)*
- FR-G7 Hazards: per-region (wind, falling rocks, lava, lasers, ghosts, etc.).
- FR-G8 Deterministic seeded generation so a run can be re-simulated for validation and "daily challenge".
- FR-G9 Pause, resume after phone call/app switch, 60 fps target, 30 fps floor on low-end devices.

### 1.2 Progression structure (P0)
- FR-P1 Regions: 20 live at launch (the current milestone has 5 playable regions, the rest of the world map shows "Coming soon"), 10 additional shown as locked teasers ("Coming soon" with silhouette art and a countdown or notify-me).
- FR-P2 Each region has 15 campaign levels. Levels 1-14 are goal-based climbs; level 15 is the region finale (boss/hazard gauntlet).
- FR-P3 Each level has 3 collectible stars (side routes and risky spots); the level's grade is the number collected. Stars unlock bonus content and feed region completion rewards. There is no time star and no timed goal. *(Update 2026-10-20.)*
- FR-P4 Endless mode per region (the same tileset and hazards, infinite height). Feeds leaderboards. Entry: an Endless button on each region's level screen (owner decision 2026-10-20; implementation in progress).
- FR-P5 Region unlock: finish level 15 of the previous region, or pay gems to unlock early (price rises with region index).
- FR-P6 Locked teaser regions cannot be entered. They display name, art silhouette and an unlock-date or "notify me".

### 1.3 Characters and customization (P0/P1)
- FR-C1 Default mascot is free and is the game's brand icon.
- FR-C2 Players can own multiple characters (full skins).
- FR-C3 *Removed 2026-11-01 (no gear; docs 20 and 27): equipment slots (outfit, boots, hat, accessory) no longer exist. A character's light ribbon (trail) is part of the character.*
- FR-C4 Character rarity tiers: Common, Uncommon, Rare, Epic, Legendary (doc 21). Characters give powers from Uncommon up (doc 21); characters are Coming Soon in the shop and chests (doc 22).
- FR-C5 *Removed 2026-11-01 (no wardrobe): the Heroes screen shows the chosen character on a land stage (doc 20).*
- FR-C6 Character sources: a region unlock gives that region's Common character; Uncommon and above later from chests and the shop (gems), when characters launch (doc 22).
- FR-C7 *Removed 2026-11-01 (sets belonged to gear).*

### 1.4 Power-ups (P0/P1)
- FR-U1 Consumable power-ups are bought with coins or gems (1,000 coins / 50 gems or 1,500 coins / 75 gems, doc 22 point 4.3), kept in stock and used from run buttons; a tap while active adds the full duration (doc 27 section 7).
- FR-U2 In-run power-up orbs grant brief free effects (no inventory used).
- FR-U3 Permanent upgrades (e.g. longer magnet duration) bought with coins, capped, and disabled in Ranked leaderboard runs.
- FR-U4 Leaderboard fairness: **Ranked** runs disallow consumables and upgrades. **Casual** runs allow them. Campaign runs are Casual by default and do not credit the milestone global boards unless played Ranked (doc 08 section 1).

### 1.5 Currencies and store (P0)
- FR-S1 Coins (soft) and Gems (hard). **Fixed rate: 1 gem = 20 coins** (doc 22). Gems buy coins; coins never buy gems.
- FR-S2 Gems purchasable via platform IAP (Google Play Billing, StoreKit 2) in **9 packs of 50 to 15,000 gems** ($0.79 to $119.99, owner table in doc 22 point 4.1; numbers in doc 27 section 6). Launch promo and first-purchase double exist in code but are OFF by default.
- FR-S3 In-game shop with **5 tabs** (owner, doc 26): **Bundles, Chests, Power-ups, Coins, Gems**, plus a **Subscription** button (Weekly $1.49, Monthly $4.99). Characters are a Coming Soon card. Chest, bundle, subscription and coin-pack rules: docs 22 and 26; all numbers: [27-economy-tables-from-code.md](27-economy-tables-from-code.md). (The earlier 4-tab layout of doc 14 is superseded.)
- FR-S4 Server-authoritative balances. Purchases are receipt-validated server-side before gems are granted.
- FR-S5 Restore purchases button (required by Apple). Handle pending, refunded and revoked transactions.
- FR-S6 Display localized prices from the store.

### 1.6 Leaderboards and social (P0/P1)
- FR-L1 Leaderboards for both campaign and endless: see doc 08 section 1 (per-level friends boards, per-region and total campaign boards, per-region endless all-time/weekly, daily challenge).
- FR-L1b Endless speed ramp: +4 percent every 25 floors, capped at +150 percent (doc 08 section 2).
- FR-L2 Friends leaderboard (via platform sign-in / share code).
- FR-L3 Server-side score validation: replay hash + plausibility checks (see architecture doc).
- FR-L4 Display name moderation (profanity filter), report/hide player.
- FR-L5 Score and run share card (image + deep link) for virality (P1).

### 1.7 Retention and engagement (P1)
- FR-R1 *Superseded (doc 26 section 4): the Rewards screen has Main, Daily and Weekly tabs; no login streak calendar.* Daily: 10 actions of 10 points, milestones at 20 to 100 points; Weekly: 11 tasks; Main: star and region rewards. Numbers: doc 27 sections 11 to 13.
- FR-R2 Daily and weekly tasks as above; a daily challenge (one seed, one global board) is later.
- FR-R3 Treasure chests: **Iron and Gold**, gems only, odds shown in the (i) text (doc 22 point 3); no pity counter in the shipped version; no earned chests.
- FR-R4 Battle pass: *not built and not in the current owner docs (20 to 26); later.*
- FR-R5 *Superseded (doc 26): the VIP became the **Subscription** (Weekly $1.49: 100 gems at once, then 10 gems and 500 coins a day for 7 days; Monthly $4.99: 200 gems at once, then 10 gems and 600 coins a day for 30 days, plus a free power-up every 5th claim). No VIP tiers, no stat bonuses.*
- FR-R6 Achievements (platform achievements Game Center / Play Games).
- FR-R7 Push notifications (opt-in): streak at risk, chest ready, new region, event start.

### 1.8 Ads (P1)
- FR-A1 Rewarded video, always opt-in. **Built: the free revive** (first revive of a run, instead of 5 gems; LevelPlay test ads). Planned only: daily power-up by ad (doc 22 point 8). Double result, free chest and ad gems are not in the current plan (doc 27 section 16).
- FR-A2 Optional interstitials at natural breaks only (frequency-capped); none at launch.
- FR-A3 Mediation SDK with Google UMP / Apple ATT consent flows.

### 1.9 Accounts, saves, settings (P0/P1)
- FR-X1 Anonymous device login at first launch (no friction); optional linking to Google Play Games / Apple Sign-In / email for cross-device restore.
- FR-X2 Cloud save with conflict resolution (server wins for currencies/purchases, max wins for progress).
- FR-X3 Settings: sound, music, haptics, left-handed layout, scroll difficulty (5 levels), language, privacy, delete account, data export (GDPR/CCPA). Development builds only: the testing switch "Test: all levels open" (default on) unlocks every level and region of the playable set.
- FR-X4 Localization (P1): EN, ES, PT-BR, FR, DE, IT, RU, TR, ID, JA, KO, ZH-Hans, ZH-Hant, HI, AR (RTL) staged.

### 1.10 Content delivery (P0 architecture, P1 use)
- FR-D1 All regions, levels and items are data definitions plus remote asset bundles.
- FR-D2 Remote Config feature flags and A/B tests for prices, drop rates and offers.
- FR-D3 New content added without an app update, within store policy (assets and data only; no downloaded executable code).
- FR-D4 Download-on-demand per region with progress UI; base install size under 150 MB.

### 1.11 Admin / live-ops back office (P1)
- FR-O1 A simple web dashboard (or CLI) to create items, regions, offers, events and to push to PlayFab title data.
- FR-O2 Content validation scripts in CI reject malformed definitions.

## 2. Non-functional requirements

> **CRITICAL (owner, 2026-10-19):** seamless, never-choppy gameplay, no improper overlap of screen items, and accessibility are mandatory. They are defined as CUX-1..CUX-10 in [11-critical-ux-gameplay-requirements.md](11-critical-ux-gameplay-requirements.md) and override every other priority in this document.

| Area | Requirement |
|---|---|
| Performance | 60 fps on mid-range 2020+ devices; 30 fps minimum on 3 GB RAM devices; cold start under 4 s |
| Size | Install under 150 MB (Play AAB base under 150 MB, iOS cellular-download friendly); regions are downloaded on demand |
| Memory | Peak under 500 MB on low-end devices; atlas-based sprites, pooled objects |
| Battery/heat | 30/60 fps toggle, no per-frame allocations, no GC spikes |
| Offline | Campaign and endless playable offline; syncs when online; purchases require online |
| Security | Receipt validation server-side, signed requests, replay validation, jailbreak/root soft signals, no secrets in client |
| Privacy | GDPR, CCPA, Apple ATT, Google Data Safety, UMP consent; privacy policy and terms; age gate (neutral) |
| Accessibility | Colour-blind-safe UI cues, haptics toggle, left/right-handed layout, reduced motion option |
| Reliability | Crash-free sessions 99.5 percent or better; analytics funnel for every purchase step |
| Compliance | Apple App Store Review Guidelines (in-app purchase, loot box odds 3.1.1), Google Play policy (payments, families, loot box odds disclosure), COPPA not targeted |
| Scalability | Content catalog scales to hundreds of regions and thousands of items with no code changes |
| Testability | Deterministic simulation allows automated replay tests; headless level-solvability tests in CI |
| Maintainability | Data-driven content, assembly definitions, CI builds (GitHub Actions or Unity Build Automation) |

## 3. Out of scope for v1
- Real-time multiplayer races (consider post-launch async "ghost" races, P2).
- User-generated levels.
- Tablet-specific layouts beyond safe scaling.
- Web or desktop builds.
