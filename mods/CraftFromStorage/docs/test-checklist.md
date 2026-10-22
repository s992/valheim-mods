# In-game test checklist (v0.1.0)

Use the **"testing storage"** r2modman profile on a **copy** of your world. Before and after each session, stand in
the base and run `storage audit` in the F5 console. The full item list goes to `BepInEx/StorageOps.log`, next to a
line for every item the mod took or fetched.

Riskiest first. "Two players" means two clients on a server where both run the profile.

## 0. Startup (solo is fine)
1. **`BepInEx/LogOutput.log` after loading in.**
   - Expect `MultiUserChest 0.6.2 found and healthy` and `Epic Loot's enchanting table now uses nearby storage`.
   - *Failure:* `...patches are missing...` means non-owned chests won't be used (see `MucHealth.cs`).
   - No line may contain `[Storage] ... expected 1 call(s)`. That line means a game update moved something,
     and counting won't work at that spot.
2. **Status line.** Open a workbench and the status line appears at the top:
   `Storage [Home]: N chests within 30 m`.

## 1. Item safety with two players (riskiest)
1. **Craft while B holds the chest open.**
   - A crafts something whose material is only in a chest that B has open.
   - Expect a short pause, the craft finishing, and the chest's count dropping on both screens.
   - *Failure:* the craft finishes but the chest still has the items (free craft), or items vanish without a craft.
2. **Both players spend the last stack.**
   - A and B craft at the same moment from one chest holding just enough for one craft.
   - Expect one craft to succeed. The other stops with "… didn't arrive from storage, crafting stopped", or never
     starts because of "not enough … in reach".
   - Check the audit: nothing lost.
3. **Disconnect mid-fetch.**
   - B has a chest open. A starts a craft that needs its contents, and B quits right then.
   - Expect A's craft to stop, and **A's auto-pickup still working** afterwards (pick something up).
   - *Failure:* auto-pickup is dead until relog.
4. **Borrowing.** A opens a workbench next to chests B last opened (B has them closed).
   - Within about a second those chests show as A's: the log has `borrowed ...` lines in StorageOps.log.
   - A craft then finishes without any pause.
5. **Never borrowed while open.** B keeps a chest open the whole time.
   - It must never switch owner, and B can keep using it normally.
   - The status line counts it as `1 in use`.

## 2. Crafting and building (solo is fine)
1. **Have/need display and priority.** Put 100 wood in a chest and 5 in your inventory.
   - A recipe needing 10 shows `105/10`.
   - Crafting it uses your 5 first, then 5 from the chest.
2. **×5 multi-craft and upgrades.** Both use storage and show the multiplied counts.
3. **Toggle.** With storage off (`Home`), counts drop back to inventory-only and nothing comes from chests.
4. **Building.**
   - A piece whose wood is only in chests can be placed, and the chest loses the wood.
   - In multiplayer, with the wood only in a chest someone has open: the first click says
     "fetching materials, place again", and the second click places it.
5. **Full inventory.** In multiplayer, with the material only in a chest someone has open, a craft says
   "need N free inventory slots…" and doesn't start.
6. **`nocost` (devcommands).** Building takes nothing from chests.
7. **Out of range.** A chest beyond the range isn't used. At ranges over 64 m in multiplayer, the status line may
   show "skipped (no owner)". That's expected (decision 0).

## 3. Smelting (solo is fine)
1. **Ore from a chest.** Put copper and tin ore in a chest and none in your inventory.
   - Hovering a smelter's ore input shows `Next: Copper ore — 0 in inventory, N in storage`.
   - E inserts one, and holding E keeps inserting.
2. **Shift+E.** Switches to tin ore, and the furnace/smelter remembers it.
3. **Kiln.** With fine wood and wood in chests, the kiln only takes wood.
   - Shift+E onto fine wood shows "blocklisted". `End` allows it, and `End` again blocks it.
4. **Fuel.** Coal for the smelter comes from a chest when you carry none.
5. **Windmill and spinning wheel.** Barley and flax from chests work. These have no fuel input.

## 4. Epic Loot
1. **Enchanting from chests.** Put dust or essence in chests and none in your inventory.
   - The enchant cost shows as available, and enchanting takes it from a chest.
2. **Sacrifice tab.**
   - An "Include chest trophies" checkbox appears above "Select All".
   - Ticked: trophies from chests are listed and can be sacrificed.
   - Unticked: they disappear.
3. **Temper screen.** Counts include materials in chests you own.
4. **Merchant.** Coins in chests are **not** spent.
5. **Magic gear.** An enchanted weapon in a chest is never consumed as a crafting material.

## 5. Quick Stack Store Sort Trash Restock
Quick stack, restock, sort and store/take all on chests another player has open all still work.
MUC handles these; this mod doesn't touch QSS.

## Logs worth sending back if something's off
- `BepInEx/LogOutput.log` (lines with `Storage`)
- `BepInEx/StorageOps.log`
