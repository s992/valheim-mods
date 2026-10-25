# EverythingsANail

## ai usage disclaimer

with the exception of this readme, everything in this repo is ai-generated.

## description

this mod allows your hammer to impersonate any other tool you've had in your inventory. you do not need the tool in your inventory to impersonate it. 

## usage

with your hammer equipped, open the radial menu (default key is `G`) and select the tool you want to use. tools with multiple tiers (axes and pickaxes) offer the best you have (or the one you're currently using) with the other options available behind a right click submenu.

your hammer's quality, durability, and - if applicable - EpicLoot affixes/shards apply to the item it is impersonating.

interacting with a crafting station turns your impersonated tool back into a hammer because things get real weird if we don't do that.

## troubleshooting

use the `nail_tools` console command to see a list of tools, their names, prefab names (these are used for the `OnlyAllow` and `Exclude` config options), and availability.

## multiplayer

this mod is not required on the server or on other clients. if the mod is installed on the server, its settings will be enforced on all clients with the mod installed.

## mod compatibility

mods that create a new item in the following categories should be supported automatically:

- `Building`
- `Butchering`
- `Harvesting`
- `Fishing`
- `Logging` (one-handed axes)
- `Mining` (pickaxes)

i've successfully used the mod with the following mods that interact with tools in some way:

- [EpicLoot](https://thunderstore.io/c/valheim/p/RandyKnapp/EpicLoot/)
- [PlanBuild](https://thunderstore.io/c/valheim/p/MathiasDecrock/PlanBuild/)
- [PlantEverything](https://thunderstore.io/c/valheim/p/Advize/PlantEverything/)
- [PlantEasily](https://thunderstore.io/c/valheim/p/Advize/PlantEasily/)
- [OCDheim](https://thunderstore.io/c/valheim/p/javadevils/OCDheim/)

## settings

settings are configurable via editing the settings file directly (`BepInEx/config/s992.EverythingsANail.cfg`) or via a [configuration manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/).

these are the default settings:

```
[Tools]

## Kinds of tools the hammer can become. Building: anything with a build menu (hoe, cultivator, tools from other mods). Butchering: butcher knife. Harvesting: scythe. Fishing: fishing rod. Logging: one-handed axes. Mining: pickaxes. The hammer itself is always available. In multiplayer the server's setting is used when the server runs this mod.
# Setting type: ToolCategories
# Default value: Building, Butchering, Harvesting, Fishing
# Acceptable values: None, Building, Butchering, Harvesting, Fishing, Logging, Mining
# Multiple values can be set at the same time by separating them with , (e.g. Debug, Warning)
Categories = Building, Butchering, Harvesting, Fishing

## Comma-separated internal item names (e.g. AxeJotunBane, FishingRod). When set, only these tools are available and Categories is ignored. The nail_tools console command lists the names. In multiplayer the server's setting is used when the server runs this mod.
# Setting type: String
# Default value: 
OnlyAllow = 

## Comma-separated internal item names that are never available, even if their category is enabled. In multiplayer the server's setting is used when the server runs this mod.
# Setting type: String
# Default value: 
Exclude = 
```
