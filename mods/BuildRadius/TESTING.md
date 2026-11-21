# BuildRadius: in-game test list

*Written and kept current by AI agents (AGENTS.md). CI can't run the game client: this is what a human checks before
a release, riskiest first. Agents add to it when a change needs a new check.*

Before anything: only one copy of the mod loads (no `BuildRadius.dll` both in `plugins/BuildRadius/` and a Thunderstore
folder), and `BepInEx/LogOutput.log` has `BuildRadius <version> loaded` and no `BuildRadius is turned off` line.

1. **Dedicated server with the mod, client with the mod, different settings.** Server `Multiplier = 3`, client's own
   config `Multiplier = 1`. Join: the client's log says `Using the server's build radius settings`, and a workbench lets
   you build about three times as far out as vanilla (hover the bench to see the circle). Failure: vanilla radius, or
   the client's own value.
2. **Admin changes it live.** As a server admin, change `Multiplier` in Configuration Manager: placed stations' circles
   and build range follow within a moment, for you and another connected player. A non-admin can't change it.
3. **Client with the mod on a vanilla server.** Join a server without BuildRadius: the radius is vanilla (no
   `Using the server's...` line), whatever the client's config says.
4. **Vanilla client on a server with the mod.** It connects (no version error) and builds with the vanilla radius.
5. **Single player.** Default `Multiplier = 2`: the workbench circle is twice the vanilla size; building, and removing
   pieces with the hammer, work at the edge of the bigger circle and not past it. Set it back to 1: vanilla again
   without relogging.
6. **Extensions.** Add a chopping block and tanning rack to a workbench: each adds twice its vanilla bonus to the
   circle. Remove one: the circle shrinks.
7. **Station radii.** `Station radii = piece_workbench:40, forge:30`: the workbench circle is 40 m before
   extensions, the forge 30 m, the stonecutter still follows the multiplier. Then `forge:far`: a warning in the log
   (`Station radii: ignored forge:far`), the forge goes back to the multiplier.
8. **Base area.** With a large radius, monsters don't spawn inside the bigger circle (the game sizes the player-base
   area from the build radius).
9. **Placing a new station.** While placing a workbench nothing odd shows; once placed its circle is the modded size.

After a session: no `[Error` lines from `BuildRadius` in `BepInEx/LogOutput.log`, and no `Error in ...` or
`kept failing` lines (the mod's error guards write those).
