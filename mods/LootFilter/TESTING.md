# LootFilter: in-game test list

*Written and kept current by AI agents (AGENTS.md). CI can't run the game client: this is what a human checks before
a release, riskiest first. Agents add to it when a change needs a new check.*

Before anything: only one copy of the mod loads (no `LootFilter.dll` both in `plugins/LootFilter/` and a Thunderstore
folder), and `BepInEx/LogOutput.log` has `LootFilter <version> loaded` and no `LootFilter is turned off` line.

1. Hover a ground item, Delete: it's filtered (message), its sparkle changes, and auto-pickup skips it while you
   walk over it. Delete again: back to normal.
2. Shift+Delete on an item filters its whole group; the tooltip says why an item is filtered.
3. Insert pauses: filtered items are picked up again and look normal; Insert resumes.
4. Hold Left Alt: filtered items look normal while held.
5. Items you dropped yourself are filtered the same way.
6. With My Build Camera: its camera pickup skips filtered items; without it installed, no warning about it.
7. Change the sparkle colour or mode in Configuration Manager: existing drops update without a restart.

After a session: no `[Error` lines from `LootFilter` in `BepInEx/LogOutput.log`, and no `Error in ...` or `kept failing`
lines (the mod's error guards write those).
