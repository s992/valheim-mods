# CraftFromStorage

## ai usage disclaimer

with the exception of this readme, everything in this repo is ai-generated.

## description

this mod allows you to craft, build, and smelt using materials in nearby chests.

## usage

all keys are configurable. defaults are presented below along with their config key.

### crafting and building

when building in the build menu and crafting at workbenches (workbench, forge, etc.), both your inventory and materials in nearby chests are counted and considered available for the craft. performing the craft will take materials from your inventory first and then consume the remainder from nearby chests.

### smelting

smelter-style stations (e.g. kiln, furnace, blast furnace, windmill, eitr refinery, etc.) are supported and behave similarly to crafting and building. when hovering over the station input, text is displayed indicating what will be inserted next, how many are in your inventory, and how many are available in storage. for station inputs that accept multiple types of material such as a kiln or a furnace, you may press `Shift+E (Change material key)` to rotate between the available options.

smelter-style stations also support a blocklist. fine wood, core wood, and yggdrasil wood wood are blocklisted by default because there's better trees to get your coal from. an item may be added to the blocklist by following these steps:

1. on the station type you want to configure, aim at the relevant input
2. if applicable, press `Shift+E (Change material key)` to cycle through materials until you locate the one you want to blocklist
3. press `End (Blocklist key)`

### miscellaneous

press `Home (Toggle key)` to toggle the mod.

there is a status line that appears at the top of the screen when crafting, building, or hovering a station's input. the status line displays how many chests are being looked at and the state of the chests.

## multiplayer

in multiplayer, [MultiUserChest](https://thunderstore.io/c/valheim/p/MSchmoecker/MultiUserChest/) is required on all clients and the server.

## settings

settings are configurable via editing the settings file directly (`BepInEx/config/s992.CraftFromStorage.cfg`) or via a [configuration manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/).

these are the default settings:

```
## Settings file was created by plugin CraftFromStorage v0.1.0
## Plugin GUID: s992.CraftFromStorage

[1. General]

## Crafting, building and smelting use materials from nearby chests. The toggle key flips this.
# Setting type: Boolean
# Default value: true
Use nearby storage = true

## Turns "use nearby storage" on and off.
# Setting type: KeyboardShortcut
# Default value: Home
Toggle key = Home

## Chests within this many metres of you are used. It's capped at the distance the game guarantees chests are loaded (128 m on the default simulation distance); the status line says when the cap applies.
# Setting type: Int32
# Default value: 30
# Acceptable value range: From 5 to 250
Range = 30

## Shows a line at the top of the screen while crafting, building or hovering a smelter: how many chests are used, how many were skipped and why.
# Setting type: Boolean
# Default value: true
Show status line = true

[2. Features]

## Crafting stations (and upgrades) use nearby storage.
# Setting type: Boolean
# Default value: true
Crafting = true

## Building pieces use nearby storage.
# Setting type: Boolean
# Default value: true
Building = true

## Smelters, furnaces, kilns, windmills, spinning wheels and eitr refineries take materials and fuel from nearby storage.
# Setting type: Boolean
# Default value: true
Smelting = true

[3. Chests]

## Carts and ships nearby count as storage.
# Setting type: Boolean
# Default value: false
Carts and ships = false

## Container prefab names that never count as storage, comma separated (e.g. piece_chest_private).
# Setting type: String
# Default value: 
Excluded containers = 

[4. Smelting]

## Item prefab names that are never put into a smelter automatically, from your inventory or storage, comma separated. Using an item from your hotbar on a smelter still works. The blocklist key toggles the material shown when hovering a smelter.
# Setting type: String
# Default value: FineWood,RoundLog,YggdrasilWood
Blocklist = FineWood,RoundLog,YggdrasilWood

## While hovering a smelter's input, adds or removes its selected material from the blocklist.
# Setting type: KeyboardShortcut
# Default value: End
Blocklist key = End

## While hovering a smelter's input, switches which material goes in next. Empty: the game's Alt Place + Use (Shift+E by default; also works on a gamepad). Your Use key is refused (it would also insert).
# Setting type: KeyboardShortcut
# Default value: 
Change material key = 

## Managed by the mod: which material each kind of smelter takes (changed in game with the change material key).
# Setting type: String
# Default value: 
Selected materials = 

[5. Diagnostics]

## Ask other players for ownership of nearby chests before they're needed, so crafting and building can take from them instantly. Off: those chests are only reached by fetching items into your inventory.
# Setting type: Boolean
# Default value: true
Borrow chests = true

## Writes every item the mod takes from or fetches out of a chest to BepInEx/CraftFromStorage.log. Handy when something looks wrong; the file is never trimmed, so leave it off otherwise. `storage audit` writes there either way.
# Setting type: Boolean
# Default value: false
Log actions = false
```
