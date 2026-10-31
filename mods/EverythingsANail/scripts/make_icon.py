"""Draws icon.png: a bronze hammer striking a glowing hot nail, in the shared s992 icon style.

Run with Pillow from the repo root (writes this mod's icon.png unless given another path):
    python mods/EverythingsANail/scripts/make_icon.py
"""
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "tools"))
OUT = sys.argv[1] if len(sys.argv) > 1 else str(Path(__file__).resolve().parents[1] / "icon.png")
from icon_kit import *  # noqa: E402,F403

img = background(seed=11)

# Handle runs from the lower left up to the head; the head is perpendicular to it
hx0, hy0 = 170, 850
hx1, hy1 = 400, 380
ux, uy = hx1 - hx0, hy1 - hy0
length = math.hypot(ux, uy)
ux, uy = ux / length, uy / length          # along the handle
px, py = -uy, ux                           # perpendicular (head axis), pointing down-right


def quad(cx, cy, along, across, ax, ay, bx, by):
    """Rectangle centred on (cx, cy): `along` half-length on (ax, ay), `across` half-width on (bx, by)."""
    return [
        (cx + ax * along + bx * across, cy + ay * along + by * across),
        (cx + ax * along - bx * across, cy + ay * along - by * across),
        (cx - ax * along - bx * across, cy - ay * along - by * across),
        (cx - ax * along + bx * across, cy - ay * along + by * across),
    ]


handle = quad((hx0 + hx1) / 2, (hy0 + hy1) / 2, length / 2, 38, ux, uy, px, py)
head_cx, head_cy = hx1 + ux * 40, hy1 + uy * 40
head = quad(head_cx, head_cy, 190, 82, px, py, ux, uy)
# Striking face: the end of the head pointing at the nail
face_x, face_y = head_cx + px * 190, head_cy + py * 190
face = quad(face_x + px * 22, face_y + py * 22, 26, 96, px, py, ux, uy)

# Nail: point away from the face, along the strike direction
nail_head_c = (face_x + px * 70, face_y + py * 70)
nail_head = quad(nail_head_c[0], nail_head_c[1], 16, 70, px, py, ux, uy)
shaft_end = (nail_head_c[0] + px * 230, nail_head_c[1] + py * 230)
shaft = quad((nail_head_c[0] + shaft_end[0]) / 2, (nail_head_c[1] + shaft_end[1]) / 2, 115, 22, px, py, ux, uy)
tip = [
    (shaft_end[0] + ux * 22, shaft_end[1] + uy * 22),
    (shaft_end[0] - ux * 22, shaft_end[1] - uy * 22),
    (shaft_end[0] + px * 50, shaft_end[1] + py * 50),
]

everything = lambda d: (d.polygon(handle, fill=255), d.polygon(head, fill=255), d.polygon(face, fill=255),
                        d.polygon(nail_head, fill=255), d.polygon(shaft, fill=255), d.polygon(tip, fill=255))
img = add_shadow(img, everything)

# Hot glow around the nail
img = add_glow(img, lambda d: d.ellipse([nail_head_c[0] - 150, nail_head_c[1] - 150,
                                         shaft_end[0] + 110, shaft_end[1] + 110], fill=255),
               ORANGE, blur=70, strength=3)

d = ImageDraw.Draw(img)


def outlined(points, grow, fill):
    # Iron outline: the same shape pushed outwards
    cx = sum(p[0] for p in points) / len(points)
    cy = sum(p[1] for p in points) / len(points)
    big = [(cx + (x - cx) * 1.0 + (grow if x > cx else -grow), cy + (y - cy) * 1.0 + (grow if y > cy else -grow)) for x, y in points]
    d.polygon(big, fill=IRON)
    d.polygon(points, fill=fill)


# Handle: wood with an iron grip band
outlined(handle, 12, WOOD + (255,))
grip = quad(hx0 + ux * 120, hy0 + uy * 120, 70, 44, ux, uy, px, py)
d.polygon(grip, fill=IRON)
for k in range(3):
    g = quad(hx0 + ux * (80 + k * 40), hy0 + uy * (80 + k * 40), 8, 40, ux, uy, px, py)
    d.polygon(g, fill=(70, 42, 16, 255))

# Head: bronze lit from the upper left
outlined(head, 14, IRON[:3] + (255,))
head_mask = mask_from(lambda m: m.polygon(head, fill=255))
img.alpha_composite(linear_fill(head_mask, BRONZE_LIGHT, BRONZE_DARK, angle_deg=45))
d = ImageDraw.Draw(img)
outlined(face, 10, (150, 96, 40, 255))
rivets(img, [
    (head_cx + px * 110 + ux * 45, head_cy + py * 110 + uy * 45),
    (head_cx + px * 110 - ux * 45, head_cy + py * 110 - uy * 45),
    (head_cx - px * 110 + ux * 45, head_cy - py * 110 + uy * 45),
    (head_cx - px * 110 - ux * 45, head_cy - py * 110 - uy * 45),
], radius=16)
d = ImageDraw.Draw(img)

# Nail: glowing hot iron
HOT, HOT_LIGHT = (255, 120, 30), (255, 230, 150)
d.polygon(shaft, fill=HOT + (255,))
d.polygon(tip, fill=HOT + (255,))
d.polygon(quad((nail_head_c[0] + shaft_end[0]) / 2 - ux * 8, (nail_head_c[1] + shaft_end[1]) / 2 - uy * 8, 110, 7, px, py, ux, uy),
          fill=HOT_LIGHT + (255,))
d.polygon(nail_head, fill=HOT + (255,))
d.polygon(quad(nail_head_c[0], nail_head_c[1], 6, 60, px, py, ux, uy), fill=HOT_LIGHT + (255,))

# Sparks flying from the strike
for angle, dist, r in ((-70, 170, 14), (-30, 200, 11), (200, 160, 12), (240, 190, 9), (10, 210, 8)):
    a = math.radians(angle)
    sx, sy = face_x + px * 40 + math.cos(a) * dist, face_y + py * 40 + math.sin(a) * dist
    d.polygon([(sx, sy - r * 2.2), (sx + r * 0.5, sy), (sx, sy + r * 2.2), (sx - r * 0.5, sy)], fill=HOT_LIGHT + (255,))
    d.polygon([(sx - r * 2.2, sy), (sx, sy + r * 0.5), (sx + r * 2.2, sy), (sx, sy - r * 0.5)], fill=HOT_LIGHT + (255,))

save_icon(img, OUT)
