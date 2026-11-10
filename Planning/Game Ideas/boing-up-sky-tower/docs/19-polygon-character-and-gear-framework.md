> **SUPERSEDED by doc 20; gear removed (owner 2026-10-24 and 2026-11-01).** The polygon framework below is still how characters are drawn; everything about gear, gear drops, gear chests and the inventory is removed. Kept as a short note.

# 19. Polygon character framework (owner issue list 22, 2026-10-24; gear part removed)

## The polygon framework (standard character)
Every character has the same polygon parts on a 1365 x 1649 canvas, each an agreed outline with fixed anchor points:
- **HEAD**: a big rounded polygon (x 160 to 1205, y 120 to 1060); ears, horns, antennae and hair may rise above it inside a margin (the head cell is the top 1130 px).
- **BODY**: a rounded trapezoid (x 390 to 975, y 940 to 1320). Anchors: neck, shoulders, hips.
- **HANDS** (arms): two capsule polygons at the sides (x 250 to 420 and 945 to 1115, y 1000 to 1330).
- **LEGS** (with feet): two capsule polygons (x 470 to 650 and 715 to 895, y 1180 to 1520) ending in feet ellipses (y 1470 to 1600).
- **BACK** (optional extra behind the body): a tail, wings or spores, used by Momo's cloud tail.

Draw order, back to front (from each character's `rig.json`): back, legs, body, hands, head.

## Making characters with OpenArt (credits only after approval)
A character is four images (head, body, hands pair, legs pair) from the polygon template, each with only that part's grey polygon on magenta, so the model fills exactly that shape; Momo needs a fifth for his tail. Cut by the polygon outline. Every generation needs the owner's explicit approval of the exact batch (OpenArt credit guardrail).

## Removed with gear
Head, body and boots gear, their rarities and drop tables, gear chests, the inventory grid and the character info screen are gone. Characters are chosen on the Heroes screen (doc 20); chests are the Iron and Gold chests of doc 22; economy numbers are in doc 27.
