# 22. Issue list 24: name tags, chest screen, economics, gems, coins, bundles, daily rewards, revive (owner, 2026-10-25)

Source: the owner's pasted "Boing Tower: full requirements for issue-list points 1 to 9". This file is the working copy with the owner's answers (Q&A) applied. Where it differs from docs/21 (characters, chests, shop of 2026-10-24) THIS file wins.

## Shared conventions
- $1 = 95 rupees. 1 gem = 20 coins. A 1,000-coin power-up = 50 gems, a 1,500-coin one = 75 gems. Reference gem value = the 1000-pack rate, $0.01099 per gem.
- Characters, the chest character slot and the shop Characters section are Coming Soon. Chests ship with coins and power-ups only (character code stays but is switched off).
- Ranked play stays neutral. Power-ups roll with equal chance across the 7 types (Magnet, Shield, Wall Stick, Double Jump, Slow Time, Rocket, Flight). 10x open costs exactly 10x a single open.
- Critical UX (docs/11, .claude/rules/ux-gameplay-critical.md): CUX-3 no overlap or clipping on 9:16, 9:19.5, 9:21, 3:4; CUX-4 targets >= 48 dp with >= 8 dp gaps; CUX-5 text >= 16 sp, contrast >= 4.5:1, survives 130% text scale; CUX-6 colour never the only cue, reduced motion honoured; pause on interruption. Progress saved immediately after every purchase/open/result.

## Owner answers that settle conflicts (2026-10-25)
1. Chest loot: THIS list. Iron = 1 power-up + 400 to 600 coins; Golden = 3 power-ups + 1,000 to 1,400 coins; no characters until they launch (code kept, switched off).
2. Treasure Chest screen is its own screen opened by the chest button (bottom bar Chests tab and chest button on home, world map and level screens, one tap); the Shop's first section links to it.
3. Coin payout: clear 70 + 60 per star + 5 per combo step, times the region coin factor; replays once a level has 3 stars pay 50% and no gems; below 3 stars full pay.
4. Parallel background helpers inside the main chat, on main, no new sessions or branches.
5. Old launch promo stays in the code but OFF by default; first-purchase double also stays but OFF by default (a switch, turned on later by the owner). Both must be tested end to end.
6. Rewarded ads: integrate Unity LevelPlay now (package install OK), test ads only until the owner gives the app key.
7. Home screen: right-side column VIP, Bundles, Daily Rewards (all Coming Soon, text tag plus lock/dim); chest button stays in the bottom bar and opens the new screen. The bar Daily tab moves into the column.
8. Name tags: fix the world map, add an automated clipping test for every text on art on all screens, fix anything it finds in the same pass.

## Point 1: name tags (no cut letters anywhere)
- Backing width = measured text width + padding (>= 12 dp each side, >= 6 dp top and bottom, plus outline width and italic/bold overhang). Never a fixed number. Solid black or very dark (>= 90% opacity), rounded corners.
- The tag stays beside the pin and never covers the art or the pin; a long name wraps to two lines or moves to the other side; text never below 16 sp. Contrast >= 4.5:1. No clipping by a parent mask or the screen edge (tags near an edge shift inward). Tags must not overlap each other.
- Audit all text on art: world map (region names, Coming Soon tags, padlock labels), level screen (title band, wallet, level numbers), level popup (goal, stars, PLAY), shop badges and cards, treasure chest screen, character cards, home tags (VIP, Bundles, Daily), run HUD, results and failed screens.
- Tests: layout test measures every name tag (text width + padding <= backing width) on four aspect ratios at 130% text; longest region name; 30% longer names; names near top, bottom, notch; close pins.

## Point 2: chest button and VIP
- Chest button stays on main, world map and level screens; one tap opens the Treasure Chest screen; Back returns to the caller.
- VIP is a separate home-screen entity (Coming Soon): tapping shows a "Coming soon" message only. Bundles and Daily Rewards under it (points 7, 8).
- No overlap with wallet, pause, settings; each >= 48 dp, gaps >= 8 dp. Labels: "Treasure chests", "VIP, coming soon", "Bundles, coming soon", "Daily rewards, coming soon". Coming Soon is a text tag plus lock/dim.

## Point 3: Treasure Chest screen and the (i) info
- One chest per row: chest art, name, (i) icon, and on the right Buy (top) and Buy 10 (below). Iron row first, Golden below. No rarity rows or boxes. Prices: Iron 100 / 1,000 gems, Golden 300 / 3,000 gems with a gem icon. Buttons >= 48 dp tall, gaps >= 8 dp.
- Opening: animation about 1.2 s, skippable by a second tap; reduced motion shows the result at once; nothing flashes more than 3 times per second.
- Single open result: coins and the power-up gained with names (not colour only) and a Collect button. Buy 10 result: summary with total coins and a count per power-up type, "Collect all".
- Too few gems: open the shop's gem purchase prompt with the smallest pack that covers the shortfall; never spend partial amounts. Confirmation above 50 gems (doc 14 rule) on both buttons with price and contents. Save immediately after every open (an app kill never loses or doubles a reward).
- (i) info: text only (body font >= 16 sp, contrast >= 4.5:1, backing panel), scrolls if needed, clear close button, no clipping at 130%.
  - Iron Chest, 100 gems: each chest gives 1 power-up and 400 to 600 coins. The power-up is random from all 7 types, 1 in 7 each. Characters: coming soon, chances will be listed here.
  - Golden Chest, 300 gems: each chest gives 3 power-ups and 1,000 to 1,400 coins. Each power-up random from all 7 types (same type may repeat). Characters: coming soon.
  - When characters go live the info lists: Iron Uncommon 67%, Rare 33%; Golden Rare 57%, Epic 29%, Legendary 14%; one character per chest; duplicates refund 25% as gems.
- Value (for tests): Iron 70 to 105 gems (avg 86, return 86%); Golden 200 to 295 gems (avg 242, return 81%); average power-up unit 60.7 gems. A Golden chest can never beat its price. 10x = ten independent openings.
- Tests: layout audit (four aspect ratios, 130% text); simulate 100,000 Iron and Golden openings (coin ranges, power-up equal chance about 14.3% each, averages within 1%); Buy 10 = sum of 10 openings; insufficient gems spends nothing; kill mid-open gives no double or lost reward.

## Point 4: economics
### 4.1 Gem packs (new 50-pack; first-purchase x2 applies to packs 50 to 1,000 only; flag stored per account AND per pack; shown as text "First purchase: x2 gems")
| Gems | $ | INR |
|---|---|---|
| 50 | 0.79 | 75 |
| 100 | 1.49 | 142 |
| 200 | 2.49 | 237 |
| 500 | 5.99 | 569 |
| 1,000 | 10.99 | 1,044 |
| 2,000 | 20.99 | 1,994 |
| 5,000 | 49.99 | 4,749 |
| 10,000 | 89.99 | 8,549 |
| 15,000 | 119.99 | 11,399 |
### 4.2 Coin packs (paid with gems; the shop never shows a pack the player can't afford without showing the gem shortfall and a link to buy gems)
| Coins | Gems |
|---|---|
| 1,000 | 50 |
| 2,000 | 100 |
| 5,000 | 250 |
| 10,000 | 500 |
| 20,000 | 900 |
| 50,000 | 2,200 |
| 100,000 | 4,000 |
### 4.3 Power-ups: Magnet, Shield, Wall Stick, Double Jump 1,000 coins / 50 gems; Slow Time, Rocket, Flight 1,500 coins / 75 gems.
### 4.4 Chests: see point 3.
### 4.5 Shop layout and badges
- Section order: Treasure Chests, Power-ups, Characters (a single Coming Soon card, no buy buttons), Coins, Gems.
- "Popular" and "Best value" badges: smaller font, centred on the card, moved slightly lower so the card's upper border stays visible (the badge must not cover the border), text carries a name. Keep the current badge assignment (if none: Popular on the 1,000 pack, Best value on the 15,000 pack).
- Validator tests: strictly improving $/gem from the 100 pack up; coins per gem non-decreasing (20, 20, 20, 20, 22.2, 22.7, 25); chest return 70 to 105%. (Status 2026-11-01: the chest return validator now exists, `ChestTests.ChestReturnIsBetween70And105PercentOfThePriceAndGoldenMaxStaysBelowIt`: Iron and Golden average return 70 to 105% of the Open 1 gem price, Golden maximum payout below its price.) Purchase flow tests per pack including first-purchase doubling (switch ON in the test) and the once-only flag; promo switch ON and OFF.

## Point 5: gems and gem pickups
- Gem spot share per difficulty level goes 5, 5, 7, 9, 10 percent to 2.5, 2.5, 3.5, 4.5, 5 percent of spots. Rarity weights unchanged (Common 1, Rare 2, Epic 5, Legendary 10 gems at 70, 22, 7, 1 percent).
- Keep the guaranteed 5 gems on the first clear of L15 and the L5 and L10 milestone gems. Gems lost on a failed run, kept on revive; replays of a 3-star level drop no gems. Endless gem rules unchanged.
- Budget: about 70 to 110 placed gems per region; free budget about 100 gem-equivalents per region (pickups plus earned chests), hard cap 130. No earned chests are granted for now.
- Tests: average placed gems per region within 70 to 110 on all five difficulty levels; rarity distribution 70/22/7/1 over 100,000 spots; budget test pickups + earned chest value per region <= 130.

## Point 6: coins in game
- Clear pays 70 + 60 per star + 5 per combo step, times the region coin factor (about 1.00 region 1 to about 1.12 region 5). 0 stars 70, 1 star 130, 2 stars 190, 3 stars 250 before combo. Failed runs pay nothing. Endless coin rules unchanged.
- Replay rule: 50% once the level has 3 stars (no gems then); below 3 stars full pay.
- Tests: payout formula for 0 to 3 stars and regions 1 to 5 including combo; replay test; economy simulation about 3,750 coins per region for a perfect player within 10%. Risk noted: replay farming below 3 stars (see open items).

## Point 7: Bundles (Coming Soon icon now; future spec)
- Icon "Bundles" under VIP, Coming Soon tag, tapping shows a message only.
- Future: Starter pack once per account $0.99 = 100 gems + 2,000 coins (separate product, hidden after purchase); first-purchase doubling (4.1); India entry bundle (example Rs 49 = 40 gems + 500 coins, to confirm); power-up bundles 5 for the price of 4 (1,000-coin types 4,000 coins / 200 gems; 1,500-coin types 6,000 coins / 300 gems; stock cap 99 blocks with a message); all prices are config; age gate applies.

## Point 8: Daily Rewards (Coming Soon icon now; future spec)
- Icon "Daily Rewards" under Bundles, Coming Soon, no action.
- Future: one free power-up per day after a rewarded ad; reset at the day boundary (local vs UTC open); random of the 7 types equal chance, added to stock up to the cap of 99; ad failure or early close grants nothing and allows a retry; ad-free VIP gets it without the ad; "Come back tomorrow" text with a countdown.

## Point 9: revive
- Prices: 5 gems first revive, 10 gems second, at most 2 per run. One free revive through a rewarded ad, offered with the first revive as an alternative to the 5 gems.
- Offer on failing: first [Watch ad (free)] and [5 gems]; second [10 gems] (no ad if one was used). [Retry] always one tap, never hidden or blocked.
- Too few gems: the gem button opens the shop's smallest pack covering the shortfall. A revive keeps the run's gems and active effects; a failed run without a revive loses its gems. Text label, gem icon and price on every button. Gems deducted only after the revive succeeds; nothing spent without a tap. No network or no ad: only the gem option shows. Ad finished but revive failed: grant again on retry. Pause/interruption during the offer handled.
- Ads: Unity LevelPlay, test ads only (owner gives the app key later); one rewarded-ad interface, a test implementation for editor/dev, LevelPlay for device.

## Cross-cutting
- Characters (Coming Soon, specified): rarities Common/Uncommon/Rare/Epic/Legendary; Momo (region 1) and Sporeling (region 2) Common; card colours as docs/21 plus the rarity name on each card; powers: Uncommon 1 power, Rare 2, Elite 3 (Elite is the shown name of the code enum Epic), Legendary 4, with cooldowns of 30 / 22 / 15 / 10 s (hero physics and tiers: docs/44); chest weights when live Iron Uncommon 67 / Rare 33, Golden Rare 57 / Epic 29 / Legendary 14.
- Analytics events to add: chest_open (type, count, gems spent, contents), chest_odds_viewed, revive_offer, revive_taken (type, price), gem_pack_bought (pack, first purchase flag), coin_pack_bought, bundle_view, daily_reward_view, name_tag_audit (CI only).
- Build order suggested by the owner: 1) name tags + badge restyle, 2) chest screen + economics tables + 50-gem pack, 3) gem share + coin payout + revive prices, 4) home buttons.

## Assumptions made by the owner's author (tell the owner if a change is wanted)
Characters of locked regions never appear in chests or the shop; powers fixed, not upgradable; every level stays beatable with the Common character; chest coins uniform in range; Daily Reward random of 7 types; the ad revive is offered only with the first revive of a run.

## OPEN ITEMS (record only, ask the owner later; do NOT block work on them)
1. Characters in chests: at the stated weights an average character is worth about 200 gems (Iron) and 586 gems (Golden); one per opening would return about 286% (Iron) and 276% (Golden). Pick before enabling: a 4 to 5% (Iron) and 7% (Golden) character chance, higher chest prices, or smaller coin/power-up contents. (Chest character code stays OFF.)
2. Daily power-up value: pool limited to cheap types? (one a day is about 1,820 gems a month, about $20.)
3. Earned chests: any granted? They eat the 100 gem-equivalent budget.
4. India price points: final amounts for the cheaper bundle.
5. Where VIP sits now: confirm so the main thread leaves it alone (decided: right-side column on home).
6. Day boundary for Daily Rewards (local vs UTC), streak bonuses, ad-free VIP rule.
7. Whether bundles can be bought with gems only or money too; whether the starter pack disappears after N days.
8. Replay farming below 3 stars (a 1-star level pays 130 coins every replay): limit full payouts to the first 3 replays per day per level?
9. Coin value up about 60%: check that 20 coins per gem packs still make sense.
10. LevelPlay app key and ad unit ids (owner supplies before release); ad SDK consent/age gate wording.
11. (from helper H1) Shop badge text is fixed at exactly 16 sp and does not grow with the text-size setting, so the card does not fill up. Acceptable?
12. (from helper H1) Badged shop cards now have a smaller icon (about 95 px instead of 130) to make room for the lowered badge. Alternative: a taller card with the amount and price rows moved down.
13. (from helper H1) The older page-style map (ShowMapPages) medallion names have no backing and a fixed text box; long names at 130% may spill. Left alone because it is not the main map.
14. (from helper H5) When gems are short for a revive, the shop opens on the Gems tab; it does not yet highlight the smallest pack that covers the shortfall (RunSession.ReviveShortfall gives the missing gems). Is opening the Gems tab enough for now?
15. (from helper H5) Should the ad revive wait until the age gate / consent is answered? Right now it is offered as soon as an ad is ready.
16. (from helper H5) No analytics layer exists in the code, so the events revive_offer, revive_taken, chest_open, gem_pack_bought etc. are not emitted yet. Add an analytics layer (which service)?
17. (from helper H5) LevelPlay package version 9.5.1 may not resolve on Unity 6000.3 (fallback 9.2.0); AD_ID permission and consent calls must be checked before release.
18. (from helper H3) Character gem prices are not set yet (CharacterSpec.GemPrice = 0), so the 25% duplicate refund is 0 until set (owner price list: 150 / 300 / 700 / 1,500 gems for Uncommon / Rare / Epic / Legendary).
19. (from helper H3) After "Get N gems" from the chest screen the player lands in the shop gem flow and Back goes to the home screen, not back to the chests. Fine?
20. (from helper H3) When the gem shortfall is bigger than the largest pack, the popup offers the largest pack, which does not cover it alone; should it say so?
21. (from helper H3) Should the (i) text list the 7 power-up names? Currently exactly the Point 3 text.
22. (from helper H2) Promo prices are invented because the doc has none: gem packs about half of list ($0.49, 0.79, 1.29, 2.99, 5.49, 10.49, 24.99, 44.99, 59.99), coin packs half the gem price. The promo is OFF so it has no effect. Accept, or use list price?
23. (from helper H2) Badges: the current assignment is Popular on the 100-gem pack and Best value on the 1,000-gem pack. The doc's fallback would put Best value on the 15,000 pack (best $/gem). Which?
24. (from helper H2) The first-purchase flag is stored in the local save per pack id (reset only by clearing the save). Server-side flag later fine?
25. (from helper H2) "Get gems" on an unaffordable coin pack opens the Gems section; in release builds that prompt says "not available yet" (no billing yet). Acceptable for now?
26. (from helper H2) The two switches (first-purchase double, launch promo) are PlayerPrefs only; no settings UI. Add a dev-build toggle?
27. (from helper H4) Your gem shares (2.5, 2.5, 3.5, 4.5, 5 percent) cannot give 70 to 110 gems per region on one spot per row (2.5% places about 62, 5% about 129). Spot share tuned instead to 3.2, 3.2, 3.5, 3.8, 4.1 percent: measured 78 to 103 gems per region, rarity exactly 70/22/7/1. Keep your exact percents (and change the target band) or keep the tuned ones?
28. (from helper H4) Before this change the game placed only about 3 to 4 gems per region (owner rule 2026-10-24: 8% per level for one gem, capped at 1 or 2 per level). The 70 to 110 target reverses that; the cap is removed. About 80 to 100 gems per region is large next to the region skip price of 100 to 116 gems. Confirm the intent.
29. (from helper H4) Milestone gems: the shipped code paid 0. Restored from level data: L15 first clear pays 5, L5 and L10 roll 1 to 3 gems at 25% (L10 guarantees 1). Confirm amounts.
30. (from helper H4) Replay farming: 1- and 2-star levels pay full coins (130 / 190) and gems on every replay until 3 stars; no daily limit yet (open item 8).
31. (from helper H4) A pickup overflow bug in the simulation (far pairs counted as touching) was fixed; it had silently dropped many placed gems.
32. (from helper H2) Pack icons are now plain images (touch-target rule), so tapping an icon no longer opens the pack-details popup and the INR price line in it is not reachable. Add a small info button (>= 48 dp) back?
