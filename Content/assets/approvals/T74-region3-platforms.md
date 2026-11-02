# Approval packet T74: Region 3 (Sunny Beach) new special platforms, 3 sheets (3 + 2 + 2 kinds), 2 options each

Status: PENDING OWNER APPROVAL. Nothing generated, no credits spent (only free read-only calls: `openart_account_get`, `openart_model_cost` list). Design: docs/30-regions-2-to-5-platforms.md section 4 (kinds 3.1 to 3.7). Method = what worked for Region 2 (T73b): text2image, 3:4, six rows, no reference.

## 1. Purpose and the 7 platform variations
Painted art for 7 new Region 3 platform kinds (3 helpful, 2 hard, 2 hazard), sliced like Region 2:
- Sheet A (helpful): Surf Boost Wave, Sandcastle Step, Clam Shell Pad (2 painted poses: open, shut)
- Sheet B (hard): Sinking Raft, Turtle Shuttle
- Sheet C (hazard): Kelp Tangle, Pufferfish Pad (3 painted poses: calm, swelling, puffed)
Drifting platforms stay Region 3's signature; Crumble and Vanishing stay as generic kinds (no art needed). Owner rule: no code-drawn cues, so the paint itself must make each kind readable.

## 2. MCP and tool
OpenArt MCP (connector `mcp__73f004bd-92f2-4e13-8abf-f0c13bc6ba8f__`), tool `openart_generate_image`, 3 calls (A, B, C), run one after the other.

## 3. Model and mode
`byte-plus-seedream-5-lite` (Seedream 5 Lite), mode text2image, resolution 2K, aspectRatio 3:4, imageCount 2 per call, no seed field, no reference images (text only, like T73b).

## 4. Prompts, word for word
COMMON PREFIX (all three): "A sprite sheet of thin side-view platform slabs for a cute chibi cartoon 2D mobile game, region 'Sunny Beach' (a bright noon beach with gold sand, aqua sea, white foam and coral pink), in a glossy cartoon style, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light. Layout: EXACTLY SIX horizontal rows, no fewer and no more, evenly spaced from the top to the bottom of the tall picture, on a flat solid #808080 background, one platform per row, grouped by kind with a clearly larger empty gap between groups, every platform left-aligned to the same left margin, nothing overlapping. All platforms have exactly the same thickness and the same side-view style, and within a group differ only in length. Every platform is a slim flat slab seen straight from the side: a perfectly flat straight top edge that clearly reads as a walkable surface, with a clean rounded cap at both ends."

SHEET A text: " Six rows in three groups of two rows. Group 1 (rows 1 and 2), Surf Boost Wave: the slab is a long aqua wave slab whose front tip curls forward, with white foam streaks along its whole length all pointing the same way; the first row is 8 times as long as it is thick and the second row 12 times. Group 2 (rows 3 and 4), Sandcastle Step: a sand-coloured block slab with TWO levels, the left half flat and the right half raised by about 0.9 of the slab thickness with a clean vertical riser between them, flat tops on both halves, one tiny shell decoration; the first row is 7 times as long as it is thick (measured at the low half) and the second row 10 times. Group 3 (rows 5 and 6), Clam Shell Pad, two poses of the same giant pink-white clam slab, both rows exactly 6 times as long as they are thick: row 5 open pose, the upper shell lifted wide open showing a glowing pearl on a flat pink inner floor; row 6 shut pose, the clam fully closed as a plain flat slab with a ridged shell and a hinge line. Both poses keep the same flat bottom edge and the same left margin."

SHEET B text: " Six rows in two groups of three rows. Group 1 (rows 1 to 3), Sinking Raft: the slab is a lashed driftwood plank raft with rope ties, slightly waterlogged and darker at one end, a few water drops on its edge; the three rows are 5 times, 8 times and 12 times as long as they are thick. Group 2 (rows 4 to 6), Turtle Shuttle: the slab is a green sea-turtle shell with a flat top, with the turtle's head and front flipper visible at the right end and the tail at the left end; the three rows are 5 times, 8 times and 12 times as long as they are thick measured over the shell only."

SHEET C text: " Six rows in two groups of three rows. Group 1 (rows 1 to 3), Kelp Tangle: the slab is wrapped and tangled in dark green kelp strands that hang over its top and ends, with a couple of bubbles caught in the strands; the three rows are 4 times, 7 times and 12 times as long as they are thick. Group 2 (rows 4 to 6), Pufferfish Pad, three poses of the same yellow pufferfish slab, all three rows exactly 6 times as long as they are thick at the base: row 4 calm pose, a smooth yellow pufferfish slab with a relaxed face and tiny fins; row 5 swelling pose, the same fish inflated and rounder with a worried face and tiny bumps along its back; row 6 puffed pose, the same fish fully puffed with short sharp spikes along its top and sides and wide eyes. All three poses keep the same flat bottom edge and the same left margin."

COMMON SUFFIX (all three): " Decoration is sparse and non-repeating so nothing looks stretched. No sky clouds, no characters, no arrows, no text, no watermark. palette: gold, aqua, white foam, coral pink. cute chibi cartoon, thick consistent dark-hue outline, saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges, solid flat #808080 mid-grey background"

No competitor game names appear. Each call sends PREFIX + sheet text + SUFFIX as one prompt.

## 5. Cost
Region 2 (T73b) priced the same setup (text2image, 2K, 3:4, 2 images) at 30 credits per call. The exact quote is checked again before the first call.
- Jobs: 3 calls x 2 images = 6 images; **total 90 credits**
- Balance now (`openart_account_get`, Plus plan): **4,358**; balance after: **4,268**

## 6. Output handling
Saved to Art/_candidates/reg003_new_platforms/ (A1 A2, B1 B2, C1 C2), contact sheet shown to you, you pick one option per sheet. Rows checked first (six rows, same thickness, no clouds/characters/arrows/text, poses share a base). Then sliced, QA'd (qa_art.py), baked widths, Sim kinds built per docs/30 section 4 (about 63 h in the doc estimate, done in stages, bot-proven). Logged in Content/assets/assets_log.csv as T74, actual credits and balance reported after each call. A failed sheet is reported, never retried without a new ask.

## 7. Alternatives
- One option per sheet (imageCount 1): 45 credits, saves 45; riskier if rows drift.
- Two sheets only (A and C): 60 credits, saves 30; Sinking Raft and Turtle Shuttle wait.
