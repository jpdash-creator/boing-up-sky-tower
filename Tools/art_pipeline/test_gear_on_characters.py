"""Composes a character's polygon parts with a gear set (draw order of docs/19) into one contact sheet.

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/test_gear_on_characters.py <out.png> <char_dir>=<label> ... -- <gear_dir> ...
"""
import sys
import numpy as np
import cv2


def load(p):
    im = cv2.imread(p, cv2.IMREAD_UNCHANGED)
    return im if im is not None else None


def over(dst, src):
    if src is None:
        return
    a = src[..., 3:4] / 255.0
    dst[..., :3] = (src[..., :3] * a + dst[..., :3] * (1 - a)).astype(np.uint8)
    dst[..., 3] = np.maximum(dst[..., 3], src[..., 3])


def compose(char, gear):
    c = np.zeros((1649, 1365, 4), np.uint8)
    g = (lambda n: load(f"{gear}/{n}.png")) if gear else (lambda n: None)
    for n in ("back", "legL", "legR"):
        over(c, load(f"{char}/{n}.png"))
    over(c, g("bootL")); over(c, g("bootR"))
    over(c, load(f"{char}/body.png")); over(c, g("body"))
    over(c, load(f"{char}/handL.png")); over(c, load(f"{char}/handR.png"))
    over(c, g("sleeveL")); over(c, g("sleeveR"))
    over(c, load(f"{char}/head.png")); over(c, g("head"))
    return c


def main():
    out = sys.argv[1]
    i = sys.argv.index("--")
    chars = [a.split("=") for a in sys.argv[2:i]]
    gears = [None] + sys.argv[i + 1:]
    rows = []
    for cdir, label in chars:
        tiles = []
        for gd in gears:
            c = compose(cdir, gd)
            a = c[..., 3:4] / 255.0
            bg = np.full(c.shape[:2] + (3,), 215, np.uint8)
            t = cv2.resize((c[..., :3] * a + bg * (1 - a)).astype(np.uint8), (341, 412))
            cv2.putText(t, label + " " + (gd.split("/")[-1] if gd else "bare"), (6, 24), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 0, 200), 2)
            tiles.append(t)
        rows.append(np.hstack(tiles))
    cv2.imwrite(out, np.vstack(rows))


if __name__ == "__main__":
    main()
