# 23. Issue list 25: screens polish, random layouts, wall death, test flags (owner, 2026-10-31; the owner's pasted title says "Issue List 24")

Working copy of the owner's list with the owner's answers applied. Builds on docs/20, 21 and 22. If something here differs from those, THIS file wins.

## Main (home) screen
1. Remove the "Coming soon" text under VIP, Bundles, Daily Rewards; the small lock icon on top of each button is enough.
2. Bundles needs its own icon: a brown bag tied with a string. (Owner chose OpenArt for it: the lead sends an approval packet; until the art exists use the current placeholder.)
3. The right-hand icons move closer to each other and lower.
4. The SELECTED character always shows on the home screen (the Sporeling if picked), drawn from its parts like the character screen.
5. The home background is the LATEST UNLOCKED region's backdrop (region 1 until region 2 unlocks, then region 2, and so on, capped at the last playable region).

## Shop screen
1. Remove Treasure Chests from the shop (the bottom bar Chests tab keeps opening the Treasure Chest screen directly).
2. Shop sections, left to right: Power-ups, Characters, Coins, Gems; it always opens on Power-ups. The four tabs are slightly wider so they take the whole bar.
### Gems tab
1. Each pack card is much less tall (50, 100 ... too high now); reduce further.
2. "Popular" / "Best value" is not a blue badge: it sits BELOW the gem icon as small green italic text (same for the 100 and the 1,000 packs and for Best value). The gem icon is bigger than now.
3. Three packs per row.
### Coins tab
- Cards are less tall; remove the text in between (the "Costs N gems / Short by" note). Button is GREEN (not yellow) with a small gem icon and the gem count. Tap: if gems >= price show a confirmation popup with the gem cost, then after buying a small "bought" confirmation; if gems are not enough go directly to the Gems tab (no popup).
### Power-ups tab
- The (i) icon is at the top right of each card.

## Treasure Chests
1. No separate "open treasure chests" page: the chest button opens the chest screen directly. Delete the link page.
2. Titles "Iron Chest" and "Gold Chest" (renamed from Golden) are written at the top centre of each section, with Buy and Buy 10 at the bottom right of each section.
3. The (i) icon is smaller; the Iron/Gold Chest info popup is smaller; its font and text are centred.

## World map
- Section 6 (Dino region): move it slightly down with its text; the "Coming soon" tag goes UNDER the name. Alien Jungle moves down and its Coming soon goes underneath. The Space Station moves down again with Coming soon underneath. This leaves room for Rift of Time to come down slightly with Coming soon at the bottom.

## Gameplay
1. "I see a lot more gems": no fix, reasoning first (given by the lead in chat).
2. The blue left and right arrows are the same height as the green (jump) button and centred on it.
3. The game uses only the SELECTED character (home page and run). If the Sporeling is selected it shows on the home page and in the game.
4. CRITICAL: platform positions are not randomized. Layouts must change on EVERY attempt, retries included (new random seed each attempt), for regular AND special platforms, keeping each level's difficulty (levels 1 to 15, regions 1 to 5). Gaps and placement are the lead's design. Guarantees: always beatable (bot/solver checked) and the same star/goal targets per level.
5. BUG: in Candy Land level 7, touching the wall killed the character (twice; level 6 fine). Audit all walls in all levels: touching a wall must never kill.
6. Endless: the text on the pop-up is not centred.

## Character screen
1. The cards are too wide: the left side gets cut and the top is cut when scrolled. Make them slightly smaller with margin.
2. Remove the lock icon in every slot; they are slots, not locked.

## General: two feature flags (dev builds only, switches in Settings)
- GAME_JUMP_TEST: OFF = levels and regions follow REAL progress while home, shop, chests, characters stay on. ON = every level and region open.
- SYSTEM_TEST: OFF = fully real (production behaviour: no test gem buys, real unlocks and ownership). ON = everything open (levels, regions, all characters, test purchases, dev tools). SYSTEM_TEST on wins over GAME_JUMP_TEST.
- Defaults in the owner's test build: GAME_JUMP_TEST OFF, SYSTEM_TEST ON (owner's answer). Release builds ignore both flags and are always real.
- Characters: Momo is open by default. The Sporeling is HIDDEN until region 2 is unlocked, then shown unlocked (one character per region added after this test).
- Shop and Treasure Chests stay working in every state.

## Notes
- The lead's note to the owner: with SYSTEM_TEST ON by default every level is open; switch it OFF in Settings for the real-progress test.

## Open questions from helpers (ask the owner later)
- Treasure popups: "Open 1 Iron Chest" / "10 Gold Chests": capital C everywhere or only in section titles?
- Home character size: keep 540 px or bigger/smaller?
- World map page 6: the name + Coming soon tag was applied to all of regions 26 to 30 (Dragon Peak included); limit to Dino, Alien Jungle, Space Station, Rift of Time only?
- Shared popup frame offsets every popup title 30 units left to leave room for the X; remove the offset everywhere so all popup titles are centred?
- OpenArt balance was 5,303 before T60, about 1,260 lower than the lead's record (6,563): check the OpenArt history.
- (H6) Settings labels: exact flag names GAME_JUMP_TEST / SYSTEM_TEST or plain "Jump test" / "System test"?
- (H6) The loading screen shows Momo until the save is loaded (the selected character is read after the save loads). Read the stored choice directly so the loading screen already shows the selected one?
- (H2b) Coin confirm popup always shows (also for the 50-gem pack): as intended? Coins now 3 per row like gems (7 packs, the last row has one centred card): fine?
- (H7) Level 7 goal: owner decided to KEEP "Reach floor N without a wall bounce": show it small italic in the level start popup, fail text "Wall touched!".
- (H8) Hidden-gem levels: random layouts often put the gem on the spine platform instead of a side platform; acceptable, or search more side-platform spots?
- (H8) Gem count and rarity follow the layout seed (count has a +-1 random rounding), milestone gem rewards still use level.Seed. Fine?
- (H8) No runtime solvability guarantee: layouts are bot-proven offline (75 levels x 20 seeds = 1500 layouts plus hidden-gem levels x 60); the bot is too slow (up to 36 s) to run at load. Want a runtime guarantee?

## Superseded by owner issue list 29 (2026-11-01)
The home right-hand column (VIP/Subscription, Bundles, Daily Rewards) and the bottom-bar Chests tab described above no longer exist: the bottom bar is Shop | Subscription | Play | Heroes, Rewards is a gift button above Heroes, Bundles and Chests are Shop tabs. See doc 26.
