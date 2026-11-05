"""Cuts a character painted on the assembled polygon doll into the standard parts (docs/19).

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/cut_polygon_character.py <painted.png> <out_dir> [--bg 128] [--zones momo|momo_u|spore|doll|forest|otter|monk|candy|candy_u] [--ref doll_slim.png] [--no-back] [--outline-key]

Input: the OpenArt image (character on a flat #808080 background, same pose as Tools/art_pipeline/make_generic_mannequin.py's doll).
Steps: key the background, register the picture to the 1365 x 1649 doll canvas (affine ECC on the silhouettes), then give every character pixel to one
polygon part: inside a polygon first (priority head, hands, feet, legs, body), outside every polygon: right of x 1010 = back (tail), else the nearest
polygon. Writes back/legL/legR/body/handL/handR/head.png, each a full-canvas transparent PNG at its assembled position (stacking them gives the
original), plus rig.json.
"""
import json
import os
import sys
import numpy as np
import cv2

W, H = 1365, 1649
# the standard polygons (assembled doll): name -> (kind, box)
POLY = {
    "head":  ("ellipse", (120, 40, 1010, 830)),
    "handL": ("round",   (240, 850, 405, 1180)),
    "handR": ("round",   (865, 850, 1060, 1180)),
    "footL": ("ellipse", (350, 1470, 660, 1640)),
    "footR": ("ellipse", (680, 1470, 1010, 1640)),
    "legL":  ("round",   (380, 1230, 640, 1560)),
    "legR":  ("round",   (700, 1230, 990, 1560)),
    "body":  ("round",   (400, 790, 870, 1260)),
}
POLY_DOLL = {   # the assembled grey doll itself (make_generic_mannequin.py): used for characters painted on the doll
    "head":  ("ellipse", (160, 120, 1205, 1060)),
    "handL": ("round",   (250, 1000, 420, 1330)),
    "handR": ("round",   (945, 1000, 1115, 1330)),
    "footL": ("ellipse", (420, 1470, 690, 1600)),
    "footR": ("ellipse", (675, 1470, 945, 1600)),
    "legL":  ("round",   (470, 1180, 650, 1520)),
    "legR":  ("round",   (715, 1180, 895, 1520)),
    "body":  ("round",   (390, 940, 975, 1320)),
}
POLY_SPORE = {   # zones measured on the registered Sporeling painting: plain partition lines (chin line, arm/torso lines, hip line)
    "head":  ("rect", (0, 0, 1365, 960)),
    "handL": ("rect", (0, 900, 395, 1500)),
    "handR": ("rect", (975, 900, 1365, 1500)),
    "body":  ("rect", (395, 960, 975, 1320)),
    "footL": ("rect", (395, 1320, 682, 1649)),
    "footR": ("rect", (682, 1320, 975, 1649)),
    "legL":  ("rect", (395, 1320, 682, 1649)),
    "legR":  ("rect", (682, 1320, 975, 1649)),
}
def _zones(head_y, armL, armR, leg_y, split_x=683):
    """Partition zones measured on a registered painting: head above head_y, two arm polygons (sleeve + hand), legs below leg_y split at split_x, the body is everything else."""
    return {
        "head":  ("rect", (0, 0, 1365, head_y)),
        "handL": ("poly", armL),
        "handR": ("poly", armR),
        "footL": ("rect", (0, 0, 1, 1)),
        "footR": ("rect", (0, 0, 1, 1)),
        "legL":  ("rect", (0, leg_y, split_x, 1649)),
        "legR":  ("rect", (split_x, leg_y, 1365, 1649)),
        "body":  ("rect", (0, 0, 1365, 1649)),
    }


ZONE_SETS = {   # registered paintings of the region heroes (T66): hats, caps, puffed sleeves and fur overhang the doll polygons, so plain partition lines are used (like POLY_SPORE)
    "forest": _zones(895, [(150, 880), (520, 880), (512, 1090), (395, 1290), (385, 1340), (150, 1340)],
                     [(850, 880), (1365, 880), (1365, 1340), (985, 1340), (975, 1260), (860, 1095)], 1378),
    "otter":  _zones(905, [(150, 880), (430, 880), (405, 1100), (405, 1360), (150, 1360)],
                     [(905, 880), (1365, 880), (1365, 1360), (950, 1360), (945, 1100)], 1372),
    "candy":  _zones(848, [(150, 848), (548, 848), (548, 1010), (470, 1060), (440, 1150), (432, 1290), (425, 1340), (150, 1340)],
                     [(825, 848), (1365, 848), (1365, 1340), (935, 1340), (930, 1290), (935, 1150), (900, 1060), (825, 1010)], 1335),
    "monk":   _zones(925, [(150, 900), (500, 900), (467, 1007), (433, 1153), (425, 1340), (150, 1340)],
                     [(840, 900), (1365, 900), (1365, 1340), (930, 1340), (924, 1160), (887, 1007)], 1405),
}
ZONE_SETS["momo_u"] = {   # Cloudguard Momo: shoulder guards stay with the arms, the chest plate with the body; the cape and the cloud tail become "back" (CAPE_BACK below)
    "head":  ("rect", (0, 0, 1010, 905)),
    "handL": ("poly", [(95, 985), (300, 985), (300, 1280), (262, 1300), (262, 1342), (95, 1342)]),
    "handR": ("poly", [(765, 990), (870, 990), (955, 1105), (985, 1200), (985, 1342), (830, 1342), (805, 1300), (805, 1150), (770, 1130)]),
    "footL": ("rect", (0, 0, 1, 1)),
    "footR": ("rect", (0, 0, 1, 1)),
    "legL":  ("rect", (0, 1330, 545, 1649)),
    "legR":  ("rect", (545, 1330, 1365, 1649)),
    "body":  ("rect", (285, 905, 805, 1342)),
}
# zone sets whose cape / cloud (blue pixels, plus the dark outline that touches only them) and everything outside the part polygons form the "back" piece; chest core = blue allowed (cloud badge)
CAPE_BACK = {"momo_u": {"keep_core": (320, 1010, 780, 1310)}}
# props painted behind the character that become the "back" piece: zone set -> polygon on the doll canvas; inside it every cane-coloured pixel (red, white, and the dark outline touching them) is "back"
BACK_PROP = {
    "candy": [(100, 480), (360, 480), (360, 745), (450, 745), (520, 830), (515, 880), (478, 892), (448, 925), (436, 950), (100, 950)],
}
ZONE_SETS["candy_u"] = ZONE_SETS["candy"]   # Sugarplate Bonbon: same pose; the longer candy cane gets a wider back polygon and a thicker outline reach
BACK_PROP["candy_u"] = [(100, 480), (440, 480), (440, 745), (520, 830), (515, 880), (478, 892), (448, 925), (436, 950), (100, 950)]
BACK_DILATE = {"candy": 9, "candy_u": 19}
PRIORITY = ["head", "handL", "handR", "footL", "footR", "legL", "legR", "body"]
OUT_PARTS = {"head": "head", "handL": "handL", "handR": "handR", "footL": "legL", "legL": "legL", "footR": "legR", "legR": "legR", "body": "body"}
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")


def poly_mask(kind, box):
    m = np.zeros((H, W), np.uint8)
    if kind == "poly":
        cv2.fillPoly(m, [np.array(box, np.int32)], 255)
        return m
    x0, y0, x1, y1 = [int(v) for v in box]
    if kind == "rect":
        cv2.rectangle(m, (int(box[0]), int(box[1])), (int(box[2]), int(box[3])), 255, -1)
    elif kind == "ellipse":
        cv2.ellipse(m, ((x0 + x1) // 2, (y0 + y1) // 2), ((x1 - x0) // 2, (y1 - y0) // 2), 0, 0, 360, 255, -1)
    else:
        r = min(x1 - x0, y1 - y0) // 2
        cv2.rectangle(m, (x0 + r, y0), (x1 - r, y1), 255, -1)
        cv2.rectangle(m, (x0, y0 + r), (x1, y1 - r), 255, -1)
        for cx, cy in ((x0 + r, y0 + r), (x1 - r, y0 + r), (x0 + r, y1 - r), (x1 - r, y1 - r)):
            cv2.circle(m, (cx, cy), r, 255, -1)
    return m


def key_background(rgb, bg=None):
    """Key a flat background (colour sampled from the four corners) and keep the largest blobs."""
    h, w = rgb.shape[:2]
    corners = np.concatenate([rgb[:40, :40].reshape(-1, 3), rgb[:40, -40:].reshape(-1, 3), rgb[-40:, :40].reshape(-1, 3), rgb[-40:, -40:].reshape(-1, 3)])
    bgc = np.median(corners, axis=0)
    d = np.abs(rgb.astype(np.int16) - bgc.astype(np.int16)).max(axis=2)
    alpha = (d > 38).astype(np.uint8)
    # a darker flat-grey ground shadow under the feet is not part of the character: drop big grey (unsaturated, mid-value) blobs
    mx, mn = rgb.max(axis=2).astype(np.int16), rgb.min(axis=2).astype(np.int16)
    grey = ((mx - mn) < 16) & (mx > 70) & (mx < 200)
    gn, glab, gst, _ = cv2.connectedComponentsWithStats(cv2.morphologyEx(grey.astype(np.uint8), cv2.MORPH_OPEN, np.ones((7, 7), np.uint8)), 8)
    for i in range(1, gn):
        if gst[i, cv2.CC_STAT_AREA] > 4000:
            alpha[glab == i] = 0
    alpha = cv2.morphologyEx(alpha, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
    n, lab, st, _ = cv2.connectedComponentsWithStats(alpha, 8)
    keep = np.zeros_like(alpha)
    big = st[1:, cv2.CC_STAT_AREA].max() if n > 1 else 0
    for i in range(1, n):
        if st[i, cv2.CC_STAT_AREA] > max(3000, big * 0.01):
            keep[lab == i] = 1
    keep = cv2.morphologyEx(keep, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
    return keep.astype(bool)


def key_outline(rgb):
    """For paintings with a light halo ring around the outline and silver (grey) parts on the character: the background is the flat grey plus the halo, flooded from the corners
    through pixels that are unsaturated mid-grey (value 80..190); the dark outline stops the flood, so grey armour inside it survives. Used with --outline-key."""
    h, w = rgb.shape[:2]
    mx, mn = rgb.max(axis=2).astype(np.int16), rgb.min(axis=2).astype(np.int16)
    free = (((mx - mn) < 16) & (mx >= 80) & (mx <= 190)).astype(np.uint8)
    free = cv2.morphologyEx(free, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    n, lab = cv2.connectedComponents(free, connectivity=4)
    bgl = {lab[2, 2], lab[2, w - 3], lab[h - 3, 2], lab[h - 3, w - 3]} - {0}
    bg = np.isin(lab, list(bgl))
    bg = cv2.dilate(bg.astype(np.uint8), np.ones((3, 3), np.uint8)) > 0   # the antialiased fringe between halo and outline
    keep = (~bg).astype(np.uint8)
    n, lab2, st, _ = cv2.connectedComponentsWithStats(keep, 8)
    big = st[1:, cv2.CC_STAT_AREA].max()
    out = np.isin(lab2, [i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] > max(3000, big * 0.01)])
    out = cv2.morphologyEx(out.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
    # remnants of the halo where the outline is missing: grey, unsaturated pixels within 30 px of the silhouette edge (silver armour sits deeper inside)
    deep = cv2.erode(out, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (61, 61)))
    halo = (((mx - mn) < 32) & (mx >= 95) & (mx <= 228) & (deep == 0)).astype(np.uint8)
    halo = cv2.morphologyEx(halo, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    halo = cv2.dilate(halo, np.ones((3, 3), np.uint8))
    nlab, llab = cv2.connectedComponents(((1 - out) | halo).astype(np.uint8), connectivity=8)   # only halo that touches the outside: no holes inside the fur
    out = (out.astype(bool) & (llab != llab[2, 2])).astype(np.uint8)
    out = cv2.morphologyEx(out, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
    return cv2.erode(out, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7))) > 0   # trims the light fringe outside the outline (no grey halo)


WALL = 31


def key_flood(rgb):
    """For paintings with a glowing halo of light rays behind the head (Sunlord Momo): the character is everything enclosed by its dark outline, found by flooding the OUTSIDE from the
    corners through every pixel that is not near-black; the sticker border, the grey ground and the halo are outside. Returns (character mask, halo alpha 0..1 or None, halo rgb).
    The halo is the warm bright glow outside the outline (soft alpha from how much brighter than the grey ground it is)."""
    h, w = rgb.shape[:2]
    v = rgb.max(axis=2)
    rr, bb = rgb[..., 0].astype(np.int16), rgb[..., 2].astype(np.int16)
    wall = cv2.dilate(((v <= 70) | ((rr - bb > 45) & (v < 130))).astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (WALL, WALL)))   # a thick wall closes gaps in the outline where the rays cross it
    free = ((v > 70) & (wall == 0)).astype(np.uint8)
    free = cv2.morphologyEx(free, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    n, lab = cv2.connectedComponents(free, connectivity=4)
    bgl = {lab[2, 2], lab[2, w - 3], lab[h - 3, 2], lab[h - 3, w - 3]} - {0}
    outside = np.isin(lab, list(bgl))
    ch = (~outside).astype(np.uint8)
    n, lab2, st, _ = cv2.connectedComponentsWithStats(ch, 8)
    big = st[1:, cv2.CC_STAT_AREA].max()
    ch = np.isin(lab2, [i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] > max(3000, big * 0.01)]).astype(np.uint8)
    ch = cv2.morphologyEx(ch, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
    ch = cv2.morphologyEx(ch, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
    ch = cv2.erode(ch, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (WALL, WALL)))   # undo the wall's growth: back to the outline's outer edge
    # fill holes (anything not reachable from the corners through the outside)
    inv = (1 - ch).astype(np.uint8)
    nl, ll = cv2.connectedComponents(inv, connectivity=4)
    border = set(ll[0, :].tolist()) | set(ll[-1, :].tolist()) | set(ll[:, 0].tolist()) | set(ll[:, -1].tolist())   # the character may touch the picture edge, so the outside can be several pieces
    border.discard(0)
    ch = (~np.isin(ll, list(border)) ).astype(np.uint8)
    corners = np.concatenate([rgb[:40, :40].reshape(-1, 3), rgb[:40, -40:].reshape(-1, 3), rgb[-40:, :40].reshape(-1, 3), rgb[-40:, -40:].reshape(-1, 3)])
    bgc = np.median(corners, axis=0).astype(np.float32)
    r, g, b = [rgb[..., i].astype(np.float32) for i in range(3)]
    away = cv2.dilate(ch, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9))) == 0   # keep clear of the outline's own fringe
    a = np.clip((r - bgc[0] - 28.0) / 110.0, 0, 1) * np.clip((r - b - 12.0) / 30.0, 0, 1)
    a = a * away
    a = cv2.GaussianBlur(a, (0, 0), 1.2)
    a[cv2.dilate(ch, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))) > 0] = 0
    colour = np.where(a[..., None] > 0.02, np.clip((rgb.astype(np.float32) - bgc * (1 - a[..., None])) / np.maximum(a[..., None], 0.25), 0, 255), np.array([255, 225, 150], np.float32))
    return ch > 0, a, colour.astype(np.uint8)


def register(alpha):
    """Uniform scale and translation to the doll canvas: character height from y 60 (ear tips) to 1605 (soles), feet centred on x 682."""
    ys, xs = np.where(alpha)
    y0, y1 = ys.min(), ys.max()
    s = (1605 - 60) / float(y1 - y0)
    foot = alpha[int(y1 - (y1 - y0) * 0.10):y1 + 1]
    fy, fx = np.where(foot)
    xf = fx.mean() if len(fx) else (xs.min() + xs.max()) / 2
    tx = 682 - xf * s
    ty = 1605 - y1 * s
    A = np.array([[1 / s, 0, -tx / s], [0, 1 / s, -ty / s]], np.float32)   # canvas -> picture
    return 1.0, A


def refine_to_doll(alpha, A, ref_img=None):
    """ECC refinement of the canvas->picture map so the character's silhouette sits on the doll polygons (affine, small corrections)."""
    ref = np.zeros((H, W), np.uint8)
    if ref_img is not None and os.path.isdir(ref_img):   # --ref <character folder>: register onto the union of that character's parts (e.g. the Common hero for its Uncommon tier)
        for f in os.listdir(ref_img):
            if f.endswith(".png") and f != "preview.png":
                im = cv2.imread(os.path.join(ref_img, f), cv2.IMREAD_UNCHANGED)
                if im is not None and im.ndim == 3 and im.shape[2] == 4:
                    ref = np.maximum(ref, ((im[..., 3] > 8) * 255).astype(np.uint8))
        ref = cv2.resize(ref, (W, H), interpolation=cv2.INTER_NEAREST)
    elif ref_img is not None:   # a doll variant (e.g. doll_slim.png, magenta background): register onto its silhouette instead of the polygons
        ri = cv2.imread(ref_img)
        ref = ((np.abs(ri.astype(np.int16) - np.array([255, 0, 255], np.int16)).max(axis=2) > 60) * 255).astype(np.uint8)
        ref = cv2.resize(ref, (W, H), interpolation=cv2.INTER_NEAREST)
    else:
        for n, (k, b) in POLY_DOLL.items():
            ref = np.maximum(ref, poly_mask(k, b))
    sc = 0.25
    a = cv2.GaussianBlur(cv2.resize(ref.astype(np.float32) / 255.0, None, fx=sc, fy=sc), (0, 0), 4)
    pic = cv2.GaussianBlur(cv2.resize(alpha.astype(np.float32), None, fx=sc, fy=sc), (0, 0), 4)
    M = A.copy()
    M[:, 2] *= sc
    try:
        cc, M = cv2.findTransformECC(a, pic, M.astype(np.float32), cv2.MOTION_AFFINE, (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_COUNT, 300, 1e-7), None, 5)
    except cv2.error:
        return 0.0, A
    M[:, 2] /= sc
    return cc, M


def main():
    global POLY
    src, out = sys.argv[1], sys.argv[2]
    zones = "momo"
    if "--zones" in sys.argv:
        zones = sys.argv[sys.argv.index("--zones") + 1]
    bgv = 128
    img = cv2.cvtColor(cv2.imread(src), cv2.COLOR_BGR2RGB)
    halo_a = halo_rgb = None
    if "--flood-key" in sys.argv:
        alpha, halo_a, halo_rgb = key_flood(img)
        if "--no-halo" in sys.argv: halo_a = None
    else:
        alpha = key_outline(img) if "--outline-key" in sys.argv else key_background(img)
    cc, Af = register(alpha)
    if zones in ("momo", "momo_u") and "--ref" in sys.argv and "--reg-from" not in sys.argv:
        cc, Af = refine_to_doll(alpha, Af, sys.argv[sys.argv.index("--ref") + 1])
    if "--reg-from" in sys.argv:   # take the registration of another sheet of the same pose (e.g. the Elite sheet for the Legendary one, whose halo rays would skew the silhouette fit)
        Af = np.array(json.load(open(sys.argv[sys.argv.index("--reg-from") + 1]))["A"], np.float32)
        cc = 1.0
    if zones == "spore":
        POLY = POLY_SPORE
        cc, Af = refine_to_doll(alpha, Af)
    if zones in ZONE_SETS and "--reg-from" not in sys.argv:
        POLY = ZONE_SETS[zones]
        refimg = sys.argv[sys.argv.index("--ref") + 1] if "--ref" in sys.argv else None
        cc, Af = refine_to_doll(alpha, Af, refimg)
    if zones == "doll":
        POLY = POLY_DOLL
        refimg = sys.argv[sys.argv.index("--ref") + 1] if "--ref" in sys.argv else None
        cc, Af = refine_to_doll(alpha, Af, refimg)
    if zones in ZONE_SETS: POLY = ZONE_SETS[zones]
    os.makedirs(out, exist_ok=True)
    json.dump({"A": Af.tolist()}, open(os.path.join(out, "reg.json"), "w"))
    # colour bleed: transparent pixels take the colour of the nearest character pixel, so the resampling never mixes the grey key colour into the edge (no grey halo)
    _, near = cv2.distanceTransformWithLabels((alpha == 0).astype(np.uint8), cv2.DIST_L2, 3, labelType=cv2.DIST_LABEL_PIXEL)
    ys0, xs0 = np.where(alpha > 0)
    lab2xy = np.zeros((near.max() + 1, 2), np.int32)
    # label ids of zero-distance (character) pixels map to their own coordinates
    lab_at = near[ys0, xs0]
    lab2xy[lab_at, 0] = ys0
    lab2xy[lab_at, 1] = xs0
    bleed = img[lab2xy[near, 0], lab2xy[near, 1]]
    img = np.where(alpha[..., None] > 0, img, bleed)
    rgba = np.dstack([img, (alpha * 255).astype(np.uint8)])
    canvas = cv2.warpAffine(rgba, Af, (W, H), flags=cv2.INTER_LANCZOS4 | cv2.WARP_INVERSE_MAP, borderValue=(0, 0, 0, 0))
    chars = canvas[..., 3] > 8
    # label every character pixel
    label = np.full((H, W), -1, np.int16)
    names = PRIORITY
    masks = {n: poly_mask(*POLY[n]) > 0 for n in names}
    for i, n in enumerate(names):
        sel = masks[n] & (label < 0)
        label[sel] = i
    outside = chars & (label < 0)
    # nearest polygon for the rest
    dists = []
    for n in names:
        d = cv2.distanceTransform((~masks[n]).astype(np.uint8), cv2.DIST_L2, 3)
        dists.append(d)
    nearest = np.argmin(np.stack(dists), axis=0)
    nb = "--no-back" in sys.argv   # characters without a tail: nothing is a "back" piece (it would steal the right arm), back.png stays empty
    back = outside & (np.arange(W)[None, :] > 940) & (np.arange(H)[:, None] > 760) & (not nb)
    label[outside] = nearest[outside]
    if zones in CAPE_BACK:
        hsv = cv2.cvtColor(np.ascontiguousarray(canvas[..., :3]), cv2.COLOR_RGB2HSV)
        hh, ss, vv = hsv[..., 0].astype(int), hsv[..., 1].astype(int), hsv[..., 2].astype(int)
        blue = (hh >= 85) & (hh <= 125) & (ss > 40) & (vv > 60)
        k = CAPE_BACK[zones]["keep_core"]
        core = np.zeros((H, W), bool); core[k[1]:k[3], k[0]:k[2]] = True
        headz = masks["head"].copy()
        headz[650:, 840:] = False   # the cloud tail behind the head (right of x 840, below y 650) is not head
        inany = np.zeros((H, W), bool)
        for n in names: inany |= masks[n]
        pale = (hh >= 85) & (hh <= 125) & (ss > 26) & (vv > 120)   # the pale highlights of the cloud tail behind the head
        pale[:650] = False; pale[:, :840] = False
        blue |= pale
        blue &= ~core & ~headz
        dark = vv < 95
        front = chars & ~blue & ~dark & inany
        near_front = cv2.dilate(front.astype(np.uint8), np.ones((13, 13), np.uint8)) > 0
        back = chars & ~headz & (blue | (inany & dark & ~near_front) | ~inany)
        back = cv2.morphologyEx(back.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8)) > 0
        back |= chars & ~headz & ~inany
    elif zones in BACK_PROP:
        hsv = cv2.cvtColor(np.ascontiguousarray(canvas[..., :3]), cv2.COLOR_RGB2HSV)
        hh, ss, vv = hsv[..., 0].astype(int), hsv[..., 1].astype(int), hsv[..., 2].astype(int)
        red = ((hh < 7) | (hh > 168)) & (ss > 120) & (vv > 90)
        white = (ss < 38) & (vv > 205)
        dark = vv < 120
        pm = poly_mask("poly", BACK_PROP[zones]) > 0
        seed = (red | white) & pm & chars
        seed = cv2.morphologyEx(seed.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8)) > 0
        nd = BACK_DILATE.get(zones, 9)
        near_seed = cv2.dilate(seed.astype(np.uint8), np.ones((nd, nd), np.uint8)) > 0
        back = pm & chars & near_seed & (seed | dark | red | white)
        back = cv2.morphologyEx(back.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8)) > 0
        back &= chars
        # drop specks (hair-outline fragments next to the cane) and open the thin dark slivers
        back = cv2.morphologyEx(back.astype(np.uint8), cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
        bn, blab, bst, _ = cv2.connectedComponentsWithStats(back, 8)
        back = np.isin(blab, [i for i in range(1, bn) if bst[i, cv2.CC_STAT_AREA] > 3000])
    os.makedirs(out, exist_ok=True)
    parts = {k: np.zeros((H, W, 4), np.uint8) for k in ("back", "legL", "legR", "body", "handL", "handR", "head")}
    for i, n in enumerate(names):
        sel = chars & (label == i) & ~back
        tgt = OUT_PARTS[n]
        parts[tgt][sel] = canvas[sel]
    parts["back"][back] = canvas[back]
    if halo_a is not None:   # the halo of light rays goes behind everything in the back piece (cape painted over it); soft alpha
        hr = np.dstack([halo_rgb, (halo_a * 255).astype(np.uint8)])
        hc = cv2.warpAffine(hr, Af, (W, H), flags=cv2.INTER_LINEAR | cv2.WARP_INVERSE_MAP, borderValue=(255, 225, 150, 0))
        ha = hc[..., 3].astype(np.float32) / 255.0
        ba = parts["back"][..., 3].astype(np.float32) / 255.0
        out_a = ba + ha * (1 - ba)
        col = (parts["back"][..., :3].astype(np.float32) * ba[..., None] + hc[..., :3].astype(np.float32) * (ha * (1 - ba))[..., None]) / np.maximum(out_a, 1e-3)[..., None]
        parts["back"] = np.dstack([col.clip(0, 255).astype(np.uint8), (out_a * 255).astype(np.uint8)])
    for k, v in parts.items():
        cv2.imwrite(os.path.join(out, k + ".png"), cv2.cvtColor(v, cv2.COLOR_RGBA2BGRA))
    json.dump({"canvas": [W, H], "order": ["back", "legL", "legR", "body", "handL", "handR", "head"] if (back.any()) else ["legL", "legR", "body", "handL", "handR", "head"], "ecc": float(cc)},
              open(os.path.join(out, "rig.json"), "w"), indent=2)
    full = np.zeros((H, W, 4), np.uint8)
    for k in ("back", "legL", "legR", "body", "handL", "handR", "head"):
        a = parts[k][..., 3:4] / 255.0
        full[..., :3] = (parts[k][..., :3] * a + full[..., :3] * (1 - a)).astype(np.uint8)
        full[..., 3] = np.maximum(full[..., 3], parts[k][..., 3])
    cv2.imwrite(os.path.join(out, "preview.png"), cv2.cvtColor(full, cv2.COLOR_RGBA2BGRA))
    print("ecc %.3f" % cc, "parts:", {k: int((v[..., 3] > 8).sum()) for k, v in parts.items()})


if __name__ == "__main__":
    main()
