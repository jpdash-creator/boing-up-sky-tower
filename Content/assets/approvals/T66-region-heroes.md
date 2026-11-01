# Approval packet T66: four new Common region heroes (R2 to R5) and the "new hero joins you" reveal background

Status: PENDING OWNER APPROVAL. Nothing has been generated and no credits were spent. Source: owner issue list 28, item 3.
Method follows T58 (character painted once on the assembled grey polygon doll, then cut by polygon zones), the way Momo and the Sporeling were made (docs 19, 20, 21; Tools/art_pipeline/cut_polygon_character.py).

## 1. Purpose
All new art, nothing reused:
1. **Mushroom girl**, Common hero for region 2 (Mushroom Grove).
2. **Beach hero**, Common hero for region 3 (Sunny Beach).
3. **Candy hero**, Common hero for region 4 (Candy Canyon).
4. **Furry Shaolin monk**, Common hero for region 5 (Bamboo Temple).
5. **One reveal background** for the "New hero joins you!" pop-up shown when a region unlock grants its Common hero: a glossy burst / ribbon / panel with rays, no characters, no text (the hero art and the title are drawn on top by code).

Common heroes have no power (doc 21), so the heroes are plain characters on the standard doll; no gear exists any more (gear dropped 2026-11-01), so unlike T58 the character may wear its own outfit as part of its parts.

## 2. MCP and tool
OpenArt MCP (connector prefix mcp__73f004bd-92f2-4e13-8abf-f0c13bc6ba8f__), tool `openart_generate_image`, **5 calls** (4 heroes + 1 popup), 2 images (options) per call.
The grey polygon doll (upload id vv2DihV31TsEhrDdDju9, the same one used by T58 and T59) and the style bible are already uploaded; no new upload is needed (upload_list is free if the ids must be re-checked).

## 3. Model and mode
`byte-plus-seedream-5-lite` (Seedream 5 Lite), mode image2image, resolution 2K, imageCount 2, autoEnhancePrompt off.
- Heroes (calls 1 to 4): aspect ratio **3:4**, same as T58/T59 and the cutter's 1365 x 1649 canvas. References: 1 style bible (vVnbzqvg1L5uSFCKydUE), 2 the grey polygon doll (vv2DihV31TsEhrDdDju9).
- Popup (call 5): aspect ratio **3:4 (portrait)**, because the pop-up is a portrait card on 9:16 to 9:21 phones. References: the style bible only.
- No seed (random), so the 2 options differ.

## 4. Prompts (word for word)

**HERO COMMON START** (same text as T58, with the "no clothes, no accessories" sentence replaced so each hero keeps its theme outfit as painted-in part of the body):
"Match the art style of the first reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subjects described here. The second reference image is a plain grey doll made of simple polygons on a magenta background: a very big round head, a rounded body, two short arms at the sides and two short legs with oval feet. Paint exactly this doll as the character described below, in the same pose, size and position on a flat solid #808080 grey background: the head on the head polygon, the body on the body polygon (it must fill the whole body polygon), the arms on the arm polygons, the legs and feet on the leg polygons and foot ovals. Keep the proportions of every polygon; only ears, horns, hair, a hat brim or a tail may stick out a little. Standing, facing front, arms at the sides, feet apart on the foot ovals, nothing held in the hands. Thick dark navy outline, glossy shading, bright saturated colours. Never use grey or magenta on the character. No text, no letters, no numbers, no watermark."

STYLE LOCK is the first sentence of the common start (match the first reference's outline weight, glossy shading and saturation). No competitor game names appear anywhere.

1. **R2 Mushroom girl**: HERO COMMON START + " The character is a cute mushroom girl, a little forest sprite child: a big round pale cream face with huge dark sparkling eyes, a small happy smile and soft pink blush, wearing as her hair a big glossy red mushroom cap with white spots that sits on the head polygon and may overhang it a little; a small cream body with a leaf-green dress ending in a ruffled hem and a tiny brown acorn belt; cream arms with small round hands; short cream legs with little round moss-green boots. Cheerful and sweet, clearly different from a lilac sprite with a sprout, not a cat, not furry."
2. **R3 Beach hero**: HERO COMMON START + " The character is a cute little beach hero, a chubby sun-tanned sea otter kid: soft short golden-brown fur, a round head with big dark friendly eyes, a tiny nose, small round ears and a cheerful open smile; a bright yellow sun hat with a turquoise band on the head polygon (the brim may stick out a little); a bright coral-red and white striped swim top and turquoise board shorts on the body polygon; one small white seashell necklace; short furry arms with small paws; short furry legs with bare round feet."
3. **R4 Candy hero**: HERO COMMON START + " The character is a cute candy girl, a little candy sprite: a round smooth pastel pink face with big sparkling dark eyes, a happy small mouth, mint-green cheek dots, and as her hair a tall glossy pink and white swirled lollipop-style twirl with two small bows of blue ribbon; a body shaped like a wrapped sweet in a glossy lilac and white striped wrapper with a golden bow, twisted wrapper ends at the sides; pink arms with small round hands; short pink legs with round chocolate-brown boots with cream sprinkles."
4. **R5 Furry Shaolin monk**: HERO COMMON START + " The character is a cute furry little Shaolin monk, a chubby fluffy golden-orange bear-cub like kung-fu apprentice: soft fluffy fur, round head with small round ears, big dark gentle eyes with a calm smile, a cream muzzle, a small golden-orange headband tied at the front (no clothes on the head otherwise); a saffron-orange and brown monk robe with a crossed collar and a dark red sash on the body polygon; wide short robe sleeves covering only the upper part of the arms, furry paws showing; short furry legs with soft cloth shoes in dark red with cream soles."
5. **Reveal pop-up background** (separate call, style bible only): "Match the art style of the reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subject described here, not the reference's layout or objects. A portrait game pop-up background for a 'new hero joins you' reveal, no characters and no creatures: a large glossy rounded-rectangle panel in rich purple with a thick golden border and a soft inner glow, behind it a big radial sunburst of bright golden and orange rays spreading from the centre and beyond the panel edges, a wide golden ribbon banner folded across the top with an empty plain face, a few gold stars and white sparkles around, confetti in blue, pink and green; the centre of the panel is a soft empty bright glow so a character can stand there. Everything is symmetrical and the panel and ribbon are completely empty. Thick consistent dark navy outline, saturated gradient colours, glossy sparkle accents, top-left light, clean vector-like edges, on a flat solid #808080 mid-grey background. No text, no letters, no numbers, no watermark."

## 5. Cost (openart_model_cost, `byte-plus-seedream-5-lite` image2image, 2K)
- Heroes: 3:4, imageCount 2: **15 credits per image = 30 per call**, 4 calls = **120 credits** (8 images).
- Popup: 3:4, imageCount 2: 30 credits, 1 call = **30 credits** (2 images).
- **Total: 5 jobs (calls), 10 images, 150 credits.**
- Current balance (openart_account_get, Plus plan): **4,928 credits**. Balance after: **4,778**.
- The price is finalised at generation and other settings cost differently.

## 6. Output handling and what happens next
Saved to Art/_candidates/region_heroes/ (r2_mushroom_a/b.png, r3_beach_a/b.png, r4_candy_a/b.png, r5_monk_a/b.png, reveal_a/b.png) and shown to you LABELLED A and B per hero.
**You pick 1 of 2 options per hero (4 picks) and 1 of 2 for the pop-up.** If no option of a hero is right I come back with a new ask (15 to 30 credits per retry); nothing is re-run without your approval.

After your picks, the following is **code and cutting work, no credits**:
1. Cut each picked picture into the 7 standard rig pieces (back, legL, legR, body, handL, handR, head) with `Tools/art_pipeline/cut_polygon_character.py` (key the grey, register to the 1365 x 1649 canvas, ECC onto the doll, `--zones doll`). The Sporeling needed hand-measured `--zones spore` partition lines, so expect a custom zone set per hero where an outfit or hat overlaps (mushroom cap, sun hat brim, monk sleeves). Check each `preview.png` against the painting and the CUX-9 art QA (`qa_art.py`).
2. Write `Assets/Resources/Characters/<id>/{back,legL,legR,body,handL,handR,head}.png` + `rig.json` (same order as the Sporeling) with import settings (centre pivot, 100 ppu).
3. Code: `CharacterSpec` rows in Economy/Characters.cs and `CharacterDef` entries in CharacterRig.cs (CharacterCatalog), ids e.g. mushroomgirl, beachotter, candysprite, monk; region assignment (see open question 1); owned-on-region-unlock rule; names; Characters screen roster (5 per row).
4. Tests: update CharacterRigTests (roster test), FlagTests (hidden until their region unlocks), the CriticalUxTests roster/layout test (more tiles on the Characters screen), and add rig-part presence tests for every new id.
5. The reveal pop-up: import the background (9-slice or full picture, safe-area fit at 9:16, 9:19.5, 9:21 and 3:4, CUX-3), build the "New hero joins you" popup scene on it with the hero doll, name and rarity box, a Continue button (48 dp, CUX-4), text on a backing panel (CUX-5), reduced-motion safe (no flashing above 3 per second, CUX-6), and its layout test. A short reveal animation is code only.
6. Version bump and build report as vMAJOR.MINOR.HOTFIX.BUILD.

## 7. Alternatives and savings
- **One option per hero and for the popup**: 5 calls x 1 image = **75 credits** (saves 75), balance after 4,853. Riskier: no choice if the single result is off.
- **Heroes only, 2 options each (120)**, popup built by code from existing popup frame art (T28) with no credits (saves 30).
- **Two heroes first** (R3 and R5, the least like the existing characters), 2 options each: 60 credits, then ask again for the rest.
- A cheaper model is not proposed: Seedream 5 Lite is the one that followed the doll in T58, and a different model would not match the earlier characters.

## 8. Open questions for the owner
1. **Region 2 already has a Common: the Sporeling** (Characters.cs: Common, Region = 2; "a region unlock gives its Common character", doc 21). Should the new mushroom girl **replace** the Sporeling in region 2, or be a **second** Common of region 2 (then region 2 gives two heroes and `CharacterSpec.Region` plus the unlock rule need a list instead of one region), or become Uncommon+ later? If she replaces it, the Sporeling stays as a possible chest or shop character.
2. **Hero designs I chose for you** (change any before approval): beach hero as a sun-tanned sea otter kid in a sun hat and swim top; candy hero as a lollipop-haired sweet-wrapper girl; monk as a golden-orange bear cub in a saffron robe (not a panda). Do you want a human-looking beach or monk instead, and the names (ids and display names)? Default names: Capi (mushroom girl), Otto (beach), Bonbon (candy), Bao (monk).
3. **Outfits painted into the character**: since gear is dropped, hats, robes and dresses are part of the cut pieces. A wide mushroom cap or hat brim goes beyond the standard head polygon (ears of Momo did too). Is it fine for these to overhang the head slightly, or must they stay inside the polygon (a rig and hitbox question, CUX-3)?
4. **Tails or capes** are not asked for (no `back` piece); the cutter's back rule is for the right side only. OK that all four have an empty `back` (like the Sporeling), or do you want a tail on the otter?
5. **Pop-up shape**: portrait 3:4 as proposed, or should it be a wide 4:3 banner at the top of the screen? Also: should the pop-up be used for every new hero only, or also for the Uncommon+ characters later (then the purple panel colour needs a rarity variant, tinted in code)?
6. **Rarity box colour** for Common is light grey with white; the pop-up purple panel is fine with that, or should the pop-up follow the rarity colours?

## Owner picks and T67 (2026-11-01)
- Picks from T66: R3 beach otter = A; R5 monk = B; pop-up background = B. R2 and R4 v1 were rejected (hands and parts did not look like one character; candy hair/body not wanted).
- T67 (owner approved 60 credits, 2 calls x 2 images): R2 = forest girl wearing a mushroom-cap hat (historyId lHYGXltLbE3PNkH2Z1vJ), R4 = candy-shop girl with normal hair (historyId cb2DxnFp8davUaq1dckw). Prompts as shown to the owner in chat (shared start with the anatomy rule + the two character tails). Sporeling is replaced and deleted.
