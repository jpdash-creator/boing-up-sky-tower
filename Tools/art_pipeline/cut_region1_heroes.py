"""Cuts the approved Region 1 hero paintings (F4.1) into the standard polygon-hero parts (docs/19, docs/34).

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/cut_region1_heroes.py [--only id,id] [--debug] [--out Assets/Resources/Characters]

Input: Art/_candidates/<folder>/<file>.png (full-body front view, 1728 x 2304, flat #808080 background, white sticker rim on most of them).
Per hero (table HEROES below): key the background (and the white sticker rim), register to the 1365 x 1649 doll canvas (height 60..1605, feet centred on x 682),
then give every pixel to exactly one part by plain partition lines: head above `head_y`, handL / handR outside the body columns `xl` / `xr` between head and legs,
legL / legR below `leg_y` split at x 682, body = the rest. Writes back (empty, never listed), legL, legR, body, handL, handR, head as full-canvas transparent PNGs,
rig.json (order back to front: legL, legR, body, handL, handR, head) and, with --debug, a grid picture next to it for choosing the lines.
The run and the UI draw the parts as one stack, so the stack equals the painting exactly; the partition only decides the draw order.
"""
import json
import os
import sys
import numpy as np
import cv2

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cut_polygon_character as cp  # noqa: E402

W, H = cp.W, cp.H
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
CAND = os.path.join(ROOT, "Art", "_candidates")

# id: (file under Art/_candidates, key mode, head_y, xl, xr, leg_y)
#   key mode: "sticker" = background grey + white sticker rim, "soft" = background grey only (Dandy: glow edge, no rim)
HEROES = {
    "momo_r": ("rare_heroes/momo_r1.png", "trim36", 905, 285, 805, 1330),
    "momo_e": ("elite_heroes/momo_e3.png", "trim30", 905, 285, 805, 1330),
    "momo_l": ("legendary_heroes/momo_l1.png", "trim33h", 905, 285, 805, 1330),
    "bumble": ("bumble/bumble_c3.png", "sticker", 900, 380, 985, 1330),
    "bumble_u": ("bumble/bumble_u1.png", "sticker", 900, 380, 985, 1330),
    "bumble_r": ("bumble/bumble_r1.png", "sticker", 900, 380, 985, 1330),
    "bumble_e": ("bumble/bumble_e2.png", "sticker", 900, 380, 985, 1330),
    "bumble_l": ("bumble/bumble_l1.png", "glow", 900, 380, 985, 1330),
    "ribbi": ("ribbi/ribbi_c1.png", "sticker", 900, 380, 985, 1330),
    "ribbi_u": ("ribbi/ribbi_u1.png", "sticker", 900, 380, 985, 1330),
    "ribbi_r": ("ribbi/ribbi_r2.png", "sticker", 900, 380, 985, 1330),
    "ribbi_e": ("ribbi/ribbi_e2.png", "sticker", 900, 380, 985, 1330),
    "ribbi_l": ("ribbi/ribbi_l2.png", "sticker", 900, 380, 985, 1330),
    "dandy": ("dandy/dandy_c4.png", "mix", 900, 380, 985, 1330),
    "dandy_u": ("dandy/dandy_u1.png", "mix", 900, 380, 985, 1330),
    "dandy_r": ("dandy/dandy_r1.png", "mix", 900, 380, 985, 1330),
    "dandy_e": ("dandy/dandy_e1.png", "mix", 900, 380, 985, 1330),
    "dandy_l": ("dandy/dandy_l2.png", "mix", 900, 380, 985, 1330),
    "pip": ("pip/pip_c1.png", "sticker", 900, 380, 985, 1330),
    "pip_u": ("pip/pip_u2.png", "sticker", 900, 380, 985, 1330),
    "pip_r": ("pip/pip_r2.png", "sticker", 900, 380, 985, 1330),
    "pip_e": ("pip/pip_e2.png", "sticker", 900, 380, 985, 1330),
    "pip_l": ("pip/pip_l2.png", "sticker", 900, 380, 985, 1330),
    # Region 2 (F4.2): Capi Rare / Elite / Legendary and Nib, Spike, Moss, Fenn
    "forestgirl_r": ("capi/capi_r2.png", "sticker", 900, 380, 985, 1330),
    "forestgirl_e": ("capi/capi_e1.png", "glow", 900, 380, 985, 1330),
    "forestgirl_l": ("capi/capi_l1.png", "glow", 900, 380, 985, 1330),
    "nib": ("nib/nib_c1.png", "sticker", 900, 380, 985, 1330),
    "nib_u": ("nib/nib_u2.png", "sticker", 900, 380, 985, 1330),
    "nib_r": ("nib/nib_r1.png", "sticker", 900, 380, 985, 1330),
    "nib_e": ("nib/nib_e1.png", "sticker", 900, 380, 985, 1330),
    "nib_l": ("nib/nib_l2.png", "glow", 900, 380, 985, 1330),
    "spike": ("spike/spike_c2.png", "sticker", 900, 380, 985, 1330),
    "spike_u": ("spike/spike_u1.png", "sticker", 900, 380, 985, 1330),
    "spike_r": ("spike/spike_r2.png", "sticker", 900, 380, 985, 1330),
    "spike_e": ("spike/spike_e1.png", "sticker", 900, 380, 985, 1330),
    "spike_l": ("spike/spike_l1.png", "glow", 900, 380, 985, 1330),
    "moss": ("moss/moss_c3.png", "sticker", 900, 380, 985, 1330),
    "moss_u": ("moss/moss_u2.png", "sticker", 900, 380, 985, 1330),
    "moss_r": ("moss/moss_r1.png", "sticker", 900, 380, 985, 1330),
    "moss_e": ("moss/moss_e1.png", "sticker", 900, 380, 985, 1330),
    "moss_l": ("moss/moss_l1.png", "glow", 900, 380, 985, 1330),
    "fenn": ("fenn/fenn_c1.png", "sticker", 900, 380, 985, 1330),
    "fenn_u": ("fenn/fenn_u2.png", "sticker", 900, 380, 985, 1330),
    "fenn_r": ("fenn/fenn_r1.png", "trim20", 900, 380, 985, 1330),
    "fenn_e": ("fenn/fenn_e2.png", "trim20", 900, 380, 985, 1330),
    "fenn_l": ("fenn/fenn_l1.png", "trim14h", 900, 380, 985, 1330),
}
PART_NAMES = ("back", "legL", "legR", "body", "handL", "handR", "head")
ORDER = ["legL", "legR", "body", "handL", "handR", "head"]


def _bg_colour(rgb):
    corners = np.concatenate([rgb[:40, :40].reshape(-1, 3), rgb[:40, -40:].reshape(-1, 3), rgb[-40:, :40].reshape(-1, 3), rgb[-40:, -40:].reshape(-1, 3)])
    return np.median(corners, axis=0)


def key_sticker(rgb, ring_depth=34, ring_min=165, wall_px=7, ring_rb=255, ring_sat=48):
    """Silhouette without the white sticker rim. The outside is the flat grey (colour distance) plus every bright, weakly coloured pixel within ring_depth px of it that is
    connected to it; the painting's own outline (dark or saturated pixels, slightly thickened so thin gaps stay closed) stops the flood. Returns a boolean character mask."""
    bgc = _bg_colour(rgb)
    d = np.abs(rgb.astype(np.int16) - bgc.astype(np.int16)).max(axis=2)
    s0 = (d > 30).astype(np.uint8)
    s0 = cv2.morphologyEx(s0, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
    outside0 = (s0 == 0)
    mx, mn = rgb.max(axis=2).astype(np.int16), rgb.min(axis=2).astype(np.int16)
    rb = rgb[..., 0].astype(np.int16) - rgb[..., 2].astype(np.int16)
    ringlike = ((mn >= ring_min) & ((mx - mn) < ring_sat) & (rb < ring_rb)).astype(np.uint8)
    wall = cv2.dilate(((1 - ringlike) * (~outside0)).astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (wall_px, wall_px)))   # the outside itself is never a wall
    dist = cv2.distanceTransform((~outside0).astype(np.uint8), cv2.DIST_L2, 3)   # distance to the nearest outside pixel
    ring = ringlike & (wall == 0) & (dist <= ring_depth)
    free = (outside0 | (ring > 0)).astype(np.uint8)
    n, lab = cv2.connectedComponents(free, connectivity=4)
    h, w = free.shape
    ids = {lab[2, 2], lab[2, w - 3], lab[h - 3, 2], lab[h - 3, w - 3]} - {0}
    out = np.isin(lab, list(ids))
    # the flood stopped half a wall short of the outline on every side: ring-coloured pixels in that strip still belong to the rim (the outline itself is not ring-coloured)
    near_out = cv2.dilate(out.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (wall_px + 3, wall_px + 3))) > 0
    out = out | (near_out & (ringlike > 0))
    ch = (~out).astype(np.uint8)
    n, lab2, st, _ = cv2.connectedComponentsWithStats(ch, 8)
    big = st[1:, cv2.CC_STAT_AREA].max()
    ch = np.isin(lab2, [i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] > max(3000, big * 0.01)]).astype(np.uint8)
    ch = cv2.morphologyEx(ch, cv2.MORPH_CLOSE, np.ones((7, 7), np.uint8))
    inv = (1 - ch).astype(np.uint8)
    nl, ll = cv2.connectedComponents(inv, connectivity=4)
    border = set(ll[0, :].tolist()) | set(ll[-1, :].tolist()) | set(ll[:, 0].tolist()) | set(ll[:, -1].tolist())
    border.discard(0)
    ch = (~np.isin(ll, list(border))).astype(np.uint8)   # fill holes: whatever the outside cannot reach
    ch = cv2.morphologyEx(ch, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
    return cv2.erode(ch, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3))) > 0


def key_trim(rgb, trim):
    """Momo: the white-and-mauve sticker rim is as wide everywhere, and white fur / white cloud are the same colour as the rim, so colour cannot tell them apart.
    Silhouette (colour distance from the grey, holes filled) eroded by the rim width `trim` px, which lands on the painting's thin brown outline."""
    bgc = _bg_colour(rgb)
    d = np.abs(rgb.astype(np.int16) - bgc.astype(np.int16)).max(axis=2)
    a = cv2.morphologyEx((d > 30).astype(np.uint8), cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
    n, lab, st, _ = cv2.connectedComponentsWithStats(a, 8)
    big = st[1:, cv2.CC_STAT_AREA].max()
    a = np.isin(lab, [i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] > max(3000, big * 0.05)]).astype(np.uint8)
    inv = (1 - a).astype(np.uint8)
    nl, ll = cv2.connectedComponents(inv, connectivity=4)
    border = set(ll[0, :].tolist()) | set(ll[-1, :].tolist()) | set(ll[:, 0].tolist()) | set(ll[:, -1].tolist())
    border.discard(0)
    a = (~np.isin(ll, list(border))).astype(np.uint8)
    return cv2.erode(a, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * trim + 1, 2 * trim + 1))) > 0


def halo_of(rgb, ch):
    """The warm glow of light rays around Legendary Momo's head, outside the character: soft alpha from how much warmer and brighter than the grey ground it is
    (the formula of cut_polygon_character.key_flood), and its colour. Returns (alpha 0..1, rgb uint8)."""
    bgc = _bg_colour(rgb).astype(np.float32)
    r, b = rgb[..., 0].astype(np.float32), rgb[..., 2].astype(np.float32)
    away = cv2.dilate(ch.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9))) == 0
    a = np.clip((r - bgc[0] - 28.0) / 110.0, 0, 1) * np.clip((r - b - 12.0) / 30.0, 0, 1) * away
    a = cv2.GaussianBlur(a, (0, 0), 1.2)
    # the 9 px band next to the character carries its fringe colours, not glow: fill it from the glow just outside so no see-through ring is left around the outline
    band = (cv2.dilate(ch.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (13, 13))) > 0) & ~ch
    a = np.where(band, cv2.dilate(a, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (19, 19))) * 0.9, a)
    a[ch] = 0
    colour = np.where(a[..., None] > 0.02, np.clip((rgb.astype(np.float32) - bgc * (1 - a[..., None])) / np.maximum(a[..., None], 0.25), 0, 255), np.array([255, 225, 150], np.float32))
    return a, colour.astype(np.uint8)


def rgb_max(rgb):
    return rgb.max(axis=2)


def key_soft(rgb):
    """Dandy: no sticker rim, a soft light glow around the puff. Silhouette = colour distance from the grey above a stricter threshold, largest blobs, holes filled."""
    bgc = _bg_colour(rgb)
    d = np.abs(rgb.astype(np.int16) - bgc.astype(np.int16)).max(axis=2)
    a = (d > 48).astype(np.uint8)
    a = cv2.morphologyEx(a, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
    n, lab, st, _ = cv2.connectedComponentsWithStats(a, 8)
    big = st[1:, cv2.CC_STAT_AREA].max()
    a = np.isin(lab, [i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] > max(3000, big * 0.01)]).astype(np.uint8)
    a = cv2.morphologyEx(a, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
    inv = (1 - a).astype(np.uint8)
    nl, ll = cv2.connectedComponents(inv, connectivity=4)
    border = set(ll[0, :].tolist()) | set(ll[-1, :].tolist()) | set(ll[:, 0].tolist()) | set(ll[:, -1].tolist())
    border.discard(0)
    return ~np.isin(ll, list(border))


def register_fit(alpha):
    """Like cut_polygon_character.register (height 60..1605, feet centred on x 682) but the scale is also reduced until the whole silhouette, capes and hats
    included, stays 20 px inside the 1365 px canvas width (nothing clipped, CUX-9)."""
    ys, xs = np.where(alpha)
    y0, y1 = ys.min(), ys.max()
    s = (1605 - 60) / float(y1 - y0)
    foot = alpha[int(y1 - (y1 - y0) * 0.10):y1 + 1]
    fy, fx = np.where(foot)
    xf = fx.mean() if len(fx) else (xs.min() + xs.max()) / 2
    reach = 682 - 22
    s = min(s, reach / max(1.0, xf - xs.min()), reach / max(1.0, xs.max() - xf))
    tx = 682 - xf * s
    ty = 1605 - y1 * s
    A = np.array([[1 / s, 0, -tx / s], [0, 1 / s, -ty / s]], np.float32)   # canvas -> picture
    return 1.0, A


def zones(head_y, xl, xr, leg_y):
    return {
        "head": ("rect", (0, 0, W, head_y)),
        "handL": ("rect", (0, head_y, xl, leg_y)),
        "handR": ("rect", (xr, head_y, W, leg_y)),
        "legL": ("rect", (0, leg_y, 683, H)),
        "legR": ("rect", (683, leg_y, W, H)),
        "body": ("rect", (0, 0, W, H)),
    }


def cut(src, outdir, mode, head_y, xl, xr, leg_y, debug=False):
    img = cv2.cvtColor(cv2.imread(src), cv2.COLOR_BGR2RGB)
    halo_a = halo_rgb = None
    if mode == "soft":
        alpha = key_soft(img)
    elif mode == "mix":   # Dandy: the puff has a soft glow edge (soft key), the body below it has a thin white sticker rim (sticker key)
        soft = key_soft(img)
        ys, _ = np.where(soft)
        ycut = ys.min() + 0.56 * (ys.max() - ys.min())
        upper = np.arange(soft.shape[0])[:, None] < ycut
        # above the cut: the puff is bright (the mauve shade of the glow behind it is not), and its outer 11 px carry the soft rim
        bright = cv2.morphologyEx((rgb_max(img) >= 165).astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8)) > 0
        puff = soft & bright & (cv2.erode(soft.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (23, 23))) > 0)
        puff = cv2.morphologyEx(puff.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((15, 15), np.uint8)) > 0
        n, lab, st, _ = cv2.connectedComponentsWithStats(puff.astype(np.uint8), 8)
        puff = np.isin(lab, [i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] > 20000])
        inv = (~puff).astype(np.uint8)   # eyes and dark details inside the puff are not bright: fill every hole the outside cannot reach
        nl, ll = cv2.connectedComponents(inv, connectivity=4)
        border = set(ll[0, :].tolist()) | set(ll[-1, :].tolist()) | set(ll[:, 0].tolist()) | set(ll[:, -1].tolist())
        border.discard(0)
        puff = ~np.isin(ll, list(border))
        alpha = soft & np.where(upper, puff, key_sticker(img, 24))
        n, lab, st, _ = cv2.connectedComponentsWithStats(alpha.astype(np.uint8), 8)
        alpha = np.isin(lab, [i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] > 3000])
    elif mode == "glow":   # a warm glow around the character (Bumble Legendary): the glow is a soft back piece, not part of the opaque character
        alpha = key_sticker(img, 70, 120, 7, 255, 140)
        halo_a, halo_rgb = halo_of(img, alpha)
    elif mode.startswith("trim"):   # "trim36" or "trim33h" (h = keep the light-ray halo as the back piece)
        alpha = key_trim(img, int(mode[4:6]))
        if mode.endswith("h"):
            halo_a, halo_rgb = halo_of(img, alpha)
    else:
        alpha = key_sticker(img)
    cc, Af = register_fit(alpha)
    # colour bleed: transparent pixels take the colour of the nearest character pixel (no grey key halo after resampling)
    _, near = cv2.distanceTransformWithLabels((~alpha).astype(np.uint8), cv2.DIST_L2, 3, labelType=cv2.DIST_LABEL_PIXEL)
    ys0, xs0 = np.where(alpha)
    lab2xy = np.zeros((near.max() + 1, 2), np.int32)
    lab_at = near[ys0, xs0]
    lab2xy[lab_at, 0] = ys0
    lab2xy[lab_at, 1] = xs0
    img = np.where(alpha[..., None], img, img[lab2xy[near, 0], lab2xy[near, 1]])
    rgba = np.dstack([img, (alpha * 255).astype(np.uint8)])
    canvas = cv2.warpAffine(rgba, Af, (W, H), flags=cv2.INTER_LANCZOS4 | cv2.WARP_INVERSE_MAP, borderValue=(0, 0, 0, 0))
    chars = canvas[..., 3] > 8
    z = zones(head_y, xl, xr, leg_y)
    label = np.full((H, W), "", dtype=object)
    claimed = np.zeros((H, W), bool)
    parts = {k: np.zeros((H, W, 4), np.uint8) for k in PART_NAMES}
    for n in ("head", "handL", "handR", "legL", "legR", "body"):
        sel = (cp.poly_mask(*z[n]) > 0) & ~claimed & chars
        parts[n][sel] = canvas[sel]
        claimed |= sel
    order = ORDER
    if halo_a is not None:   # the glow of light rays behind the head goes in the back piece (soft alpha), drawn first
        hr = np.dstack([halo_rgb, (halo_a * 255).astype(np.uint8)])
        hc = cv2.warpAffine(hr, Af, (W, H), flags=cv2.INTER_LINEAR | cv2.WARP_INVERSE_MAP, borderValue=(255, 225, 150, 0))
        ha = hc[..., 3].astype(np.float32) / 255.0
        ha[chars] = 0
        parts["back"] = np.dstack([hc[..., :3], (ha * 255).astype(np.uint8)])
        order = ["back"] + ORDER
        canvas = canvas.copy()
        canvas[..., :3] = np.where((ha > 0.01)[..., None], hc[..., :3], canvas[..., :3])
        canvas[..., 3] = np.maximum(canvas[..., 3], (ha * 255).astype(np.uint8))
    os.makedirs(outdir, exist_ok=True)
    for k, v in parts.items():
        if k not in order:   # an empty back piece is not written (it would only cost build size)
            stale = os.path.join(outdir, k + ".png")
            if os.path.exists(stale):
                os.remove(stale)
            continue
        cv2.imwrite(os.path.join(outdir, k + ".png"), cv2.cvtColor(v, cv2.COLOR_RGBA2BGRA))
    json.dump({"id": os.path.basename(outdir.rstrip("/\\")), "canvas": [W, H], "order": order}, open(os.path.join(outdir, "rig.json"), "w"), indent=2)
    if debug:
        a = canvas[..., 3:4] / 255.0
        bg = np.full((H, W, 3), (70, 150, 70), np.float32)
        out = (canvas[..., :3] * a + bg * (1 - a)).astype(np.uint8)
        for y in range(0, H, 100):
            cv2.line(out, (0, y), (W, y), (255, 255, 255) if y % 500 else (255, 0, 0), 1)
            cv2.putText(out, str(y), (4, y - 3), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 0, 0), 2)
        for x in range(0, W, 100):
            cv2.line(out, (x, 0), (x, H), (255, 255, 255) if x % 500 else (255, 0, 0), 1)
            cv2.putText(out, str(x), (x + 3, 20), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 0, 0), 2)
        for (x0, y0, x1, y1) in ((0, head_y, W, head_y), (xl, head_y, xl, leg_y), (xr, head_y, xr, leg_y), (0, leg_y, W, leg_y)):
            cv2.line(out, (x0, y0), (x1, y1), (255, 0, 255), 3)
        cv2.imwrite(os.path.join(outdir, "_debug.png"), cv2.cvtColor(out, cv2.COLOR_RGB2BGR))
    return {k: int((v[..., 3] > 8).sum()) for k, v in parts.items()}


def main():
    args = sys.argv[1:]
    only = args[args.index("--only") + 1].split(",") if "--only" in args else list(HEROES)
    out_root = args[args.index("--out") + 1] if "--out" in args else os.path.join(ROOT, "Assets", "Resources", "Characters")
    debug = "--debug" in args
    for hid in only:
        f, mode, hy, xl, xr, ly = HEROES[hid]
        d = os.path.join(out_root, hid)
        print(hid, cut(os.path.join(CAND, f), d, mode, hy, xl, xr, ly, debug))


if __name__ == "__main__":
    main()
