"""Bug List 6 item 4: 25 levels per region (regions 1-5 first; regions 6-30 follow in a later task).

Rewrites Content/levels/region_00N_*_levels.json and the 'levels' list of Content/regions/region_00N_*.json for the MIGRATED regions:
levels 1..15 keep their ids and seeds, levels 16..25 are new (seed formula of gen_content.py). Goals, classes, rewards and the
'difficulty' field follow the new curve; the difficulty values come from Content/config/difficulty_curve.json (written by the
Simulation test DifficultyCurveTests.Calibrate with CALIBRATE_CURVE=1; a provisional straight line is used where it has no entry).

Run from the repo root:  python Tools/content_cli/migrate_levels_25.py
Idempotent. Never touches regions above MIGRATED_REGIONS.
"""
import glob
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CONTENT = os.path.join(ROOT, "Content")
MIGRATED_REGIONS = 5          # TODO(regions 6-30 migration): raise to 30 when the other regions are migrated
LEVELS = 25
SPECIAL = (5, 10, 15, 20)
# Target difficulty score per level (Bug List 6 item 4), see Tests/Simulation/DIFFICULTY_CURVE_REPORT.md
TARGET = [5, 20, 26.7, 33.3, 40, 44, 48, 52, 56, 60, 63, 66, 69, 72, 75, 77.5, 80, 82.5, 85, 87.5, 90, 92.5, 95, 97.5, 100]
SPEEDRUN_VALUE, SPEEDRUN_TIME = 90, 75


def goal(i):
    if i == 1: return {"type": "reachFloor", "value": 60}
    if i in (2, 3, 4): return {"type": "reachFloor", "value": 90}
    if i == 5: return {"type": "comboChain", "value": 3, "count": 3}
    if i in (6, 7, 8, 9): return {"type": "reachFloor", "value": 120}
    if i == 10: return {"type": "noWallBounce", "value": 150}
    if i == 15: return {"type": "findHiddenGem", "value": 150}
    if i == 20: return {"type": "speedrun", "value": SPEEDRUN_VALUE, "timeSec": SPEEDRUN_TIME}
    return {"type": "reachFloor", "value": 150}


def cls(i):
    if i == LEVELS: return "highly_complex"
    s = TARGET[i - 1]
    return "simple" if s < 30 else "medium" if s < 55 else "hard" if s < 75 else "complex"


NORMAL = [i for i in range(1, LEVELS) if i not in SPECIAL]


def coin_payout(i):
    if i in SPECIAL: return 300
    if i == LEVELS: return 900
    k = NORMAL.index(i)
    return int(round((60 + 80 * k / (len(NORMAL) - 1)) / 5) * 5)


def gem_rewards(i):
    if i == LEVELS:
        return {"chance": 1.0, "min": 5, "max": 5, "firstClearGuaranteed": 5, "starBonusMax": 5, "replayChance": 0.5, "replayAmount": 2}
    if i in (10, 20):
        return {"chance": 0.25, "min": 1, "max": 3, "firstClearGuaranteed": 1}
    if i in SPECIAL:
        return {"chance": 0.25, "min": 1, "max": 3}
    return {"chance": 0.03, "min": 1, "max": 1}


def provisional_d(i):
    return round(0.05 + 0.95 * ((TARGET[i - 1] - 5) / 95.0) ** 1.3, 3)


def load_curve():
    p = os.path.join(CONTENT, "config", "difficulty_curve.json")
    if os.path.exists(p):
        with open(p, encoding="utf-8") as f:
            return json.load(f).get("d", {})
    return {}


def write(path, data):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2)
        f.write("\n")


def main():
    curve = load_curve()
    for n in range(1, MIGRATED_REGIONS + 1):
        lf = glob.glob(os.path.join(CONTENT, "levels", "region_%03d_*_levels.json" % n))[0]
        rf = glob.glob(os.path.join(CONTENT, "regions", "region_%03d_*.json" % n))[0]
        with open(lf, encoding="utf-8") as f:
            old = {lv["index"]: lv for lv in json.load(f)}
        with open(rf, encoding="utf-8") as f:
            region = json.load(f)
        out = []
        for i in range(1, LEVELS + 1):
            lid = "lvl_%03d_%02d" % (n, i)
            prev = old.get(i)
            seed = prev["seed"] if prev else (n * 7919 + i * 104729 + 12345) % 2147483647
            star = prev["stars"][1]["value"] if prev else min(500, 20 + 5 * i)
            out.append({
                "id": lid, "region": region["id"], "index": i, "seed": seed, "class": cls(i),
                "difficulty": curve.get(lid, provisional_d(i)), "goal": goal(i),
                "stars": [{"type": "clear"}, {"type": "coins", "value": star}, {"type": "noRevive"}],
                "rewards": {"coinPayout": coin_payout(i), "gems": gem_rewards(i)},
            })
        region["levels"] = [lv["id"] for lv in out]
        write(lf, out)
        write(rf, region)
        print("region %d: %d levels" % (n, len(out)))


if __name__ == "__main__":
    main()
