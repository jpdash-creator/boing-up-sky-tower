# Approval packet T70: Flight power-up wings (glossy white feather wings with gold tips)

Status: PENDING OWNER APPROVAL. Nothing has been generated and no credits were spent. Source: owner issue list 29, item 4 ("the Flight wings look hideous").

## 0. What exists today (read-only check)
- `Assets/_Game/Gameplay/PowerGearArt.cs` `BuildWing()` bakes a 128 x 128 sprite in code (four light-blue/white ellipses with a navy outline, pivot (0.78, 0.52), 64 ppu = 2 world units wide). It is used for the left wing and mirrored (`flipX`) for the right.
- `Assets/_Game/Gameplay/RunView.cs`: `BuildPowerFx()` (line 926) creates `WingL` / `WingR` with `NewSprite(..., PowerGearArt.Wing, 8)` (sorting order 8, behind the character); `UpdateGearFx(cx, cy, h)` (lines 1002 to 1021, called at line 1152 with the player centre) shows them while `FlightTicks > 0`.
  - `playerHeight = 1.5f` world units (line 40). Scale `s = playerHeight * 0.95 / 2` = 0.7125, so each wing is about 1.43 u wide and 1.43 u tall (the character is 1.5 u tall).
  - Each wing sits at x = centre -/+ 0.22 * playerHeight (0.33 u), y = centre + 0.05 * playerHeight.
  - Flap (skipped when `reduced` motion is on): vertical lift 0.12 * playerHeight * sin(t * 14), y-scale 1 +/- 0.12, rotation +/- 14 degrees; the left wing rotates one way and the right the other.
- Consequence for the new art: a wing about 1.4 u tall and 1.4 u wide, pair centre on the character, so the pair spans about 2 x 0.33 + 1.43 = 2.1 u at rest, i.e. roughly 2x the body width (Momo's body is about 1.0 u wide). That matches the brief; no change in proportions is needed, only a new picture.

## 1. Purpose
One asset: **the Flight power-up wings**, replacing the code-drawn `power_wing`. A pair of big glossy WHITE feather wings with gold tips and a soft highlight, spread symmetrically as seen from the FRONT of the character, each wing about as tall as the character, the pair about 2x the body width. Drawn BEHIND the character, attached at the shoulders. Background flat #808080, nothing else (no character, no body, no text).

## 2. MCP and tool
OpenArt MCP (connector prefix `mcp__73f004bd-92f2-4e13-8abf-f0c13bc6ba8f__`), tool `openart_generate_image`, **1 call**, 2 images (options). No upload or project creation is needed (all references are already uploaded).

## 3. Model and mode
- Model `byte-plus-seedream-5-lite` (Seedream 5 Lite), mode **image2image**.
- Params: resolution **2K**, aspectRatio **4:3** (wide, the pair is about as wide as 1.5x its height), imageCount **2**, autoEnhancePrompt **false**, no seed (random, so the options differ).
- visualReferences (3), passed with their real urls (the earlier empty-url trick was silently dropped, so the style bible is NOT used):
  1. STYLE reference, type image, id `z3SsdRJRqpKz1192UlAL`, label "style reference (monk painting)", url `https://cdn.openart.ai/openart-ai/production/2026-11/create-image/eKcljWsHfmOD6SYI79NB/021793533628335e9db803be2da2d1fb0370cddd09ba5d6a20a7c_0_1793533696112_017d099d.png`
  2. SIZE reference, type image, id `ig6UtIp8Q7to2wWV6ibE`, label "Momo size reference (do not draw him)", url `https://cdn.openart.ai/openart-uploads/production/2026-10/create-image/eKcljWsHfmOD6SYI79NB/char_momo_1792832236441_b2c0996f.png`
- Reference 2 is optional: it helps the model get the "wing as tall as the character" scale but risks Momo appearing in the picture. The prompt forbids him; if you prefer the safest result, use the cheaper variant in section 7 (style reference only; same credits).

## 4. Prompt (word for word, one prompt for both options)
STYLE LOCK is the first sentence.

"Match the art style of the first reference image exactly (outline weight, glossy shading, colour saturation, top-left light), but draw only the new subject described here, not the reference's character, layout or objects. The second reference image shows a small cartoon character only to judge scale: do not draw that character or any body, head, arms or legs. Draw only a pair of big glossy WHITE feather wings, spread open symmetrically and seen from straight in front, a left wing and a right wing that mirror each other, joined by a small empty gap in the middle where a character's back would be. Each wing is as tall as the reference character from head to feet, the whole pair is about twice as wide as the reference character's body, swept up and outwards like an angel wing, made of three or four layers of big rounded overlapping feathers that get longer towards the outer edge, with a few long pointed primary feathers fanning out at the bottom. Pure white feathers with soft pale blue-grey shading between the layers, golden-yellow tips on the outer ends of the long feathers, a soft glossy white highlight along the top curve of each wing, a small golden sparkle near each tip. Thick consistent dark navy outline, saturated clean colours, glossy shading, clean vector-like edges, nothing cropped, a little margin all around. On a flat solid #808080 grey background, no shadow on the background. Nothing else in the picture: no character, no body, no hands, no text, no letters, no numbers, no watermark."

No competitor game names appear. The word "grey" appears only for the background; the wings themselves are white with pale blue-grey shading (keying note in section 6).

## 5. Cost (openart_model_cost, Seedream 5 Lite image2image, 2K, 4:3, imageCount 2)
- **15 credits per image = 30 credits per job**, **1 job**, 2 images. **Total: 30 credits.**
- Current balance (openart_account_get, Plus plan): **4,673 credits**. Balance after: **4,643**.
- The price is finalised at generation and other settings cost differently.

## 6. Output handling and what happens next
Saved to `Art/_candidates/power_wings/` (`flight_wings_a.png`, `flight_wings_b.png`) and shown to you LABELLED A and B. **You pick A or B**, or tell me what is wrong. If neither is right I come back with a new ask (15 to 30 credits per retry); nothing is re-run without your approval, and a failed call is not retried without asking.

After your pick, the following is **code and cutting work, no credits**:
1. Key the #808080 background to transparent (the white feathers and the pale blue-grey shading are far enough from mid grey in saturation/brightness for the existing keyer; check for grey halos, CUX-9), trim to the content bounds, and run `Tools/art_pipeline/qa_art.py` and a visual check on a dark and a light backdrop.
2. Decide the cut with you, my default is **one symmetric sprite (both wings in one picture)** since the picture is already symmetrical and attached in the middle:
   - Option 1 (default): one sprite `Assets/Resources/Art/Power/flight_wings.png`, pivot at the centre of the gap, 100 ppu import (or 64 to keep world sizes), drawn as one renderer behind the character.
   - Option 2: split the picture down the middle into `flight_wing_l.png` and `flight_wing_r.png` (pivot at the inner shoulder edge) so the two halves can flap in opposite directions like today. I recommend this only if you want the flap animation to look like two wings; otherwise Option 1 is simpler and cheaper to run.
3. Code in RunView (and PowerGearArt as the loader with the existing code-drawn wing kept as the visible fallback when the PNG is missing, CUX-10): load via `Resources.Load<Sprite>("Art/Power/flight_wings")`; scale so the sprite height equals `playerHeight` (about 1.5 u) and the width follows from the art (target about 2x body width, roughly 2.1 u); sorting order 8 (behind the character), anchored on the player centre at shoulder height (`cy + 0.05 * playerHeight`); flap (small rotation/scale pulse of the halves, or a slight vertical bob for the single sprite) **only when reduced motion is off**; with reduced motion the wings are static. Allocation free (CUX-1): sprites and transforms cached, no per-frame `new`.
4. Tests: a `CriticalUX` test that the wing sprite loads and falls back when absent; the existing power-up/RunView tests stay green; a layout/visual check that the wings do not cover the next platforms or hazards beyond what the code-drawn ones did (CUX-3), at 9:16 and 9:21; reduced-motion test that no flap is applied.
5. Version bump and build report as vMAJOR.MINOR.HOTFIX.BUILD.

## 7. Alternatives and savings
- **One option instead of two**: 1 image = 15 credits (saves 15), balance after 4,658. Riskier: no choice if it comes out wrong.
- **Style reference only (no Momo)**: same 30 credits; avoids any chance of Momo being painted in but the wing size relative to him is then only by prompt. Fewer references does not change the price.
- **Draw it in code again (0 credits)**: improve the SDF shapes (feather layers, gold tips) in `PowerGearArt`; no cost, but it is the approach that currently "looks hideous".
- A cheaper model is not proposed: Seedream 5 Lite already costs 15 credits per image and is the one the rest of the game art was made with, so the style matches.

## 8. Open questions for the owner
1. **One sprite or two halves** (section 6, step 2)? Default: one sprite.
2. **Include the Momo size reference** (default yes) or style reference only?
3. **Aspect 4:3** assumes a pair a bit wider than tall. If you want the wings to reach higher (more upright, "angel" shape), 1:1 gives the same price.
4. **Wings drawn bigger than the character?** The brief says each wing about as tall as the character (1.5 u); say so if you want larger wings than today's 1.43 u.
5. **Gold tips only** (as asked) or also a thin gold edge along the top curve? Default: tips only.
