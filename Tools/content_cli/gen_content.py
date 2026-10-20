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
    ("Cloud Kingdom", ["white", "lavender"], "Dissolving Clouds", "Dissolving clouds plus lightning flashes"),
    ("Steampunk Skyport", ["brass", "teal"], "Steam Vents", "Steam vents give upward lift"),
    ("Deep Sea Reef", ["deep blue", "coral"], "Water Currents", "Low-gravity water currents"),
    ("Moon Carnival", ["rainbow", "neon"], "Carnival Mix", "Trampolines plus rotating wheels"),
    ("Dragon Peak", ["crimson", "gold"], "Fire-Breath Sweeps", "Fire-breath sweeps across the tower"),
    ("Space Station", ["black", "cyan"], "Zero-G Zones", "Zero-g zones with floaty jumps"),
    ("Pyramid Sands", ["gold", "lapis"], "Sliding Stone Doors", "Sliding stone doors open and close"),
    ("Viking Fjord", ["steel blue", "frost"], "Ice Floes", "Drifting ice floes plus gusts (reuses Wind Gusts)"),
    ("Wild West Mesa", ["terracotta", "sunset"], "Tumbleweeds And Barrels", "Tumbleweeds and rolling barrels"),
    ("Dino Valley", ["lime", "orange"], "Stomp Quakes", "Stomp quakes shake the platforms"),
    ("Storybook Castle", ["pastel", "royal"], "Drawbridge Platforms", "Tilting drawbridge platforms"),
    ("Arcade Realm", ["pixel", "neon"], "Pixel Snap", "Pixel-grid snap platforms that blink"),
    ("Samurai Dojo", ["ink", "cherry"], "Falling Blades", "Falling blades with warning marks"),
    ("Alien Jungle", ["glow green", "violet"], "Gravity Pods", "Gravity pods with a flip pull"),
    ("Rift Of Time", ["chrono violet", "white"], "Rewind Zones", "Rewind zones leave a position echo"),
]

# Speedrun time goal (level index 9, every region), seconds. Owner decision 2026-10-19: relax the time, keep the normal
# vertical gaps. Value = slowest bot time over regions 1-5 times 1.2 (margin), rounded up to 5 s; see
# Tests/Simulation/SOLVABILITY_REPORT.md and Assets/_Game/Simulation/NOTES.md.
SPEEDRUN_TIME_SEC = 75

# index -> (class, d, goal)
LEVELS = {
    1: ("simple", 0.05, {"type": "reachFloor", "value": 60}),
    2: ("simple", 0.10, {"type": "reachFloor", "value": 90}),
    3: ("simple", 0.15, {"type": "reachFloor", "value": 120}),
    4: ("simple", 0.22, {"type": "reachFloor", "value": 100}),
    5: ("complex", 0.40, {"type": "comboChain", "value": 3, "count": 2}),
    6: ("medium", 0.35, {"type": "reachFloor", "value": 160}),
    7: ("medium", 0.42, {"type": "noWallBounce", "value": 120}),
    8: ("medium", 0.50, {"type": "findHiddenGem"}),
    9: ("medium", 0.58, {"type": "reachFloor", "value": 180}),
    10: ("complex", 0.70, {"type": "surviveWave"}),
    11: ("hard", 0.62, {"type": "comboChain", "value": 6, "count": 1}),
    12: ("hard", 0.70, {"type": "reachFloor", "value": 200}),
    13: ("hard", 0.78, {"type": "reachFloor", "value": 220}),
    14: ("hard", 0.86, {"type": "reachFloor", "value": 240}),
    15: ("highly_complex", 1.00, {"type": "bossChase", "value": 250}),
}
NORMAL_LEVELS = [1, 2, 3, 4, 6, 7, 8, 9, 11, 12, 13, 14]


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
GEM_PACKS = [  # (name, gems, usdList, usdPromo, badge)
    ("pinch", 10, 0.99, 0.49, ""), ("handful", 20, 1.99, 0.99, ""), ("pouch", 50, 3.99, 1.99, ""),
    ("sack", 100, 7.99, 3.99, "popular"), ("chest", 200, 11.99, 5.99, ""), ("vault", 500, 19.99, 9.99, ""),
    ("treasury", 1000, 29.99, 14.99, "best_value"),
]
COIN_PACKS = [  # (coins, gemsList, gemsPromo)
    (100, 10, 5), (200, 20, 10), (500, 50, 25), (1000, 100, 50), (2000, 200, 100), (5000, 500, 200), (10000, 1000, 500),
]


def gem_packs():
    return [{"id": "gempack_" + n, "gems": g, "usdList": ul, "usdPromo": up,
             "sku": "skyhopper.gems." + n, "badge": b} for n, g, ul, up, b in GEM_PACKS]


def coin_packs():
    return [{"id": "coinpack_%d" % c, "coins": c, "gemsList": gl, "gemsPromo": gp} for c, gl, gp in COIN_PACKS]


def coin_payout(index):
    if index in (5, 10):
        return 300
    if index == 15:
        return 900
    k = NORMAL_LEVELS.index(index)
    return int(round((60 + 80 * k / 11) / 5) * 5)


def gem_rewards(index):
    if index == 15:
        return {"chance": 1.0, "min": 5, "max": 5, "firstClearGuaranteed": 5,
                "starBonusMax": 5, "replayChance": 0.5, "replayAmount": 2}
    if index == 10:
        return {"chance": 0.25, "min": 1, "max": 3, "firstClearGuaranteed": 1}
    if index == 5:
        return {"chance": 0.25, "min": 1, "max": 3}
    return {"chance": 0.03, "min": 1, "max": 1}


def load_coin_star_targets():
    """Star-2 targets of regions 1-5 from Content/config/coin_star_targets.json (written by the Simulation test
    CoinStarTargetTests from the bot's proven coin bound). Returns {level id: target}; empty if the file is absent."""
    path = os.path.join(CONTENT, "config", "coin_star_targets.json")
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as f:
        table = json.load(f)
    return {lid: e["target"] for lid, e in table["levels"].items()}


COIN_STAR_TARGETS = load_coin_star_targets()


def coin_star_target(level_id, i):
    """Raised target for the five playable regions (from the bot table); the old formula for regions 6-30."""
    # Economy v2: the coin star is retired (stars are collectible pickups); the value is kept only to satisfy the schema.
    return min(500, COIN_STAR_TARGETS.get(level_id, 20 + 5 * i))


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
        "stars": [{"type": "clear"}, {"type": "coins", "value": coin_star_target(level_id, i)}, {"type": "noRevive"}],
        "rewards": {"coinPayout": coin_payout(i), "replayCoinPercent": 40, "gems": gem_rewards(i)},
    }
    if i in (5, 10):
        level["overrides"] = {"fixedChunks": ["chunk_complex_a"]}
    if i == 15:
        level["overrides"] = {"fixedChunks": ["chunk_gauntlet_a"], "ramp": True}
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
        weights = {"normal": 1.0} if n == 1 else {"normal": 1.0, "signature": 0.3}
        region = {
            "id": rid,
            "index": n,
            "nameKey": "region." + slug(name),
            "name": name,
            "status": "live" if n <= 20 else "teaser",
            "unlock": {
                "requires": None if n == 1 else region_ids[n - 2],
                "gemSkip": None if n == 1 else 100 + (n - 2) * 300 // 18,
            },
            "theme": {"bundle": "region_%03d" % n, "palette": palette, "music": None},
            "rules": {"signatureModule": mod_id,
                      "difficultyOffset": round(0.25 * (n - 1) / 29, 4),
                      "platformWeights": weights, "hazards": []},
            "coinFactor": round(1 + 0.03 * (n - 1), 2),
            "levels": ["lvl_%03d_%02d" % (n, i) for i in range(1, 16)],
            "availableFrom": None,
        }
        write("regions/%s.json" % rid, region)
        write("levels/%s_levels.json" % rid, [make_level(n, rid, i) for i in range(1, 16)])

    write("config/modules.json", modules)

    write("economy/gem_packs.json", gem_packs())
    write("economy/coin_packs.json", coin_packs())

    write("items/items.json", [{
        "id": "boots_cloud_runner", "slot": "boots", "rarity": "rare", "bundle": "items_boots_01",
        "sprites": {"left": "boots_cloud_runner_L", "right": "boots_cloud_runner_R"},
        "price": {"gems": 150}, "tags": ["cloud", "season1"], "availableFrom": None, "availableTo": None}])

    write("config/remote_config.json", {
        "ramp": {"rampFloors": 25, "rampPercent": 4, "rampCap": 150, "playerSpeedShare": 0.5},
        "generator": {"endlessDifficultyFloors": 400, "screenWidthUnits": 10},
        "score": {"floorPoints": 10, "comboPointsMultiplier": 5, "coinPoints": 1},
        "economy": {"coinsPerGem": 100, "replayCoinPercent": 40, "regionCoinScalePercent": 3,
                    "vipGemsPerDay": 25, "endlessCoinDailyFullRateRuns": 3,
                    "endlessCoinOverCapMultiplier": 0.5},
    })
    print("generated %d regions, %d levels" % (len(REGIONS), len(REGIONS) * 15))


if __name__ == "__main__":
    main()
