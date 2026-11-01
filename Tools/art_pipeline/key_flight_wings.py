"""Key the grey-blue background (and the light-blue sticker halo) of the Flight wings picture to transparent, trim, scale to <=1024 wide.
Usage: python key_flight_wings.py in.png out.png
Flood fill from the border through background/halo colours only (never through the dark navy outline), so white feathers with pale shading survive."""
import sys, numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage as ndi
src, dst = sys.argv[1], sys.argv[2]
im = Image.open(src).convert('RGB'); a = np.asarray(im).astype(np.float32)
bg = np.array([109, 122, 131], np.float32); halo = np.array([155, 176, 194], np.float32)
d_bg = np.linalg.norm(a - bg, axis=2); d_h = np.linalg.norm(a - halo, axis=2)
luma = a @ np.array([.299, .587, .114], np.float32)
sat = a.max(2) - a.min(2)
passable = (d_bg < 28) | ((d_h < 40) & (luma < 205) & (a[..., 2] > a[..., 0] + 15))
lab, n = ndi.label(passable)
border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
bgmask = np.isin(lab, list(border))
bgmask |= (d_h < 26) & (luma < 195)   # leftover halo pockets (near the sparkles and the inner edge)
# keep gold sparkles: pixels clearly yellow are never background
bgmask &= ~((a[..., 0] > 200) & (a[..., 2] < a[..., 0] - 60))
fg = ~bgmask
fg = ndi.binary_fill_holes(fg)
fg = ndi.binary_opening(fg, iterations=2)
# drop tiny specks
l2, n2 = ndi.label(fg); sizes = ndi.sum(fg, l2, range(1, n2 + 1))
fg = np.isin(l2, [i + 1 for i, s in enumerate(sizes) if s > 400])
fg = ndi.binary_erosion(fg, iterations=1)
alpha = Image.fromarray((fg * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))
out = im.convert('RGBA'); out.putalpha(alpha)
out = out.crop(out.getchannel('A').point(lambda v: 255 if v > 8 else 0).getbbox())
pad = 6; c = Image.new('RGBA', (out.width + 2 * pad, out.height + 2 * pad), (0, 0, 0, 0)); c.paste(out, (pad, pad))
if c.width > 1024: c = c.resize((1024, round(c.height * 1024 / c.width)), Image.LANCZOS)
c.save(dst); print(dst, c.size)
