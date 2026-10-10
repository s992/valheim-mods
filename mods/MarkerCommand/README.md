# MarkerCommand

## ai usage disclaimer

with the exception of this readme, everything in this repo is ai-generated.

## description

this mod adds a console command that allows you to place a marker on the map at specific coordinates

## non-goals

this project will not make any attempt to support icons not included in the "supported icons" list below.

## usage

you can enter these into the [developer console](https://valheim.weirdgloop.org/w/Developer_console) or chat. if you are entering them in chat you must prefix them with `/`, e.g. `/marker me`.

all parameters other than position (`me` or `<x> <y>`) are optional. if an icon type is not specified, the default `dot` (`icon3`) is used.

**place marker at current position**

`marker me [name] [icon]`

**place marker at coordinates**

`marker <x> <y> [name] [icon]`

### examples

#### mark bone crypt as you run past

`marker me "spooky skellington cave" rune`

#### plot a course through the sea

`marker -2819 -1642 "noboat canal" death`

#### combining with keybinds

`bind <key> marker me "some mushrooms to pick after i lose this troll" hammer`

press `<key>` to drop a marker without stopping to type 

### supported icons

#### standard icons

icons that you can place on the map normally via double clicking

- `fire` (also accepts `icon0`): campfire, the first icon
- `house` (`icon1`): the second
- `hammer` (`icon2`): aka anchor, the third
- `dot` (`icon3`): the fourth
- `rune` (`icon4`): i dont know what the hell this thing is, but it's the bottom one

#### special icons

icons that are not normally placeable by the player. i have not tested these with the cartography table, so godspeed.

- `death`
- `bed`
- `shout`
- `boss`
- `player`
- `randomevent`
- `ping`
- `eventarea`
- `memorial`
- `hildir1`, `hildir2, `hildir3`
