"""Bakes the seven region 5 (Bamboo Temple) special platforms from the owner-picked OpenArt sheets A1 / B1 / C2 in Art/_candidates/reg005_new_platforms/.

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/bake_region5_new_platforms.py [kind ...]      (default: all kinds)

Same method as bake_region4_new_platforms.py (its helpers are reused): grey key background, drop shadows / sticker halo removed, the white sparkle of every row inpainted
inside a hand-placed box, row numbers at the left margin (B1, C2) and divider lines removed, rows split by their connected objects (C2 with a wider join distance so the
floating mallet head stays with its row), ONE uniform scale per kind makes surface-to-bottom 0.5 u at 167 px per u, and every game width is cut / repeated in a window of
the middle (nothing is stretched; ropes, bow, mallet, post, dust, the lantern caps and the end curls stay whole). Output per kind
(Assets/Art/Regions/region_005_bamboo_temple/tiles/<kind>_widths/): reg005_<kind>_[<pose>_]w<width>.png + platform_set.json (rollinglog also log.png + "log").
Sheets / rows (1 based): A1 1-2 bambooflick rest(-1) / sprung(1), 3-4 lanternlift, 5-6 koistone; B1 1-3 swingplank, 4-6 pagodaridge;
C2 1-4 malletsmash (raised -1 / smashed 1 at 4.5 u then 6 u), 5 rollinglog track, 6 the log alone.
Pairs (Flick, Mallet) are fitted per pose to the same body width with the same window rule. Also writes Art/_candidates/reg005_new_platforms/baked_contact.png.
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

import process_art as pa  # noqa: E402
import slice_width_sheet as sws  # noqa: E402
import bake_region1_new_platforms as b1  # noqa: E402
import bake_region3_new_platforms as b3  # noqa: E402
import bake_region4_new_platforms as b4  # noqa: E402

ROOT = pa.ROOT
CAND = os.path.join(ROOT, "Art", "_candidates", "reg005_new_platforms")
OUT = os.path.join(ROOT, "Assets", "Art", "Regions", "region_005_bamboo_temple", "tiles")
PPU = b1.PPU
PADPX = b1.PADPX
THICK_PX = b1.THICK_PX
KEYS = {}
S = 1.152

# sparkle boxes (x0, y0, x1, y1) in the 1500 px review frame
SPARKLES = {
    "A1": [(150, 95, 270, 235), (265, 440, 345, 610), (200, 735, 300, 870), (215, 990, 305, 1110), (235, 1310, 330, 1440), (255, 1640, 345, 1795)],
    "B1": [(270, 1080, 320, 1120), (390, 1085, 450, 1125), (280, 1330, 340, 1410), (520, 1385, 600, 1430), (300, 1640, 360, 1715), (720, 1660, 800, 1735)],
    "C2": [(225, 220, 295, 305), (235, 485, 305, 575), (215, 995, 295, 1085), (215, 1655, 295, 1730)],
}
CLONE = set()
b4.CLONE = CLONE
b4.SPARKLES = SPARKLES
b4.S = S
SURF = dict(bambooflick=0.12, lanternlift=0.05, koistone=0.30, swingplank=0.10, pagodaridge=0.03, malletsmash=0.10, rollinglog=0.10)
b4.SURF = SURF


# ---------------------------------------------------------------- sheet cleaning

def clean_sheet(name):
    rgb = np.asarray(Image.open(os.path.join(CAND, name + ".png")).convert("RGB"))
    rgba, bg = pa.key_image(rgb, False)
    KEYS[name] = tuple(int(round(v)) for v in bg)
    # grey faces (stone, slab ends, feet) are close to the key colour and leak into the background through hairline gaps of the outline: a face that is only reachable
    # through a thin gap is given back (opening of the key area, then growing it back inside itself)
    dist0 = np.sqrt(((rgb.astype(np.float32) - bg) ** 2).sum(axis=2))
    near = dist0 < pa.KEY_TOL
    near_open = ndi.binary_opening(near, structure=b3.disk(4))
    lab0, _ = ndi.label(near_open)
    edge0 = np.unique(np.concatenate([lab0[0], lab0[-1], lab0[:, 0], lab0[:, -1]]))
    edge0 = edge0[edge0 != 0]
    true_bg = ndi.binary_dilation(np.isin(lab0, edge0), iterations=6) & near
    restore = (rgba[:, :, 3] == 0) & ~true_bg
    if name == "B1":
        restore[:] = False                  # only background inside the rope loops
    elif name == "A1":
        restore[:int(1300 * S)] = False     # the koi stones (grey) need it, the gaps of the bamboo flick are background
    rgba[restore, 3] = 255
    f = rgb.astype(np.float32)
    ratio = f / bg[None, None, :]
    k = ratio.mean(axis=2)
    spread = ratio.max(axis=2) - ratio.min(axis=2)
    clear = rgba[:, :, 3] == 0
    shadowish = (spread < 0.18) & (k > 0.24) & (k < 1.1)
    # the grey stone / slab faces look like the background: an opening breaks thin leaks through the outline so only wide shadow bands connect to the key area,
    # and nothing farther than 70 px from the key area can go
    barrier = ndi.binary_dilation(k < 0.40, iterations=3)       # the dark outline, with its small gaps closed
    shadow_open = ndi.binary_opening(shadowish & ~barrier, structure=b3.disk(3))
    lab, _ = ndi.label(shadow_open | clear)
    edge_labs = np.unique(lab[clear])
    edge_labs = edge_labs[edge_labs != 0]
    core = np.isin(lab, edge_labs) & ~clear
    gone = ndi.binary_dilation(core, iterations=5) & shadowish
    gone &= ndi.distance_transform_edt(~clear) < 70
    gone = ndi.binary_dilation(gone, iterations=1)
    if name in ("A1", "B1"):
        # background-coloured pockets enclosed by the art (between the two ropes of a swing, between the flick arm and the plank) are not edge connected: clear them
        pocket = (shadowish & ~barrier) | (dist0 < pa.KEY_TOL)
        plab, pn = ndi.label(pocket)
        parea = ndi.sum(pocket, plab, range(1, pn + 1))
        edge_p = np.unique(plab[ndi.binary_dilation(clear, iterations=2)])
        for i, ar in enumerate(parea, start=1):
            if i in edge_p or ar > 15000 or ar < 30:
                continue
            m = plab == i
            ys = np.nonzero(m.any(axis=1))[0]
            if name == "A1" and ys.min() > 1300 * S:
                continue                 # the grey koi stone faces stay
            gone |= ndi.binary_dilation(m, iterations=2) & shadowish
            gone |= m
    gone &= ~(spread > 0.30)
    rgba[gone, 3] = 0
    rgba[:, :, 3] = np.where(rgba[:, :, 3] >= 128, 255, 0).astype(np.uint8)
    # row numbers, divider lines and specks go first (they must not join the rows)
    op = rgba[:, :, 3] > 128
    lab2, n = ndi.label(op, structure=np.ones((3, 3)))
    for i, sl in enumerate(ndi.find_objects(lab2), start=1):
        h, w = sl[0].stop - sl[0].start, sl[1].stop - sl[1].start
        area = int((lab2[sl] == i).sum())
        if (sl[1].stop < 200 and area < 8000 and name != "A1") or (h < 16 and w < 400 and area < 4000) or (h < 14 and w > 600) or area < 60:
            rgba[sl][lab2[sl] == i] = 0
    rgba = b3.bleed_colour(rgba)
    for box in SPARKLES.get(name, []):
        rgba = b4.remove_sparkle(rgba, [int(round(v * S)) for v in box[:4]], clone=box in CLONE)
    # leftover thin spikes of the sparkles (they stand in the air): thinner than 7 px and near a sparkle box
    al = rgba[:, :, 3] >= 128
    thin = al & ~ndi.binary_opening(np.pad(al, 16), structure=b3.disk(9))[16:-16, 16:-16]
    win = np.zeros_like(al)
    for box in SPARKLES.get(name, []):
        x0, y0, x1, y1 = [int(round(v * S)) for v in box[:4]]
        win[max(0, y0 - 30):y1 + 30, max(0, x0 - 30):x1 + 30] = True
    rgba[thin & win, 3] = 0
    op = rgba[:, :, 3] > 128
    lab2, n = ndi.label(op, structure=np.ones((3, 3)))
    for i, sl in enumerate(ndi.find_objects(lab2), start=1):
        if int((lab2[sl] == i).sum()) < 60:
            rgba[sl][lab2[sl] == i] = 0
    return rgba


_CACHE = {}


def split_rows(rgba, near=20, minmain=15000):
    """Rows as connected painted objects of at least 15000 px; smaller parts (hammer head, dust, bubbles) join the nearest row within `near` px."""
    op = rgba[:, :, 3] > 128
    lab, n = ndi.label(ndi.binary_dilation(op, iterations=2), structure=np.ones((3, 3)))
    objs = ndi.find_objects(lab)
    area = ndi.sum(op, lab, range(1, n + 1))
    mains = [i for i in range(n) if area[i] >= minmain]
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
        if gap(j) < near * near:
            owner[i] = j
        else:
            print("   (dropped loose part area %d at %s)" % (area[i], (sl[1].start, sl[0].start)))
    rows = []
    for m in mains:
        keep = np.isin(lab, [i + 1 for i, o in owner.items() if o == m]) & op
        a = rgba.copy()
        a[~keep, 3] = 0
        ys, xs = np.nonzero(keep)
        a = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        rows.append(b3.finish_row(a, star=False))
    return rows


def sheet_rows(name):
    if name not in _CACHE:
        cache = os.path.join(os.environ.get("TEMP", "."), "reg005_clean_%s.npz" % name)      # cleaning takes about a minute per sheet; set REG5_FRESH=1 to redo it
        if os.path.exists(cache) and not os.environ.get("REG5_FRESH"):
            z = np.load(cache)
            rgba = z["rgba"]
            KEYS[name] = tuple(int(v) for v in z["key"])
        else:
            rgba = clean_sheet(name)
            np.savez(cache, rgba=rgba, key=np.array(KEYS[name]))
        rows = split_rows(rgba, 150 if name == "C2" else 20, 40000 if name == "C2" else 15000)
        print("sheet %s: %d rows %s" % (name, len(rows), [tuple(r.shape[:2]) for r in rows]))
        for i, r in enumerate(rows):
            Image.fromarray(r).save(os.path.join(CAND, "_row_%s_%d.png" % (name, i + 1)))
        _CACHE[name] = rows
    return _CACHE[name]


# ---------------------------------------------------------------- kinds

KINDS = {
    "bambooflick": dict(sheet="A1", rows=[0, 1], widths=[3, 3], cols=[(0.0, 0.18)], top=50, tmul=0.62, poses=[("rest", -1), ("sprung", 1)]),
    "lanternlift": dict(sheet="A1", rows=[2, 3], widths=[3, 5], cols=[(0.25, 0.45)], top=50),
    "koistone":    dict(sheet="A1", rows=[4, 5], widths=[5, 7], cols=[(0.3, 0.7)], top=50),
    "swingplank":  dict(sheet="B1", rows=[0, 1, 2], widths=[3, 4.5, 6], cols=[(0.0, 1.0)], top=85),
    "pagodaridge": dict(sheet="B1", rows=[3, 4, 5], widths=[2.5, 3.5, 4.5], cols=[(0.3, 0.7)], top=50),
    "malletsmash": dict(sheet="C2", rows=[0, 1, 2, 3], widths=[4.5, 4.5, 6, 6], cols=[(0.02, 0.12)], top=50,
                        poses=[("raised", -1), ("smashed", 1), ("raised", -1), ("smashed", 1)]),
    "rollinglog":  dict(sheet="C2", rows=[4], widths=[5], cols=[(0.02, 0.12)], top=50),
}


def above_cols(a, surf, thr, lo, hi):
    """Columns in [lo, hi) whose top is more than thr px above the surface line."""
    al = a[:, :, 3] > 128
    out = []
    for x in range(max(0, lo), min(a.shape[1], hi)):
        ys = np.nonzero(al[:, x])[0]
        if len(ys) and ys.min() < surf - thr:
            out.append(x)
    return out


def windows_for(kind, info):
    a, surf, bot, bl, br, T = info["a"], info["surf"], info["bot"], info["bl"], info["br"], info["T"]
    W = br - bl
    if kind == "bambooflick":
        sprung = a.shape[0] > 1.8 * (bot - surf)
        lo = bl + int((0.298 if sprung else 0.328) * W)      # right behind the round end of the left log
        rgb = a[:, :, :3].astype(int)
        red = (a[:, :, 3] > 128) & (rgb[:, :, 0] > 150) & (rgb[:, :, 1] < 100) & (rgb[:, :, 2] < 100)
        cols = [x for x in range(bl + int(0.45 * W), br) if red[:, x].sum() >= 4]
        groups = []
        for x in cols:
            if groups and x - groups[-1][1] <= 10:
                groups[-1][1] = x
            else:
                groups.append([x, x])
        groups = [g for g in groups if g[1] - g[0] >= 14]       # the bow, not a red grain line of the plank
        z0 = groups[0][0] if groups else br - int(1.0 * T)
        return [(lo, z0 - 10)]
    if kind == "lanternlift":
        m = int(1.3 * T)
        return [(bl + m, br - m)]
    if kind == "koistone":
        m = int(0.9 * T)
        return [(bl + m, br - m)]
    if kind == "swingplank":
        cols = above_cols(a, surf, 0.35 * T, bl, br)
        groups = []
        for x in cols:
            if groups and x - groups[-1][1] <= 14:
                groups[-1][1] = x
            else:
                groups.append([x, x])
        wins = [(g0[1] + 10, g1[0] - 10) for g0, g1 in zip(groups, groups[1:])]
        wins = [w for w in wins if w[1] - w[0] > 60]
        if not wins:
            raise ValueError("no window between the ropes")
        return wins
    if kind == "pagodaridge":
        return [(bl + int(0.25 * W), br - int(0.25 * W))]
    if kind == "malletsmash":
        lo = bl + 160
        cols = above_cols(a, surf, 0.25 * T, lo, br)
        z0 = min(cols) if cols else br - 200
        return [(lo, z0 - 10)]
    if kind == "rollinglog":
        return [(bl + 150, br - 180)]
    raise KeyError(kind)


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
    name = "reg005_%s_%sw%s.png" % (kind, tag, ("%g" % w).replace(".", "_"))
    Image.fromarray(img).save(os.path.join(outdir, name))
    m = b1.measure_piece(img, surf + PADPX, bot + PADPX)
    m.update({"width": float(w), "file": name, "ppu": round((m["bodyRight"] - m["bodyLeft"]) / w, 3), "kind": kind, "dir": 0})
    m.update(extra)
    print("   %-40s body %4d px = %.2f u (want %g)  thickness %.2f u  size %dx%d" % (
        name, m["bodyRight"] - m["bodyLeft"], (m["bodyRight"] - m["bodyLeft"]) / PPU, w, (m["bodyBottom"] - m["surfaceY"]) / PPU, m["w"], m["h"]))
    return m


def write_doc(kind, outdir, pieces, extra=None):
    doc = {"asset": kind + "_widths", "kind": kind, "ppuBake": PPU, "pieces": pieces}
    if extra:
        doc.update(extra)
    with open(os.path.join(outdir, "platform_set.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(doc, fh, indent=2)
        fh.write("\n")
    return doc


def fit(kind, info, w):
    a = info["a"]
    delta = int(round(w * PPU)) - (info["br"] - info["bl"])
    if abs(delta) <= 1:
        return a
    wins = windows_for(kind, info)
    if kind == "bambooflick" and delta < 0:      # one single cut of the plank: the halving loop of multi_fit would leave too much of the long art
        lo, hi = wins[0]
        if -delta > hi - lo - b1.FADE - b1.WIN - 8:
            raise ValueError("window %d..%d too small to cut %d px" % (lo, hi, -delta))
        return b1.cut_window(a, -delta, lo, hi)
    return b4.multi_fit(a, delta, wins)


def make_log(outdir):
    rows = sheet_rows("C2")
    a = rows[5]
    ys, xs = np.nonzero(a[:, :, 3] > 24)
    a = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    f = 1.2 * PPU / a.shape[1]
    a = b1.resample(a, f)
    a = np.ascontiguousarray(np.pad(a, ((PADPX, PADPX), (PADPX, PADPX), (0, 0))))
    Image.fromarray(a).save(os.path.join(outdir, "log.png"))
    print("   log.png %dx%d px (%.2f x %.2f u incl. padding)" % (a.shape[1], a.shape[0], a.shape[1] / PPU, a.shape[0] / PPU))


def bake(kind, cfg):
    sheet = sheet_rows(cfg["sheet"])
    rows = [sheet[i].copy() for i in cfg["rows"]]
    outdir = prepare_outdir(kind)
    sws.pink = lambda a, x0, x1: 0
    b1.FADE, b1.WIN = 32, 14
    sws.FADE, sws.WIN = 32, 14
    poses = cfg.get("poses")
    pieces = []
    b4.THICK_PX = THICK_PX * cfg.get("tmul", 1.0)
    info = b4.scale_rows(kind, cfg, rows, list(range(len(rows))))
    b4.THICK_PX = THICK_PX
    if poses:
        for i, (inf, (pose, d)) in enumerate(zip(info, poses)):
            w = cfg["widths"][i]
            try:
                img = fit(kind, inf, w)
            except ValueError as e:
                print("   !! %s %g: %s" % (pose, w, e))
                continue
            pieces.append(emit(kind, outdir, img, inf["surf"], inf["bot"], w, pose + "_", {"dir": d, "pose": pose}))
    else:
        for w in cfg["widths"]:
            for si in sorted(range(len(info)), key=lambda k: abs(info[k]["natural"] - w)):
                src = info[si]
                try:
                    img = fit(kind, src, w)
                except ValueError as e:
                    print("   (width %g from the %.2f u row: %s)" % (w, src["natural"], e))
                    continue
                pieces.append(emit(kind, outdir, img, src["surf"], src["bot"], w, "", {"src_row": cfg["rows"][si] + 1, "src_natural_u": round(src["natural"], 2)}))
                break
            else:
                print("   !! width %g could not be baked" % w)
    extra = None
    if kind == "rollinglog":
        make_log(outdir)
        extra = {"log": {"file": "log.png"}}
    return write_doc(kind, outdir, pieces, extra), outdir


def contact(docs):
    from PIL import ImageDraw
    scale = 100.0 / PPU
    items = []
    for kind, (doc, outdir) in docs.items():
        for piece in doc["pieces"]:
            im = Image.open(os.path.join(outdir, piece["file"])).convert("RGBA")
            im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)
            items.append(("%s %gu %s d%+d" % (kind, piece["width"], piece.get("pose", ""), piece.get("dir", 0)), im))
        if doc.get("log"):
            im = Image.open(os.path.join(outdir, doc["log"]["file"])).convert("RGBA")
            items.append(("rollinglog log.png", im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)))
    cols = 3
    cw = max(i.width for _, i in items) + 20
    ch = max(i.height for _, i in items) + 40
    sheet = Image.new("RGBA", (cols * cw, ((len(items) + cols - 1) // cols) * ch), (210, 232, 200, 255))
    d = ImageDraw.Draw(sheet)
    for n, (label, im) in enumerate(items):
        cx, cy = (n % cols) * cw, (n // cols) * ch
        d.text((cx + 8, cy + 4), label, fill=(20, 40, 20, 255))
        sheet.alpha_composite(im, (cx + 10, cy + 24))
    sheet.convert("RGB").save(os.path.join(CAND, "baked_contact.png"))


def run_qa(docs):
    import qa_art
    bad = total = 0
    for kind, (doc, outdir) in docs.items():
        key = KEYS.get(KINDS[kind]["sheet"])
        files = [p["file"] for p in doc["pieces"]] + ([doc["log"]["file"]] if doc.get("log") else [])
        for f in files:
            _, issues, _ = qa_art.analyse(os.path.join(outdir, f), key)
            total += 1
            issues = [i for i in issues if not i.startswith("extreme")]
            if issues:
                print("QA %-40s %s" % (f, ", ".join(issues)))
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
