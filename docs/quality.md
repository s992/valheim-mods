# Quality and stability

*Written by Claude, an AI assistant, and kept current by the agents that work here.*

Everything in this repo except the READMEs is written by AI agents, and nobody can run the game in CI. This is how a
change is kept from breaking players' games anyway: what each gate catches, why it exists, and what still needs a
human. Agents follow it through AGENTS.md; this file is the reasoning.

## What can go wrong, worst first

1. **A game update moves a method a patch targets.** Harmony used to stop patching at the first failure and keep the
   rest, so a mod could half work: CraftFromStorage counting chest contents without taking them (free crafts) or
   the reverse. Players update the game without any commit here.
2. **A change breaks something that worked.** Agents write the code; there's no in-game test in CI.
3. **A mod we integrate with changes** (MultiUserChest, Epic Loot, Server Devcommands, My Build Camera). Our
   integrations are by reflection and degrade to a warning, which nobody reads.
4. **A bad release.** A Thunderstore version can't be replaced, only superseded.
5. **Losing players' items.** CraftFromStorage edits chests other players can see; the rules for that are in
   `mods/CraftFromStorage/docs/ownership.md`.

## The gates

| Gate | Where | Catches | Covers |
|---|---|---|---|
| Warnings are errors, .NET + Unity analyzers | every build (`Directory.Build.props`, `.editorconfig`) | sloppy code, `?.`/`??` on Unity objects (skips Unity's destroyed-object check), locale-dependent number formatting, field names outside the `m_`/`s_` style | 2 |
| Banned APIs | every build (`BannedSymbols.txt`) | code that bypasses `shared/`: Harmony's `PatchAll` (half-patching), legacy input, `ZInput` key reads, Unity's log | 1, 2 |
| Format check | CI (`dotnet format --verify-no-changes`) | style drift between agents | 2 |
| Commit rules | CI on pull requests (`tools/check_commits.py`) | a commit changing several mods without a shared change; an AI-authored commit editing a README | all |
| Unit tests | CI, `tests/ValheimMods.Tests` | logic regressions in parsing, rules, wire formats, CraftFromStorage's item taking and pull planning, labels; a game update adding an item type LootFilter hasn't grouped | 2, 1, 5 |
| Boot test | CI, `tools/boot_test.py` | patches that don't apply, transpiler counts that changed, errors in `Awake`/`Start`, missing references, on the real game with real BepInEx | 1, 2 |
| Nightly game check | `game-check.yml` | the above against the latest game build, and with the latest integrated mods (their warnings fail it); opens a `game-check` issue | 1, 3 |
| All-or-nothing patching | every mod (`shared/SafePatching.cs`) | a patch that fails in a player's game turns the whole mod off with a log line and an in-game message, instead of half applying | 1 |
| Per-frame guards | `shared/FrameGuard.cs`, `shared/PatchGuard.cs` | an error in an `Update`/`OnGUI` or in a patch on a per-frame game method is logged once and contained, instead of filling the log or breaking the player's update loop | 2 |
| Package check | `release.yml` (`tools/check_package.py`) | missing files, a manifest that doesn't match the csproj, a stale or extra DLL | 4 |
| Changelog as you go | `## Unreleased` in each `CHANGELOG.md`, `release.yml`'s changelog check | a release whose notes miss changes (shared code reaches every mod) | 4 |
| Dev builds say so | `Directory.Build.targets` | "which copy was I running?": local builds log `<version>-dev+<time>` and deploy over the Thunderstore install's DLL, so a profile has one copy | 2 |
| In-game test list | `mods/<Name>/TESTING.md`, before each release | everything client-side: UI, input, multiplayer behaviour | 2, 5 |
| Review routing | `CODEOWNERS`, pull request template, branch protection on `main` | READMEs and the build/release files go past the maintainer; every change says its risk, what was checked and what wasn't | all |

Known gap: agents push and merge as the maintainer's GitHub account, so branch protection can't require a review
(nobody can approve their own pull request) and the history can't tell an agent's change from the maintainer's.
Commits carry a `Co-Authored-By` line for the agent. Separating them needs a bot account or GitHub App, which only the
maintainer can create.

Deliberately not done:
- **Nullable reference types**: Unity's destroyed objects compare equal to null without being null, which nullable
  analysis can't express; the Unity analyzers cover the real problem instead.
- **Coverage targets**: most mod code talks to the running game; tests target logic where a bug would be expensive.
- **A shared runtime DLL**: mods are released separately and players mix versions; `shared/` is compiled into each.
- **Valheim's public-test branch in the nightly check**: only the client has it, and the client can't be downloaded
  anonymously, so the check sees an update the day it reaches players.

## Audit, October 2026

Every mod was read for risk after the move into this repo. Fixed:
- CraftFromStorage: transpilers whose call counts changed only logged a warning and stayed half applied (now fail the
  patch, turning the mod off).
- LootFilter: the optional My Build Camera patch could throw out of `Plugin.Awake`, leaving the filter without its
  hotkeys and drop effects (now contained, with a warning).
- EverythingsANail: a short or malformed rules package from the server threw inside the RPC handler (now rejected
  with a warning; local rules stay).
- All: 13 uses of `?.`/`??` on Unity objects; per-frame code with no error containment; StabilityView's number
  formatting now states the player's locale explicitly.

Checked and left as is:
- CraftFromStorage smelting takes an ore from a chest before the game's own insert runs. The game can only refuse
  after that for a full smelter, and the mod's own fullness check (the game's queue plus our in-flight inserts) runs
  first and is never lower, so the ore always arrives.
- CraftFromStorage pulls from other players' chests: planned all or nothing, timed out, and a late reply lands in
  the inventory or at the player's feet.
- EverythingsANail's disguise only swaps the hammer's shared item data while equipped; saves store the prefab name
  and one custom-data key, so a saved or dropped hammer is a plain hammer.
- LootFilter's drop effects record and restore every change, and skip destroyed drops.

Not read line by line: StabilityView's support analysis (read-only display) and CraftFromStorage's storage
network bookkeeping beyond the paths above. Their failures show up on screen, not in save data.

The cleanup that followed (also October 2026) moved code shared by several mods into `shared/` (hotkeys, the log
source, name and version), gave every mod a changelog, and split testable logic out of CraftFromStorage,
StabilityView and EverythingsANail: 85 tests, up from 39.

## When something fails

- **Build run red**: the failing step's log says which gate; fix the cause rather than loosening the gate. Turning an
  analyzer rule off needs a reason in `.editorconfig` like the ones there.
- **Boot test lists a new `[Error : Unity Log]` line after a game update**: if the game prints it with no mods
  (before `Chainloader startup complete`, or vanilla code in its stack trace), add it to `ALLOWED` in
  `tools/boot_test.py`; otherwise it's ours.
- **`game-check` issue**: the run with mods alone failing means the game changed (decompile, fix, release); only the
  run with integrations failing means another mod changed (check `docs/integrations.md`, update the hookup).
