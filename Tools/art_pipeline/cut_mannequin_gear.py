"""Cuts worn gear layers from sheets where a flat grey mannequin wears the pieces on a magenta background (docs/18 section B and C).

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/cut_mannequin_gear.py <input_dir_with_head.png_body.png_pants.png_boots.png> [out_dir]

For every input image (one piece per image, one mannequin per image):
  1. key: background = magenta, mannequin = low colour (grey), gear = everything else (navy outlines and any colour that is not grey),
  2. register the image to the reference mannequin (Art/_candidates/mannequin/mannequin_momo.png, the Momo canvas) with an affine ECC fit of the grey masks,
  3. write the gear alone as a transparent PNG on the Momo canvas (1365 x 1649): <slot>_<n>.png, plus a tight icon <slot>_<n>_icon.png.
Because the layer is on the Momo canvas it is drawn over Momo (or, through a rig, over any character built on the same template) at the same rect.
"""
import os
import sys
import numpy as np
import cv2

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
REF = os.path.join(ROOT, "Art", "_candidates", "mannequin", "mannequin_momo.png")


def masks(rgb):
    r, g, b = [rgb[..., i].astype(np.int16) for i in range(3)]
    mx = np.maximum(np.maximum(r, g), b)
    mn = np.minimum(np.minimum(r, g), b)
    lum = (r * 3 + g * 6 + b) // 10
    bg = (r > 170) & (b > 150) & (g < 140) & (r - g > 70) & (b - g > 50)
    neutral = ((mx - mn) < 24) & (lum > 40) & (lum < 215) & ~bg
    # gear = clearly coloured pixels (and cream trim), plus the dark navy outline only where it hugs such a colour; the mannequin's own
    # thin outline is navy too, but it is far from any gear colour, so it is dropped
    cream = (r > 205) & (g > 190) & (b > 150) & (r - b > 28) & ~bg
    core = (((mx - mn) >= 48) & ~bg) | cream
    core = cv2.morphologyEx(core.astype(np.uint8), cv2.MORPH_OPEN, np.ones((5, 5), np.uint8)).astype(bool)
    near = cv2.dilate(core.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (31, 31))).astype(bool)
    navy = (lum < 95) & ~bg & (b >= r)
    gear = core | (navy & near)
    return bg, neutral, gear


def clean(gear):
    k5 = np.ones((5, 5), np.uint8)
    g = gear.astype(np.uint8)
    opened = cv2.morphologyEx(g, cv2.MORPH_OPEN, k5)             # thin fringes between magenta and grey vanish
    n, lab, stats, _ = cv2.connectedComponentsWithStats(opened, 8)
    keep = np.zeros_like(opened)
    for i in range(1, n):
        if stats[i, cv2.CC_STAT_AREA] > 1500:
            keep[lab == i] = 1
    grown = cv2.dilate(keep, np.ones((7, 7), np.uint8))
    out = (grown > 0) & gear
    closed = cv2.morphologyEx(out.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
    # holes inside the piece (highlights that read as grey) belong to it
    inv = (1 - closed).astype(np.uint8)
    n2, lab2, st2, _ = cv2.connectedComponentsWithStats(inv, 4)
    for i in range(1, n2):
        if st2[i, cv2.CC_STAT_AREA] < 6000 and st2[i, cv2.CC_STAT_LEFT] > 0 and st2[i, cv2.CC_STAT_TOP] > 0:
            closed[lab2 == i] = 1
    return closed > 0


def register(img_neutral, ref_neutral, size_ref):
    """Affine map from image pixels to the reference canvas that best overlays the two mannequin masks."""
    sc = 0.25
    a = cv2.GaussianBlur(cv2.resize(ref_neutral.astype(np.float32), None, fx=sc, fy=sc), (0, 0), 3)
    b = cv2.GaussianBlur(cv2.resize(img_neutral.astype(np.float32), None, fx=sc, fy=sc), (0, 0), 3)
    best = None
    h, w = a.shape
    ih, iw = b.shape
    for s in (0.60, 0.68, 0.76, 0.84, 0.92):
        # image -> ref: ref = M * img ; start by centring the scaled image on the ref canvas
        m = np.array([[s, 0, (w - iw * s) / 2], [0, s, (h - ih * s) / 2]], np.float32)
        # findTransformECC wants the warp that maps TEMPLATE (ref) coordinates into the input (image) coordinates
        inv = cv2.invertAffineTransform(m)
        try:
            cc, warp = cv2.findTransformECC(a, b, inv.astype(np.float32), cv2.MOTION_AFFINE, (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_COUNT, 200, 1e-6), None, 5)
        except cv2.error:
            continue
        if best is None or cc > best[0]:
            best = (cc, warp)
    if best is None:
        raise SystemExit("registration failed")
    cc, warp = best
    full = warp.copy()
    full[:, 2] = full[:, 2] / sc    # translation back to full resolution (scale terms are unitless)
    return cc, full               # maps ref pixels to image pixels


def main():
    src = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "Art", "_candidates", "mannequin_test", "layers")
    os.makedirs(out, exist_ok=True)
    ref = cv2.cvtColor(cv2.imread(REF), cv2.COLOR_BGR2RGB)
    rh, rw = ref.shape[:2]
    _, ref_neutral, _ = masks(ref)
    for slot in ("head", "body", "pants", "boots"):
        path = os.path.join(src, slot + ".png")
        if not os.path.exists(path):
            continue
        img = cv2.cvtColor(cv2.imread(path), cv2.COLOR_BGR2RGB)
        bg, neutral, gear = masks(img)
        gear = clean(gear)
        cc, warp = register(neutral & ~cv2.dilate(gear.astype(np.uint8), np.ones((15, 15), np.uint8)).astype(bool), ref_neutral, (rw, rh))
        # warp maps ref -> image; to render the image into the ref canvas use WARP_INVERSE_MAP
        rgba = np.dstack([img, (gear * 255).astype(np.uint8)])
        layer = cv2.warpAffine(rgba, warp, (rw, rh), flags=cv2.INTER_LANCZOS4 | cv2.WARP_INVERSE_MAP, borderValue=(0, 0, 0, 0))
        # un-premultiplied colour at the edges: rgb of transparent pixels does not matter
        name = os.path.join(out, slot + "_1.png")
        cv2.imwrite(name, cv2.cvtColor(layer, cv2.COLOR_RGBA2BGRA))
        ys, xs = np.where(layer[..., 3] > 8)
        print(slot, "ecc=%.3f" % cc, "gear px", int((layer[..., 3] > 8).sum()), "bbox", xs.min(), ys.min(), xs.max(), ys.max())
        pad = 12
        crop = layer[max(0, ys.min() - pad):ys.max() + pad, max(0, xs.min() - pad):xs.max() + pad]
        side = max(crop.shape[:2])
        icon = np.zeros((side, side, 4), np.uint8)
        icon[(side - crop.shape[0]) // 2:(side - crop.shape[0]) // 2 + crop.shape[0], (side - crop.shape[1]) // 2:(side - crop.shape[1]) // 2 + crop.shape[1]] = crop
        cv2.imwrite(os.path.join(out, slot + "_1_icon.png"), cv2.cvtColor(cv2.resize(icon, (256, 256), interpolation=cv2.INTER_AREA), cv2.COLOR_RGBA2BGRA))
    print("wrote", out)


if __name__ == "__main__":
    main()
