"""Put a character drawn on a magenta OpenArt sheet onto the shared 1365 x 1649 rig canvas (same size and proportions
as Momo), ready for cut_character_parts.py. Costs nothing (no OpenArt calls).

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/prepare_character_canvas.py <sporeling|moth|rootling|all> [--src Art/_candidates/parts] [--out <dir>]

Per sheet: the magenta background is removed by a flood fill from the border (so pink spots INSIDE the art stay),
the pieces are picked by box (sheet pixel coordinates) and then scaled / placed on the canvas by the LAYOUT table:
head height about 55 percent of the character, chin at the neck line, feet line at y 1600, centred on x 682.
The assembled flat picture is written to <out>/<id>.png; the grey / purple template shapes on the sheets are ignored.
"""
import os
import sys

sys.path.insert(0, "E:/pylibs")
import cv2  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402
from scipy import ndimage  # noqa: E402

W, H = 1365, 1649
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULT_SRC = "E:/Code/boing-up-sky-tower/Art/_candidates/parts"
K = 1.15  # sheet is 2304 x 1728; the boxes below were read off a 2000 px wide view


def key_out_magenta(rgb, interior=False):
    r, g, b = [rgb[..., i].astype(int) for i in range(3)]
    bgish = (r > 190) & (b > 150) & (g < 120) & (b > g + 90) & (r > g + 100)
    lab, n = ndimage.label(bgish)
    border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    bg = np.isin(lab, list(border))
    if interior:  # holes between roots / twigs are background too (no pink art on that sheet)
        strict = (r > 225) & (b > 200) & (g < 70)
        bg |= ndimage.binary_dilation(strict, iterations=1) & bgish
    bg = ndimage.binary_dilation(bg, iterations=1)  # eat the one pixel blend with the background
    a = np.where(bg, 0, 255).astype(np.uint8)
    out = np.dstack([rgb, a])
    # blend pixels right next to the background lose their magenta cast
    edge = ndimage.binary_dilation(bg, iterations=3) & ~bg
    px = out[edge]
    pink = (px[:, 0] > px[:, 1] + 60) & (px[:, 2] > px[:, 1] + 40) & (px[:, 0] > 150)
    px[pink, 3] = 0
    out[edge] = px
    return out


def components(rgba, min_area=2500, split=6):
    """pieces of the sheet. Pieces that only touch through their sticker halo are split: the mask is eroded by
    `split` px, labelled, and every pixel goes to the nearest eroded blob."""
    m = rgba[..., 3] > 0
    # flat grey template shapes are never art
    px = rgba[..., :3].astype(int)
    grey = (np.abs(px[..., 0] - px[..., 1]) < 18) & (np.abs(px[..., 1] - px[..., 2]) < 22) & (px[..., 0] > 110) & (px[..., 0] < 175)
    grey = ndimage.binary_opening(grey, iterations=3)
    grey = ndimage.binary_dilation(ndimage.binary_closing(grey, iterations=4), iterations=3)
    m &= ~(grey & (ndimage.binary_erosion(ndimage.binary_fill_holes(grey), iterations=0)))
    core = ndimage.binary_erosion(m, iterations=split) if split else m
    lab0, n = ndimage.label(core, structure=np.ones((3, 3)))
    if split:
        idx = ndimage.distance_transform_edt(lab0 == 0, return_distances=False, return_indices=True)
        lab = lab0[idx[0], idx[1]]
        lab[~m] = 0
    else:
        lab = lab0
    objs = []
    for i, sl in enumerate(ndimage.find_objects(lab), 1):
        if sl is None:
            continue
        area = int((lab[sl] == i).sum())
        if area < min_area:
            continue
        piece = rgba[sl].copy()
        piece[lab[sl] != i, 3] = 0
        objs.append({"box": (sl[1].start, sl[0].start, sl[1].stop, sl[0].stop), "img": piece, "area": area})
    return objs


def pick(objs, box_disp):
    """components whose centre lies in the given box (given on the 2000 px wide display)."""
    x0, y0, x1, y1 = [v * K for v in box_disp]
    return [o for o in objs if x0 <= (o["box"][0] + o["box"][2]) / 2 <= x1 and y0 <= (o["box"][1] + o["box"][3]) / 2 <= y1]


def merge(objs):
    x0 = min(o["box"][0] for o in objs); y0 = min(o["box"][1] for o in objs)
    x1 = max(o["box"][2] for o in objs); y1 = max(o["box"][3] for o in objs)
    im = np.zeros((y1 - y0, x1 - x0, 4), np.uint8)
    for o in objs:
        bx = o["box"]
        sub = im[bx[1] - y0:bx[3] - y0, bx[0] - x0:bx[2] - x0]
        m = o["img"][..., 3] > 0
        sub[m] = o["img"][m]
    return im, (x0, y0, x1, y1)


def scaled(im, s, flip=False):
    if flip:
        im = im[:, ::-1]
    return np.array(Image.fromarray(np.ascontiguousarray(im)).resize(
        (max(1, round(im.shape[1] * s)), max(1, round(im.shape[0] * s))), Image.LANCZOS))


def paste(canvas, im, x, y):
    """alpha-over paste, (x, y) = top-left on the canvas."""
    h, w = im.shape[:2]
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(W, x + w), min(H, y + h)
    if x1 <= x0 or y1 <= y0:
        return
    src = im[y0 - y:y1 - y, x0 - x:x1 - x].astype(np.float32) / 255
    dst = canvas[y0:y1, x0:x1].astype(np.float32) / 255
    a = src[..., 3:4]
    oa = dst[..., 3:4]
    na = a + oa * (1 - a)
    rgb = (src[..., :3] * a + dst[..., :3] * oa * (1 - a)) / np.maximum(na, 1e-6)
    canvas[y0:y1, x0:x1] = np.clip(np.round(np.concatenate([rgb, na], 2) * 255), 0, 255).astype(np.uint8)


def put(canvas, im, cx=None, left=None, top=None, bottom=None, right=None):
    h, w = im.shape[:2]
    x = int(round(cx - w / 2)) if cx is not None else (left if left is not None else right - w)
    y = top if top is not None else bottom - h
    paste(canvas, im, int(x), int(y))
    return x, y, w, h


# ---------------------------------------------------------------------------------------------------------------

def sporeling(src):
    sheet = key_out_magenta(np.array(Image.open(os.path.join(src, "sheet_sporeling.png")).convert("RGB")))
    objs = components(sheet)
    body = pick(objs, (150, 90, 1170, 1190))
    spore = pick(objs, (1290, 170, 1700, 520))
    fig = [o for o in body if o["area"] == max(b["area"] for b in body)]
    hands = sorted([o for o in body if o not in fig], key=lambda o: o["box"][0])
    hands = [h for h in hands if h["area"] > 6000][:2]
    fim, fbox = merge(fig)
    s = 1.14
    c = np.zeros((H, W, 4), np.uint8)
    sp, _ = merge(spore)
    sp = scaled(sp, 0.75)  # about 340 wide, hanging at Momo's tail position (below the cap rim, above the hand)
    put(c, sp, cx=1190, top=900)
    f = scaled(fim, s)
    x, y, w, h = put(c, f, cx=682, bottom=1600)
    ox, oy = fbox[0], fbox[1]  # figure origin on the sheet: hands are placed relative to it
    for hnd in hands:
        hx = x + (hnd["box"][0] - ox) * s
        hy = y + (hnd["box"][1] - oy) * s
        paste(c, scaled(hnd["img"], s), int(round(hx)), int(round(hy)))
    return c


def box_px(box_disp):
    return [int(v * K) for v in box_disp]


def cut_box(sheet, box_disp):
    """take the pixels of a display-space box out of the sheet (returns the piece cropped to its alpha)."""
    x0, y0, x1, y1 = box_px(box_disp)
    piece = sheet[y0:y1, x0:x1].copy()
    sheet[y0:y1, x0:x1, 3] = 0
    ys, xs = np.nonzero(piece[..., 3] > 0)
    return piece[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def moth(src):
    sheet = key_out_magenta(np.array(Image.open(os.path.join(src, "sheet_moth.png")).convert("RGB")))
    sheet[int(895 * K):, :int(436 * K), 3] = 0  # grey template capsule that touches the left wing tip
    cut_box(sheet, (545, 972, 705, 1145))   # the two fluffy tufts (not used)
    cut_box(sheet, (1170, 962, 1325, 1145))
    objs = components(sheet, 2500)
    fig = pick(objs, (430, 70, 1330, 1170))  # head + body + wings drawn behind it + the left dark paw
    big = max(o["area"] for o in fig)
    paws = [o for o in fig if o["area"] < big and o["area"] > 8000]  # the right dark paw is a separate piece
    fig = [o for o in fig if o["area"] == big]
    wings = pick(objs, (1200, 180, 1740, 800))
    s = 1.22
    c = np.zeros((H, W, 4), np.uint8)
    wim, _ = merge(wings)
    put(c, scaled(wim, 0.62), cx=1175, top=800)  # back part: the separate butterfly wing pair
    fim, fbox = merge(fig)
    layer = np.zeros((H, W, 4), np.uint8)
    x, y, w, h = put(layer, scaled(fim, s), cx=682, bottom=1600)
    layer[1345:, :200, 3] = 0  # rest of the grey template capsule
    for pw in paws:  # right paw mirrors the left one about the body centre
        put(layer, scaled(pw["img"], s), cx=785, bottom=1600)
    if not KEEP_BODY_WINGS:
        layer = strip_wings(layer)
    paste(c, layer, 0, 0)
    return c


KEEP_BODY_WINGS = False


# canvas coordinates of the moth's torso + arms (everything else below the head that the figure drew is the pair of
# wings behind the body; the separate butterfly pair is the character's back part instead)
MOTH_KEEP = [(518, 925), (890, 925), (895, 985), (993, 1050), (1002, 1150), (975, 1215), (918, 1210), (904, 1300), (885, 1400), (495, 1400), (518, 1300), (507, 1205), (450, 1220), (382, 1190), (382, 1100), (450, 1030), (510, 975)]


def strip_wings(layer):
    keep = np.zeros(layer.shape[:2], np.uint8)
    cv2.fillPoly(keep, [np.array(MOTH_KEEP, np.int32)], 1)
    keep[:940] = 1
    keep[1400:] = 1
    out = layer.copy()
    out[(keep == 0), 3] = 0
    return out


def rootling(src):
    sheet = key_out_magenta(np.array(Image.open(os.path.join(src, "sheet_rootling.png")).convert("RGB")), interior=True)
    objs = components(sheet, 1500)
    head, _ = merge(pick(objs, (340, 80, 940, 890)))
    roots, _ = merge(pick(objs, (1090, 200, 1660, 900)))
    moss, _ = merge(pick(objs, (260, 940, 660, 1380)))
    twig = [merge(pick(objs, bx))[0] for bx in ((690, 930, 900, 1350), (930, 930, 1130, 1350), (1175, 930, 1335, 1350))]
    feet = [merge(pick(objs, bx))[0] for bx in ((1420, 960, 1650, 1055), (1420, 1130, 1650, 1215), (1420, 1265, 1650, 1360))]
    c = np.zeros((H, W, 4), np.uint8)
    r = scaled(roots, 0.54)
    put(c, r, cx=1190, top=800)                                     # back
    put(c, scaled(twig[1], 0.5), cx=600, top=1400)                  # legL
    put(c, scaled(twig[1], 0.5, flip=True), cx=765, top=1400)       # legR: the missing 4th limb is a mirror
    put(c, scaled(feet[0], 0.8), cx=600, bottom=1620)               # footL
    put(c, scaled(feet[1], 0.8), cx=765, bottom=1620)               # footR
    put(c, scaled(moss, 0.95), cx=682, top=930)                     # torso
    put(c, scaled(twig[0], 0.55), cx=430, top=1010)                 # armL
    put(c, scaled(twig[2], 0.55), cx=935, top=1010)                 # armR
    put(c, scaled(head, 1.0), cx=682, bottom=980)                   # head
    return c


LAYOUT_NOTE = "see main()"


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return 2
    src = args[args.index("--src") + 1] if "--src" in args else DEFAULT_SRC
    out = args[args.index("--out") + 1] if "--out" in args else os.path.join(ROOT, "Art", "_candidates", "parts", "canvas")
    os.makedirs(out, exist_ok=True)
    which = args[0]
    done = []
    if which in ("sporeling", "all"):
        Image.fromarray(sporeling(src), "RGBA").save(os.path.join(out, "sporeling.png"))
        done.append("sporeling")
    if which in ("moth", "all"):
        Image.fromarray(moth(src), "RGBA").save(os.path.join(out, "moth.png"))
        done.append("moth")
    if which in ("rootling", "all"):
        Image.fromarray(rootling(src), "RGBA").save(os.path.join(out, "rootling.png"))
        done.append("rootling")
    for d in done:
        print("wrote", os.path.join(out, d + ".png"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
