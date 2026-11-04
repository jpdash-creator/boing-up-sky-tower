"""Bakes the seven region 4 (Candy Canyon) special platforms from the owner-picked OpenArt sheets A1 / B1 / C1 in Art/_candidates/reg004_new_platforms/.

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_region4_new_platforms.py [kind ...]      (default: all kinds)

Same method as bake_region3_new_platforms.py (its helpers and those of bake_region1_new_platforms.py are reused): grey key background, soft drop shadows, the
white sparkle star of every row (inpainted inside a hand-placed box per row), the row numbers at the left margin of C1 and the divider lines of B1 are removed,
rows are split by their empty bands, ONE uniform scale per kind makes the slab thickness (walking surface to bottom) 0.5 u at 167 px per u, and every game
width is cut / repeated in the middle (nothing is stretched). Output per kind (Assets/Art/Regions/region_004_candy_canyon/tiles/<kind>_widths/):
  reg004_<kind>_[<pose>_]w<width>.png + platform_set.json (kind = lower-case PlatformKind name, dir = pose number, width in u).
Sheets / rows (1 based, top to bottom): A1 1-2 candybowl, 3-4 flossglide, 5-6 fizzvent (bubbles above the slab are part of the piece);
B1 1-2 licoricebelt frames A / B (dir -1 / 1, art looks +1; the view mirrors), 3-6 taffystretch four widths (dir -1,1,2,3 = 2.4, 3.3, 4.3, 5.2 u);
C1 1-4 jellywobble (calm -1 / squashed 1 at 4 u, then at 6 u), 5-6 pinwheelgust frames A / B (dir -1 / 1; the pinwheel stays whole, the base is cut around it).
Also writes Art/_candidates/reg004_new_platforms/baked_contact.png.
"""
import glob
import json
import os
import sys

sys.path.insert(0, "E:/pylibs")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402
from scipy import ndimage as ndi  # noqa: E402
import cv2  # noqa: E402

import process_art as pa  # noqa: E402
import slice_width_sheet as sws  # noqa: E402
import bake_region1_new_platforms as b1  # noqa: E402
import bake_region3_new_platforms as b3  # noqa: E402

ROOT = pa.ROOT
CAND = os.path.join(ROOT, "Art", "_candidates", "reg004_new_platforms")
OUT = os.path.join(ROOT, "Assets", "Art", "Regions", "region_004_candy_canyon", "tiles")
PPU = b1.PPU
PADPX = b1.PADPX
THICK_PX = b1.THICK_PX
KEYS = {}
S = 1.152            # the sheets were reviewed at 1500 px width, the files are 1728 px

# sparkle boxes per sheet: (x0, y0, x1, y1) in the 1500 px review frame
SPARKLES = {
    "A1": [(320, 190, 440, 290), (280, 1390, 480, 1500)],
    "B1": [(200, 110, 360, 260), (200, 370, 360, 520), (200, 730, 360, 880), (200, 1050, 360, 1190), (200, 1350, 360, 1500), (200, 1665, 360, 1810)],
    "C1": [(170, 40, 390, 210), (170, 580, 270, 700), (790, 1350, 900, 1440), (860, 1760, 960, 1850),],
}
# A1 floss stars sit over the fluffy top edge: handled by the window below
SPARKLES["A1"] += [(430, 750, 540, 830), (440, 1000, 560, 1070)]

SURF = dict(candybowl=0.12, flossglide=0.10, fizzvent=0.22, licoricebelt=0.20, taffystretch=0.14, jellywobble=0.10, pinwheelgust=0.10)


# ---------------------------------------------------------------- sheet cleaning

def clean_sheet(name):
    rgb = np.asarray(Image.open(os.path.join(CAND, name + ".png")).convert("RGB"))
    rgba, bg = pa.key_image(rgb, False)
    KEYS[name] = tuple(int(round(v)) for v in bg)
    f = rgb.astype(np.float32)
    ratio = f / bg[None, None, :]
    k = ratio.mean(axis=2)
    spread = ratio.max(axis=2) - ratio.min(axis=2)
    clear = rgba[:, :, 3] == 0
    shadowish = (spread < 0.18) & (k > 0.24) & (k < 1.1)
    lab, _ = ndi.label(shadowish | clear)
    edge_labs = np.unique(lab[clear])
    edge_labs = edge_labs[edge_labs != 0]
    gone = np.isin(lab, edge_labs) & ~clear
    gone = ndi.binary_dilation(gone, iterations=1)
    gone &= ~(spread > 0.30)
    rgba[gone, 3] = 0
    rgba[:, :, 3] = np.where(rgba[:, :, 3] >= 128, 255, 0).astype(np.uint8)
    # sparkles first (they must not bridge the rows or survive as specks)
    rgba = bleed_all(rgba)
    for box in SPARKLES.get(name, []):
        rgba = remove_sparkle(rgba, [int(round(v * S)) for v in box[:4]], clone=name in ("B1",) or box in CLONE)
    op = rgba[:, :, 3] > 128
    lab2, n = ndi.label(op, structure=np.ones((3, 3)))
    for i, sl in enumerate(ndi.find_objects(lab2), start=1):
        h, w = sl[0].stop - sl[0].start, sl[1].stop - sl[1].start
        area = int((lab2[sl] == i).sum())
        if (sl[1].stop < 150 and area < 8000) or (h < 14 and w > 600) or area < 60:      # row numbers, divider lines, specks
            rgba[sl][lab2[sl] == i] = 0
    return rgba


def bleed_all(a):
    return b3.bleed_colour(a)


def disk(r):
    return b3.disk(r)


CLONE = {(430, 750, 540, 830), (440, 1000, 560, 1070), (790, 1350, 900, 1440), (860, 1760, 960, 1850), (320, 190, 440, 290)}


def remove_sparkle(a, box, clone=False):
    """Inpaints the white sparkle inside box (x0, y0, x1, y1): near-white pixels grown by a few px are repainted from their surroundings, spikes that stand in the air
    (thin parts that a small opening cannot hold, and everything outside the closed silhouette) become transparent."""
    h, w = a.shape[:2]
    x0, y0, x1, y1 = box
    x0, y0, x1, y1 = max(0, x0), max(0, y0), min(w, x1), min(h, y1)
    rgb = a[:, :, :3].astype(int)
    al = a[:, :, 3] >= 128
    win = np.zeros((h, w), bool)
    win[y0:y1, x0:x1] = True
    white = al & (rgb.min(axis=2) >= 244) & ((rgb.max(axis=2) - rgb.min(axis=2)) <= 12) & win
    if white.sum() < 20:
        return a
    if clone:
        loose = al & (rgb.min(axis=2) >= 222) & ((rgb.max(axis=2) - rgb.min(axis=2)) <= 26) & win
        star = ndi.binary_dilation(white, iterations=2)
        loose = loose & ndi.binary_dilation(star, iterations=24)         # the glow around the core and the spikes
        thin = win & al & ~ndi.binary_opening(np.pad(al, 6), structure=disk(3))[6:-6, 6:-6]
        thin &= ndi.binary_dilation(star, iterations=40)
        mask = ndi.binary_dilation(star | loose | thin, iterations=6) & win
        ys, xs = np.nonzero(mask)
        out = a.copy()
        for dx in (x1 - x0 + 12, -(x1 - x0 + 12), 2 * (x1 - x0), -2 * (x1 - x0)):
            sx = xs + dx
            ok = (sx >= 0) & (sx < w) & ~mask[ys, np.clip(sx, 0, w - 1)]
            if ok.mean() > 0.8:
                break
        sx = np.clip(xs + dx, 0, w - 1)
        out[ys, xs] = a[ys, sx]
        return out
    star = ndi.binary_dilation(white, iterations=2)
    thin = win & al & ~ndi.binary_opening(np.pad(al, 6), structure=disk(3))[6:-6, 6:-6]
    thin &= ndi.binary_dilation(star, iterations=30)
    mask = ndi.binary_dilation(star | thin, iterations=5) & win
    a2 = a.copy()
    a2[mask, 3] = 0
    R = 90
    ker = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * R + 1, 2 * R + 1))
    padded = np.pad((a2[:, :, 3] >= 128).astype(np.uint8) * 255, R + 2)
    closed = cv2.morphologyEx(padded, cv2.MORPH_CLOSE, ker)[R + 2:-(R + 2), R + 2:-(R + 2)] > 0
    # what stays of the shape without thin spikes, closed over the covered outline: only mask pixels inside it are restored
    solid = ndi.binary_opening(np.pad(a2[:, :, 3] >= 128, 8), structure=disk(5))[8:-8, 8:-8]
    solid_closed = cv2.morphologyEx(np.pad(solid.astype(np.uint8) * 255, R + 2), cv2.MORPH_CLOSE, ker)[R + 2:-(R + 2), R + 2:-(R + 2)] > 0
    keep = mask & solid_closed
    base = b3.bleed_colour(a2, protect=mask)
    painted = cv2.inpaint(np.ascontiguousarray(base[:, :, :3]), mask.astype(np.uint8) * 255, 6, cv2.INPAINT_TELEA)
    out = base.copy()
    out[mask, :3] = painted[mask]
    out[mask, 3] = np.where(keep[mask], 255, 0)
    shape = out[:, :, 3] >= 128
    rim = shape & ~ndi.binary_erosion(shape, iterations=5) & ndi.binary_dilation(mask, iterations=2)
    out[rim & keep, :3] = (24, 24, 28)             # the outline the sparkle covered
    return out


_CACHE = {}


def split_rows(rgba):
    """Rows as connected painted objects (the vent bubbles of one row overlap the next row in y, so empty bands are not enough): objects of at least 15000 px are the rows
    (top to bottom), smaller loose parts (a stray bubble) join the nearest row by bounding box distance, specks are dropped."""
    op = rgba[:, :, 3] > 128
    lab, n = ndi.label(ndi.binary_dilation(op, iterations=2), structure=np.ones((3, 3)))
    objs = ndi.find_objects(lab)
    area = ndi.sum(op, lab, range(1, n + 1))
    mains = [i for i in range(n) if area[i] >= 15000]
    mains.sort(key=lambda i: (objs[i][0].start + objs[i][0].stop) / 2)
    owner = {i: i for i in mains}
    for i in range(n):
        if i in owner or area[i] < 300:
            continue
        sl = objs[i]
        def gap(j):
            sj = objs[j]
            dy = max(0, sj[0].start - sl[0].stop, sl[0].start - sj[0].stop)
            dx = max(0, sj[1].start - sl[1].stop, sl[1].start - sj[1].stop)
            return dx * dx + dy * dy
        j = min(mains, key=gap)
        if gap(j) < 20 * 20:
            owner[i] = j
    rows = []
    for m in mains:
        keep = np.isin(lab, [i + 1 for i, o in owner.items() if o == m]) & op
        a = rgba.copy()
        a[~keep, 3] = 0
        sl = objs[m]
        ys, xs = np.nonzero(keep)
        a = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        rows.append(b3.finish_row(a, star=False))
    return rows


def sheet_rows(name):
    if name not in _CACHE:
        rgba = clean_sheet(name)
        rows = split_rows(rgba)
        print("sheet %s: %d rows %s" % (name, len(rows), [tuple(r.shape[:2]) for r in rows]))
        _CACHE[name] = rows
    return _CACHE[name]


# ---------------------------------------------------------------- kinds

# kind -> sheet, rows (0 based), widths, measuring columns / percentiles (source px, per row)
KINDS = {
    "candybowl":    dict(sheet="A1", rows=[0, 1], widths=[4, 6, 8], cols=[(0.35, 0.65)], top=80),
    "flossglide":   dict(sheet="A1", rows=[2, 3], widths=[2, 3, 4, 6], cols=[(0.4, 0.6)], top=50),
    "fizzvent":     dict(sheet="A1", rows=[4, 5], widths=[2, 3, 4], cols=[(0.08, 0.3)], top=50),
    "licoricebelt": dict(sheet="B1", rows=[0, 1], widths=[4, 6], cols=[(0.3, 0.7)], top=50, poses=[("framea", -1), ("frameb", 1)]),
    "taffystretch": dict(sheet="B1", rows=[2, 3, 4, 5], widths=[2.4, 3.3, 4.3, 5.2], cols=[(0.3, 0.7)], top=50,
                         poses=[("narrow", -1), ("second", 1), ("third", 2), ("wide", 3)]),
    "jellywobble":  dict(sheet="C1", rows=[0, 1, 2, 3], widths=[4, 4, 6, 6], cols=[(0.3, 0.7)], top=50,
                         poses=[("calm", -1), ("squashed", 1), ("calm", -1), ("squashed", 1)]),
    "pinwheelgust": dict(sheet="C1", rows=[4, 5], widths=[2, 3, 4], cols=[(0.05, 0.3)], top=50, poses=[("framea", -1), ("frameb", 1)]),
}


def prepare_outdir(kind):
    outdir = os.path.join(OUT, kind + "_widths")
    os.makedirs(outdir, exist_ok=True)
    for old in glob.glob(os.path.join(outdir, "*.png")) + glob.glob(os.path.join(outdir, "platform_set.json")):
        try:
            os.remove(old)
        except PermissionError:
            pass
    return outdir


def emit(kind, outdir, img, surf, bot, w, tag, extra):
    bl, br = b1.body_extent(img, surf, bot)
    img = b3.center_pad(img, bl, br)
    img = np.ascontiguousarray(np.pad(img, ((PADPX, PADPX), (PADPX, PADPX), (0, 0))))
    name = "reg004_%s_%sw%s.png" % (kind, tag, ("%g" % w).replace(".", "_"))
    Image.fromarray(img).save(os.path.join(outdir, name))
    m = b1.measure_piece(img, surf + PADPX, bot + PADPX)
    m.update({"width": float(w), "file": name, "ppu": round((m["bodyRight"] - m["bodyLeft"]) / w, 3), "kind": kind, "dir": 0})
    m.update(extra)
    print("   %-40s body %4d px = %.2f u (want %g)  thickness %.2f u  size %dx%d" % (
        name, m["bodyRight"] - m["bodyLeft"], (m["bodyRight"] - m["bodyLeft"]) / PPU, w, (m["bodyBottom"] - m["surfaceY"]) / PPU, m["w"], m["h"]))
    return m


def write_doc(kind, outdir, pieces):
    doc = {"asset": kind + "_widths", "kind": kind, "ppuBake": PPU, "pieces": pieces}
    with open(os.path.join(outdir, "platform_set.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(doc, fh, indent=2)
        fh.write("\n")
    return doc


def multi_fit(a, delta, windows):
    """Changes the width by delta px split over several windows [(lo, hi)] in proportion to their size (right to left so the left ones keep their columns)."""
    windows = sorted(windows)
    rooms = [max(0, hi - lo) for lo, hi in windows]
    tot = float(sum(rooms))
    ds, left = [], delta
    for i, r in enumerate(rooms):
        d = left if i == len(rooms) - 1 else int(round(delta * r / tot))
        ds.append(d)
        left -= d
    for (lo, hi), d in reversed(list(zip(windows, ds))):
        if d:
            a, _ = b1.fit_window(a, d, lo, hi)
    return a


def windows_for(kind, a, bl, br, T, side=None):
    """Column windows [(lo, hi)] of a scaled row inside which stretches are cut or repeated; the caps / curls / wisps / rollers / bubbles / pinwheel are outside."""
    if kind == "candybowl":
        m = int(0.9 * T)
        return [(bl + m, br - m)]
    if kind == "flossglide":
        m = int(1.15 * T)
        return [(bl + m, br - m)]
    if kind == "licoricebelt":
        m = int(1.3 * T)
        return [(bl + m, br - m)]
    if kind in ("fizzvent", "pinwheelgust"):
        z0, z1 = feature_zone(kind, a, T)
        m = int(0.45 * T)
        wins = [(bl + m, z0 - 14), (z1 + 14, br - m)]
        return [w for w in wins if w[1] - w[0] > 60]
    if kind == "jellywobble":
        z0, z1 = cherry_zone(a)
        m = int(1.0 * T)
        if z0 < (bl + br) / 2:
            return [(z1 + 40, br - m)]
        return [(bl + m, z0 - 40)]
    return [(bl + 100, br - 100)]


def cherry_zone(a):
    """Columns of the cherry (red and dark red that stand above the slab top)."""
    al = a[:, :, 3] > 128
    tops = np.array([np.nonzero(al[:, x])[0].min() if al[:, x].any() else 10 ** 6 for x in range(a.shape[1])])
    flat = np.median(tops[al.any(axis=0)])
    cols = np.nonzero(tops < flat - 4)[0]
    return int(cols.min()), int(cols.max())


def feature_zone(kind, a, T):
    """Columns that must stay whole: the bubbles plus the vent hole, or the pinwheel above the base."""
    al = a[:, :, 3] > 128
    tops = np.array([np.nonzero(al[:, x])[0].min() if al[:, x].any() else 10 ** 6 for x in range(a.shape[1])])
    cols_alive = np.nonzero(al.any(axis=0))[0]
    base = np.percentile(tops[cols_alive], 80)           # top of the slab (80th percentile of the column tops)
    above = np.nonzero(tops < base - 0.12 * T)[0]
    z0, z1 = int(above.min()), int(above.max())
    if kind == "fizzvent":
        rgb = a[:, :, :3].astype(int)
        dark = al & (rgb.max(axis=2) < 70)
        dark = ndi.binary_erosion(dark, iterations=4)
        lab, n = ndi.label(dark)
        for i, sl in enumerate(ndi.find_objects(lab), start=1):
            if (lab[sl] == i).sum() > 120:
                z0, z1 = min(z0, sl[1].start - 4), max(z1, sl[1].stop + 4)
    return z0, z1


def measure(a, cfg):
    return b1.measure_row(a, dict(cols=cfg["cols"], top=cfg.get("top", 50), bot=50))


# ---------------------------------------------------------------- the bake

def scale_rows(kind, cfg, rows, tref):
    """Uniform scale of the kind: the thickness of the reference rows (surface to bottom) becomes 0.5 u."""
    sf = SURF[kind]
    ms = [measure(a, cfg) for a in rows]
    T = float(np.mean([ms[i][1] - ms[i][0] for i in tref]))
    f = THICK_PX / ((1.0 - sf) * T)
    out = []
    for a, (t, b) in zip(rows, ms):
        ra = b1.resample(a, f)
        T2 = T * f
        b2 = b * f
        t2 = b2 - T2 if False else t * f
        surf = int(round(t2 + sf * (b2 - t2)))
        bot = int(round(b2))
        bl, br = b1.body_extent(ra, surf, bot)
        ra = np.array(ra)
        if kind == "fizzvent":       # a stray bubble of the neighbouring row that sits beside the slab
            lab, n = ndi.label(ra[:, :, 3] > 24, structure=np.ones((3, 3)))
            for i, sl in enumerate(ndi.find_objects(lab), start=1):
                cx = (sl[1].start + sl[1].stop) / 2
                if cx < bl or cx > br:
                    ra[lab == i, 3] = 0
        out.append(dict(a=ra, surf=surf, bot=bot, bl=bl, br=br, T=(b2 - t2), natural=(br - bl) / PPU))
        print("   row: body %d px = %.2f u, thickness %.1f px, image %dx%d" % (br - bl, (br - bl) / PPU, bot - surf, ra.shape[1], ra.shape[0]))
    print("%s: slab height %.0f px -> uniform scale %.3f" % (kind, T, f))
    return out


def stack_pair(A, B):
    """Aligns two frames (left body edge, base line) and stacks them so one cut / repeat treats both identically."""
    def shift(info, dx, dtop):
        a = info["a"]
        if dx > 0:
            a = np.pad(a, ((0, 0), (dx, 0), (0, 0)))
        if dtop > 0:
            a = np.pad(a, ((dtop, 0), (0, 0), (0, 0)))
        return a
    dx = A["bl"] - B["bl"]
    dtopB = A["bot"] - B["bot"]
    a = shift(A, max(0, -dx), max(0, -dtopB))
    b = shift(B, max(0, dx), max(0, dtopB))
    W = max(a.shape[1], b.shape[1])
    H = max(a.shape[0], b.shape[0])
    a = np.pad(a, ((0, H - a.shape[0]), (0, W - a.shape[1]), (0, 0)))
    b = np.pad(b, ((0, H - b.shape[0]), (0, W - b.shape[1]), (0, 0)))
    bl = max(A["bl"], B["bl"])
    bot = max(A["bot"], B["bot"])
    br = max(A["br"] - A["bl"], B["br"] - B["bl"]) + bl
    surf = bot - (A["bot"] - A["surf"])
    return a, b, H, dict(bl=bl, br=br, bot=bot, surf=surf, T=A["T"])


def bake(kind, cfg):
    sheet = sheet_rows(cfg["sheet"])
    rows = [sheet[i].copy() for i in cfg["rows"]]
    outdir = prepare_outdir(kind)
    sws.pink = lambda a, x0, x1: 0
    b1.FADE, b1.WIN = 32, 14
    sws.FADE, sws.WIN = 32, 14
    poses = cfg.get("poses")
    pieces = []
    if kind in ("taffystretch", "jellywobble"):
        tref = [0, 2] if kind == "jellywobble" else list(range(len(rows)))
        info = scale_rows(kind, cfg, rows, tref)
        T = info[tref[0]]["T"]
        thick = THICK_PX * 1.0
        for i, (inf, (pose, d)) in enumerate(zip(info, poses)):
            w = cfg["widths"][i]
            a = inf["a"]
            surf, bot = inf["surf"], inf["bot"]
            if kind == "jellywobble":
                surf = bot - int(round(thick))         # every pose keeps the surface the same height above its base line
                natural = (inf["br"] - inf["bl"]) / PPU
                delta = int(round(w * PPU)) - (inf["br"] - inf["bl"])
                if abs(delta) > 2:
                    wins = windows_for(kind, a, inf["bl"], inf["br"], inf["T"])
                    a = multi_fit(a, delta, wins)
            pieces.append(emit(kind, outdir, a, surf, bot, w, pose + "_", {"dir": d, "pose": pose}))
    elif poses:                   # belt, pinwheel: two frames, cut together
        info = scale_rows(kind, cfg, rows, [0, 1])
        a, b, H, g = stack_pair(info[0], info[1])
        stack = np.concatenate([a, b], axis=0)
        for w in cfg["widths"]:
            delta = int(round(w * PPU)) - (g["br"] - g["bl"])
            ref = np.maximum(a[:, :, 3], b[:, :, 3])
            ra = np.concatenate([a, b], axis=0) if False else a
            wins = windows_for(kind, np.dstack([a[:, :, :3], ref]), g["bl"], g["br"], g["T"])
            if kind == "pinwheelgust":
                z = [feature_zone(kind, x, g["T"]) for x in (a, b)]
                z0, z1 = min(v[0] for v in z), max(v[1] for v in z)
                m = int(0.15 * g["T"])
                wins = [(g["bl"] + m, z0 - 4), (z1 + 4, g["br"] - m)]
            try:
                fit = multi_fit(stack, delta, wins) if delta else stack
            except ValueError as e:
                print("   !! width %g: %s" % (w, e))
                continue
            Hs = fit.shape[0] // 2
            fa, fb = fit[:Hs], fit[Hs:]
            for (pose, d), img in zip(poses, (fa, fb)):
                pieces.append(emit(kind, outdir, trim_rows(img), g["surf"] - trim_top(img), g["bot"] - trim_top(img), w, pose + "_", {"dir": d, "pose": pose}))
    else:                         # candybowl, flossglide, fizzvent: independent rows, nearest row is cut / repeated
        info = scale_rows(kind, cfg, rows, list(range(len(rows))))
        for w in cfg["widths"]:
            order = sorted(info, key=lambda r: abs(r["natural"] - w))
            done = False
            for src in order:
                a, bl, br = src["a"], src["bl"], src["br"]
                delta = int(round(w * PPU)) - (br - bl)
                try:
                    wins = windows_for(kind, a, bl, br, src["T"])
                    img = multi_fit(a, delta, wins) if abs(delta) > 1 else a
                except ValueError as e:
                    print("   (width %g from the %.2f u row: %s)" % (w, src["natural"], e))
                    continue
                pieces.append(emit(kind, outdir, img, src["surf"], src["bot"], w, "", {}))
                done = True
                break
            if not done:
                print("   !! width %g could not be baked" % w)
    return write_doc(kind, outdir, pieces), outdir


def trim_top(img):
    ys = np.nonzero(img[:, :, 3] > 24)[0]
    return int(ys.min()) if len(ys) else 0


def trim_rows(img):
    ys, xs = np.nonzero(img[:, :, 3] > 24)
    return img[ys.min():, max(0, xs.min() - 6):xs.max() + 7].copy() if False else img[ys.min():]


def contact(docs):
    from PIL import ImageDraw
    scale = 120.0 / PPU
    items = []
    for kind, (doc, outdir) in docs.items():
        for piece in doc["pieces"]:
            im = Image.open(os.path.join(outdir, piece["file"])).convert("RGBA")
            im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)
            items.append(("%s %gu %s d%+d" % (kind, piece["width"], piece.get("pose", ""), piece.get("dir", 0)), im))
    cols = 3
    cw = max(i.width for _, i in items) + 20
    ch = max(i.height for _, i in items) + 40
    sheet = Image.new("RGBA", (cols * cw, ((len(items) + cols - 1) // cols) * ch), (150, 205, 245, 255))
    d = ImageDraw.Draw(sheet)
    for n, (label, im) in enumerate(items):
        cx, cy = (n % cols) * cw, (n // cols) * ch
        d.text((cx + 8, cy + 4), label, fill=(20, 20, 60, 255))
        sheet.alpha_composite(im, (cx + 10, cy + 24))
    sheet.convert("RGB").save(os.path.join(CAND, "baked_contact.png"))


def run_qa(docs):
    import qa_art
    bad = total = 0
    for kind, (doc, outdir) in docs.items():
        key = KEYS.get(KINDS[kind]["sheet"])
        for piece in doc["pieces"]:
            _, issues, _ = qa_art.analyse(os.path.join(outdir, piece["file"]), key)
            total += 1
            issues = [i for i in issues if not i.startswith("extreme")]
            if issues:
                print("QA %-40s %s" % (piece["file"], ", ".join(issues)))
                bad += 1
    print("QA: %d pieces checked, %d with findings" % (total, bad))


def main():
    want = sys.argv[1:] or list(KINDS)
    docs = {}
    for kind in KINDS:
        if kind in want:
            docs[kind] = bake(kind, KINDS[kind])
        else:
            p = os.path.join(OUT, kind + "_widths", "platform_set.json")
            if os.path.exists(p):
                docs[kind] = (json.load(open(p)), os.path.dirname(p))
    for k in KINDS:
        sheet_rows(KINDS[k]["sheet"])
    contact(docs)
    run_qa(docs)


if __name__ == "__main__":
    main()
