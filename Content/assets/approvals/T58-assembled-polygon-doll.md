# Approval packet T58: Momo and the Sporeling painted on the ASSEMBLED polygon doll

Status: PENDING OWNER APPROVAL. Nothing has been generated. Replaces the stage 2 gear step of T57 until the characters are right. Spent so far on T57 stage 1: 135 credits (balance 6,653 after; 9 images).

## 1. What went wrong in T57 stage 1 (my method)
The model does not fill a part polygon: it draws the part small on top of the grey shape, and every part comes out at a different scale (Momo's legs huge, his body small and bean shaped, hands tiny and slanted; the Sporeling's body is a small bottle, so its torso is effectively missing). Separate part images cannot keep consistent proportions.

## 2. New method
Paint the WHOLE character once on the assembled grey polygon doll (Art/_candidates/mannequin/mannequin_generic.png: big round head, rounded body, two arms, two legs with oval feet, on magenta). With an assembled doll the model followed the silhouette closely in the earlier mannequin tests, so proportions stay consistent. The standard polygons are then used as fixed cut zones (head, body, hands, legs with feet, optional back): each pixel goes to the polygon it falls in (ears, horns, hair and tails that stick out are assigned to the nearest polygon), giving one transparent PNG per part on the 1365 x 1649 canvas, in the standard polygon positions. Gear is made the same way later (assembled doll wearing head, body, boots), cut by the same zones.

## 3. MCP, model, mode
OpenArt MCP: free upload of the polygon doll (upload_sign + PUT), then `openart_generate_image` 2 calls (1 image each), `byte-plus-seedream-5-lite`, image2image, 2K, aspect 3:4, refs: style bible (vVnbzqvg1L5uSFCKydUE), the polygon doll, and for Momo his clean picture (ig6UtIp8Q7to2wWV6ibE).

## 4. Prompts (word for word)
Common start: "Match the art style of the first reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subjects described here. The second reference image is a plain grey doll made of simple polygons on a magenta background: a very big round head, a rounded body, two short arms at the sides and two short legs with oval feet. Paint exactly this doll as the character described below, in the same pose, size and position on a flat solid #808080 grey background: the head on the head polygon, the body on the body polygon (it must fill the whole body polygon), the arms on the arm polygons, the legs and feet on the leg polygons and foot ovals. Keep the proportions of every polygon; only ears, horns, hair or a tail may stick out a little. Thick dark navy outline, glossy shading, bright saturated colours. No clothes, no accessories, no text, no letters, no numbers, no watermark."
1. Momo: Common start + " The third reference image is the character Momo. The character is Momo exactly as in the Momo picture (fluffy cream hair, big pointed ears, brown eyes, pink scarf, cream body, pale blue cloud tail)."
2. Sporeling: Common start + " The character is a Sporeling, a little forest sprite: a round smooth lilac head with big dark glossy eyes, a happy small mouth, two glowing magenta cheek spots, a tiny curled sprout of lighter lilac hair and two small round purple ears; a full rounded cream stalk-like body with a soft purple underside and glowing cyan spots; smooth lilac arms with small round hands; short smooth lilac legs with round feet. No cap, no hat, no mushroom, not a cat, not furry."

## 5. Cost
15 credits per call, 2 calls = **30 credits** (price finalised at generation; other aspect ratios can differ). Balance 6,653 now (after T57 stage 1), 6,623 after. Two options each (4 images) = 60.

## 6. Output handling
Art/_candidates/polygons/out/doll_*.png, shown to you; a script (cut_polygon_character.py) cuts them by the polygon zones into Assets/Resources/Characters/<id>/ parts; I show the assembled result and the parts.

## 7. Alternatives
Two options per character (60 credits). Skip the Sporeling for now (15). Keep Momo's existing picture and cut it by the polygon zones with no credits (his proportions then do not follow the standard doll).
