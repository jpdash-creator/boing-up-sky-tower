# Approval packet T54: region 2 test character redo (purple, no hat)

Status: PENDING OWNER APPROVAL. Nothing has been generated. Replaces the mushroom-cap test character from T53 (owner 2026-10-24: no mushroom hat, a purple character in the style of region 2, Mushroom Grove). The T53 character picture is kept only as a rejected candidate (Art/_candidates/mannequin_test/pip.png).

## 1. Purpose
The test character for region 2 (development builds only, never shipped) must wear the same gear layers as Momo through the rig. It must not have headwear of its own, so head gear can be tested on it: a purple chibi with a bare head, painted in region 2's colours (purple, violet, glowing magenta and cyan accents, soft glowing spots).

## 2. MCP and tool
OpenArt MCP, `openart_generate_image`, 1 call (the grey mannequin reference is already uploaded: id MeaArHyNP7f8yO63iW8S).

## 3. Model and mode
`byte-plus-seedream-5-lite` (Seedream 5 Lite), image2image, 2K, aspect 3:4, 1 image, references: style bible (vVnbzqvg1L5uSFCKydUE) then the grey mannequin.

## 4. Prompt (word for word)
"Match the art style of the first reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subjects described here. The second reference image is a flat grey mannequin doll on a magenta background. Use the mannequin only for its exact pose, framing, proportions and silhouette size (big round head, short body, short legs, small arms). Draw one NEW cute character in that same pose, size and position, on a flat solid #808080 grey background: a purple chibi creature with soft violet fur, a bare head with two small round purple ears and a tuft of lighter lilac hair, big dark glossy eyes, a cream belly patch, a few small glowing magenta and cyan spots on its body like a magical mushroom forest, a short curly purple tail, no hat, no cap, no clothes, no accessories. Thick dark navy outline, glossy shading, bright saturated colours. No text, no letters, no numbers, no watermark."

## 5. Cost
15 credits (Seedream 5 Lite image2image 2K, one image; the price is finalised at generation). Balance 6,878 now, 6,863 after.

## 6. Output handling
Saved to Art/_candidates/mannequin_test/pip_purple.png and shown to you, then cut to a transparent PNG on the template canvas, its rig fitted (head, torso, legs, feet boxes) and registered as the development-only test character for region 2.

## 7. Alternatives
Two options in one call (pick the best): 30 credits. Skip the redo and keep the mushroom creature: 0 credits (rejected by the owner).
