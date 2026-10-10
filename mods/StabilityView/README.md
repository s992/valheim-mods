# StabilityView

## ai usage disclaimer

with the exception of this readme, everything in this repo is ai-generated.

## description

this mod adds a detailed stability overlay to help plan structures or better understand how valheim's stability works.

![mod screenshot](https://raw.githubusercontent.com/s992/ValheimStabilityView/main/screenshot.jpg)

## usage

with your hammer out, press F7 to enable the overlay. there are some additional keybinds that can be configured via settings.

### settings

settings are configurable via editing the settings file directly (`BepinEx/config/s992.StabilityView.cfg`) or via a [configuration manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/).

these are the default settings:

```
[Changes]

## Take a snapshot automatically just before you place or remove a piece, so each change shows its effect.
# Setting type: Boolean
# Default value: true
AutoSnapshot = true

## Tint pieces whose support changed since the snapshot (green = up, red = down).
# Setting type: Boolean
# Default value: true
HighlightChanges = true

## Ignore changes smaller than this.
# Setting type: Single
# Default value: 0.5
# Acceptable value range: From 0 to 100
ChangeThreshold = 0.5

## Stop showing changes this many seconds after the snapshot. 0 = until the next snapshot.
# Setting type: Single
# Default value: 0
# Acceptable value range: From 0 to 600
ChangeDuration = 0

[Display]

## Only show the overlay while a building tool (hammer, hoe, ...) is equipped.
# Setting type: Boolean
# Default value: true
OnlyInBuildMode = true

## Show on-screen pieces within this many metres of the camera.
# Setting type: Single
# Default value: 30
# Acceptable value range: From 2 to 150
Radius = 30

## Show at most this many on-screen pieces (closest to the camera first).
# Setting type: Int32
# Default value: 200
# Acceptable value range: From 1 to 1000
MaxPieces = 200

## Show a status line with how many pieces are labelled and how many were left out.
# Setting type: Boolean
# Default value: true
ShowStatus = true

## Label font size.
# Setting type: Int32
# Default value: 13
# Acceptable value range: From 8 to 32
FontSize = 13

## Prefix labels with the piece's material (Wood, Stone, ...).
# Setting type: Boolean
# Default value: false
ShowMaterial = false

## Show support as current/max instead of just current.
# Setting type: Boolean
# Default value: true
ShowMaxSupport = true

## Show the support value below which the piece collapses.
# Setting type: Boolean
# Default value: false
ShowBreakThreshold = false

## Skip labels for pieces at their material's maximum (grounded pieces).
# Setting type: Boolean
# Default value: false
HideFullySupported = false

[Keys]

## Show or hide the stability overlay.
# Setting type: KeyboardShortcut
# Default value: F7
ToggleOverlay = F7

## Show or hide the lines to where each piece gets its support.
# Setting type: KeyboardShortcut
# Default value: F9
ToggleSourceLines = F9

## Record current support values; changes are then shown relative to them.
# Setting type: KeyboardShortcut
# Default value: F8
TakeSnapshot = F8

## Forget the snapshot and stop showing changes.
# Setting type: KeyboardShortcut
# Default value: F8 + LeftShift
ClearSnapshot = F8 + LeftShift

[Support sources]

## Draw a line from each piece to the piece(s) it gets its support from.
# Setting type: Boolean
# Default value: true
ShowSourceLines = true

## Draw source lines on top of everything instead of hiding them behind geometry.
# Setting type: Boolean
# Default value: true
LinesThroughWalls = true
```
