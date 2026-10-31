"""LootFilter icon (drawn before tools/icon_kit.py existed, so it carries its own helpers).

Run with Pillow from the repo root (writes this mod's icon.png unless given another path):
    python mods/LootFilter/scripts/make_icon.py
"""
import math
import random
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

S = 1024  # supersampled; downscaled to 256 at the end
random.seed(7)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def radial(size, inner, outer, center, radius):
    img = Image.new("RGB", (size, size))
    px = img.load()
    cx, cy = center
    for y in range(size):
        for x in range(size):
            t = min(1.0, math.hypot(x - cx, y - cy) / radius)
            px[x, y] = lerp(inner, outer, t ** 1.4)
    return img


def linear_v(size, top, bottom, box):
    """Vertical gradient clipped later by a mask."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = box
    for y in range(int(y0), int(y1) + 1):
        t = (y - y0) / max(1, (y1 - y0))
        d.line([(x0, y), (x1, y)], fill=lerp(top, bottom, t) + (255,))
    return img


def glow(shape_fn, color, blur, strength=2):
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    shape_fn(ImageDraw.Draw(layer), color + (255,))
    layer = layer.filter(ImageFilter.GaussianBlur(blur))
    out = layer
    for _ in range(strength - 1):
        out = Image.alpha_composite(out, layer)
    return out


# --- background: same dark chart as the other icons ---
bg = radial(S, (44, 54, 62), (12, 15, 19), (S * 0.42, S * 0.38), S * 0.85).convert("RGBA")
overlay = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(overlay)
for k in range(9):
    pts = []
    base = 120 + k * 105
    phase = random.uniform(0, math.tau)
    for x in range(-20, S + 40, 16):
        y = base + 38 * math.sin(x / 140 + phase) + 18 * math.sin(x / 57 + phase * 2)
        pts.append((x, y))
    d.line(pts, fill=(120, 140, 150, 40), width=5, joint="curve")
bg = Image.alpha_composite(bg, overlay)

IRON = (34, 22, 12, 255)
cx = 512

# --- funnel ---
rim_y, rim_half = 330, 330          # wide top
neck_y, neck_half = 640, 70         # where the cone meets the spout
spout_bottom, spout_half = 760, 52

funnel_poly = [
    (cx - rim_half, rim_y), (cx + rim_half, rim_y),
    (cx + neck_half, neck_y), (cx + spout_half, neck_y + 10),
    (cx + spout_half, spout_bottom), (cx - spout_half, spout_bottom),
    (cx - spout_half, neck_y + 10), (cx - neck_half, neck_y),
]


def poly_mask(points, grow=0):
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).polygon(points, fill=255)
    if grow:
        m = m.filter(ImageFilter.MaxFilter(grow * 2 + 1))
    return m


# drop shadow
shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
shadow.putalpha(poly_mask(funnel_poly, 10).filter(ImageFilter.GaussianBlur(24)).point(lambda v: int(v * 0.6)))
shifted = Image.new("RGBA", (S, S), (0, 0, 0, 0))
shifted.paste(shadow, (16, 24))
bg = Image.alpha_composite(bg, shifted)

# iron outline then bronze body (light from the upper left)
outline = Image.new("RGBA", (S, S), IRON)
outline.putalpha(poly_mask(funnel_poly, 14))
bg = Image.alpha_composite(bg, outline)

body = Image.new("RGBA", (S, S), (0, 0, 0, 0))
px_src = Image.new("RGB", (S, S))
pd = px_src.load()
for y in range(rim_y - 20, spout_bottom + 20):
    for x in range(cx - rim_half - 20, cx + rim_half + 20):
        t = ((x - (cx - rim_half)) / (2 * rim_half)) * 0.75 + ((y - rim_y) / (spout_bottom - rim_y)) * 0.25
        pd[x, y] = lerp((246, 196, 102), (120, 68, 22), max(0.0, min(1.0, t)))
body = px_src.convert("RGBA")
body.putalpha(poly_mask(funnel_poly))
bg = Image.alpha_composite(bg, body)

# bands and rivets
bd = ImageDraw.Draw(bg)
for frac in (0.35, 0.7):
    y = rim_y + (neck_y - rim_y) * frac
    half = rim_half + (neck_half - rim_half) * frac
    bd.line([(cx - half + 8, y), (cx + half - 8, y)], fill=IRON, width=16)
    for k in range(5):
        rx = cx - half * 0.75 + k * (half * 1.5 / 4)
        bd.ellipse([rx - 11, y - 11, rx + 11, y + 11], fill=(70, 42, 16, 255))
        bd.ellipse([rx - 7, y - 8, rx + 4, y + 1], fill=(255, 226, 160, 255))
bd.line([(cx - spout_half + 6, spout_bottom - 40), (cx + spout_half - 6, spout_bottom - 40)], fill=IRON, width=14)

# grate across the mouth
rim_box = [cx - rim_half - 14, rim_y - 40, cx + rim_half + 14, rim_y + 22]
bd.rounded_rectangle(rim_box, radius=16, fill=(58, 40, 24, 255), outline=IRON, width=12)
for k in range(1, 12):
    x = rim_box[0] + k * (rim_box[2] - rim_box[0]) / 12
    bd.line([(x, rim_box[1] + 10), (x, rim_box[3] - 10)], fill=(24, 16, 10, 255), width=8)

# --- junk stuck on the grate: dull grey stones, no glow ---
def stone(draw, box, base):
    x0, y0, x1, y1 = box
    draw.ellipse(box, fill=IRON)
    draw.ellipse([x0 + 8, y0 + 8, x1 - 8, y1 - 8], fill=base + (255,))
    draw.ellipse([x0 + 20, y0 + 16, x0 + (x1 - x0) * 0.45, y0 + (y1 - y0) * 0.4], fill=lerp(base, (255, 255, 255), 0.25) + (255,))

sd = ImageDraw.Draw(bg)
stone(sd, [240, 196, 380, 300], (104, 108, 112))
stone(sd, [600, 214, 700, 296], (92, 96, 100))
stone(sd, [690, 180, 800, 292], (112, 112, 108))

# --- the good drop: a glowing faceted gem falling out of the spout ---
gx, gy, gr = cx, 860, 78
gem = [(gx, gy - gr), (gx + gr * 0.85, gy - gr * 0.25), (gx + gr * 0.55, gy + gr * 0.8),
       (gx - gr * 0.55, gy + gr * 0.8), (gx - gr * 0.85, gy - gr * 0.25)]
GEM = (190, 90, 255)
bg = Image.alpha_composite(bg, glow(lambda dr, c: dr.ellipse([gx - 150, gy - 150, gx + 150, gy + 150], fill=c), GEM, 60, 2))
gd = ImageDraw.Draw(bg)
gd.polygon(gem, fill=IRON)
inner = [(gx + (x - gx) * 0.86, gy + (y - gy) * 0.86) for x, y in gem]
gd.polygon(inner, fill=(150, 60, 230, 255))
# facets
gd.polygon([(gx, gy - gr * 0.86), inner[1], (gx, gy + gr * 0.1)], fill=(205, 140, 255, 255))
gd.polygon([(gx, gy - gr * 0.86), inner[4], (gx, gy + gr * 0.1)], fill=(235, 190, 255, 255))
gd.polygon([inner[4], (gx, gy + gr * 0.1), inner[3]], fill=(120, 40, 200, 255))
# sparkle
for (sx, sy, r) in ((gx - 120, gy - 70, 16), (gx + 118, gy - 30, 12), (gx + 70, gy + 96, 10)):
    gd.polygon([(sx, sy - r * 2.2), (sx + r * 0.5, sy), (sx, sy + r * 2.2), (sx - r * 0.5, sy)], fill=(255, 240, 255, 255))
    gd.polygon([(sx - r * 2.2, sy), (sx, sy + r * 0.5), (sx + r * 2.2, sy), (sx, sy - r * 0.5)], fill=(255, 240, 255, 255))

# rounded-square mask and downscale
mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=150, fill=255)
final = Image.new("RGBA", (S, S), (0, 0, 0, 0))
final.paste(bg, (0, 0), mask)
final = final.resize((256, 256), Image.LANCZOS)
OUT = sys.argv[1] if len(sys.argv) > 1 else str(Path(__file__).resolve().parents[1] / "icon.png")
final.save(OUT)
print("saved", final.size)
