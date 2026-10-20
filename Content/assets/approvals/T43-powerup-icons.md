# Approval packet T43-powerup-icons (11 power-up icons, one sheet)

Status: PENDING OWNER APPROVAL. Nothing has been generated.

- Purpose: replace the code-drawn power-up icons (shop Tab 2, loadout picker, run buttons) with on-style art for all 11 planned power-ups: Flight, Rocket, Double Jump, Wall Stick, Shield, Magnet, Slow Time, Spring Boots, Coin Doubler, Star Seeker, Safety Net.
- MCP / tool: OpenArt MCP, `openart_generate_image` (1 job).
- Model / mode: byte-plus-seedream-5-lite (Seedream 5 Lite), image2image, 2K, aspect 1:1, imageCount 2, no seed field.
- Reference: style bible resource vVnbzqvg1L5uSFCKydUE (owner-locked).
- Cost: 15 credits per image, 2 images in the job = 30 credits total. Balance 8195, after 8165.
- Alternative: imageCount 1 = 15 credits (saves 15), one candidate, no choice.
- Output: creations returned by OpenArt; downloaded to Art/_candidates/powerup_icons/, you pick one, `process_art.py` slices the 11 icons (QA by qa_art.py). Re-runs are a new ask.

## prompt (word for word)

Match the art style of the reference image exactly (outline weight, neon rim light and glow, glossy shading, colour saturation), but draw only the new subjects described here, not the reference's layout or objects. A sprite sheet of eleven round-badge game icons for power-ups, arranged in a grid of four columns and three rows (the last row has three icons), on a flat solid #808080 mid-grey background with generous spacing, nothing overlapping. Each icon is one simple bold symbol centred inside a glossy circular badge, readable at small size, no text, no letters, no numbers. Row 1: 1 Flight: a pair of white feathered wings. 2 Rocket: a small red-and-white rocket pointing up with a flame. 3 Double Jump: two stacked curved arrows pointing up. 4 Wall Stick: a hand silhouette gripping a vertical wall edge. Row 2: 5 Shield: a blue shield with a bright rim. 6 Magnet: a red horseshoe magnet with silver tips and tiny sparkles being pulled in. 7 Slow Time: a pale blue hourglass with a gentle swirl. 8 Spring Boots: a pair of boots with coiled springs under the soles. Row 3: 9 Coin Doubler: two overlapping gold coins with a sparkle. 10 Star Seeker: a gold star with a small arrow pointing at it. 11 Safety Net: a round net with a soft green rim, like a trampoline seen from the side. Each badge uses a different bright rim colour so they are told apart by shape as well as colour. palette: sky blue, gold, pink, mint, red. cute chibi cartoon with neon rim light and glow, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background
