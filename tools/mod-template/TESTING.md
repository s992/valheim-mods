# __MODNAME__: in-game test list

*Written and kept current by AI agents (AGENTS.md). CI can't run the game client: this is what a human checks before
a release, riskiest first. Agents add to it when a change needs a new check.*

Before anything: only one copy of the mod loads (no `__MODNAME__.dll` both in `plugins/__MODNAME__/` and a Thunderstore
folder), and `BepInEx/LogOutput.log` has `__MODNAME__ <version> loaded` and no `__MODNAME__ is turned off` line.

1. (The mod's main feature, the way a player uses it.)

After a session: no `[Error` lines from `__MODNAME__` in `BepInEx/LogOutput.log`, and no `Error in ...` or
`kept failing` lines (the mod's error guards write those).
