"""Slices a platform width sheet (one platform per row, thin to long) and bakes one UNSCALED piece for every target width.

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/slice_width_sheet.py <sheet.png> <region_index> 1.5 2 3 4 6 8 [--ppu 334]

The generator cannot draw exact lengths, so each target width starts from the sliced row whose natural length is closest and is then
shortened (a stretch of plain top is cut out) or lengthened (a seamless stretch is repeated) in the middle. The cut/repeat columns are chosen where the
picture matches itself best, so no seam shows. Nothing is ever scaled: every pixel is real painted art and the slab thickness is identical
for all widths (CUX-9 addendum, docs/16). Output:
  Art/_candidates/<sheet dir>/raw/            the raw rows
  Assets/Art/Regions/<region>/tiles/reg00N_widths/reg00N_w<width>.png  + platform_set.json (body extent, ppu)
"""
import glob
import json
import os
import sys

sys.path.insert(0, "E:/pylibs")
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

import platform_metrics as pm  # noqa: E402
import process_art as pa  # noqa: E402

WIN = 28          # px of context compared at the join
CAP = 110         # px at each end that are never cut or repeated (the rounded caps)
FADE = 64         # px cross-fade at every join
SCALE = 0.5       # uniform shrink applied identically to every baked piece (keeps textures at most 1400 px wide)


def pink(a, x0, x1):
    """Number of flower-pink pixels in the columns [x0, x1): a cross-fade through a flower would leave a ghost."""
    r = a[:, max(0, x0):x1]
    return int(((r[:, :, 0] > 200) & (r[:, :, 1] < 170) & (r[:, :, 2] > 110) & (r[:, :, 3] > 200)).sum())


def column_cost(a, p, q):
    """How badly columns p.. and q.. differ (RGBA, WIN wide): low = the two joins look alike. Joins near flowers are ruled out."""
    x = a[:, p:p + WIN].astype(np.float32)
    y = a[:, q:q + WIN].astype(np.float32)
    cost = float(np.abs(x - y).mean())
    if pink(a, p - FADE, p + 2 * FADE) or pink(a, q - FADE, q + 2 * FADE):
        cost += 1000.0
    return cost


def blend(x, y):
    """Linear cross-fade from x to y over equally wide column blocks (RGBA)."""
    t = np.linspace(0.0, 1.0, x.shape[1], dtype=np.float32)[None, :, None]
    return (x.astype(np.float32) * (1.0 - t) + y.astype(np.float32) * t).round().astype(np.uint8)


def cut_out(a, remove):
    """Shorter by `remove` px: delete the stretch [p, p+remove) whose two joins match best, cross-faded."""
    lo, hi = CAP, a.shape[1] - CAP - remove - FADE - WIN
    best = min(range(lo, hi), key=lambda p: column_cost(a, p, p + remove))
    mid = blend(a[:, best:best + FADE], a[:, best + remove:best + remove + FADE])
    return np.concatenate([a[:, :best], mid, a[:, best + remove + FADE:]], axis=1)


def repeat(a, add):
    """Longer by `add` px: repeat the stretch [p, q) whose two joins match best (in chunks when add is large), cross-faded at the seam."""
    while add > 0:
        chunk = min(add, max(150, (a.shape[1] - 2 * CAP) // 2))
        lo, hi = CAP + FADE, a.shape[1] - CAP - chunk - WIN
        best = min(range(lo, hi), key=lambda p: column_cost(a, p, p + chunk))
        q = best + chunk
        first = a[:, :q].copy()
        first[:, q - FADE:q] = blend(a[:, q - FADE:q], a[:, best - FADE:best])
        a = np.concatenate([first, a[:, best:]], axis=1)
        add -= chunk
    return a


def main():
    args = [x for x in sys.argv[1:]]
    ppu = 334.0
    if "--ppu" in args:
        i = args.index("--ppu")
        ppu = float(args[i + 1])
        del args[i:i + 2]
    sheet, region = args[0], int(args[1])
    widths = [float(x) for x in args[2:]]
    regionDir = glob.glob(os.path.join(pa.ROOT, "Assets", "Art", "Regions", "region_00%d_*" % region))[0]
    outdir = os.path.join(regionDir, "tiles", "reg00%d_widths" % region)
    rawdir = os.path.join(os.path.dirname(os.path.abspath(sheet)), "raw")
    os.makedirs(outdir, exist_ok=True)
    os.makedirs(rawdir, exist_ok=True)
    for f in glob.glob(os.path.join(outdir, "*.png")) + glob.glob(os.path.join(outdir, "*.json")) + glob.glob(os.path.join(rawdir, "*.png")):
        os.remove(f)

    rgb = np.asarray(Image.open(sheet).convert("RGB"))
    rgba, bg = pa.key_image(rgb, False)
    found = [f for f in pa.split_sheet(rgba, None) if f[0][2] > 100]
    found.sort(key=lambda f: f[0][1])
    rows = []
    for n, (bbox, crop) in enumerate(found, start=1):
        path = os.path.join(rawdir, "row%d.png" % n)
        Image.fromarray(crop).save(path)
        m = pm.measure(path)
        rows.append({"a": crop, "m": m, "natural": (m["bodyRight"] - m["bodyLeft"]) / ppu})
        print("row %d: body %d px = %.2f u" % (n, m["bodyRight"] - m["bodyLeft"], rows[-1]["natural"]))

    pieces = []
    for w in widths:
        src = min(rows, key=lambda r: abs(r["natural"] - w))
        a = src["a"]
        margin = a.shape[1] - (src["m"]["bodyRight"] - src["m"]["bodyLeft"])      # glow margins left and right of the body
        want = int(round(w * ppu)) + margin
        delta = want - a.shape[1]
        if delta < 0:
            a = cut_out(a, -delta)
        elif delta > 0:
            a = repeat(a, delta)
        name = "reg00%d_w%s.png" % (region, ("%g" % w).replace(".", "_"))
        path = os.path.join(outdir, name)
        im = Image.fromarray(a)
        im = im.resize((max(1, round(im.width * SCALE)), max(1, round(im.height * SCALE))), Image.LANCZOS)   # ONE uniform shrink for every piece (texture size)
        im.save(path)
        m = pm.measure(path)
        body = m["bodyRight"] - m["bodyLeft"]
        m.update({"width": w, "file": name, "ppu": round(body / w, 3)})
        pieces.append(m)
        print("%-16s target %.1f u: body %d px = %.2f u at ppu %.0f  thickness %.2f u  (from natural %.2f u)" % (
            name, w, body, body / ppu, ppu, (m["bodyBottom"] - m["surfaceY"]) / ppu, src["natural"]))
    with open(os.path.join(outdir, "platform_set.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump({"asset": "reg00%d_widths" % region, "ppuBake": ppu, "pieces": pieces}, f, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
