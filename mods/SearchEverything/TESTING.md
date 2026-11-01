# SearchEverything: in-game test list

*Written and kept current by AI agents (AGENTS.md). CI can't run the game client: this is what a human checks before
a release, riskiest first. Agents add to it when a change needs a new check.*

Before anything: only one copy of the mod loads (no `SearchEverything.dll` both in `plugins/SearchEverything/` and a
Thunderstore folder), and `BepInEx/LogOutput.log` has `SearchEverything <version> loaded` and no `SearchEverything is
turned off` line.

1. Typing in each search box (recipes, chest, nearby window) does nothing else: WASD, E, Tab, digits, M, Enter (chat)
   and the camera don't react. Escape clears focus or closes the window without closing the inventory or opening the
   pause menu, and the next Escape behaves normally again.
2. Ctrl+K with debug mode on opens the window and doesn't kill nearby enemies; plain K still does.
3. Ctrl+K and F1 (Configuration Manager) both work within a few seconds of spawning, after a fresh game start.
4. Nearby window: the cursor is free, the mouse wheel scrolls results without zooming the camera, and the camera is
   back to normal after closing it. Arrows, Enter and clicks pick a result.
5. Picking a result marks its chests through walls with a distance; each marker goes when its chest is opened, after
   the marker duration, or on the dismiss key (Ctrl+Shift+K). The window's status line names any range limit.
6. Recipe search at a workbench and the cauldron: the box sits right of the tabs without covering them; name,
   ingredient (`wood`) and type (`helmet`, `food`) searches filter the list; switching tabs keeps the filter.
7. Chest search: the box replaces the chest name; non-matching items fade in the chest and the inventory; take-all,
   moving items and closing the chest all work, and the box is empty next time.
8. The inventory button (Search chests) sits above the inventory without covering other mods' buttons.
9. Carts and ships are searched only with Carts and ships on; with the mod's features turned off in Configuration
   Manager, their boxes and button go away.

After a session: no `[Error` lines from `SearchEverything` in `BepInEx/LogOutput.log`, and no `Error in ...` or
`kept failing` lines (the mod's error guards write those).
