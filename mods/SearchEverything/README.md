# SearchEverything

## ai usage disclaimer

with the exception of this readme, everything in this repo is ai-generated.

## description

this mod adds search functionality to the crafting and storage UIs as well as a "global" (read: nearby chests) search UI to help find which chest you stuffed something into.

## usage

### crafting

a search input is available in the top right of the crafting UI. typing into it will filter recipes by name and ingredient.

![crafting screenshot](https://raw.githubusercontent.com/s992/valheim-mods/main/mods/SearchEverything/screenshots/crafting.png)

### storage

a search input is available in the top of the storage UI. typing into it will dim icons for items that don't match the filter.

![crafting screenshot](https://raw.githubusercontent.com/s992/valheim-mods/main/mods/SearchEverything/screenshots/storage.png)

### global search

pressing `Ctrl+K (Open key)` will present a UI displaying the contents of chests near you:

![global search screenshot](https://raw.githubusercontent.com/s992/valheim-mods/main/mods/SearchEverything/screenshots/global.png)

selecting an item will highlight the containing chest by placing a temporary icon above the chest. the icon is cleared when you interact with the chest, clear the icon manually (`Ctrl+Shift+K (Dismiss key)`), or when the icon expires (60 seconds default, configurable in `Marker duration`).

## settings

settings are configurable via editing the settings file directly (`BepInEx/config/s992.SearchEverything.cfg`) or via a [configuration manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/).

these are the default settings:

```
[1. Recipes]

## A search box next to the Craft and Upgrade tabs filters the recipe list. Matches the item's name, its ingredients and its type (bow, helmet, food...).
# Setting type: Boolean
# Default value: true
Recipe search = true

[2. Chests]

## A search box at the top of an open chest. Items that don't match fade out, in the chest and in your inventory.
# Setting type: Boolean
# Default value: true
Chest search = true

## How visible items that don't match the chest search stay (0 = hidden, 1 = not faded).
# Setting type: Single
# Default value: 0.25
# Acceptable value range: From 0 to 1
Faded opacity = 0.25

[3. Nearby chests]

## A search window for everything in nearby chests. Picking a result marks the chests that hold it.
# Setting type: Boolean
# Default value: true
Nearby search = true

## Opens the nearby search, in the world or in the inventory. Escape closes it. The game doesn't act on this key while it opens the search (e.g. debug mode's K, which kills nearby enemies).
# Setting type: KeyboardShortcut
# Default value: K + LeftControl
Open key = K + LeftControl

## Shows a button above your inventory that opens the nearby search.
# Setting type: Boolean
# Default value: true
Inventory button = true

## Chests within this many metres of the camera are searched. Chests further than the game keeps loaded (128 m from you on the default simulation distance) can't be seen; the window's status line says when that applies.
# Setting type: Int32
# Default value: 64
# Acceptable value range: From 5 to 250
Range = 64

## Carts and ships count as chests.
# Setting type: Boolean
# Default value: true
Carts and ships = true

[4. Markers]

## Seconds a chest stays marked. A marker also goes away when you open its chest. 0: until opened or dismissed.
# Setting type: Int32
# Default value: 60
# Acceptable value range: From 0 to 600
Marker duration = 60

## Removes every marker.
# Setting type: KeyboardShortcut
# Default value: K + LeftShift + LeftControl
Dismiss key = K + LeftShift + LeftControl

[5. Diagnostics]

## Writes where the search boxes were placed, and the panels around them, to the BepInEx log. Useful when a box overlaps something.
# Setting type: Boolean
# Default value: false
Log UI layout = false
```
