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

smelter-style stations (e.g. kiln, furnace, blast furnace, windmill, eitr refinery, etc.) are supported and behave similarly to crafting and building. when hovering over the station input, text is displayed indicating what will be inserted next, how many are in your inventory, and how many are available in storage. for station inputs that accept multiple types of material such as a kiln or a furnace, you may press `Shift+E ()` to rotate between the available options.

smelter-style stations also support a blocklist. fine wood, core wood, and yggdrasil wood wood are blocklisted by default because there's better trees to get your coal from. an item may be added to the blocklist by following these steps:

1. on the station type you want to configure, aim at the relevant input
2. if applicable, press `Shift+E ()` to cycle through materials until you locate the one you want to blocklist
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
```
