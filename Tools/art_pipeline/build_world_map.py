"""Stitches the chained world-map segments into the scrolling map the game loads (T33).

    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/build_world_map.py

Reads Art/Map/segments/map_seg_01.png, 02, ... (bottom to top). Each segment overlaps the one below by 20 percent of its height; the overlap is
cross-faded with a smooth ramp. The result is scaled to 1080 px wide and cut into chunks of 3840 px (a phone texture limit is safer than one
giant image). Writes Assets/Resources/Map/world_chunk_NN.png (chunk 01 = the bottom) and Assets/Resources/Map/world_map.json.
"""
import glob
import json
import os
import sys

sys.path.insert(0, "E:/pylibs")
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OVERLAP = 0.20
WIDTH = 1080
CHUNK = 3840


def cloud_band(width, height, seed):
    """A thin stream of stylised clouds (white puffs, soft lavender underside, dark-hue outline) as RGBA, wavy and feathered at top and bottom."""
    from PIL import ImageDraw, ImageFilter
    rng = np.random.RandomState(seed)
    ss = 2
    W, H = width * ss, height * ss
    puffs = []
    x = -80 * ss
    while x < W + 80 * ss:
        r = int(rng.uniform(0.15, 0.21) * height * ss)
        cy = int(H / 2 + np.sin(x / (170.0 * ss)) * H * 0.05 + rng.uniform(-0.05, 0.05) * H)
        puffs.append((x, cy, r))
        if rng.rand() < 0.55:
            puffs.append((x + int(rng.uniform(-30, 30) * ss), cy + int(rng.uniform(-0.10, 0.10) * H), int(r * rng.uniform(0.6, 0.8))))
        x += int(r * rng.uniform(0.9, 1.5))
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    ol = max(2, int(0.025 * height * ss))
    for (cx, cy, r) in puffs:                      # outline pass
        d.ellipse((cx - r - ol, cy - r - ol, cx + r + ol, cy + r + ol), fill=(70, 70, 120, 255))
    for (cx, cy, r) in puffs:                      # lavender body
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(196, 206, 244, 255))
    for (cx, cy, r) in puffs:                      # white top-left highlight
        k = int(r * 0.80)
        d.ellipse((cx - r + 6 * ss, cy - r + 4 * ss, cx - r + 6 * ss + 2 * k, cy - r + 4 * ss + 2 * k), fill=(255, 255, 255, 255))
    layer = layer.resize((width, height), Image.LANCZOS)
    soft = layer.filter(ImageFilter.GaussianBlur(max(4, height * 0.05)))          # wispy halo around the outlined clouds
    halo = np.asarray(soft).astype(np.float32)
    halo[..., :3] = 255
    halo[..., 3] = np.clip(halo[..., 3] * 0.9, 0, 255)
    out = np.asarray(layer).astype(np.float32)
    a = out[..., 3:4] / 255.0
    rgb = halo[..., :3] * (1 - a) + out[..., :3] * a
    alpha = np.maximum(halo[..., 3:4], out[..., 3:4])
    fade = np.minimum(np.linspace(0, 1, height), np.linspace(1, 0, height))[:, None, None]
    alpha = alpha * np.clip(fade * 12.0, 0, 1)
    return np.concatenate([rgb, alpha], axis=2)


BAND = 150          # height of the cloud stream (thinner: owner 2026-10-20, it covered too much art)


def main():
    files = sorted(glob.glob(os.path.join(ROOT, "Art", "Map", "segments", "map_seg_*.png")))
    segs = []
    for f in files:
        im = Image.open(f).convert("RGB")
        im = im.resize((WIDTH, round(im.height * WIDTH / im.width)), Image.LANCZOS)
        segs.append(np.asarray(im).astype(np.float32))
    # Segment i (above segment i-1) loses its bottom OVERLAP part, which only repeats the strip below, so the segments butt together;
    # a stream of clouds is laid over each butt joint.
    ov = [int(min(segs[i].shape[0], segs[i + 1].shape[0]) * OVERLAP) for i in range(len(segs) - 1)]
    parts = [segs[0]] + [segs[i][: segs[i].shape[0] - ov[i - 1]] for i in range(1, len(segs))]
    total = sum(p.shape[0] for p in parts)
    canvas = np.zeros((total, WIDTH, 3), np.float32)
    y = total
    joints = []
    for p in parts:
        canvas[y - p.shape[0]: y] = p
        y -= p.shape[0]
        joints.append(y)                           # canvas y of this part's top edge
    seam_meta = []
    spans = []
    below = 0
    for p_ in parts:                                # segment spans measured in px from the bottom of the map
        spans.append({"bottom": below, "height": int(p_.shape[0])})
        below += p_.shape[0]
    for i, jy in enumerate(joints[:-1]):           # joint above part i
        band = cloud_band(WIDTH, BAND, 100 + i)
        top = max(0, jy - BAND // 2)
        sl = canvas[top: top + BAND]
        al = band[: sl.shape[0], :, 3:4] / 255.0
        canvas[top: top + sl.shape[0]] = sl * (1 - al) + band[: sl.shape[0], :, :3] * al
        seam_meta.append(top + BAND // 2)
    out_dir = os.path.join(ROOT, "Assets", "Resources", "Map")
    os.makedirs(out_dir, exist_ok=True)
    for old in glob.glob(os.path.join(out_dir, "world_chunk_*.png")):
        os.remove(old)
    chunks = []
    n = 1
    bottom = total
    while bottom > 0:
        top = max(0, bottom - CHUNK)
        Image.fromarray(canvas[top:bottom].clip(0, 255).astype(np.uint8)).save(os.path.join(out_dir, "world_chunk_%02d.png" % n))
        chunks.append({"file": "world_chunk_%02d" % n, "height": bottom - top})
        bottom = top
        n += 1
    meta = {"width": WIDTH, "totalHeight": total, "segments": len(segs), "cloudJoints": seam_meta, "segmentSpans": spans, "chunks": chunks}
    with open(os.path.join(out_dir, "world_map.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(meta, fh, indent=2)
    print("segments", len(segs), "total", WIDTH, "x", total, "chunks", len(chunks))


if __name__ == "__main__":
    main()
