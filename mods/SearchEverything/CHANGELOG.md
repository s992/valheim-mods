# Changelog

*This changelog is written by Claude, an AI assistant, from the project's commit history.*

## 1.0.0

- Initial release: a search box on the crafting panel filters recipes by name, ingredient or item type (such as "helmet" or "food"), and a search box in place of an open chest's name fades the items that don't match, in the chest and the inventory.
- A nearby-chest search window (Ctrl+K, or a button above the inventory) lists matching items and the chests holding them within 64 m of the camera, including carts and ships. Picking one marks its chests on screen, through walls, with their distance.
- Markers go away when their chest is opened, after 60 seconds, or with Ctrl+Shift+K. Keys, range, timings and each feature can be changed in the config.
- Typing in a search box doesn't move the player, the camera or trigger game keys, and Ctrl+K doesn't count as K for debug mode's keys.
- Read-only: the mod doesn't move items and doesn't need any other mod.
