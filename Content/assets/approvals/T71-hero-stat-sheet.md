# Approval packet T71: Hero sheet screen frame (4 layout variations, empty)

Status: PENDING OWNER APPROVAL. Nothing generated, no credits spent. Only free read-only tools were used (`openart_account_get`, `openart_model_cost`, `openart_model_form_get`, `openart_upload_list`). Source: owner request list 30 item 2.

## 1. Purpose
Art for the new refined **Hero sheet** screen (opened by tapping the hero on the home screen, its (i) icon, or a hero on the Heroes screen), in our own colour scheme and as sleek as the Shop pages (see E:\Unity\logs\shots\shop-tab-chests_9x16.png and the Rewards/ShopChrome look). Modelled on your reference (dark portrait UI: header bar, portrait frame top right, two-column stat rows with dark plates and gold text, section tabs, feature rows).

The sheet contains (all drawn by code except the empty frame art): header bar "HERO" with back/close; big portrait frame (hero picture, e.g. Momo); name plate; rarity banner (Common grey, Uncommon green, Rare blue, Epic purple, Legendary gold: the code tints it and draws the text); region name; two-column stats table with the six stats of docs/29-character-stats.md (jump launch, top speed, control, wall boost, coin find, gem find); a second section for derived numbers; the hero power text.

The art = **4 different layouts of the EMPTY sheet frame**, same look (you chose 4 layouts):
- **A** portrait top-left, stats table below.
- **B** portrait top-right (as your reference), rarity banner under it.
- **C** full-width portrait banner on top, two stat columns beneath.
- **D** tall portrait on the left, stat rows stacked on the right.

Empty means NO text, NO numbers, NO hero, NO icons inside any slot. Plates are clearly rectangular with a clear empty interior, symmetrical, slice-friendly (9-slice), 9:16 portrait 2K, flat #808080 outside the sheet.

## 2. MCP and tool
OpenArt MCP (connector `73f004bd-92f2-4e13-8abf-f0c13bc6ba8f`), tool `openart_generate_image`, **4 calls** (one per layout, 1 image each).

## 3. Model and mode
`byte-plus-seedream-5-lite` (Seedream 5 Lite), mode **image2image**. Params per call: resolution 2K, aspectRatio 9:16, imageCount 1, autoEnhancePrompt false (default), no seed field exists in this form.
Reference images (visualReferences), same for all 4 calls:
1. Style bible resource `vVnbzqvg1L5uSFCKydUE` (our locked art style; used by T1, T48, T65).
2. **Optional but recommended, needs your OK:** `Art/_candidates/rewards_ui/installed_contact.png` (our installed Rewards panels, the closest UI look). It is NOT in `openart_upload_list` yet (the list holds only characters, map strips and mannequins), so it would have to be uploaded. Uploads cost no credits; I will only do it if you say yes. Without it, only reference 1 is used and the UI look comes from the prompt.
The monk painting (z3SsdRJRqpKz1192UlAL) is NOT used (not a UI reference).

## 4. Full prompts, word for word
STYLE LOCK (first sentence of every prompt): "Match the art style of the reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subject described here, not the reference's layout or objects."
COMMON BODY (used in all four, between the lock and the layout sentence): "A completely empty game screen frame for a sleek mobile game character sheet screen, seen straight on, portrait, filling the whole image. Dark navy glossy rounded plates with a thin gold edge and a subtle inner highlight, a sky-blue and cloud-white accent, gold trim. Every plate is a clean plain rectangle with rounded corners, perfectly symmetric, with a large empty flat interior, evenly sized corner radius, ready to be cut into nine slices. A slim dark navy header bar across the top with an empty round button socket at its left end and an empty round button socket at its right end and an empty middle. Sleek and tidy, lots of breathing room between plates, nothing overlapping."
COMMON END: " Absolutely empty: no text, no letters, no numbers, no hero, no character, no picture, no icons, no symbols, no watermark inside any plate or anywhere. Thick consistent dark navy outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background outside the sheet."

**Layout A (portrait top-left):**
STYLE LOCK + COMMON BODY + " Layout A, top to bottom: the header bar; below it on the left a large square gold-framed empty portrait frame with a dark empty inside; to the right of the portrait frame, stacked, one wide empty name plate, one wide empty smaller rarity banner plate, one wide empty region plate; below that a full-width section tab strip of two empty tabs; below that a two-column stats table of six equal wide empty dark rows (three rows by two columns, each row split by a thin gold divider into a small empty icon square on the left and a wide empty value area on the right); below that a second wide empty panel with three empty rows for derived numbers; at the bottom one wide empty hero power panel." + COMMON END

**Layout B (portrait top-right, as the reference):**
STYLE LOCK + COMMON BODY + " Layout B, top to bottom: the header bar; below it a slim empty level bar plate across the full width; below that on the left an empty wide name plate with an empty region plate under it, and on the right a large square gold-framed empty portrait frame with a dark empty inside, with a wide empty rarity banner ribbon plate directly under the portrait frame, the same width as the frame; below that a full-width section tab strip of two empty tabs; below that a stats table of six stacked full-width empty dark rows (each row split by a thin gold divider into a small empty icon square plus a wide empty label area on the left and a separate empty value plate on the right); below that a second empty panel with three empty rows for derived numbers; at the bottom one wide empty hero power panel." + COMMON END

**Layout C (full-width portrait banner on top):**
STYLE LOCK + COMMON BODY + " Layout C, top to bottom: the header bar; below it a large full-width landscape gold-framed empty portrait banner with a dark empty inside, taking about one third of the height; overlapping nothing, directly under it a centred wide empty name plate, and under that a row with one empty rarity banner plate on the left and one empty region plate on the right; below that two equal columns side by side, each column holding three stacked empty dark stat rows (each row with a small empty icon square on the left and a wide empty value area on the right, thin gold divider between), six rows in total; below that a full-width empty panel with three empty rows for derived numbers; at the bottom one wide empty hero power panel." + COMMON END

**Layout D (tall portrait on the left, rows on the right):**
STYLE LOCK + COMMON BODY + " Layout D, top to bottom: the header bar; below it a slim empty name plate across the full width with an empty rarity banner plate on its right end; below that, a tall narrow gold-framed empty portrait frame on the left side taking about 40 percent of the width and half of the height, with a dark empty inside, and an empty region plate directly under it; on the right of the portrait frame, six stacked equal empty dark stat rows (each row with a small empty icon square on the left and a wide empty value area on the right, thin gold divider between); below those, spanning the full width, a second empty panel with three empty rows for derived numbers; at the bottom one wide empty hero power panel." + COMMON END

No competitor game names appear anywhere.

## 5. Cost (from `openart_model_cost`, byte-plus-seedream-5-lite, image2image, 2K, 9:16)
- 1 image per job: **15 credits** per job. imageCount 2 per job: 30 credits for the job (also 15 per image).
- **Recommended: 4 calls x 1 image = 4 jobs = 60 credits** (one image per layout A, B, C, D).
- Current balance (`openart_account_get`, Plus plan): **4,643**. Balance after: **4,583**.

Both options compared (the price per image is identical, so batching saves nothing):
| Option | Calls | Images | Credits | What you get |
|---|---|---|---|---|
| 4 calls x 1 image (recommended) | 4 | 4 | 60 | one try of each of A, B, C, D |
| 2 calls x 2 images | 2 | 4 | 60 | only 2 layouts, 2 tries each (a call has a single prompt, so 2 x 2 cannot cover 4 layouts) |
| 4 calls x 2 images | 4 | 8 | 120 | two tries of each layout (safer if slicing fails) |

## 6. Output handling and what happens next (no more credits)
1. Results saved to `Art/_candidates/hero_sheet/` (A, B, C, D) and shown to you labelled; you pick the layout(s) to keep.
2. Grey #808080 keyed out and QA run (`Tools/art_pipeline/qa_art.py`, CUX-9); plates cut into 9-slice sprites (header, portrait frame, name plate, rarity banner, stat row, tab, panel) with border insets recorded; installed as `Assets/Resources/Art/Ui/hero_sheet_<variant>.png` (plus per-plate slices).
3. Build `HeroSheetScreen` (code): header with back/close, portrait (hero art), name, rarity banner tinted and drawn by code (grey, green, blue, purple, gold), region name, six-stat table (jump launch, top speed, control, wall boost, coin find, gem find from `CharacterStats`), derived-numbers section, hero power text. Text and values drawn by code, nothing baked into art. Accessibility labels on buttons.
4. Entry points: tap the hero on Home, the (i) icon, and a hero card on the Heroes screen.
5. Tests: EditMode tests for stat rows against `CharacterStats` (ids CUX-3/CUX-5), and the `CriticalUX` layout test for the new screen.
6. Layout audit at 9:16, 9:19.5, 9:21 and 3:4, plus 130 percent text scale; touch targets at least 48 dp, safe area honoured, screenshots reviewed in `E:\Unity\logs\shots`. Needs green CriticalUX suite before commit. Build reported with its vMAJOR.MINOR.HOTFIX.BUILD version.

## 7. Risks and alternatives
- AI plates can come out with uneven corners, stray ornaments, or an inexact layout; if a layout does not slice cleanly I come back with a NEW ask (15 to 30 credits). Fallback: code-drawn plates reusing the existing Shop/Rewards slices (0 credits) for any layout that fails.
- Cheaper alternatives: only 2 layouts (A and B) = 30 credits, saves 30; or only your pick of one layout = 15 credits, saves 45. Generating no art at all and building the screen from the existing Shop/Rewards 9-slice plates = 0 credits, saves 60.

## Open questions for the owner
1. OK to upload `installed_contact.png` (free) as a second style reference, or style bible only?
2. All 4 layouts (60 credits), or fewer?
3. Approve the exact batch above: 4 calls, 1 image each, 2K, 9:16, 60 credits, balance 4,643 to 4,583?
4. Header buttons: confirm back on the left and close/(i) on the right (the art leaves both sockets empty).
