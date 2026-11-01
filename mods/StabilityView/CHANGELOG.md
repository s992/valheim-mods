# Changelog

*This changelog is written by Claude, an AI assistant, from the project's commit history.*

## Unreleased

- If a game update breaks something the mod relies on, the mod now turns itself off completely and says so in game and in the log, instead of running half-patched.
- An error in the overlay is logged once and shown as a single message; if it keeps failing, the overlay switches itself off and disappears instead of staying frozen on screen.

## 0.1.0

- Initial release: a structure-support overlay showing each nearby piece's support value, colour-coded from collapse to maximum.
- Optional lines show where each piece gets its support from.
- Snapshots show how support changed while building; a manual snapshot stays until it's cleared.
- Labels are picked by distance from the camera and only for pieces on screen, and a status line says when pieces are left out.
