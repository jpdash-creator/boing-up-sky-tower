# Fluffy Climbers: Tower Jump - Master Plan

A 2D vertical-jumping arcade game for Android and iOS in the generic vertical-jumper genre: momentum jumps, wall bounces, combo chains, and a tower that keeps rising. Original name, mascot, world and art. Only the genre mechanics are shared.

All names (game, mascot, regions, items) are **placeholders** and can be changed. They live in data files, not code.

## Decisions locked in (from the Q&A)

| Topic | Decision |
|---|---|
| Engine | Unity 6 LTS (C#), 2D URP, one codebase for Android + iOS |
| Deliverable now | Planning docs (this folder) are complete enough to start building; open items are in doc 09. Game repo: `E:\Code\boing-tower-game`. **Update 2026-10-20:** the game is playable on Android as pre-release v0.9.x (5 regions x 15 levels); status in doc 07 |
| Art | AI-generated + manual cleanup, hybrid-ready; cute chibi cartoon style |
| Backend | Managed BaaS (PlayFab core, Firebase for crash/analytics) |
| Originality | Generic vertical-jumper core mechanics, brand-new look and feel; no third-party game name, art or character referenced anywhere (docs, code, store listing, marketing) |
| Audience | Teens and general, age rating 12+ (not a kids-directed app) |
| Monetization | Gem IAP, rewarded ads, battle pass, starter packs/limited offers, ad removal, daily streak, mystery chests (gacha), VIP subscription |
| Progression | Campaign levels (15 per region) AND endless mode per region, both with leaderboards |
| Region unlock | Sequential, or pay gems to skip ahead |
| Economy | Hard currency (gems) is scarce from play; a free soft currency (coins) for basics |
| Scope | 20 live regions + 10 visible locked teaser regions; 15 levels each. **Update 2026-10-20:** the current milestone ships 5 playable regions (75 levels); regions 6-30 are shown on the scrollable world map as "Coming soon". The 20-region launch target below is unchanged |
| Team | Solo / tiny team, lean budget: automate everything, scope by phase |
| Launch scope | All 20 regions playable at launch (confirmed); roadmap in doc 07 stands as written |
| Format | Markdown in `E:\Source\Game Ideas\boing-tower-docs\docs` |
| Typography | Lilita One (display) + Nunito Bold (body), both free OFL fonts (owner decision 2026-10-20) |
| Versioning | Every build is vMAJOR.MINOR.HOTFIX.BUILD (major 0 = pre-release); rules in the game repo `.claude/rules/versioning.md`, version file `Tools/version.json`, build log `Content/builds/build_log.csv` |

## Document index

1. [01-requirements.md](01-requirements.md) - functional and non-functional requirements
2. [02-game-design.md](02-game-design.md) - mechanics, regions, levels, characters, power-ups, economy numbers
3. [03-technical-architecture.md](03-technical-architecture.md) - Unity project, data-driven content, backend, anti-cheat
4. [04-asset-plan.md](04-asset-plan.md) - asset taxonomy, naming, AI pipeline, prompt library, extensibility
5. [05-monetization-liveops.md](05-monetization-liveops.md) - IAP catalog, store compliance, live-ops calendar
6. [06-tooling-mcp.md](06-tooling-mcp.md) - which MCP servers/tools to hook to Claude vs code directly
7. [07-roadmap-risks.md](07-roadmap-risks.md) - phases, milestones, KPIs, risks, open items
8. [08-rules-addendum.md](08-rules-addendum.md) - **v2 rules: leaderboards, speed ramp, neon style, generator, 30 regions, coin/gem/USD ratios, plus the owner device-test rules and economy v2 (2026-10-19/20). Where it conflicts with docs 01, 02 or 05, doc 08 wins.**
9. [09-decisions-needed.md](09-decisions-needed.md) - v2.1 review decisions (all accepted and applied) and the decided / still-open items from the device-test rounds
10. [10-asset-catalog.md](10-asset-catalog.md) - exhaustive OpenArt asset catalog, prompts, approval batches (generated in the game repo)
11. [11-critical-ux-gameplay-requirements.md](11-critical-ux-gameplay-requirements.md) - **MANDATORY** critical UX, seamless gameplay and accessibility requirements (CUX-1..10); override convenience everywhere
12. [12-audio-placeholder-set.md](12-audio-placeholder-set.md) - placeholder CC0 sound effects and music loop, file-by-file mapping
12. [13-iap-ads-implementation-plan.md](13-iap-ads-implementation-plan.md) - in-app purchases and ads: launch scope, Google Play checklist, design, tasks T-IAP-1..17


> **Critical:** user experience and accessibility are critical; gameplay must be seamless and never choppy, and screen items must never overlap improperly. See doc 11. Every doc and every release is bound by it.

## The core idea in one paragraph

Everything that can grow (regions, levels, characters, power-ups, gem packs, offers) is a **data record plus an asset bundle**, delivered remotely. Adding region 31 means adding a JSON definition, a tileset/background asset set and a bundle upload. No app-store update is needed, provided it reuses existing signature modules (all 30 regions' modules ship in v1.0 where the schedule allows). That is what lets the game "go forever" with a solo team.

## Honest caveats

- "Viral" is not something a plan guarantees. The plan raises the odds through a short-session hook, shareable replays/scores, collectible skins, social leaderboards and a soft-launch KPI gate before spending on marketing.
- Prices, store fees and SDK/MCP names change. Re-verify the items marked **(verify)** before committing money.
- Gacha/mystery chests need published odds and are restricted in some regions (see monetization doc).
