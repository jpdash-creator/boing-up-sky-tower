"""Draws the polygon framework templates (docs/19): grey polygons on magenta, 4:3, one image per OpenArt call.

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/make_polygon_templates.py [out_dir]

Character templates (one part group per image, so the model fills exactly that shape): char_head, char_body, char_hands, char_legs, char_back.
Gear templates (three variants side by side, one slot per image): gear_head, gear_body (body + the two hands, sleeves go on the upper part of the hands),
gear_boots (the two legs with feet, boots go on the feet and ankle).
polygons.json records, per template image and per cell, the scale and offset that map the assembled doll (canvas 1365 x 1649) into that image, so a cut
part can be placed back exactly (assembled = (sheet - offset) / scale).
"""
import json
import os
import sys
from PIL import Image, ImageDraw

W, H = 1365, 1649
SW, SH = 2880, 2160
MAGENTA, GREY, EDGE = (255, 0, 255), (140, 140, 140), (96, 96, 96)

# assembled doll polygons: name -> (kind, box)
SHAPES = {
    "head":  ("ellipse", (160, 120, 1205, 1060)),
    "body":  ("round",   (390, 940, 975, 1320)),
    "handL": ("round",   (250, 1000, 420, 1330)),
    "handR": ("round",   (945, 1000, 1115, 1330)),
    "legL":  ("round",   (470, 1180, 650, 1520)),
    "legR":  ("round",   (715, 1180, 895, 1520)),
    "footL": ("ellipse", (420, 1470, 690, 1600)),
    "footR": ("ellipse", (675, 1470, 945, 1600)),
    "back":  ("ellipse", (1010, 760, 1340, 1260)),
}
GROUPS = {
    "char_head":  ["head"],
    "char_body":  ["body"],
    "char_hands": ["handL", "handR"],
    "char_legs":  ["legL", "legR", "footL", "footR"],
    "char_back":  ["back"],
}
GEAR = {"gear_head": ["head"], "gear_body": ["body", "handL", "handR"], "gear_boots": ["legL", "legR", "footL", "footR"]}


def bbox(names):
    xs0, ys0, xs1, ys1 = [], [], [], []
    for n in names:
        x0, y0, x1, y1 = SHAPES[n][1]
        xs0.append(x0); ys0.append(y0); xs1.append(x1); ys1.append(y1)
    return min(xs0), min(ys0), max(xs1), max(ys1)


def draw_group(d, names, s, ox, oy):
    for n in names:
        kind, (x0, y0, x1, y1) = SHAPES[n]
        b = [x0 * s + ox, y0 * s + oy, x1 * s + ox, y1 * s + oy]
        k = dict(fill=GREY, outline=EDGE, width=max(6, int(12 * s)))
        if kind == "ellipse":
            d.ellipse(b, **k)
        else:
            d.rounded_rectangle(b, radius=int(min(b[2] - b[0], b[3] - b[1]) / 2 - 3), **k)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Art", "_candidates", "polygons")
    os.makedirs(out, exist_ok=True)
    meta = {"canvas": [W, H], "sheet": [SW, SH], "templates": {}}
    # character parts: one group per image, centred, scaled to fill about 70 percent of the shorter side
    for name, parts in GROUPS.items():
        if name == "char_hands":   # the two hands side by side, each big, so the model fills real hands (not two tiny capsules)
            img = Image.new("RGB", (SW, SH), MAGENTA)
            d = ImageDraw.Draw(img)
            cells = {}
            for i, n in enumerate(parts):
                bx0, by0, bx1, by1 = bbox([n])
                s = SH * 0.66 / (by1 - by0)
                cx = SW * (0.30 + 0.40 * i)
                ox, oy = cx - (bx0 + bx1) / 2 * s, (SH - (by1 - by0) * s) / 2 - by0 * s
                draw_group(d, [n], s, ox, oy)
                cells[n] = {"scale": s, "offset": [ox, oy]}
            img.save(os.path.join(out, name + ".png"))
            meta["templates"][name] = {"parts": parts, "perPart": cells}
            continue
        bx0, by0, bx1, by1 = bbox(parts)
        gw, gh = bx1 - bx0, by1 - by0
        s = min(SW * 0.72 / gw, SH * 0.72 / gh)
        ox, oy = (SW - gw * s) / 2 - bx0 * s, (SH - gh * s) / 2 - by0 * s
        img = Image.new("RGB", (SW, SH), MAGENTA)
        draw_group(ImageDraw.Draw(img), parts, s, ox, oy)
        img.save(os.path.join(out, name + ".png"))
        meta["templates"][name] = {"parts": parts, "cells": [{"scale": s, "offset": [ox, oy]}]}
    # gear: three cells side by side
    for name, parts in GEAR.items():
        bx0, by0, bx1, by1 = bbox(parts)
        gw, gh = bx1 - bx0, by1 - by0
        cw = SW / 3
        s = min(cw * 0.86 / gw, SH * 0.80 / gh)
        img = Image.new("RGB", (SW, SH), MAGENTA)
        d = ImageDraw.Draw(img)
        cells = []
        for i in range(3):
            ox = i * cw + (cw - gw * s) / 2 - bx0 * s
            oy = (SH - gh * s) / 2 - by0 * s
            draw_group(d, parts, s, ox, oy)
            cells.append({"scale": s, "offset": [ox, oy]})
        img.save(os.path.join(out, name + ".png"))
        meta["templates"][name] = {"parts": parts, "cells": cells}
    json.dump(meta, open(os.path.join(out, "polygons.json"), "w"), indent=2)
    print("wrote", out)


if __name__ == "__main__":
    main()
