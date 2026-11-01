"""SearchEverything icon: a bronze magnifying glass whose glowing lens shows a small chest.

Run with Pillow from the repo root (writes this mod's icon.png unless given another path):
    python mods/SearchEverything/scripts/make_icon.py
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "tools"))
OUT = sys.argv[1] if len(sys.argv) > 1 else str(Path(__file__).resolve().parents[1] / "icon.png")
from icon_kit import *  # noqa: E402,F401

CYAN = (80, 210, 255)
img = background()

# --- geometry: lens upper left, handle down to the lower right ---
lx, ly, lr = 430, 420, 250          # lens centre and outer radius of the rim
rim = 46                             # bronze rim width
angle = math.radians(45)
hx0, hy0 = lx + (lr - 10) * math.cos(angle), ly + (lr - 10) * math.sin(angle)
hx1, hy1 = lx + 600 * math.cos(angle), ly + 600 * math.sin(angle)
hw = 58                              # handle half-width


def handle_poly(half, start=0.0, end=1.0):
    nx, ny = -math.sin(angle), math.cos(angle)
    ax, ay = hx0 + (hx1 - hx0) * start, hy0 + (hy1 - hy0) * start
    bx, by = hx0 + (hx1 - hx0) * end, hy0 + (hy1 - hy0) * end
    return [(ax + nx * half, ay + ny * half), (bx + nx * half, by + ny * half),
            (bx - nx * half, by - ny * half), (ax - nx * half, ay - ny * half)]


def glass_shape(d):
    d.ellipse([lx - lr, ly - lr, lx + lr, ly + lr], fill=255)
    d.polygon(handle_poly(hw + 14), fill=255)


img = add_shadow(img, glass_shape)
# the lens glows from behind, so its light spills past the rim
img = add_glow(img, lambda g: g.ellipse([lx - lr - 40, ly - lr - 40, lx + lr + 40, ly + lr + 40], fill=255),
               CYAN, blur=60, strength=2)

# --- handle: iron outline, bronze grip with iron bands ---
d = ImageDraw.Draw(img)
d.polygon(handle_poly(hw + 14), fill=IRON)
m = mask_from(lambda g: g.polygon(handle_poly(hw, 0.12, 0.98), fill=255))
img = Image.alpha_composite(img, linear_fill(m, BRONZE_LIGHT, BRONZE_DARK, angle_deg=-45))
d = ImageDraw.Draw(img)
for t in (0.32, 0.62, 0.86):
    d.polygon(handle_poly(hw + 6, t - 0.035, t + 0.035), fill=IRON)

# --- rim: iron outline, bronze ring ---
d.ellipse([lx - lr - 12, ly - lr - 12, lx + lr + 12, ly + lr + 12], fill=IRON)
ring = mask_from(lambda g: (g.ellipse([lx - lr, ly - lr, lx + lr, ly + lr], fill=255),
                            g.ellipse([lx - lr + rim, ly - lr + rim, lx + lr - rim, ly + lr - rim], fill=0)))
img = Image.alpha_composite(img, linear_fill(ring, BRONZE_LIGHT, BRONZE_DARK))
d = ImageDraw.Draw(img)
ir = lr - rim
d.ellipse([lx - ir - 2, ly - ir - 2, lx + ir + 2, ly + ir + 2], outline=IRON, width=12)

# --- lens: glowing glass ---
lens = mask_from(lambda g: g.ellipse([lx - ir + 8, ly - ir + 8, lx + ir - 8, ly + ir - 8], fill=255))
glass = radial((150, 235, 255), (20, 90, 130), (lx - 60, ly - 70), ir * 1.3)
glass_rgba = glass.convert("RGBA")
glass_rgba.putalpha(lens)
img = Image.alpha_composite(img, glass_rgba)

# --- the chest seen through the lens ---
d = ImageDraw.Draw(img)
cx, cy, cw, ch = lx, ly + 30, 116, 74          # half-width and body height
d.rounded_rectangle([cx - cw - 10, cy - 64, cx + cw + 10, cy + ch + 10], radius=20, fill=IRON)
d.rounded_rectangle([cx - cw, cy - 54, cx + cw, cy - 6], radius=14, fill=(196, 132, 58, 255))   # lid
d.rounded_rectangle([cx - cw, cy + 4, cx + cw, cy + ch], radius=10, fill=(176, 110, 44, 255))   # body
d.rectangle([cx - cw, cy - 6, cx + cw, cy + 4], fill=IRON)
for x in (cx - 70, cx + 70):
    d.rectangle([x - 12, cy - 54, x + 12, cy + ch], fill=IRON)
d.rounded_rectangle([cx - 22, cy - 22, cx + 22, cy + 28], radius=6, fill=IRON)
d.rounded_rectangle([cx - 13, cy - 13, cx + 13, cy + 19], radius=4, fill=(255, 214, 120, 255))

# the marker floating over the chest, like the in-game one
img = add_glow(img, lambda g: g.polygon([(cx, cy - 92), (cx - 34, cy - 150), (cx + 34, cy - 150)], fill=255),
               (255, 255, 255), blur=14, strength=2)
d = ImageDraw.Draw(img)
d.polygon([(cx, cy - 86), (cx - 40, cy - 154), (cx + 40, cy - 154)], fill=IRON)
d.polygon([(cx, cy - 104), (cx - 24, cy - 144), (cx + 24, cy - 144)], fill=(255, 244, 200, 255))

# glint on the glass
d.arc([lx - ir + 40, ly - ir + 40, lx + ir - 40, ly + ir - 40], 200, 250, fill=(235, 252, 255, 255), width=16)

rivets(img, [(lx - lr + rim // 2, ly), (lx + lr - rim // 2, ly), (lx, ly - lr + rim // 2), (lx, ly + lr - rim // 2)])
rivets(img, [(hx0 + (hx1 - hx0) * t, hy0 + (hy1 - hy0) * t) for t in (0.47, 0.74)])

save_icon(img, OUT)
