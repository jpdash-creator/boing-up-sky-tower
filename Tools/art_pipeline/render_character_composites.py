"""Assemble characters from their part PNGs, optionally wearing gear, and write previews / contact sheets.
Costs nothing (no OpenArt calls). This is the reference for how the game stacks parts.

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/render_character_composites.py wear <char_id> <gear_id> <out.png>
    python Tools/art_pipeline/render_character_composites.py sheet <out.png> [gear_id]

Stacking: character parts in the order of rig.json 'order' (back ... head); a gear part is drawn right above the
character part named in gear.json ('above'). rig.json may hold per-slot 'gear_offsets': {slot: {dx, dy, scale}},
applied to the gear parts of that slot about the slot's anchor (gear.json 'anchors', canvas px of the gear's own
canvas): p' = anchor + (dx, dy) + scale * (p - anchor).
"""
import json
import os
import sys

sys.path.insert(0, "E:/pylibs")
import cv2  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cut_character_parts import RES, composite  # noqa: E402

DEFAULT_ANCHORS = {"head": [520, 935], "body": [520, 1000], "pants": [520, 1390], "boots": [520, 1600]}
SLOT_ORDER = ["pants", "body", "boots", "head"]


def load_char(cid):
    d = os.path.join(RES, cid)
    rig = json.load(open(os.path.join(d, "rig.json"), encoding="utf-8"))
    parts = {p: np.array(Image.open(os.path.join(d, p + ".png")).convert("RGBA")) for p in rig["order"]}
    return rig, parts


def affine(anchor, dx, dy, scale):
    ax, ay = anchor
    return np.array([[scale, 0, ax + dx - scale * ax], [0, scale, ay + dy - scale * ay], [0, 0, 1]], np.float64)


def transform(im, slot_off, anchor, part_off=None):
    """slot offset about the slot anchor, then (optional) part override about the part's own transformed centre."""
    m = affine(anchor, slot_off.get("dx", 0), slot_off.get("dy", 0), slot_off.get("scale", 1.0))
    if part_off:
        ys, xs = np.nonzero(im[..., 3] > 0)
        if len(xs):
            c = m @ np.array([(xs.min() + xs.max()) / 2, (ys.min() + ys.max()) / 2, 1.0])
            m = affine((c[0], c[1]), part_off.get("dx", 0), part_off.get("dy", 0), part_off.get("scale", 1.0)) @ m
    if np.allclose(m, np.eye(3)):
        return im
    h, w = im.shape[:2]
    return cv2.warpAffine(im, m[:2].astype(np.float32), (w, h), flags=cv2.INTER_LANCZOS4,
                          borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0))


def wear(cid, gear_ids, rig=None, parts=None):
    rig, parts = (rig, parts) if rig else load_char(cid)
    order = list(rig["order"])
    stack = [(p, parts[p]) for p in order]
    extra = []  # (z, im)
    for gid in gear_ids:
        gd = os.path.join(RES, "gear_" + gid)
        gear = json.load(open(os.path.join(gd, "gear.json"), encoding="utf-8"))
        for slot in SLOT_ORDER:
            if slot not in gear["slots"]:
                continue
            off = rig.get("gear_offsets", {}).get(slot, {})
            anchor = gear.get("anchors", {}).get(slot, DEFAULT_ANCHORS[slot])
            for e in gear["slots"][slot]["parts"]:
                im = np.array(Image.open(os.path.join(gd, e["file"])).convert("RGBA"))
                im = transform(im, off, anchor, off.get("parts", {}).get(e["part"]))
                extra.append((order.index(e["above"]) + 0.5 + SLOT_ORDER.index(slot) * 0.01, im))
    z = [(float(i), im) for i, (_, im) in enumerate(stack)] + extra
    z.sort(key=lambda t: t[0])
    layers = {str(i): im for i, (_, im) in enumerate(z)}
    return composite(layers, order=list(layers.keys()))


def on_grey(rgba, bg=(112, 118, 128)):
    a = rgba[..., 3:4] / 255.0
    base = np.zeros(rgba.shape[:2] + (3,), np.uint8)
    base[:] = bg
    return (rgba[..., :3] * a + base * (1 - a)).astype(np.uint8)


def main():
    a = sys.argv[1:]
    if len(a) < 2:
        print(__doc__)
        return 2
    if a[0] == "wear":
        Image.fromarray(on_grey(wear(a[1], a[2].split(",")))).save(a[3])
    elif a[0] == "sheet":
        chars = ["momo", "sporeling", "moth", "rootling"]
        gear = a[2].split(",") if len(a) > 2 else []
        tiles = [on_grey(wear(c, gear)) for c in chars]
        sheet = np.concatenate(tiles, 1)
        Image.fromarray(sheet).resize((sheet.shape[1] // 2, sheet.shape[0] // 2), Image.LANCZOS).save(a[1])
    return 0


if __name__ == "__main__":
    sys.exit(main())
