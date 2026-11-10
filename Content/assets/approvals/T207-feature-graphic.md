# Approval packet T207: Play Store / Play Games feature graphic (catalog B37 mkt_feature_graphic)

Status: APPROVED by the owner on 2026-11-10 (AskUserQuestion answer "Approve 4 images (60 cr)"); generated the same day.

- Purpose: the 1024 x 500 feature graphic that the Play Games Services configuration page and the Play Store listing ask for (owner request 2026-11-10). The art is generated without text; the existing wordmark `Assets/Resources/Art/Ui/logo.png` is composited on the left afterwards with Pillow (no credits).
- MCP / tool: OpenArt MCP, `openart_generate_image` (1 job).
- Model / mode: byte-plus-seedream-5-lite (Seedream 5 Lite), image2image, resolution 2K, aspectRatio 21:9 (the closest form option to 1024 x 500; the sides are trimmed afterwards), imageCount 4, autoEnhancePrompt false, no seed field.
- Reference images: (1) Momo reference, the owner pick of B00, as the upload `upload:NrwshICTmQtSkNo2uC5E` (the same picture the style bible was built from); (2) Style bible, owner pick image 4 of B01a, `resource:vVnbzqvg1L5uSFCKydUE`.
- Cost (openart_model_cost, this exact config): 15 credits per image, 4 images = **60 credits**. Balance 9,608, after 9,548.
- Alternative A: imageCount 2 = 30 credits (saves 30, less choice). Alternative B: no credits, compose the graphic from existing art (sky gradient, `char_momo.png`, the logo) with Pillow.
- Output: the 4 candidates are downloaded to `Art/_candidates/feature_graphic/fg_1.png` to `fg_4.png` and shown to the owner; the picked one is cropped to 1024 x 500, the logo is placed on the left, flattened to 24-bit PNG under 1 MB (JPEG fallback) and saved as `Assets/Art/Marketing/store/feature_graphic_1024x500.png`; the row is logged in `Content/assets/assets_log.csv`.

## Prompt (exact)

Use the first reference image as the exact design of Momo and the second reference image as the art style guide (outline weight, neon rim light and glow, glossy shading, colour saturation). Wide store feature graphic for a mobile jumping game: Momo, the round chibi cloud-fox with the magenta scarf and pale sky-blue cloud tail, bouncing joyfully up a tall glowing tower of floating grass-and-flower platforms with neon rims, through a bright daylight sky with fluffy clouds and a sunny meadow far below, golden stars and faceted blue-pink gems sparkling around the platforms, a few oversized bounce flowers and petal platforms, Momo large on the right third in a mid-jump pose with arms up and a happy open-mouth smile, lots of calm empty sky on the left half for a logo, dynamic diagonal composition, no text, no letters, no numbers, no watermark. cute chibi cartoon with neon rim light and glow, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, no watermark

## Result (2026-11-10)

Creation prMzCYksZVZkLFNpvwiw, 4 images of 3136 x 1344, 60 credits used (balance 9,608 to 9,548, as quoted). Candidates in `Art/_candidates/feature_graphic/fg_1.png` to `fg_4.png` with 1024 x 500 logo previews. Owner pick: **candidate 1** (resource RtYzk9U5Py2JUL6peHx1). Final file: `Assets/Art/Marketing/store/feature_graphic_1024x500.png` (24-bit PNG, 696 KB, centre crop, logo at the left with a soft shadow). Raw pick kept as `Assets/Art/Marketing/feature_graphic_v1.png`.

## Acceptance checks
Style matches the locked style bible (outline weight, neon rim light, palette); Momo identical to the reference (cream fur, magenta scarf, blue cloud tail); no text, letters or watermark in the generated art; no resemblance to any existing game's character or branding; the left half is calm enough for the logo; prompt, model and settings logged to assets_log.csv.
