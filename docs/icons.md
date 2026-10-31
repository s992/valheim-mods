# Mod icons

Every mod has a 256×256 `mods/<Name>/icon.png` (Thunderstore requires exactly that size) in a shared style, drawn
by a script so it can be tweaked and regenerated:

- dark slate "old chart" background with faint contour lines, in a rounded square
- one bronze/iron object with rivets that says what the mod does
- one glowing focal element in a saturated colour

`tools/icon_kit.py` provides the background, shadow, glow, rivets and the downscale from the 1024 px working
canvas, so only the foreground is new work. Put the script at `mods/<Name>/scripts/make_icon.py`; it writes the
mod's `icon.png` by default:

```bash
python -m venv <temp>/pil && <temp>/pil/Scripts/pip install pillow     # once; any Python with Pillow works
<temp>/pil/Scripts/python mods/<Name>/scripts/make_icon.py
```

Examples: `mods/EverythingsANail/scripts/make_icon.py` and `mods/CraftFromStorage/scripts/make_icon.py` use the
kit; `mods/LootFilter/scripts/make_icon.py` predates it and carries its own helpers. MarkerCommand's and
StabilityView's icons have no script.

Look at the PNG before committing and iterate: first drafts usually need a stronger glow, a recentred
composition, or a symbol that reads as a letter replaced.
