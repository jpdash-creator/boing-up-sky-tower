"""Cut worn-gear layers out of the OpenArt gear pilot sheets. Costs nothing (no OpenArt calls).

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/cut_gear_layers.py [--sheets Art/_candidates/gear_pilot] [--debug E:/Unity/logs/gear_cut_debug]

Input : four 2304x1728 sheets head/body/pants/boots.png (3 columns x 2 rows, cells 768x864) of the clean Momo
        (Assets/Art/Characters/char_momo/char_momo/char_momo.png) wearing ONE piece of one slot per cell.
Method: per cell
  1. the cell is registered to the clean Momo (SIFT matches inside a mask of the body parts the slot does not
     cover -> similarity transform, refined with ECC);
  2. the cell background (flat grey) is removed by a flood fill from the cell border;
  3. the gear is what differs from the clean Momo (colour distance, tolerant to 1-2 px misregistration) plus
     everything outside Momo's silhouette; the sticker outline the generator drew around the gear is peeled off;
  4. pieces that sit behind the body (wings) go to a separate `back` layer;
  5. layers are written in the canvas of char_momo.png so any layer can be drawn on Momo at the same position.
Output: Assets/Art/Gear/layers/<slot>_<index>.png (tight crop; layers.json 'offset' = top-left on the padded canvas) (+ <slot>_<index>_back.png), Assets/Art/Gear/icons/<slot>_<index>.png
        (256 px), Assets/Art/Gear/layers.json.
"""
import argparse
import json
import os
import sys

sys.path.insert(0, "E:/pylibs")
import cv2  # noqa: E402
import numpy as np  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MOMO = os.path.join(ROOT, "Assets", "Art", "Characters", "char_momo", "char_momo", "char_momo.png")
OUT = os.path.join(ROOT, "Assets", "Art", "Gear")
CELL_W, CELL_H = 768, 864
RARITY = ["Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic"]
# draw order, bottom to top (Momo is drawn first)
DRAW_ORDER = {"back": 0, "body": 1, "pants": 2, "boots": 3, "jacket": 4, "head": 5}

# Momo canvas rows (char_momo.png, 1365 x 1649). Used to build the masks of the unchanged body parts and to
# limit where each slot may produce pixels.
SLOT_BAND = {  # (y0, y1) rows a piece of this slot may occupy; None = unlimited
    "head": (-9999, 960),
    "body": (860, 99999),
    "pants": (1270, 1585),
    "boots": (1380, 99999),
}
# rows of the clean Momo that this slot's gear never touches, used for registration
REG_BAND = {"head": (700, 1400), "body": (150, 800), "pants": (150, 1050), "boots": (150, 1300)}
PNG9 = [cv2.IMWRITE_PNG_COMPRESSION, 9]
OPEN_PX = 9  # minimum thickness of gear pixels inside the silhouette
# Gear is bigger than Momo (hat tips, cloak hems, wings, soles), so layers live on a padded canvas: Momo's canvas
# plus these margins (left, top, right, bottom). layers.json records the offset where Momo's own canvas starts.
PAD = (260, 320, 260, 200)
HOLE_MAX = 9000  # px (Momo canvas) of the biggest enclosed hole that is filled
HEAD_FINAL_OPEN = 15  # kills the fringe of the redrawn head outline left around a head piece
# Visual review verdict per piece (compose_gear_test.py contact sheets, 2026-10-24). Anything not listed is usable.
REVIEW = {
    ("head", 1): "fail: cream cap has almost the fur colour, only blotches survive",
    ("head", 6): "fail: thin cream headband on cream fur, only the bell survives",
    ("body", 1): "poor: tan vest low contrast to fur, lower half partly missing",
    ("pants", 1): "fail: cream shorts have the fur colour",
    ("pants", 6): "fail: white socks/shorts have the fur colour, only the blue stripes survive",
    ("boots", 1): "poor: white sneakers have the fur colour, outline and sole survive, fill is patchy",
    ("pants", 3): "partial: knight set drawn as full armour, only waist-down kept; arm guards belong to the body slot",
    ("head", 3): "usable: a bit of the redrawn left ear fringe remains",
    ("head", 4): "usable: base left ear shows next to the hat (needs a hide mask)",
}
SHEETS = ["head", "body", "pants", "boots"]
# pieces whose parts outside Momo's silhouette sit behind the body (wings): (slot, index)
BACK_OUTSIDE = {("boots", 5)}
# per piece extraction overrides (head: diff threshold and opening size in px)
PIECE = {("head", 1): {"thr": 22, "open": 17}, ("head", 6): {"thr": 18, "open": 7}}


def load_momo():
    m = cv2.imread(MOMO, cv2.IMREAD_UNCHANGED)
    if m is None or m.shape[2] != 4:
        raise SystemExit("cannot read " + MOMO)
    return m


def cell_of(sheet, i):
    r, c = divmod(i, 3)
    return sheet[r * CELL_H:(r + 1) * CELL_H, c * CELL_W:(c + 1) * CELL_W]


def momo_on(bg, momo):
    """Clean Momo composited on a flat background colour (BGR)."""
    a = momo[:, :, 3:4].astype(np.float32) / 255
    return (momo[:, :, :3] * a + np.array(bg, np.float32) * (1 - a)).astype(np.uint8)


def cell_bg(cell):
    """Median colour of the cell border strip (flat grey on every sheet)."""
    strip = np.concatenate([cell[:6].reshape(-1, 3), cell[-6:].reshape(-1, 3),
                            cell[:, :6].reshape(-1, 3), cell[:, -6:].reshape(-1, 3)])
    return np.median(strip, axis=0)


def register(cell, momo, slot):
    """Return 2x3 matrix mapping Momo canvas coordinates -> cell coordinates (similarity, ECC refined)."""
    bg = cell_bg(cell)
    ref = momo_on(bg, momo)
    y0, y1 = REG_BAND[slot]
    rmask = np.zeros(ref.shape[:2], np.uint8)
    rmask[y0:y1] = 255
    rmask[momo[:, :, 3] < 128] = 0
    rg = cv2.cvtColor(ref, cv2.COLOR_BGR2GRAY)
    cg = cv2.cvtColor(cell, cv2.COLOR_BGR2GRAY)
    ak = cv2.SIFT_create(4000)
    kr, dr = ak.detectAndCompute(rg, rmask)
    kc, dc = ak.detectAndCompute(cg, None)
    bf = cv2.BFMatcher(cv2.NORM_L2)
    good = []
    for pair in bf.knnMatch(dr, dc, k=2):
        if len(pair) == 2 and pair[0].distance < 0.75 * pair[1].distance:
            good.append(pair[0])
    src = np.float32([kr[g.queryIdx].pt for g in good])
    dst = np.float32([kc[g.trainIdx].pt for g in good])
    mat, inl = cv2.estimateAffinePartial2D(src, dst, method=cv2.RANSAC, ransacReprojThreshold=4.0)
    if mat is None or inl.sum() < 12:
        raise SystemExit("registration failed for %s (%d matches)" % (slot, len(good)))
    n_in = int(inl.sum())
    # ECC refine at half resolution inside the mask (warp maps template(ref) -> input(cell))
    s = 0.5
    S = np.diag([s, s, 1.0])
    full = np.vstack([mat, [0, 0, 1]])
    w0 = (S @ full @ np.linalg.inv(S))[:2].astype(np.float32)
    rs = cv2.resize(rg, None, fx=s, fy=s, interpolation=cv2.INTER_AREA)
    cs = cv2.resize(cg, None, fx=s, fy=s, interpolation=cv2.INTER_AREA)
    ms = cv2.resize(rmask, None, fx=s, fy=s, interpolation=cv2.INTER_NEAREST)
    ms = cv2.erode(ms, np.ones((9, 9), np.uint8))
    try:
        _, w1 = cv2.findTransformECC(rs, cs, w0.copy(), cv2.MOTION_AFFINE,
                                     (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_COUNT, 80, 1e-6), ms, 5)
    except cv2.error:
        w1 = w0
    full1 = np.linalg.inv(S) @ np.vstack([w1, [0, 0, 1]]) @ S
    return full1[:2], n_in


def lab(img):
    return cv2.cvtColor(img, cv2.COLOR_BGR2LAB).astype(np.float32)


def tolerant_diff(a, b, r=3):
    """Per pixel colour distance between a and b, minimised over shifts of b within +-r px (kills 1-3 px misregistration)."""
    la, lb = lab(a), lab(b)
    best = np.full(a.shape[:2], 1e9, np.float32)
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            sh = np.roll(lb, (dy, dx), axis=(0, 1))
            d = np.sqrt(((la - sh) ** 2).sum(2))
            np.minimum(best, d, out=best)
    return best


def bg_mask_cell(cell, tol=7):
    """Cell background by flood fill from the border over smooth pixels (handles the vignette / gradients next to the
    cell separators that a single background colour cannot)."""
    blur = cv2.GaussianBlur(cell, (0, 0), 1.2)
    h, w = blur.shape[:2]
    bgm = np.zeros((h + 2, w + 2), np.uint8)
    flags = 4 | cv2.FLOODFILL_MASK_ONLY | (255 << 8)
    for x in range(14, w - 14, 40):
        for y in (14, h - 15):
            if not bgm[y + 1, x + 1]:
                cv2.floodFill(blur.copy(), bgm, (x, y), 0, (tol,) * 3, (tol,) * 3, flags)
    for y in range(14, h - 14, 40):
        for x in (14, w - 15):
            if not bgm[y + 1, x + 1]:
                cv2.floodFill(blur.copy(), bgm, (x, y), 0, (tol,) * 3, (tol,) * 3, flags)
    return bgm[1:-1, 1:-1] > 0


def foreground(cell_bg_mask, mat, shape):
    """Figure mask in the Momo canvas: warp the (inverted) cell background mask, drop specks."""
    fgc = (~cell_bg_mask).astype(np.uint8) * 255
    fg = cv2.warpAffine(fgc, mat, (shape[1], shape[0]), flags=cv2.INTER_LINEAR | cv2.WARP_INVERSE_MAP) > 127
    fg = cv2.morphologyEx(fg.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    n, lbl, st, _ = cv2.connectedComponentsWithStats(fg, connectivity=8)
    keep = np.zeros_like(fg)
    for k in range(1, n):
        if st[k, cv2.CC_STAT_AREA] > 400:
            keep[lbl == k] = 1
    return keep


def peel_outline(fg, warped, depth=15):
    """Remove the pale sticker ring the generator drew around the outside of the figure: bright, low-chroma pixels
    reachable from the background within `depth` px."""
    l = lab(warped)
    bright = l[:, :, 0] > 175
    lowchroma = np.hypot(l[:, :, 1] - 128, l[:, :, 2] - 128) < 14
    ringlike = (bright & lowchroma & (fg > 0)).astype(np.uint8)
    cur = (fg == 0).astype(np.uint8)
    k3 = np.ones((3, 3), np.uint8)
    removed = np.zeros_like(fg)
    for _ in range(depth):
        grow = cv2.dilate(cur, k3) & ringlike & (1 - cur)
        if not grow.any():
            break
        cur |= grow
        removed |= grow
    return (fg > 0).astype(np.uint8) & (1 - removed)


def clean_mask(m, min_area=250):
    m = cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    n, lbl, st, _ = cv2.connectedComponentsWithStats(m, connectivity=8)
    out = np.zeros_like(m)
    for k in range(1, n):
        if st[k, cv2.CC_STAT_AREA] >= min_area:
            out[lbl == k] = 1
    return out > 0


def fill_small_holes(m, max_area):
    """Enclosed holes (white stars on a robe, a gem) are cloth, not fur: fill the ones below max_area px."""
    m8 = m.astype(np.uint8)
    cnts, hier = cv2.findContours(m8, cv2.RETR_CCOMP, cv2.CHAIN_APPROX_SIMPLE)
    if hier is None:
        return m
    for c, h in zip(cnts, hier[0]):
        if h[3] >= 0 and cv2.contourArea(c) < max_area:
            cv2.drawContours(m8, [c], -1, 1, -1)
    return m8 > 0


def to_rgba(warped, mask, shrink=1):
    """BGR + mask -> BGRA, eroded by `shrink` px (grey edge halo) and feathered by ~1 px."""
    m = mask.astype(np.uint8)
    if shrink:
        m = cv2.erode(m, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * shrink + 1, 2 * shrink + 1)))
    a = cv2.GaussianBlur(m.astype(np.float32), (0, 0), 0.8)
    a = np.clip((a - 0.25) / 0.5, 0, 1)
    return np.dstack([warped, (a * 255).astype(np.uint8)])


def align_ref(warped, momo, bg, max_shift=14):
    """The generator redraws Momo a little differently in every cell. A smooth dense flow (warped cell -> clean Momo)
    moves the clean Momo and its silhouette onto the cell's own drawing, so outlines line up to ~1 px."""
    H, Wd = momo.shape[:2]
    ref = momo_on(bg, momo)
    g0 = cv2.cvtColor(warped, cv2.COLOR_BGR2GRAY)
    g1 = cv2.cvtColor(ref, cv2.COLOR_BGR2GRAY)
    dis = cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
    flow = dis.calc(g0, g1, None)
    flow = cv2.GaussianBlur(flow, (0, 0), 10)
    mag = np.hypot(flow[:, :, 0], flow[:, :, 1])
    flow *= np.minimum(1.0, max_shift / np.maximum(mag, 1e-6))[:, :, None]
    gx, gy = np.meshgrid(np.arange(Wd, dtype=np.float32), np.arange(H, dtype=np.float32))
    mx, my = gx + flow[:, :, 0], gy + flow[:, :, 1]
    ref_a = cv2.remap(ref, mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_REPLICATE)
    alpha_a = cv2.remap(momo[:, :, 3], mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=0)
    return ref_a, alpha_a > 128


def extract(warped, bg, momo, slot, index, fg0, thr=30, valid=None, off=(0, 0)):
    H, Wd = momo.shape[:2]
    ox, oy = off
    ref, alpha = align_ref(warped, momo, bg)
    fg = peel_outline(fg0, warped)
    if valid is not None:
        fg &= valid
    diff = tolerant_diff(warped, ref, 2)
    inside = alpha & (diff > thr)
    if slot != "head":
        # thin lines are the generator's re-drawn Momo outline sitting 1-4 px off the clean one (a double outline)
        inside = cv2.morphologyEx(inside.astype(np.uint8), cv2.MORPH_OPEN,
                                  cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (OPEN_PX, OPEN_PX))) > 0
    # outside the silhouette: figure pixels that are not the generator's re-drawn Momo ring (<= 3 px from it)
    near = cv2.dilate(alpha.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7) if slot in ("head", "body") else (19, 19))) > 0
    outside = (fg > 0) & ~alpha & ~near
    if slot != "head":
        outside = cv2.morphologyEx(outside.astype(np.uint8), cv2.MORPH_OPEN,
                                   cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (OPEN_PX, OPEN_PX))) > 0
    gear = inside | outside
    if slot == "head":
        # the head is redrawn differently in every cell (ears, fluff, outline): keep only thick, strongly different
        # regions, join them and fill their holes (the piece is solid), then require it to differ at all
        k = PIECE.get((slot, index), {})
        core = (alpha & (diff > k.get("thr", 44))).astype(np.uint8)
        o = k.get("open", 13)
        core = cv2.morphologyEx(core, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (o, o)))
        strong = (core > 0) | outside
        strong = cv2.morphologyEx(strong.astype(np.uint8), cv2.MORPH_OPEN,
                                  cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (o, o)))
        solid = cv2.morphologyEx(strong, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (31, 31)))
        cnts, _ = cv2.findContours(solid, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        filled = np.zeros_like(solid)
        cv2.drawContours(filled, cnts, -1, 1, -1)
        gear = (filled > 0) & (inside | outside | (diff > 14))
        gear = cv2.morphologyEx(gear.astype(np.uint8), cv2.MORPH_OPEN,
                                cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (HEAD_FINAL_OPEN,) * 2)) > 0
    if valid is not None:
        gear &= valid
    y0, y1 = SLOT_BAND[slot]
    band = np.zeros_like(gear)
    band[max(0, y0 + oy):y1 + oy] = True
    if slot == "head":
        band[640 + oy:, 1000 + ox:] = False  # the fluffy tail: never head gear, and it touches the cell separator bar
    gear &= band
    gear = clean_mask(gear)
    if slot != "head":
        gear = fill_small_holes(gear, HOLE_MAX)
    back = np.zeros_like(gear)
    if (slot, index) in BACK_OUTSIDE:
        back = gear & (~alpha) & (np.arange(H)[:, None] < 1500 + oy)
        gear = gear & ~back
    return gear, back, diff, alpha


def over(dst, src):
    """Straight-alpha 'over' of two BGRA uint8 images."""
    a = src[:, :, 3:4].astype(np.float32) / 255
    b = dst[:, :, 3:4].astype(np.float32) / 255
    oa = a + b * (1 - a)
    rgb = (src[:, :, :3] * a + dst[:, :, :3] * b * (1 - a)) / np.maximum(oa, 1e-6)
    return np.dstack([rgb, oa * 255]).astype(np.uint8)


def crop_to_alpha(rgba, margin=2):
    """Tight crop of a BGRA layer; returns (crop, x, y) with x, y = top-left on the padded canvas."""
    ys, xs = np.where(rgba[:, :, 3] > 0)
    x0, x1 = max(0, xs.min() - margin), min(rgba.shape[1], xs.max() + 1 + margin)
    y0, y1 = max(0, ys.min() - margin), min(rgba.shape[0], ys.max() + 1 + margin)
    return rgba[y0:y1, x0:x1], int(x0), int(y0)


def make_icon(rgba_list, size=256, pad=14):
    comp = None
    for r in rgba_list:
        comp = r if comp is None else over(comp, r)
    ys, xs = np.where(comp[:, :, 3] > 8)
    crop = comp[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    h, w = crop.shape[:2]
    s = (size - 2 * pad) / max(h, w)
    crop = cv2.resize(crop, (max(1, round(w * s)), max(1, round(h * s))), interpolation=cv2.INTER_AREA)
    out = np.zeros((size, size, 4), np.uint8)
    oy, ox = (size - crop.shape[0]) // 2, (size - crop.shape[1]) // 2
    out[oy:oy + crop.shape[0], ox:ox + crop.shape[1]] = crop
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--sheets", default=os.path.join(ROOT, "Art", "_candidates", "gear_pilot"))
    ap.add_argument("--debug", default="")
    ap.add_argument("--only", default="", help="e.g. body_2")
    args = ap.parse_args()
    momo = load_momo()
    H0, W0 = momo.shape[:2]
    pl, pt, pr, pb = PAD
    momo_p = cv2.copyMakeBorder(momo, pt, pb, pl, pr, cv2.BORDER_CONSTANT, value=(0, 0, 0, 0))
    H, Wd = momo_p.shape[:2]
    layers_dir = os.path.join(OUT, "layers")
    icons_dir = os.path.join(OUT, "icons")
    os.makedirs(layers_dir, exist_ok=True)
    os.makedirs(icons_dir, exist_ok=True)
    entries = []
    for slot in SHEETS:
        sheet = cv2.imread(os.path.join(args.sheets, slot + ".png"))
        if sheet is None:
            raise SystemExit("missing sheet " + slot)
        for i in range(6):
            idx = i + 1
            name = "%s_%d" % (slot, idx)
            if args.only and args.only != name:
                continue
            cell = cell_of(sheet, i)
            bg = cell_bg(cell)
            mat, n = register(cell, momo, slot)
            mat = mat.copy()
            mat[:, 2] -= mat[:, :2] @ np.array([pl, pt])  # padded canvas coords -> cell coords
            warped = cv2.warpAffine(cell, mat, (Wd, H), flags=cv2.INTER_CUBIC | cv2.WARP_INVERSE_MAP,
                                    borderMode=cv2.BORDER_REPLICATE)
            vcell = np.zeros(cell.shape[:2], np.uint8)
            vcell[12:-12, 22:-22] = 255
            valid = cv2.warpAffine(vcell, mat, (Wd, H), flags=cv2.INTER_NEAREST | cv2.WARP_INVERSE_MAP) > 0
            fg0 = foreground(bg_mask_cell(cell), mat, (H, Wd))
            gear, back, diff, _ = extract(warped, bg, momo_p, slot, idx, fg0, valid=valid, off=(pl, pt))
            front_rgba = to_rgba(warped, gear)
            crop, cx, cy = crop_to_alpha(front_rgba)
            cv2.imwrite(os.path.join(layers_dir, name + ".png"), crop, PNG9)
            parts = [front_rgba]
            back_file = None
            back_off = None
            if back.any():
                back_rgba = to_rgba(warped, back)
                back_file = name + "_back.png"
                bcrop, bx, by = crop_to_alpha(back_rgba)
                back_off = [bx, by]
                cv2.imwrite(os.path.join(layers_dir, back_file), bcrop, PNG9)
                parts = [back_rgba, front_rgba]
            cv2.imwrite(os.path.join(icons_dir, name + ".png"), make_icon(parts))
            sc = float(np.hypot(mat[0, 0], mat[0, 1]))
            print("%s inliers=%d scale=%.3f gear_px=%d back_px=%d" % (name, n, sc, int(gear.sum()), int(back.sum())))
            entries.append({"slot": slot, "index": idx, "rarity": RARITY[i], "file": "layers/%s.png" % name,
                            "offset": [cx, cy], "size": [crop.shape[1], crop.shape[0]], "backOffset": back_off,
                            "backFile": ("layers/" + back_file) if back_file else None,
                            "usable": REVIEW.get((slot, idx), "usable").split(":")[0] in ("usable", "partial"),
                            "review": REVIEW.get((slot, idx), "usable"),
                            "icon": "icons/%s.png" % name, "drawOrder": DRAW_ORDER[slot],
                            "backDrawOrder": DRAW_ORDER["back"] if back_file else None})
            if args.debug:
                os.makedirs(args.debug, exist_ok=True)
                dbg = np.dstack([np.clip(diff * 4, 0, 255).astype(np.uint8)] * 3)
                dbg[gear > 0] = (0, 200, 0)
                dbg[back > 0] = (200, 0, 200)
                cv2.imwrite(os.path.join(args.debug, "mask_%s.jpg" % name), cv2.resize(dbg, None, fx=0.4, fy=0.4))
    if not args.only:
        meta = {"canvas": [Wd, H], "momoOffset": [pl, pt], "momoSize": [W0, H0], "reference": "Assets/Art/Characters/char_momo/char_momo/char_momo.png",
                "drawOrder": DRAW_ORDER, "layers": entries}
        with open(os.path.join(OUT, "layers.json"), "w") as f:
            json.dump(meta, f, indent=2)


if __name__ == "__main__":
    main()
