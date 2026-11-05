# Bug List 3

Owner list received 2026-11-04 (after build v0.47.0.55, commit 3473813). Written down first, then worked. Status column is updated as items land.
Item ids: D1 = World Map, D2 = Shop (Heroes/Power-ups, entry), D3 = Shop Bundles, D4 = Rewards, D5 = Platforms.
Reference images for D4: SS16, SS17, SS18 (a Main / Daily / Weekly quest screen with a region tab strip, a progress header with milestone chests, big task cards, a tab row Main | Daily | Weekly and a back + Claim All footer).

| Id | Request | Status |
|---|---|---|
| D1 | World Map: move the Mushroom icon slightly up and the Meadow icon slightly lower on the road. | Done (code, tests written, not run in Unity) |
| D2.a | Shop, Heroes and Power-ups page: it opens at Power-ups and the Heroes banner is hidden above. It must start from Heroes (top of the page); this page is currently scrolled past it every time the Shop opens. | Done (code, tests written, not run in Unity) |
| D2.b | Shop entry point should always be Gems and Coins, on the top (every time the Shop opens, the Gems / Coins tab is selected, scrolled to the top). | Done (code, tests written, not run in Unity) |
| D3 | Shop, Bundles: the bundle name goes on the same line as the "i" icon. | Done (code, tests written, not run in Unity) |
| D4 | Rewards (Main, Daily, Weekly): looks horrendous. Redesign the background and the presentation to look sleek, following SS16, SS17, SS18. Use OpenArt as needed. | Done (code, placeholders for art, not run in Unity) |
| D5 | Create new unique platforms for Region 4 and Region 5: 7 each, aligned to their regions. | Region 4: Done (7 kinds, enum 29-35, Sim, generator, bot, view with tinted fallback, Region4PlatformTests green, golden for region 4 regenerated; art sheets drafted in T78, not generated, no credits). Region 5: Done (7 kinds, enum 36-42, Sim, generator, bot, view with tinted fallback, Region5PlatformTests, golden for region 5 regenerated; art sheets drafted in T80, not generated, no credits; docs/30 section 15) |

## Notes
- OpenArt (D4 art, D5 platform sheets) needs the full approval packet and an explicit yes before any credits are spent.
- Platform design docs: docs/30-regions-2-to-5-platforms.md. Region 3 was done in T74 (7 kinds, enum 22-28).
