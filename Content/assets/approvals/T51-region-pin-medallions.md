# Approval packet T51: painted region pin medallions (30 round map pins)

Status: PENDING OWNER APPROVAL. Nothing has been generated.

- Purpose (owner 2026-10-24): each of the 30 regions gets its own painted round pin on the world map, made as separate images so it never conflicts with the map paintings (the map pages are marker-free). docs/16 section D: 30 medallions, 5 sheets of 6.
- What the code does with them: the medallion is the pin of its region. A region that is locked is drawn grey with the padlock icon over it, the current region gets a gold ring and a gentle bounce, a finished region gets a star ribbon. So the art is one clean, unlocked picture per region.
- MCP / tool: OpenArt MCP, `openart_generate_image`, 5 jobs, one per sheet of 6 medallions, 1 image each.
- Model / mode: byte-plus-seedream-5-lite (Seedream 5 Lite), image2image, 2K, aspect 3:2, 1 image per job, style bible vVnbzqvg1L5uSFCKydUE as the style reference (and the picked UI kit icon sheets for the glossy look).
- Cost: 15 credits per image, 5 images = 75 credits. Balance about 7,118, after about 7,043.
- Alternative: two images per sheet = 150 credits, to choose between candidates per sheet. Or do sheet 1 first (15 credits), check the look, then the other four.
- Output: Art/_candidates/pins_sheetN/, shown to you labelled; process_art.py cuts every medallion with a transparent background; they are named pin_<region slug>.png in Assets/Resources/Art/Pins and wired in code.

## Prompt template (the [LIST] is one sheet's six regions, left to right, top row then bottom row)
Match the art style of the reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new subjects described here. A sprite sheet of six round map pin medallions for a cute mobile game, in a grid of three columns and two rows on a flat solid #808080 background, generous empty space around every medallion, nothing overlapping. Every medallion is a perfectly round glossy white-cream button plate with a thin golden rim and a thick dark navy outer outline, the same size and the same plate in all six, and inside the plate sits ONE small glossy picture of that region's signature object, centred, with a soft shadow, bright saturated colours. The six regions, left to right, top row then bottom row: [LIST]. No text, no letters, no numbers, no characters, no watermark.

## Lists per sheet (the signature object of each region)
1. Meadow Springs (a pink flower on a green hill); Mushroom Grove (a glowing purple mushroom); Sunny Beach (a palm tree and a wave); Candy Canyon (a pink and mint swirl lollipop); Bamboo Temple (a green pagoda); Frosty Peaks (a snowy blue mountain).
2. Autumn Orchard (a red apple with an orange leaf); Desert Oasis (a palm and a small pond in sand); Pirate Cove (a pirate ship); Toy Factory (colourful toy blocks and a gear); Haunted Manor (a spooky purple mansion); Crystal Caverns (a blue and pink crystal cluster).
3. Volcano Forge (a volcano with lava and an anvil); Jungle Ruins (an overgrown stone temple); Neon City (glowing city towers); Deep Sea Reef (coral and a fish); Moon Carnival (a ferris wheel under the moon); Arcade Realm (a pixel arcade cabinet).
4. Steampunk Skyport (a brass airship); Cloud Kingdom (a white castle on a cloud); Storybook Castle (a fairy-tale castle with blue roofs); Samurai Dojo (a red torii gate with cherry blossoms); Pyramid Sands (a golden pyramid); Viking Fjord (a longship with a round shield).
5. Wild West Mesa (a red rock mesa with a cactus); Dino Valley (a friendly green dinosaur); Dragon Peak (a red dragon head); Alien Jungle (a glowing green alien plant); Space Station (a space station with a planet); Rift Of Time (a glowing clock portal).
