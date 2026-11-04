# T78 DRAFT: Region 4 (Candy Canyon) new special platforms, sheet texts only (3 + 2 + 2 kinds)

Status: DRAFT SHEET TEXTS, NOT an approval packet. Nothing generated, no credits spent, no OpenArt call made. The lead assembles the packet (purpose, MCP and tool, model, cost quote, output handling, alternatives) and asks for the owner's explicit yes before any call. Design and sheet row plan: docs/30-regions-2-to-5-platforms.md section 14.2 (the baked widths and pose numbers come from that table). Method as in T74: text2image, 3:4, six rows, no reference, three calls A, B, C.

## Substitutions in the T74 common prefix and suffix
- Region: 'Candy Canyon' (a sweet canyon of frosting cliffs, lollipop trees and jelly pools). Colours: pink, mint, cream, caramel brown, strawberry red.
- Prefix wording otherwise unchanged (EXACTLY SIX horizontal rows, flat solid #808080 background, one platform per row, groups of kinds with a larger gap between groups, every platform left-aligned to the same left margin, same thickness and style in all rows, a perfectly flat straight top edge that reads as a walkable surface, clean rounded cap at both ends). Exceptions are stated per sheet (the bowl lips, the vent bubbles, the taffy twist).
- Suffix palette line: "palette: pink, mint, cream, caramel brown, strawberry red, licorice black".
- No competitor game names appear in any text below.

Each call sends PREFIX + sheet text + SUFFIX as one prompt. Lengths are given as "times as long as it is thick" (the slab is 0.5 u thick in the game, so 8 times = 4 u).

## SHEET A (helpful: Candy Bowl, Floss Glide, Fizz Vent)

SHEET A text: " Six rows in three groups of two rows. Group 1 (rows 1 and 2), Candy Bowl: a dish-shaped candy slab, a pink-and-white striped candy dish whose walkable floor is perfectly flat and straight, with a raised curled lip at both ends that rises about 0.8 of the slab thickness above the floor (the lips are the only parts above the flat floor), a few rainbow sprinkles lying inside the dish; the first row is 8 times as long as it is thick and the second row 16 times. Group 2 (rows 3 and 4), Floss Glide: a puffy pastel pink cotton-candy cloud slab with a flat top, a small swirl wisp trailing from each end, soft fibre texture on the sides; the first row is 4 times as long as it is thick and the second row 12 times. Group 3 (rows 5 and 6), Fizz Vent: a mint soda-bottle-cap slab with a ridged rim and a round vent opening in the middle of its flat top, with a loose cluster of glossy soda bubbles of different sizes rising straight above the opening to about 4 times the slab thickness in height (leave an empty band above rows 5 and 6 for the bubbles); the first row is 4 times as long as it is thick and the second row 8 times."

Kinds and rows: 1-2 Candy Bowl (8x, 16x), 3-4 Floss Glide (4x, 12x), 5-6 Fizz Vent (4x, 8x). No poses (all static).

## SHEET B (hard: Licorice Belt, Taffy Stretch)

SHEET B text: " Six rows in two groups. Group 1 (rows 1 and 2), Licorice Belt, two animation frames of the same conveyor: a black-and-red licorice conveyor slab with bold diagonal tread stripes leaning to the right and a round caramel roller cap at each end, both rows exactly 12 times as long as they are thick; row 1 frame A, row 2 frame B, identical in every way (same outline, same rollers, same base line) except that the diagonal stripes are shifted by half a stripe. Group 2 (rows 3 to 6), Taffy Stretch, four widths of the same golden pulled-taffy slab: a golden-yellow taffy slab with glossy highlights, with a visibly twisted rope end at each end and a slightly thinner waist in the middle, the same colour and the same twist in all four rows; the four rows are 4.8 times, 6.6 times, 8.6 times and 10.4 times as long as they are thick, and the longer the row the thinner and more stretched its middle looks (row 3 is short and plump, row 6 is long with a clearly thinned waist). All rows keep the same flat bottom edge and the same left margin."

Kinds and rows: 1-2 Licorice Belt (frame A, frame B, both 12x), 3 Taffy narrow (4.8x = 2.4 u), 4 Taffy second (6.6x = 3.3 u), 5 Taffy third (8.6x = 4.3 u), 6 Taffy wide (10.4x = 5.2 u). Poses: Belt 2 loop frames, Taffy 4 widths.

## SHEET C (hazard: Jelly Wobble, Pinwheel Gust)

SHEET C text: " Six rows in two groups. Group 1 (rows 1 to 4), Jelly Wobble, two poses of the same translucent strawberry jelly cube slab, glossy, a shiny red cherry on top at one side, soft inner glow; row 1 calm pose 8 times as long as it is thick, row 2 squashed pose 8 times as long as it is thick (the jelly slightly wider and flatter, the cherry slid toward one side, the top still flat), row 3 calm pose 12 times as long as it is thick, row 4 squashed pose 12 times as long as it is thick; calm and squashed rows keep the same flat bottom edge and the same left margin. Group 2 (rows 5 and 6), Pinwheel Gust, two animation frames of the same rainbow-swirl lollipop pinwheel standing on a short striped stick in the middle of a white candy slab base, the swirl lines curling toward the right; both rows exactly 8 times as long as they are thick measured over the base; row 5 frame A, row 6 frame B, identical except that the pinwheel blades are turned by about 45 degrees; the pinwheel rises above the base to about 3 times the slab thickness, so leave an empty band above rows 5 and 6."

Kinds and rows: 1-2 Jelly 4 u (calm, squashed), 3-4 Jelly 6 u (calm, squashed), 5-6 Pinwheel Gust (frame A, frame B, both 8x). Poses: Jelly 2, Pinwheel 2.

## Notes for the packet
- Baking and binding: docs/30 section 14.2 (pose numbers = `dir` in `platform_set.json`, folder `Assets/Art/Regions/region_004_candy_canyon/tiles/<kind>_widths`). The Taffy wide widths and the Belt and Pinwheel cut/repeat rules are listed there.
- Region 4 also uses the existing Sticky Gum signature art and the generic Crumble and Slippery pieces: no art needed for them.
- Open points to show the owner with the packet: Fizz Vent bubbles reach about 2 u while the gravity zone is 3.2 u high; Taffy is drawn as four swapped widths (small pop) instead of a stretched piece.
- Cost: the same setup as T74 (text2image, 2K, 3:4, 2 images per call) was quoted at 30 credits per call, about 90 credits for three calls; the lead re-quotes with `openart_model_cost` and reads the balance with `openart_account_get` before asking.
