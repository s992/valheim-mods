# LootFilter

## ai usage disclaimer

with the exception of this readme, everything in this repo is ai-generated.

## description

this mod adds a configurable loot filter. when an item is filtered:

1. it will not be picked up automatically
2. its sparkles will be hidden (or optionally recolored)

## usage

all keys are configurable. defaults are presented below along with their config key. "hovering" below refers to both the act of hovering over it in your inventory/storage and the act of aiming at it on the ground such that the tooltip appears.

to add/remove an item from the filtered list, press `Del (ToggleItem)` while hovering over it.

to add/remove an item group (e.g., materials, trophies, tools, etc.), press `Shift+Del (ToggleGroup)` while hovering over an item from the desired group.

to toggle filtering, press `Ins (TogglePause)`. this is intended to be a temporary toggle and is not persisted between game launches. if you want to disable the mod entirely, either remove all items and groups from the filter or uninstall it.

to temporarily reveal filtered items, hold `Alt (Reveal)`. this temporarily removes any sparkle configuration and makes the item's sparkles display as they normally would. it does not disable filtering.

## not supported

group exclusions (e.g., "i don't want anything from the materials group except for stone") are not supported. they may be supported next time i feel like slopping on this mod.

## mod compatibility

[MyBuildCamera](https://thunderstore.io/c/valheim/p/EndoCo/MyBuildCamera/)'s camera auto-pickup should respect the filter.

## non-goals

advanced filtering for mods like [EpicLoot](https://thunderstore.io/c/valheim/p/RandyKnapp/EpicLoot/) will never be supported. custom items should be filterable, but this mod will never add support for filtering based on rarity, stats, or things of that nature.

## settings

settings are configurable via editing the settings file directly (`BepInEx/config/s992.LootFilter.cfg`) or via a [configuration manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/).

these are the default settings:

```
## Settings file was created by plugin LootFilter v0.1.0
## Plugin GUID: s992.LootFilter

[Behaviour]

## Don't auto-pickup filtered items. They can still be picked up manually.
# Setting type: Boolean
# Default value: true
BlockAutoPickup = true

## Add a line to item tooltips saying whether (and why) the item is filtered.
# Setting type: Boolean
# Default value: true
ShowInTooltip = true

[Effects]

## What to do with the sparkles and glow of filtered drops: hide them, recolor them with SparkleColor, or leave them alone.
# Setting type: SparkleMode
# Default value: Hidden
# Acceptable values: Hidden, Recolored, Unchanged
FilteredSparkles = Hidden

## Color of filtered drops' sparkles when FilteredSparkles is Recolored. The alpha (A) value sets how visible they are.
# Setting type: Color
# Default value: 8C8C8C59
SparkleColor = 8C8C8C59

[Keys]

## Filter/unfilter the hovered item (inventory, chest or on the ground).
# Setting type: KeyboardShortcut
# Default value: Delete
ToggleItem = Delete

## Filter/unfilter the hovered item's group (Weapons, Trophies, ...).
# Setting type: KeyboardShortcut
# Default value: Delete + LeftShift
ToggleGroup = Delete + LeftShift

## Hold to show the original effects of filtered items.
# Setting type: KeyboardShortcut
# Default value: LeftAlt
Reveal = LeftAlt

## Pause/resume the filter: while paused, filtered items are auto-picked up and keep their effects. The filter is always active again after restarting the game.
# Setting type: KeyboardShortcut
# Default value: Insert
TogglePause = Insert

[Rules]

## Comma-separated item names to filter (internal prefab names, e.g. Resin, Stone, RoundLog). Easiest to edit with the ToggleItem key while hovering an item.
# Setting type: String
# Default value: 
FilteredItems = 

## Item groups to filter.
# Setting type: ItemGroups
# Default value: None
# Acceptable values: None, Materials, FoodAndPotions, Weapons, Shields, Armor, Ammo, Trophies, Fish, ToolsAndTorches, UtilityAndTrinkets, Misc
# Multiple values can be set at the same time by separating them with , (e.g. Debug, Warning)
FilteredGroups = None
```
