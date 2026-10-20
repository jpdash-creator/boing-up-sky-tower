"""Renders the three font pairings on a mock of the game's screens, for the owner's choice (T21).
    set PYTHONPATH=E:/pylibs
    python Tools/art_pipeline/font_samples.py [out.png]
"""
import os
import sys

sys.path.insert(0, "E:/pylibs")
from PIL import Image, ImageDraw, ImageFont  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
F = os.path.join(ROOT, "Assets", "Art", "Fonts")
PAIRS = [
    ("A  Lilita One + Nunito Bold", "LilitaOne-Regular.ttf", "Nunito-Bold.ttf"),
    ("B  Luckiest Guy + Fredoka SemiBold", "LuckiestGuy-Regular.ttf", "Fredoka-SemiBold.ttf"),
    ("C  Baloo 2 ExtraBold + Baloo 2 SemiBold", "Baloo2-ExtraBold.ttf", "Baloo2-SemiBold.ttf"),
]
W, H = 540, 960


def outlined(d, xy, text, font, fill, outline=(20, 16, 40), r=3, anchor="mm"):
    x, y = xy
    for dx in range(-r, r + 1):
        for dy in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r:
                d.text((x + dx, y + dy), text, font=font, fill=outline, anchor=anchor)
    d.text(xy, text, font=font, fill=fill, anchor=anchor)


def panel(title, disp, body):
    im = Image.new("RGB", (W, H), (70, 170, 235))
    d = ImageDraw.Draw(im)
    for i in range(0, H, 4):   # simple sky gradient
        d.rectangle([0, i, W, i + 4], fill=(70 + i // 14, 170 + i // 30, 235 - i // 12))
    d.rectangle([0, 800, W, H], fill=(70, 160, 60))
    D = lambda s: ImageFont.truetype(os.path.join(F, disp), s)
    B = lambda s: ImageFont.truetype(os.path.join(F, body), s)
    d.text((10, 8), title, font=B(20), fill=(255, 255, 255))
    outlined(d, (W // 2, 120), "SKY", D(96), (255, 255, 255), r=5)
    outlined(d, (W // 2, 205), "TOWER", D(96), (255, 214, 64), r=5)
    # HUD
    outlined(d, (22, 300), "Floor 12 / 60", D(34), (255, 255, 255), anchor="lm")
    outlined(d, (22, 336), "Reach floor 60", B(26), (255, 240, 150), r=2, anchor="lm")
    outlined(d, (470, 300), "148", D(36), (255, 255, 255), anchor="rm")
    outlined(d, (W // 2, 400), "COMBO x4", D(52), (255, 214, 64), r=4)
    # button
    d.rounded_rectangle([140, 480, 400, 560], 28, fill=(250, 130, 180), outline=(120, 30, 80), width=4)
    d.text((270, 520), "PLAY", font=D(54), fill=(40, 12, 40), anchor="mm")
    d.rounded_rectangle([170, 590, 370, 650], 22, fill=(90, 190, 240), outline=(20, 70, 120), width=4)
    d.text((270, 620), "Settings", font=B(34), fill=(20, 12, 40), anchor="mm")
    # level node and popup
    d.ellipse([60, 690, 160, 790], fill=(120, 220, 140), outline=(30, 110, 50), width=5)
    d.text((110, 738), "1", font=D(60), fill=(30, 20, 50), anchor="mm")
    d.rounded_rectangle([200, 680, 520, 790], 20, fill=(252, 238, 214), outline=(90, 50, 40), width=4)
    d.text((360, 712), "Level clear!", font=D(34), fill=(160, 40, 80), anchor="mm")
    d.text((360, 756), "Coins +66   Gems +1", font=B(24), fill=(51, 28, 41), anchor="mm")
    d.text((W // 2, 850), "Hold  <  >  to move, tap JUMP", font=B(26), fill=(255, 255, 255), anchor="mm")
    d.text((W // 2, 900), "0123456789  Floor Score Best combo", font=B(22), fill=(255, 255, 255), anchor="mm")
    return im


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "E:/Unity/qa/font_samples.png"
    sheet = Image.new("RGB", (W * 3 + 40, H), (30, 30, 40))
    for i, (t, d, b) in enumerate(PAIRS):
        sheet.paste(panel(t, d, b), (i * (W + 20), 0))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    sheet.save(out)
    print(out)


if __name__ == "__main__":
    main()
