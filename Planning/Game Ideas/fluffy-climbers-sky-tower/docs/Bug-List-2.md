# Bug List 2

Owner list received 2026-11-03 (after build v0.46.0.54, commit 8bd72dd). Written down first, then worked. Status column is updated as items land.
Item ids: C1.x = World Map, C2.x = Shop.

## 1. World Map
| Id | Request | Status |
|---|---|---|
| C1.a | Hide all the region name text behind a flag: true shows the text, false hides it; keep the flag in code for now. Where a tag says "Coming Soon" it stays. The flag only affects the actively available regions. All "Coming Soon" tags keep showing. | Done (built, tests written, not run: Unity not started) |
| C1.b | Move all the pins/icons onto the road for region 1: for example the Meadow Springs icon is on the pond, bring it to the road. Sunny Beach also onto the road, but slightly lower so it is just next to the beach water. Check all 30 regions: each one aligns with its region and sits on the road. | Done (built, tests written, not run: Unity not started) |

## 2. Shop
| Id | Request | Status |
|---|---|---|
| C2.a | The yellow banners at the top (Bundles, Subscription, Heroes, Gems) are tied to the shop header and stay fixed when scrolling. Make them part of the scroll: bring them down to sit just above the text/content, keep the Momo shop header completely visible. So when the list scrolls, the banner scrolls away with it. | Done (code and tests written, not yet run in Unity) |
| C2.b.i | Bundles cost only between 200 and 500 gems. | Done (v0.47, needs device check) |
| C2.b.ii | Worth stays between 1.5x and 2.5x the gems. Show it as smaller text (30 percent smaller), for example "Worth x3.3" (format example), in the same font as the bundle name and in italics. No star icon. | Done (v0.47, needs device check) |
| C2.b.iii | Regenerate bundles from scratch with coins, power-ups and a character from open regions only. Uncommon 70 percent, Rare 20 percent, Epic 10 percent. | Done (v0.47, needs device check) |
| C2.b.iv | Delete the existing bundles and regenerate a fresh 2,000 with new names (similar decent style, 2,000+ new names), gems 200 to 500, worth 1.5x to 2.5x. | Done (v0.47, needs device check) |
| C2.b.v | The hero card shows an icon and "?" only, no words. | Done (v0.47, needs device check) |
| C2.b.vi | The "i" popup for bundles looks good. Add a small funny one-liner to it for each bundle, based on its name. | Done (v0.47, needs device check) |

## Notes
- Replaces Bug List 1 B4 numbers (200 to 600 gems, worth 2.0 to 3.5x). Bundle doc: docs/37-bundles.md.
- The "Worth x3.3" example is outside the 1.5 to 2.5 range: it is only a format example (to confirm).
