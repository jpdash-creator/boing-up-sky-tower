# OpenArt approval packet: leaderboard UI kit (Main Bug List 1, item 2b), 2026-11-13

Purpose: replace the code-drawn leaderboard (button rows, black blocks) with a painted UI kit. Text and numbers stay live text drawn by the game (Lilita One, Nunito Bold), so every plate below must be EMPTY (no words, no digits, no avatars). Colour rule (owner): rank 1 gold, rank 2 silver, rank 3 bronze, ranks 4 to 10 light green, ranks 11 and lower soft grey, the player's own row highlighted.

Tool: OpenArt MCP `openart_generate_image` (credit consuming). Model: `byte-plus-seedream-5-lite` (Seedream 5 Lite), mode text2image, resolution 2K, imageCount 2 per call (two variants to pick from, same as the background batch T209), autoEnhancePrompt false, no reference images, no seed.
Cost (openart_model_cost, 2K, 1 image) = 15 credits per image. 4 calls x 2 images = 8 images = 120 credits. Balance before 9,488, after 9,368.
Results: saved as candidates in `Art/_candidates/leaderboard_ui/`, contact sheet for the owner pick, then sliced by `Tools/art_pipeline` and imported to `Assets/Resources/Art/Ui/`. Nothing else is spent without a new approval.

STYLE LOCK (appended to every prompt): cute chibi cartoon with neon rim light and glow, thick consistent dark navy outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background, no text, no letters, no numbers, no watermark.

## Call 1: row plates, aspect 3:4
Six wide horizontal leaderboard row plates stacked in one column with clear gaps between them, each plate a long rounded glossy bar about five times wider than tall, completely empty inside: smooth flat inner surface with a soft inner shadow and a bright highlight along the top edge, nothing painted on it. Plate 1 shiny gold with warm glow and small sparkle accents. Plate 2 polished silver with cool blue-white shine. Plate 3 warm bronze copper. Plate 4 light fresh mint green. Plate 5 soft light grey-blue. Plate 6 bright sky blue with a thick golden outline to mark the player's own row. Same size and shape for all six, evenly spaced, centred. Mobile game UI asset sheet.

## Call 2: board frame, aspect 3:4
A tall rounded leaderboard board frame for a mobile game screen: a thick golden-cream outer border with a dark navy outline and soft bevel, a large empty soft lavender-purple inner panel with a faint inner shadow, and a wide empty ribbon banner overlapping the top edge of the frame (no text on the ribbon), small sparkles at the corners. The inner panel is completely empty and plain so rows can be placed on it. Symmetrical, centred, the frame fills most of the image. Mobile game UI asset.

## Call 3: icon sheet, aspect 3:4
A neat 3 by 4 grid of twelve separate icons for a mobile game leaderboard, each icon centred in its own cell with wide empty space between icons, each readable at small size, no text and no numbers: 1 a wrapped gift box with a bow, 2 a round alarm clock, 3 a large golden crown with one blue gem, 4 a round blue information bubble with a plain dot-and-bar symbol, 5 a golden trophy cup, 6 a folded treasure map with a flag, 7 a glossy infinity loop symbol, 8 a round gold medal with a ribbon and an empty face, 9 a round silver medal with a ribbon and an empty face, 10 a round bronze medal with a ribbon and an empty face, 11 an empty round mint-green badge, 12 an empty round soft grey badge.

## Call 4: tabs and chips sheet, aspect 4:3
A neat sheet of six separate mobile game UI pieces on the grey background, each with wide empty space around it, all empty with no text: 1 a wide rounded tab pill in bright glossy golden orange (selected), 2 the same tab pill in muted soft lavender (not selected), 3 a wide slim rounded timer pill in dark navy with a light inner edge, 4 a rounded score pill in warm cream with a golden outline, 5 a large round glossy button plate in sky blue with a thick outline and empty centre, 6 a small narrow banner strip in dark navy with a golden outline for a section header.

## Alternatives / savings
- Calls 1 and 2 only (4 images, 60 credits): icons and tabs reuse existing painted icons and plates where they fit.
- imageCount 1 instead of 2 per call: half the cost (60 credits for all four), but no choice between variants.
