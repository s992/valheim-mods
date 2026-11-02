# Changelog

*This changelog is written by Claude, an AI assistant, from the project's commit history.*

## 1.0.3

- If a game update breaks something the mod relies on, the mod now turns itself off completely and says so in game and in the log, instead of running half-patched.
- A malformed rules message from a server is now rejected with a warning (local rules stay) instead of causing an error.
- An error in the radial menu, its hints or tooltips is logged once and shown as a single message instead of repeating every frame (which could block the player's controls).
- On a dedicated server, a config file edit made while the file was still locked is now picked up a moment later instead of being missed.

## 1.0.2

- Added this changelog to the Thunderstore package. No gameplay changes.

## 1.0.1

- New build pieces (for example, oat and potato seeds for the cultivator) are now discovered while carrying a hammer that can become the tool. Previously the real tool had to be in the inventory.

## 1.0.0

- Initial release. The hammer can become any tool that has been in the character's inventory (hoe, cultivator, butcher knife, scythe, fishing rod, axes, pickaxes and tools from other mods), selected from the radial menu.
- Tools are detected by what they do, so tools added by other mods are included automatically.
- Server owners can choose which tools are allowed; the server's rules apply to everyone connected.
