"""Slices the power-up icon sheet (3 rows: 4 + 4 + 3 round badges on a flat grey background) into 256 px sprites.

Usage: python slice_powerup_icons.py <sheet.png> <out_dir>
Output: pw_flight.png ... pw_safetynet.png (RGBA, circular alpha). Numpy and PIL only.
"""
import os
import sys

import numpy as np
from PIL import Image

IDS = ["pw_flight", "pw_rocket", "pw_doublejump", "pw_wallstick",
       "pw_shield", "pw_magnet", "pw_slowtime", "pw_springboots",
       "pw_coindoubler", "pw_starseeker", "pw_safetynet"]
ROWS = [4, 4, 3]
OUT = 256


def bands(mask_1d, min_gap, min_len):
    runs, start = [], None
    for i, v in enumerate(mask_1d):
        if v and start is None:
            start = i
        if not v and start is not None:
            runs.append([start, i])
            start = None
    if start is not None:
        runs.append([start, len(mask_1d)])
    merged = []
    for r in runs:
        if merged and r[0] - merged[-1][1] < min_gap:
            merged[-1][1] = r[1]
        else:
            merged.append(r)
    return [r for r in merged if r[1] - r[0] >= min_len]


def main():
    sheet, out_dir = sys.argv[1], sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)
    img = Image.open(sheet).convert("RGB")
    a = np.asarray(img).astype(np.int16)
    h, w, _ = a.shape
    bg = np.median(a[:40, :40].reshape(-1, 3), axis=0)
    mask = (np.abs(a - bg).sum(axis=2) > 60)
    rows = bands(mask.sum(axis=1) > 8, 20, 150)
    assert len(rows) == 3, "expected 3 rows, got %d" % len(rows)
    k = 0
    for ri, (y0, y1) in enumerate(rows):
        cols = bands(mask[y0:y1].sum(axis=0) > 8, 20, 150)
        assert len(cols) == ROWS[ri], "row %d: expected %d badges, got %d" % (ri, ROWS[ri], len(cols))
        for (x0, x1) in cols:
            sub = mask[y0:y1, x0:x1]
            ys, xs = np.nonzero(sub)
            by0, by1, bx0, bx1 = y0 + ys.min(), y0 + ys.max() + 1, x0 + xs.min(), x0 + xs.max() + 1
            size = max(by1 - by0, bx1 - bx0)
            cy, cx = (by0 + by1) / 2.0, (bx0 + bx1) / 2.0
            pad = 6
            box = (int(cx - size / 2 - pad), int(cy - size / 2 - pad), int(cx + size / 2 + pad), int(cy + size / 2 + pad))
            crop = img.crop(box).convert("RGBA")
            n = crop.size[0]
            yy, xx = np.mgrid[0:n, 0:n]
            r = np.hypot(xx - n / 2.0 + 0.5, yy - n / 2.0 + 0.5)
            radius = size / 2.0 + 1.0
            alpha = np.clip((radius - r) * 1.0 + 0.5, 0, 1)
            arr = np.asarray(crop).copy()
            arr[:, :, 3] = (alpha * 255).astype(np.uint8)
            res = Image.fromarray(arr, "RGBA").resize((OUT, OUT), Image.LANCZOS)
            res.save(os.path.join(out_dir, IDS[k] + ".png"))
            k += 1
    print("wrote", k, "icons to", out_dir)


if __name__ == "__main__":
    main()
