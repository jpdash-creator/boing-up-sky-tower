#!/usr/bin/env python3
"""Cuts the five Gameplay mode icons out of an OpenArt sheet (Art/_candidates/gameplay_icons/sheet_A.png),
keys out the grey background, redraws the pip row so the lit count is exactly 1..5 (the model cannot count),
and writes 256 px sprites plus a preview. Usage: python Tools/art_pipeline/cut_gameplay_icons.py [sheet.png] [--install]
Without --install the sprites go to Art/_candidates/gameplay_icons/processed/; with --install they also go to
Assets/Resources/Art/Ui/gameplay_<mode>.png (the names GameplayIcons.For(mode) loads)."""
import sys, os
from collections import deque
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Art", "_candidates", "gameplay_icons")
MODES = ["relaxed", "easy", "normal", "hard", "insane"]
OUT = 256

def bg_distance(px, bg):
    return abs(px[0]-bg[0]) + abs(px[1]-bg[1]) + abs(px[2]-bg[2])

def find_icons(im):
    w, h = im.size
    bg = im.getpixel((8, 8))
    cols = []
    px = im.load()
    for x in range(w):
        has = False
        for y in range(0, h, 3):
            if bg_distance(px[x, y], bg) > 70:
                has = True; break
        cols.append(has)
    segs, start = [], None
    for x, c in enumerate(cols + [False]):
        if c and start is None: start = x
        if not c and start is not None:
            if x - start > 120: segs.append((start, x))
            start = None
    return bg, segs

def vertical_extent(im, bg, x0, x1):
    px = im.load(); ys = []
    for y in range(im.size[1]):
        for x in range(x0, x1, 4):
            if bg_distance(px[x, y], bg) > 70: ys.append(y); break
    return min(ys), max(ys)

def key_background(icon, bg, keep_x0, keep_x1, tol=60):
    icon = icon.convert("RGBA"); w, h = icon.size; px = icon.load()
    for yy in range(h):
        for xx in range(w):
            if xx < keep_x0 or xx > keep_x1: px[xx, yy] = (bg[0], bg[1], bg[2], 255)
    seen = [[False]*w for _ in range(h)]; q = deque()
    for x in range(w):
        for y in (0, h-1): q.append((x, y))
    for y in range(h):
        for x in (0, w-1): q.append((x, y))
    while q:
        x, y = q.popleft()
        if x < 0 or y < 0 or x >= w or y >= h or seen[y][x]: continue
        seen[y][x] = True
        if bg_distance(px[x, y], bg) <= tol:
            px[x, y] = (0, 0, 0, 0)
            q.extend(((x+1, y), (x-1, y), (x, y+1), (x, y-1)))
    # soften the edge: erode one pixel of alpha halo
    a = icon.getchannel("A").filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8))
    icon.putalpha(a)
    return icon

def redraw_pips(icon, lit):
    """Draws exactly `lit` of 5 pips on the dial's lower band (relative positions measured on sheet A)."""
    w, h = icon.size; d = ImageDraw.Draw(icon)
    cy = int(h * 0.760); r = int(w * 0.043); gap = int(w * 0.108)
    band_h = int(r * 3.3); band_w = gap * 4 + r * 5
    cx = w // 2
    # dark rounded band so old pips are fully covered
    d.rounded_rectangle((cx - band_w//2, cy - band_h//2, cx + band_w//2, cy + band_h//2), radius=band_h//2, fill=(28, 36, 74, 255))
    for i in range(5):
        x = cx + (i - 2) * gap
        if i < lit:
            d.ellipse((x - r, cy - r, x + r, cy + r), fill=(255, 244, 190, 255), outline=(255, 255, 255, 255), width=max(2, r//5))
            d.ellipse((x - r//2, cy - r//2, x, cy), fill=(255, 255, 255, 255))
        else:
            d.ellipse((x - r, cy - r, x + r, cy + r), fill=(52, 62, 110, 255), outline=(12, 18, 46, 255), width=max(2, r//5))

def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    install = "--install" in sys.argv
    sheet = args[0] if args else os.path.join(SRC, "sheet_A.png")
    im = Image.open(sheet).convert("RGB")
    bg, segs = find_icons(im)
    assert len(segs) == 5, "expected 5 icons, found %d" % len(segs)
    outdir = os.path.join(SRC, "processed"); os.makedirs(outdir, exist_ok=True)
    sprites = []
    for i, (x0, x1) in enumerate(segs):
        y0, y1 = vertical_extent(im, bg, x0, x1)
        cx, cy = (x0 + x1)//2, (y0 + y1)//2; half = max(x1 - x0, y1 - y0)//2 + 14
        crop = im.crop((cx - half, cy - half, cx + half, cy + half))
        icon = key_background(crop, bg, x0 - (cx - half) - 2, x1 - (cx - half) + 2)
        redraw_pips(icon, i + 1)
        icon = icon.resize((OUT, OUT), Image.LANCZOS)
        icon.save(os.path.join(outdir, "gameplay_%s.png" % MODES[i]))
        sprites.append(icon)
        if install:
            dst = os.path.join(ROOT, "Assets", "Resources", "Art", "Ui", "gameplay_%s.png" % MODES[i]); icon.save(dst); print("installed", dst)
    prev = Image.new("RGBA", (OUT*5 + 60, OUT + 20), (40, 44, 60, 255))
    for i, s in enumerate(sprites): prev.alpha_composite(s, (10 + i*(OUT+10), 10))
    prev.save(os.path.join(outdir, "preview.png"))
    print("segments", segs); print("wrote", outdir)
main()
