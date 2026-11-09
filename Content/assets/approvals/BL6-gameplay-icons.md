# Approval packet BL6: the five Gameplay icons (Relaxed, Easy, Normal, Hard, Insane)

Status: PENDING OWNER APPROVAL. Nothing generated, no credits spent. Only free read-only tools were used (`openart_account_get`, `openart_model_list`/`openart_model_cost`, `openart_model_form_get`). Source: Bug List 6 item 6 (owner 2026-11-09): "icons with text, new OpenArt icons", on the Level screen under the region name.

## 1. Purpose
Five square icons for the Gameplay row on the Level screen (one per mode). Each icon sits above its text label on a 48 dp or larger button. Until the art is approved the game uses code-drawn fallback icons (`GameplayIcons.For(mode)`); approved art dropped into `Assets/Resources/Art/Ui/` as `gameplay_relaxed`, `gameplay_easy`, `gameplay_normal`, `gameplay_hard`, `gameplay_insane` (Sprite import) replaces them with no code change.
Design: one family of five round speed-dial badges, so they read as a set. The difference is carried by SHAPE and COUNT, never colour alone (CUX-6): the needle position, the number of lit pips (1 to 5) and a small symbol. Relaxed = needle far left, 1 lit pip, a small sleeping cloud, calm green; Easy = 2 pips, a small leaf, light green; Normal = 3 pips, a small star, golden yellow; Hard = 4 pips, a small flame, orange; Insane = 5 pips, a small lightning bolt with spiky rim, hot red-magenta.

## 2. MCP and tool
OpenArt MCP (connector `73f004bd-92f2-4e13-8abf-f0c13bc6ba8f`), tool `openart_generate_image`, **5 calls** (one per icon, 1 image each).

## 3. Model and mode
`byte-plus-seedream-5-lite` (Seedream 5 Lite), mode **image2image** (this mode needs at least one reference). Params per call: resolution 2K (the model offers only 2K or 3K), aspectRatio 1:1, imageCount 1, autoEnhancePrompt false (default), no seed field in this form.
Reference (visualReferences), the same for all 5 calls: the owner-locked style bible, resource `vVnbzqvg1L5uSFCKydUE` (used by T1, T46, T56, T65, T71). No other reference and no upload.

## 4. Full prompts, word for word
STYLE LOCK (first sentence of every prompt): "Match the art style of the reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subject described here, not the reference's layout or objects."
COMMON BODY: " A single round game-UI icon for a mobile jumper game, seen straight on and centred, filling about 80 percent of the square image: a chunky circular speed-dial badge with a thick dark navy outline, a glossy domed glass face, a short rounded needle and a row of five small round pips along the bottom of the dial, some lit and the rest dark. A small symbol sits above the needle pivot. Clean readable shape at a small size, high contrast, no text, no letters, no numbers."
Mode sentences (one per call, appended to the common body):
- **Relaxed:** " The needle points far left. Exactly 1 of the 5 pips is lit. The symbol is a tiny sleeping cloud with closed eyes. Calm mint-green palette with a soft sky-blue rim."
- **Easy:** " The needle points left of centre. Exactly 2 of the 5 pips are lit. The symbol is a tiny green leaf. Light lime-green palette with a pale yellow rim."
- **Normal:** " The needle points straight up. Exactly 3 of the 5 pips are lit. The symbol is a tiny five-point star. Golden-yellow palette with a warm cream rim."
- **Hard:** " The needle points right of centre. Exactly 4 of the 5 pips are lit. The symbol is a tiny flame. Orange palette with a deep red rim."
- **Insane:** " The needle points far right, shaking slightly. All 5 pips are lit. The symbol is a tiny lightning bolt, and the badge rim has small spikes. Hot red and magenta palette with a bright yellow rim."
COMMON END: " Thick consistent dark navy outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background outside the badge, nothing else in the image."
Each prompt = STYLE LOCK + COMMON BODY + mode sentence + COMMON END. No competitor game names anywhere.

## 5. Cost (free quote, `openart_model_cost`, this exact config: byte-plus-seedream-5-lite, image2image, 2K, 1:1, imageCount 1)
- **15 credits per job.** 5 jobs = **75 credits**.
- Current balance (`openart_account_get`, Plus plan): **9,933**. Balance after: **9,858**.
- Other settings cost differently; this quote is for the config above only. The price is finalized at generation time.

## 6. Output handling and what happens next (no more credits)
1. Results are shown to you labelled Relaxed, Easy, Normal, Hard, Insane, saved under `Art/_candidates/gameplay_icons/` (outside Assets, not in the build).
2. Grey #808080 keyed out and checked with `Tools/art_pipeline/qa_art.py` (CUX-9: no grey halos, clean edges, readable at about 96 px), then resized to a square sprite (256 px) and installed as `Assets/Resources/Art/Ui/gameplay_<mode>.png` (Sprite).
3. The Level screen row picks them up automatically; a screenshot at 9:16 and 9:21 is reviewed before commit. Icons that fail QA are not installed (the code-drawn fallback stays for that mode).
4. If one icon is unusable, a redo of that single icon is a NEW ask (15 credits).

## 7. Risks and alternatives
- Seedream may draw the pip count or needle position wrongly (counting is a known weak point of image models). If so, those icons fail the "not colour alone" rule and I come back with a new ask; the code-drawn fallback, which is exact, stays in the meantime.
- **Cheaper:** Kling 3 Omni image2image at 1k costs 10 credits per job (50 for all five, saves 25) but its match to the locked style is untested here; not recommended for a set that must match.
- **Safer:** 2 tries per icon (10 jobs, 150 credits, balance after 9,783): a second pick per mode if the first fails QA.
- **Free:** keep the code-drawn icons only (0 credits).

## Open questions for the owner
1. Approve the exact batch above: 5 calls, 1 image each, 2K, 1:1, reference = style bible only, 75 credits, balance 9,933 to 9,858?
2. Or the safer 10-job batch (150 credits)?
3. Do you want a different symbol set (the ones in section 1 are my choice)?

---
# REVISION 2 (2026-11-09, after the owner's question "why not 30 credits for 5 icons")
The cost is per generated IMAGE (15 credits for Seedream 5 Lite at 2K, 1:1 or 16:9, free quote), not per icon. Revision: draw all five icons in ONE image (a horizontal sheet, 16:9) and slice it, like the earlier platform and hero-sheet batches. 2 calls x 1 image = 2 sheets = **30 credits** (balance 9,933 to 9,903); the two sheets are two independent tries of the same five icons, so each mode gets two candidates and the best of each is installed.
Model/mode/settings unchanged: `byte-plus-seedream-5-lite`, image2image, 2K, aspectRatio 16:9, imageCount 1, autoEnhance off, reference = style bible `vVnbzqvg1L5uSFCKydUE` only.
Sheet prompt = STYLE LOCK (as in section 4) + " A horizontal sprite sheet of five separate round game-UI icons in one row on a flat solid #808080 background, equally spaced with clear empty grey gaps between them, nothing overlapping, all five exactly the same size, centred vertically, together filling about 90 percent of the image width. Each icon is a chunky circular speed-dial badge with a thick dark navy outline, a glossy domed glass face, a short rounded needle and a row of five small round pips along the bottom of the dial, some lit and the rest dark, with one small symbol above the needle pivot. From left to right: icon 1, Relaxed: the needle points far left, exactly 1 of the 5 pips is lit, the symbol is a tiny sleeping cloud with closed eyes, calm mint-green palette with a soft sky-blue rim. Icon 2, Easy: the needle points left of centre, exactly 2 of the 5 pips are lit, the symbol is a tiny green leaf, light lime-green palette with a pale yellow rim. Icon 3, Normal: the needle points straight up, exactly 3 of the 5 pips are lit, the symbol is a tiny five-point star, golden-yellow palette with a warm cream rim. Icon 4, Hard: the needle points right of centre, exactly 4 of the 5 pips are lit, the symbol is a tiny flame, orange palette with a deep red rim. Icon 5, Insane: the needle points far right, shaking slightly, all 5 pips are lit, the symbol is a tiny lightning bolt and the badge rim has small spikes, hot red and magenta palette with a bright yellow rim." + " No text, no letters, no numbers, no watermark. Thick consistent dark navy outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background."
Slicing: the sheet is cut at the grey gaps into 5 square sprites (the art pipeline's sheet cutter), grey keyed out, QA (CUX-9), 256 px, installed as gameplay_<mode>.png.
Alternatives: 1 sheet only = 15 credits (balance after 9,918); Kling 3 Omni 1k at 10 credits per sheet = 20 for two (style match untested); free = code-drawn icons.

---
# RESULT (2026-11-09)
- Batch 1 (owner approved, 2 calls, 30 credits): sheets A and B. Both had good rim/dial symbols but wrong needle angles and pip counts (the model cannot count).
- Batch 2 (owner approved, 2 calls, 30 credits): redo of set B with the style bible plus set B as references. Both sheets largely copied the reference and ignored the needle/pip corrections.
- Owner picked "Set B redo, try 1" (`Art/_candidates/gameplay_icons/sheet_B2_1.png`). Dials were finished in code (free): `Tools/art_pipeline/finish_gameplay_icons_b.py` repaints each face with the needle at 9, 10:30, 12, 1:30, 3 o'clock and exactly 1..5 of 5 pips lit; rings, rim symbols and the spiky Insane rim stay from the art.
- QA: no clipped edges, grey halo 0 to 1 px per icon, 256 px. Installed as Assets/Resources/Art/Ui/gameplay_{relaxed,easy,normal,hard,insane}.png (imported as Sprites by ArtImportPostprocessor.ApplyUiKit).
- Credits: 60 spent in total (4 images x 15). Balance before 9,933, after 9,873.
- The unused sheets stay in Art/_candidates/gameplay_icons (outside the build).
