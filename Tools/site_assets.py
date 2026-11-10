#!/usr/bin/env python3
"""Builds the web-optimised images and fonts for the public website (docs/img, docs/fonts) from the game's own art.

Run from the repo root:  python Tools/site_assets.py
Sources (all existing game art, no new generation): the logo, the store feature graphic, the Momo preview, the app icon, the painted world map pages
and the five Gameplay icons. Fonts are the game's own Lilita One and Nunito (both SIL OFL, licences in Assets/Art/Fonts).
"""
import shutil
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "docs"
IMG = OUT / "img"
FONTS = OUT / "fonts"
ART = ROOT / "Assets"


def save(im, name, q=82, width=None):
    if width and im.width > width:
        im = im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)
    p = IMG / name
    im.save(p, "WEBP", quality=q, method=6)
    print("img/%s %dx%d %d KB" % (name, im.width, im.height, p.stat().st_size // 1024))
    return im


def momo_cutout():
    im = Image.open(ART / "Resources/Characters/momo/preview.png").convert("RGB")
    a = np.array(im).astype(int)
    h, w, _ = a.shape
    corners = np.concatenate([a[:30, :30].reshape(-1, 3), a[:30, -30:].reshape(-1, 3), a[-30:, :30].reshape(-1, 3), a[-30:, -30:].reshape(-1, 3)])
    bg = np.median(corners, axis=0)
    fg = np.abs(a - bg).sum(axis=2) > 36
    fg = ndimage.binary_opening(fg, iterations=2)
    lab, n = ndimage.label(fg)
    sizes = ndimage.sum(fg, lab, range(1, n + 1))
    fg = lab == (1 + int(np.argmax(sizes)))
    fg = ndimage.binary_fill_holes(fg)
    alpha = Image.fromarray((fg * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))
    rgba = im.convert("RGBA")
    rgba.putalpha(alpha)
    ys, xs = np.where(fg)
    rgba = rgba.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    return rgba


def main():
    IMG.mkdir(parents=True, exist_ok=True)
    FONTS.mkdir(parents=True, exist_ok=True)
    save(Image.open(ART / "Resources/Art/Ui/logo.png").convert("RGBA"), "logo.webp", 90, 1100)
    save(momo_cutout(), "momo.webp", 88, 760)
    save(Image.open(ART / "Art/Marketing/store/feature_graphic_1024x500.png").convert("RGB"), "hero-scene.webp", 86)
    save(Image.open(ART / "Art/Marketing/icon/out/icon_play_512.png").convert("RGB"), "app-icon.webp", 88, 512)
    save(Image.open(ART / "Art/Map/map_world_v1.png").convert("RGB"), "map-world.webp", 80, 900)
    for i in range(1, 7):
        save(Image.open(ART / ("Art/Maps/approved/map_page_%d.png" % i)).convert("RGB"), "region-%d.webp" % i, 78, 640)
    for m in ("relaxed", "easy", "normal", "hard", "insane"):
        save(Image.open(ART / ("Resources/Art/Ui/gameplay_%s.png" % m)).convert("RGBA"), "mode-%s.webp" % m, 90, 256)
    for m in ("bronze", "silver", "gold"):
        save(Image.open(ART / ("Resources/Art/Ui/medal_%s.png" % m)).convert("RGBA"), "medal-%s.webp" % m, 90, 256)
    icon = Image.open(ART / "Art/Marketing/icon/out/icon_legacy_192.png").convert("RGBA")
    icon.save(OUT / "favicon.png")
    icon.resize((180, 180), Image.LANCZOS).save(OUT / "apple-touch-icon.png")
    for f in ("LilitaOne-Regular.ttf", "Nunito-Bold.ttf", "LICENSE_lilitaone.txt"):
        shutil.copyfile(ART / "Art/Fonts" / f, FONTS / f)
    print("fonts copied")


if __name__ == "__main__":
    main()
