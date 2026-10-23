# Approval packet T47: UI style kit sheet and logo (vertical slice step 1)

Status: PENDING OWNER APPROVAL. Nothing has been generated.

- Purpose: first art of the UI overhaul (docs/16 sections A, C): a style kit sheet (chunky toy-plate buttons, plates, ribbon, pills, badges, tab bar pieces) and the BOING UP / SKY TOWER wordmark. Loading key art and the Shop scene follow in a later packet once this look is approved.
- MCP / tool: OpenArt MCP, `openart_generate_image` (2 jobs).
- Model / mode: byte-plus-seedream-5-lite (Seedream 5 Lite), image2image, 2K, imageCount 2 per job, no seed field.
  Job A aspect 3:4 (style kit), Job B aspect 4:3 (logo). Reference: style bible vVnbzqvg1L5uSFCKydUE.
- Cost: 15 credits per image, 4 images = 60 credits. Balance 7,793, after 7,733 (cost checked with openart_model_cost at imageCount 2, 2K, 4:3).
- Alternative: imageCount 1 per job = 30 credits (saves 30), no choice between candidates.
- Output: downloaded to Art/_candidates/ui_kit/ and Art/_candidates/logo/, shown to you as images; you pick; process_art.py cuts the pieces for 9-slice and the Shop slice.

## Fonts (free, already in the project, all OFL)
Sample sheet: E:/Unity/logs/font_samples.png. Recommendation: Lilita One for titles, numbers and buttons; Nunito Bold for body text on plates (36 px minimum, CUX-5). Alternatives shown: Luckiest Guy, Fredoka SemiBold, Baloo 2 ExtraBold.

## Job A: ui_kit_sheet (3:4)
Match the art style of the reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subjects described here. A sprite sheet of chunky glossy UI parts for a cute mobile jumping game, on a flat solid #808080 background, generous empty space around every part, nothing overlapping, no text, no letters, no numbers. Sky-blue, cloud-white and gold colour world, thick dark navy outline, bevelled gradient surfaces, top-left light. Row 1: four wide rounded rectangle buttons, identical size and shape, in solid green, yellow, blue and red. Row 2: two large empty rounded panels (one cream-white, one sky-blue) with thick gold-edged frames. Row 3: one wide gold ribbon banner with folded tails, empty; one dark navy rounded currency pill with an empty round socket at its left end; one small red round notification badge. Row 4: a bottom tab bar strip in sky-blue with five empty round tab sockets, the middle one larger and gold; one round blue back button with a plain left arrow. No characters, no clouds, no watermark.

## Job B: logo (4:3)
Match the art style of the reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subject described here. A game logo wordmark: the words BOING UP in huge chunky bubbly gold letters with a thick dark navy outline and white highlight, and below them SKY TOWER in smaller sky-blue chunky letters on a small cloud-white ribbon. A small round chibi cloud-fox mascot with soft white and warm cream fur, a pale sky-blue cloud-puff tail, big glossy eyes and a tiny magenta scarf is bouncing through the O of BOING with a motion arc and little stars. Flat solid #808080 background, logo centred with generous empty space, no other text, no watermark.
