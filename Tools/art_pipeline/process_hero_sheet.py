"""Cuts the hero sheet frame art (owner pick sheet_a_wood.png, Layout A) into 9-slice plates, no credits.
    python Tools/art_pipeline/process_hero_sheet.py
Keys the flat grey around the plates to transparent (process_art.key_image), crops every plate by hand-measured box,
scales it to 0.7 (so the widest is under the 1024 px import cap and the slice borders written here are exact) and writes
Assets/Resources/Art/HeroSheet/<name>.png plus slices.json ({"name": {"w","h","border":[l,b,r,t]}}).
ribbon_gray is a grey-scale copy of the rarity ribbon so the game can tint it per rarity without muddy colours.
"""
import json
import os
import sys

sys.path.insert(0, "E:/pylibs")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402
from process_art import key_image  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Art", "_candidates", "hero_sheet", "sheet_a_wood.png")
DST = os.path.join(ROOT, "Assets", "Resources", "Art", "HeroSheet")
K = 1.4235   # display (1124 x 2000) to the 1600 x 2848 original
SCALE = 0.7

# name: (x0, y0, x1, y1 in display coords, border l, b, r, t in OUTPUT px)
PLATES = {
    "header":   ((130, 44, 998, 188), (104, 70, 104, 70)),
    "portrait": ((64, 300, 536, 810), (84, 84, 84, 84)),
    "name":     ((562, 302, 1056, 428), (48, 44, 48, 44)),
    "ribbon":   ((556, 424, 1060, 556), (190, 0, 190, 0)),
    "region":   ((562, 547, 1056, 673), (48, 44, 48, 44)),
    "gold":     ((562, 677, 1056, 808), (48, 44, 48, 44)),
    "tab_a":    ((130, 854, 563, 983), (44, 44, 44, 44)),
    "tab_b":    ((562, 854, 993, 983), (44, 44, 44, 44)),
    "stat_icon": ((72, 1027, 232, 1178), (44, 44, 44, 44)),
    "stat_value": ((226, 1027, 1056, 1178), (50, 50, 50, 50)),
    "panel":    ((64, 1537, 1058, 1983), (88, 88, 88, 88)),
}


def main():
    rgb = np.asarray(Image.open(SRC).convert("RGB"))
    rgba, _ = key_image(rgb)
    os.makedirs(DST, exist_ok=True)
    out = {}
    for name, ((x0, y0, x1, y1), border) in PLATES.items():
        box = [int(round(v * K)) for v in (x0, y0, x1, y1)]
        crop = rgba[box[1]:box[3], box[0]:box[2]].copy()
        a = crop[:, :, 3] > 24
        ys, xs = np.nonzero(a)
        crop = crop[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        im = Image.fromarray(crop)
        w, h = max(1, round(im.width * SCALE)), max(1, round(im.height * SCALE))
        im = im.resize((w, h), Image.LANCZOS)
        im.save(os.path.join(DST, name + ".png"))
        out[name] = {"w": w, "h": h, "border": list(border)}
        if name == "ribbon":
            arr = np.asarray(im).astype(np.float32)
            lum = 0.299 * arr[:, :, 0] + 0.587 * arr[:, :, 1] + 0.114 * arr[:, :, 2]
            vis = arr[:, :, 3] > 200
            lum = np.clip(lum / np.percentile(lum[vis], 92), 0, 1) * 235
            # keep the dark outline dark, lift the body so a tint stays saturated
            g = np.stack([lum, lum, lum, arr[:, :, 3]], axis=2).astype(np.uint8)
            Image.fromarray(g).save(os.path.join(DST, "ribbon_gray.png"))
            out["ribbon_gray"] = {"w": w, "h": h, "border": list(border)}
        print(name, w, "x", h)
    with open(os.path.join(DST, "slices.json"), "w") as f:
        json.dump(out, f, indent=1)


if __name__ == "__main__":
    main()
