# Approval packet T55: region 2 test character, a completely different creature (not a Momo)

Status: PENDING OWNER APPROVAL. Nothing has been generated. Replaces T53 (mushroom cap, rejected) and T54 (two purple cat-like creatures, rejected: "both are bad, a completely brand new character, not another Momo").

## 1. Purpose and what went wrong
Both earlier tries used the Momo mannequin (cat ears, fluffy cloud tail) as the reference, so the model drew Momo clones. This time the pose and size reference is a generic grey doll with no species features at all (round head, small body, two legs, two arms; Art/_candidates/mannequin/mannequin_generic.png), made by script. The creature must be a new species, similar chibi size, purple in region 2's palette (Mushroom Grove), no hat or clothes so head, body, pants and boots gear can be tested on it.

## 2. MCP and tool
OpenArt MCP: `openart_upload_sign` plus HTTP PUT for the generic mannequin (free), then `openart_generate_image`, 1 call, 2 images.

## 3. Model and mode
`byte-plus-seedream-5-lite`, image2image, 2K, aspect 3:4, imageCount 2. References: style bible (vVnbzqvg1L5uSFCKydUE), then the generic mannequin.

## 4. Prompt (word for word; [SPECIES] chosen by the owner)
"Match the art style of the first reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subjects described here. The second reference image is a plain grey doll on a magenta background: use it only for the pose (standing, facing front, arms at the sides), the framing, the overall size and the chibi proportions (very big round head, small body, short legs). Draw one completely NEW cute character in that pose, size and position, on a flat solid #808080 grey background: [SPECIES]. It is clearly NOT a cat, NOT a fox, NOT a fluffy furry animal and has no cloud tail. Purple and violet colours with a few glowing magenta and cyan accents, like a magical mushroom forest. Bare head, no hat, no cap, no clothes, no accessories. Thick dark navy outline, glossy shading, bright saturated colours. No text, no letters, no numbers, no watermark."

[SPECIES] options: (a) "a baby dragon with smooth purple scales, two small curved horns, a short pointed tail and tiny folded wings"; (b) "a smooth purple crystal golem with a glowing magenta gem on its forehead, rounded stone-like body and no fur"; (c) "a purple frog creature with smooth glossy skin, big round eyes on top of its head, a wide small mouth and a cream belly".

## 5. Cost
15 credits per image, 2 images = 30 credits (Seedream 5 Lite image2image 2K; price finalised at generation). Balance 6,848 now, 6,818 after.

## 6. Output handling
Art/_candidates/mannequin_test/ for review, then the chosen picture is cut to a transparent PNG on the template canvas, its rig measured, and registered as the development-only test character for region 2.

## 7. Alternatives
One image (15 credits); or all three species, 1 image each (45 credits).
