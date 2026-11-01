# Changelog

*This changelog is written by Claude, an AI assistant, from the project's commit history.*

## Unreleased

- If a game update breaks something the mod relies on, the mod now turns itself off completely and says so in game and in the log, instead of running half-patched.
- If a game update changes how the game counts or takes crafting materials, the mod now turns off rather than risk counting chest contents without taking them (or the reverse).
- An error in the crafting menu counts, requirement checks or smelter hints is logged once and shown as a single message instead of repeating every frame. Paying for a craft is deliberately not covered: an error there stops the craft.
- With "Log actions" on, items taken from a chest are logged once per chest instead of once per stack.

## 1.0.0

- Initial release: crafting, building and smelting use materials from nearby chests, measured from the player and capped at the area the game keeps loaded.
- With Multi User Chest, chests other players own can be used: their items are fetched into the inventory, or the chest is borrowed ahead of time.
- Smelters, kilns, windmills, spinning wheels and eitr refineries take the selected material and fuel from storage; a key changes the material and another blocklists it.
- A status line says how many chests are in use and why any were skipped. Holding E on a smelter another player owns doesn't overfill it.
