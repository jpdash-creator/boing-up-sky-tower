# Approval packet T79: Rewards screen redesign art (3 jobs, 2 options each)

Status: PENDING OWNER APPROVAL. Nothing generated. Free calls only: `openart_account_get` (balance 3,908), `openart_model_cost`, `openart_model_form_get`.

## 1. Purpose (Bug List 3, D4)
- S1 header scenes (Daily, Weekly): 2 wide scenes, you pick which one is Daily and which Weekly (or one for both).
- S2 task icons (12): enemy skull, boss skull, login scroll, hourglass, level flag, combo flame, upgrade arrow, key, shop stall, coin stack, gem, calendar check.
- S3 milestone chests (6): bronze, gold, purple closed and open.
Existing icons are NOT replaced. Style = your existing icon set (S2 and S3 use my montage of your own icons as the style reference, so they should match; the earlier text-only sheets looked off-style).

## 2. MCP and tool
OpenArt MCP (connector `mcp__73f004bd-92f2-4e13-8abf-f0c13bc6ba8f__`), `openart_generate_image`, 3 calls. Before S2/S3, one free upload of the reference montage (`openart_upload_sign` + PUT; no credits).

## 3. Model and mode
`byte-plus-seedream-5-lite` (Seedream 5 Lite), 2K, imageCount 2, no seed. S1: text2image, aspect 21:9. S2 and S3: image2image, aspect 1:1, reference = montage of existing icons (icon_heroes, gem, coin, crown, bundle bag, chest, sound, music, vibrate, motion, bolt, settings, daily gift, shop bag, play, size).

## 4. Prompts, word for word
S1: "A wide background scene for a cute chibi cartoon 2D mobile game quest screen header, glossy painterly cartoon style, thick soft outlines, saturated gradient colors, glossy sparkle accents, top-left light. A bright cheerful meadow landscape with rolling green hills, a winding golden road, fluffy white clouds, candy-coloured mushrooms and a distant pagoda, with a calm, simple, uncluttered lower third where a row of chests and numbers can sit. No characters, no text, no letters, no numbers, no watermark. palette: sky blue, grass green, sunny yellow, soft pink, white."
S2: "Use the reference image only for the art style: glossy chunky chibi game UI icons with a thick dark-navy outline, a thin light inner rim and soft gradients. Paint a NEW icon sheet in exactly that style: a clean 4 by 3 grid of twelve separate icons, each centred in its own cell with generous empty margins on a flat solid #808080 background, nothing overlapping, all the same size and style. Row 1: an orange round skull; a red horned demon skull; a rolled paper scroll with a feather quill; a golden hourglass. Row 2: a green flag on a pole; a flame inside a dark round badge; a bold green upward arrow; a golden key. Row 3: a small market stall with a red and white awning; a stack of gold coins; a blue faceted gem; a calendar page with a green check mark. No text, no letters, no numbers, no watermark."
S3: "Use the reference image only for the art style: glossy chunky chibi game UI icons with a thick dark-navy outline, a thin light inner rim and soft gradients. Paint a NEW icon sheet in exactly that style: a clean 3 by 2 grid of six separate treasure chest icons, each centred in its own cell with generous empty margins on a flat solid #808080 background, nothing overlapping, all the same size. Top row, closed chests: a bronze wooden chest with metal bands; a golden chest with a star lock; a purple jeweled chest with a pink gem lock. Bottom row, the same three chests open with warm glowing light and coins inside. No text, no letters, no numbers, no watermark."
No competitor names.

## 5. Cost
3 calls x 2 images x 15 = **90 credits** (30 per call, quotes confirmed for 21:9 and 1:1). Balance **3,908**, after **3,818**.

## 6. Output handling
Candidates to Art/_candidates/rewards_t79/, contact sheet shown, you pick per sheet. Then keyed to alpha, sliced into single icons, QA'd, placed in Resources/Art/Rewards slots (scene_daily.png, scene_weekly.png, task_*.png, chest_*.png), wired into the new Rewards screen. Logged as T79 with actual credits and balance.

## 7. Alternatives
- One option per call: 45 credits (saves 45).
- Scenes only (S1): 30 credits; icons and chests keep the existing art.
