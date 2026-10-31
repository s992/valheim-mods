"""Shared look for the repo's mod icons (see docs/icons.md).

The style: a dark slate "old chart" background with faint contour lines, a rounded square,
a bronze/iron object with rivets, and one glowing focal element in a saturated colour.
Draw at 1024px (S) and let save_icon() downscale to the 256x256 Thunderstore requires.

Needs Pillow (`pip install pillow`, in a throwaway venv is fine). Typical script:

    # mods/<Name>/scripts/make_icon.py
    import sys; from pathlib import Path
    sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "tools"))
    from icon_kit import *
    img = background()
    img = add_shadow(img, lambda d: d.ellipse([300, 300, 724, 724], fill=255))
    d = ImageDraw.Draw(img)
    d.ellipse([300, 300, 724, 724], fill=IRON); d.ellipse([314, 314, 710, 710], fill=BRONZE_LIGHT + (255,))
    img = add_glow(img, lambda d: d.ellipse([440, 440, 584, 584], fill=255), (255, 140, 40))
    rivets(img, [(512, 330), (512, 694)])
    save_icon(img, str(Path(__file__).resolve().parents[1] / "icon.png"))

Always look at the result (Read the PNG) before committing: the first draft usually needs a tweak
(e.g. glow too faint, composition off-centre, a symbol that reads as a letter).
"""
import math
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

S = 1024
IRON = (34, 22, 12, 255)
BRONZE_LIGHT = (246, 196, 102)
BRONZE_DARK = (130, 74, 24)
WOOD = (120, 80, 44)
# Support/stability-style status colours, matching the in-game overlays
BLUE, GREEN, YELLOW, ORANGE, RED = (150, 205, 255), (90, 255, 100), (255, 225, 60), (255, 150, 50), (255, 70, 55)
PURPLE = (190, 90, 255)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def radial(inner, outer, center, radius, size=S):
    img = Image.new("RGB", (size, size))
    px = img.load()
    cx, cy = center
    for y in range(size):
        for x in range(size):
            t = min(1.0, math.hypot(x - cx, y - cy) / radius)
            px[x, y] = lerp(inner, outer, t ** 1.4)
    return img


def background(seed=7):
    """Dark slate radial gradient with faint chart contour lines (RGBA, S x S)."""
    rng = random.Random(seed)
    bg = radial((44, 54, 62), (12, 15, 19), (S * 0.42, S * 0.38), S * 0.85).convert("RGBA")
    overlay = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    for k in range(9):
        base = 120 + k * 105
        phase = rng.uniform(0, math.tau)
        pts = [(x, base + 38 * math.sin(x / 140 + phase) + 18 * math.sin(x / 57 + phase * 2))
               for x in range(-20, S + 40, 16)]
        d.line(pts, fill=(120, 140, 150, 40), width=5, joint="curve")
    # Draw translucent shapes on a separate layer and composite: drawing alpha straight onto
    # an RGBA image replaces pixels instead of blending (lines come out nearly white)
    return Image.alpha_composite(bg, overlay)


def mask_from(draw_fn):
    """A greyscale mask from a function that draws white shapes: draw_fn(ImageDraw)."""
    m = Image.new("L", (S, S), 0)
    draw_fn(ImageDraw.Draw(m))
    return m


def add_shadow(img, draw_fn, offset=(16, 24), blur=24, opacity=0.6):
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    shadow.putalpha(mask_from(draw_fn).filter(ImageFilter.GaussianBlur(blur)).point(lambda v: int(v * opacity)))
    return Image.alpha_composite(img, ImageChops.offset(shadow, *offset))


def add_glow(img, draw_fn, color, blur=40, strength=2):
    """Coloured halo from white shapes drawn by draw_fn. strength stacks the layer to make it punchier;
    a single blurred layer is usually too faint to read at 256px."""
    layer = Image.new("RGBA", (S, S), color + (0,))
    layer.putalpha(mask_from(draw_fn).filter(ImageFilter.GaussianBlur(blur)))
    out = img
    for _ in range(strength):
        out = Image.alpha_composite(out, layer)
    return out


def linear_fill(mask, top_left_color, bottom_right_color, angle_deg=60):
    """An RGBA layer filled with a diagonal gradient, clipped to mask (e.g. a bronze body lit from the upper left)."""
    img = Image.new("RGB", (S, S))
    px = img.load()
    ang = math.radians(angle_deg)
    dx, dy = math.cos(ang), math.sin(ang)
    for y in range(S):
        for x in range(S):
            t = ((x - S / 2) * dx + (y - S / 2) * dy) / (S * 0.75) + 0.5
            px[x, y] = lerp(top_left_color, bottom_right_color, max(0.0, min(1.0, t)))
    out = img.convert("RGBA")
    out.putalpha(mask)
    return out


def rivets(img, points, radius=13):
    d = ImageDraw.Draw(img)
    for x, y in points:
        d.ellipse([x - radius, y - radius, x + radius, y + radius], fill=(70, 42, 16, 255))
        d.ellipse([x - radius * 0.7, y - radius * 0.8, x + radius * 0.4, y + radius * 0.15], fill=(255, 226, 160, 255))


def save_icon(img, path, corner_radius=150):
    """Rounded-square mask, downscale to 256x256 (Thunderstore's required size) and save as PNG."""
    mask = mask_from(lambda d: d.rounded_rectangle([0, 0, S - 1, S - 1], radius=corner_radius, fill=255))
    final = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    final.paste(img, (0, 0), mask)
    final = final.resize((256, 256), Image.LANCZOS)
    final.save(path)
    print("saved", path, final.size)
