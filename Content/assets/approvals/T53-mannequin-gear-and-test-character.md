# Approval packet T53: mannequin gear test (4 pieces) and the region 2 test character (1 image)

Status: PENDING OWNER APPROVAL. Nothing has been generated. Prepared with read-only OpenArt calls (account, model cost). Design: docs/18-wearable-gear-and-character-rig.md.

## 1. Purpose
Owner issue list 21 (2026-10-24): gear must be real wearables that fit the in-game Momo and every future character, made from scratch with OpenArt, "only 1 each for testing", plus a completely different test character for region 2 (never shipped).
- 4 gear test pieces (head, body, pants, boots), one per image, painted on a flat grey mannequin built from Momo (script, no credits) on a magenta background so each piece cuts out cleanly by colour (cream fur is no longer a problem) and is fitted to any character by the rig.
- 1 test character: a different chibi on the same template, to prove the same layers fit a second body.

## 2. MCP and tools
OpenArt MCP (`mcp__73f004bd-92f2-4e13-8abf-f0c13bc6ba8f__`): `openart_upload_sign` plus an HTTP PUT for the mannequin picture (free, not charged), then `openart_generate_image`, 5 calls, 1 image each.

## 3. Model and mode
`byte-plus-seedream-5-lite` (Seedream 5 Lite), image2image, 2K, aspect 3:4, 1 image per call, no seed field. References: first the UI style bible (resource vVnbzqvg1L5uSFCKydUE), second the grey mannequin upload (Art/_candidates/mannequin/mannequin_momo.png, 1365 x 1649).

## 4. Prompts (word for word)
Common start (all five): "Match the art style of the first reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subjects described here. The second reference image is a flat grey mannequin doll on a magenta background."

Gear (calls 1 to 4): Common start + " Draw this exact mannequin once, with the same front pose, the same size and the same position in the frame as in the reference, on a flat solid #FF00FF magenta background. The mannequin stays flat grey and unchanged; only the garment has colour and the garment must never be grey or magenta. It wears ONE [PIECE], fitted to the [PART] of the mannequin, with a thick dark navy outline like the rest of the style. Nothing else is added: no props, no shadow, no text, no letters, no numbers, no watermark."
1. [PIECE] = a tall red wizard hat with gold stars and holes for the ears; [PART] = head and ears.
2. [PIECE] = a teal explorer jacket with brown leather straps and a gold buckle, sleeves over the arms; [PART] = torso and arms.
3. [PIECE] = orange adventure trousers with two brown patches and a brown belt, down to the ankles; [PART] = legs.
4. [PIECE] = a pair of purple boots with gold buckles and cream soles; [PART] = feet.

Test character (call 5): Common start + " Use the mannequin only for its exact pose, framing, proportions and silhouette size (big round head, short body, short legs, small arms). Draw one NEW cute character in that same pose, size and position, on a flat solid #808080 grey background: a round mushroom creature with a big spotted orange mushroom cap on top of its head, small round ears under the cap, a mint green body, big dark eyes, a small white scarf-free neck, no tail cloud, no clothes, no accessories. Thick dark navy outline, glossy shading, bright saturated colours. No text, no letters, no numbers, no watermark."

## 5. Cost
`openart_model_cost` (2026-10-24): Seedream 5 Lite image2image, 2K, 1 image = 15 credits per call (price shown at 4:3; other aspect ratios or resolutions can cost differently and the price is finalised at generation). 5 calls = **75 credits**. Balance 6,953 now, about 6,878 after.

## 6. Output handling
Results go to Art/_candidates/mannequin_test/ and are shown to you labelled. A script cuts the four gear pieces by colour (grey and magenta key), fits them onto Momo and onto the test character through the rig, and the test character is registered in development builds only, region 2 only. Everything is logged in Content/assets/assets_log.csv.

## 7. Alternatives
- Gear only, no test character: 4 calls = 60 credits.
- Two options per call (pick the best): 150 credits.
- Production later (not in this approval): 40 sheets of 6 pieces on 6 mannequins = 600 credits for all 240 pieces, with a 25 percent re-roll budget.
