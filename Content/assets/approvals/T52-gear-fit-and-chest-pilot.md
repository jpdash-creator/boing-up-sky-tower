# Approval packet T52: gear fitting pilot (4 sheets) and the two treasure chests (2 images)

Status: PENDING OWNER APPROVAL. Nothing has been generated. Prepared with read-only OpenArt calls (account, model cost, upload list).

## 1. Purpose
Owner issue lists 19 and 20 (2026-10-24): gear must be wearable (fitted layers), chests must be painted. Spec: docs/17-gear-inventory-chests-characters.md.
- 4 gear sheets (head, body, pants, boots): each shows Momo six times in the same pose and place, each wearing one piece of that slot from Common to Legendary. A script compares every cell with the clean Momo picture and cuts out the worn layer. This pilot tells us whether the generated pieces can be cut into fitted layers (alignment, clean outline, no changes to Momo). Only if it works do I send the large packets (240 pieces).
- 2 chest pictures: Iron chest and Golden chest for the chest shop (docs/17 section D).

## 2. MCP and tool
OpenArt MCP (`mcp__73f004bd-92f2-4e13-8abf-f0c13bc6ba8f__`), `openart_generate_image`, 6 calls, 1 image each. Before the calls: `openart_upload_import` of the clean Momo picture (Assets/Art/Characters/char_momo/char_momo/char_momo.png) as a reference (free, not charged).

## 3. Model and mode
`byte-plus-seedream-5-lite` (Seedream 5 Lite), image2image, 2K, aspect 4:3, 1 image per call. References: the UI style bible (resource vVnbzqvg1L5uSFCKydUE) first, the Momo upload second (gear sheets only; the chests use the style bible only). No seed field.

## 4. Prompts (word for word)
Common start: "Match the art style of the first reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subjects described here."

1. Head sheet: Common start + " The second reference image is the character Momo. Draw Momo six times in a grid of three columns and two rows on a flat solid #808080 background, same front view, same size and same position inside every cell as in the reference, with generous empty space between cells. Every Momo is identical to the reference (body, pink scarf, arms, legs and feet unchanged) except for ONE head accessory fitted to the head and ears: top row left to right a plain cloth cap, a green leaf crown, a blue knight helmet with holes for the ears; bottom row left to right a purple wizard hat with holes for the ears, a golden crown with a glowing gem, a simple headband with a small bell. No text, no letters, no numbers, no watermark."
2. Body sheet: same, with: "...except for ONE body garment fitted over the torso and arms, the head, legs and feet unchanged: top row a plain cloth vest, a green hooded cloak, a blue plate armour; bottom row a purple star robe, a golden royal cape with a gem clasp, a simple striped sweater."
3. Pants sheet: same, with: "...except for ONE leg garment fitted over the legs, everything above the waist and the feet unchanged: top row plain short trousers, green leaf shorts, blue armoured leg guards; bottom row purple star trousers, golden royal trousers with gem buttons, simple striped socks and shorts."
4. Boots sheet: same, with: "...except for ONE pair of footwear fitted over the feet, everything else unchanged: top row plain cloth shoes, green leaf boots, blue knight boots; bottom row purple star boots, golden winged boots, simple red sneakers."
5. Iron chest (no Momo): Common start without the second reference + " A single treasure chest for a cute mobile game, three-quarter front view, centred on a flat solid #808080 background with generous empty space. Iron chest: light steel blue metal bands and corners with orange brown wooden panels, a round steel lock plate with a keyhole on the front, small rivets, glossy shading and a thick dark navy outline, bright saturated colours, a soft shadow under it. No text, no letters, no numbers, no watermark."
6. Golden chest: same with: "Golden chest: shiny gold metal frame and corners with deep red brown wooden panels, a star shaped gold lock plate with a keyhole on the front, tiny sparkles, glossy shading and a thick dark navy outline, bright saturated colours, a soft shadow under it."

## 5. Cost
`openart_model_cost`: Seedream 5 Lite image2image 2K 4:3, 1 image = 15 credits per job. 6 jobs = **90 credits**. Balance now 7,043, after 6,953. (Other settings cost differently.)

## 6. Output handling
Results are saved under Art/_candidates/gear_pilot/ and Art/_candidates/chests/, shown to you labelled, logged in Content/assets/assets_log.csv. Then: cut and align test of the 24 worn pieces; I report whether they cut cleanly. Chests are cut to transparent PNGs and wired into the chest screen.

## 7. Alternatives
- Chests only: 2 jobs = 30 credits (gear stays code-drawn placeholders).
- Two images per sheet to choose from: 180 credits.
- A stronger model (Seedream 5 Pro image2image 1K, 30 credits per job) for the 4 gear sheets: 4 x 30 + 30 = 150 credits.
