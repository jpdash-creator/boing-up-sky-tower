"""Removes the painted round marker pads from a world-map page (the pins are drawn in code, owner 2026-10-24).

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/remove_markers.py <in.png> <out.png> x,y,width_px [x,y,width_px ...]

x and y are fractions (x across, y up from the bottom); width_px is the marker's width in the source image. Each marker gets a disc mask
(1.4 x its radius); the masked pixels are filled from the nearest clean pixels and softened, which is enough for the plain golden road.
"""
import sys

sys.path.insert(0, "E:/pylibs")
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402
from scipy import ndimage as ndi  # noqa: E402


def main():
    src, dst = sys.argv[1], sys.argv[2]
    im = np.asarray(Image.open(src).convert("RGB")).astype(np.float32)
    h, w = im.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    mask = np.zeros((h, w), bool)
    for spec in sys.argv[3:]:
        x, y, mw = [float(v) for v in spec.split(",")]
        cx, cy = x * w, (1.0 - y) * h
        r = mw * 0.5 * 1.9
        # the pad is a flat ellipse seen from above plus a soft shadow below it
        mask |= ((xx - cx) ** 2 + ((yy - cy - r * 0.12) / 1.25) ** 2) < r * r
    # fill from the nearest clean pixel, then soften inside the hole
    idx = ndi.distance_transform_edt(mask, return_distances=False, return_indices=True)
    filled = im[idx[0], idx[1]]
    soft = np.stack([ndi.gaussian_filter(filled[:, :, c], 14) for c in range(3)], axis=2)
    feather = ndi.gaussian_filter(mask.astype(np.float32), 5)[:, :, None]
    out = im * (1 - feather) + soft * feather
    out = np.where(mask[:, :, None], soft, out)
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(dst)
    print("markers removed:", len(sys.argv) - 3, "->", dst)


if __name__ == "__main__":
    main()
