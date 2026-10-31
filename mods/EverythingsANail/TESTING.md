# EverythingsANail: in-game test list

*Written and kept current by AI agents (AGENTS.md). CI can't run the game client: this is what a human checks before
a release, riskiest first. Agents add to it when a change needs a new check.*

Before anything: only one copy of the mod loads (no `EverythingsANail.dll` both in `plugins/EverythingsANail/` and a Thunderstore
folder), and `BepInEx/LogOutput.log` has `EverythingsANail <version> loaded` and no `EverythingsANail is turned off` line.

## Multiplayer (dedicated server running the mod)

1. Server config `OnlyAllow = Hoe`: a client's radial menu offers only the hammer and hoe, whatever the client's
   own config says. The client log has `Using the server's tool rules`.
2. Edit the server's config file while a client is connected: the client's menu follows within a few seconds.
3. Client with the server's rules leaves and plays single player: its own rules apply again.

## Single player

4. With the hammer equipped, the radial key opens the tool menu; picking the hoe/cultivator/pickaxe makes the
   hammer act as it (build menu, attacks, skill), with the hammer's durability bar.
5. Log out with the hammer disguised and back in: it comes back as the same tool; dropped or put in a chest it's
   a plain hammer.
6. At a workbench the disguised hammer turns back into a hammer (no tool upgrade offered for it).
7. A tool you've never owned isn't offered; one excluded by the config isn't either.
8. Epic Loot magic on the hammer survives switching tools.
9. `nail_tools` in the console lists the tools and their internal names.

After a session: no `[Error` lines from `EverythingsANail` in `BepInEx/LogOutput.log`, and no `Error in ...` or `kept failing`
lines (the mod's error guards write those).
