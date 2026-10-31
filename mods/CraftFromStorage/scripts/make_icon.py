"""CraftFromStorage icon: an open iron-banded chest with warm light and a glowing ingot rising out of it.

Run with Pillow from the repo root (writes this mod's icon.png unless given another path):
    python mods/CraftFromStorage/scripts/make_icon.py
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "tools"))
OUT = sys.argv[1] if len(sys.argv) > 1 else str(Path(__file__).resolve().parents[1] / "icon.png")
from icon_kit import *  # noqa: E402,F401

AMBER = (255, 160, 50)
img = background()
cx = 512

# --- chest geometry ---
body = [cx - 300, 560, cx + 300, 850]           # box
lid_back = [cx - 300, 330, cx + 300, 560]        # open lid, seen from the front (tilted back)

def chest_shape(d):
    d.rounded_rectangle(body, radius=26, fill=255)
    d.polygon([(cx - 300, 560), (cx + 300, 560), (cx + 270, 360), (cx - 270, 360)], fill=255)

img = add_shadow(img, chest_shape)

# light pouring out of the open chest, behind the lid so the lid stays readable
img = add_glow(img, lambda g: g.ellipse([cx - 380, 300, cx + 380, 640], fill=255), AMBER, blur=70, strength=3)
d = ImageDraw.Draw(img)
# open lid: inner face (dark wood), with iron rim
d.polygon([(cx - 312, 572), (cx + 312, 572), (cx + 282, 348), (cx - 282, 348)], fill=IRON)
d.polygon([(cx - 290, 556), (cx + 290, 556), (cx + 262, 370), (cx - 262, 370)], fill=(70, 44, 24, 255))
for k in range(1, 5):
    y = 370 + k * (556 - 370) / 5
    half = 262 + (290 - 262) * (k / 5)
    d.line([(cx - half + 6, y), (cx + half - 6, y)], fill=(52, 32, 18, 255), width=6)


# chest body: iron outline, bronze front
d.rounded_rectangle([body[0] - 14, body[1] - 6, body[2] + 14, body[3] + 14], radius=34, fill=IRON)
front = linear_fill(Image.new("L", (S, S), 0), BRONZE_LIGHT, BRONZE_DARK)
m = Image.new("L", (S, S), 0)
ImageDraw.Draw(m).rounded_rectangle([body[0], body[1] + 6, body[2], body[3]], radius=24, fill=255)
front = linear_fill(m, BRONZE_LIGHT, BRONZE_DARK)
img = Image.alpha_composite(img, front)
d = ImageDraw.Draw(img)
# iron bands, lock plate
for x in (cx - 190, cx + 190):
    d.rectangle([x - 20, body[1] + 6, x + 20, body[3]], fill=IRON)
d.rectangle([body[0], 640, body[2], 668], fill=IRON)
d.rounded_rectangle([cx - 46, 600, cx + 46, 712], radius=12, fill=IRON)
d.rounded_rectangle([cx - 30, 616, cx + 30, 696], radius=8, fill=(214, 160, 70, 255))
d.ellipse([cx - 11, 640, cx + 11, 662], fill=IRON)
d.rectangle([cx - 5, 656, cx + 5, 680], fill=IRON)
rivets(img, [(cx - 190, 600), (cx - 190, 800), (cx + 190, 600), (cx + 190, 800),
             (body[0] + 40, 654), (body[2] - 40, 654)])

# the gap between lid and box, where the light comes out
img = add_glow(img, lambda g: g.rectangle([cx - 270, 530, cx + 270, 562], fill=255), AMBER, blur=18, strength=2)
d = ImageDraw.Draw(img)
d.rounded_rectangle([cx - 276, 536, cx + 276, 558], radius=8, fill=(255, 226, 160, 255))

# --- glowing ingot rising out of the chest ---
iy = 250
img = add_glow(img, lambda g: g.ellipse([cx - 170, iy - 120, cx + 170, iy + 120], fill=255), AMBER, blur=50, strength=2)
d = ImageDraw.Draw(img)
top = [(cx - 96, iy - 40), (cx + 96, iy - 40), (cx + 130, iy + 6), (cx - 130, iy + 6)]
side = [(cx - 130, iy + 6), (cx + 130, iy + 6), (cx + 130, iy + 56), (cx - 130, iy + 56)]
d.polygon([(cx - 108, iy - 52), (cx + 108, iy - 52), (cx + 144, iy + 4), (cx + 144, iy + 68), (cx - 144, iy + 68), (cx - 144, iy + 4)], fill=IRON)
d.polygon(side, fill=(220, 120, 30, 255))
d.polygon(top, fill=(255, 210, 120, 255))
d.line([(cx - 80, iy - 26), (cx + 40, iy - 26)], fill=(255, 245, 210, 255), width=10)

# sparks between chest and ingot
for (sx, sy, r) in ((cx - 200, 380, 14), (cx + 210, 330, 11), (cx + 120, 440, 9), (cx - 110, 470, 8)):
    d.polygon([(sx, sy - r * 2.2), (sx + r * 0.5, sy), (sx, sy + r * 2.2), (sx - r * 0.5, sy)], fill=(255, 236, 190, 255))
    d.polygon([(sx - r * 2.2, sy), (sx, sy + r * 0.5), (sx + r * 2.2, sy), (sx, sy - r * 0.5)], fill=(255, 236, 190, 255))

save_icon(img, OUT)
print("saved", OUT)
