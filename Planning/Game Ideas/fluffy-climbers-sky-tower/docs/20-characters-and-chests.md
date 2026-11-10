# 20. Characters and chests (owner issue list 23, 2026-10-24)

Replaces docs 17, 18 and 19. Gear, pants, boots, hats, the inventory screen, the forge, the character info and power screen and level-clear gear drops are all removed (code, art, tests, tools).

## Characters
- A character is just a character: a set of full-canvas part pictures (1365 x 1649) drawn back to front, listed in its `rig.json` (`order`). Momo keeps his nine parts; the region heroes (Capi, Otto, Bao) use the polygon parts (legL, legR, body, handL, handR, head) made with OpenArt on the polygon doll (Tools/art_pipeline/make_generic_mannequin.py, cut_polygon_character.py).
- Character screen: the chosen character stands on the green land stage (current region backdrop, grass, dirt oval) with one round button that opens the roster (the characters the player has, locked silhouettes for the rest). Tap a character to choose it; the run draws the same parts.
- Later (owner): each region unlocks one new character; the others are bought with gems or come from chests.

## Power-ups
Unchanged: coin shop, stock, 3-slot loadout in the level popup, HUD buttons. Only the power-up section that was on the character screen is gone.

## Chests (replace the inventory tab)
- The second tab of the bottom bar is "Chests" and opens the shop's Chests tab. Gems only: Iron 100 / 1000 (open 10), Golden 300 / 3000.
- Contents: Iron 3 power-ups and up to 1000 coins; Golden 6 power-ups and up to 3000 coins. No gear.
- Old saves with gear fields load fine; the fields are ignored.
