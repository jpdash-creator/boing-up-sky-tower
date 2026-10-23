"""Re-orders the 30 regions to a new slug order (owner changes after the painted map pages, 2026-10-24).

    python Tools/content_cli/reorder_regions.py slug1,slug2,...            (the complete new order of all 30 slugs, slot 1 first)
    python Tools/content_cli/reorder_regions.py --dry-run slug1,slug2,...

The region definition, its theme, rules and level files MOVE with the slug to the new slot; per-slot values stay with the slot
(difficultyOffset, coinFactor, gemSkip, status, availableFrom). Ids, level ids, the unlock chain, the bundle names and the text references in
Content/assets (manifest.json, CATALOG.md, catalog/*.md) are rewritten. Run ContentManifestBuilder in Unity afterwards.
"""
import glob
import json
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")


def load_all():
    regions = {}
    for f in glob.glob(os.path.join(ROOT, "Content", "regions", "region_*.json")):
        d = json.load(open(f, encoding="utf-8"))
        slug = d["id"].split("_", 2)[2]
        lf = glob.glob(os.path.join(ROOT, "Content", "levels", d["id"] + "_levels.json"))[0]
        regions[slug] = {"file": f, "data": d, "lfile": lf, "levels": json.load(open(lf, encoding="utf-8"))}
    return regions


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    dry = "--dry-run" in sys.argv
    order = args[0].split(",")
    regions = load_all()
    if sorted(order) != sorted(regions) or len(order) != 30:
        raise SystemExit("the order must list each of the 30 slugs exactly once; missing: %s, unknown: %s" % (sorted(set(regions) - set(order)), sorted(set(order) - set(regions))))
    slotdata = {r["data"]["index"]: r["data"] for r in regions.values()}   # per-slot values
    mapping, newr, newl = {}, {}, {}
    for slot, slug in enumerate(order, start=1):
        src = regions[slug]
        od, sd = src["data"], slotdata[slot]
        nid = "region_%03d_%s" % (slot, slug)
        mapping[od["id"]] = nid
        nr = json.loads(json.dumps(od))
        nr["id"], nr["index"] = nid, slot
        nr["status"], nr["availableFrom"] = sd["status"], sd.get("availableFrom")
        nr["unlock"] = {"requires": None, "gemSkip": sd["unlock"]["gemSkip"]}
        nr["theme"]["bundle"] = "region_%03d" % slot
        nr["rules"]["difficultyOffset"] = sd["rules"]["difficultyOffset"]
        nr["coinFactor"] = sd["coinFactor"]
        nr["levels"] = ["lvl_%03d_%02d" % (slot, k + 1) for k in range(len(od["levels"]))]
        lv = json.loads(json.dumps(src["levels"]))
        for k, l in enumerate(lv):
            l["id"], l["region"] = "lvl_%03d_%02d" % (slot, k + 1), nid
        newr[slot], newl[slot] = nr, lv
    for slot in range(2, 31):
        newr[slot]["unlock"]["requires"] = newr[slot - 1]["id"]
    changed = [s for s in range(1, 31) if newr[s]["id"] != load_slot_id(regions, s)]
    print("slots that change:", changed)
    if dry:
        return
    for r in regions.values():
        os.remove(r["file"])
        os.remove(r["lfile"])
    for slot in range(1, 31):
        nid = newr[slot]["id"]
        with open(os.path.join(ROOT, "Content", "regions", nid + ".json"), "w", encoding="utf-8", newline="\n") as fh:
            json.dump(newr[slot], fh, indent=2)
            fh.write("\n")
        with open(os.path.join(ROOT, "Content", "levels", nid + "_levels.json"), "w", encoding="utf-8", newline="\n") as fh:
            json.dump(newl[slot], fh, indent=2)
            fh.write("\n")
    # text references: full ids first, then bare bundle ids of the moved regions
    old_bundle = {}
    for slug, r in regions.items():
        new_slot = order.index(slug) + 1
        old_bundle["region_%03d" % r["data"]["index"]] = "region_%03d" % new_slot
    paths = glob.glob(os.path.join(ROOT, "Content", "assets", "manifest.json")) + glob.glob(os.path.join(ROOT, "Content", "assets", "**", "*.md"), recursive=True)
    for path in sorted(set(paths)):
        if os.sep + "approvals" + os.sep in path:
            continue   # approval packets are history: they keep the ids of the time
        txt = open(path, encoding="utf-8").read()
        orig = txt
        # one pass with placeholders so no id is rewritten twice
        for i, (oid, nid) in enumerate(mapping.items()):
            txt = txt.replace(oid, "\u0001%d\u0002" % i)
        txt = re.sub(r"region_0\d\d(?![0-9_\u0001])", lambda m: old_bundle.get(m.group(0), m.group(0)), txt)
        for i, (oid, nid) in enumerate(mapping.items()):
            txt = txt.replace("\u0001%d\u0002" % i, nid)
        if txt != orig:
            open(path, "w", encoding="utf-8", newline="").write(txt)
            print("updated", os.path.relpath(path, ROOT))


def load_slot_id(regions, slot):
    for r in regions.values():
        if r["data"]["index"] == slot:
            return r["data"]["id"]


if __name__ == "__main__":
    main()
