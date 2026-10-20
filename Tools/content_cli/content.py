"""Boing Up: Sky Tower content CLI.

Usage (from the repo root):
    python Tools/content_cli/content.py validate

Validates every Content/ JSON file against Content/schemas/*.schema.json (stdlib-only
subset validator) and runs cross-reference and doc 08 rule checks. Exit code 1 on any error.
"""
import glob
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CONTENT = os.path.join(ROOT, "Content")

TYPE_MAP = {"string": str, "integer": int, "number": (int, float), "boolean": bool,
            "object": dict, "array": list, "null": type(None)}


def type_ok(value, t):
    types = t if isinstance(t, list) else [t]
    for name in types:
        py = TYPE_MAP[name]
        if name in ("integer", "number") and isinstance(value, bool):
            continue
        if name == "integer" and isinstance(value, float):
            continue
        if isinstance(value, py):
            return True
    return False


def check_schema(value, schema, path, errors):
    """Validates the JSON Schema subset used in Content/schemas."""
    if "type" in schema and not type_ok(value, schema["type"]):
        errors.append("%s: expected %s, got %s" % (path, schema["type"], type(value).__name__))
        return
    if "enum" in schema and value not in schema["enum"]:
        errors.append("%s: %r not in %s" % (path, value, schema["enum"]))
    if isinstance(value, str):
        if "pattern" in schema and not re.search(schema["pattern"], value):
            errors.append("%s: %r does not match %s" % (path, value, schema["pattern"]))
        if "minLength" in schema and len(value) < schema["minLength"]:
            errors.append("%s: string too short" % path)
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        if "minimum" in schema and value < schema["minimum"]:
            errors.append("%s: %s < minimum %s" % (path, value, schema["minimum"]))
        if "maximum" in schema and value > schema["maximum"]:
            errors.append("%s: %s > maximum %s" % (path, value, schema["maximum"]))
    if isinstance(value, list):
        if "minItems" in schema and len(value) < schema["minItems"]:
            errors.append("%s: fewer than %d items" % (path, schema["minItems"]))
        if "maxItems" in schema and len(value) > schema["maxItems"]:
            errors.append("%s: more than %d items" % (path, schema["maxItems"]))
        if "items" in schema:
            for i, item in enumerate(value):
                check_schema(item, schema["items"], "%s[%d]" % (path, i), errors)
    if isinstance(value, dict):
        for key in schema.get("required", []):
            if key not in value:
                errors.append("%s: missing required '%s'" % (path, key))
        props = schema.get("properties", {})
        if schema.get("additionalProperties") is False:
            for key in value:
                if key not in props:
                    errors.append("%s: unexpected property '%s'" % (path, key))
        for key, sub in props.items():
            if key in value:
                check_schema(value[key], sub, "%s.%s" % (path, key), errors)


def load_abs(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def load(rel):
    return load_abs(os.path.join(CONTENT, rel))


def rel(path):
    return os.path.relpath(path, CONTENT).replace("\\", "/")


def expected_gem_skip(n):
    return None if n == 1 else 100 + (n - 2) * 300 // 18


# doc 08 section 6 template
CLASS_BY_LEVEL = {**{i: "simple" for i in (1, 2, 3, 4)}, **{i: "medium" for i in (6, 7, 8, 9)},
                  **{i: "hard" for i in (11, 12, 13, 14)}, 5: "complex", 10: "complex", 15: "highly_complex"}
D_BY_LEVEL = {1: 0.05, 2: 0.10, 3: 0.15, 4: 0.22, 5: 0.40, 6: 0.35, 7: 0.42, 8: 0.50, 9: 0.58,
              10: 0.70, 11: 0.62, 12: 0.70, 13: 0.78, 14: 0.86, 15: 1.00}


# Owner decision 2026-10-19 (T14): Speedrun keeps normal gaps, the time goal is relaxed. Regions 1-5 were verified by the bot
# (slowest bot time 61.0 s, timeSec = ceil(1.2 * 61.0 to 5 s) = 75). Any change must be re-verified with the Simulation tests.
SPEEDRUN_VALUE = 90
SPEEDRUN_TIME_MIN, SPEEDRUN_TIME_MAX = 60, 120
PLAYABLE_REGIONS = 5
COIN_STAR_MIN, COIN_STAR_MAX = 15, 500


def validate():
    errors = []
    schemas = {name: load("schemas/%s.schema.json" % name)
               for name in ("region", "level", "item", "gem_pack", "coin_pack", "module")}

    # --- schema pass ---
    regions, levels, modules, packs, coin_packs, items = {}, {}, {}, [], [], []
    for path in sorted(glob.glob(os.path.join(CONTENT, "regions", "*.json"))):
        data = load_abs(path)
        check_schema(data, schemas["region"], rel(path), errors)
        if isinstance(data, dict) and "id" in data:
            if data["id"] in regions:
                errors.append("%s: duplicate region id %s" % (rel(path), data["id"]))
            regions[data["id"]] = data
            if os.path.basename(path) != data["id"] + ".json":
                errors.append("%s: file name must equal region id" % rel(path))
    for path in sorted(glob.glob(os.path.join(CONTENT, "levels", "*.json"))):
        data = load_abs(path)
        for i, lv in enumerate(data):
            check_schema(lv, schemas["level"], "%s[%d]" % (rel(path), i), errors)
            if isinstance(lv, dict) and "id" in lv:
                if lv["id"] in levels:
                    errors.append("%s: duplicate level id %s" % (rel(path), lv["id"]))
                levels[lv["id"]] = lv
    for m in load("config/modules.json"):
        check_schema(m, schemas["module"], "config/modules.json:" + str(m.get("id")), errors)
        modules[m.get("id")] = m
    for p in load("economy/gem_packs.json"):
        check_schema(p, schemas["gem_pack"], "economy/gem_packs.json:" + str(p.get("id")), errors)
        packs.append(p)
    for p in load("economy/coin_packs.json"):
        check_schema(p, schemas["coin_pack"], "economy/coin_packs.json:" + str(p.get("id")), errors)
        coin_packs.append(p)
    for it in load("items/items.json"):
        check_schema(it, schemas["item"], "items/items.json:" + str(it.get("id")), errors)
        items.append(it)

    # --- structure rules ---
    if len(regions) != 30:
        errors.append("expected 30 regions, found %d" % len(regions))
    by_index = {r["index"]: r for r in regions.values() if "index" in r}
    for n in range(1, 31):
        r = by_index.get(n)
        if r is None:
            errors.append("missing region index %d" % n)
            continue
        want_status = "live" if n <= 20 else "teaser"
        if r["status"] != want_status:
            errors.append("%s: status should be %s" % (r["id"], want_status))
        if not r["id"].startswith("region_%03d_" % n):
            errors.append("%s: id does not match index %d" % (r["id"], n))
        prev = by_index.get(n - 1)
        want_req = prev["id"] if prev else None
        if r["unlock"]["requires"] != want_req:
            errors.append("%s: unlock.requires should be %s" % (r["id"], want_req))
        if r["unlock"]["gemSkip"] != expected_gem_skip(n):
            errors.append("%s: gemSkip should be %s" % (r["id"], expected_gem_skip(n)))
        want_off = round(0.25 * (n - 1) / 29, 4)
        if abs(r["rules"]["difficultyOffset"] - want_off) > 1e-4:
            errors.append("%s: difficultyOffset should be %s" % (r["id"], want_off))
        want_cf = round(1 + 0.03 * (n - 1), 2)
        if abs(r["coinFactor"] - want_cf) > 1e-9:
            errors.append("%s: coinFactor should be %s" % (r["id"], want_cf))
        if r["rules"]["signatureModule"] not in modules:
            errors.append("%s: unknown signatureModule %s" % (r["id"], r["rules"]["signatureModule"]))
        for lid in r["levels"]:
            lv = levels.get(lid)
            if lv is None:
                errors.append("%s: level %s not defined" % (r["id"], lid))
            elif lv["region"] != r["id"]:
                errors.append("%s: belongs to %s, listed by %s" % (lid, lv["region"], r["id"]))
    if len(levels) != 450:
        errors.append("expected 450 levels, found %d" % len(levels))
    listed = {lid for r in regions.values() for lid in r.get("levels", [])}
    for lid in levels:
        if lid not in listed:
            errors.append("%s: level not referenced by any region" % lid)

    # --- coin-star table (regions 1-5, generated from the bot by Tests/Simulation CoinStarTargetTests) ---
    table_path = os.path.join(CONTENT, "config", "coin_star_targets.json")
    coin_table = {}
    if os.path.exists(table_path):
        coin_table = load_abs(table_path).get("levels", {})
    else:
        errors.append("config/coin_star_targets.json missing (run the Simulation tests with UPDATE_COIN_TARGETS=1)")
    for lid in sorted(levels):
        n = int(lid.split("_")[1])
        if n <= PLAYABLE_REGIONS and lid not in coin_table:
            errors.append("%s: no entry in config/coin_star_targets.json" % lid)
    speedrun_times = set()

    # --- level rules (doc 08 sections 6-7) ---
    seeds = {}
    for lid, lv in levels.items():
        i = lv["index"]
        if lid != "lvl_%s_%02d" % (lv["region"].split("_")[1], i):
            errors.append("%s: id does not match region/index" % lid)
        if lv["class"] != CLASS_BY_LEVEL[i]:
            errors.append("%s: class should be %s" % (lid, CLASS_BY_LEVEL[i]))
        if abs(lv["difficulty"] - D_BY_LEVEL[i]) > 1e-9:
            errors.append("%s: base difficulty should be %s" % (lid, D_BY_LEVEL[i]))
        if i in (5, 10) and lv["rewards"]["coinPayout"] != 300:
            errors.append("%s: complex level payout must be 300" % lid)
        if i == 15 and lv["rewards"]["coinPayout"] != 900:
            errors.append("%s: finale payout must be 900" % lid)
        if i not in (5, 10, 15) and not 60 <= lv["rewards"]["coinPayout"] <= 140:
            errors.append("%s: normal payout must be 60-140" % lid)
        if lv["rewards"]["gems"]["min"] > lv["rewards"]["gems"]["max"]:
            errors.append("%s: gems min > max" % lid)
        if i == 15 and lv["rewards"]["gems"].get("firstClearGuaranteed") != 5:
            errors.append("%s: L15 must guarantee 5 gems on first clear" % lid)
        n = int(lid.split("_")[1])
        coin_stars = [st for st in lv["stars"] if st["type"] == "coins"]
        if len(coin_stars) != 1 or lv["stars"][1]["type"] != "coins":
            errors.append("%s: star 2 must be a coins star" % lid)
        else:
            target = coin_stars[0].get("value", 0)
            if not COIN_STAR_MIN <= target <= COIN_STAR_MAX:
                errors.append("%s: coin star %s outside %d-%d" % (lid, target, COIN_STAR_MIN, COIN_STAR_MAX))
            # Economy v2: the coin star is retired (stars are collectible pickups); only the range is kept for the schema.
        if lv["goal"]["type"] == "speedrun":
            g = lv["goal"]
            speedrun_times.add(g.get("timeSec"))
            if g.get("value") != SPEEDRUN_VALUE:
                errors.append("%s: speedrun value must be %d" % (lid, SPEEDRUN_VALUE))
            if not SPEEDRUN_TIME_MIN <= g.get("timeSec", 0) <= SPEEDRUN_TIME_MAX or g.get("timeSec", 0) % 5:
                errors.append("%s: speedrun timeSec must be a multiple of 5 in %d-%d" % (lid, SPEEDRUN_TIME_MIN, SPEEDRUN_TIME_MAX))
        if lv["seed"] in seeds:
            errors.append("%s: seed collides with %s" % (lid, seeds[lv["seed"]]))
        seeds[lv["seed"]] = lid

    if len(speedrun_times) > 1:
        errors.append("speedrun timeSec must be one constant for all regions, found %s" % sorted(speedrun_times))

    # --- modules ---
    covered = set()
    for m in modules.values():
        covered.update(m.get("regions", []))
    if covered != set(range(1, 31)):
        errors.append("modules must cover regions 1-30, missing %s" % sorted(set(range(1, 31)) - covered))

    # --- economy (doc 08 section 8) ---
    # Owner tables (docs/14): promo never above list, more gems per dollar as the price rises (1.5 percent tolerance for the
    # owner's rounded price points), unique SKUs and ids.
    for key in ("usdList", "usdPromo"):
        ladder = sorted(packs, key=lambda p: p[key])
        rates = [p["gems"] / p[key] for p in ladder]
        for a, b in zip(rates, rates[1:]):
            if b < a * 0.985:
                errors.append("gem packs (%s) must give more gems per dollar as price rises" % key)
                break
    for p in packs:
        if p["usdPromo"] > p["usdList"]:
            errors.append("gem pack %s: promo price above list price" % p["id"])
    if len({p["sku"] for p in packs}) != len(packs):
        errors.append("duplicate gem pack sku")
    if len({p["id"] for p in packs}) != len(packs):
        errors.append("duplicate gem pack id")
    for p in coin_packs:
        if p["gemsPromo"] > p["gemsList"]:
            errors.append("coin pack %s: promo price above list price" % p["id"])
    if len({p["id"] for p in coin_packs}) != len(coin_packs):
        errors.append("duplicate coin pack id")
    ids = [x["id"] for x in items]
    if len(ids) != len(set(ids)):
        errors.append("duplicate item id")

    return errors, len(regions), len(levels)


def main(argv):
    if len(argv) < 2 or argv[1] != "validate":
        print(__doc__)
        return 2
    errors, nr, nl = validate()
    if errors:
        for e in errors:
            print("ERROR", e)
        print("%d error(s) in %d regions / %d levels" % (len(errors), nr, nl))
        return 1
    print("OK: %d regions, %d levels validated" % (nr, nl))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
