"""QA and contact sheet for cut polygon-doll hero parts (CUX-9). Costs nothing.

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/qa_hero_parts.py <out_sheet.png> <part_dir> [<part_dir> ...]

Per folder: part overlap (pixels opaque in two parts, must be 0), opaque pixels touching the canvas edge (clipped), grey key-colour fringe
(share of edge pixels that are unsaturated mid-grey, the Common level is 0.1 to 3.7 percent), and the bounding box / area of every part.
Writes one contact sheet: per hero a row with the stack on a dark and a light background and every part on a checkerboard.
"""
import os
import sys
import numpy as np
import cv2

PARTS = ["back", "legL", "legR", "body", "handL", "handR", "head"]


def load(d):
    out = {}
    for p in PARTS:
        f = os.path.join(d, p + ".png")
        if os.path.exists(f):
            im = cv2.imread(f, cv2.IMREAD_UNCHANGED)
            out[p] = cv2.cvtColor(im, cv2.COLOR_BGRA2RGBA)
    return out


def stack(parts, order, bg):
    h, w = next(iter(parts.values())).shape[:2]
    full = np.zeros((h, w, 3), np.float32) + np.array(bg, np.float32)
    for k in order:
        if k not in parts:
            continue
        a = parts[k][..., 3:4].astype(np.float32) / 255.0
        full = parts[k][..., :3].astype(np.float32) * a + full * (1 - a)
    return full.astype(np.uint8)


def checker(h, w, cell=40):
    yy, xx = np.mgrid[0:h, 0:w]
    m = ((yy // cell + xx // cell) % 2) == 0
    a = np.zeros((h, w, 3), np.uint8)
    a[m] = 205
    a[~m] = 160
    return a


def qa(d, parts):
    order = [p for p in PARTS if p in parts]
    h, w = parts[order[0]].shape[:2]
    cnt = np.zeros((h, w), np.int16)
    for p in order:
        cnt += (parts[p][..., 3] > 8)
    overlap = int((cnt > 1).sum())
    union = (cnt > 0)
    edge = union.copy()
    edge[2:-2, 2:-2] = False
    # grey fringe: edge pixels of the union (1 px ring) that are unsaturated mid-grey
    er = cv2.erode(union.astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
    ring = union & ~er
    rgb = stack(parts, order, (0, 0, 0))
    mx, mn = rgb.max(axis=2).astype(int), rgb.min(axis=2).astype(int)
    grey = ring & ((mx - mn) < 16) & (mx > 90) & (mx < 190)
    print("%s: overlap %d px, touches canvas edge %d px, grey edge pixels %.2f%% of %d edge px" % (os.path.basename(d.rstrip("/\\")), overlap, int(edge.sum()), 100.0 * grey.sum() / max(1, ring.sum()), int(ring.sum())))
    for p in order:
        ys, xs = np.where(parts[p][..., 3] > 8)
        print("   %-6s area %7d  bbox x %4d..%4d  y %4d..%4d" % (p, len(ys), xs.min() if len(xs) else 0, xs.max() if len(xs) else 0, ys.min() if len(ys) else 0, ys.max() if len(ys) else 0))


def main():
    out = sys.argv[1]
    dirs = sys.argv[2:]
    cell_w = 340
    rows = []
    for d in dirs:
        parts = load(d)
        qa(d, parts)
        order = [p for p in PARTS if p in parts]
        h, w = parts[order[0]].shape[:2]
        sc = cell_w / float(w)
        tiles = [stack(parts, order, (30, 30, 40)), stack(parts, order, (235, 235, 235))]
        ck = checker(h, w)
        for p in order:
            t = ck.copy().astype(np.float32)
            a = parts[p][..., 3:4].astype(np.float32) / 255.0
            tiles.append((parts[p][..., :3] * a + t * (1 - a)).astype(np.uint8))
        tiles = [cv2.resize(t, None, fx=sc, fy=sc, interpolation=cv2.INTER_AREA) for t in tiles]
        label = np.zeros((40, tiles[0].shape[1] * len(tiles), 3), np.uint8)
        cv2.putText(label, os.path.basename(d.rstrip("/\\")) + "  (stack dark, stack light, " + ", ".join(order) + ")", (6, 28), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (255, 255, 255), 2)
        rows.append(np.vstack([label, np.hstack(tiles)]))
    wmax = max(r.shape[1] for r in rows)
    rows = [np.pad(r, ((0, 0), (0, wmax - r.shape[1]), (0, 0))) for r in rows]
    cv2.imwrite(out, cv2.cvtColor(np.vstack(rows), cv2.COLOR_RGB2BGR))
    print("wrote", out)


if __name__ == "__main__":
    main()
