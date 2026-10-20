"""Builds the complete asset request catalog for Sky Hopper (docs 02, 04, 08, 09).

    python Tools/asset_cli/gen_asset_manifest.py

Outputs (all generated; edit THIS file, not the outputs):
  Content/assets/manifest.json         every asset request with full prompt, settings, QA, path, cost
  Content/assets/catalog/<class>.md    human-readable per-class detail
  Content/assets/CATALOG.md            index, totals, credit scenarios, approval batches

NOTHING here spends credits. Generation requires the owner's explicit approval per batch
(.claude/rules/openart-credit-guardrail.md). Prompts never name any other game.
"""
import json
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Content", "assets")

STYLE = ("cute chibi cartoon with neon rim light and glow, thick consistent dark-hue outline, "
         "saturated gradient colors, glossy sparkle accents, top-left light, clean vector-like edges")
KEY = "solid flat #808080 mid-grey background, no text, no watermark"
SCENE = "no text, no watermark, simple readable shapes so gameplay stays visible"
BANNED = ["any existing game's name, character, logo or level layout", "real brand names", "text or letters inside the art"]

# Planning credit assumptions (OpenArt quotes at default 1-image config; verified per batch with openart_model_cost).
LOCKED_MODEL = "byte-plus-seedream-5-lite"   # Gate 0 winner (owner pick, B00)
# Real quote (openart_model_cost): Seedream 5 Lite is a flat 15 credits per image at 2K and 3K. No separate hero tier.
TIER_CREDITS = {"hero": 15, "std": 15}
SCENARIOS = [10, 15, 20, 30, 40]
BALANCE_AFTER_B00 = 11960
STYLE_REF = {"label": "Style bible (owner pick: image 4 of B01a)", "creation": "lY5zfWGMT0zVY1Tq4p8h", "id": "vVnbzqvg1L5uSFCKydUE",
             "url": "https://cdn.openart.ai/openart-ai/production/2026-10/create-image/eKcljWsHfmOD6SYI79NB/021792344354602ba082e75ad6045f729531cc716e805ec9f2b21_0_1792344400206_ecb4d010.png"}
MOMO_REF = {"label": "Momo reference (owner pick in B00)", "creation": "JSJcbfsZQsoank92X5Lq", "id": "JXcAM1vuW99E8S4E8AO8",
            "url": "https://cdn.openart.ai/openart-ai/production/2026-10/create-image/eKcljWsHfmOD6SYI79NB/0217923433042673cb8a93f7287826a4278aac7f0d089f95e0407_0_1792343333681_f967dd6a.png"}
MOMO_PREFIX = ("Use the first reference image as the exact design of Momo and the second reference image as the art style guide "
               "(outline weight, neon rim light and glow, glossy shading, colour saturation). ")
REF_PREFIX = ("Match the art style of the reference image exactly (outline weight, neon rim light and glow, glossy shading, colour saturation), "
              "but draw only the new subject described here, not the reference's layout or objects. ")
PLAN_CREDITS = 12000   # Plus plan monthly credits (owner confirmed)

CHECKS_COMMON = ["Style matches the locked style bible (outline weight, neon rim light, palette)",
                 "No text, letters or watermark", "No resemblance to any existing game's character or branding",
                 "Prompt, seed, model and settings logged to assets_log.csv"]

REGION_ENV = {
    1: "rolling green meadow hills with pink flowers, fluffy clouds and a bright sky blue sky",
    2: "giant glowing mushrooms in a purple forest grove with teal spore lights",
    3: "sunny tropical beach with golden sand, aqua waves and palm trees",
    4: "candy canyon of pink and mint sugar cliffs, lollipop trees and gumdrop rocks",
    5: "bamboo temple with jade pagodas, red lanterns and hanging gongs",
    6: "frosty snow peaks with ice-blue glaciers, falling snow and white pines",
    7: "autumn orchard with orange and brown leaves, apple trees and drifting leaves",
    8: "desert oasis with sand dunes, turquoise pool, palms and sandstone ruins",
    9: "pirate cove with navy sea, rocky cliffs, gold treasure and wooden ships",
    10: "toy factory with colourful blocks, gears, conveyor belts and primary-colour machines",
    11: "haunted manor with violet night sky, crooked towers and green glowing ghost lights",
    12: "crystal caverns with glowing cyan and magenta crystal clusters",
    13: "volcano forge with red lava rivers, charcoal rock, anvils and glowing embers",
    14: "jungle ruins with emerald canopy, amber stone temples and hanging vines",
    15: "neon city skyline with electric blue and pink signs-free towers, glowing grids and laser beams",
    16: "cloud kingdom with white and lavender castles floating on clouds, golden light and small lightning flashes",
    17: "steampunk skyport with brass airships, teal pipes, gears and steam clouds",
    18: "deep sea reef with coral, drifting bubbles, blue water and light shafts",
    19: "space station with black starry space, cyan panels, planets and floating debris",
    20: "moon carnival with rainbow neon tents, ferris wheel, trampolines and a big moon",
    21: "dragon peak with crimson volcanic mountains, gold temples and a sleeping dragon silhouette",
    22: "pyramid sands with gold pyramids, lapis blue sky and carved stone doors",
    23: "viking fjord with steel-blue water, ice floes, longships and frosted cliffs",
    24: "wild west mesa with terracotta rock arches, sunset sky and tumbleweeds",
    25: "dino valley with lime ferns, orange volcanoes and friendly dinosaur silhouettes",
    26: "storybook castle with pastel royal towers, drawbridge and floating book pages",
    27: "arcade realm with pixel-grid neon landscape, glowing cabinets and blocky clouds",
    28: "samurai dojo with ink-wash mountains, cherry blossoms and paper lanterns",
    29: "alien jungle with glow-green giant plants, violet sky and floating pods",
    30: "rift of time with chrono violet swirling clock-less vortex, floating shards and white light",
}

CHARACTERS = [
    ("biscuit", "Biscuit the Bunny", "rabbit", "cream and pink, long floppy ears, tiny carrot charm"),
    ("captain_waddle", "Captain Waddle", "penguin", "navy and white, tiny captain's hat badge-free, round belly"),
    ("zippy", "Zippy", "robot", "silver and electric blue, antenna with a glowing bulb, round visor eyes"),
    ("luna", "Luna", "cat witch", "deep purple and silver, pointed witch hat, crescent moon charm"),
    ("bolt", "Bolt", "dino", "lime green with orange spikes, lightning-bolt shaped tail tip"),
    ("sir_nibbles", "Sir Nibbles", "knight hamster", "golden fur, tiny shiny helmet, round shield"),
    ("pixel", "Pixel", "cyber frog", "neon green with magenta circuit lines, big glowing eyes"),
    ("coco", "Coco", "monkey", "chocolate brown with cream face, curled tail, banana-yellow scarf"),
    ("frost", "Frost", "yeti cub", "icy white and pale blue, fluffy mane, rosy cheeks"),
    ("blaze", "Blaze", "dragon pup", "orange and red, tiny wings, small flame on the tail"),
    ("nova", "Nova", "astronaut panda", "black and white, clear round space helmet, star patches"),
    ("boo", "Boo", "friendly ghost", "translucent pale lavender, wavy bottom, big smile, soft glow"),
    ("rex", "Rex", "T-rex in a hoodie", "teal green dinosaur wearing an oversized orange hoodie, tiny arms"),
    ("sakura", "Sakura", "fox maiden", "pink and white, three fluffy tails, cherry-blossom hair clip"),
    ("ziggy", "Ziggy", "alien", "mint green, three eyes, antenna with a star, silver jumpsuit"),
]

OUTFITS = [  # (id, name, description, rarity)
    ("sunny_tee", "Sunny Tee", "bright yellow t-shirt with a smiling sun patch", "common"),
    ("cloud_hoodie", "Cloud Hoodie", "fluffy white hoodie with a cloud-shaped hood", "common"),
    ("leaf_vest", "Leaf Vest", "green vest with leaf-shaped buttons", "common"),
    ("stripe_sweater", "Stripe Sweater", "chunky knit sweater with pastel stripes", "common"),
    ("denim_overalls", "Denim Overalls", "blue denim overalls with a front pocket", "common"),
    ("berry_dress", "Berry Dress", "pink dress with berry prints and a ribbon", "common"),
    ("mint_jacket", "Mint Jacket", "mint-green zip jacket with white trim", "common"),
    ("sailor_top", "Sailor Top", "navy and white sailor top with a square collar", "common"),
    ("patch_poncho", "Patchwork Poncho", "colourful patchwork poncho", "common"),
    ("cozy_cardigan", "Cozy Cardigan", "warm orange cardigan with big buttons", "common"),
    ("sporty_jersey", "Sporty Jersey", "red and white striped sports jersey, no numbers or letters", "common"),
    ("picnic_apron", "Picnic Apron", "red gingham apron with a heart pocket", "common"),
    ("rain_slicker", "Rain Slicker", "shiny yellow raincoat with a hood", "common"),
    ("scarf_tunic", "Scarf Tunic", "simple tunic with a long striped scarf", "common"),
    ("neon_bomber", "Neon Bomber Jacket", "glossy black bomber jacket with glowing cyan and pink piping", "rare"),
    ("candy_swirl", "Candy Swirl Dress", "pink and white swirl-candy dress with a lollipop belt", "rare"),
    ("frost_parka", "Frost Parka", "ice-blue parka with white fur hood", "rare"),
    ("buccaneer_coat", "Buccaneer Coat", "navy captain coat with gold buttons and a red sash", "rare"),
    ("bamboo_robe", "Bamboo Robe", "jade green robe with a bamboo pattern and red belt", "rare"),
    ("mushroom_cape", "Mushroom Cape", "purple cape with white mushroom spots", "rare"),
    ("toy_overalls", "Toy Factory Overalls", "primary-colour block overalls with gear badges", "rare"),
    ("ghostlight_gown", "Ghost-Light Gown", "violet gown with soft green glowing hem", "rare"),
    ("crystal_vest", "Crystal Vest", "cyan vest with faceted magenta crystal shoulder pieces", "rare"),
    ("steam_aviator", "Steam Aviator Jacket", "brass-buttoned leather jacket with teal pipes", "rare"),
    ("reef_wrap", "Reef Wrap", "coral and deep-blue wrap with tiny shell decorations", "rare"),
    ("carnival_coat", "Carnival Coat", "rainbow neon striped showman coat", "rare"),
    ("forge_plate", "Forge Plate Armor", "charcoal armor with glowing red lava cracks", "epic"),
    ("jungle_regalia", "Jungle Regalia", "emerald and amber ceremonial tunic with feathers", "epic"),
    ("cloud_royal", "Cloud Kingdom Robes", "white and lavender royal robes with golden trim", "epic"),
    ("astro_suit", "Astro Suit", "black and cyan space suit with glowing panels", "epic"),
    ("laser_grid_suit", "Laser-Grid Suit", "electric blue suit with pink glowing grid lines", "epic"),
    ("dragon_scale_coat", "Dragon-Scale Coat", "crimson scaled coat with gold clasps", "epic"),
    ("sandstorm_cloak", "Sandstorm Cloak", "sand and turquoise flowing cloak", "epic"),
    ("moon_tux", "Moon Carnival Tux", "glowing rainbow-trim tuxedo with a moon brooch", "epic"),
    ("neon_kimono", "Neon Kimono", "ink-black kimono with glowing cherry-blossom pattern", "epic"),
    ("aurora_gown", "Aurora Gown", "flowing gown with animated-looking aurora gradient, shimmering foil", "legendary"),
    ("golden_champion", "Golden Champion Armor", "polished gold armor with a radiant glow", "legendary"),
    ("starlight_cape", "Starlight Cape", "deep blue cape filled with glowing stars", "legendary"),
    ("chrono_mantle", "Chrono Mantle", "chrono-violet mantle with floating glowing shards", "legendary"),
    ("prism_hero", "Prism Hero Suit", "crystal prism suit refracting rainbow light", "legendary"),
]

BOOTS = [
    ("red_sneakers", "Red Sneakers", "classic red sneakers with white soles", "common"),
    ("rain_boots", "Yellow Rain Boots", "shiny yellow rubber boots", "common"),
    ("sprout_shoes", "Sprout Shoes", "green shoes with little leaf sprouts", "common"),
    ("moccasins", "Moccasins", "soft tan moccasins with beads", "common"),
    ("canvas_hightops", "Canvas High-Tops", "blue canvas high-top sneakers", "common"),
    ("fuzzy_slippers", "Fuzzy Slippers", "pink fluffy slippers", "common"),
    ("hiking_boots", "Hiking Boots", "brown chunky boots with orange laces", "common"),
    ("wooden_clogs", "Wooden Clogs", "wooden clogs with painted flowers", "common"),
    ("beach_sandals", "Beach Sandals", "aqua sandals with a shell charm", "common"),
    ("knit_boots", "Knit Boots", "cream knit boots with a pom-pom", "common"),
    ("cloud_runner", "Cloud Runner Boots", "white cloud-puff running boots with sky-blue soles", "rare"),
    ("spring_coils", "Spring Coil Boots", "boots with visible chunky metal springs", "rare"),
    ("frost_skates", "Frost Skates", "ice-blue ice skates with white laces", "rare"),
    ("buccaneer_boots", "Buccaneer Boots", "navy pirate boots with gold buckles", "rare"),
    ("bamboo_geta", "Bamboo Geta", "raised wooden sandals with jade straps", "rare"),
    ("neon_trainers", "Neon Trainers", "black trainers with glowing cyan and pink soles", "rare"),
    ("bubble_boots", "Bubble Boots", "transparent bubble-soled boots", "rare"),
    ("mini_rocket_boots", "Mini Rocket Boots", "boots with tiny rocket thrusters", "rare"),
    ("candy_cane_boots", "Candy Cane Boots", "red and white striped candy boots", "rare"),
    ("gear_boots", "Gear Boots", "brass boots with turning gear details", "rare"),
    ("lava_stompers", "Lava Stompers", "charcoal boots with glowing red cracks", "epic"),
    ("crystal_kicks", "Crystal Kicks", "cyan crystal-faceted sneakers", "epic"),
    ("ghost_slippers", "Ghost Slippers", "translucent violet slippers with a green glow", "epic"),
    ("jetstream_boots", "Jetstream Boots", "silver boots with blue jet flames", "epic"),
    ("dragon_claw_boots", "Dragon Claw Boots", "crimson boots with golden claw toes", "epic"),
    ("gravity_boots", "Gravity Boots", "dark boots with floating glowing rings", "epic"),
    ("pixel_kicks", "Pixel Kicks", "blocky pixel-style neon sneakers", "epic"),
    ("aurora_wing_boots", "Aurora Wing Boots", "boots with small shimmering aurora wings", "legendary"),
    ("golden_sprinters", "Golden Sprinters", "gleaming gold racing boots", "legendary"),
    ("comet_trailblazers", "Comet Trailblazers", "deep-blue boots leaving a comet tail", "legendary"),
]

HATS = [
    ("sun_hat", "Sun Hat", "wide straw hat with a yellow ribbon", "common"),
    ("knit_beanie", "Knit Beanie", "cozy striped beanie with a pom-pom", "common"),
    ("baseball_cap", "Baseball Cap", "red cap worn forward with a plain front", "common"),
    ("bow_headband", "Bow Headband", "pink headband with a big bow", "common"),
    ("leaf_crown", "Leaf Crown", "crown woven from green leaves", "common"),
    ("bucket_hat", "Bucket Hat", "denim bucket hat", "common"),
    ("chef_hat", "Chef Hat", "tall white chef hat", "common"),
    ("chunky_headphones", "Chunky Headphones", "big neon headphones", "common"),
    ("propeller_cap", "Propeller Cap", "colourful cap with a spinning propeller", "common"),
    ("flower_crown", "Flower Crown", "crown of pastel flowers", "common"),
    ("wizard_hat", "Wizard Hat", "purple pointed hat with golden stars", "rare"),
    ("pirate_tricorn", "Pirate Tricorn", "navy tricorn hat with a gold trim and no skull emblem", "rare"),
    ("tiny_knight_helm", "Tiny Knight Helm", "shiny silver helmet with a red plume", "rare"),
    ("crystal_tiara", "Crystal Tiara", "tiara with cyan and magenta crystals", "rare"),
    ("cloud_halo", "Cloud Halo", "small fluffy cloud floating as a halo", "rare"),
    ("bamboo_straw_hat", "Bamboo Straw Hat", "cone-shaped bamboo hat", "rare"),
    ("mushroom_cap", "Mushroom Cap", "red mushroom cap with white spots", "rare"),
    ("aviator_goggles", "Aviator Cap & Goggles", "brown leather cap with brass goggles", "rare"),
    ("party_cone", "Party Hat", "rainbow party cone hat with confetti", "rare"),
    ("ghost_hood", "Ghost Hood", "white sheet hood with big eyes", "rare"),
    ("dragon_horns", "Dragon Horns", "crimson horns with golden tips", "epic"),
    ("astro_helmet", "Astro Helmet", "clear space helmet with cyan glow", "epic"),
    ("laser_visor", "Laser Visor", "wrap-around visor with glowing pink lasers", "epic"),
    ("magma_crown", "Magma Crown", "charcoal crown dripping glowing lava", "epic"),
    ("moon_jester", "Moon Jester Hat", "rainbow neon jester hat with moon bells", "epic"),
    ("kabuto_helm", "Kabuto Helm", "ink-black helmet with cherry-blossom crest", "epic"),
    ("neon_halo_ring", "Neon Halo Ring", "glowing neon ring floating above the head", "epic"),
    ("sunburst_crown", "Sunburst Crown", "radiant golden crown with sun rays", "legendary"),
    ("chrono_crown", "Chrono Crown", "violet crown with floating glowing shards", "legendary"),
    ("prism_crown", "Prism Crown", "crystal crown refracting rainbow light", "legendary"),
]

ACCESSORIES = [  # (id, name, description, rarity, kind back|face)
    ("tiny_backpack", "Tiny Backpack", "small red backpack with a keychain", "common", "back"),
    ("angel_wings", "Angel Wings", "small white feathered wings with a soft glow", "rare", "back"),
    ("bat_wings", "Bat Wings", "small purple bat wings", "rare", "back"),
    ("mini_jetpack", "Mini Jetpack", "tiny silver jetpack with blue flames", "epic", "back"),
    ("hero_cape", "Hero Cape", "flowing red cape with a gold clasp", "common", "back"),
    ("balloon_bundle", "Balloon Bundle", "bundle of three pastel balloons", "rare", "back"),
    ("glider_umbrella", "Glider Umbrella", "open yellow umbrella on the back", "common", "back"),
    ("paper_lantern", "Paper Lantern Pole", "red paper lantern on a short pole", "rare", "back"),
    ("star_satchel", "Star Satchel", "navy satchel covered in glowing stars", "epic", "back"),
    ("aurora_wings", "Aurora Wings", "large shimmering aurora-gradient wings", "legendary", "back"),
    ("round_glasses", "Round Glasses", "big round pink glasses", "common", "face"),
    ("star_shades", "Star Shades", "star-shaped sunglasses", "rare", "face"),
    ("heart_mask", "Heart Mask", "small heart-shaped face mask", "common", "face"),
    ("cat_whiskers", "Cat Whiskers", "painted whiskers on cheeks", "common", "face"),
    ("monocle", "Monocle", "gold monocle with a chain", "rare", "face"),
    ("bandana", "Bandana", "red bandana covering the lower face", "common", "face"),
    ("cheek_sparkles", "Cheek Sparkles", "glowing star stickers on cheeks", "rare", "face"),
    ("pixel_shades", "Pixel Shades", "blocky pixel sunglasses with neon edge", "epic", "face"),
    ("curly_moustache", "Curly Moustache", "big curly brown moustache", "rare", "face"),
    ("prism_visor", "Prism Visor", "crystal visor splitting light into rainbows", "legendary", "face"),
]

TRAILS = [
    ("rainbow_ribbon", "Rainbow Ribbon", "smooth rainbow gradient ribbon"),
    ("neon_cyan", "Neon Cyan", "glowing cyan light ribbon with white core"),
    ("fire_streak", "Fire Streak", "orange and red flame ribbon with ember sparks"),
    ("ice_crystals", "Ice Crystals", "pale blue ribbon with falling ice shards"),
    ("sakura_petals", "Sakura Petals", "pink ribbon of drifting cherry petals"),
    ("stardust", "Stardust", "golden ribbon of tiny twinkling stars"),
    ("lightning_arc", "Lightning Arc", "electric yellow-white zigzag ribbon"),
    ("bubble_stream", "Bubble Stream", "aqua ribbon of floating bubbles"),
    ("music_notes", "Music Notes", "purple ribbon with floating musical notes"),
    ("pixel_blocks", "Pixel Blocks", "green and magenta blocky pixel ribbon"),
    ("galaxy_swirl", "Galaxy Swirl", "deep violet ribbon with galaxy swirls and stars"),
    ("golden_comet", "Golden Comet", "bright gold comet tail with sparkles"),
]

POWERUPS = [
    ("spring_boots", "Spring Boots", "a pair of boots on coil springs"),
    ("rocket_pack", "Rocket Pack", "a small rocket backpack with flame"),
    ("coin_magnet", "Coin Magnet", "a horseshoe magnet pulling gold coins"),
    ("bubble_shield", "Bubble Shield", "a shiny translucent bubble"),
    ("slow_time", "Slow Time", "a stopwatch with a snail curl, no digits"),
    ("double_coins", "Double Coins", "two stacked gold coins with a sparkle"),
    ("safety_net", "Safety Net", "a trampoline net catching a star"),
    ("lucky_clover", "Lucky Clover", "a four-leaf clover with a tiny gem"),
    ("feather_fall", "Feather Fall", "a single fluffy feather floating down"),
    ("revive_heart", "Revive Heart", "a glowing heart with small wings"),
]

PACKS = [
    ("gempack_handful", "Handful (80 gems, $0.99)", "a small pouch with 3 gems peeking out", 1),
    ("gempack_pouch", "Pouch (440 gems, $4.99)", "a bigger drawstring bag with gems spilling", 2),
    ("gempack_sack", "Sack (920 gems, $9.99)", "a large full burlap sack overflowing with gems", 3),
    ("gempack_chest", "Chest (1,900 gems, $19.99)", "an open wooden treasure chest packed with gems and coins", 4),
    ("gempack_vault", "Vault (5,000 gems, $49.99)", "an open round vault door with glowing gems inside", 5),
    ("gempack_treasury", "Treasury (10,500 gems, $99.99)", "a golden castle made of gems with a crown on top", 6),
]


def slug(s):
    return re.sub(r"[^a-z0-9]+", "_", s.lower()).strip("_")


def load_regions():
    regions = []
    d = os.path.join(ROOT, "Content", "regions")
    for f in sorted(os.listdir(d)):
        with open(os.path.join(d, f), encoding="utf-8") as fh:
            regions.append(json.load(fh))
    return regions


def modules_by_id():
    with open(os.path.join(ROOT, "Content", "config", "modules.json"), encoding="utf-8") as fh:
        return {m["id"]: m for m in json.load(fh)}


ASSETS = []


def add(**kw):
    kw.setdefault("tier", "std")
    kw.setdefault("candidates", 3)
    kw.setdefault("mode", "text2image")
    kw.setdefault("reference", [])
    kw.setdefault("depends_on", [])
    kw.setdefault("notes", [])
    kw.setdefault("manual", [])
    kw["checks"] = kw.get("checks", []) + CHECKS_COMMON
    kw["forbidden"] = BANNED
    ASSETS.append(kw)


def build():
    regions = load_regions()
    mods = modules_by_id()

    # ---------- Phase A: foundation ----------
    add(id="fnd_model_gate", cls="foundation", phase="A0", group="gate0", name="Model comparison gate (Momo test)",
        purpose="Pick the one image model locked for characters/items before spending real credits (doc 04: one model per asset class).",
        prompt=("Full-body front view of Momo, a round chibi cloud-fox mascot, 3-head-tall chibi proportions, neutral standing pose, "
                "arms slightly away from body, soft white and warm cream fur with a pale sky-blue cloud-puff tail, big glossy eyes, tiny magenta scarf. "
                + STYLE + ", " + KEY),
        aspect="1:1", size="1K", candidates=3, tier="std", fixed_images=3,
        models=["kling-3-omni (10 cr)", "byte-plus-seedream-5-lite (15 cr)", "nano-banana-2-lite (15 cr)"],
        path="Art/_work/gate0/", post=["Owner picks the winner on outline consistency, glow staying off the background, small-size readability, chibi proportions"],
        checks=["Same prompt on 3 models, one image each", "Result decides the locked model for FND/CHR/OUT/BTS/HAT/ACC"],
        notes=["Exactly 3 images, one per model. Free model comparison, no retries without a new approval."])

    add(id="fnd_style_bible", cls="foundation", phase="A1", group="style", tier="hero", candidates=4, mode="image2image",
        name="Style bible sheet", purpose="One-page reference image used as the locked reference for every later generation.",
        prompt=("Using the reference image as the exact design of Momo, create a style reference artwork for a mobile game: Momo the round chibi cloud-fox mascot "
                "(centre, large, identical to the reference), a glowing neon-rimmed grass platform, a spinning gold coin, a faceted blue-pink gem, a small round power-up icon, "
                "and a row of six plain colour swatches with no labels, hex codes or captions, all drawn in the same style. " + STYLE + ", " + KEY),
        aspect="4:3", size="2K", path="Art/_work/style_bible/style_bible_v1.png",
        reference=["gate-0 winner: Seedream 5 Lite Momo (creation JSJcbfsZQsoank92X5Lq, resource JXcAM1vuW99E8S4E8AO8)"],
        post=["Owner approves ONE image as the locked style reference for later runs"],
        depends_on=["fnd_model_gate"], checks=["Momo matches the reference", "Outline 3 px at 512 px equivalent", "Neon rim light visible but not overpowering", "Top-left light", "No labels, hex codes or captions on the swatches"])

    add(id="fnd_momo_master", cls="foundation", phase="A1", group="style", tier="hero", candidates=6,
        name="Momo master art (default mascot)", purpose="Brand icon and base for the rig; round chibi cloud-fox, free default character.",
        prompt=("Full-body front view of Momo, a round chibi cloud-fox mascot, 3-head-tall chibi proportions, neutral standing pose, "
                "arms slightly away from body, soft white and warm cream fur, a fluffy pale sky-blue cloud-puff tail, pointed ears with pink insides, "
                "big glossy eyes, tiny magenta scarf. " + STYLE + ", " + KEY),
        aspect="1:1", size="2K", path="Art/Characters/char_momo/momo_master_v1.png", reference=["fnd_style_bible (owner upload)"],
        depends_on=["fnd_style_bible"], mode="image2image",
        post=["Background removal (#808080 key), trim, pivot at feet, split into layers (head, body, arms, legs, tail) - MANUAL or semi-auto, AI cannot output layers"],
        manual=["Layer splitting for the paper-doll rig", "Hero polish: hand cleanup of outline and hands"],
        checks=["Matches the 3-head chibi template", "Arms away from body so layers separate", "Tail separate silhouette"])

    add(id="fnd_momo_expressions", cls="foundation", phase="A1", group="style", tier="std", candidates=2,
        name="Momo expression sheet", purpose="Reference for face states (happy, jump, surprised, win, fail, sleepy).",
        prompt=("Character expression sheet of Momo the round chibi cloud-fox: six head-and-shoulders poses in a 3 by 2 grid: happy, determined jump, "
                "surprised, celebrating win, sad fail, sleepy. Same character as the reference. " + STYLE + ", " + KEY),
        aspect="4:3", size="2K", path="Art/Characters/char_momo/momo_expressions_v1.png", mode="image2image",
        reference=["fnd_momo_master (selected)"], depends_on=["fnd_momo_master"])

    add(id="fnd_rig_template_guide", cls="foundation", phase="A1", group="style", tier="std", candidates=0, fixed_images=0,
        name="Paper-doll rig template guide (NOT generated)", purpose="Standard body canvas/skeleton every outfit and boot aligns to.",
        prompt="(manual - draw the template in Krita/Unity 2D Animation; no OpenArt credits)", aspect="n/a", size="n/a",
        path="Art/Characters/_rig/body_template.psd", manual=["Create guide image: T-pose silhouette with pivots, layer order from doc 04 section 3"],
        notes=["Outfits/boots/hats generated with this guide as a reference image (image2image)."])

    add(id="fnd_logo", cls="foundation", phase="A1", group="brand", tier="hero", candidates=6,
        name="Sky Hopper logo / wordmark concepts", purpose="Game logo exploration. Final wordmark text is added in Unity/vector, not trusted from AI.",
        prompt=("Game logo emblem concept (symbol only, no letters): a stack of glowing neon platforms forming a tower with a small cloud-fox bouncing on a spring at the top. "
                + STYLE + ", " + KEY),
        aspect="1:1", size="2K", path="Art/Marketing/logo/logo_concept_v1.png",
        manual=["Wordmark typography by hand/vector (AI text is unreliable)"], checks=["No letters", "Reads at 64 px"])

    add(id="fnd_app_icon", cls="foundation", phase="A1", group="brand", tier="hero", candidates=6,
        name="App icon concepts", purpose="1024x1024 app icon; Momo face bouncing, bold readable shapes.",
        prompt=("Mobile app icon art: Momo the round chibi cloud-fox face and shoulders, big happy eyes, magenta scarf, bouncing upward with motion sparkles, "
                "centered, bold simple shapes readable at 64 px, glowing neon rim light, full-bleed gradient sky-blue to violet background, no text. "
                + STYLE + ", no watermark"),
        aspect="1:1", size="2K", path="Art/Marketing/icon/app_icon_concept_v1.png", depends_on=["fnd_momo_master"],
        manual=["Final icon needs hand polish and required platform sizes/rounded-mask safe zone"], checks=["Readable at 64 px", "No alpha for iOS icon"])

    # ---------- Characters ----------
    for cid, name, species, details in CHARACTERS:
        add(id="chr_" + cid, cls="character", phase="C", group="characters", candidates=4,
            name=name, purpose="Collectible character body+head art on the shared rig (doc 02 section 7).",
            prompt=("Full-body front view of " + name + ", a " + species + " mascot, 3-head-tall chibi proportions, neutral standing pose, arms slightly away from body, "
                    + details + ". " + STYLE + ", " + KEY),
            aspect="1:1", size="2K", mode="image2image", reference=["fnd_style_bible", "fnd_momo_master"],
            depends_on=["fnd_momo_master"], path="Art/Characters/char_%s/%s_master_v1.png" % (cid, cid),
            manual=["Split into head/body/arms/legs/tail layers on the shared rig", "Define unique trail and shout line (design, not art)"],
            checks=["Same 3-head proportions as Momo", "Arms away from body", "Rarity tier to be set by owner (doc 02: Common 2,000 coins, Rare 300 gems, Epic 700, Legendary 1,500)"])

    # ---------- Cosmetics ----------
    rar_note = {"common": "flat clean colours, no glow beyond rim light", "rare": "subtle extra glow accent",
                "epic": "strong glow and detailed accents", "legendary": "radiant glow, animated-foil feel (static art; shimmer added in Unity)"}
    for iid, name, desc, rar in OUTFITS:
        add(id="out_" + iid, cls="outfit", phase="D", group="cosmetics_outfit_" + rar, candidates=3, tier="std",
            name=name + " (" + rar + ")", purpose="Outfit layer on the shared chibi rig.",
            prompt=("Game costume piece '" + name + "': " + desc + ", shown as a flat garment aligned to a chibi body torso, no body inside, symmetrical front view, "
                    "rarity feel: " + rar + " (" + rar_note[rar] + "). " + STYLE + ", " + KEY),
            aspect="1:1", size="1K", mode="image2image", reference=["Art/Characters/_rig/body_template", "fnd_style_bible"],
            depends_on=["fnd_rig_template_guide"], path="Art/Items/outfit/outfit_%s_front.png" % iid,
            checks=["Garment only, no body inside", "Symmetric and aligned to the torso template", "Sleeves/arms handled as separate layers if needed"])
    for iid, name, desc, rar in BOOTS:
        add(id="bts_" + iid, cls="boots", phase="D", group="cosmetics_boots_" + rar, candidates=3, tier="std",
            name=name + " (" + rar + ")", purpose="Boots; generate ONE boot, mirror for left/right (AI cannot do matching pairs).",
            prompt=("A single right-side-facing chibi game boot '" + name + "': " + desc + ", chunky, rounded toe, small detail accent, rarity feel: " + rar
                    + " (" + rar_note[rar] + "). " + STYLE + ", " + KEY),
            aspect="1:1", size="1K", reference=["fnd_style_bible"], depends_on=["out_sunny_tee"],
            path="Art/Items/boots/boots_%s_R.png" % iid, post=["Mirror to create _L", "Trim, pivot at ankle, template overlap check"])
    for iid, name, desc, rar in HATS:
        add(id="hat_" + iid, cls="hat", phase="D", group="cosmetics_hat_" + rar, candidates=3, tier="std",
            name=name + " (" + rar + ")", purpose="Head item aligned to the head layer pivot.",
            prompt=("Game hat '" + name + "': " + desc + ", front view, designed to sit on a round chibi head, no head or face inside, symmetrical, rarity feel: "
                    + rar + " (" + rar_note[rar] + "). " + STYLE + ", " + KEY),
            aspect="1:1", size="1K", reference=["fnd_style_bible"], depends_on=["out_sunny_tee"],
            path="Art/Items/hat/hat_%s_front.png" % iid)
    for iid, name, desc, rar, kind in ACCESSORIES:
        where = "worn on the back, shown flat from behind-front" if kind == "back" else "worn on the face, front view, no face inside"
        add(id="acc_" + iid, cls="accessory", phase="D", group="cosmetics_accessory_" + rar, candidates=3, tier="std",
            name=name + " (" + rar + ", " + kind + ")", purpose="Accessory slot item (" + kind + ").",
            prompt=("Game accessory '" + name + "': " + desc + ", " + where + ", symmetrical, rarity feel: " + rar + " (" + rar_note[rar] + "). " + STYLE + ", " + KEY),
            aspect="1:1", size="1K", reference=["fnd_style_bible"], depends_on=["out_sunny_tee"],
            path="Art/Items/acc/acc_%s_%s.png" % (iid, kind))
    for iid, name, desc in TRAILS:
        add(id="trl_" + iid, cls="trail", phase="D", group="cosmetics_trail", candidates=3, tier="std",
            name="Trail: " + name, purpose="Light-trail ribbon + particle sprites (doc 08 signature visual).",
            prompt=("Game light-trail effect sprite sheet '" + name + "': " + desc + ", one seamless horizontally tileable ribbon segment and four small matching particle sprites below it, "
                    "glowing neon edges. " + STYLE + ", " + KEY),
            aspect="16:9", size="2K", reference=["fnd_style_bible"], depends_on=["fnd_style_bible"], path="Art/Items/trail/trail_%s_sheet.png" % iid,
            post=["Slice ribbon + particles", "Check horizontal seamlessness", "Build Unity TrailRenderer/particle material"],
            checks=["Ribbon tiles with no seam", "Readable at game speed on a dark and bright background"])

    # ---------- Regions ----------
    for r in regions:
        n = r["index"]; slugname = r["id"].split("_", 2)[2]; rid = r["id"]
        pal = ", ".join(r["theme"]["palette"]); env = REGION_ENV[n]
        mod = mods[r["rules"]["signatureModule"]]
        base = "Art/Regions/%s/" % rid
        if r["status"] == "live":
            grp = "region_%03d" % n
            add(id="reg%03d_tileset" % n, cls="region_tileset", phase="E", group=grp, candidates=4,
                name="%s: platform tileset" % r["name"], purpose="Platform set for the region skin (normal, narrow, moving, crumbling, bouncy) and side walls.",
                prompt=("Seamless 2D platformer platform tiles for '" + r["name"] + "' (" + env + "), set of: wide platform, narrow platform, moving platform, crumbling platform, "
                        "bouncy platform, plus left and right wall tiles; side view, top lit; palette: " + pal + ". " + STYLE + ", " + KEY),
                aspect="4:3", size="2K", reference=["fnd_style_bible", "reg001_tileset (once approved)"] if n > 1 else ["fnd_style_bible"],
                depends_on=["fnd_style_bible"], path=base + "tiles/tileset_%s_v1.png" % slugname,
                post=["Slice per platform type", "Edge alignment test in Unity", "Pack into the region SpriteAtlas (ASTC)"],
                checks=["Platform tops clearly readable as walkable", "Same thickness and outline across all 5 types", "Wall tiles repeat vertically"])
            for layer, desc in (("far", "far distant layer: sky gradient with distant silhouettes"),
                                ("mid", "middle layer: mid-distance landmarks with transparent sky"),
                                ("near", "near layer: foreground framing elements at the edges, transparent centre")):
                add(id="reg%03d_bg_%s" % (n, layer), cls="region_background", phase="E", group=grp, candidates=3,
                    name="%s: background %s" % (r["name"], layer), purpose="Parallax layer (%s) for the region." % layer,
                    prompt=("Vertical mobile-game background for '" + r["name"] + "': " + env + ", " + desc + ", horizontally tileable, palette: " + pal
                            + ", " + STYLE + ", " + SCENE),
                    aspect="9:16", size="2K", reference=["fnd_style_bible"], depends_on=["fnd_style_bible"],
                    path=base + "bg_%s/bg_%s_%s_v1.png" % (layer, slugname, layer),
                    post=["Make horizontally tileable (offset + blend)", "Transparent sky where noted (matte pass)", "Resize/compress ASTC"],
                    checks=["Gameplay stays readable on top", "Near layer keeps the centre clear", "Seamless tiling left-right"])
            add(id="reg%03d_props" % n, cls="region_props", phase="E", group=grp, candidates=3,
                name="%s: props and decoration sheet" % r["name"], purpose="Scattered decoration sprites for the region.",
                prompt=("Sprite sheet of 8 small decoration props for '" + r["name"] + "' (" + env + "), evenly spaced on a grid, separate objects, palette: " + pal + ". " + STYLE + ", " + KEY),
                aspect="4:3", size="2K", reference=["fnd_style_bible"], depends_on=["fnd_style_bible"], path=base + "props/props_%s_v1.png" % slugname,
                post=["Slice objects with a matte pass", "Pivot at base"])
            add(id="reg%03d_signature" % n, cls="region_signature", phase="E", group=grp, candidates=3,
                name="%s: signature mechanic art (%s)" % (r["name"], mod["name"]),
                purpose="Art for the region's signature module: " + mod["description"] + ". Behaviour is code (doc 08 section 5).",
                prompt=("Game mechanic sprite sheet for '" + r["name"] + "': art for '" + mod["name"] + "' - " + mod["description"]
                        + ". Show the active object plus 3 state variations (idle, active, warning) in a row, separate sprites, palette: " + pal + ". " + STYLE + ", " + KEY),
                aspect="16:9", size="2K", reference=["fnd_style_bible"], depends_on=["fnd_style_bible"], path=base + "hazards/signature_%s_v1.png" % slugname,
                checks=["States clearly distinguishable (warning must read before danger)", "No reliance on colour alone for the warning (add shape/motion)"])
        else:
            add(id="reg%03d_teaser" % n, cls="region_teaser", phase="F", group="teasers", candidates=3,
                name="%s: locked teaser art" % r["name"], purpose="Dark silhouette mood art with a padlock for the map (doc 04 teaser recipe).",
                prompt=("Dark silhouette mood version of '" + r["name"] + "' (" + env + ") with a glowing padlock, mysterious purple tint, hints of the theme shapes. "
                        + STYLE + ", no text, no watermark"),
                aspect="1:1", size="2K", reference=["fnd_style_bible"], depends_on=["fnd_style_bible"], path=base + "teaser/teaser_%s_v1.png" % slugname,
                notes=["Full tileset/backgrounds for this region are generated only when the region goes live (live-ops)."])

    # ---------- Pickups / icons / currency ----------
    add(id="pck_coin_sheet", cls="pickup", phase="G", group="pickups", candidates=3, name="Coin sprite sheet",
        purpose="Spinning coin frames (6) used for pickups.",
        prompt=("Sprite sheet of a shiny gold coin spinning, six rotation frames in a row, equal spacing, centered, glowing rim light, star emblem without text. " + STYLE + ", " + KEY),
        aspect="16:9", size="2K", reference=["fnd_style_bible"], depends_on=["fnd_style_bible"], path="Art/Pickups/pickup_coin_sheet.png")
    add(id="pck_gem_sheet", cls="pickup", phase="G", group="pickups", candidates=3, name="Gem sprite sheet (3 sizes)",
        purpose="Faceted blue-pink gem in small/medium/large (pickup + UI).",
        prompt=("Game currency: a single faceted blue-pink gem shown at three sizes (small, medium, large) side by side, glossy highlights, sparkle accents. " + STYLE + ", " + KEY),
        aspect="16:9", size="2K", reference=["fnd_style_bible"], depends_on=["fnd_style_bible"], path="Art/Pickups/pickup_gem_sheet.png")
    add(id="pck_orbs", cls="pickup", phase="G", group="pickups", candidates=3, name="In-run power-up orbs (6)",
        purpose="Free in-run temporary power-ups: spring boots, rocket, magnet, shield, slow time, double coins.",
        prompt=("Sprite sheet of six glossy round power-up orbs in a 3 by 2 grid, each with a distinct simple symbol inside: boots on a spring, small rocket, horseshoe magnet, "
                "shield bubble, snail-curl clock without digits, two coins; bright rarity ring, readable at 64 px. " + STYLE + ", " + KEY),
        aspect="4:3", size="2K", reference=["fnd_style_bible"], depends_on=["fnd_style_bible"], path="Art/Pickups/pickup_orbs_sheet.png")
    for pid, name, symbol in POWERUPS:
        add(id="pwr_" + pid, cls="powerup_icon", phase="G", group="powerup_icons", candidates=2, name="Power-up icon: " + name,
            purpose="Shop/loadout icon for the consumable.",
            prompt=("Glossy round game icon of '" + name + "': " + symbol + ", bright rarity ring, centered, readable at 64 px. " + STYLE + ", " + KEY),
            aspect="1:1", size="1K", reference=["fnd_style_bible"], depends_on=["fnd_style_bible"], path="Art/PowerUps/pwr_%s.png" % pid)
    cur = [("coin_single", "a single large gold coin"), ("coin_stack_s", "a small stack of 5 gold coins"), ("coin_stack_m", "a medium stack of 15 gold coins"),
           ("coin_stack_l", "a tall pile of 40 gold coins"), ("gem_single", "a single large faceted blue-pink gem"), ("gem_cluster_s", "a small cluster of 3 gems"),
           ("gem_cluster_m", "a medium pile of 10 gems"), ("gem_cluster_l", "a big mound of 30 gems"), ("coin_gem_mix", "gold coins and gems mixed in a small pile"),
           ("reward_ribbon", "a reward ribbon badge with a star, no text"), ("star_filled", "a glossy gold star, filled"), ("star_empty", "a glossy outline star, empty slot")]
    for cid, desc in cur:
        add(id="cur_" + cid, cls="currency", phase="G", group="currency", candidates=2, name="Currency art: " + cid.replace("_", " "),
            purpose="UI/currency illustration.", prompt=("Game currency illustration: " + desc + ", glossy highlights, sparkle accents. " + STYLE + ", " + KEY),
            aspect="1:1", size="1K", reference=["fnd_style_bible"], depends_on=["fnd_style_bible"], path="Art/Currency/cur_%s.png" % cid)
    for pid, name, desc, step in PACKS:
        add(id="pak_" + pid, cls="pack_art", phase="H", group="packs", candidates=3, tier="std", name="Gem pack art: " + name,
            purpose="Real-money shop art. Ladder must look visibly bigger/richer each step (doc 04).",
            prompt=("Game shop illustration, tier " + str(step) + " of 6: " + desc + ", glossy highlights, sparkle accents, scale tier " + str(step) + ". " + STYLE + ", " + KEY),
            aspect="1:1", size="2K", reference=["fnd_style_bible", "previous pack tier (selected)"] if step > 1 else ["fnd_style_bible"], depends_on=["fnd_style_bible"],
            path="Art/Packs/%s.png" % pid, checks=["Clearly larger/richer than the previous tier", "No numbers/prices in the art (rendered in UI)"])
    extra = [("starter_pack", "a starter bundle: a glowing gem pile, an epic outfit box and boots box tied with a ribbon"),
             ("vip_pass", "a golden VIP crown on a velvet cushion with sparkles"),
             ("battle_pass_banner", "a wide banner with a glowing ticket and tier ladder stars, season theme neutral, no text"),
             ("remove_ads", "a shield with a crossed-out play-button shape, no text"),
             ("offer_banner_a", "a wide limited-offer banner with gift boxes and sparkles, no text"),
             ("offer_banner_b", "a wide limited-offer banner with a stack of chests and confetti, no text"),
             ("offer_banner_c", "a wide limited-offer banner with a rocket and gems, no text"),
             ("chest_bronze_closed", "a closed bronze treasure chest with a small lock"), ("chest_bronze_open", "an open bronze treasure chest with light rays"),
             ("chest_silver_closed", "a closed silver treasure chest with a jewel lock"), ("chest_silver_open", "an open silver treasure chest with light rays"),
             ("chest_gold_closed", "a closed golden treasure chest with a crown lock"), ("chest_gold_open", "an open golden treasure chest overflowing with light and gems")]
    for pid, desc in extra:
        add(id="pak_" + pid, cls="pack_art", phase="H", group="packs_extra", candidates=3, name="Shop/offer art: " + pid.replace("_", " "),
            purpose="Monetization/shop surface art.", prompt=("Game shop illustration: " + desc + ", glossy highlights, sparkle accents. " + STYLE + ", " + KEY),
            aspect="16:9" if "banner" in pid else "1:1", size="2K", reference=["fnd_style_bible"], depends_on=["fnd_style_bible"], path="Art/Packs/%s.png" % pid)

    # ---------- UI kit (sheets) ----------
    ui = [
        ("buttons_primary", "primary, secondary, disabled and pressed large rounded buttons in a 2 by 2 grid, no text, glass neon panels"),
        ("buttons_icon_round", "twelve round icon buttons in 4 by 3 grid with blank centres (pause, play, home, back, settings, sound, music, haptics, share, shop, wardrobe, quests), simple symbols, no letters"),
        ("panel_glass", "three 9-slice friendly glass neon panels (small, medium, large) with rounded corners and blank centres"),
        ("panel_popup", "a popup dialog frame with a title ribbon area and a close-button socket, blank, no text"),
        ("frames_rarity", "five square item frames for common, rare, epic, legendary and event rarity, blank centres, legendary has shimmering foil look"),
        ("tabs_shop", "ten shop tab icons in a row-grid: gems, characters, outfits, boots, hats, accessories, power-ups, offers, chests, VIP, simple symbols, no letters"),
        ("hud_icons", "HUD icons: floor arrow, combo flame, pause, heart, magnet, shield, clock-less timer ring, coin, gem, speed-up chevron, no letters"),
        ("progress_bars", "four progress bar styles (horizontal fill, level-progress, battle-pass track, combo meter) empty and full states"),
        ("toggles_sliders", "toggle switch on/off, slider track and knob, checkbox on/off, radio on/off"),
        ("level_nodes", "level select nodes: normal, complex (5/10) with gem, boss (15) with crown, locked, completed with stars, current highlighted, blank centres"),
        ("map_nodes", "region map nodes: unlocked, current, completed, locked silhouette with padlock, teaser coming-soon, ring glow"),
        ("badges_medals", "achievement badges and medals in bronze, silver, gold, rainbow, blank centres"),
        ("quest_icons", "daily quest and calendar icons: calendar day tile, streak flame, gift box, claimed check, locked, ticket"),
        ("rank_icons", "leaderboard icons: crown, medal 1-3 shapes without numbers, friends heads, global globe, weekly calendar"),
        ("banners_ribbons", "six decorative ribbons and banner shapes with blank centres in different colours"),
        ("dividers_corners", "UI dividers, corner ornaments and glow lines in neon style"),
        ("notification_badges", "red dot badge, NEW tag shape without letters, timer pill, plus button, arrow chevrons"),
        ("loading_spinner", "round loading spinner in 8 rotation steps and a small progress ring"),
        ("keyboard_free_input", "text input field frames, search bar, dropdown arrow, cursor caret"),
        ("share_card_frame", "a vertical share-card frame with blank photo window, confetti corners and sparkle border, no text"),
        ("settings_icons", "settings icons: language globe, privacy shield, delete bin, export arrow, left-hand mirror, reduced-motion wave, colour-blind eye"),
        ("shop_offer_tags", "discount burst, best-value ribbon, limited-time clock ribbon, pity-progress token, shapes only, no text"),
        ("wardrobe_ui", "wardrobe screen elements: slot sockets for outfit, boots, hat, accessory, trail, favourite heart, loadout tabs, blank"),
        ("tutorial_hints", "tutorial hint elements: tap ring, swipe arrow, hand pointer, highlight spotlight frame, arrows"),
        ("loading_screen_art", "a full-screen vertical loading splash with Momo mid-bounce above glowing platforms, empty centre-bottom for a progress bar, no text"),
    ]
    for uid, desc in ui:
        add(id="ui_" + uid, cls="ui_kit", phase="I", group="ui_kit", candidates=3, tier="std", name="UI sheet: " + uid.replace("_", " "),
            purpose="UI elements sliced in Unity (9-slice). Text is always rendered by Unity UI, never baked into the art.",
            prompt=("Game UI element sheet: " + desc + ". Elements separated on a grid, consistent glass-and-neon style. " + STYLE + ", " + KEY),
            aspect="4:3" if "loading_screen" not in uid and "share_card" not in uid else "9:16", size="2K", reference=["fnd_style_bible"],
            depends_on=["fnd_style_bible"], path="Art/UI/ui_%s.png" % uid, post=["Slice elements", "Define 9-slice borders", "Atlas"],
            checks=["No text or numerals", "Consistent corner radius and glow across sheets", "Each element separated enough to slice"])

    # ---------- Map, VFX, marketing ----------
    add(id="map_region_map", cls="map", phase="J", group="map", tier="hero", candidates=4, name="World map (30 nodes)",
        purpose="Map screen background with a winding path for 30 region nodes. Node art comes from the UI sheets.",
        prompt=("Vertical world map background for a mobile game: a winding glowing path rising upward through 30 distinct areas from a green meadow at the bottom through beach, snow, desert, "
                "neon city and clouds to space and a time vortex at the top, areas blend smoothly, empty circular spots along the path for level nodes. " + STYLE + ", no text, no watermark"),
        aspect="9:16", size="2K", reference=["fnd_style_bible"], depends_on=["fnd_style_bible"], path="Art/Map/map_world_v1.png",
        post=["Slice into scrollable segments", "Place nodes in Unity from region data"])
    vfx = [("shockwave_ring", "expanding shockwave rings in 6 frames"), ("speed_streaks", "motion speed-streak lines in 4 intensity levels"),
           ("confetti", "confetti pieces in many colours, 12 shapes"), ("sparkles", "sparkle and star particle shapes, 12 variants"),
           ("dust_puff", "landing dust puffs in 6 frames"), ("coin_burst", "coin collect burst in 6 frames"),
           ("wall_spark", "wall bounce spark burst in 6 frames"), ("glow_blobs", "soft round glow blobs in 6 colours for baked glow on low-end devices")]
    for vid, desc in vfx:
        add(id="vfx_" + vid, cls="vfx", phase="J", group="vfx", candidates=2, name="VFX sheet: " + vid.replace("_", " "),
            purpose="Particle sprites. Colour-shift and glow are finished in Unity.",
            prompt=("Game VFX sprite sheet: " + desc + ", evenly spaced on a grid, glowing neon look. " + STYLE + ", " + KEY),
            aspect="4:3", size="2K", reference=["fnd_style_bible"], depends_on=["fnd_style_bible"], path="Art/VFX/vfx_%s.png" % vid,
            checks=["Frames equally spaced", "Pure additive-friendly (works on dark bg)"])
    mk = [("feature_graphic", "wide store feature graphic: Momo bouncing up a glowing neon tower of platforms through clouds, dynamic composition, empty space on the left, no text", "16:9", "hero"),
          ("social_card_a", "vertical social share card background: Momo celebrating with confetti above a glowing tower, empty area for score, no text", "9:16", "std"),
          ("social_card_b", "square social share card background: glowing neon platforms ladder and sparkles, empty centre, no text", "1:1", "std"),
          ("social_card_c", "wide social share card background: Momo mid-air with a rainbow light trail, empty right side, no text", "16:9", "std"),
          ("splash_screen", "full-screen vertical splash: Momo on a spring at the base of a glowing tower, night gradient sky, empty top area for the logo, no text", "9:16", "hero")]
    for mid, desc, asp, tier in mk:
        add(id="mkt_" + mid, cls="marketing", phase="K", group="marketing", candidates=4, tier=tier, name="Marketing: " + mid.replace("_", " "),
            purpose="Store/social art; final text/logo overlaid by hand.", prompt=desc + ". " + STYLE + ", no watermark",
            aspect=asp, size="2K", reference=["fnd_style_bible", "fnd_momo_master"], depends_on=["fnd_momo_master"], path="Art/Marketing/%s_v1.png" % mid,
            manual=["Add logo/text in vector tools", "Export store-required sizes"])

    # Phase-1 test set (doc 07): 3 outfits, 3 boots, Meadow Springs full set. Proves the pipeline before bulk runs.
    test_ids = {"out_sunny_tee", "out_cloud_hoodie", "out_leaf_vest", "bts_red_sneakers", "bts_rain_boots", "bts_cloud_runner"}
    for a in ASSETS:
        if a["id"] in test_ids or a["id"].startswith("reg001_"):
            a["phase"] = "B"; a["group"] = "phase1_test"
        if a["id"] in test_ids and a["cls"] in ("outfit", "boots"):
            a["notes"].append("Phase-1 pipeline test: review alignment, mirroring and trim BEFORE approving the bulk cosmetic batches")
    for a in ASSETS:
        if a["phase"] in ("D",) and a["id"] not in test_ids:
            a["depends_on"] = sorted(set(a["depends_on"] + ["out_sunny_tee"]))
    for a in ASSETS:
        a["model"] = LOCKED_MODEL
        refs = " ".join(a["reference"])
        needs_momo = a["id"] == "fnd_app_icon" or (a["cls"] == "marketing" and "momo" in refs.lower())
        if needs_momo:
            a["mode"] = "image2image"
            a["prompt"] = MOMO_PREFIX + a["prompt"]
            a["reference"] = [MOMO_REF["label"] + " creation " + MOMO_REF["creation"] + " resource " + MOMO_REF["id"],
                              STYLE_REF["label"] + " creation " + STYLE_REF["creation"] + " resource " + STYLE_REF["id"]]
            a["visualReferences"] = [{"type": "image", "id": MOMO_REF["id"], "url": MOMO_REF["url"], "label": "Momo reference"},
                                     {"type": "image", "id": STYLE_REF["id"], "url": STYLE_REF["url"], "label": "Style reference"}]
        elif "fnd_style_bible" in refs and a["id"] not in ("fnd_style_bible", "fnd_model_gate"):
            a["mode"] = "image2image"
            a["prompt"] = REF_PREFIX + a["prompt"]
            a["reference"] = [STYLE_REF["label"] + " creation " + STYLE_REF["creation"] + " resource " + STYLE_REF["id"]] + [r for r in a["reference"] if "fnd_style_bible" not in r and "momo" not in r.lower()]
            a["visualReferences"] = [{"type": "image", "id": STYLE_REF["id"], "url": STYLE_REF["url"], "label": "Style reference"}]
        if a["id"] == "fnd_style_bible":
            a["status"] = "done"; a["spent_credits"] = 60
            a["result"] = {"winner": "image 4 of 4", "creation": STYLE_REF["creation"], "resource": STYLE_REF["id"], "url": STYLE_REF["url"]}
        if a["id"] == "fnd_momo_master":
            a["status"] = "skipped"; a["notes"].append("Owner picked the B00 Seedream Momo; used as the master art. Re-open only if the rig needs a cleaner pose.")
        if a["id"] == "fnd_momo_expressions":
            a["reference"] = [MOMO_REF["label"]]
            a["visualReferences"] = [{"type": "image", "id": MOMO_REF["id"], "url": MOMO_REF["url"], "label": "Momo reference"}]
        a["depends_on"] = ["fnd_style_bible" if d == "fnd_momo_master" else d for d in a["depends_on"]]
        if a["id"] != "fnd_model_gate":
            if a["size"] == "1K":
                a["size"] = "2K"
                a["notes"].append("Seedream 5 Lite has no 1K option; 2K costs the same 15 credits")
        else:
            a["status"] = "done"
            a["spent_credits"] = 40
            a["result"] = {"winner": "byte-plus-seedream-5-lite 2K 1:1", "creation": "JSJcbfsZQsoank92X5Lq",
                           "resource": "JXcAM1vuW99E8S4E8AO8", "balance_after": BALANCE_AFTER_B00}
    return ASSETS


SEQ = {"fnd_model_gate": 0, "fnd_style_bible": 1, "fnd_momo_master": 2, "fnd_momo_expressions": 3,
       "fnd_rig_template_guide": 4, "fnd_logo": 5, "fnd_app_icon": 6}


def credits_of(a, per_image=None):
    if a.get("status") in ("done", "skipped"):
        return 0
    n = a.get("fixed_images", a["candidates"])
    c = per_image if per_image is not None else TIER_CREDITS[a["tier"]]
    return n * c


def assign_batches(assets, cap=400):
    order = {"A0": 0, "A1": 1, "B": 2, "C": 3, "D": 4, "E": 5, "F": 6, "G": 7, "H": 8, "I": 9, "J": 10, "K": 11}
    ordered = sorted(assets, key=lambda a: (order[a["phase"]], SEQ.get(a["id"], 50), a["group"], a["id"]))
    batches, cur, tot, num = [], [], 0, 0
    last_group = None
    last_phase = None
    for a in ordered:
        c = credits_of(a)
        if a["id"] == "fnd_model_gate":
            batches.append(("B00", [a])); continue
        if cur and (tot + c > cap or a["phase"] != last_phase or (a["group"] != last_group and a["phase"] in ("A1", "B"))):
            num += 1; batches.append(("B%02d" % num, cur)); cur, tot = [], 0
        cur.append(a); tot += c; last_group = a["group"]; last_phase = a["phase"]
    if cur:
        num += 1; batches.append(("B%02d" % num, cur))
    for bid, items in batches:
        for a in items:
            a["batch"] = bid
    return batches


def md_asset(a):
    lines = ["### %s - %s" % (a["id"], a["name"]), "",
             "- **Batch:** %s | **Phase:** %s | **Class:** %s | **Group:** %s" % (a["batch"], a["phase"], a["cls"], a["group"]),
             "- **Purpose:** " + a["purpose"],
             "- **Mode:** %s | **Aspect:** %s | **Size:** %s | **Images to generate:** %s (tier %s, assumed %s credits each = **%s credits**)" % (
                 a["mode"], a["aspect"], a["size"], a.get("fixed_images", a["candidates"]), a["tier"], TIER_CREDITS[a["tier"]], credits_of(a)),
             "- **Reference images:** " + (", ".join(a["reference"]) if a["reference"] else "none"),
             "- **Depends on:** " + (", ".join(a["depends_on"]) if a["depends_on"] else "none"),
             "- **Save to:** `" + a["path"] + "`"]
    if "models" in a:
        lines.append("- **Models compared:** " + "; ".join(a["models"]))
    lines += ["- **Prompt (exact):**", "", "  > " + a["prompt"], ""]
    if a.get("post"):
        lines.append("- **Post-processing:** " + "; ".join(a["post"]))
    if a.get("manual"):
        lines.append("- **Manual work (no credits):** " + "; ".join(a["manual"]))
    lines.append("- **Acceptance checks:** " + "; ".join(a["checks"]))
    if a.get("notes"):
        lines.append("- **Notes:** " + "; ".join(a["notes"]))
    lines.append("")
    return "\n".join(lines)


def main():
    assets = build()
    batches = assign_batches(assets)
    os.makedirs(os.path.join(OUT, "catalog"), exist_ok=True)

    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump({"version": 1, "styleLock": STYLE + ", " + KEY, "tierAssumptions": TIER_CREDITS,
                   "planCredits": PLAN_CREDITS, "assets": assets}, f, indent=2)
        f.write("\n")

    by_cls = {}
    for a in assets:
        by_cls.setdefault(a["cls"], []).append(a)
    for cls, items in by_cls.items():
        with open(os.path.join(OUT, "catalog", cls + ".md"), "w", encoding="utf-8", newline="\n") as f:
            f.write("# Asset requests: %s (%d assets)\n\nGenerated by `Tools/asset_cli/gen_asset_manifest.py`. "
                    "No credits are spent without the owner's explicit approval per batch.\n\n" % (cls, len(items)))
            for a in sorted(items, key=lambda x: x["id"]):
                f.write(md_asset(a) + "\n")

    imgs = sum(0 if a.get("status") in ("done", "skipped") else a.get("fixed_images", a["candidates"]) for a in assets)
    tot_default = sum(credits_of(a) for a in assets)
    L = ["# Sky Hopper - Asset Catalog (generated)", "",
         "**Status:** planning only. Zero credits spent. Every batch below needs the owner's explicit approval (`.claude/rules/openart-credit-guardrail.md`).", "",
         "Source of truth for rules: docs 02, 04, 08, 09. Style lock: `%s`." % (STYLE + ", " + KEY), "",
         "## Totals", "", "- Asset requests: **%d** (%d still need generation; B00 model gate and B01a style bible done: 100 credits spent, Seedream 5 Lite 2K locked, style bible image 4 chosen; Momo master skipped)" % (len(assets), sum(1 for a in assets if a.get('status') not in ('done', 'skipped') and a.get('fixed_images', a['candidates']) > 0)),
         "- Images still to generate (all candidates): **%d**" % imgs,
         "- Credits at the real locked-model price (Seedream 5 Lite, %d per image; no hero tier, std %d): **%s**" % (TIER_CREDITS['hero'], TIER_CREDITS['std'], format(tot_default, ",")),
         "- Plus plan allowance: %s credits/month" % format(PLAN_CREDITS, ","), "",
         "### Sensitivity (same image count, different model price per image)", "", "| Credits per image | Total credits | Months of Plus allowance |", "|---|---|---|"]
    for c in SCENARIOS:
        t = imgs * c
        L.append("| %d | %s | %.1f |" % (c, format(t, ","), t / PLAN_CREDITS))
    L += ["", "Real cost depends on the model locked in Gate 0 (B00) and settings (resolution, count). Each batch is re-quoted with `openart_model_cost` before asking for approval.", "",
          "## Classes", "", "| Class | Assets | Images | Planning credits |", "|---|---|---|---|"]
    for cls, items in sorted(by_cls.items()):
        L.append("| [%s](catalog/%s.md) | %d | %d | %s |" % (cls, cls, len(items), sum(0 if a.get('status') in ('done', 'skipped') else a.get('fixed_images', a['candidates']) for a in items), format(sum(credits_of(a) for a in items), ",")))
    L += ["", "## Approval batches (in order)", "",
          "Each batch is a separate approval. Batches are capped near 400 planning credits so no single approval is large.", "",
          "| Batch | Assets | Images | Planning credits | First asset | Groups |", "|---|---|---|---|---|---|"]
    for bid, items in batches:
        L.append("| %s | %d | %d | %s | %s | %s |" % (bid, len(items), sum(a.get('fixed_images', a['candidates']) for a in items),
                                                       format(sum(credits_of(a) for a in items), ","), items[0]["id"], ", ".join(sorted({a['group'] for a in items}))))
    L += ["", "## Not generated by OpenArt (no credits here)", "",
          "- Audio: ~20 music loops, ~80 SFX, jingles (doc 04). OpenArt MCP lists image/video models only; use another licensed source.",
          "- Paper-doll rig template, layer splitting, final wordmark/typography, store screenshots (captured from the real game), localisation text.",
          "- Fonts, Lottie/animation, Unity shaders and post-processing.", ""]
    with open(os.path.join(OUT, "CATALOG.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(L))
    print("assets:", len(assets), "images:", imgs, "planning credits:", tot_default, "batches:", len(batches))


if __name__ == "__main__":
    main()
