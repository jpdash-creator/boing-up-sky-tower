"""Cleans the exported pilot gear layers (Assets/Resources/Art/GearLayers/<slot>_<n>.png) in place: the cut-outs still carry fragments of Momo's cream fur
and dark sticker outline (the ghost double outline around the head and body in the game). Kept: pixels within REACH px of the gear's own colour (saturated
and not too dark) and the connected pieces that are big enough; everything else is made transparent. Idempotent. Run after export_gear_layers.py and before
export_gear_icons.py:

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/clean_gear_layers.py [--preview out.png]
"""
import os
import re
import sys

import cv2
import numpy as np
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
DIR = os.path.join(ROOT, "Assets", "Resources", "Art", "GearLayers")
REACH = 7
MIN_AREA = 900


def clean(rgba):
    a = rgba[..., 3]
    hsv = cv2.cvtColor(rgba[..., :3], cv2.COLOR_RGB2HSV)
    core = (a > 200) & (hsv[..., 1] > 85) & (hsv[..., 2] > 60)
    k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * REACH + 1, 2 * REACH + 1))
    near = cv2.dilate(core.astype(np.uint8), k).astype(bool)
    out = rgba.copy()
    out[~near, 3] = 0
    n, lab, stats, _ = cv2.connectedComponentsWithStats((out[..., 3] > 0).astype(np.uint8), connectivity=8)
    if n > 1:
        biggest = stats[1:, cv2.CC_STAT_AREA].max()
        for i in range(1, n):
            if stats[i, cv2.CC_STAT_AREA] < max(MIN_AREA, 0.025 * biggest):   # stray eye, mouth and fur fragments
                out[lab == i, 3] = 0
    return out


def main():
    files = [f for f in sorted(os.listdir(DIR)) if re.match(r"^[a-z]+_\d+(_back)?\.png$", f)]
    cells = []
    for f in files:
        p = os.path.join(DIR, f)
        im = np.array(Image.open(p).convert("RGBA"))
        before = int((im[..., 3] > 0).sum())
        out = clean(im)
        Image.fromarray(out).save(p)
        print(f, before, "->", int((out[..., 3] > 0).sum()))
        cells.append(out)
    if "--preview" in sys.argv:
        out_png = sys.argv[sys.argv.index("--preview") + 1]
        w, h = 340, 410
        sheet = Image.new("RGB", (w * 7, h * ((len(cells) + 6) // 7)), (90, 100, 120))
        for i, c in enumerate(cells):
            im = Image.fromarray(c).resize((w, h))
            sheet.paste(im, ((i % 7) * w, (i // 7) * h), im)
        sheet.save(out_png)


if __name__ == "__main__":
    main()
