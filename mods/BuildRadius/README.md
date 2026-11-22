# BuildRadius

## ai usage disclaimer

with the exception of this readme, everything in this repo is ai-generated.

## description

this mod allows you to increase (or decrease, if you're a masochist) the build radius of crafting stations.

## usage

set the `Multiplier` config option to increase the build radius of each crafting station by that multiplier. for example, a default workbench has a build radius of 20 and would be increased to 40 under the default settings.

you may also specify station-specific radius overrides in the `Station radii` config option.

## multiplayer

this mod is required on the server. it is not required on clients, but clients will not benefit from the mod unless they have it installed. the mod's settings are enforced by the server.

## settings

settings are configurable via editing the settings file directly (`BepInEx/config/s992.BuildRadius.cfg`) or via a [configuration manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/).

these are the default settings:

```
[1. Server]

## Scales every crafting station's build radius, and the extra radius each extension adds. 1 is the game's own radius. Modded stations are included. Set by the server in multiplayer.
# Setting type: Single
# Default value: 2
# Acceptable value range: From 0.5 to 10
Multiplier = 2

## Exact base radius in metres for named stations, as name:metres separated by commas, e.g. piece_workbench:40, forge:30. Names are prefab names (piece_workbench, forge, piece_stonecutter, piece_artisanstation, blackforge, piece_magetable) or station names without the $. Extensions still add their bonus times the multiplier. Stations not listed use the multiplier. Set by the server in multiplayer.
# Setting type: String
# Default value: 
Station radii = 
```
