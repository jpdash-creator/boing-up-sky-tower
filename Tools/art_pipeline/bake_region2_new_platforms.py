"""Bakes the seven region 2 (Mushroom Grove) special platforms from the owner-picked OpenArt sheets in Art/_candidates/reg002_new_platforms/v2/.

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_region2_new_platforms.py [kind ...]      (default: all kinds)

Same method as bake_region1_new_platforms.py (its helpers are imported): rows are separated after the background is keyed, ONE uniform scale per kind
makes the slab thickness (walking surface to bottom) 0.5 u at 167 px per u, every game width is cut / repeated in the middle (nothing is stretched).
Extra cleaning for these sheets: the grey drop shadow under every slab, the number labels at the left margin, the divider lines and loose sparkles are removed.
Output per kind (Assets/Art/Regions/region_002_mushroom_grove/tiles/<kind>_widths/):
  reg002_<kind>_w<width>.png + platform_set.json (kind = PlatformKind name, like region 1).
Kinds: squishcap, sproutlift (+ stem sprite), pufffloat, fairyring (hole kept: only the two halves are cut, hole HOLE_U wide), orbitcap,
dizzyspores, sporepop (one piece per pose: dir -1 calm, +1 swollen, +2 flat; the json also carries "pose").
Also writes Art/_candidates/reg002_new_platforms/baked_contact.png.
"""
import glob
import json
import os
import sys

sys.path.insert(0, "E:/pylibs")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402
from scipy import ndimage as ndi  # noqa: E402

import process_art as pa  # noqa: E402
import slice_width_sheet as sws  # noqa: E402
import bake_region1_new_platforms as b1  # noqa: E402

ROOT = pa.ROOT
CAND = os.path.join(ROOT, "Art", "_candidates", "reg002_new_platforms")
SRC = os.path.join(CAND, "v2")
OUT = os.path.join(ROOT, "Assets", "Art", "Regions", "region_002_mushroom_grove", "tiles")
PPU = b1.PPU
PADPX = b1.PADPX
THICK_PX = b1.THICK_PX
SURF_FRAC = b1.SURF_FRAC
HOLE_U = 1.4                     # Fairy Ring: painted hole width (the Sim draws the half width in 0.6..0.8 u, mean 0.7)

# kind -> sheet, row picks (index in y-sorted row groups) and widths to bake
KINDS = {
    "squishcap":   dict(sheet="A1", rows=[1], widths=[1.5, 2, 3, 4, 6, 8]),
    "sproutlift":  dict(sheet="A1", rows=[2], widths=[1.5, 2, 3, 4, 6]),
    "pufffloat":   dict(sheet="A1", rows=[4, 5], widths=[1.5, 2, 3, 4, 6, 8]),
    "fairyring":   dict(sheet="B2", rows=[0, 1, 2], widths=[4.4, 6, 8]),
    "orbitcap":    dict(sheet="B2", rows=[4, 5], widths=[2, 3, 4]),
    "dizzyspores": dict(sheet="C2", rows=[0, 1, 2], widths=[1.5, 2, 3, 4, 6, 8]),
    "sporepop":    dict(sheet="C2", rows=[3, 4, 5], widths=[2, 3, 4, 6]),
}
PER_ROW_SCALE = ('dizzyspores',)   # dome shaped rows differ in thickness: each row gets its own scale so every piece is 0.5 u thick
POSES = [("calm", -1), ("swollen", 1), ("flat", 2)]


# ---------------------------------------------------------------- sheet cleaning

def clean_sheet(name):
    """Key the grey background, then remove drop shadows, number labels, divider lines and tiny loose marks. Returns RGBA uint8."""
    rgb = np.asarray(Image.open(os.path.join(SRC, name + ".png")).convert("RGB"))
    rgba, bg = pa.key_image(rgb, False)
    f = rgb.astype(np.float32)
    ratio = f / bg[None, None, :]
    k = ratio.mean(axis=2)
    spread = ratio.max(axis=2) - ratio.min(axis=2)
    shadowish = (spread < 0.12) & (k > 0.45) & (k < 1.08)
    clear = rgba[:, :, 3] == 0
    cand = shadowish | clear
    lab, _ = ndi.label(cand)
    edge_labs = np.unique(lab[clear])
    edge_labs = edge_labs[edge_labs != 0]
    gone = np.isin(lab, edge_labs) & ~clear
    gone = ndi.binary_dilation(gone, iterations=2) & ~ndi.binary_erosion(~clear, iterations=0)
    # only remove dilated pixels that are not strongly coloured art (keeps the cyan rim glow of the slabs)
    strong = spread > 0.45
    gone &= ~(strong & ~(shadowish))
    rgba[gone, 3] = 0
    # background seen through the orbit rings / fairy ring: enclosed pixels that are exactly the key colour (or its shadow) become transparent too
    enclosed = (spread < 0.06) & (k > 0.9) & (k < 1.08) & (rgba[:, :, 3] > 0)
    labe, ne = ndi.label(enclosed)
    sizes = ndi.sum(enclosed, labe, range(1, ne + 1))
    for i, sz in enumerate(sizes, start=1):
        if sz > 150:
            rgba[labe == i, 3] = 0
    # components: labels (digits at the left margin), divider lines, specks
    op = rgba[:, :, 3] > 128
    lab2, n = ndi.label(op, structure=np.ones((3, 3)))
    objs = ndi.find_objects(lab2)
    for i, sl in enumerate(objs, start=1):
        h, w = sl[0].stop - sl[0].start, sl[1].stop - sl[1].start
        area = int((lab2[sl] == i).sum())
        kill = False
        if sl[1].stop < 150 and area < 8000:
            kill = True                       # number label at the left margin
        if h < 14 and w > 600:
            kill = True                       # divider line
        if area < 40:
            kill = True
        if kill:
            rgba[sl][lab2[sl] == i] = 0
    return rgba


def split_rows(rgba, core_area=20000, reach=90):
    """Row separation: big components are the slab cores (halves of one Fairy Ring row share a row by vertical overlap); every
    smaller component (stem, sparkle, spore dots, orbit beads) joins the nearest core within `reach` px or is dropped. Rows are
    returned top to bottom as RGBA crops that contain only their own pixels."""
    op = rgba[:, :, 3] > 128
    lab, n = ndi.label(op, structure=np.ones((3, 3)))
    objs = ndi.find_objects(lab)
    areas = ndi.sum(op, lab, range(1, n + 1))
    cores = [i for i in range(1, n + 1) if areas[i - 1] >= core_area]
    groups = []   # list of lists of labels
    for i in sorted(cores, key=lambda c: objs[c - 1][0].start):
        y0, y1 = objs[i - 1][0].start, objs[i - 1][0].stop
        for g in groups:
            gy0 = min(objs[c - 1][0].start for c in g)
            gy1 = max(objs[c - 1][0].stop for c in g)
            ov = min(y1, gy1) - max(y0, gy0)
            if ov > 0.5 * min(y1 - y0, gy1 - gy0):
                g.append(i)
                break
        else:
            groups.append([i])

    def bbox(g):
        return (min(objs[c - 1][1].start for c in g), min(objs[c - 1][0].start for c in g),
                max(objs[c - 1][1].stop for c in g), max(objs[c - 1][0].stop for c in g))

    def dist(b, o):
        sl = objs[o - 1]
        dx = max(b[0] - sl[1].stop, sl[1].start - b[2], 0)
        dy = max(b[1] - sl[0].stop, sl[0].start - b[3], 0)
        return (dx * dx + dy * dy) ** 0.5
    members = [list(g) for g in groups]
    for i in range(1, n + 1):
        if i in cores or areas[i - 1] < 40:
            continue
        ds = [dist(bbox(g), i) for g in groups]
        j = int(np.argmin(ds))
        if ds[j] <= reach:
            members[j].append(i)
    rows = []
    for g, mem in zip(groups, members):
        mask = np.isin(lab, mem)
        ys, xs = np.nonzero(mask)
        y0, y1, x0, x1 = max(0, ys.min() - 8), ys.max() + 9, max(0, xs.min() - 8), xs.max() + 9
        crop = rgba[y0:y1, x0:x1].copy()
        m = mask[y0:y1, x0:x1]
        m = ndi.binary_dilation(m, iterations=4)          # keeps the anti-aliased rim of the kept pixels
        crop[~m] = 0
        rows.append(crop)
    return rows


# columns (share of the crop width) and percentile used to find the top and bottom of the slab
MEASURE = {
    "squishcap":   dict(cols=[(0.2, 0.8)], top=50, bot=50),
    "sproutlift":  dict(cols=[(0.15, 0.45), (0.6, 0.9)], top=50, bot=50),
    "pufffloat":   dict(cols=[(0.2, 0.8)], top=50, bot=50),
    "fairyring":   dict(cols=[(0.08, 0.3), (0.72, 0.92)], top=50, bot=50),
    "orbitcap":    dict(cols=[(0.08, 0.3)], top=50, bot=50),
    "dizzyspores": dict(cols=[(0.3, 0.7)], top=50, bot=50),
    "sporepop":    dict(cols=[(0.3, 0.7)], top=50, bot=50, rows=[0]),   # thickness from the calm pose; the other poses share its scale and base line
}
KEYS = {"A1": (117, 131, 144), "B2": (108, 111, 119), "C2": (103, 112, 119)}
_CACHE = {}


def load_rows(cfg):
    if cfg["sheet"] not in _CACHE:
        _CACHE[cfg["sheet"]] = split_rows(clean_sheet(cfg["sheet"]))
    return [_CACHE[cfg["sheet"]][i].copy() for i in cfg["rows"]]


def trim(a):
    ys, xs = np.nonzero(a[:, :, 3] > 24)
    return a[ys.min():ys.max() + 1, xs.min():xs.max() + 1].copy()


# ---------------------------------------------------------------- per kind preparation (source px, before the uniform scale)

def prep_squish(a, first):
    """Removes glint stars and grey glow blobs that stick out above / left of the slab (first, short row of a kind)."""
    return a      # the long row (no glint star) is used for every width: nothing to clip


def clip_outside(a):
    t, b = b1.col_extent(a, int(0.3 * a.shape[1]), int(0.7 * a.shape[1]), 50, 50)
    op = a[:, :, 3] > 128
    cov = op[int(t) + 8:int(b) - 8].mean(axis=0)
    left = int(np.nonzero(cov >= 0.6)[0].min())
    left = max(0, left - 6)                                  # the rounded cap / outline sits a few px left of the 60 percent column
    a[:int(t) - 3, :, 3] = 0                                 # above the top edge
    a[:, :max(0, left - 1), 3] = 0                           # left of the slab
    return trim(a)


def prep_sprout(a):
    """Cuts the hanging stem (everything below the slab bottom line) off the row. Returns (slab, stem)."""
    op = a[:, :, 3] > 128
    h, w = op.shape
    bots = np.array([np.nonzero(op[:, x])[0].max() if op[:, x].any() else -1 for x in range(w)])
    good = np.sort(bots[bots >= 0])
    b = int(np.median(good[int(0.1 * len(good)):int(0.9 * len(good))]))
    below = np.zeros_like(op)
    below[b + 1:] = op[b + 1:]
    lab, n = ndi.label(ndi.binary_dilation(below, iterations=3))
    stem = None
    if n:
        sizes = ndi.sum(below, lab, range(1, n + 1))
        keep = (lab == (1 + int(np.argmax(sizes)))) & below
        st = a.copy()
        st[:, :, 3] = np.where(keep, st[:, :, 3], 0)
        stem = trim(st)
    slab = a.copy()
    slab[b + 1:, :, 3] = 0
    g_, r_, b_ = slab[:, :, 1].astype(int), slab[:, :, 0].astype(int), slab[:, :, 2].astype(int)
    nub = (slab[:, :, 3] > 0) & (g_ > r_ + 45) & (g_ > b_ + 15) & (np.arange(h)[:, None] > b - 10)
    slab[nub, 0], slab[nub, 1], slab[nub, 2] = 28, 52, 66     # green stem neck on the underside: recoloured to the slab outline
    return trim(clip_outside(slab) if False else slab), stem


def prep_fairy(a):
    """Ring columns [rl, rr] (the white mushrooms in the bottom half): the halves are left of rl and right of rr."""
    h, w = a.shape[:2]
    rgb = a[:, :, :3].astype(int)
    white = (a[:, :, 3] > 200) & (rgb.min(axis=2) > 200) & ((rgb.max(axis=2) - rgb.min(axis=2)) < 50)
    white[:int(0.45 * h)] = False
    white[:, :int(0.3 * w)] = False
    white[:, int(0.7 * w):] = False
    cols = np.nonzero(white.any(axis=0))[0]
    return int(cols.min()) - 2, int(cols.max()) + 2


def orbit_ring_span(a):
    """Columns of the orbit ring and beads: everything that sticks out above or below the slab (the ring is wider than the slab is thick)."""
    t, b = b1.col_extent(a, int(0.08 * a.shape[1]), int(0.3 * a.shape[1]), 50, 50)
    op = a[:, :, 3] > 128
    out = op.copy()
    out[int(t) + 4:int(b) - 4] = False
    cols = np.nonzero(out.sum(axis=0) > 1)[0]
    cols = cols[cols > 0.1 * a.shape[1]]
    return int(cols.min()) - 6, int(cols.max()) + 6


def make_guard(kind):
    """Cut / blend windows must not run through painted features that would ghost (spots, swirls, ring beads, stems)."""
    def feature(a, x0, x1):
        sub = a[:, max(0, x0):x1]
        al = sub[:, :, 3] > 200
        r, g, b = sub[:, :, 0].astype(int), sub[:, :, 1].astype(int), sub[:, :, 2].astype(int)
        if kind == "squishcap":
            return int((al & (np.minimum(np.minimum(r, g), b) > 215)).sum())
        if kind == "dizzyspores":
            cream = al & (r > 215) & (g > 160) & (b > 120)
            swirl = al & (g > 190) & (b < 160) & (r > 120)
            return int((cream | swirl).sum())
        if kind == "orbitcap":
            return int((al & (b >= g + 6) & (g > 200) & (r < 190)).sum())
        if kind == "pufffloat":
            return int((al & (b > g) & (g > 200) & (r < 190)).sum())     # cyan spore dots
        if kind == "sporepop":
            return int((al & (g > 150) & (r > 200) & (b < 120)).sum())   # yellow sparks
        return 0
    return lambda a, x0, x1: feature(a, x0, x1) * 10


def center_pad(img, bl, br):
    """Transparent padding so the body centre is the image centre (the view puts the sprite centre on the platform centre)."""
    d = (img.shape[1] - 1 - br) - bl
    if d > 0:
        img = np.pad(img, ((0, 0), (d, 0), (0, 0)))
    elif d < 0:
        img = np.pad(img, ((0, 0), (0, -d), (0, 0)))
    return img


# ---------------------------------------------------------------- one width of one row

def fit_piece(kind, src, w):
    a = src["a"]
    bl, br = src["bl"], src["br"]
    want_body = int(round(w * PPU))
    if kind == "fairyring":
        rl, rr = src["ring"]
        hole = int(round(HOLE_U * PPU))
        mid = a[:, rl:rr + 1].copy()
        mr, mg, mb = [mid[:, :, k].astype(int) for k in range(3)]
        lip = (mb > mg + 25) & (mr > mg + 5) & ~((mr > 200) & (mg > 200))     # purple wall of the half
        mid[lip, 3] = 0
        if mid.shape[1] > hole:
            raise ValueError("ring wider than the hole")
        padl = (hole - mid.shape[1]) // 2
        mid = np.pad(mid, ((0, 0), (padl, hole - mid.shape[1] - padl), (0, 0)))
        half = int(round((w - HOLE_U) / 2 * PPU))
        left = a[:, :rl]
        left, _ = b1.fit_window(left, half - (rl - bl), bl + b1.CAP, rl - b1.CAP)
        right = a[:, rr + 1:]
        brr = br - (rr + 1)
        right, _ = b1.fit_window(right, half - brr, b1.CAP, brr - b1.CAP)
        return np.concatenate([left, mid, right], axis=1)
    margin = a.shape[1] - (br - bl)
    delta = want_body + margin - a.shape[1]
    lo, hi = bl + b1.CAP, br - b1.CAP
    if kind == "orbitcap" and False:
        rl, rr = src["ring"]
        left_room = (rl - 8) - lo
        right_room = hi - (rr + 8)
        if left_room >= right_room:
            hi = rl - 8
        else:
            lo = rr + 8
    out, _ = b1.fit_window(a, delta, lo, hi)
    return out


def emit_piece(kind, outdir, img, src, w, tag, extra_meta):
    surf, bot = src["surf"], src["bot"]
    bl, br = b1.body_extent(img, surf, bot)
    img = center_pad(img, bl, br)
    img = np.pad(img, ((PADPX, PADPX), (PADPX, PADPX), (0, 0)))
    img = np.ascontiguousarray(img)
    name = "reg002_%s_%sw%s.png" % (kind, tag, ("%g" % w).replace(".", "_"))
    Image.fromarray(img).save(os.path.join(outdir, name))
    m = b1.measure_piece(img, surf + PADPX, bot + PADPX)
    m.update({"width": float(w), "file": name, "ppu": round((m["bodyRight"] - m["bodyLeft"]) / w, 3), "kind": kind, "dir": 0})
    m.update(extra_meta)
    print("   %-36s body %4d px = %.2f u (want %g)  surface %d bottom %d  thickness %.2f u  size %dx%d" % (
        name, m["bodyRight"] - m["bodyLeft"], (m["bodyRight"] - m["bodyLeft"]) / PPU, w, m["surfaceY"], m["bodyBottom"],
        (m["bodyBottom"] - m["surfaceY"]) / PPU, m["w"], m["h"]))
    return m


# ---------------------------------------------------------------- bake one kind

def bake_kind(kind, cfg):
    rows = load_rows(cfg)
    spec = MEASURE[kind]
    stem = None
    prepared = []
    for ri, a in enumerate(rows):
        if kind == "squishcap":
            a = prep_squish(a, ri == 0)
        elif kind == "sproutlift":
            mx, mn = a[:, :, :3].max(axis=2).astype(int), a[:, :, :3].min(axis=2).astype(int)
            a[(a[:, :, 3] > 0) & (mx - mn < 75) & (mx > 100) & (mx < 200), 3] = 0     # grey-cyan glint glow blob at the top left
            a, st = prep_sprout(a)
            a = clip_outside(a)
            if ri == 0:
                stem = st
        prepared.append(a)
    mrows = spec.get("rows", list(range(len(prepared))))
    ts = [b1.measure_row(a, spec) for a in prepared]
    T = float(np.mean([ts[i][1] - ts[i][0] for i in mrows]))
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
    b1.FADE, b1.WIN = 32, 14
    sws.FADE, sws.WIN = 32, 14

    scaled = []
    for ri, (a, (t, b)) in enumerate(zip(prepared, ts)):
        if ri not in mrows:
            t = b - T                      # this row's own top is not measurable (bulging pose): same thickness from its base line
        fr = f
        if kind in PER_ROW_SCALE:
            fr = THICK_PX / ((1.0 - SURF_FRAC) * (b - t))
        ra = b1.resample(a, fr)
        t2, b2 = t * fr, b * fr
        surf = int(round(t2 + SURF_FRAC * (b2 - t2)))
        bot = int(round(b2))
        bl, br = b1.body_extent(ra, surf, bot)
        info = dict(a=ra, surf=surf, bot=bot, bl=bl, br=br, natural=(br - bl) / PPU)
        if kind == "fairyring":
            rl, rr = prep_fairy(a)
            info["ring"] = (int(round(rl * f)), int(round(rr * f)))
        if kind == "orbitcap":
            rl, rr = orbit_ring_span(a)
            info["ring"] = (int(round(rl * f)), int(round(rr * f)))
        scaled.append(info)
        print("   row: body %d px = %.2f u, thickness %.1f px%s" % (br - bl, info["natural"], bot - surf, ("  ring %s" % (info["ring"],)) if "ring" in info else ""))

    pieces = []
    if kind == "sporepop":
        jobs = [(info, w, pose, d) for (pose, d), info in zip(POSES, scaled) for w in cfg["widths"]]
    else:
        jobs = [(None, w, None, 0) for w in cfg["widths"]]
    for info, w, pose, d in jobs:
        order = [info] if info is not None else sorted(scaled, key=lambda r: abs(r["natural"] - w))
        done = False
        for src in order:
            try:
                img = fit_piece(kind, src, w)
            except ValueError as e:
                print("   (width %g from the %.2f u row: %s)" % (w, src["natural"], e))
                continue
            meta = {"dir": d, "pose": pose} if pose else {}
            if not pose:
                meta.pop("pose", None)
            pieces.append(emit_piece(kind, outdir, img, src, w, (pose + "_") if pose else "", meta))
            done = True
            break
        if not done:
            print("   !! width %g%s could not be baked" % (w, (" " + pose) if pose else ""))
    doc = {"asset": kind + "_widths", "kind": kind, "ppuBake": PPU, "pieces": pieces}
    if kind == "fairyring":
        doc["holeWidth"] = HOLE_U
    if stem is not None:
        s0 = scaled[0]
        sp = np.pad(b1.resample(stem, f), ((PADPX, PADPX), (PADPX, PADPX), (0, 0)))
        sname = "reg002_sproutlift_stem.png"
        Image.fromarray(np.ascontiguousarray(sp)).save(os.path.join(outdir, sname))
        drop = s0["bot"] + int(round(1 * f)) - s0["surf"] - PADPX     # top edge of the stem sprite below the walking surface, in baked px
        doc["stem"] = {"file": sname, "w": int(sp.shape[1]), "h": int(sp.shape[0]), "topDropPx": drop, "ppu": PPU}
        print("   stem %dx%d px, top %d px below the walking surface" % (sp.shape[1], sp.shape[0], drop))
    with open(os.path.join(outdir, "platform_set.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(doc, fh, indent=2)
        fh.write("\n")
    return doc, outdir


def contact(docs):
    """All baked pieces at game size (120 px per unit) on a sky colour, labelled."""
    scale = 120.0 / PPU
    rowsimg = []
    for kind, (doc, outdir) in docs.items():
        for piece in doc["pieces"]:
            im = Image.open(os.path.join(outdir, piece["file"])).convert("RGBA")
            im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)
            rowsimg.append(("%s  %s u %s" % (kind, "%g" % piece["width"], piece.get("pose", "")), im))
        if "stem" in doc:
            im = Image.open(os.path.join(outdir, doc["stem"]["file"])).convert("RGBA")
            rowsimg.append(("sprout stem", im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)))
    cols = 3
    cellw = max(i.width for _, i in rowsimg) + 20
    cellh = max(i.height for _, i in rowsimg) + 40
    nrows = (len(rowsimg) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cellw, nrows * cellh), (45, 40, 70, 255))
    d = ImageDraw.Draw(sheet)
    for n, (label, im) in enumerate(rowsimg):
        cx, cy = (n % cols) * cellw, (n // cols) * cellh
        d.text((cx + 8, cy + 4), label, fill=(240, 240, 255, 255))
        sheet.alpha_composite(im, (cx + 10, cy + 24))
    sheet.convert("RGB").save(os.path.join(CAND, "baked_contact.png"))


def run_qa(docs):
    """Art QA of every baked piece with qa_art.analyse (clipped / halo / tiny / extreme); the key colour is the sheet's own background."""
    import qa_art
    bad = 0
    total = 0
    for kind, (doc, outdir) in docs.items():
        key = KEYS[KINDS[kind]["sheet"]]
        files = [p["file"] for p in doc["pieces"]] + ([doc["stem"]["file"]] if "stem" in doc else [])
        for fn in files:
            _, issues, _ = qa_art.analyse(os.path.join(outdir, fn), key)
            total += 1
            if issues:
                print("QA %-36s %s" % (fn, ", ".join(issues)))
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
