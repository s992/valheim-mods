<!--
One mod per pull request, unless it changes shared files at the root. Agents fill this in completely;
"none" is a valid answer, an empty section is not.
-->

## What and why

<!-- The change and the real reason for it. No invented motivation. -->

## Risk

<!--
Which of these does it touch? Delete the ones it doesn't.
- Harmony patches (new or changed targets, transpilers, prefixes that skip the original)
- Other players' data: chests, ZDOs, RPCs, anything synced in multiplayer
- Integrations with other mods (reflection)
- Per-frame code (Update, OnGUI, hot patches)
- Config: a changed default (existing configs keep their old value), renamed or removed keys
- Release pipeline or shared build settings
-->

## Checked

<!-- What was actually verified: build, format, logic against decompiled game code (which build), etc. -->

## Not checked

<!-- Agents can't run the game. Say what is unverified. -->

## In-game test list (riskiest first)

1.
