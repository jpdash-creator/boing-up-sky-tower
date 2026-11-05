# Approval packet T81: Momo Rare (Region 1 hero line, step 1 of 3: Rare, then Elite, then Legendary)

Status: PENDING OWNER APPROVAL. Nothing generated.

1. Purpose: Rare tier art for Momo ("Stormguard Momo"), the next step of the armour-grows-each-tier line (Common = fluffy white cat-creature with pink scarf; Uncommon = Cloudguard Momo, light silver armour with sky-blue cloud emblem and short cape). Elite and Legendary follow only after you pick the Rare.
2. MCP and tool: OpenArt MCP (`mcp__73f004bd-92f2-4e13-8abf-f0c13bc6ba8f__`), `openart_generate_image`, 1 call. Before it, one free upload of the reference (`openart_upload_sign` + PUT).
3. Model and mode: `byte-plus-seedream-5-lite`, image2image, 2K, aspect 3:4, imageCount 2, no seed. Reference: the picked Uncommon Momo (Art/_candidates/uncommon_heroes/momo_2.png).
4. Prompt, word for word: "Keep exactly the same character as the reference image (face, hair, colours, proportions, size, pose, outline weight, glossy shading, art style), the same full-body standing front view, and the same flat solid #808080 mid-grey background. Upgrade it to Rare rarity with sturdier armor and glowing cloud runes: a full silver-blue chest plate with a large glowing sky-blue cloud emblem, bigger layered shoulder guards with small cyan rune lines, light bracers on the forearms, and a longer sky-blue cape with a white cloud trim clipped at the pink scarf. The armor is heavier than the reference but never hides the face, no weapons, nothing held in the hands, no text, no extra characters. Clean vector-like edges, solid flat #808080 mid-grey background."
5. Cost: 1 call x 2 images x 15 = **30 credits** (same setup as T75). Balance **3,638**, after **3,608**.
6. Output handling: candidates to Art/_candidates/rare_heroes/ (momo_1, momo_2), shown to you with tags; you pick one; then cut into the doll parts like the Uncommon (polygon pipeline), QA, added as `momo_r`, logged as T81. Then the Elite packet (T82) uses your Rare pick as the reference.
7. Alternatives: one option only (15 credits).
