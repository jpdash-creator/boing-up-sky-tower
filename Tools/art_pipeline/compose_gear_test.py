"""Composite test for the gear layers cut by cut_gear_layers.py. Costs nothing.

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/compose_gear_test.py [--out E:/Unity/logs]

Draws the clean Momo plus one layer per slot (back, body, pants, boots, head) and saves contact sheets:
  gear_compose_legendary.png  all slots index 5 (Legendary)   gear_compose_common.png   all slots index 1
  gear_compose_mixed.png      assorted combinations            gear_compose_single_<slot>.png  every piece of one slot alone
Checkerboard-free grey background so grey halos and double outlines are visible; review by eye.
"""
import argparse
import json
import os
import sys

sys.path.insert(0, "E:/pylibs")
import cv2  # noqa: E402
import numpy as np  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GEAR = os.path.join(ROOT, "Assets", "Art", "Gear")
MOMO = os.path.join(ROOT, "Assets", "Art", "Characters", "char_momo", "char_momo", "char_momo.png")
BG = (200, 170, 140)  # BGR: a light blue-ish sky so white/grey halos show up


def over(dst, src, off=(0, 0)):
    if off != (0, 0) or src.shape[:2] != dst.shape[:2]:
        full = np.zeros(dst.shape[:2] + (4,), np.uint8)
        h, w = src.shape[:2]
        full[off[1]:off[1] + h, off[0]:off[0] + w] = src
        src = full
    a = src[:, :, 3:4].astype(np.float32) / 255
    d = dst.astype(np.float32)
    d[:, :, :3] = src[:, :, :3] * a + d[:, :, :3] * (1 - a)
    return d.astype(np.uint8)


def compose(momo, layers, picks):
    """picks: {slot: index}. Order: back layers, Momo, body, pants, boots, head."""
    ox, oy = layers["momoOffset"]
    cw, ch = layers["canvas"]
    canvas = np.zeros((ch, cw, 3), np.uint8)
    canvas[:] = BG
    mp = np.zeros((ch, cw, 4), np.uint8)
    mp[oy:oy + momo.shape[0], ox:ox + momo.shape[1]] = momo
    entries = {(e["slot"], e["index"]): e for e in layers["layers"]}
    img = canvas
    for slot, idx in picks.items():
        e = entries[(slot, idx)]
        if e["backFile"]:
            img = over(img, cv2.imread(os.path.join(GEAR, e["backFile"]), cv2.IMREAD_UNCHANGED), tuple(e["backOffset"]))
    img = over(img, mp)
    for slot in sorted(picks, key=lambda s: layers["drawOrder"][s]):
        e = entries[(slot, picks[slot])]
        img = over(img, cv2.imread(os.path.join(GEAR, e["file"]), cv2.IMREAD_UNCHANGED), tuple(e["offset"]))
    return img


def sheet(images, cols, scale=0.38):
    ims = [cv2.resize(i, None, fx=scale, fy=scale, interpolation=cv2.INTER_AREA) for i in images]
    h, w = ims[0].shape[:2]
    rows = (len(ims) + cols - 1) // cols
    out = np.full((rows * h, cols * w, 3), BG, np.uint8)
    for k, im in enumerate(ims):
        r, c = divmod(k, cols)
        out[r * h:(r + 1) * h, c * w:(c + 1) * w] = im
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="E:/Unity/logs")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    momo = cv2.imread(MOMO, cv2.IMREAD_UNCHANGED)
    with open(os.path.join(GEAR, "layers.json")) as f:
        layers = json.load(f)
    slots = ["head", "body", "pants", "boots"]
    sets = {
        "legendary": [{s: 5 for s in slots}],
        "common": [{s: 1 for s in slots}],
        "epic": [{s: 4 for s in slots}],
        "rare": [{s: 3 for s in slots}],
        "uncommon": [{s: 2 for s in slots}],
        "mythic6": [{s: 6 for s in slots}],
    }
    mixed = [{"head": 3, "body": 6, "pants": 4, "boots": 1}, {"head": 2, "body": 1, "pants": 5, "boots": 6},
             {"head": 4, "body": 3, "pants": 2, "boots": 5}, {"head": 6, "body": 2, "pants": 1, "boots": 3},
             {"head": 1, "body": 5, "pants": 6, "boots": 4}, {"head": 5, "body": 4, "pants": 3, "boots": 2}]
    allsets = [sets[k][0] for k in ["common", "uncommon", "rare", "epic", "legendary", "mythic6"]]
    paths = []
    p = os.path.join(args.out, "gear_compose_levels.png")
    cv2.imwrite(p, sheet([compose(momo[:, :, :], layers, s) for s in allsets], 3)); paths.append(p)
    for name in ("legendary", "common"):
        p = os.path.join(args.out, "gear_compose_%s.png" % name)
        cv2.imwrite(p, sheet([compose(momo, layers, sets[name][0])], 1, 0.6)); paths.append(p)
    p = os.path.join(args.out, "gear_compose_mixed.png")
    cv2.imwrite(p, sheet([compose(momo, layers, s) for s in mixed], 3)); paths.append(p)
    for slot in slots:
        ims = [compose(momo, layers, {slot: i}) for i in range(1, 7)]
        p = os.path.join(args.out, "gear_compose_single_%s.png" % slot)
        cv2.imwrite(p, sheet(ims, 3)); paths.append(p)
    print("\n".join(paths))


if __name__ == "__main__":
    main()
