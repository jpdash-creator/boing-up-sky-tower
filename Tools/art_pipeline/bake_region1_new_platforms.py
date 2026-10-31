"""Bakes the seven region 1 special platforms (doc 24) from the owner-chosen OpenArt sheets in Art/_candidates/reg001_new_platforms/.

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_region1_new_platforms.py [kind ...]      (default: all kinds)

Per kind: the painted rows are separated (background keyed by process_art.key_image), ONE uniform scale per kind makes the slab thickness
(walking surface to bottom) equal to the normal platforms (0.5 u at 167 px per u, same as reg001_widths), then every game width is cut / repeated
in the middle exactly as slice_width_sheet.py does (nothing is ever stretched). Output per kind:
  Assets/Art/Regions/region_001_meadow_springs/tiles/<kind>_widths/reg001_<kind>_w<width>.png + platform_set.json
Thorn Bush: the thorny half is exactly half of every width (both halves are cut / repeated on their own), baked LEFT-thorny (dir -1) and
mirrored RIGHT-thorny (dir +1). Seesaw Leaf: the pivot knob is cut off the leaf and saved as its own sprite (the leaf tilts, the knob does not).
Also writes Art/_candidates/reg001_new_platforms/baked_contact.png (all pieces at game size).
"""
import glob
import json
import os
import sys

sys.path.insert(0, "E:/pylibs")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402

import process_art as pa  # noqa: E402
import slice_width_sheet as sws  # noqa: E402

ROOT = pa.ROOT
CAND = os.path.join(ROOT, "Art", "_candidates", "reg001_new_platforms")
OUT = os.path.join(ROOT, "Assets", "Art", "Regions", "region_001_meadow_springs", "tiles")
PPU = 167.0                     # pixels per world unit of every baked piece (same as reg001_widths)
PADPX = 6
THICK_PX = 0.5 * PPU            # walking surface to bottom: 0.5 u like the normal platforms
SURF_FRAC = 0.10                # surface sits this share of the full height below the top (feet sink into grass / fluff slightly)

# final-domain constants of the cut / repeat code (slice_width_sheet uses 2x these before its 0.5 shrink)
CAP, FADE, WIN = 55, 32, 14
sws.CAP, sws.FADE, sws.WIN = CAP, FADE, WIN

# kind -> sheet, merge gap, row picks (index in y-sorted objects) and the widths to bake
KINDS = {
    "hivelift":  dict(sheet="A2", merge=30, rows=[2], widths=[2, 3, 4]),
    "dandelion": dict(sheet="B2", merge=30, rows=[0, 2], widths=[2, 3, 4]),
    "thornbush": dict(sheet="B2", merge=30, rows=[3, 4], widths=[4, 6, 8]),
    "ladybug":   dict(sheet="C2", merge=30, rows=[0], widths=[2, 3, 4]),
    "seesaw":    dict(sheet="C2", merge=30, rows=[3, 4], widths=[3, 4, 6]),
    "windpetal": dict(sheet="D1", merge=30, rows=[0, 1], widths=[3, 4, 6, 8]),
    "snapdaisy": dict(sheet="D2", merge=8, rows=[-2, -1], widths=[2, 3, 4, 6, 8], fade=12, win=28),
}
# columns (share of the crop width) and percentile used to find the top and bottom of the slab
MEASURE = {
    "hivelift":  dict(cols=[(0.15, 0.85)], top=50, bot=50),
    "dandelion": dict(cols=[(0.1, 0.9)], top=15, bot=50),
    "thornbush": dict(cols=[(0.72, 0.95)], top=50, bot=50),
    "ladybug":   dict(cols=[(0.3, 0.95)], top=50, bot=50),
    "seesaw":    dict(cols=[(0.2, 0.42), (0.58, 0.85)], top=50, bot=50),
    "windpetal": dict(cols=[(0.42, 0.58)], top=50, bot=50),
    "snapdaisy": dict(cols=[(0.1, 0.9)], top=15, bot=50),
}


def load_rows(cfg):
    rgb = np.asarray(Image.open(os.path.join(CAND, cfg["sheet"] + ".png")).convert("RGB"))
    rgba, _ = pa.key_image(rgb, False)
    found = pa.split_sheet(rgba, cfg["merge"])
    found.sort(key=lambda f: f[0][1])
    return [found[i][1] for i in cfg["rows"]]


def keep_main(a):
    """Drops loose parts (a flying seed next to the dandelion row): keeps the largest blob, grown by 14 px."""
    from scipy import ndimage as ndi
    op = a[:, :, 3] > 24
    lab, n = ndi.label(ndi.binary_dilation(op, iterations=14))
    if n < 2:
        return a
    sizes = ndi.sum(op, lab, range(1, n + 1))
    keep = lab == (1 + int(np.argmax(sizes)))
    out = a.copy()
    out[:, :, 3] = np.where(keep, out[:, :, 3], 0)
    ys, xs = np.nonzero(out[:, :, 3] > 24)
    return out[ys.min():ys.max() + 1, xs.min():xs.max() + 1].copy()


def col_extent(a, c0, c1, top_pct, bot_pct):
    """Top and bottom row of the opaque pixels, per-column percentile over the columns [c0, c1)."""
    op = a[:, c0:c1, 3] > 128
    tops, bots = [], []
    for x in range(op.shape[1]):
        ys = np.nonzero(op[:, x])[0]
        if len(ys):
            tops.append(ys.min())
            bots.append(ys.max())
    return float(np.percentile(tops, top_pct)), float(np.percentile(bots, bot_pct))


def measure_row(a, spec):
    w = a.shape[1]
    ts, bs = [], []
    for f0, f1 in spec["cols"]:
        t, b = col_extent(a, int(f0 * w), int(f1 * w), spec["top"], spec["bot"])
        ts.append(t)
        bs.append(b)
    return min(ts), max(bs)


def resample(a, f):
    im = Image.fromarray(a)
    return np.asarray(im.resize((max(1, round(im.width * f)), max(1, round(im.height * f))), Image.LANCZOS))


def body_extent(a, surf, bot):
    """Columns of the slab: share of the rows between surface and bottom that are opaque is at least 30 percent."""
    band = a[int(surf + 0.25 * (bot - surf)):int(bot - 0.1 * (bot - surf)), :, 3] > 128
    cov = band.mean(axis=0)
    cols = np.nonzero(cov >= 0.3)[0]
    return int(cols.min()), int(cols.max())


# ---------------------------------------------------------------- cut / repeat inside a column window [lo, hi)

def cost(a, p, q):
    x = a[:, p:p + WIN].astype(np.float32)
    y = a[:, q:q + WIN].astype(np.float32)
    return float(np.abs(x - y).mean()) + (sws.pink(a, p, p + FADE) + sws.pink(a, q, q + FADE)) * 0.5


def cut_window(a, remove, lo, hi):
    plo, phi = lo, hi - remove - FADE - WIN
    if phi <= plo:
        raise ValueError("window too small to cut %d px (lo %d hi %d)" % (remove, lo, hi))
    best = min(range(plo, phi), key=lambda p: cost(a, p, p + remove))
    mid = sws.blend(a[:, best:best + FADE], a[:, best + remove:best + remove + FADE])
    return np.concatenate([a[:, :best], mid, a[:, best + remove + FADE:]], axis=1)


def repeat_window(a, add, lo, hi):
    while add > 0:
        chunk = min(add, max(70, (hi - lo) // 2))
        plo, phi = lo + FADE, hi - chunk - WIN
        if phi <= plo:
            raise ValueError("window too small to repeat %d px" % chunk)
        best = min(range(plo, phi), key=lambda p: cost(a, p, p + chunk))
        q = best + chunk
        first = a[:, :q].copy()
        first[:, q - FADE:q] = sws.blend(a[:, q - FADE:q], a[:, best - FADE:best])
        a = np.concatenate([first, a[:, best:]], axis=1)
        add -= chunk
        hi += chunk
    return a


def fit_window(a, delta, lo, hi):
    """Change the width by delta px inside [lo, hi). Returns the new array and the new hi."""
    while delta < 0:
        room = (hi - lo) - FADE - WIN - 4
        chunk = min(-delta, max(40, room // 2))
        a = cut_window(a, chunk, lo, hi)
        hi -= chunk
        delta += chunk
    if delta > 0:
        a = repeat_window(a, delta, lo, hi)
        hi += delta
    return a, hi


# ---------------------------------------------------------------- per kind preparation (source px, before the uniform scale)

def prep_seesaw(a):
    """Cuts the pivot knob off the leaf. Returns (leaf, knob, knobTopRowInLeafRows, knobCentreX)."""
    op = a[:, :, 3] > 128
    h, w = op.shape
    lower = np.array([np.nonzero(op[:, x])[0].max() if op[:, x].any() else -1 for x in range(w)])
    med = int(np.median(lower[lower >= 0][int(0.2 * w):int(0.4 * w)]))
    kcols = np.nonzero(lower > med + 22)[0]
    kcols = kcols[(kcols > 0.35 * w) & (kcols < 0.65 * w)]
    kl, kr = int(kcols.min()) - 45, int(kcols.max()) + 45
    xs = np.arange(w)
    ok = (xs < kl - 30) | (xs > kr + 30)
    ok &= lower >= 0
    ok &= (xs > 0.12 * w) & (xs < 0.9 * w)
    curve = np.interp(xs, xs[ok], lower[ok])           # leaf underside (including the pale outline) as if the knob were not there
    halo = 14                                           # px of pale outline below the black underside line
    cut = (curve - halo).astype(int)
    leaf = a.copy()
    knob = np.zeros_like(a)
    L = (kr - kl) + 24                                  # clean leaf of the same width just left of the knob: its lower rows replace the knob's
    for x in range(kl, kr + 1):
        y0 = cut[x]
        knob[y0:, x] = a[y0:, x]
        sx = x - L
        shift = int(curve[sx] - curve[x])
        top = y0 - 24
        for yy in range(top, h):
            sy = yy + shift
            leaf[yy, x] = a[sy, sx] if 0 <= sy < h else 0
    # the pale outline strip along the knob's top belongs to the leaf, not to the knob
    for x in range(kl, kr + 1):
        for yy in range(cut[x], min(h, cut[x] + 20)):
            r_, g_, b_ = [int(v) for v in knob[yy, x, :3]]
            if knob[yy, x, 3] and g_ > 170 and g_ >= r_ and b_ > 140:
                knob[yy, x, 3] = 0
    ys, xs2 = np.nonzero(knob[:, :, 3] > 24)
    ky0, ky1, kx0, kx1 = ys.min(), ys.max() + 1, xs2.min(), xs2.max() + 1
    return leaf, knob[ky0:ky1, kx0:kx1].copy(), int(np.median(cut[kl:kr + 1])), (kx0 + kx1) / 2.0, ky0


def prep_snap(a):
    """Deskews the tilted daisy row (per-column vertical shift, no scaling) and cuts the hanging dirt off below the green lip."""
    r, g, b, al = [a[:, :, i].astype(int) for i in range(4)]
    lime = (al > 200) & (g > 150) & (g - r > 40) & (g - b > 70)
    h, w = lime.shape
    xs = np.arange(w)
    lip = np.array([np.nonzero(lime[:, x])[0].max() if lime[:, x].sum() > 3 else -1 for x in xs])
    good = (lip >= 0) & (xs > 0.1 * w) & (xs < 0.88 * w)
    coef = np.polyfit(xs[good], lip[good], 1)
    pad = 40
    out = np.zeros((h + 2 * pad, w, 4), np.uint8)
    xc = w / 2.0
    for x in xs:
        s = int(round(coef[0] * (x - xc)))
        y0 = pad - s
        lo, hi = max(0, -y0), min(h, h + 2 * pad - y0)
        out[y0 + lo:y0 + hi, x] = a[lo:hi, x]
    line = int(round(np.polyval(coef, xc))) + pad
    out[line + 14:] = 0
    ys, xs2 = np.nonzero(out[:, :, 3] > 24)
    out = out[ys.min():ys.max() + 1, xs2.min():xs2.max() + 1].copy()
    return out


def prep_thorn(a):
    """Column where the thorny half ends and the smooth half begins (found from the dark spike pixels above the smooth top)."""
    al = a[:, :, 3] > 128
    h, w = al.shape
    sm0, _ = col_extent(a, int(0.72 * w), int(0.95 * w), 50, 50)
    above = a[:int(sm0) - 6, :, :]
    dark = (above[:, :, 3] > 128) & (above[:, :, :3].max(axis=2) < 90)
    cols = np.nonzero(dark.sum(axis=0) > 0)[0]
    return int(cols.max())


# ---------------------------------------------------------------- bake one kind

def make_guard(kind):
    """Cut / blend windows must not run through painted features that would ghost: ladybug spots."""
    if kind == "thornbush":
        def tguard(a, x0, x1):
            sub = a[:int(0.55 * a.shape[0]), max(0, x0):x1]
            return int(((sub[:, :, 3] > 128) & (sub[:, :, :3].max(axis=2) < 90)).sum()) * 4
        return tguard
    if kind != "ladybug":
        return lambda a, x0, x1: 0
    def guard(a, x0, x1):
        sub = a[:, max(0, x0):x1]
        dark = (sub[:, :, 3] > 200) & (sub[:, :, :3].max(axis=2) < 80)
        # only the inside of the bar counts (the outlines at the top and bottom are dark everywhere): rows between 22 and 78 percent
        n = dark.shape[0]
        return int(dark[int(0.22 * n):int(0.78 * n)].sum()) * 40
    return guard


def bake_kind(kind, cfg):
    rows = load_rows(cfg)
    spec = MEASURE[kind]
    extra = {}
    prepared = []
    for a in rows:
        if kind == "seesaw":
            leaf, knob, kcut, kcx, ky0 = prep_seesaw(a)
            extra = dict(knob=knob, kcut=ky0)
            a = leaf
        elif kind == "snapdaisy":
            a = prep_snap(a)
        elif kind == "dandelion":
            a = keep_main(a)
        prepared.append(a)
    # one scale per kind, from the average row (every row of a kind shares it so natural lengths stay comparable)
    ts = [measure_row(a, spec) for a in prepared]
    T = float(np.mean([b - t for t, b in ts]))
    f = THICK_PX / ((1.0 - SURF_FRAC) * T)
    print("%s: slab height %.0f px -> uniform scale %.3f" % (kind, T, f))
    outdir = os.path.join(OUT, kind + "_widths")
    os.makedirs(outdir, exist_ok=True)
    for old in glob.glob(os.path.join(outdir, "*.png")) + glob.glob(os.path.join(outdir, "platform_set.json")):
        try:
            os.remove(old)
        except PermissionError:
            pass
    sws.pink = make_guard(kind)
    global FADE, WIN
    FADE, WIN = cfg.get("fade", 32), cfg.get("win", 14)
    sws.FADE, sws.WIN = FADE, WIN

    scaled = []
    for a, (t, b) in zip(prepared, ts):
        ra = resample(a, f)
        t2, b2 = t * f, b * f
        surf = int(round(t2 + SURF_FRAC * (b2 - t2)))
        bot = int(round(b2))
        bl, br = body_extent(ra, surf, bot)
        info = dict(a=ra, surf=surf, bot=bot, bl=bl, br=br, natural=(br - bl) / PPU)
        if kind == "thornbush":
            info["split"] = int(round(prep_thorn(a) * f))
        scaled.append(info)
        print("   row: body %d px = %.2f u, thickness %.1f px" % (br - bl, info["natural"], bot - surf))

    pieces = []
    variants = [("", 0)] if kind != "thornbush" else [("left_", -1), ("right_", 1)]
    for w in cfg["widths"]:
        src = min(scaled, key=lambda r: abs(r["natural"] - w))
        a = src["a"]
        bl, br = src["bl"], src["br"]
        margin = a.shape[1] - (br - bl)
        want = int(round(w * PPU)) + margin
        if kind == "thornbush":
            sp = src["split"]
            half = int(round(w * PPU / 2))
            # thorny half: from the left body edge to the transition; smooth half: the rest
            thorny_have, smooth_have = sp - bl, br - sp
            a1, hi1 = fit_window(a, (half - thorny_have), CAP, sp - CAP // 2)
            sp1 = sp + (half - thorny_have)
            a2, _ = fit_window(a1, (half - smooth_have), sp1 + CAP // 2, a1.shape[1] - margin // 2 - CAP)
            a = a2
        else:
            delta = want - a.shape[1]
            lo = bl + CAP
            hi = br - CAP
            if kind == "ladybug":
                lo = bl + CAP + 20
            a, _ = fit_window(a, delta, lo, hi)
        for tag, d in variants:
            img = a if d <= 0 else a[:, ::-1].copy()
            img = np.pad(img, ((PADPX, PADPX), (PADPX, PADPX), (0, 0)))   # transparent border: no opaque pixel on the image edge (QA clipped)
            name = "reg001_%s_%sw%s.png" % (kind, tag, ("%g" % w).replace(".", "_"))
            Image.fromarray(np.ascontiguousarray(img)).save(os.path.join(outdir, name))
            m = measure_piece(np.ascontiguousarray(img), src["surf"] + PADPX, src["bot"] + PADPX)
            m.update({"width": float(w), "file": name, "ppu": round((m["bodyRight"] - m["bodyLeft"]) / w, 3), "kind": kind, "dir": d})
            pieces.append(m)
            print("   %-34s body %4d px = %.2f u (want %g)  surface %d bottom %d  thickness %.2f u" % (
                name, m["bodyRight"] - m["bodyLeft"], (m["bodyRight"] - m["bodyLeft"]) / PPU, w, m["surfaceY"], m["bodyBottom"],
                (m["bodyBottom"] - m["surfaceY"]) / PPU))
    doc = {"asset": kind + "_widths", "kind": kind, "ppuBake": PPU, "pieces": pieces}
    if kind == "seesaw":
        knob = np.pad(resample(extra["knob"], f), ((PADPX, PADPX), (PADPX, PADPX), (0, 0)))
        kname = "reg001_seesaw_knob.png"
        Image.fromarray(knob).save(os.path.join(outdir, kname))
        # world offset from the walking surface to the top of the knob, in pixels of the baked pieces (all seesaw pieces share surface and scale)
        drop = int(round(extra["kcut"] * f)) - scaled[0]["surf"] - PADPX   # knob sprite top edge (without its pad) below the surface
        doc["knob"] = {"file": kname, "w": int(knob.shape[1]), "h": int(knob.shape[0]), "topDropPx": drop, "ppu": PPU}
        print("   knob %dx%d px, top %d px below the walking surface" % (knob.shape[1], knob.shape[0], drop))
    with open(os.path.join(outdir, "platform_set.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(doc, fh, indent=2)
        fh.write("\n")
    return doc, outdir


def measure_piece(a, surf, bot):
    bl, br = body_extent(a, surf, bot)
    return {"w": int(a.shape[1]), "h": int(a.shape[0]), "bodyLeft": bl, "bodyRight": br, "surfaceY": int(surf), "bodyBottom": int(bot)}


def contact(docs):
    """All baked pieces at game size (120 px per unit) on a sky colour, labelled."""
    scale = 120.0 / PPU
    rowsimg = []
    for kind, (doc, outdir) in docs.items():
        for piece in doc["pieces"]:
            im = Image.open(os.path.join(outdir, piece["file"])).convert("RGBA")
            im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)
            rowsimg.append(("%s  %s u%s" % (kind, ("%g" % piece["width"]), (" dir %+d" % piece["dir"]) if kind == "thornbush" else ""), im))
        if "knob" in doc:
            im = Image.open(os.path.join(outdir, doc["knob"]["file"])).convert("RGBA")
            rowsimg.append(("seesaw knob", im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)))
    cols = 2
    cellw = max(i.width for _, i in rowsimg) + 20
    cellh = max(i.height for _, i in rowsimg) + 40
    nrows = (len(rowsimg) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cellw, nrows * cellh), (150, 205, 245, 255))
    d = ImageDraw.Draw(sheet)
    for n, (label, im) in enumerate(rowsimg):
        cx, cy = (n % cols) * cellw, (n // cols) * cellh
        d.text((cx + 8, cy + 4), label, fill=(20, 30, 60, 255))
        # a 1 unit ruler under the label, so the scale is obvious
        sheet.alpha_composite(im, (cx + 10, cy + 24))
    sheet.convert("RGB").save(os.path.join(CAND, "baked_contact.png"))


KEYS = {"A2": (101, 107, 119), "B2": (107, 109, 116), "C2": (98, 105, 115), "D1": (97, 104, 116), "D2": (108, 112, 124)}


def run_qa(docs):
    """Art QA of every baked piece with qa_art.analyse (clipped / halo / tiny / extreme); the key colour is the sheet's own background."""
    import qa_art
    bad = 0
    total = 0
    for kind, (doc, outdir) in docs.items():
        key = KEYS[KINDS[kind]["sheet"]]
        files = [p["file"] for p in doc["pieces"]] + ([doc["knob"]["file"]] if "knob" in doc else [])
        for f in files:
            _, issues, _ = qa_art.analyse(os.path.join(outdir, f), key)
            total += 1
            if issues:
                print("QA %-34s %s" % (f, ", ".join(issues)))
                bad += 1
    print("QA: %d pieces checked, %d with findings" % (total, bad))


def main():
    want = [a for a in sys.argv[1:]] or list(KINDS)
    docs = {}
    for kind in KINDS:
        if kind not in want:
            doc_path = os.path.join(OUT, kind + "_widths", "platform_set.json")
            if os.path.exists(doc_path):
                docs[kind] = (json.load(open(doc_path)), os.path.dirname(doc_path))
            continue
        docs[kind] = bake_kind(kind, KINDS[kind])
    contact(docs)
    run_qa(docs)


if __name__ == "__main__":
    main()
