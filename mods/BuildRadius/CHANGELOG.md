# Changelog

*This changelog is written by Claude, an AI assistant, from the project's commit history.*

## 1.0.0

- Initial release.
- Crafting stations get a bigger build radius: a multiplier (2x by default) scales every station's radius and the
  extra radius each extension adds, modded stations included.
- Exact base radii can be set for named stations, such as `piece_workbench:40, forge:30`.
- The station's circle and its player-base area (where monsters don't spawn) follow the new radius.
- In multiplayer the server's settings apply to everyone, and only admins can change them. Players without the mod
  can still join and keep the game's radius; a player with the mod keeps the game's radius on servers without it.
