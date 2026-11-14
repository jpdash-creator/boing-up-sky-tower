"""Generates the baseline Content/ JSON from doc 08 (rules addendum).

Run from the repo root:  python Tools/content_cli/gen_content.py
The output is committed; re-run only when doc 08 changes. Hand edits to generated files
will be overwritten, so put long-lived overrides in the level 'overrides' block via this script.
"""
import json
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CONTENT = os.path.join(ROOT, "Content")

# (region name, palette, signature module name, module description)
# Region order changed 2026-10-24: slots 16-20 follow the painted map page 4 (Deep Sea Reef, Moon Carnival, Arcade Realm,
# Steampunk Skyport, Cloud Kingdom) and Dragon Peak moved to slot 27. Level files were MOVED with their regions (Content/levels),
# so do not regenerate levels from this table without checking the seeds.
REGIONS = [
    ("Meadow Springs", ["green", "pink", "sky blue"], "Bounce Flowers", "Tutorial region; bouncy flower platforms, no hazards"),
    ("Mushroom Grove", ["purple", "teal"], "Bounce Caps", "Bounce-cap platforms with a high launch"),
    ("Sunny Beach", ["gold", "aqua"], "Drifting Platforms", "Platforms sway horizontally"),
    ("Candy Canyon", ["pink", "mint"], "Sticky Gum", "Sticky platforms reduce jump height"),
    ("Bamboo Temple", ["jade", "red"], "Swinging Gongs", "Swinging gongs knock the player back"),
    ("Frosty Peaks", ["ice blue", "white"], "Slippery Platforms", "Low-friction platforms"),
    ("Autumn Orchard", ["orange", "brown"], "Wind Gusts", "Gusts push the player left or right"),
    ("Desert Oasis", ["sand", "turquoise"], "Crumbling Sand", "Sand platforms crumble after landing"),
    ("Pirate Cove", ["navy", "gold"], "Cannonballs", "Cannonballs fly in from the sides"),
    ("Toy Factory", ["red", "blue", "yellow"], "Conveyor Platforms", "Conveyor platforms move the player"),
    ("Haunted Manor", ["violet", "green glow"], "Ghost Platforms", "Vanishing / ghost platforms"),
    ("Crystal Caverns", ["cyan", "magenta"], "Crystal Bounce", "Reflective crystals give an angled rebound"),
    ("Volcano Forge", ["red", "charcoal"], "Rising Lava", "Rising lava plus timed fire jets"),
    ("Jungle Ruins", ["emerald", "amber"], "Vine Swing", "Grab and release vines"),
    ("Neon City", ["electric blue", "pink"], "Laser Gates", "Timed laser gates"),
    ("Deep Sea Reef", ["deep blue", "coral"], "Water Currents", "Low-gravity water currents"),
    ("Moon Carnival", ["rainbow", "neon"], "Carnival Mix", "Trampolines plus rotating wheels"),
    ("Arcade Realm", ["pixel", "neon"], "Pixel Snap", "Pixel-grid snap platforms that blink"),
    ("Steampunk Skyport", ["brass", "teal"], "Steam Vents", "Steam vents give upward lift"),
    ("Cloud Kingdom", ["white", "lavender"], "Dissolving Clouds", "Dissolving clouds plus lightning flashes"),
    ("Space Station", ["black", "cyan"], "Zero-G Zones", "Zero-g zones with floaty jumps"),
    ("Pyramid Sands", ["gold", "lapis"], "Sliding Stone Doors", "Sliding stone doors open and close"),
    ("Viking Fjord", ["steel blue", "frost"], "Ice Floes", "Drifting ice floes plus gusts (reuses Wind Gusts)"),
    ("Wild West Mesa", ["terracotta", "sunset"], "Tumbleweeds And Barrels", "Tumbleweeds and rolling barrels"),
    ("Dino Valley", ["lime", "orange"], "Stomp Quakes", "Stomp quakes shake the platforms"),
    ("Storybook Castle", ["pastel", "royal"], "Drawbridge Platforms", "Tilting drawbridge platforms"),
    ("Dragon Peak", ["crimson", "gold"], "Fire-Breath Sweeps", "Fire-breath sweeps across the tower"),
    ("Samurai Dojo", ["ink", "cherry"], "Falling Blades", "Falling blades with warning marks"),
    ("Alien Jungle", ["glow green", "violet"], "Gravity Pods", "Gravity pods with a flip pull"),
    ("Rift Of Time", ["chrono violet", "white"], "Rewind Zones", "Rewind zones leave a position echo"),
]

# Speedrun time goal (level index 9, every region), seconds. Owner decision 2026-10-19: relax the time, keep the normal
# vertical gaps. Value = slowest bot time over regions 1-5 times 1.2 (margin), rounded up to 5 s; see
# Tests/Simulation/SOLVABILITY_REPORT.md and Assets/_Game/Simulation/NOTES.md.
SPEEDRUN_TIME_SEC = 75

# index -> (class, d, goal). Doc 25 (owner list 27): rows per level 60 / 90 (L2-5) / 120 (L6-9) / 150 (L10-15); every level reaches the last
# platform except level 5 and 10 (combo chain, the ladder sits at the top), level 7 (no wall touch) and levels 8 and 15 (reach the last platform
# with gems). Survive wave, hidden gem search and boss chase goals are gone from the campaign.
LEVELS = {
    1: ("simple", 0.05, {"type": "reachFloor", "value": 60}),
    2: ("simple", 0.10, {"type": "reachFloor", "value": 90}),
    3: ("simple", 0.15, {"type": "reachFloor", "value": 90}),
    4: ("simple", 0.22, {"type": "reachFloor", "value": 90}),
    5: ("complex", 0.40, {"type": "comboChain", "value": 3, "count": 3}),
    6: ("medium", 0.35, {"type": "reachFloor", "value": 120}),
    7: ("medium", 0.42, {"type": "noWallBounce", "value": 120}),
    8: ("medium", 0.50, {"type": "reachFloor", "value": 120}),
    9: ("medium", 0.58, {"type": "reachFloor", "value": 120}),
    10: ("complex", 0.70, {"type": "comboChain", "value": 4, "count": 3}),
    11: ("hard", 0.62, {"type": "reachFloor", "value": 150}),
    12: ("hard", 0.70, {"type": "reachFloor", "value": 150}),
    13: ("hard", 0.78, {"type": "reachFloor", "value": 150}),
    14: ("hard", 0.86, {"type": "reachFloor", "value": 150}),
    15: ("highly_complex", 1.00, {"type": "reachFloor", "value": 150}),
}

# Bug List 6 item 4: regions 1..MIGRATED_REGIONS have 25 levels (new difficulty curve, goals and rewards); their region and level files are written by
# Tools/content_cli/migrate_levels_25.py and are NOT regenerated here. The tables above stay the 15-level tables of the regions 6-30 until they are
# migrated (TODO regions 6-30 migration: move them to migrate_levels_25.py by raising its MIGRATED_REGIONS, then delete this table).
MIGRATED_REGIONS = 5


def slug(text):
    return re.sub(r"[^a-z0-9]+", "_", text.lower()).strip("_")


def write(rel, data):
    path = os.path.join(CONTENT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2)
        f.write("\n")


# Shop tables (owner brief 2026-10-20, docs/14-shop-requirements.md). The promo price is a launch promotion: the player pays usdPromo
# (gemsPromo for coin packs) while the remote flag shop.launchPromo.enabled is on, the list price is shown crossed out.
GEM_PACKS = [  # (gems, usdList, usdPromo, inrList, badge); owner table docs/22 4.1, the promo prices are placeholders (promo is OFF)
    (50, 0.79, 0.49, 75, ""), (100, 1.49, 0.79, 142, "popular"), (200, 2.49, 1.29, 237, ""), (500, 5.99, 2.99, 569, ""),
    (1000, 10.99, 5.49, 1044, "best_value"), (2000, 20.99, 10.49, 1994, ""), (5000, 49.99, 24.99, 4749, ""),
    (10000, 89.99, 44.99, 8549, ""), (15000, 119.99, 59.99, 11399, ""),
]
COIN_PACKS = [  # (coins, gemsList, gemsPromo); owner table docs/22 4.2
    (1000, 50, 25), (2000, 100, 50), (5000, 250, 125), (10000, 500, 250), (20000, 900, 450), (50000, 2200, 1100), (100000, 4000, 2000),
]


def gem_packs():
    return [{"id": "gempack_%d" % g, "gems": g, "usdList": ul, "usdPromo": up, "inrList": inr, "firstPurchaseDouble": g <= 1000,
             "sku": "fluffyclimbers.gems.%d" % g, "badge": b} for g, ul, up, inr, b in GEM_PACKS]


def coin_packs():
    return [{"id": "coinpack_%d" % c, "coins": c, "gemsList": gl, "gemsPromo": gp} for c, gl, gp in COIN_PACKS]


# Economy review 2026-11-13: gems only on levels 5, 10, 15 (the 15-level teaser regions; regions 1-5 use 5, 10, 15, 20, 25 = 1, 1, 2, 2, 3 in migrate_levels_25.py).
MILESTONE_GEMS = {5: 1, 10: 1, 15: 2, 20: 2, 25: 3}
# Region skip price (owner 2026-11-13): 500 gems for every region 2 and up.
GEM_SKIP = 500


def gem_rewards(index):
    """Economy review 2026-11-13: gems ONLY on levels 5, 10, 15, 20, 25 (1, 1, 2, 2, 3), fixed, first clear only; no other level pays gems.
    Level 15 is the hidden-gem level: its 2 gems are this fixed reward (the hidden gem pickup is only the goal object)."""
    g = MILESTONE_GEMS.get(index, 0)
    if g:
        return {"chance": 1.0, "min": g, "max": g, "firstClearGuaranteed": g}
    return {"chance": 0.0, "min": 0, "max": 0}


def make_level(r, region_id, i):
    cls, d, goal = LEVELS[i]
    level_id = "lvl_%03d_%02d" % (r, i)
    level = {
        "id": level_id,
        "region": region_id,
        "index": i,
        "seed": (r * 7919 + i * 104729 + 12345) % 2147483647,
        "class": cls,
        "difficulty": d,
        "goal": goal,
        "rewards": {"gems": gem_rewards(i)},
    }
    return level


def main():
    modules = []
    region_ids = []
    for n, (name, palette, mod_name, mod_desc) in enumerate(REGIONS, start=1):
        rid = "region_%03d_%s" % (n, slug(name))
        region_ids.append(rid)
        mod_id = "mod_" + slug(mod_name)
        modules.append({"id": mod_id, "name": mod_name, "description": mod_desc,
                        "regions": [n], "buildStatus": "planned"})
        region = {
            "id": rid,
            "index": n,
            "nameKey": "region." + slug(name),
            "name": name,
            "status": "live" if n <= 20 else "teaser",
            "unlock": {
                "requires": None if n == 1 else region_ids[n - 2],
                "gemSkip": None if n == 1 else GEM_SKIP,
            },
            "theme": {"bundle": "region_%03d" % n, "palette": palette, "music": None},
            "rules": {"signatureModule": mod_id,
                      "difficultyOffset": round(0.25 * (n - 1) / 29, 4),
                      "hazards": []},
            "coinFactor": round(1 + 0.03 * (n - 1), 2),
            "levels": ["lvl_%03d_%02d" % (n, i) for i in range(1, 16)],
            "availableFrom": None,
        }
        if n <= MIGRATED_REGIONS:
            continue
        write("regions/%s.json" % rid, region)
        write("levels/%s_levels.json" % rid, [make_level(n, rid, i) for i in range(1, 16)])

    write("config/modules.json", modules)

    write("economy/gem_packs.json", gem_packs())
    write("economy/coin_packs.json", coin_packs())

    write("items/items.json", [])  # gear dropped 2026-11-01: no purchasable items for now

    write("config/remote_config.json", {
        "ramp": {"rampFloors": 25, "rampPercent": 4, "rampCap": 150, "playerSpeedShare": 0.5},
        "generator": {"endlessDifficultyFloors": 400, "screenWidthUnits": 10},
        "score": {"floorPoints": 10, "comboPointsMultiplier": 5, "coinPoints": 1},
        "economy": {"coinsPerGem": 20, "replayCoinPercent": 25,
                    "endlessCoinDailyFullRateRuns": 3,
                    "endlessCoinOverCapMultiplier": 0.5},
    })
    print("generated %d regions (regions 1-%d are kept: migrate_levels_25.py)" % (len(REGIONS), MIGRATED_REGIONS))


if __name__ == "__main__":
    main()
