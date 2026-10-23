"""Bakes the special platforms (crumble, moving, bounce) from the approved width slabs plus the overlay parts, at native size.

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_specials.py <overlay_sheet.png> <region_index> [2 4]

1. The 3 x 3 overlay sheet (crack long, crack short, rock chips / knob, knob, glow / spring short, spring tall, spark) is keyed and cut into parts
   (Art/_candidates/<sheet dir>/parts/).
2. For each special kind and each width, the slab of that width (reg00N_w<width>.png from slice_width_sheet.py) gets its parts pasted on with ONE
   uniform scale per part. Nothing is ever stretched (CUX-9 addendum, docs/16). The baked pieces are added to platform_set.json with their kind.
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

NAMES = ["crack_long", "crack_short", "chips", "knob_a", "knob_b", "glow", "spring_short", "spring_tall", "spark"]


def cut_parts(sheet, outdir):
    os.makedirs(outdir, exist_ok=True)
    for f in glob.glob(os.path.join(outdir, "*.png")):
        try:
            os.remove(f)
        except PermissionError:
            pass   # a viewer may hold the file; it is overwritten below
    rgb = np.asarray(Image.open(sheet).convert("RGB"))
    rgba, bg = pa.key_image(rgb, True)     # glow style keying: every pixel near the background colour is background
    h, w = rgba.shape[:2]
    parts = {}
    for n, name in enumerate(NAMES):
        r, c = divmod(n, 3)
        cell = rgba[r * h // 3:(r + 1) * h // 3, c * w // 3:(c + 1) * w // 3]
        ys, xs = np.nonzero(cell[:, :, 3] > 24)
        crop = cell[ys.min():ys.max() + 1, xs.min():xs.max() + 1].copy()
        im = Image.fromarray(crop)
        im.save(os.path.join(outdir, name + ".png"))
        parts[name] = im
    return parts


def uniform(im, width=None, height=None):
    """Uniform resize to the given width or height (never both)."""
    if width is not None:
        s = width / im.width
    else:
        s = height / im.height
    return im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)


def paste(canvas, im, x, y):
    canvas.alpha_composite(im, (int(round(x)), int(round(y))))


def main():
    sheet, region = sys.argv[1], int(sys.argv[2])
    widths = [float(x) for x in sys.argv[3:]] or [2.0, 4.0]
    regionDir = glob.glob(os.path.join(pa.ROOT, "Assets", "Art", "Regions", "region_00%d_*" % region))[0]
    outdir = os.path.join(regionDir, "tiles", "reg00%d_widths" % region)
    setPath = os.path.join(outdir, "platform_set.json")
    data = json.load(open(setPath, encoding="utf-8"))
    pieces = [p for p in data["pieces"] if p.get("kind", "normal") == "normal"]
    parts = cut_parts(sheet, os.path.join(os.path.dirname(os.path.abspath(sheet)), "parts"))
    for f in glob.glob(os.path.join(outdir, "reg00%d_[cmb]*.png" % region)):
        os.remove(f)

    PADX, PADB = 96, 150
    for kind in ("crumble", "moving", "bounce"):
        for w in widths:
            base = [p for p in pieces if abs(p["width"] - w) < 0.01][0]
            slab = Image.open(os.path.join(outdir, base["file"])).convert("RGBA")
            bl, br, sy, bb = base["bodyLeft"], base["bodyRight"], base["surfaceY"], base["bodyBottom"]
            bodyW, t = br - bl, bb - sy
            canvas = Image.new("RGBA", (slab.width + 2 * PADX, slab.height + PADB), (0, 0, 0, 0))
            canvas.alpha_composite(slab, (PADX, 0))
            L, R, SY, BB = bl + PADX, br + PADX, sy, bb
            if kind == "crumble":
                crack = uniform(parts["crack_long" if w >= 3 else "crack_short"], height=0.62 * t)
                paste(canvas, crack, L + bodyW * 0.5 - crack.width / 2, SY + 0.42 * t - crack.height / 2)
                chips = uniform(parts["chips"], width=min(110, max(50, bodyW * 0.22)))
                paste(canvas, chips, L + bodyW * 0.55, BB - 0.2 * t)
            elif kind == "moving":
                knob = uniform(parts["knob_a"], height=0.9 * t)
                paste(canvas, knob.transpose(Image.FLIP_LEFT_RIGHT), L - knob.width * 0.62, SY + 0.5 * t - knob.height / 2 + 0.1 * t)
                paste(canvas, knob, R - knob.width * 0.38, SY + 0.5 * t - knob.height / 2 + 0.1 * t)
            else:
                spring = uniform(parts["spring_tall"], height=1.0 * t)
                paste(canvas, spring, L + bodyW / 2 - spring.width / 2, BB - 0.35 * t)
            name = "reg00%d_%s_w%s.png" % (region, kind, ("%g" % w).replace(".", "_"))
            canvas.save(os.path.join(outdir, name))
            ent = {"w": canvas.width, "h": canvas.height, "bodyLeft": L, "bodyRight": R, "surfaceY": SY, "bodyBottom": BB,
                   "width": w, "file": name, "ppu": round(bodyW / w, 3), "kind": kind}
            pieces.append(ent)
            print("%-26s %dx%d  body %d px at ppu %.1f" % (name, canvas.width, canvas.height, bodyW, ent["ppu"]))
    data["pieces"] = pieces
    with open(setPath, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
