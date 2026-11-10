"""Draws icon.png: a riveted bronze dome cage on a base plate, struck through the middle by a glowing electric-blue
lightning bolt, in the shared s992 icon style.

Run with Pillow from the repo root (writes this mod's icon.png unless given another path):
    python mods/Thunderdome/scripts/make_icon.py
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "tools"))
OUT = sys.argv[1] if len(sys.argv) > 1 else str(Path(__file__).resolve().parents[1] / "icon.png")
from icon_kit import *  # noqa: E402,F403

BOLT, BOLT_CORE = (110, 190, 255), (235, 248, 255)

CX, GROUND, R = 512, 770, 360  # dome centre on the base plate, and its radius
RIB = 30  # thickness of the dome's bronze bands


def upper(draw_ring):
    """A mask of a ring drawn by draw_ring, cut off at the base plate so only the dome's upper half shows."""
    def fn(m):
        draw_ring(m)
        m.rectangle([0, GROUND, S, S], fill=0)
    return mask_from(fn)


def ring(rx, ry, width):
    def fn(m):
        m.ellipse([CX - rx, GROUND - ry, CX + rx, GROUND + ry], fill=255)
        m.ellipse([CX - rx + width, GROUND - ry + width, CX + rx - width, GROUND + ry - width], fill=0)
    return fn


# The outer arch, two meridians and a latitude band make the cage
bands = [ring(R, R, RIB), ring(R * 0.55, R, RIB * 0.8), ring(R * 0.18, R, RIB * 0.7)]
latitude_y = GROUND - R * 0.55
plate = [CX - R - 70, GROUND - 6, CX + R + 70, GROUND + 70]

bolt = [(548, 92), (430, 392), (512, 400), (402, 640), (470, 640), (420, 770),
        (592, 560), (520, 552), (626, 342), (546, 336), (628, 92)]


def everything(d):
    d.ellipse([CX - R, GROUND - R, CX + R, GROUND + R], fill=255)
    d.rectangle([0, GROUND + 70, S, S], fill=0)
    d.rounded_rectangle(plate, radius=26, fill=255)


img = add_shadow(img := background(seed=41), everything)

# A faint blue haze inside the dome, like the traders' force field
haze = Image.new("RGBA", (S, S), BOLT + (0,))
haze.putalpha(upper(lambda m: m.ellipse([CX - R, GROUND - R, CX + R, GROUND + R], fill=255)).point(lambda v: v * 46 // 255))
img = Image.alpha_composite(img, haze)

# The bolt's wide glow goes behind the cage, so the bands stand out against it
img = add_glow(img, lambda m: m.polygon(bolt, fill=255), BOLT, blur=60, strength=3)

# Cage: iron outlines first, then bronze bands lit from the upper left
for band in bands:
    outline = upper(band).filter(ImageFilter.MaxFilter(13))
    img.alpha_composite(Image.composite(Image.new("RGBA", (S, S), IRON), Image.new("RGBA", (S, S), (0, 0, 0, 0)), outline))
lat = mask_from(lambda m: m.rectangle([0, latitude_y - RIB * 0.4, S, latitude_y + RIB * 0.4], fill=255))
dome_mask = upper(lambda m: m.ellipse([CX - R + 6, GROUND - R + 6, CX + R - 6, GROUND + R - 6], fill=255))
lat = ImageChops.multiply(lat, dome_mask)
img.alpha_composite(Image.composite(Image.new("RGBA", (S, S), IRON), Image.new("RGBA", (S, S), (0, 0, 0, 0)),
                                    lat.filter(ImageFilter.MaxFilter(13))))
cage = lat
for band in bands:
    cage = ImageChops.lighter(cage, upper(band))
img.alpha_composite(linear_fill(cage, BRONZE_LIGHT, BRONZE_DARK, angle_deg=55))

# The bolt itself, in front of the cage: iron edge, blue body, white-hot core
d = ImageDraw.Draw(img)
img = add_glow(img, lambda m: m.polygon(bolt, fill=255), BOLT, blur=18, strength=2)
d = ImageDraw.Draw(img)
d.polygon(bolt, fill=BOLT + (255,))
core = [(552, 130), (462, 384), (540, 392), (440, 616), (500, 616), (452, 728),
        (568, 572), (498, 566), (592, 352), (520, 346), (600, 130)]
d.polygon(core, fill=BOLT_CORE + (255,))

# Base plate with rivets, and a burst where the bolt hits it
img = add_glow(img, lambda m: m.ellipse([340, 700, 520, 820], fill=255), BOLT, blur=40, strength=3)
d = ImageDraw.Draw(img)
d.rounded_rectangle([plate[0] - 12, plate[1] - 12, plate[2] + 12, plate[3] + 12], radius=34, fill=IRON)
img.alpha_composite(linear_fill(mask_from(lambda m: m.rounded_rectangle(plate, radius=26, fill=255)),
                                BRONZE_LIGHT, BRONZE_DARK, angle_deg=20))
rivets(img, [(plate[0] + 40, GROUND + 32), (plate[2] - 40, GROUND + 32), (CX - 190, GROUND + 32), (CX + 190, GROUND + 32)],
       radius=16)
img = add_glow(img, lambda m: m.ellipse([396, 742, 456, 790], fill=255), BOLT_CORE, blur=16, strength=3)

save_icon(img, OUT)
