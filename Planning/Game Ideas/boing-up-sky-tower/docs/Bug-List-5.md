# Bug List 5

Owner list received 2026-11-05 (after build v0.49.0.60, commit 22cbeac). Written down first, then worked. Status column is updated as items land.
Item ids: F1 = Rewards (Main, Daily, Weekly), F2 = Hero screen, F3 = Shop Treasure Chests, F4 = New heroes (rarity tiers and 4 more characters per region).
Reference image for F2: SS19 (a merge screen: result card at the top-left with a before/after stat panel, the three source cards below it joined by a plus, a Filter button and an orange FORGE button, then a grid of cards with a green tick on the selected ones; the Merge button stays disabled if the wrong cards are selected).
Order from the owner: F1, F2 and F3 first; F4 (new heroes) last, one region and one character at a time. Heroes must be tied into Bundles, Chests and Rewards so every open region can drop them.

## F1 Rewards
| Id | Request | Status |
|---|---|---|
| F1.a | Main rewards need a complete revamp and must be per region: today everything is in one place and unlocking a region does not unlock that region's main rewards. A slider (swipe/scroll) across all 30 regions, regions 6 to 30 marked "Coming Soon". Every time a region is selected it shows its own goals and tasks (not a static set): stars, clear levels, jumps, total combos in the region, play time in the region, and so on. "Clear all levels" (the purple band) becomes just one of the goals. Remove the numbered 1, 2, 3 rail from Main. Use the Daily/Weekly card UI. Map region goals and tasks to rewards, redo the rewards and do a complete economic analysis; do not give away too many gems. | Done (data layer, tests; UI, code, tests written, not run in Unity) |
| F1.b | Redo the Daily and Weekly rewards and tasks: login, play time, combos, totals, clearing hard levels, no-wall levels, using power-ups (probably weekly rather than daily), and so on. | Done (data layer, tests; UI, code, tests written, not run in Unity) |
| F1.c | Rewards can also give Common characters for open regions, in Main or Weekly rewards depending on the economics. | Done (data layer, tests; UI, code, tests written, not run in Unity) |
| F1.d | Everything must be achievable without spending real money. Redo the Rewards UI for Main and redo the tasks for Main, Daily and Weekly. | Done (data layer, tests; UI, code, tests written, not run in Unity) |

## F2 Hero screen
| Id | Request | Status |
|---|---|---|
| F2.a | Remove the bottom row. "Add 2 copies" is test mode only. | Done (code, tests written, not run in Unity). The test-only 'Add 2 copies' button is gone from the Hero screen (nothing sits in a bottom row any more); the Character Test admin popup (Hero test page) is the copies tool. See docs/34 section 10. |
| F2.b | Add a row just below the "i" icon and above the cards: "All Heroes" and the Merge button moved into this row with the text "Merge" next to it. | Done (code, tests written, not run in Unity). Action row under the 'i': All Heroes (hero icon + label) left, Merge (painted MergeIcon + the word) right. docs/34 section 10. |
| F2.c | "All Heroes" opens a full-screen list of ALL characters. Anything the player does not have is shown locked (like the Common ones), even in test mode, including Rare, Elite and Legendary. 3 cards per row, as many rows as needed. It is the hero inventory: when a character is unlocked it shows up, with an "i" icon for details. Heroes are grouped by region. | Done (code, tests written, not run in Unity). New AllHeroesScreen: 3 cards per row, grouped by region (data driven), locked = muted card + dark silhouette + padlock, Rare/Elite/Legendary are locked placeholders per Common hero, 'i' details popup with how to get it. |
| F2.d | Main Hero screen = only the heroes the player really owns (bought, merged, got): not all characters. In test mode, with regions unlocked, only Commons show; Uncommon stay locked (neither merged nor bought) and appear only in All Heroes as locked. No counts next to heroes any more: if the player gets another copy of the same hero (a second Momo Common), a new card appears. This is the entire owned list, not the available list (the current behaviour is a mistake). | Done (code, tests written, not run in Unity). Owned list = one card per owned copy, no counts anywhere; Character Test owns only the Common heroes; the chosen tick is on the first card of the chosen type. |
| F2.e | Merge screen: instead of counts show the list of characters that can be merged; characters that cannot be merged are not shown. Example: 3 Momo + 2 Capi, later 4 Momo, 3 Capi, 2 Bonbon shows 3 Momo and 3 Capi. After a merge is done, remove the merged heroes from the bottom list and add the new Uncommon (e.g. Uncommon Momo) to the main Hero screen; same for the next merge. No numbers on hero cards at all. Wrong selection keeps the Merge button disabled; selected heroes get a tick (SS19). | Done (code, tests written, not run in Unity). Merge list = only hero types with 3+ copies, one card per copy, no numbers; tap to select (green tick, 3 slots); Merge enabled only for exactly 3 of one type; after a merge the list is rebuilt and the new tier joins the Hero screen. |

## F3 Shop Treasure Chests
| Id | Request | Status |
|---|---|---|
| F3 | Add heroes into the chests like Bundles; make all necessary changes; add a "worth" for each chest like in Bundles. Iron Chest: 70 percent Common, 20 percent Uncommon, 10 percent Rare. Gold Chest: 50 percent Uncommon, 40 percent Rare, 10 percent Elite. | Done (code, tests written, not run in Unity). Iron: 800-1,200 coins + 1 power-up + 1 hero card, worth x1.8. Gold: 3,000-4,200 coins + 3 power-ups + 1 hero card, worth x1.9. Prices unchanged. No duplicate refund any more. See docs/26 (chests with a hero card) and docs/37. |

## F4 New heroes (last, one region and one character at a time)
| Id | Request | Status |
|---|---|---|
| F4.1 | Region 1 Meadow Springs, Momo: has Common and Uncommon; add Rare, Elite, Legendary. Plus 4 more new characters for region 1: once Common is finalised, add Uncommon, Rare, Elite and Legendary for each. | Done (code, tests written, run in Unity). See `40-region1-heroes.md`. |
| F4.2 | Region 2 Mushroom Grove, Capi: same (Rare, Elite, Legendary + 4 more characters with all tiers). | Done (code, tests written, run in Unity). See `40-region1-heroes.md` section 7. |
| F4.3 | Region 3 Sunny Beach, Otto: same. | Done (code, tests written, run in Unity) - docs/41-region3-heroes.md |
| F4.4 | Region 4 Candy Canyon, Bonbon: same. | Open |
| F4.5 | Region 5 Bamboo Temple, Bao: same. | Open |
| F4.x | In the shop all heroes are tied to Bundles, Chests and Rewards so open regions can drop them. | Open |

## Notes
- Owner's wording lists regions 2 to 5 as "Mushroom Grove" for Otto, Bonbon, Bao: treated as typos (Region 3 Sunny Beach, 4 Candy Canyon, 5 Bamboo Temple).
- OpenArt for new characters (F4) needs the full approval packet and an explicit yes before any credits are spent (see .claude/rules/openart-credit-guardrail.md).
- Related docs: 26 (shop/bundles/rewards), 34 (character merge), 37 (bundles), Bug-List-4.md (hero cards, rewards polish).
