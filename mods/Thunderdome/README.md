# Thunderdome

## ai usage disclaimer

with the exception of this readme, everything in this repo is ai-generated.

## description

this mod adds a new buildable, the Thunderdome. when placed, the 'dome will actively monitor and obliterate enemies who wander into its radius. this mod is not intended to be balanced and will not receive any balance changes. i built it because i got sick of greydwarf brutes murdering me while i was trying to build.

the 'dome *does not discriminate* (mostly, see below): it will happily obliterate yagluth as easily as it'll obliterate a neck. enemies will not drop loot when they are obliterated.

the 'dome will not strike:

- players
- passive/friendly npcs (e.g., tamed animals, deer, happy dvergr)

*important boss note*: if the Thunderdome kills a boss, the world will still "count it" and progress despite not dropping the important items you might want to progress along with it.

## usage

### placing

the Thunderdome build piece can be found in the build hammer's "Misc" section. by default (config option: `Cost`), it costs:

- 5 surtling cores
- 10 fine wood
- 10 ancient bark
- 10 greydwarf eyes

once placed, the Thunderdome immediately starts functioning in the `Mode` (default: `Thunderdome`) and `Radius` (default: `30`) that you've configured.

there is no cost (e.g., fuel) for ongoing usage.

### modes

there are three modes (config option: `Mode`):

1. `Thunderdome` obliterates any hostile npc that enters its radius.
2. `Repel` peacefully suggests that hostile npcs should leave the radius, and they usually (slowly) flee.
3. `Both` allows you to strike terror into the hearts of your enemies immediately before destroying the very atoms that comprise their soul. in this mode, hostile npcs both flee and get obliterated.

### loot

enemies that are killed by the 'dome do not drop loot. i'm not interested in changing this.

### multiplayer

the mod is required on the server and all players.

## settings

settings are configurable via editing the settings file directly (`BepInEx/config/s992.Thunderdome.cfg`) or via a [configuration manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/).

these are the default settings:

```
[1. Server]

## Thunderdome: lightning kills any hostile creature inside the dome, bosses included, and it drops nothing. Repel: monsters turn away like at the traders' camps. Both: monsters turn away, and lightning kills any that come in anyway. Set by the server in multiplayer.
# Setting type: WardMode
# Default value: Thunderdome
# Acceptable values: Thunderdome, Repel, Both
Mode = Thunderdome

## Size of every ward's dome in metres. Lightning strikes inside the dome. Repelling reaches a little further: the area is a box as wide as the dome, and monsters with nothing to chase stay about 15 m beyond it. Changing this updates placed wards. Set by the server in multiplayer.
# Setting type: Single
# Default value: 30
# Acceptable value range: From 5 to 100
Radius = 30

## What a ward costs to build, as ItemPrefabName:amount separated by commas. Internal names, e.g. ElderBark is Ancient Bark. Applies to wards built after the change. Set by the server in multiplayer.
# Setting type: String
# Default value: SurtlingCore:5,FineWood:10,ElderBark:10,GreydwarfEye:10
Cost = SurtlingCore:5,FineWood:10,ElderBark:10,GreydwarfEye:10

[2. Personal]

## Show the dome around placed wards, like the traders'. It always shows while placing a ward. Just for you.
# Setting type: Boolean
# Default value: true
Show dome = true
```
