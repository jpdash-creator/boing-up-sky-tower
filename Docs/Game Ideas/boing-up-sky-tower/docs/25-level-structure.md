# 25. Level structure for all regions (owner issue list 27, 2026-10-31)

Applies to regions 1 to 5 (and the rules every later region follows). Replaces the old goal layout of docs 08 for the campaign. Owner answers applied.

| Level | Rows | Platforms per row | Special platforms (share of rows, ramp from the bottom to the top of the level) | Goal |
|---|---|---|---|---|
| 1 (tutorial) | 60 | 1.3 | 10% rising to 30% | Reach the last platform |
| 2 to 5 | 90 | 1.3 | 30% rising to 50% | Reach the last platform; level 5 = combo chain |
| 6 to 9 | 120 | 1.3 | 40% rising to 60% | Reach the last platform; level 7 = no wall touch; level 8 = reach the last platform with gems |
| 10 to 14 | 150 | 1.3 | 40% rising to 60% | Reach the last platform; level 10 = combo chain |
| 15 (final) | 150 | 1.3 | 50% rising to 70% | Reach the last platform, with gems, no chase |

Rules
- Combo chain levels: level 5 (90 rows) and level 10 (150 rows), with the combo ladder inside and the combo target scaled to the level. They have NO gem pickups/icon. The survive-wave goal is removed everywhere (level 10 was survive wave; level 11 was a combo chain and is now reach-the-last-platform).
- Level 7: no wall touch (a wall touch in the air fails the goal; the start popup shows the small italic line; fail text 'Wall touched!').
- Level 8 and level 15: goal is to reach the last platform (no hidden-gem search goal, no boss chase goal; the danger zone still rises like in every level). Gems are placed along the route with a chance that a gem is a double gem (worth 2); level 8 also keeps one hidden bonus gem (also maybe double); the first clear of level 15 pays an extra double-gem bonus.
- All other levels: reach the last platform (floor goal at the top row).
- Platforms per row 1.3 means 130 platforms per 100 rows (the main platform per row plus extra row mates), respecting the stack rule (at most 4 platforms in any 3 u window) and solvability.
- 'Special platforms' = the region's special kinds (region 1: the 7 new kinds plus flowers; later regions: their own); the share ramps inside each level from the low value at the first rows to the high value at the last rows; layouts stay random per attempt and always beatable (bot proof, 20+ seeds per level).
- Star targets stay the level's fixed numbers (rescale the coin targets for the new lengths if the old ones become unreachable or trivial; report the new numbers).
- Endless mode keeps its own generator.
- After implementation: run AltTester on the phone for region 1 levels 1 to 15 with the autopilot (owner confirms the phone is connected).
