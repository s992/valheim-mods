# CraftFromStorage: in-game test list

*Written and kept current by AI agents (AGENTS.md). CI can't run the game client: this is what a human checks before
a release, riskiest first. Agents add to it when a change needs a new check.*

Before anything: only one copy of the mod loads (no `CraftFromStorage.dll` both in `plugins/CraftFromStorage/` and a Thunderstore
folder), and `BepInEx/LogOutput.log` has `CraftFromStorage <version> loaded` and no `CraftFromStorage is turned off` line.

## Multiplayer (two clients and Multi User Chest; this is where items can be lost)

1. Log has `MultiUserChest <version> found and healthy`. Without MUC, only your own chests are used: check the
   status line says so.
2. Player B owns a chest (opened it last) holding Wood; player A, out of Wood, crafts something needing Wood. A gets
   "fetching", then crafts; the chest has exactly that much less, and neither player gained or lost anything else.
3. Same, but B logs out during the fetch: A's craft stops with "didn't arrive", nothing is crafted for free, and A's
   auto-pickup still works afterwards.
4. Building with materials only in B's chest: the first click fetches, the next one places; the cost is taken once.
5. Two players crafting from the same chests at once: no item counts go negative or duplicate; `storage audit` in
   the console totals match the chests.

## Single player

6. Crafting with materials split between inventory and a nearby chest: inventory is used first, the rest from the
   chest; the requirement shows `have/need` with the combined count.
7. Building a piece with materials only in a chest: placed, cost taken from the chest.
8. Smelter with ore only in chests: E inserts one at a time from chests; holding E never overfills it; fuel too.
9. Change material (Alt Place + Use, or the configured key) cycles inputs; End blocklists one.
10. Home toggles the mod; with it off, nothing comes from chests.
11. Carts and ships: their chests are used or not according to the setting.

After a session: no `[Error` lines from `CraftFromStorage` in `BepInEx/LogOutput.log`, and no `Error in ...` or `kept failing`
lines (the mod's error guards write those).
