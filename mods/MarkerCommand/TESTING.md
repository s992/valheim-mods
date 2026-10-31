# MarkerCommand: in-game test list

*Written and kept current by AI agents (AGENTS.md). CI can't run the game client: this is what a human checks before
a release, riskiest first. Agents add to it when a change needs a new check.*

Before anything: only one copy of the mod loads (no `MarkerCommand.dll` both in `plugins/MarkerCommand/` and a Thunderstore
folder), and `BepInEx/LogOutput.log` has `MarkerCommand <version> loaded` and no `MarkerCommand is turned off` line.

1. `marker 100 -200 "My base" house` in the console adds a house pin named My base at (100, -200).
2. `/marker me camp` in chat adds a dot pin at the player.
3. `marker 1.5 2` works with a decimal point whatever the system language (German Windows uses commas).
4. `marker 10 10 x boss` uses the boss icon; an unknown icon name becomes part of the name.
5. With Server Devcommands: typing `marker ` shows parameter hints; without it, no warning.
6. In the main menu (no world): a clear message, no error.

After a session: no `[Error` lines from `MarkerCommand` in `BepInEx/LogOutput.log`, and no `Error in ...` or `kept failing`
lines (the mod's error guards write those).
