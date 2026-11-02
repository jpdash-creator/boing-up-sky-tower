# Approval packet T73: Region 2 (Mushroom Grove) new special platforms, 3 sheets x 2 options

Status: PENDING OWNER APPROVAL. Nothing generated, no credits spent (only free read-only calls: `openart_account_get`, `openart_model_cost`, `openart_model_form_get`, `openart_creation_get`). Design: docs/30-regions-2-to-5-platforms.md section 3 (kinds 2.1 to 2.7). Structure copied from what was really run for region 1 (T61/T62): 3 sheets, 6 rows each, 2 options per sheet.

Ground rules from the owner (2026-11-01) applied to every prompt: no code-drawn cues (no arrows, rings, icons or speed lines drawn by code), so the PAINTED art itself must make each kind's behaviour readable by look (shape, pose, material), but the sheets contain no arrows, no text, no characters and no sky clouds.

## 1. Purpose
Painted art for the 7 new Region 2 platform kinds, sliced by Tools/art_pipeline/slice_width_sheet.py (or its region 2 bake script, see section 8) into baked widths:
- Sheet A (helpful): Squish Cap, Sprout Lift, Puff Float
- Sheet B (hard): Fairy Ring, Orbit Cap
- Sheet C (hazard): Dizzy Spores, Spore Pop (3 painted poses: calm, swollen, flat)

## 2. MCP and tool
OpenArt MCP (connector `mcp__73f004bd-92f2-4e13-8abf-f0c13bc6ba8f__`), tool `openart_generate_image`, 3 calls (sheet A, sheet B, sheet C), run one after the other (a parallel submit hit PARALLEL_LIMIT in T63).

## 3. Model and mode
`byte-plus-seedream-5-lite` (Seedream 5 Lite), mode `image2image`, resolution 2K, aspectRatio 4:3, imageCount 2 per call, no seed field, autoEnhancePrompt false.

Reference (ONE image, attached by a REAL url, never by id alone, because a reference with an empty url is silently dropped):
- The owner-picked Region 1 platform sheet B2 (dandelion + thorn bush), the result of T61 creation `oBkk0YNpt0tEdA37ZWD0`, second resource, id `oGjsuZhO6qldZvhT7dtm`, url:
  `https://cdn.openart.ai/openart-ai/production/2026-10/create-image/eKcljWsHfmOD6SYI79NB/02179345903088222877b9ff06629ca98d99f0267ba452b267977_0_1793459141188_42a712df.png`
  (visualReferences item: type image, id `oGjsuZhO6qldZvhT7dtm`, that url, label "region 1 platform sheet (style only)"). It is already an OpenArt result, so NO upload is needed. I inferred that "second resource = local B2.png" from the order A1,A2 / B1,B2 and the log note "owner picked ... B2 (dandelion+thorn)"; I did not compare pixels. Before the call I will confirm by viewing the url or the local Art/_candidates/reg001_new_platforms/B2.png. If the owner prefers another picked sheet (A2 hive, C2 ladybug+seesaw, D1 wind petal, D2 snap daisy), D1/D2 are creation `3TezJyYTrj2k9waM1PkK` (resource ids `h0Oex2ep59bN7rSrO4BQ` and `j3FwtA4ODU094yqw4M6O`), C2 is creation `FgODnEEUDPPCCovIZt62` (second resource `sdPB07UYhChJLMuhDful`), with their cdn urls. Only the local files exist for the sheets otherwise; if a local-only file were chosen, a free `openart_upload_import` would be needed first (I will tell you before doing it).
- Optional second reference (not used unless you say so): the style bible url `https://cdn.openart.ai/openart-ai/production/2026-10/create-image/eKcljWsHfmOD6SYI79NB/021792344354602ba082e75ad6045f729531cc716e805ec9f2b21_0_1792344400206_ecb4d010.png`. It was the T61 reference; it can pull the layout toward the old scene, so default is the platform sheet only.

## 4. Prompts, word for word
Each prompt = STYLE LOCK PREFIX + sheet text + STYLE LOCK SUFFIX. Same wording scheme as the T61 prompts that worked (6 rows, groups, length ratios), palette changed to the Mushroom Grove palette. No competitor game names anywhere.

STYLE LOCK PREFIX (all three sheets):
"Match the art style of the reference image exactly (outline weight, neon rim light and glow, glossy shading, colour saturation), but draw only the new subject described here, not the reference's layout or objects. A sprite sheet of thin side-view platform slabs for the 2D game region 'Mushroom Grove' (a dim magical forest floor of giant purple and teal mushrooms lit from within). Layout: horizontal rows on a flat solid #808080 background, one platform per row, grouped by kind with a clearly larger empty gap between groups, every platform left-aligned to the same left margin, nothing overlapping. All platforms have exactly the same thickness and the same side-view style, and within a group differ only in length. Every platform is a slim flat slab seen straight from the side: a perfectly flat straight top edge that clearly reads as a walkable surface, with a clean rounded cap at both ends."

SHEET A text (3 kinds x 2 rows = 6 rows):
" Six rows in three groups of two rows. In each group the first row is 5 times as long as it is thick and the second row 10 times. Group 1 (rows 1 and 2), Squish Cap: the slab is a glossy purple toadstool-cap slab with soft rubbery white spots; its flat top is visibly soft and jelly-like with a gentle shallow dimple pressed into the middle of the top, the rim bulging slightly outward at the sides as if it is being squashed, and a faint cyan underglow beneath. Group 2 (rows 3 and 4), Sprout Lift: the slab is a teal leaf-shaped cap slab sitting on top of a short thin curled green sprout stem that hangs below the middle of the slab, with a small closed green bud at the foot of the stem; the stem and bud are clearly part of the platform and are small compared with the slab. Group 3 (rows 5 and 6), Puff Float: the slab is a pale lilac puffball slab, soft and fuzzy along its top and sides like a dandelion-fluff cushion, with a few tiny glowing spore dots drifting upward just above it."

SHEET B text (2 kinds x 3 rows = 6 rows):
" Six rows in two groups of three rows. Group 1 (rows 1 to 3), Fairy Ring: each row is TWO matching purple mushroom-cap slabs placed side by side on the same level with a clear empty gap between them (the gap is about one quarter of the whole length and shows the grey background), and a ring of tiny white mushrooms painted on the ground under the gap; both halves have rounded outer caps and rounded inner ends facing the gap; the rows are 9 times, 12 times and 16 times as long as they are thick measured over both halves and the gap, and the gap has the same width in all three rows. Group 2 (rows 4 to 6), Orbit Cap: the slab is a teal mushroom-cap slab whose whole body has a round glowing ring pattern painted on its side, like a planet with a thin orbit line engraved around it, with two small glowing cyan beads on the ring; no separate ring or line outside the slab; the three rows are 4 times, 7 times and 12 times as long as they are thick."

SHEET C text (2 kinds x 3 rows = 6 rows):
" Six rows in two groups of three rows. Group 1 (rows 1 to 3), Dizzy Spores: the slab is a red-brown toadstool-cap slab with pale spots; swirling yellow-green spore dust puffs with spiral curls are wrapped around the top and ends of the slab, a dizzy spinning look; the three rows are 4 times, 7 times and 12 times as long as they are thick. Group 2 (rows 4 to 6), Spore Pop, three poses of the same puffball platform, all three rows exactly 7 times as long as they are thick at the base: row 4 calm pose, a round warm-pink puffball slab with a smooth glossy skin and a small relaxed seam line; row 5 swollen pose, the same puffball inflated and bulging upward with a taut stretched shiny skin, cracked lines and tiny bursting sparks of spores at the crack, ready to burst; row 6 flat pose, the same puffball deflated and wrinkled, flat and floppy with a dull skin and a wrinkled cap, a little dust beside it. All three poses keep the same flat bottom edge and the same left margin."

STYLE LOCK SUFFIX (all three sheets):
" Decoration is sparse and non-repeating so nothing looks stretched. No sky clouds, no characters, no arrows, no text, no watermark. palette: purple, teal, glow cyan. cute chibi cartoon with neon rim light and glow, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background"

Why these shapes read without code cues: Squish Cap = visibly soft dented cap; Sprout Lift = stem and bud under the cap (the Sim rises it, the stem is painted); Puff Float = fuzzy light cushion with drifting dots; Fairy Ring = the hole is painted; Orbit Cap = ring motif (the motion is read from the Sim); Dizzy Spores = swirl dust; Spore Pop = three Sim-chosen poses (calm, swollen, flat).

## 5. Cost (free quote, `openart_model_cost`)
Config priced: byte-plus-seedream-5-lite, image2image, 2K, 4:3, imageCount 2 = 15 credits per image, 30 credits per call (price finalised at generation; a different resolution, count or reference setup costs differently).
- Jobs: 3 calls x 2 images = 6 images
- **Total: 90 credits**
- Current balance (`openart_account_get`, Plus plan): **4,538 credits**
- Balance after: **4,448 credits**
(Note: T61 quoted 5,273; the real balance today is 4,538.)

## 6. Output handling
- Each call downloaded to Art/_candidates/reg002_new_platforms/ as A1 A2 (sheet A), B1 B2, C1 C2; a contact sheet shown to you; you pick one option per sheet (or ask for a redo, which needs a NEW approval).
- Rows are checked first: same thickness, no clouds, characters, arrows or text, no kind missing, Fairy Ring gap identical in all rows, Spore Pop three poses with the same base. A sheet that fails is reported to you; no automatic retry.
- Logged in Content/assets/assets_log.csv (batch T73, creation ids, credits used, balance remaining), report of actual credits used and the remaining balance after each call.

## 7. Alternatives and savings
- One option per sheet (imageCount 1): 3 x 15 = 45 credits, saves 45 (balance after 4,493). Riskier: no second option if a row set drifts.
- Merge sheets B and C into one 12-row sheet: 30 instead of 60 for those two but T61 v2 showed 12 rows drift; not recommended.
- Cheaper model: none quoted here; Seedream 5 Lite is the model that matched the region 1 look, and a different model changes the style.
- Redo of one sheet later: 30 credits (needs a new approval).

## 8. After generation (no credits)
1. Download picks, remove background and split rows (`process_art.key_image` / `split_sheet`), contact sheet, run `Tools/art_pipeline/qa_art.py` on every piece (CUX-9: no clipped objects, no grey halo, consistent scale and pivot).
2. New `Tools/art_pipeline/bake_region2_new_platforms.py`, a copy of `bake_region1_new_platforms.py` (KINDS table with sheet, rows, widths per kind; MEASURE for top/bottom of each slab; ONE uniform scale per kind so thickness is 0.5 u at 167 px per u; output to `Assets/Art/Regions/region_002_mushroom_grove/tiles/<kind>_widths/reg002_<kind>_w<width>.png` plus `platform_set.json`, and a `baked_contact.png`). Kind keys: squishcap, sproutlift, pufffloat, fairyring, orbitcap, dizzyspores, sporepop. Special handling needed in the copy:
   - Fairy Ring: cut/repeat only inside each half (never through the gap) so the hole keeps its size; bake the width (at least 4.4 u) from the 9x/12x/16x rows.
   - Sprout Lift: stem and bud saved as a separate sprite (the cap rises, the stem is drawn by the view), like the Seesaw pivot knob.
   - Spore Pop: one piece per pose (calm, swollen, flat) per width, chosen by the Sim state; widths cut from the single 7x row of each pose.
   - Sheet A rows come in two lengths (5x, 10x), so the slicer works from those.
3. `ArtCatalog` entries: `Assets/_Game/Gameplay/ArtCatalog.cs`, `Editor/ArtCatalog/ArtCatalogBuilder.cs`, regenerate `Resources/ArtCatalog.asset`; picks recorded in Content/assets/picks.json; update CATALOG.md.
4. Screenshots at 9:16 and 9:21 in game before it ships (CUX tests, category `CriticalUX`, must be green). Sim/code for the kinds is separate work (doc 30, about 63 h) and is not part of this approval.

## 9. Open questions for the owner
1. Which picked Region 1 sheet is the style reference (default here: B2)? It is a green meadow sheet; the prompts push the palette to purple, teal and cyan.
2. Sheet A uses only 2 painted lengths per kind (5x, 10x) instead of 3 (4x, 7x, 12x) to keep to 6 rows. OK, or 9 rows (3 kinds x 3) with a higher drift risk?
3. Orbit Cap: the doc says "glowing ring painted behind"; I changed it to a ring pattern on the slab itself because anything outside the slab breaks the row slicing and a code-drawn ring is not allowed. OK?
4. Spore Pop poses are all the same length (7x) and cut by the slicer for other widths; the swollen pose will be taller than the slab, so its thickness must be measured at the base. OK?
5. Confirm the exact batch (model, 3 sheets x 2 options, prompts above, reference above, 90 credits) with a clear yes before I run anything.
