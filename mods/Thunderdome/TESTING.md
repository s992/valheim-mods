# Thunderdome: in-game test list

*Written and kept current by AI agents (AGENTS.md). CI can't run the game client: this is what a human checks before
a release, riskiest first. Agents add to it when a change needs a new check.*

Before anything: only one copy of the mod loads (no `Thunderdome.dll` both in `plugins/Thunderdome/` and a Thunderstore
folder), and `BepInEx/LogOutput.log` has `Thunderdome <version> loaded` and no `Thunderdome is turned off` line.

1. `Mode` Thunderdome (default): a hostile creature that enters the dome is struck by a lightning bolt within about
   half a second and dies, dropping nothing (no items, and nothing from the ragdoll a few seconds later). Nearby
   buildings take no damage from the bolt.
2. A second player standing nearby sees and hears the same strike, and the creature dies only once.
3. Not struck inside the dome: you, other players, tamed animals, deer and other passive animals, and dvergr until
   they're angered. A boss summoned inside the dome is struck.
4. `Mode` Repel: a monster that's idle or attacking a piece runs away, and one chasing you stops and flees once you step
   inside; no lightning. `Mode` Both: monsters turn away, and any that come in anyway get struck.
5. Place a ward while monsters are close, but only aim at them with the hammer first: the ghost alone must not strike
   or repel anything.
6. While placing: the ghost shows a dome and a ring at the configured radius, and goes red where it can't be placed.
7. Placed: the dome stays, the ring is gone, and hovering shows the piece's name and radius. It looks unlike a guard stone
   (blue), and building nearby needs no permission from it.
8. Change `Radius` in Configuration Manager: the dome and strike range or monster behaviour on placed wards follow without relogging.
   Turning `Show dome` off hides the dome but the ward still works.
9. Log out and back in: the ward still works. Destroying it with the hammer refunds the cost, and monsters come back.
10. On a dedicated server: `Mode`, `Radius` and `Cost` come from the server and a non-admin can't change them; a client without
   the mod is turned away.
11. `Cost`: an unknown item name logs a warning and is skipped; with no usable entry, the cost stays as it was.

After a session: no `[Error` lines from `Thunderdome` in `BepInEx/LogOutput.log`, and no `Error in ...` or
`kept failing` lines (the mod's error guards write those).
