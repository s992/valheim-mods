# StabilityView: in-game test list

*Written and kept current by AI agents (AGENTS.md). CI can't run the game client: this is what a human checks before
a release, riskiest first. Agents add to it when a change needs a new check.*

Before anything: only one copy of the mod loads (no `StabilityView.dll` both in `plugins/StabilityView/` and a Thunderstore
folder), and `BepInEx/LogOutput.log` has `StabilityView <version> loaded` and no `StabilityView is turned off` line.

1. F7 in build mode shows support values on nearby pieces; outside build mode nothing (unless OnlyInBuildMode is
   off).
2. Placing or removing a piece: changed values show deltas (green up, red down); new pieces say "new".
3. F8 takes a snapshot, Shift+F8 clears it; deltas compare against it.
4. F9 shows lines to where each piece gets its support from.
5. With a build-camera mod, values are around the camera, not the player; the status line says how many pieces are
   hidden by MaxPieces or Radius.
6. Leaving build mode or turning the overlay off leaves nothing drawn on screen.

After a session: no `[Error` lines from `StabilityView` in `BepInEx/LogOutput.log`, and no `Error in ...` or `kept failing`
lines (the mod's error guards write those).
