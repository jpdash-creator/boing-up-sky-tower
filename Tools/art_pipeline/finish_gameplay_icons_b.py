#!/usr/bin/env python3
"""Finishes the owner-picked Gameplay icon set (OpenArt sheet Art/_candidates/gameplay_icons/sheet_B2_1.png).
The model could not draw exact needle angles or lit-pip counts, so every dial face is repainted in code: needle at the five clock positions
(9, 10:30, 12, 1:30, 3 o'clock), hub, ticks and exactly 1..5 of 5 pips lit. Rings, rim symbols and the spiky Insane rim stay from the art.
Writes 256 px sprites + preview to Art/_candidates/gameplay_icons/processed_b/. Usage: python Tools/art_pipeline/finish_gameplay_icons_b.py [--install]
--install also writes Assets/Resources/Art/Ui/gameplay_<mode>.png (the names GameplayIcons.For(mode) loads)."""
import math, os, sys
from collections import deque
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Art", "_candidates", "gameplay_icons")
MODES = ["relaxed", "easy", "normal", "hard", "insane"]
K = 1.424                       # sheet is 2848 px wide, measured on a 2000 px preview
HUBS = [(264, 553), (630, 553), (1000, 553), (1372, 553), (1737, 553)]
RF = 176                        # radius of the repainted face (inside the dark face outline)
CLOCK = [270, 315, 0, 45, 90]   # needle angle in degrees clockwise from 12 o'clock
NEEDLE = [(70, 165, 185), (60, 150, 175), (125, 85, 55), (238, 150, 130), (240, 105, 170)]
OUT = 256
NAVY = (22, 30, 66, 255)

def dist(a, b): return abs(a[0]-b[0]) + abs(a[1]-b[1]) + abs(a[2]-b[2])

def patch_median(im, cx, cy, r=7):
    px = []
    for y in range(int(cy-r), int(cy+r)):
        for x in range(int(cx-r), int(cx+r)): px.append(im.getpixel((x, y))[:3])
    return tuple(sorted(c[i] for c in px)[len(px)//2] for i in range(3))

def lerp(a, b, t): return tuple(int(a[i] + (b[i]-a[i]) * t) for i in range(3))

def fit_face(sheet, guess):
    """Fits the circle of the dark face outline (around the glass) by scanning rays from the estimated hub; returns (cx, cy, r)."""
    gx, gy = guess; pts = []
    for a in range(0, 360, 4):
        th = math.radians(a); ux, uy = math.sin(th), -math.cos(th)
        for r in range(222, 150, -1):   # from outside inward: the first dark pixel is the OUTER edge of the face outline
            x, y = int(gx + ux*r), int(gy + uy*r)
            p = sheet.getpixel((x, y))[:3]
            if dist(p, (22, 30, 66)) < 110 and sum(p) < 330:
                pts.append((gx + ux*r, gy + uy*r)); break
    cx, cy, r = gx, gy, 195.0
    for _ in range(4):   # least squares circle fit, dropping outliers
        n = len(pts)
        A = [[2*x, 2*y, 1.0] for x, y in pts]; b = [x*x + y*y for x, y in pts]
        # normal equations
        ata = [[sum(A[k][i]*A[k][j] for k in range(n)) for j in range(3)] for i in range(3)]
        atb = [sum(A[k][i]*b[k] for k in range(n)) for i in range(3)]
        det = lambda m: (m[0][0]*(m[1][1]*m[2][2]-m[1][2]*m[2][1]) - m[0][1]*(m[1][0]*m[2][2]-m[1][2]*m[2][0]) + m[0][2]*(m[1][0]*m[2][1]-m[1][1]*m[2][0]))
        D = det(ata); sol = []
        for c in range(3):
            m = [row[:] for row in ata]
            for rr in range(3): m[rr][c] = atb[rr]
            sol.append(det(m) / D)
        cx, cy = sol[0], sol[1]; r = math.sqrt(max(1.0, sol[2] + cx*cx + cy*cy))
        pts = [(x, y) for x, y in pts if abs(math.hypot(x-cx, y-cy) - r) < 7]
    return cx, cy, r

def repaint_dial(sheet, hub, idx, lit):
    """Returns an RGBA layer (size 2*RF+4 square) holding the new face, centred on the hub."""
    hx, hy = hub
    gloss = patch_median(sheet, hx - 65, hy - 95)
    body = patch_median(sheet, hx - 95, hy + 58)
    RFLOC = RF
    S = 4; n = (2 * RFLOC + 4) * S; c = n // 2
    layer = Image.new("RGBA", (n, n), (0, 0, 0, 0)); d = ImageDraw.Draw(layer)
    # face: vertical blend from the gloss colour (top) to the body colour (bottom), clipped to a disc
    face = Image.new("RGBA", (n, n)); fd = ImageDraw.Draw(face)
    for y in range(n):
        t = max(0.0, min(1.0, (y - c * 0.55) / (c * 0.75)))
        fd.line((0, y, n, y), fill=lerp(gloss, body, t) + (255,))
    mask = Image.new("L", (n, n), 0); ImageDraw.Draw(mask).ellipse((c - RFLOC*S, c - RFLOC*S, c + RFLOC*S, c + RFLOC*S), fill=255)
    layer.paste(face, (0, 0), mask)
    # glossy dome highlight, upper left
    hl = Image.new("RGBA", (n, n), (0, 0, 0, 0)); hd = ImageDraw.Draw(hl)
    hd.ellipse((c - 150*S, c - 160*S, c + 60*S, c - 40*S), fill=(255, 255, 255, 70))
    hl.putalpha(Image.composite(hl.getchannel("A"), Image.new("L", (n, n), 0), mask)); layer.alpha_composite(hl)
    # ticks at 9 and 3 o'clock
    for sx in (-1, 1):
        d.rounded_rectangle((c + sx*118*S - 11*S, c - 3*S, c + sx*118*S + 11*S, c + 3*S), radius=3*S, fill=NAVY)
    # needle (tapered, outlined)
    th = math.radians(CLOCK[idx]); ux, uy = math.sin(th), -math.cos(th); px_, py_ = -uy, ux
    L, w0, w1 = 120*S, 15*S, 7*S
    pts = [(c + px_*w0, c + py_*w0), (c + ux*L + px_*w1, c + uy*L + py_*w1), (c + ux*L - px_*w1, c + uy*L - py_*w1), (c - px_*w0, c - py_*w0)]
    col = NEEDLE[idx] + (255,)
    d.polygon(pts, fill=col, outline=NAVY)
    d.line(pts + [pts[0]], fill=NAVY, width=3*S, joint="curve")
    d.ellipse((c - 21*S, c - 21*S, c + 21*S, c + 21*S), fill=lerp(NEEDLE[idx], (255, 255, 255), 0.35) + (255,), outline=NAVY, width=4*S)
    d.ellipse((c - 7*S, c - 7*S, c + 7*S, c + 7*S), fill=NAVY)
    # five pips on one straight row, lit from the left
    py = c + 112*S; gap = 52*S; pr = 17*S
    for i in range(5):
        x = c + (i - 2) * gap
        if i < lit:
            glow = Image.new("RGBA", (n, n), (0, 0, 0, 0)); gd = ImageDraw.Draw(glow)
            gd.ellipse((x - pr*1.7, py - pr*1.7, x + pr*1.7, py + pr*1.7), fill=(255, 240, 150, 110))
            layer.alpha_composite(glow.filter(ImageFilter.GaussianBlur(5*S)))
            d = ImageDraw.Draw(layer)
            d.ellipse((x - pr, py - pr, x + pr, py + pr), fill=(255, 246, 180, 255), outline=NAVY, width=4*S)
            d.ellipse((x - pr*0.5, py - pr*0.55, x, py - pr*0.05), fill=(255, 255, 255, 255))
        else:
            d.ellipse((x - pr, py - pr, x + pr, py + pr), fill=lerp(body, (10, 16, 48), 0.72) + (255,), outline=NAVY, width=4*S)
    # sparkle upper right
    sx, sy, sr = c + 78*S, c - 108*S, 15*S
    d.polygon([(sx, sy - sr*1.6), (sx + sr*0.35, sy - sr*0.35), (sx + sr*1.6, sy), (sx + sr*0.35, sy + sr*0.35), (sx, sy + sr*1.6), (sx - sr*0.35, sy + sr*0.35), (sx - sr*1.6, sy), (sx - sr*0.35, sy - sr*0.35)], fill=(255, 255, 255, 235))
    return layer.resize((n // S, n // S), Image.LANCZOS)

def key_background(icon, bg, tol=60):
    icon = icon.convert("RGBA"); w, h = icon.size; px = icon.load()
    q = deque(); seen = [[False]*w for _ in range(h)]
    for x in range(w): q.append((x, 0)); q.append((x, h-1))
    for y in range(h): q.append((0, y)); q.append((w-1, y))
    while q:
        x, y = q.popleft()
        if x < 0 or y < 0 or x >= w or y >= h or seen[y][x]: continue
        seen[y][x] = True
        if dist(px[x, y], bg) <= tol:
            px[x, y] = (0, 0, 0, 0); q.extend(((x+1, y), (x-1, y), (x, y+1), (x, y-1)))
    a = icon.getchannel("A").filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8)); icon.putalpha(a)
    return icon

def keep_main_piece(icon, start):
    """Keeps only the opaque pixels connected to the icon centre (drops slivers of the neighbouring icons)."""
    w, h = icon.size; a = icon.getchannel("A"); ap = a.load()
    keep = Image.new("L", (w, h), 0); kp = keep.load(); q = deque([start]); seen = set([start])
    while q:
        x, y = q.popleft()
        if x < 0 or y < 0 or x >= w or y >= h or ap[x, y] < 24: continue
        kp[x, y] = ap[x, y]
        for nx, ny in ((x+1, y), (x-1, y), (x, y+1), (x, y-1)):
            if (nx, ny) not in seen: seen.add((nx, ny)); q.append((nx, ny))
    icon.putalpha(keep); return icon

def main():
    install = "--install" in sys.argv
    sheet = Image.open(os.path.join(SRC, "sheet_B2_1.png")).convert("RGB"); bg = sheet.getpixel((8, 8))
    work = sheet.copy().convert("RGBA")
    global RF
    centres = []
    for i, (hx, hy) in enumerate(HUBS):
        fx, fy, fr = fit_face(sheet, (int(hx * K), int(hy * K)))
        RF = int(fr - 13); cx, cy = int(round(fx)), int(round(fy)); centres.append((cx, cy))
        print("icon", i + 1, "face centre", cx, cy, "outline radius %.1f" % fr, "(guess %d,%d)" % (int(hx * K), int(hy * K)))
        layer = repaint_dial(sheet, (cx, cy), i, i + 1)
        work.alpha_composite(layer, (cx - layer.width // 2, cy - layer.height // 2))
    work = work.convert("RGB")
    outdir = os.path.join(SRC, "processed_b"); os.makedirs(outdir, exist_ok=True)
    sprites = []
    for i, (cx, cy) in enumerate(centres):
        half = 345
        crop = work.crop((cx - half, cy - half - 45, cx + half, cy + half - 45))
        icon = keep_main_piece(key_background(crop, bg), (half, half + 45)).resize((OUT, OUT), Image.LANCZOS)
        icon.save(os.path.join(outdir, "gameplay_%s.png" % MODES[i])); sprites.append(icon)
        if install:
            dst = os.path.join(ROOT, "Assets", "Resources", "Art", "Ui", "gameplay_%s.png" % MODES[i]); icon.save(dst); print("installed", dst)
    prev = Image.new("RGBA", (OUT*5 + 60, OUT + 20), (40, 44, 60, 255))
    for i, s in enumerate(sprites): prev.alpha_composite(s, (10 + i*(OUT+10), 10))
    prev.save(os.path.join(outdir, "preview.png")); work.save(os.path.join(outdir, "sheet_fixed.png")); print("wrote", outdir)
main()
