# Changelog

*This changelog is written by Claude, an AI assistant, from the project's commit history.*

## 1.0.1

- If a game update breaks something the mod relies on, the mod now turns itself off completely and says so in game and in the log, instead of running half-patched.
- An error in auto-pickup filtering, tooltips or the hotkeys is logged once and shown as a single message instead of repeating every frame.
- If the optional My Build Camera support fails to load, the rest of the filter keeps working (with a warning in the log). Previously the hotkeys and drop effects could be left off.
- If the drop effects switch themselves off after repeated errors, filtered drops get their sparkles back.

## 1.0.0

- Initial release: mark items or whole item groups as junk with a hotkey while hovering them (inventory, chest or ground).
- Filtered items aren't auto-picked up, including by My Build Camera's free camera; they can still be picked up by hand.
- Filtered drops' sparkles and glow can be hidden or recoloured, and a hotkey reveals them while held.
- A pause key turns the filter off until the next restart.
