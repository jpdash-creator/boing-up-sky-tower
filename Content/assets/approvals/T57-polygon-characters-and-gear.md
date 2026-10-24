# Approval packet T57: polygon framework: Momo, a second character and 3 gear sets (everything from OpenArt)

Status: PENDING OWNER APPROVAL. Nothing has been generated. Design: docs/19-polygon-character-and-gear-framework.md. Replaces T56 (stage 1 images are kept only as rejected candidates).

## 1. Purpose
Owner issue list 22 (2026-10-24): every character and every gear piece is made with OpenArt in a STANDARD POLYGON FRAMEWORK so gear fits any character. Test set: Momo and a second character (a Sporeling without the mushroom head, region 2) and 3 gear sets (head gear, body gear with short sleeves so hands show, short boots), tested on both.
Method: grey polygon templates on magenta (Tools/art_pipeline/make_polygon_templates.py, Art/_candidates/polygons/*.png, 2880 x 2160 = 4:3). The model fills exactly the polygon, so every part has the standard outline and cuts out cleanly by colour (grey and magenta are never used by characters or gear).

## 2. MCP and tools
OpenArt MCP: `openart_upload_sign` plus an HTTP PUT for the 8 templates (free, not charged), then `openart_generate_image`, 12 calls, 1 image each (staged, section 7).

## 3. Model and mode
`byte-plus-seedream-5-lite`, image2image, 2K, aspect 4:3, imageCount 1. References: 1 the style bible (vVnbzqvg1L5uSFCKydUE); 2 the matching polygon template; 3 (Momo calls only) the clean Momo picture (upload id ig6UtIp8Q7to2wWV6ibE).

## 4. Prompts (word for word)
Common start: "Match the art style of the first reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subjects described here. The second reference image is a template: flat grey polygon shapes on a magenta background. Keep the magenta background and keep every polygon exactly in its position, size and outline; fill the polygon(s) with the artwork described below and never draw outside them except for the small features named. Thick dark navy outline, glossy shading, bright saturated colours. No text, no letters, no numbers, no watermark."

Characters (template, then prompt tail):
1. Momo head (char_head): " The third reference image is the character Momo. Draw Momo's head only, exactly as in the Momo picture: fluffy cream hair, big pointed ears (the ears may rise above the polygon), brown eyes, small nose, blush. No neck, no body, no scarf."
2. Momo body (char_body): " The third reference image is the character Momo. Draw Momo's body only: his cream chest and belly with the pink scarf around the top, exactly as in the Momo picture, no head, no arms, no legs."
3. Momo hands (char_hands): " The third reference image is the character Momo. Draw Momo's two arms and paws only, one in each polygon, cream fur with small paw lines, exactly as in the Momo picture."
4. Momo legs (char_legs): " The third reference image is the character Momo. Draw Momo's two legs and feet only, one leg with its foot per pair of polygons, cream fur with toe lines, exactly as in the Momo picture."
5. Momo back (char_back): " The third reference image is the character Momo. Draw only Momo's pale blue cloud tail inside the polygon, glossy, with a small sparkle."
Sporeling (second character, region 2; no mushroom head, not a cat, not furry):
6. head (char_head): " Draw the head only of a Sporeling, a little forest sprite: a round smooth lilac head with big dark glossy eyes, a happy small mouth, two glowing magenta cheek spots, a tiny curled sprout of lighter lilac hair on top, two small round purple ears. No cap, no hat, no mushroom, no neck, no body."
7. body (char_body): " Draw the body only of a Sporeling: a small cream stalk-shaped body with soft purple underside and a few glowing cyan spots, no head, no arms, no legs."
8. hands (char_hands): " Draw the two arms of a Sporeling, one in each polygon: smooth lilac skin with small round hands, a glowing magenta spot on each."
9. legs (char_legs): " Draw the two legs and feet of a Sporeling, smooth lilac skin, round small feet, a glowing cyan spot on each foot."
Gear (3 variants side by side in the three cells of each template; the grey polygon stays flat grey where there is no garment; the garments must never be grey, magenta, cream or white):
10. head gear (gear_head): " Draw three different head gear pieces, one on each grey head polygon: left a green ranger hood with a red feather, middle a blue steel knight helmet with a white plume (plume may rise above), right a golden crown with a red gem. Each fits the head polygon with holes for the ears."
11. body gear (gear_body): " Draw three different body gear pieces, one on each group (a body polygon with a hand polygon on each side): left a green ranger tunic with a brown strap and short green sleeves that cover only the upper half of the hand polygons, middle a blue steel breastplate with short steel pauldron sleeves, right a red and gold royal vest with short gold sleeves. The lower half of each hand polygon stays bare grey so hands stay visible."
12. boots (gear_boots): " Draw three different pairs of short boots, one pair on each group of two legs with feet: left brown leather boots with green trim, middle blue steel boots, right golden boots with red gems. The boots cover only the feet and the ankle; the legs above stay bare grey."

## 5. Cost
15 credits per call (Seedream 5 Lite image2image 2K; the price is finalised at generation and other settings can differ). All 12 calls = **180 credits**; stage 1 (characters, calls 1 to 9) = **135**; stage 2 (gear, calls 10 to 12) = **45**. Balance 6,788: after all 6,608.

## 6. Output handling
Saved to Art/_candidates/polygons/out/ and shown to you. A script cuts each image by colour, maps it back into the assembled doll with polygons.json, and writes Assets/Resources/Characters/<id>/{back,legL,legR,body,handL,handR,head}.png and Assets/Resources/Characters/gear/<piece>/... (head gear head.png; body gear body.png, sleeveL.png, sleeveR.png; boots bootL.png, bootR.png). Tested wearing all 3 sets on Momo and on the Sporeling.

## 7. Alternatives
Stage 1 only (135 credits), then gear. Momo and the gear only (no second character yet): 8 calls = 120. One character part at a time for review.
