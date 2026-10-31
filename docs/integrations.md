# Integrating with other mods

Mods the maintainer (s992) runs (r2modman profile, October 2026) that new mods have touched or may touch. Versions change,
so decompile the installed DLL before relying on these notes:
`python tools/decompile.py --plugin <DllNameSubstring> --project`.
Always integrate optionally (patterns.md, "Optional integration"): never make another mod a hard dependency
unless the user asks.

## Contents
- Server Devcommands (parameter hints in the console)
- Epic Loot (rarity, loot beams, item tiers, biome progression)
- Configuration Manager (shudnal)
- My Build Camera (free camera; its own resource pickup)
- MultiUserChest and chest ownership
- Others in the profile

## Server Devcommands (JereKuusela, GUID `server_devcommands`, 1.115)

Provides the rich console parameter hints ("Amount | a positive integer") and quote-aware argument parsing
for autocomplete. Vanilla only completes a command's first parameter.

- `ServerDevcommands.AutoComplete.Register(string command, Func<int, int, string[], List<string>> fetcher)`
  (also overloads without the `string[]`, and with named-parameter fetchers). Arguments: positional index,
  sub-index (for comma-separated values), and the typed parameters after the command (quoted strings arrive as one
  parameter, still with quotes).
- Return a list. Entries starting with `?` are shown as a hint; a list of plain strings is offered as completions.
  Don't mix the two in one list.
- Hint style used by SDC itself: `"Name | description"`, `<color=yellow>...</color>` to highlight.
- Register by reflection (find the method with the 3-argument Func first, fall back to the 2-argument one)
  so SDC isn't needed to build. SDC deliberately skips the chat window, so hints only show in the F5 console.

## Epic Loot (RandyKnapp, GUID `randyknapp.mods.epicloot`, 0.14.x)

Reference implementation of everything below (rarity, per-biome and outdated rules, beam hiding): LootFilter at
tag `LootFilter/checkpoint/with-epic-loot` (`EpicLootCompat.cs`, `Rules.cs`, `DropEffects.cs`). Epic Loot support was later
removed from LootFilter (Oct 2026) because s992 plans a separate Epic Loot filter mod; start that mod from this tag.

Official API: `EpicLoot.API` (static class, members marked `[PublicAPI]`):
- `bool IsMagicItem(ItemDrop.ItemData)`
- `bool TryGetRarity(ItemDrop.ItemData, ref int rarity)`: also true for magic crafting materials and runestones,
  not only gear. Check `IsMagicItem` to tell them apart.
- `ItemRarity`: 0 Magic (blue), 1 Rare, 2 Epic, 3 Legendary, 4 Mythic, 5 Ancient.
- Magic data lives in `ItemData.m_customData` and is initialized by Epic Loot's `ItemDrop.Awake` postfix, so it's
  ready on spawned drops.

Internals that worked (not official API; keep behind a separate availability flag):
- Loot beams: Epic Loot adds `EpicLoot.LootBeams.LootBeam` (a MonoBehaviour) to magic drops in an `ItemDrop.Start`
  postfix. In its `Update` it instantiates the beam (`private GameObject _beam`, includes particles, lights and an
  AudioSource) as a child of the drop. To hide: disable the component and `SetActive(false)` the `_beam` object;
  reverse both to restore. Exclude the beam's subtree (`transform.IsChildOf(beam.transform)`) when processing a
  drop's other particles.
- Item tiers ("which biome is this item from"): `EpicLoot.GatedItemType.GatedItemTypeHelper.AllItemsWithDetails`
  (static `Dictionary<string, GatedItemDetails>`, keyed by prefab name; `GatedItemDetails.RequiredBosses` is a
  `List<string>` of boss keys such as `defeated_gdking`, or `none`). Epic Loot clears and refills the same
  dictionary on config reload, so a cached reference stays valid. Items in several lists: take the latest boss.
- Biome progression: `EpicLoot.Biomes.BiomeDataManager` static methods `int GetOrder(Heightmap.Biome)`
  (index in progression; -1 for None, int.MaxValue for unknown), `Heightmap.Biome GetFirstBiomeForBossKey(string)`
  (0 for "none" → treat as Meadows), `bool HasAllBossKeys(Heightmap.Biome)` (world global keys).
  Default order: Meadows, BlackForest, Ocean, Swamp, Mountain, Plains, Mistlands, AshLands, DeepNorth.
  The Ocean counts as a step, which matters for "N biomes behind" margins.
- "Progression biome" = first biome in that order where `HasAllBossKeys` is false (last biome once all are done).
  This is how Epic Loot itself gates loot, and players found it more useful than the biome they stand in.
- Item data ships as embedded JSON in EpicLoot.dll (`EpicLoot.config.iteminfo.json`, `...biomedata.json`); the
  decompiled project includes them, handy for offline sanity checks of tier logic.

## Configuration Manager (shudnal, `shudnal-ConfigurationManager`)

- Opens with F1 in game. Shows `[Flags]` enums as checkboxes (verified in its source: `SettingFieldDrawer` checks
  `FlagsAttribute`), `Color` with a picker, `KeyboardShortcut` with key capture.
- Players can edit any setting live; subscribe to `SettingChanged` so changes take effect without a restart.

## My Build Camera (EndoCo, GUID `EndosBuildCamera`, 1.0.12)

- Moves the camera far from the player: measure "nearby" from the camera, not the player.
- Has its own resource pickup around the free camera: `MyValheimBuildCamera.BuildCameraBase.ExecuteCameraAutoPickup`
  (private) loops `FindObjectsByType<ItemDrop>`, filters by type/name/range and `Inventory.CanAddItem(ItemData, int)`,
  then **moves the drop to the player** and calls `Humanoid.Pickup`. It ignores vanilla's auto-pickup check, so
  pickup filters must hook it separately. Blocking `Pickup` would leave items piled at the player's feet; instead
  LootFilter transpiles that one `CanAddItem` callvirt into a static helper with the same stack shape
  (`Patches.cs`, `BuildCameraPickupPatch`), applied by hand only when the GUID is loaded.

## MultiUserChest and chest ownership (MSchmoecker, GUID `com.maxsch.valheim.MultiUserChest`, 0.6.2)

Full write-up with code references: `mods/CraftFromStorage/docs/ownership.md`. The essentials:
- Only a chest's ZDO owner may change it. A non-owner's edit to `container.GetInventory()` is never saved and is wiped on
  the next reload; writing the ZDO as a non-owner races the owner (higher `DataRevision` wins, ties ignored, no merge).
- MUC turns non-owner `Inventory` edits into requests to the owner (global routed RPCs). The safe call shape for taking
  items is QSS restock's `playerInventory.MoveItemToThis(chestInventory, item, amount, x, y)`; target an empty slot or a
  same-type partial stack, or MUC treats it as a swap. Requests name a chest slot, not an item.
- MUC has no timeout: a pending request blocks its target slot and all auto-pickup until the reply. Release a stuck slot via
  `MultiUserChest.InventoryBlock.Get(inv).ReleaseSlot(slot)` (reflection). Stale owners answer "failed"; only a
  disconnected owner never replies.
- One failed MUC patch aborts its whole `PatchAll` (issue #19). Check `Harmony.GetPatchInfo` on the methods you rely on.
- QSS changes behaviour when MUC's GUID is loaded (edits non-owned chests directly instead of grabbing ownership).
- Ownership is only guaranteed within 64 m of a player; chests are loaded further out (128 m default) but may have owner 0.
- Smelter queue/fuel live on the smelter owner; a remote client's count lags one round trip, and the owner's RPC doesn't
  re-check the limit, so hold-to-repeat can overfill. Count your own unconfirmed inserts.

## Others in the profile

Jotunn (2.30), Epic Loot, Server Devcommands, World Edit Commands (older Steam install), Infinity Hammer,
PlanBuild, My Build Camera (above), Compass, Configuration Manager, QuickStackStore, CraftFromChests, Adventure Backpacks, MultiUserChest, and others.
Check the profile's `BepInEx/LogOutput.log` "Loading [...]" lines for the current list and versions.
Jotunn is installed but none of s992's mods depend on it; plain BepInEx + Harmony has been enough.
