"""Cuts a gear set painted on the grey polygon doll into pieces on the 1365 x 1649 canvas (docs/19).

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/cut_gear_set.py <painted.png> <out_dir>

Writes head.png, body.png, sleeveL.png, sleeveR.png, bootL.png, bootR.png (full-canvas transparent, assembled positions) and preview.png.
Gear = pixels that are coloured (not the grey doll, not the grey background) plus the dark outline touching them.
Registration: uniform scale from the doll's arm span (canvas x 250 to 1115 at the arm rows), feet bottom to y 1605, centred on x 682.
"""
import os
import sys
import numpy as np
import cv2

W, H = 1365, 1649


def figure_mask(img):
    """Closed silhouette from the dark navy outline (the doll and gear are always outlined)."""
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
    dark = (hsv[..., 2] < 95).astype(np.uint8)
    dark = cv2.morphologyEx(dark, cv2.MORPH_CLOSE, np.ones((7, 7), np.uint8))
    h, w = dark.shape
    flood = dark.copy()
    ff = np.zeros((h + 2, w + 2), np.uint8)
    cv2.floodFill(flood, ff, (2, 2), 2)
    fig = (flood != 2).astype(np.uint8)
    n, lab, st, _ = cv2.connectedComponentsWithStats(fig, 8)
    big = 1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA]))
    return (lab == big)


def gear_mask(img, fig):
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
    sat, val = hsv[..., 1], hsv[..., 2]
    col = ((sat > 70) | (val > 215)) & fig
    col = cv2.morphologyEx(col.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    near = cv2.dilate(col, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (25, 25)))
    dark = (val < 95)
    g = (col > 0) | ((near > 0) & dark & fig)
    return cv2.morphologyEx(g.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8)).astype(bool)


def register(fig):
    ys, xs = np.where(fig)
    y0, y1 = ys.min(), ys.max()
    fh = y1 - y0
    xs_all = []
    for y in range(int(y0 + 0.64 * fh), int(y0 + 0.80 * fh)):
        r = np.where(fig[y])[0]
        if len(r):
            xs_all.append((r.min(), r.max()))
    xl = np.median([a for a, b in xs_all])
    xr = np.median([b for a, b in xs_all])
    s = 870.0 / (xr - xl)
    cx = (xl + xr) / 2
    A = np.array([[s, 0, 682 - cx * s], [0, s, 1605 - y1 * s]], np.float32)   # picture -> canvas
    return A


def main():
    src, out = sys.argv[1], sys.argv[2]
    img = cv2.imread(src)
    fig = figure_mask(img)
    gm = gear_mask(img, fig)
    A = register(fig)
    rgba = np.dstack([img, (gm * 255).astype(np.uint8)])
    canvas = cv2.warpAffine(rgba, A, (W, H), flags=cv2.INTER_AREA, borderValue=(0, 0, 0, 0))
    a = canvas[..., 3] > 100
    yy, xx = np.mgrid[0:H, 0:W]
    zones = {
        "head": yy < 1000,
        "sleeveL": (yy >= 1000) & (xx < 400) & (yy < 1400),
        "sleeveR": (yy >= 1000) & (xx > 965) & (yy < 1400),
        "body": (yy >= 1000) & (xx >= 400) & (xx <= 965) & (yy < 1400),
        "bootL": (yy >= 1400) & (xx < 682),
        "bootR": (yy >= 1400) & (xx >= 682),
    }
    os.makedirs(out, exist_ok=True)
    full = np.zeros((H, W, 4), np.uint8)
    info = {}
    for k, z in zones.items():
        p = np.zeros((H, W, 4), np.uint8)
        sel = a & z
        p[sel] = canvas[sel]
        p[..., 3] = np.where(sel, 255, 0)
        cv2.imwrite(os.path.join(out, k + ".png"), p)
        al = p[..., 3:4] / 255.0
        full[..., :3] = (p[..., :3] * al + full[..., :3] * (1 - al)).astype(np.uint8)
        full[..., 3] = np.maximum(full[..., 3], p[..., 3])
        info[k] = int(sel.sum())
    cv2.imwrite(os.path.join(out, "preview.png"), full)
    print(info, "scale %.3f" % A[0, 0])


if __name__ == "__main__":
    main()
