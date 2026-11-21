"""Draws icon.png: a riveted bronze anvil standing in a glowing ground circle, with a wider dashed circle and
outward arrows around it for the bigger build radius, in the shared s992 icon style.

Run with Pillow from the repo root (writes this mod's icon.png unless given another path):
    python mods/BuildRadius/scripts/make_icon.py
"""
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "tools"))
OUT = sys.argv[1] if len(sys.argv) > 1 else str(Path(__file__).resolve().parents[1] / "icon.png")
from icon_kit import *  # noqa: E402,F403

RING, RING_CORE = (90, 255, 170), (225, 255, 240)

CX, CY = 512, 640  # centre of the ground circles (seen in perspective, so flattened)
FLAT = 0.36  # vertical squash of the circles


def ellipse_box(r):
    return [CX - r, CY - r * FLAT, CX + r, CY + r * FLAT]


def ring_mask(r, width):
    def fn(m):
        m.ellipse(ellipse_box(r), fill=255)
        m.ellipse(ellipse_box(r - width), fill=0)
    return fn


def dashed_ring(r, width, dashes, duty=0.55):
    """A flattened ring cut into dashes, like the game's station circle."""
    def fn(m):
        m.ellipse(ellipse_box(r), fill=255)
        m.ellipse(ellipse_box(r - width), fill=0)
        for i in range(dashes):
            a0 = 2 * math.pi * (i + duty) / dashes
            a1 = 2 * math.pi * (i + 1) / dashes
            pts = [(CX, CY)]
            for k in range(6):
                a = a0 + (a1 - a0) * k / 5
                pts.append((CX + math.cos(a) * r * 1.2, CY + math.sin(a) * r * FLAT * 1.2))
            m.polygon(pts, fill=0)
    return fn


def arrow(x, direction):
    """An outward-pointing arrowhead on the circles' horizontal axis."""
    tip = x + direction * 70
    return [(x, CY - 46), (tip, CY), (x, CY + 46), (x + direction * 18, CY)]


inner_r, outer_r = 250, 430

img = background(seed=23)

# The circles glow on the ground first, so the anvil stands in front of them
img = add_glow(img, ring_mask(inner_r, 26), RING, blur=34, strength=3)
img = add_glow(img, dashed_ring(outer_r, 22, 18), RING, blur=26, strength=2)
d = ImageDraw.Draw(img)
img.alpha_composite(Image.composite(Image.new("RGBA", (S, S), RING + (255,)), Image.new("RGBA", (S, S), (0, 0, 0, 0)),
                                    mask_from(dashed_ring(outer_r, 22, 18))))
img.alpha_composite(Image.composite(Image.new("RGBA", (S, S), RING + (255,)), Image.new("RGBA", (S, S), (0, 0, 0, 0)),
                                    mask_from(ring_mask(inner_r, 26))))
img.alpha_composite(Image.composite(Image.new("RGBA", (S, S), RING_CORE + (255,)), Image.new("RGBA", (S, S), (0, 0, 0, 0)),
                                    mask_from(ring_mask(inner_r - 8, 10))))

# Arrows pushing out from the inner circle to the outer one
for direction in (-1, 1):
    x = CX + direction * (inner_r + 40)
    img = add_glow(img, lambda m, x=x, dr=direction: m.polygon(arrow(x, dr), fill=255), RING, blur=16, strength=2)
    d = ImageDraw.Draw(img)
    d.polygon(arrow(x, direction), fill=RING_CORE + (255,))

# The anvil: horn to the left, flat face, waist and a wide foot, standing on the circle's centre
face = [(300, 380), (690, 380), (720, 400), (720, 470), (300, 470)]
horn = [(300, 384), (170, 402), (300, 466)]
waist = [(410, 470), (610, 470), (570, 560), (450, 560)]
foot = [(360, 560), (660, 560), (700, 640), (320, 640)]


def grow(points, k=1.25, origin=(512, 640)):
    """Scales a shape about the anvil's foot, which stays on the circle's centre."""
    return [(origin[0] + (x - origin[0]) * k, origin[1] + (y - origin[1]) * k) for x, y in points]


face, horn, waist, foot = (grow(p) for p in (face, horn, waist, foot))
edge = grow([(304, 384), (688, 384), (712, 400), (304, 400)])


def anvil(m):
    for part in (face, horn, waist, foot):
        m.polygon(part, fill=255)


img = add_shadow(img, anvil)
outline = mask_from(anvil).filter(ImageFilter.MaxFilter(17))
img.alpha_composite(Image.composite(Image.new("RGBA", (S, S), IRON), Image.new("RGBA", (S, S), (0, 0, 0, 0)), outline))
img.alpha_composite(linear_fill(mask_from(anvil), BRONZE_LIGHT, BRONZE_DARK, angle_deg=55))
# A bright edge along the face, where light catches it
d = ImageDraw.Draw(img)
d.polygon(edge, fill=(255, 226, 160, 255))
rivets(img, grow([(360, 600), (512, 600), (664, 600), (510, 430)]), radius=17)

save_icon(img, OUT)
