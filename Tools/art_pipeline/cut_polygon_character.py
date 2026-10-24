"""Cuts a character painted on the assembled polygon doll into the standard parts (docs/19).

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/cut_polygon_character.py <painted.png> <out_dir> [--bg 128]

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
PRIORITY = ["head", "handL", "handR", "footL", "footR", "legL", "legR", "body"]
OUT_PARTS = {"head": "head", "handL": "handL", "handR": "handR", "footL": "legL", "legL": "legL", "footR": "legR", "legR": "legR", "body": "body"}
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")


def poly_mask(kind, box):
    m = np.zeros((H, W), np.uint8)
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


def refine_to_doll(alpha, A):
    """ECC refinement of the canvas->picture map so the character's silhouette sits on the doll polygons (affine, small corrections)."""
    ref = np.zeros((H, W), np.uint8)
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
    if zones == "doll":
        POLY = POLY_DOLL
        cc, Af = refine_to_doll(alpha, Af)
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
    back = outside & (np.arange(W)[None, :] > 940) & (np.arange(H)[:, None] > 760)
    label[outside] = nearest[outside]
    os.makedirs(out, exist_ok=True)
    parts = {k: np.zeros((H, W, 4), np.uint8) for k in ("back", "legL", "legR", "body", "handL", "handR", "head")}
    for i, n in enumerate(names):
        sel = chars & (label == i) & ~back
        tgt = OUT_PARTS[n]
        parts[tgt][sel] = canvas[sel]
    parts["back"][back] = canvas[back]
    for k, v in parts.items():
        cv2.imwrite(os.path.join(out, k + ".png"), cv2.cvtColor(v, cv2.COLOR_RGBA2BGRA))
    json.dump({"canvas": [W, H], "order": ["back", "legL", "legR", "body", "handL", "handR", "head"], "ecc": float(cc)},
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
