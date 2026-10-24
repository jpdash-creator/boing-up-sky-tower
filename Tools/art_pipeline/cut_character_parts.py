"""Cut a character picture (or a gear layer) into rig parts by FIXED ZONES. Costs nothing (no OpenArt calls).

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/cut_character_parts.py char  <id> <picture.png> [--spec Tools/art_pipeline/zones/<id>.json]
    python Tools/art_pipeline/cut_character_parts.py gear  <gear_id> <slot>=<layer.png> ... [--zones momo]

Parts, draw order back to front (docs/18, section F):
    back, legL, legR, footL, footR, torso, armL, armR, head
Every part is a transparent PNG of the SAME size as the character canvas (1365 x 1649), parts stay at their
assembled position, so stacking them reproduces the picture exactly (every opaque pixel belongs to ONE part).
L / R are the viewer's left / right.

Zone spec (JSON, one per character, Tools/art_pipeline/zones/<id>.json):
    canvas        [w, h]
    hip_y, ankle_y   the lines the gear fits to (pants end above ankle_y, boots start at it)
    rules         ordered list, first matching rule wins for a pixel (only pixels with alpha > 0 are assigned)
        part      one of the nine parts
        rect      [x0, y0, x1, y1]            region (x1, y1 exclusive); or "poly": [[x, y], ...]; or "polys": [poly, ...]
        color     optional colour test (with "grow": n the match is dilated by n px to take its outline too), ALL given keys must hold:
                  hue [lo, hi] degrees (wraps), sat_min, sat_max, val_min, val_max (0..1),
                  rgb {"to": [r, g, b], "max": dist}
        no region = whole canvas.  The last rule should have no region and no colour (the default part).
        "fixed": true marks a rule as geometry only; rules with a colour test are skipped when the zones are
        applied to GEAR (a pink scarf rule must not catch a pink jacket).
    pivots        optional {part: [x, y]} joint for later animation; missing ones are derived from the bbox.
    gear_targets  optional {slot: {"box": [x0,y0,x1,y1], "align": "top|bottom|center", "parts": {part: {"box": [..]}}}}
                  where the shared gear should end up on THIS character (the fit command turns it into gear_offsets)
    gear_offsets  {slot: {"dx", "dy", "scale", "parts": {part: {dx, dy, scale}}}} per-slot fit of the shared gear
                  (anchor = centre of the gear slot bbox; a part override acts about that part's own centre).
"""
import colorsys
import json
import os
import sys

sys.path.insert(0, "E:/pylibs")
import cv2  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ZONES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "zones")
RES = os.path.join(ROOT, "Assets", "Resources", "Characters")
PARTS = ["back", "legL", "legR", "footL", "footR", "torso", "armL", "armR", "head"]  # draw order, back to front


def load_spec(name_or_path):
    p = name_or_path if name_or_path.endswith(".json") else os.path.join(ZONES, name_or_path + ".json")
    with open(p, encoding="utf-8") as f:
        return json.load(f)


def region_mask(rule, h, w):
    m = np.zeros((h, w), np.uint8)
    if "rect" in rule:
        x0, y0, x1, y1 = rule["rect"]
        m[max(0, y0):max(0, y1), max(0, x0):max(0, x1)] = 1
    elif "poly" in rule or "polys" in rule:
        polys = rule.get("polys") or [rule["poly"]]
        for pl in polys:
            cv2.fillPoly(m, [np.array(pl, np.int32)], 1)
    else:
        m[:] = 1
    return m.astype(bool)


def color_mask(rgb, spec):
    h, w, _ = rgb.shape
    ok = np.ones((h, w), bool)
    f = rgb.astype(np.float32) / 255.0
    mx, mn = f.max(2), f.min(2)
    v = mx
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    d = mx - mn
    r, g, b = f[..., 0], f[..., 1], f[..., 2]
    hue = np.zeros((h, w), np.float32)
    dd = np.maximum(d, 1e-6)
    hue = np.where(mx == r, ((g - b) / dd) % 6, np.where(mx == g, (b - r) / dd + 2, (r - g) / dd + 4)) * 60.0
    hue = np.where(d < 1e-6, 0, hue)
    if "hue" in spec:
        lo, hi = spec["hue"]
        ok &= ((hue >= lo) & (hue <= hi)) if lo <= hi else ((hue >= lo) | (hue <= hi))
    if "sat_min" in spec: ok &= s >= spec["sat_min"]
    if "sat_max" in spec: ok &= s <= spec["sat_max"]
    if "val_min" in spec: ok &= v >= spec["val_min"]
    if "val_max" in spec: ok &= v <= spec["val_max"]
    if "rgb" in spec:
        to = np.array(spec["rgb"]["to"], np.float32)
        ok &= np.sqrt(((rgb.astype(np.float32) - to) ** 2).sum(2)) <= spec["rgb"]["max"]
    return ok


def classify(rgba, spec, geometry_only=False):
    """label image: index into PARTS for every pixel (-1 where alpha is 0)."""
    h, w = rgba.shape[:2]
    label = np.full((h, w), -1, np.int16)
    todo = rgba[..., 3] > 0
    for rule in spec["rules"]:
        if geometry_only and "color" in rule:
            continue
        m = region_mask(rule, h, w) & todo
        if "color" in rule:
            cm = color_mask(rgba[..., :3], rule["color"]) & region_mask(rule, h, w)
            g = rule.get("grow", 0)
            if g:  # also take the outline / rim pixels around the colour match (still limited to the region)
                k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * g + 1, 2 * g + 1))
                cm = cv2.dilate(cm.astype(np.uint8), k).astype(bool)
            if rule.get("fill_holes"):  # highlights inside the matched shape
                from scipy.ndimage import binary_fill_holes
                cm = binary_fill_holes(cm)
            m &= cm
        label[m] = PARTS.index(rule["part"])
        todo &= ~m
    if todo.any():
        raise SystemExit("zone spec has no default rule: %d pixels unassigned" % todo.sum())
    return label


def derive_pivots(layers, spec):
    """joint per part: head bottom centre (neck), torso bottom centre (hip), arms top inner (shoulder),
    legs/feet top centre (hip / ankle), back left centre (where it is attached)."""
    piv = {}
    for name, im in layers.items():
        ys, xs = np.nonzero(im[..., 3] > 0)
        if len(xs) == 0:
            continue
        x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
        cx = int((x0 + x1) / 2)
        if name == "head": p = (cx, int(y1))
        elif name == "torso": p = (cx, int(y1))
        elif name == "armL": p = (int(x1), int(y0))
        elif name == "armR": p = (int(x0), int(y0))
        elif name in ("legL", "legR", "footL", "footR"): p = (cx, int(y0))
        else: p = (int(x0), int((y0 + y1) / 2))
        piv[name] = list(p)
    for k, v in spec.get("pivots", {}).items():
        piv[k] = v
    return piv


def split_layers(rgba, label):
    out = {}
    for i, name in enumerate(PARTS):
        im = np.zeros_like(rgba)
        m = label == i
        im[m] = rgba[m]
        out[name] = im
    return out


def composite(layers, order=PARTS):
    """straight-alpha 'over' compositing in float, returns uint8 RGBA."""
    h, w = next(iter(layers.values())).shape[:2]
    acc = np.zeros((h, w, 4), np.float32)
    for name in order:
        if name not in layers:
            continue
        src = layers[name].astype(np.float32) / 255.0
        a = src[..., 3:4]
        oa = acc[..., 3:4]
        na = a + oa * (1 - a)
        rgb = (src[..., :3] * a + acc[..., :3] * oa * (1 - a)) / np.maximum(na, 1e-6)
        acc = np.concatenate([rgb, na], 2)
    return np.clip(np.round(acc * 255), 0, 255).astype(np.uint8)


def save_part(path, im):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(im, "RGBA").save(path, optimize=True)


def preview(layers, path, bg=(112, 118, 128)):
    comp = composite(layers)
    base = np.zeros(comp.shape[:2] + (3,), np.uint8)
    base[:] = bg
    a = comp[..., 3:4] / 255.0
    rgb = (comp[..., :3] * a + base * (1 - a)).astype(np.uint8)
    Image.fromarray(rgb).save(path)


def tint_preview(label, path):
    cols = [(120, 120, 255), (255, 120, 120), (255, 180, 80), (120, 220, 120), (60, 160, 60),
            (255, 255, 120), (255, 120, 255), (120, 255, 255), (200, 200, 200)]
    h, w = label.shape
    im = np.full((h, w, 3), 30, np.uint8)
    for i, c in enumerate(cols):
        im[label == i] = c
    for gx in range(0, w, 100):
        im[:, gx] = (255, 255, 255) if gx % 500 == 0 else (90, 90, 90)
    for gy in range(0, h, 100):
        im[gy, :] = (255, 255, 255) if gy % 500 == 0 else (90, 90, 90)
    Image.fromarray(im).resize((w // 2, h // 2), Image.NEAREST).save(path)


def write_character(char_id, layers, spec, out_dir=None, extra=None):
    out_dir = out_dir or os.path.join(RES, char_id)
    for name in PARTS:
        save_part(os.path.join(out_dir, name + ".png"), layers[name])
    h, w = layers["head"].shape[:2]
    rig = {"id": char_id, "canvas": [w, h], "order": PARTS, "hip_y": spec["hip_y"], "ankle_y": spec["ankle_y"],
           "pivots": derive_pivots(layers, spec)}
    if "gear_offsets" in spec:
        rig["gear_offsets"] = spec["gear_offsets"]
    if extra:
        rig.update(extra)
    with open(os.path.join(out_dir, "rig.json"), "w", encoding="utf-8") as f:
        json.dump(rig, f, indent=1)
    preview(layers, os.path.join(out_dir, "preview.png"))
    return rig


def cmd_char(args):
    char_id, pic = args[0], args[1]
    spec = load_spec(args[args.index("--spec") + 1] if "--spec" in args else char_id)
    rgba = np.array(Image.open(pic).convert("RGBA"))
    if list(rgba.shape[1::-1]) != spec["canvas"]:
        raise SystemExit("canvas %s != spec %s" % (rgba.shape[1::-1], spec["canvas"]))
    label = classify(rgba, spec)
    layers = split_layers(rgba, label)
    write_character(char_id, layers, spec)
    comp = composite(layers)
    m = rgba[..., 3] > 0
    diff = np.abs(comp.astype(int) - rgba.astype(int))
    bad = int(((diff[..., :3].max(2) > 0) & m).sum() + (diff[..., 3] > 0).sum())
    print("%s: parts %s | composite vs original differing pixels: %d" % (
        char_id, {p: int((label == i).sum()) for i, p in enumerate(PARTS)}, bad))
    if "--debug" in args:
        tint_preview(label, args[args.index("--debug") + 1])
    return 0 if bad == 0 else 1


# ---- gear ---------------------------------------------------------------------------------------------------

# slot -> (parts the piece is cut into, part of the character each gear part is drawn right above)
SLOTS = {
    "head": {"parts": ["head"], "above": {"head": "head"}},
    "body": {"parts": ["torso", "armL", "armR"], "above": {"torso": "torso", "armL": "armL", "armR": "armR"}},
    # the waist band of the pants sits on the belly, which the torso part draws over the legs: pants go above the torso
    "pants": {"parts": ["legL", "legR"], "above": {"legL": "torso", "legR": "torso"}},
    "boots": {"parts": ["footL", "footR"], "above": {"footL": "footL", "footR": "footR"}},
}
PANTS_GAP = 12  # px of fur kept between the pants hem and the boots (pants end above the ankle line, boots start at it)


def clean_gear_layer(rgba, core_rule, reach=14):
    """The pilot head and pants layers still carry a ring of Momo's dark sticker outline. Keep what lies within
    `reach` px of the gear's own colour (warm, saturated) and drop the rest."""
    r, g, b = [rgba[..., i].astype(int) for i in range(3)]
    core = (rgba[..., 3] > 128) & (r > b + core_rule)
    k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * reach + 1, 2 * reach + 1))
    near = cv2.dilate(core.astype(np.uint8), k).astype(bool)
    out = rgba.copy()
    out[~near, 3] = 0
    n, lab, stats, _ = cv2.connectedComponentsWithStats((out[..., 3] > 0).astype(np.uint8), connectivity=8)
    for i in range(1, n):
        if stats[i, cv2.CC_STAT_AREA] < 400:
            out[lab == i, 3] = 0
    return out


def cut_gear(slot, rgba, spec, centre_x):
    """returns {part: rgba} for one gear layer drawn on the Momo canvas."""
    h, w = rgba.shape[:2]
    if slot in ("head", "pants"):
        rgba = clean_gear_layer(rgba, 60)
    ankle = spec["ankle_y"]
    if slot == "pants":
        rgba = rgba.copy(); rgba[ankle - PANTS_GAP:, :, 3] = 0   # pants end ABOVE the ankle line
    if slot == "boots":
        rgba = rgba.copy(); rgba[:ankle, :, 3] = 0               # boots START at the ankle line
    allowed = SLOTS[slot]["parts"]
    label = classify(rgba, spec, geometry_only=True)
    out = {p: np.zeros_like(rgba) for p in allowed}
    ys, xs = np.mgrid[0:h, 0:w]
    for i, name in enumerate(PARTS):
        m = (label == i) & (rgba[..., 3] > 0)
        if not m.any():
            continue
        if name in allowed:
            tgt = np.full((h, w), name)
        elif slot == "body":
            tgt = np.full((h, w), "torso")                       # collar over the chin, hem over the hip
        elif slot == "head":
            tgt = np.full((h, w), "head")
        else:                                                    # pants / boots: left or right of the centre line
            side = np.where(xs < centre_x + (ys - spec["hip_y"]) * 0.12, "L", "R")
            tgt = np.char.add(allowed[0][:-1], side)
        for p in allowed:
            mm = m & (tgt == p)
            out[p][mm] = rgba[mm]
    return out


def bbox_of(path):
    a = np.array(Image.open(path))[..., 3] > 0
    ys, xs = np.nonzero(a)
    return [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]


def cmd_fit(args):
    """fit <char_id> <gear_id>: turn the 'gear_targets' of the character's zone spec (boxes on its canvas where each
    slot / sleeve should end up) into 'gear_offsets' (dx, dy, scale per slot, plus per-part overrides) in the zone
    spec and in rig.json. The shared gear parts themselves are never edited."""
    cid, gid = args[0], args[1]
    spec_path = os.path.join(ZONES, cid + ".json")
    spec = json.load(open(spec_path, encoding="utf-8"))
    gear = json.load(open(os.path.join(RES, "gear_" + gid, "gear.json"), encoding="utf-8"))
    offs = {}
    for slot, tg in spec.get("gear_targets", {}).items():
        sd = gear["slots"].get(slot)
        if not sd:
            continue
        ax, ay = gear["anchors"][slot]
        x0, y0, x1, y1 = sd["bbox"]
        tx0, ty0, tx1, ty1 = tg["box"]
        sc = tg.get("scale", (tx1 - tx0) / float(x1 - x0))
        dx = (tx0 + tx1) / 2 - ax
        align = tg.get("align", "top")
        if align == "top": dy = ty0 - ay - sc * (y0 - ay)
        elif align == "bottom": dy = ty1 - ay - sc * (y1 - ay)
        else: dy = (ty0 + ty1) / 2 - ay - sc * ((y0 + y1) / 2 - ay)
        o = {"dx": round(dx, 1), "dy": round(dy, 1), "scale": round(sc, 4)}
        parts = {}
        for name, pt in tg.get("parts", {}).items():
            e = [e for e in sd["parts"] if e["part"] == name][0]
            bx0, by0, bx1, by1 = e["bbox"]
            cx = ax + dx + sc * ((bx0 + bx1) / 2 - ax)
            cy = ay + dy + sc * ((by0 + by1) / 2 - ay)
            px0, py0, px1, py1 = pt["box"]
            w, h = sc * (bx1 - bx0), sc * (by1 - by0)
            ps = min((px1 - px0) / w, (py1 - py0) / h)
            parts[name] = {"dx": round((px0 + px1) / 2 - cx, 1), "dy": round((py0 + py1) / 2 - cy, 1), "scale": round(ps, 4)}
        if parts:
            o["parts"] = parts
        offs[slot] = o
    spec["gear_offsets"] = offs
    json.dump(spec, open(spec_path, "w", encoding="utf-8"), indent=1)
    rp = os.path.join(RES, cid, "rig.json")
    rig = json.load(open(rp, encoding="utf-8"))
    rig["gear_offsets"] = offs
    json.dump(rig, open(rp, "w", encoding="utf-8"), indent=1)
    print(cid, json.dumps(offs))
    return 0


def cmd_gear(args):
    gear_id = args[0]
    spec = load_spec(args[args.index("--zones") + 1] if "--zones" in args else "momo")
    centre_x = int(args[args.index("--centre") + 1]) if "--centre" in args else 520
    out_dir = os.path.join(RES, "gear_" + gear_id)
    pieces = [a for a in args[1:] if "=" in a]
    gear = {"id": gear_id, "canvas": spec["canvas"], "ankle_y": spec["ankle_y"], "pants_gap": PANTS_GAP, "slots": {}}
    for pc in pieces:
        slot, path = pc.split("=", 1)
        rgba = np.array(Image.open(path).convert("RGBA"))
        parts = cut_gear(slot, rgba, spec, centre_x)
        entries = []
        for name in SLOTS[slot]["parts"]:
            im = parts[name]
            if (im[..., 3] > 0).sum() == 0:
                continue
            save_part(os.path.join(out_dir, slot, name + ".png"), im)
            entries.append({"part": name, "file": "%s/%s.png" % (slot, name), "above": SLOTS[slot]["above"][name]})
        gear["slots"][slot] = {"parts": entries}
        print("gear %s: %s" % (slot, {e["part"]: int((parts[e["part"]][..., 3] > 0).sum()) for e in entries}))
    anchors = {}
    for slot, sd in gear["slots"].items():
        bb = [bbox_of(os.path.join(out_dir, e["file"])) for e in sd["parts"]]
        u = [min(b[0] for b in bb), min(b[1] for b in bb), max(b[2] for b in bb), max(b[3] for b in bb)]
        anchors[slot] = [(u[0] + u[2]) // 2, (u[1] + u[3]) // 2]
        sd["bbox"] = u
        for e, b in zip(sd["parts"], bb):
            e["bbox"] = b
    gear["anchors"] = anchors
    gear["order_note"] = "each gear part is drawn right above the character part named in 'above'"
    with open(os.path.join(out_dir, "gear.json"), "w", encoding="utf-8") as f:
        json.dump(gear, f, indent=1)
    return 0


if __name__ == "__main__":
    cmds = {"char": cmd_char, "gear": cmd_gear, "fit": cmd_fit}
    if len(sys.argv) < 3 or sys.argv[1] not in cmds:
        print(__doc__)
        sys.exit(2)
    sys.exit(cmds[sys.argv[1]](sys.argv[2:]))
