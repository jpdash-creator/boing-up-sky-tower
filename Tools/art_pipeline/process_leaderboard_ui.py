"""Slice the owner-picked leaderboard UI kit (OpenArt candidates, 2026-11-13) into transparent sprites.

Run:  set PYTHONPATH=E:/pylibs  &&  python Tools/art_pipeline/process_leaderboard_ui.py [--preview]
Reads  Art/_candidates/leaderboard_ui/{rows_1,frame_2,icons_1,tabs_1}.png  (owner picks)
Writes Assets/Resources/Art/Ui/lb_*.png  and a contact sheet E:/Unity/shots/lb_kit_preview.png

Keying: the flat grey backdrop is removed by flood fill from the image border (colour close to the border median, low saturation),
so grey inside outlined silver pieces stays opaque. The icon sheet has slightly darker grey cell squares, so the fill tolerance is wide.
Each piece is a connected component of the remaining pixels (sparkle halos merged by a small dilation), trimmed to its bounding box.
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Art", "_candidates", "leaderboard_ui")
DST = os.path.join(ROOT, "Assets", "Resources", "Art", "Ui")

# sheet file -> piece names in reading order (top to bottom, left to right)
SHEETS = {
    "rows_1.png": ["lb_row_gold", "lb_row_silver", "lb_row_bronze", "lb_row_green", "lb_row_grey", "lb_row_you"],
    "frame_2.png": ["lb_frame"],
    "icons_1.png": ["lb_icon_gift", "lb_icon_clock", "lb_icon_crown", "lb_icon_info",
                    "lb_icon_trophy", "lb_icon_map", "lb_icon_infinity", "lb_medal_gold",
                    "lb_medal_silver", "lb_medal_bronze", "lb_badge_green", "lb_badge_grey"],
    "tabs_1.png": ["lb_tab_on", "lb_tab_off", "lb_pill_timer", "lb_pill_score", "lb_plate_round", "lb_strip_header"],
}


def background_mask(rgb):
    h, w, _ = rgb.shape
    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]]).astype(np.float32)
    key = np.median(border, axis=0)
    diff = np.sqrt(((rgb.astype(np.float32) - key) ** 2).sum(axis=2))
    sat = rgb.max(axis=2).astype(np.int32) - rgb.min(axis=2).astype(np.int32)
    cand = (diff < 46) & (sat < 38)
    lab, n = ndi.label(cand)
    edge_labels = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    return np.isin(lab, list(edge_labels))


def cut_pieces(img, names):
    rgb = np.array(img.convert("RGB"))
    bg = background_mask(rgb)
    fg = ~bg
    fg = ndi.binary_opening(fg, iterations=1)
    merged = ndi.binary_dilation(fg, iterations=14)
    lab, n = ndi.label(merged)
    boxes = []
    for i, sl in enumerate(ndi.find_objects(lab), start=1):
        area = int((fg[sl] & (lab[sl] == i)).sum())
        if area < 4000:
            continue
        boxes.append((sl, i, area))
    if len(boxes) != len(names):
        raise SystemExit("expected %d pieces, found %d (areas %s)" % (len(names), len(boxes), [b[2] for b in boxes]))
    # reading order: group into rows by vertical centre
    boxes.sort(key=lambda b: (b[0][0].start + b[0][0].stop) / 2)
    rows, cur = [], [boxes[0]]
    for b in boxes[1:]:
        cy = (b[0][0].start + b[0][0].stop) / 2
        py = sum((c[0][0].start + c[0][0].stop) / 2 for c in cur) / len(cur)
        if abs(cy - py) < 0.5 * (b[0][0].stop - b[0][0].start) + 40:
            cur.append(b)
        else:
            rows.append(cur)
            cur = [b]
    rows.append(cur)
    ordered = [b for r in rows for b in sorted(r, key=lambda b: b[0][1].start)]
    out = {}
    for (sl, i, area), name in zip(ordered, names):
        region = (lab[sl] == i)
        alpha_bool = fg[sl] & region
        # fill interior holes (eyes of the infinity loop, rings) that are not connected to the backdrop are already opaque (not bg)
        alpha = ndi.binary_fill_holes(alpha_bool) if name not in ("lb_icon_infinity", "lb_plate_round") else alpha_bool
        a = ndi.gaussian_filter(alpha.astype(np.float32), 0.8)
        a = np.clip((a - 0.25) / 0.5, 0, 1)
        rgba = np.dstack([rgb[sl], (a * 255).astype(np.uint8)])
        im = Image.fromarray(rgba, "RGBA")
        bbox = im.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
        out[name] = im.crop(bbox)
    return out


def main():
    os.makedirs(DST, exist_ok=True)
    pieces = {}
    for fn, names in SHEETS.items():
        pieces.update(cut_pieces(Image.open(os.path.join(SRC, fn)), names))
    for name, im in pieces.items():
        # rows and pills are painted at about 1500 px wide; keep them at 1024 max edge so ASTC stays small
        m = max(im.size)
        if m > 1024:
            s = 1024.0 / m
            im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
        elif name.startswith(("lb_icon", "lb_medal", "lb_badge")) and m > 384:
            s = 384.0 / m
            im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
        im.save(os.path.join(DST, name + ".png"))
        pieces[name] = im
        print("%-18s %dx%d" % (name, im.width, im.height))
    if "--preview" in sys.argv:
        W, pad = 1800, 20
        sheet = Image.new("RGB", (W, 2400), (92, 80, 140))
        x = y = pad
        rowh = 0
        for name, im in pieces.items():
            t = im.copy()
            t.thumbnail((520, 420))
            if x + t.width > W - pad:
                x = pad
                y += rowh + pad
                rowh = 0
            sheet.paste(t, (x, y), t)
            x += t.width + pad
            rowh = max(rowh, t.height)
        sheet.crop((0, 0, W, y + rowh + pad)).save("E:/Unity/shots/lb_kit_preview.png")


if __name__ == "__main__":
    main()
