# Boing Up: Sky Tower - Master Plan

A 2D vertical-jumping arcade game for Android and iOS in the generic vertical-jumper genre: momentum jumps, wall bounces, combo chains, and a tower that keeps rising. Original name, mascot, world and art. Only the genre mechanics are shared.

All names (game, mascot, regions, items) are **placeholders** and can be changed. They live in data files, not code.

## Decisions locked in (from the Q&A)

| Topic | Decision |
|---|---|
| Engine | Unity 6 LTS (C#), 2D URP, one codebase for Android + iOS |
| Deliverable now | Planning docs (this folder) are complete enough to start building; open items are in doc 09. Game repo: `E:\Code\boing-up-sky-tower`. **Update 2026-10-20:** the game is playable on Android as pre-release v0.9.x (5 regions x 15 levels); status in doc 07 |
| Art | AI-generated + manual cleanup, hybrid-ready; cute chibi cartoon style |
| Backend | Managed BaaS (PlayFab core, Firebase for crash/analytics) |
| Originality | Generic vertical-jumper core mechanics, brand-new look and feel; no third-party game name, art or character referenced anywhere (docs, code, store listing, marketing) |
| Audience | Teens and general, age rating 12+ (not a kids-directed app) |
| Monetization | Gem IAP (9 packs, 50 to 15,000 gems), Weekly and Monthly Subscription, rewarded ads (the free revive today), gem-priced bundles, Iron and Gold treasure chests (gems only), Rewards screen (Main, Daily, Weekly). Not built / not planned now: battle pass, starter pack, ad removal, VIP tiers. Numbers: doc 27 |
| Progression | Campaign levels (15 per region) AND endless mode per region, both with leaderboards |
| Region unlock | Sequential, or pay gems to skip ahead |
| Economy | Hard currency (gems) and soft currency (coins), **fixed 1 gem = 20 coins** (doc 22); numbers read from the code in doc 27 |
| Scope | 20 live regions + 10 visible locked teaser regions; 15 levels each. **Update 2026-10-20:** the current milestone ships 5 playable regions (75 levels); regions 6-30 are shown on the scrollable world map as "Coming soon". The 20-region launch target below is unchanged |
| Team | Solo / tiny team, lean budget: automate everything, scope by phase |
| Launch scope | All 20 regions playable at launch (confirmed); roadmap in doc 07 stands as written |
| Format | Markdown in `E:\Source\Game Ideas\boing-up-sky-tower\docs` |
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
13. [13-iap-ads-implementation-plan.md](13-iap-ads-implementation-plan.md) - in-app purchases and ads: launch scope, Google Play checklist, design, tasks T-IAP-1..17
14. [14-shop-requirements.md](14-shop-requirements.md) - shop requirements (older; shop tabs, packs and chest numbers are superseded by docs 22, 26, 27)
15. [15-power-ups-characters-gear-requirements.md](15-power-ups-characters-gear-requirements.md) - power-ups, chests, characters (older; the gear part is removed)
16. [16-ui-polish-and-platform-overhaul.md](16-ui-polish-and-platform-overhaul.md) - UI polish, style kit, screens
17. [17-gear-inventory-chests-characters.md](17-gear-inventory-chests-characters.md), 18. [18-wearable-gear-and-character-rig.md](18-wearable-gear-and-character-rig.md), 19. [19-polygon-character-and-gear-framework.md](19-polygon-character-and-gear-framework.md) - **superseded by doc 20**; gear removed; short historical notes
20. [20-characters-and-chests.md](20-characters-and-chests.md) - characters as part pictures, no gear, chests replace the inventory tab
21. [21-characters-rarity-chests-shop.md](21-characters-rarity-chests-shop.md) - character rarities, powers, cooldowns (docs 22 and 26 win on chests and shop)
22. [22-issue-list-24-economy-chests-shop.md](22-issue-list-24-economy-chests-shop.md) - owner economy tables: gem packs, coin packs, power-up prices, Iron and Gold chests, payout, revive (**wins over docs 05, 08, 13, 14, 15**)
23. [23-issue-list-25-screens-randomization-flags.md](23-issue-list-25-screens-randomization-flags.md) - home, shop and chest screen fixes, random layouts, test flags
24. [24-difficulty-and-new-platforms.md](24-difficulty-and-new-platforms.md) - difficulty curve and the 7 new region 1 platforms
25. [25-level-structure.md](25-level-structure.md) - rows, platforms and goals for the 15 levels
26. [26-shop-bundles-subscription-rewards.md](26-shop-bundles-subscription-rewards.md) - 5-tab shop, bundles, subscription, Rewards screen
27. [27-economy-tables-from-code.md](27-economy-tables-from-code.md) - **economy reference read from the code**: every table in gems, coins and dollars, gems vs ads, recommendations
29. ~~29-character-stats.md~~ - **removed (2026-11-08), replaced by doc 44**
34. [34-character-merge.md](34-character-merge.md) - character merge (3 copies make the next tier), Merge screen, hero powers in a run; its stat and percent rules moved to doc 44
44. [44-power-mechanics-bible.md](44-power-mechanics-bible.md) - **Power mechanics bible: hero physics, rarity tiers, the Forgiveness Index, generator, results** (single source of truth for hero physics; replaces doc 29 and the stat rules of doc 34)


> **Critical:** user experience and accessibility are critical; gameplay must be seamless and never choppy, and screen items must never overlap improperly. See doc 11. Every doc and every release is bound by it.

## The core idea in one paragraph

Everything that can grow (regions, levels, characters, power-ups, gem packs, bundles) is a **data record plus an asset bundle**, delivered remotely. Adding region 31 means adding a JSON definition, a tileset/background asset set and a bundle upload. No app-store update is needed, provided it reuses existing signature modules (all 30 regions' modules ship in v1.0 where the schedule allows). That is what lets the game "go forever" with a solo team.

## Honest caveats

- "Viral" is not something a plan guarantees. The plan raises the odds through a short-session hook, shareable replays/scores, collectible skins, social leaderboards and a soft-launch KPI gate before spending on marketing.
- Prices, store fees and SDK/MCP names change. Re-verify the items marked **(verify)** before committing money.
- Paid random items (the Iron and Gold chests) need published contents (shown in the chest (i) text) and are restricted in some regions (see monetization doc). **No gear exists in the game** (owner 2026-10-24 and 2026-11-01).
