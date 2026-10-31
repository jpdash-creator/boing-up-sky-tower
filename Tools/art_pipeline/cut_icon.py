"""Cuts a game icon painted on a flat grey background: keys the background (colour sampled at the corners), trims and writes a transparent square PNG.

    PYTHONPATH=E:/pylibs python Tools/art_pipeline/cut_icon.py <raw.png> <out.png> [size]
"""
import sys
import numpy as np
import cv2


def main():
    src, out = sys.argv[1], sys.argv[2]
    size = int(sys.argv[3]) if len(sys.argv) > 3 else 256
    img = cv2.imread(src)
    h, w = img.shape[:2]
    corners = np.concatenate([img[:30, :30].reshape(-1, 3), img[:30, -30:].reshape(-1, 3), img[-30:, :30].reshape(-1, 3), img[-30:, -30:].reshape(-1, 3)])
    bg = np.median(corners, axis=0)
    d = np.abs(img.astype(np.int16) - bg.astype(np.int16)).max(axis=2)
    a = (d > 30).astype(np.uint8)
    a = cv2.morphologyEx(a, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
    n, lab, st, _ = cv2.connectedComponentsWithStats(a, 8)
    big = 1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA]))
    a = (lab == big).astype(np.uint8)
    # fill holes
    ff = a.copy()
    m = np.zeros((h + 2, w + 2), np.uint8)
    cv2.floodFill(ff, m, (0, 0), 2)
    a = np.where(ff == 2, 0, 1).astype(np.uint8)
    a = cv2.morphologyEx(a, cv2.MORPH_CLOSE, np.ones((7, 7), np.uint8))
    alpha = cv2.GaussianBlur(a.astype(np.float32) * 255, (0, 0), 1.2).astype(np.uint8)
    ys, xs = np.where(a > 0)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    rgba = np.dstack([img, alpha])[y0:y1 + 1, x0:x1 + 1]
    s = max(rgba.shape[:2])
    canvas = np.zeros((s, s, 4), np.uint8)
    oy, ox = (s - rgba.shape[0]) // 2, (s - rgba.shape[1]) // 2
    canvas[oy:oy + rgba.shape[0], ox:ox + rgba.shape[1]] = rgba
    pad = int(s * 0.04)
    canvas = cv2.copyMakeBorder(canvas, pad, pad, pad, pad, cv2.BORDER_CONSTANT, value=(0, 0, 0, 0))
    canvas = cv2.resize(canvas, (size, size), interpolation=cv2.INTER_AREA)
    cv2.imwrite(out, canvas)
    print("ok", out)


if __name__ == "__main__":
    main()
