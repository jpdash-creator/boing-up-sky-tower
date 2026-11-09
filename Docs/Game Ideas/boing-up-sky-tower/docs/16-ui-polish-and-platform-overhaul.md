# 16 UI polish and platform no-stretch overhaul (owner decisions 2026-10-23)

Source: owner posted three reference screenshots of a polished mobile game (loading screen, chest shop, gem shop) and asked for the same level of polish in images, icons, look and feel and fonts, without copying (names, characters, logos and the skull motif are NOT to be reused; layout conventions and polish level are the reference). Owner also reported that platforms look stretched. Every item below was answered by the owner in chat.

## A. Style direction (replaces "daylight glass")
- Chunky toy-plate style on every screen: thick dark outlines, bevelled gradient plates, glossy rendered icons, ribbons and banners, scene art behind the UI. The in-run HUD gets small chunky pills and round buttons, kept thin along the screen edges (CUX-3).
- Colour world: sky blue, cloud white and gold (not brown wood). Button meaning stays fixed: green = main action, yellow = secondary/bulk, blue = back, red = notification badge.
- Fonts: owner delegated the choice. Pick a heavier rounded display face (candidates shown as a sample sheet; free fonts only, OFL), thick dark outline on every label, body copy on plates. Must keep CUX-5 (at least 36 px body, contrast, 130 percent scale).
- Motion: rich (idle breathing on host characters, squash on press, shine sweep on primary buttons, coins and gems fly to the wallet, chest-open burst, springy popups, tab slide, loading bar glow). Reduced motion switches all of it off; nothing flashes more than 3 per second. Haptics default off (toggle kept).

## B. Art production
- OpenArt for everything visual (logo, UI kit, icons, chests, gems, ribbons, scene backgrounds, host scenes), all locked to the owner style bible reference. Approval packet and a clear yes for every batch; ceiling about 3,000 credits for the whole overhaul (balance about 8,165 on 2026-10-23); no competitor names in prompts.
- Redo in the new style: buttons, popup frame, close X, panels, plates, ribbons, pills, badges, tabs, coin and gem, power-up icons, logo, host scenes.
- KEEP as approved (not redone): in-run backgrounds (5 regions), platform sheets as a look (but see section E), Momo player sprite, the 10 painted map segments (reused, see D).

## C. Structure
- Host character: Momo, a different painted scene per screen (shopkeeper counter, map explorer scene, ...); these are scene paintings, not equipment.
- Logo: original OpenArt wordmark "FLUFFY CLIMBERS / TOWER JUMP" with Momo bouncing through it, 2 candidates to pick from.
- Persistent 5-tab bottom bar: Shop, Chests, PLAY (centre, biggest), Characters, Rewards; selected tab yellow with a label; blue back button on sub-screens; red badge for news. (The former Gear tab became Chests and the Daily tab became Rewards; gear was removed 2026-10-24 and 2026-11-01, docs 20, 26.)
- Top currency bar: three dark rounded pills: Coins, Gems, total Stars.
- Loading screen: full-bleed key art (Momo, tower, floating power-ups), logo, real progress bar tied to asset warm-up, rotating one-line tip, Momo bounce.
- Results: full ceremony (banner drops, three stars pop one by one with sound, coins and gems count up and fly to the wallet, chest teaser, Next/Retry/Menu in green/yellow/blue).
- Level screen of a region: winding path of 15 round nodes on the region's painted background, 3 stars under each, region ribbon on top, Endless as a bold banner at the bottom, boss node larger with a crown, padlock explains why locked.

## D. World map (replaces the long stitched scroll as the main screen)
- 30 regions in total, 6 painted sheets of 5 regions, swipe pages with page dots; tap a small round medallion to open the region.
- Design all 30 region names and themes now (text, free) for owner approval; regions 6 to 30 are marked Coming soon.
- The 10 approved painted segments are reused as the backgrounds of regions 1 to 10; new paintings only for regions 11 to 30.
- 30 medallion icons (signature object in a chunky round frame, star ribbon, padlock): 5 sheets of 6; regions 1 to 5 real art first, the rest silhouettes until painted.

## E. Platforms: no stretching ever
- Finding (2026-10-23): level platform widths are continuous from 1.5 to 10 units, but each region has only three normal pieces (long, medium, short), so most platforms get a stretched middle or a shrunk piece.
- Decision: fixed width set with a real painted, unscaled piece for every width, for all 5 approved regions with the identical set: normal 1.5, 2, 3, 4, 6, 8 units; crumble, moving and bounce each in 2 widths; levels snap their platform widths to this set, bot proofs and level hashes are regenerated.
- Hard rule (added to CUX-9): no image may be scaled non-uniformly; only flat-colour 9-slice middles may stretch. Acceptance: a contact sheet of every platform width for all 5 regions approved by the owner first, plus an automatic CriticalUX test that fails on any non-uniform scale in platforms, buttons, icons and panels.

## F. Order
1. Platform fix for the 5 regions. 2. UI vertical slice (style kit, Loading, Shop, tab bar), owner approves on the phone. 3. All other screens. 4. World map pages and medallions. 5. Play Store assets (icon, feature graphic, screenshots) after the in-game screens are approved. Feature work (chests, characters, subscription, shop tabs) waits until the look is settled.

## G. In-run screen (owner references: two gameplay screenshots, 2026-10-23)
- HUD, all icon-first (a 1-word outlined caption at most, run HUD wordless), every icon with an accessibility label and a distinct shape: tower progress gauge (our minimap: slim vertical mini-tower with Momo's height, finish line, stars and gems left, the rising danger line); goal chip like a quest tracker with the goal icon and progress; thin chunky pills for Floors, Coins, Gems, Stars; round cog pause button with a red notification badge; power-up stack on the right with glossy icons, count badges and a draining ring; combo flame meter with a pop per step. Left-handed mirror kept.
- Pause, game over (failed) and level intro use the same chunky popup family: ribbon title, big icon buttons (Resume, Retry, Home) with tiny captions, Momo reacting in a corner; Retry is the biggest green button and one tap (CUX-7).
- Feedback: floating '+N' numbers with a coin/gem/star icon at each pickup and a wallet-pill bump; landing dust, squash on landing, jump trail, wall-bounce spark; chunky SPEED UP and COMBO banners that never cover the next platforms; danger line as a glowing rising hazard with warning icons and a pulse as it nears (no flashing above 3 Hz); faint region-themed foreground particles; a ring burst and glow on star pickups. All of it off under reduced motion.

## H. Progress log
- 2026-10-23: region 1 platform widths approved and baked (normal 1.5, 2, 3, 4, 6, 8 and the 10 unit floor, plus crumble, moving, bounce in 2 and 4 unit widths from the approved slabs plus overlay parts); level widths snap to the set; renderer draws one exact piece; tool Tools/art_pipeline/slice_width_sheet.py and bake_specials.py. Regions 2 to 5 follow with the same prompts after approval.

## I. Owner issue list 14 and its answers (2026-10-23, after the first UI slice)
The five reference screenshots (SS1 loading, SS2 chest shop, SS3 gem shop, SS4 and SS5 gameplay) were recovered from the chat transcript and compared with the game. Owner verdict: the slice looked bad (wide, wordy, mixed styles). Decisions:
- Bottom bar = 5 separate square icon buttons (SS2 style): Menu bar Shop, Chests, PLAY, Heroes, Rewards; selected one yellow with a tiny caption, PLAY raised and green; red notification badges.
- Shop like SS2/SS3: the bottom row of 5 icon tabs switches sections (since doc 26: Bundles, Chests, Power-ups, Coins, Gems, with the Subscription as a separate button; VIP is gone), a back arrow below, a Momo shopkeeper painted header (OpenArt, approved T49), ribbon title, three-column icon cards (icon, amount, green price button), no top tab rows, no promo words; details only on tapping a card.
- Backgrounds: calm the current art in code (remove the pale wash, soften sparkles and flowers so platforms and Momo stand out); plain sky-blue gradient with soft clouds for Settings and Levels. No new background paintings.
- Pause = a round pause button top right. Popup actions = round icon buttons with a one-word caption, Retry biggest and green.
- Settings = icon rows with switches on a clean cream card. Run HUD: tower gauge for floor and goal, gems and stars as pills, icon banners for combo and speed-up, icon hint instead of the intro sentence.
- Front screen: currency pills on top, cog (settings) icon top right, red badges, and a loading screen with a green progress bar (key art painted later after a packet).
- Art: T49 icon sheets A and B and the Momo shop scene (90 credits approved).

## J. Owner issue list 17: economy (2026-10-24)
1. **Power-ups are bought with coins only.** The gem price and the gem Buy button are removed (catalog gem price 0). Prices: Flight, Rocket, Slow Time 1500 coins; Double Jump, Wall Stick, Shield, Magnet 1000 coins.
2. **Replays.** Once a level has been cleared (any stars), every replay pays 67 percent of the coins (clear, stars and combo) and shows no gems at all: the gem pickups are not spawned, and no gem is paid. The level popup shows a 67% pill.
3. **Gems are rare** (owner answer "Rare"). Only the planned gem pickups pay; the milestone gem payout (L5/L10/L15 `rewards.gems`) and the hidden-gem bonus are switched off so all sources count once. Per level: ordinary levels 8 percent for exactly one gem, never two; levels 5 and 10 60 percent for one gem; level 15 always at least one and 40 percent for a second (the only double). The reward calculator caps the payout again (1, or 2 on level 15). Endless has no gems.
4. The winding level line is one solid translucent line with no dots (stamps densified).
Build changes: LevelBuilder.PlannedGems, RewardCalculator.MaxGems, RunSession(stripGems), PowerUpCatalog prices, ShopScreen card. Golden level hashes re-rolled for this change.
