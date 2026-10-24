# Approval packet T56: separate-part characters (Momo + 3 region 2 options) and 4 matching gear pieces

Status: PENDING OWNER APPROVAL. Nothing has been generated. Replaces T55 (cancelled by the owner before any call ran; no credits spent on it). Design: docs/18 section F.

## 1. Purpose
Owner direction 2026-10-24: characters and gear must be built from SEPARATE PARTS so a character is configurable and gear is plug and play; Momo is rebuilt from parts too; pants and boots must not overlap (boots shorter, pants shorter at the bottom); three NEW mushroom-theme characters for region 2 (not Momo clones): Sporeling, Glimmer moth, Rootling.
Method: a parts template (Tools/art_pipeline/make_parts_template.py, Art/_candidates/parts/parts_template.png, 2880 x 2160 = 4:3): a flat grey doll taken apart on magenta, one silhouette per part: head (with room for ears, horns, antennae), back (tail, wings), torso (neck and scarf), arm L, arm R, leg L, leg R, foot L, foot R. The model fills every silhouette, so parts come out separate, complete and in known positions; a script cuts them and shifts them back into the assembled doll.

## 2. MCP and tools
OpenArt MCP: `openart_upload_sign` plus an HTTP PUT for the parts template (free, not charged), then `openart_generate_image`: 8 calls, 1 image each (staged, see section 7).

## 3. Model and mode
`byte-plus-seedream-5-lite`, image2image, 2K, aspect 4:3, 1 image per call. References: 1 the style bible (vVnbzqvg1L5uSFCKydUE), 2 the parts template, 3 (Momo only) the clean Momo picture (upload id ig6UtIp8Q7to2wWV6ibE).

## 4. Prompts (word for word)
Common start: "Match the art style of the first reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subjects described here. The second reference image is a parts template: flat grey silhouettes on a magenta background, one silhouette per body part of a chibi doll taken apart (top left the big round head, top right the back part, bottom row from left to right the torso, the two arms, the two legs, and the two feet). Keep the magenta background, keep every part in its own silhouette position, size and orientation, and draw every part completely and separately, nothing overlapping, nothing outside its silhouette except small features that belong to that part. Thick dark navy outline on every part, glossy shading, bright saturated colours. No text, no letters, no numbers, no watermark."

1. Momo: Common start + " The third reference image is the character Momo. Redraw Momo taken apart: the head with his fluffy cream hair, big pointed ears (ears may rise above the head silhouette) and brown eyes, the back part is his pale blue cloud tail, the torso is his cream body with the pink scarf, then his left arm, right arm, left leg, right leg, left foot, right foot, exactly as in the Momo picture."
2. Sporeling: Common start + " Draw one completely NEW character taken apart: a Sporeling, a little mushroom person: the head is a big round glossy purple spotted mushroom cap with a few glowing magenta spots and a friendly face on the cap rim, the back part is a small cluster of glowing spores and a curled stalk tail, the torso is a small cream stalk body, then two tiny round arms, two short stubby legs and two small feet. Purple and violet with magenta and cyan glows. It is NOT a cat, NOT a fox, NOT furry."
3. Glimmer moth: Common start + " Draw one completely NEW character taken apart: a Glimmer moth, a fuzzy purple moth sprite: the head is round with two long feathery antennae rising above the silhouette and big glossy dark eyes, the back part is a pair of small glowing wings with sparkling spore dust, the torso is a soft lilac fuzzy body, then two thin arms, two short legs and two small feet. Purple and violet with magenta and cyan glows. It is NOT a cat, NOT a fox."
4. Rootling: Common start + " Draw one completely NEW character taken apart: a Rootling, a stubby bark and moss creature: the head is a round purple-brown bark head with a small face, tiny glowing mushrooms growing on top, the back part is a small bunch of roots, the torso is a rounded mossy body, then two twig-like arms, two short root legs and two root feet. Purple and violet with magenta and cyan glows. It is NOT a cat, NOT a fox, NOT furry."
Gear (calls 5 to 8): Common start + " The grey silhouettes stay flat grey and unchanged (the doll); only the garment parts have colour and a garment must never be grey or magenta. Dress these parts with ONE piece of gear matched to the parts: [GEAR]. Garment parts stay inside or hugging their own silhouette, do not touch other parts."
5. head: [GEAR] = a tall red wizard hat with gold stars on the head silhouette (the rest stay bare grey).
6. body: [GEAR] = a teal explorer jacket with brown leather straps and a gold buckle: the jacket body on the torso silhouette and one sleeve on each arm silhouette (head, legs and feet stay bare grey).
7. pants: [GEAR] = orange adventure trousers with brown patches: one trouser leg on each leg silhouette, ending ABOVE the ankle (the lowest quarter of each leg silhouette stays bare grey), plus a brown belt on the torso silhouette's bottom edge.
8. boots: [GEAR] = a pair of purple boots with gold buckles and cream soles, one on each foot silhouette, short (ankle height only), the legs stay bare grey.

## 5. Cost
15 credits per call (Seedream 5 Lite image2image 2K; the price is finalised at generation and other aspect ratios or settings can differ). All 8 calls = **120 credits**; stage 1 (calls 1 to 4) = **60**. Balance 6,848 now: after stage 1 6,788, after all 6,728.

## 6. Output handling
Saved to Art/_candidates/parts/ and shown to you. `cut_parts.py` (to write) cuts every cell and shifts it back: one transparent PNG per part on the 1365 x 1649 character canvas, plus a parts.json per character; gear parts are cut by colour (the grey doll is the key). The game assembles a character from its parts and draws each gear part right above the part it covers.

## 7. Alternatives
Stage 1 only (60 credits): Momo + the three characters, check that the template method gives clean, complete parts, then I send the gear stage. Or 1 character only. Production later (not in this approval): 40 gear sheets.
