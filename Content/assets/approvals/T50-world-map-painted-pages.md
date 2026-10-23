# Approval packet T50: six painted world-map pages (5 regions each)

Status: PENDING OWNER APPROVAL. Nothing has been generated.

- Purpose (owner 2026-10-24): replace the 10 painted map segments with six tall painted images, 5 regions each, joined into one vertical scroll (region 1 at the bottom, region 30 at the top). The pins, names and star pills are drawn on top in code, so the painting only needs five themed areas along a winding path. docs/16 section D.
- MCP / tool: OpenArt MCP, `openart_generate_image` (6 jobs, run ONE AT A TIME).
- Model / mode: byte-plus-seedream-5-lite, image2image, 2K, aspect 9:16, imageCount 2 per job, style bible vVnbzqvg1L5uSFCKydUE.
- Cost: 15 credits per image, 2 per job = 30 per page, 180 credits for six pages. Balance 7,583, after 7,403.
- Staging so no credits are wasted: page 1 (30 credits) first. I check that the five areas land in the right order and at heights that fit the pins and show it to you. Pages 2 to 6 only start after your OK on page 1. Any change to a prompt is a new ask.
- Alternative: imageCount 1 per page = 90 credits (saves 90), no candidate choice.
- Output: Art/_candidates/map_pageN/ (candidates A and B per page), shown to you labelled; the picked page is cut and stitched with a short cross-fade at the joins; pins are placed from measured landmark heights (tool: Tools/art_pipeline/map_pages.py).

## Prompt template (the bracket is the page's list, bottom to top)
Match the art style of the reference image exactly (outline weight, glossy shading, colour saturation), but draw only the new scene described here. A tall vertical painted world-map background for a mobile game, seen from a high angle like a floating-island adventure map, bright sky-blue and cloud-white colour world. Five distinct themed areas arranged along one winding golden path that climbs from the bottom of the image to the top, evenly spaced from bottom to top: [LIST]. Between the areas the path winds through soft clouds. Every one of the five areas has a calm, open, flat spot on the path where a round marker will be placed later. The very top and the very bottom of the image fade into soft clouds so the pages can be joined. No text, no letters, no numbers, no characters, no user interface, no watermark.

## Lists per page (bottom to top)
1. Meadow Springs (green meadow with pink flowers and a small spring pond); Mushroom Grove (giant glowing purple mushrooms with teal spore lights); Sunny Beach (golden sand, aqua waves, palm trees); Candy Canyon (pink and mint sugar cliffs and gumdrop rocks); Bamboo Temple (bamboo groves, jade pagoda, red lanterns).
2. Frosty Peaks (snowy mountains and ice); Autumn Orchard (orange and red trees with falling leaves); Desert Oasis (sand dunes around a palm pond); Pirate Cove (rocky cove with a wooden ship); Toy Factory (colourful toy blocks, gears and chimneys).
3. Haunted Manor (spooky mansion with a graveyard and purple mist); Crystal Caverns (glowing blue and pink crystals); Volcano Forge (volcano with lava rivers and an anvil forge); Jungle Ruins (overgrown stone temple ruins); Neon City (glowing night city towers).
4. Cloud Kingdom (white castle on fluffy clouds); Steampunk Skyport (brass airships and gears); Deep Sea Reef (coral reef under the sea); Moon Carnival (moonlit funfair with a ferris wheel); Dragon Peak (rocky peak with a dragon's nest).
5. Space Station (floating space station among stars); Pyramid Sands (desert pyramids); Viking Fjord (icy fjord with longships); Wild West Mesa (red rock mesa and a saloon town); Dino Valley (green valley with friendly dinosaurs and ferns).
6. Storybook Castle (fairy-tale castle in a book-page landscape); Arcade Realm (giant pixel arcade machines); Samurai Dojo (cherry blossoms and a dojo); Alien Jungle (glowing alien plants); Rift of Time (swirling clock portal in the sky).
