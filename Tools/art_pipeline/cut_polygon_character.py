"""Cuts a character painted on the assembled polygon doll into the standard parts (docs/19).

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/cut_polygon_character.py <painted.png> <out_dir> [--bg 128] [--zones momo|spore|doll|forest|otter|monk|candy] [--ref doll_slim.png] [--no-back]

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
# props painted behind the character that become the "back" piece: zone set -> polygon on the doll canvas; inside it every cane-coloured pixel (red, white, and the dark outline touching them) is "back"
BACK_PROP = {
    "candy": [(100, 480), (360, 480), (360, 745), (450, 745), (520, 830), (515, 880), (478, 892), (448, 925), (436, 950), (100, 950)],
}
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
    if ref_img is not None:   # a doll variant (e.g. doll_slim.png, magenta background): register onto its silhouette instead of the polygons
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
    alpha = key_background(img)
    cc, Af = register(alpha)
    if zones == "spore":
        POLY = POLY_SPORE
        cc, Af = refine_to_doll(alpha, Af)
    if zones in ZONE_SETS:
        POLY = ZONE_SETS[zones]
        refimg = sys.argv[sys.argv.index("--ref") + 1] if "--ref" in sys.argv else None
        cc, Af = refine_to_doll(alpha, Af, refimg)
    if zones == "doll":
        POLY = POLY_DOLL
        refimg = sys.argv[sys.argv.index("--ref") + 1] if "--ref" in sys.argv else None
        cc, Af = refine_to_doll(alpha, Af, refimg)
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
    if zones in BACK_PROP:
        hsv = cv2.cvtColor(np.ascontiguousarray(canvas[..., :3]), cv2.COLOR_RGB2HSV)
        hh, ss, vv = hsv[..., 0].astype(int), hsv[..., 1].astype(int), hsv[..., 2].astype(int)
        red = ((hh < 7) | (hh > 168)) & (ss > 120) & (vv > 90)
        white = (ss < 38) & (vv > 205)
        dark = vv < 120
        pm = poly_mask("poly", BACK_PROP[zones]) > 0
        seed = (red | white) & pm & chars
        seed = cv2.morphologyEx(seed.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8)) > 0
        near_seed = cv2.dilate(seed.astype(np.uint8), np.ones((9, 9), np.uint8)) > 0
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
