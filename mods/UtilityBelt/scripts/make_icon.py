"""Draws icon.png: a leather belt with pouches and a bronze buckle set with a glowing wisp-blue gem, in the shared
s992 icon style.

Run with Pillow from the repo root (writes this mod's icon.png unless given another path):
    python mods/UtilityBelt/scripts/make_icon.py
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "tools"))
OUT = sys.argv[1] if len(sys.argv) > 1 else str(Path(__file__).resolve().parents[1] / "icon.png")
from icon_kit import *  # noqa: E402,F403

LEATHER_LIGHT, LEATHER_DARK = (150, 92, 48), (70, 38, 16)
STITCH = (214, 170, 110, 255)
WISP, WISP_LIGHT = (90, 190, 255), (225, 245, 255)

img = background(seed=23)


def centre_y(x):
    # The belt sags in the middle, like one lying in a curve
    return 560 - 0.00055 * (x - 512) ** 2


def band(x0, x1, half, step=8):
    top = [(x, centre_y(x) - half) for x in range(x0, x1 + 1, step)]
    bottom = [(x, centre_y(x) + half) for x in range(x1, x0 - 1, -step)]
    return top + bottom


def pouch_shape(cx, top, w, h):
    return [cx - w / 2, top, cx + w / 2, top + h]


belt = band(70, 954, 62)
pouches = [(250, 200, 220), (774, 200, 220)]


def everything(d):
    d.polygon(belt, fill=255)
    for cx, w, h in pouches:
        d.rounded_rectangle(pouch_shape(cx, centre_y(cx) - 10, w, h), radius=40, fill=255)
    d.rounded_rectangle([392, 440, 632, 680], radius=36, fill=255)


img = add_shadow(img, everything)
d = ImageDraw.Draw(img)

# Belt: iron outline, leather lit from the upper left, stitching along both edges
d.polygon(band(62, 962, 72), fill=IRON)
img.alpha_composite(linear_fill(mask_from(lambda m: m.polygon(belt, fill=255)), LEATHER_LIGHT, LEATHER_DARK, angle_deg=70))
d = ImageDraw.Draw(img)
for edge in (-44, 44):
    for x in range(96, 930, 36):
        y0, y1 = centre_y(x) + edge, centre_y(x + 18) + edge
        d.line([(x, y0), (x + 18, y1)], fill=STITCH, width=6)

# Pouches hanging from the belt, each with a flap and a rivet
for cx, w, h in pouches:
    top = centre_y(cx) - 10
    d.rounded_rectangle([cx - w / 2 - 12, top - 12, cx + w / 2 + 12, top + h + 12], radius=48, fill=IRON)
    body = pouch_shape(cx, top, w, h)
    img.alpha_composite(linear_fill(mask_from(lambda m: m.rounded_rectangle(body, radius=40, fill=255)),
                                    LEATHER_LIGHT, LEATHER_DARK, angle_deg=60))
    d = ImageDraw.Draw(img)
    flap = [(cx - w / 2, top), (cx + w / 2, top), (cx + w / 2, top + 80), (cx, top + 120), (cx - w / 2, top + 80)]
    d.polygon(flap, fill=(108, 64, 30, 255), outline=IRON[:3] + (255,), width=8)
    rivets(img, [(cx, top + 92)], radius=16)
    d = ImageDraw.Draw(img)

# Buckle: a bronze frame with the belt showing through, set with a glowing gem. The wide glow goes behind it.
img = add_glow(img, lambda m: m.ellipse([360, 408, 664, 712], fill=255), WISP, blur=70, strength=2)
d = ImageDraw.Draw(img)
d.rounded_rectangle([380, 428, 644, 692], radius=44, fill=IRON)
frame_mask = mask_from(lambda m: (m.rounded_rectangle([392, 440, 632, 680], radius=36, fill=255),
                                  m.rounded_rectangle([446, 494, 578, 626], radius=20, fill=0)))
img.alpha_composite(linear_fill(frame_mask, BRONZE_LIGHT, BRONZE_DARK, angle_deg=45))
rivets(img, [(418, 466), (606, 466), (418, 654), (606, 654)], radius=14)

img = add_glow(img, lambda m: m.ellipse([462, 510, 562, 610], fill=255), WISP, blur=22, strength=3)
d = ImageDraw.Draw(img)
d.ellipse([466, 514, 558, 606], fill=IRON)
d.ellipse([474, 522, 550, 598], fill=WISP + (255,))
d.ellipse([488, 532, 522, 564], fill=WISP_LIGHT + (255,))

save_icon(img, OUT)
