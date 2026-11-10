# Bug List 4

Owner list received 2026-11-05 (after build v0.48.1.59, commit 5fa6b23). Written down first, then worked. Status column is updated as items land.
Item ids: E1 = Shop Bundles, E2 = Shop Heroes section, E3 = Settings Admin, E4 = Rewards.
Also continued from docs/PAUSE_STATE.md (open decisions): Weekly milestone chests, endless difficulty cap.

| Id | Request | Status |
|---|---|---|
| E1 | Shop, Bundles: like the Coins page where the green button is wrapped in a brown frame, do the same for the green (buy) button of each bundle. Not for the Refresh buttons. | Done (code, not run in Unity): brown frame plate (Coins strip colour, radius 26, 12 px padding) around each bundle's Buy button; Refresh buttons unchanged; timer-offer popup button left as is. Test: BundleBuyButtonsSitInABrownFrame (+130 percent). |
| E2.a | Shop, Heroes section: 4 small cards with the Momo "?" like in Bundles, but bigger: 2 per row, 2 rows. First row Uncommon, Rare; second row Elite, Legendary. | Done (code, tests written, not run in Unity) |
| E2.b | Gem prices (already documented): Uncommon 150, Rare 300, Epic 700, Legendary 1,500 gems. Shown on each card. | Done (code, tests written, not run in Unity) |
| E2.c | An "i" icon on each card explaining the hero properties of that level and that only heroes of open regions are offered. | Done (code, tests written, not run in Unity) |
| E2.d | Look and feel same as Power-ups (cards), but 2 x 2; Uncommon, Rare, Elite and Legendary each have their own background tile as decided (ask if it does not exist). | Done (code, tests written, not run in Unity) |
| E2.e | All buttons work. Where there are no Rare, Elite or Legendary heroes yet, a buy gives a short pop-up saying so. | Done (code, tests written, not run in Unity) |
| E3 | Settings Admin: add a button for "main rewards unlock" so all regions' main rewards can be reviewed (owner may change things afterwards). | Done (code, tests written, not run in Unity) |
| E4.a | Rewards Main tab: needs a background on top, repeating what Daily has (scene header). | Done (code, tests written, not run in Unity) |
| E4.b | Main and Weekly (and Daily) rewards: when something is completed but not claimed, the card's button turns green so it can be pressed directly, apart from Claim All. | Done (code, tests written, not run in Unity) |
| E4.c | Besides the header, the Main, Daily and Weekly lists are too widely spaced vertically: reduce the height of each card and the gap so it looks sleek. | Done (code, tests written, not run in Unity) |
| P1 | From the pause note: Weekly milestone chests on the Weekly header (needs counts and rewards). | Open (question) |
| P2 | From the pause note: endless difficulty cap (layout stops rising at floor 400, shares at 150, speed at about 937): keep or make it keep climbing. Owner answer: endless cycle (L1-L15 over floors 1-1500, then L9-L15 repeating every 1000 floors, see doc 24 section Endless cycle). | Done (code, tests written, not run in Unity) |
