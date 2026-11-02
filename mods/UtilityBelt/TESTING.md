# UtilityBelt: in-game test list

*Written and kept current by AI agents (AGENTS.md). CI can't run the game client: this is what a human checks before
a release, riskiest first. Agents add to it when a change needs a new check.*

Before anything: only one copy of the mod loads (no `UtilityBelt.dll` both in `plugins/UtilityBelt/` and a Thunderstore
folder), Jotunn is installed, and `BepInEx/LogOutput.log` has `UtilityBelt <version> loaded` and no
`UtilityBelt is turned off` or `Couldn't create the utility belt` line.

Quick setup with cheats (`devcommands`): `spawn UtilityBelt`, `spawn CryptKey`, `spawn Wishbone`, `spawn CapeWolf`,
`spawn Demister`, `spawn CapeFeather`, `spawn Coins 3000`.

1. Upgrading at a workbench (level 1): the upgrade tab lists the belt with the next tier's cost only (e.g. 1 swamp
   key), the upgrade takes exactly that, and the tooltip's "Upgrades" list gains the tier. Repeat up to tier 6
   (1500 gold); afterwards the belt is gone from the upgrade tab.
2. A tier turned off in the server settings: the next upgrade skips it; a belt that already had it shows it as
   "turned off on this server" and its effect stops. Turn it back on: the effect returns, and a belt that skipped it
   is offered it as its next upgrade.
3. Two belts at different tiers in the inventory: each shows its own next cost in the upgrade tab.
4. With Equipment and Quick Slots (RandyKnapp) installed: the belt goes in the utility slot and the effects apply.
5. Haldor: once he sells Megingjord, the Utility Belt is listed right after it at 950 (or the configured price), with
   its blue icon and its own tooltip (not "Tooltip"), and everything he sold before is still listed after it. Buying
   it gives a quality 1 belt that isn't craftable at the workbench.
6. Effects while worn: +150 carry weight (+300 with tier 6) and one "Utility Belt" status icon; a sunken crypt door
   opens without a swamp key; wishbone pings near buried treasure or silver; no freezing at night in the mountains;
   the wisp clears Mistlands mist; slow fall with no fall damage. All of them stop when the belt is taken off.
7. Wearing a feather cape with a feather-fall belt, then taking the cape off: slow fall stays.
8. Personal toggles: PageUp turns the wishbone off and on (top-left message, tooltip says "off in your settings");
   the wisplight and feather fall keys, once bound, do the same.
9. Multiplayer: the server's settings (price, costs, tiers) reach a client and a non-admin can't change them; a client
   without the mod is turned away.
10. The belt looks blue-tinted on the character, on the ground and in its icon, and Megingjord still looks normal.

After a session: no `[Error` lines from `UtilityBelt` in `BepInEx/LogOutput.log`, and no `Error in ...` or
`kept failing` lines (the mod's error guards write those).
