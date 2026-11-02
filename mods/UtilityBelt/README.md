# UtilityBelt

## ai usage disclaimer

with the exception of this readme, everything in this repo is ai-generated.

## description

this is a QoL mod that adds a new item named "Utility Belt." it's intended to replace megingjord (though you can still buy megingjord if you want) and stay relevant through your entire playthrough. the belt has the following upgrade tiers:

1. +150 carry weight
2. swamp key
3. wishbone
4. frost resistance
5. wisplight
6. feather fall
7. +150 carry weight (for a total of +300)

## usage

### acquiring

purchase the belt from haldor for 950 gold coins.

### upgrading

the belt can be upgraded at the workbench.

default upgrade costs are listed below by tier:

0. N/A (this is what you get from haldor)
1. 1 swamp key
2. 1 wishbone
3. 1 wolf fur cape
4. 1 wisplight
5. 1 feather cape
6. 1500 gold coins

you can also set a per-tier gold price for each upgrade if desired.

### toggling functionality

you may toggle wishbone, wisplight, and/or feather fall functionality for yourself via the config. these toggles support keybinds; see the "settings" section for more details.

### multiplayer

this mod must be installed on the server and clients. the server may disable any of the upgrade tiers by setting the appropriate config setting. for example: if you don't want to enable feather fall, you can set `Tier 5 feather fall = false` to disable for all players.

## settings

settings are configurable via editing the settings file directly (`BepInEx/config/s992.UtilityBelt.cfg`) or via a [configuration manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/).

these are the default settings:

```
[1. Server]

## Gold coins Haldor asks for a new belt. He sells it once he sells Megingjord. Set by the server in multiplayer.
# Setting type: Int32
# Default value: 950
# Acceptable value range: From 1 to 100000
Haldor price = 950

## Whether the belt has this upgrade (opens sunken crypts). When off, belts skip it when upgrading, and belts that already have it lose its effect until it's back on. Set by the server in multiplayer.
# Setting type: Boolean
# Default value: true
Tier 1 swamp key = true

## Gold coins this upgrade costs at the workbench (a swamp key plus this gold). Set by the server in multiplayer.
# Setting type: Int32
# Default value: 0
# Acceptable value range: From 0 to 100000
Tier 1 swamp key gold = 0

## Whether the belt has this upgrade (finds hidden treasure and ores). When off, belts skip it when upgrading, and belts that already have it lose its effect until it's back on. Set by the server in multiplayer.
# Setting type: Boolean
# Default value: true
Tier 2 wishbone = true

## Gold coins this upgrade costs at the workbench (a wishbone plus this gold). Set by the server in multiplayer.
# Setting type: Int32
# Default value: 0
# Acceptable value range: From 0 to 100000
Tier 2 wishbone gold = 0

## Whether the belt has this upgrade (frost resistant, like a wolf fur cape). When off, belts skip it when upgrading, and belts that already have it lose its effect until it's back on. Set by the server in multiplayer.
# Setting type: Boolean
# Default value: true
Tier 3 frost resistance = true

## Gold coins this upgrade costs at the workbench (a wolf fur cape plus this gold). Set by the server in multiplayer.
# Setting type: Int32
# Default value: 0
# Acceptable value range: From 0 to 100000
Tier 3 frost resistance gold = 0

## Whether the belt has this upgrade (clears the mist around you). When off, belts skip it when upgrading, and belts that already have it lose its effect until it's back on. Set by the server in multiplayer.
# Setting type: Boolean
# Default value: true
Tier 4 wisplight = true

## Gold coins this upgrade costs at the workbench (a wisplight plus this gold). Set by the server in multiplayer.
# Setting type: Int32
# Default value: 0
# Acceptable value range: From 0 to 100000
Tier 4 wisplight gold = 0

## Whether the belt has this upgrade (slow fall, no fall damage). When off, belts skip it when upgrading, and belts that already have it lose its effect until it's back on. Set by the server in multiplayer.
# Setting type: Boolean
# Default value: true
Tier 5 feather fall = true

## Gold coins this upgrade costs at the workbench (a feather cape plus this gold). Set by the server in multiplayer.
# Setting type: Int32
# Default value: 0
# Acceptable value range: From 0 to 100000
Tier 5 feather fall gold = 0

## Whether the belt has this upgrade (+150 more carry weight). When off, belts skip it when upgrading, and belts that already have it lose its effect until it's back on. Set by the server in multiplayer.
# Setting type: Boolean
# Default value: true
Tier 6 reinforced = true

## Gold coins this upgrade costs at the workbench (gold only). Set by the server in multiplayer.
# Setting type: Int32
# Default value: 1500
# Acceptable value range: From 0 to 100000
Tier 6 reinforced gold = 1500

[2. Personal]

## Use the belt's wishbone upgrade (pings near buried treasure and ores). Just for you.
# Setting type: Boolean
# Default value: true
Wishbone = true

## Turns the belt's wishbone on or off.
# Setting type: KeyboardShortcut
# Default value: PageUp
Wishbone key = PageUp

## Use the belt's wisplight upgrade (the wisp that clears mist). Just for you.
# Setting type: Boolean
# Default value: true
Wisplight = true

## Turns the belt's wisplight on or off.
# Setting type: KeyboardShortcut
# Default value: 
Wisplight key = 

## Use the belt's feather fall upgrade (slow fall, no fall damage). Just for you.
# Setting type: Boolean
# Default value: true
Feather fall = true

## Turns the belt's feather fall on or off.
# Setting type: KeyboardShortcut
# Default value: 
Feather fall key = 
```
