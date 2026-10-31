# valheim-mods

Client-side Valheim mods for BepInEx 5, written with AI agents (mostly Claude). Each folder under `mods/` is
one mod, published as its own Thunderstore package by the maintainer, s992 (Thunderstore team `s992`). This file
is what any agent needs to build, change or create a mod here; the `docs/` files go deeper.

## Ground rules

- **Ask before anything public or irreversible**: pushing, opening pull requests, tags, Thunderstore publishes,
  GitHub settings. A go-ahead covers that one action, not later ones. The repo is public, so a push publishes code.
- Only the maintainer releases (tags, Thunderstore). Changes reach `main` through pull requests, agents' included:
  work on a branch and fill in every section of `.github/pull_request_template.md`.
- **Humans write READMEs** (`mods/<Name>/README.md`, including the AI-usage disclaimer). Don't create or rewrite
  one; review it against the code when asked (docs/releasing.md, "Reviewing the README").
- **Agents write changelogs, visibly as AI**: `mods/<Name>/CHANGELOG.md` entries in neutral sentence-case prose
  under the file's "written by an AI" note, never in the README's voice. The same goes for anything else an agent
  writes for a public page. The project doesn't hide that its content is AI-generated.
- Don't change the user's global setup (git config, SSH, installed tools) to get around a problem; explain it.
- Commit messages explain why, and the why must be true (no invented "players asked for..."). Keep each commit to
  one mod, unless it changes shared files at the root.
- Nothing in game can be verified by an agent. Say what was checked (builds, logic against decompiled code) and
  give the user an in-game test list, riskiest first.

## Setup

Needs Valheim (Steam), BepInEx 5 in a mod-manager profile (r2modman by default), the .NET 8 SDK and Python 3.
Builds reference the game's own DLLs from the install (never committed) and copy each mod into the profile.

| Setting | Default | Meaning |
|---|---|---|
| `ValheimDir` | `C:\Program Files (x86)\Steam\steamapps\common\Valheim` | game (or dedicated server) install |
| `PluginsDir` | `%APPDATA%\r2modmanPlus-local\Valheim\profiles\Default\BepInEx\plugins` | where builds are deployed; skipped if missing |

Override them as environment variables, or in a gitignored `Directory.Build.user.props` at the repo root, which
both the build and `tools/decompile.py` read:

```xml
<Project>
  <PropertyGroup>
    <ValheimDir>D:\SteamLibrary\steamapps\common\Valheim</ValheimDir>
    <PluginsDir>$(APPDATA)\r2modmanPlus-local\Valheim\profiles\Testing\BepInEx\plugins</PluginsDir>
  </PropertyGroup>
</Project>
```

## Layout

- `mods/<Name>/`: one mod. `<Name>.csproj` (only `<Version>`, `<Description>` and extra game references), sources,
  `thunderstore.toml`, `README.md`, `CHANGELOG.md`, `icon.png`, optional `scripts/make_icon.py` and `docs/`.
- `Directory.Build.props`: everything shared: `netstandard2.1`, BepInEx packages, the common game references,
  plugin GUID `s992.<Name>` from the folder name, the paths above. `Directory.Build.targets`: deploy to the profile.
- `ValheimMods.sln`: every mod.
- `shared/`: source compiled into every mod: all-or-nothing patching, per-frame error guard, player notices
  (docs/patterns.md, "Failing safely").
- `tools/`: `new_mod.py` (scaffold), `mod-template/`, `decompile.py` (read game/mod code), `icon_kit.py`,
  `boot_test.py` (CI's boot test).
- `docs/`: `game-api.md`, `integrations.md`, `patterns.md`, `icons.md`, `releasing.md` (see below).
- `.github/workflows/`: `build.yml` (every push; builds against the Valheim dedicated server, checks formatting,
  boots the server with every mod: docs/releasing.md, "Boot test"),
  `release.yml`. `.github/`: `CODEOWNERS` (READMEs and build/release files need the maintainer's review),
  `dependabot.yml`, `pull_request_template.md`.

| Mod | What it does |
|---|---|
| MarkerCommand | `marker` console/chat command for map pins; Server Devcommands hints |
| StabilityView | structure-support overlay: values, where support comes from, deltas while building |
| LootFilter | junk rules for auto-pickup and ground sparkles (items and groups, pause key). Its Epic Loot support was removed and is kept at tag `LootFilter/checkpoint/with-epic-loot` for a planned separate mod |
| EverythingsANail | the hammer becomes any tool you own (radial menu), server-synced rules over its own RPC |
| CraftFromStorage | craft, build and smelt from nearby chests on top of Multi User Chest (not a Thunderstore dependency); `mods/CraftFromStorage/docs/ownership.md` covers chest ownership and ZDO conflicts |

These are the best examples of finished code: read the closest one before building something similar.

## Commands

```bash
dotnet build -c Release                                   # every mod; deploys to the profile. Warnings fail it
dotnet build mods/<Name>/<Name>.csproj -c Release         # one mod
dotnet format ValheimMods.sln                             # fix style; CI runs it with --verify-no-changes
python tools/new_mod.py <Name> --description "<one sentence, max 250 chars>" --categories tools,client-side
python tools/decompile.py --type Player --type ItemDrop   # decompiled .cs paths for game classes
python tools/decompile.py --project                       # whole game as a .cs tree, to grep
python tools/decompile.py --plugin EpicLoot --project     # another installed mod
gh workflow run release.yml -f mod=<Name>                 # release dry run (needs write access)
```

`decompile.py` installs `ilspycmd` as a dotnet tool on first use (ask the user first) and caches output per game
build in the temp folder.

Warnings are errors, locally and in CI. MSB3277 is silenced on purpose (Unity's System.* versions). Analyzer rules
that clash with Harmony, Unity or Valheim are turned off in `.editorconfig`, each with its reason. Fix a warning
rather than silencing it; when a rule really is wrong for one spot, suppress it there with a justification
(`[SuppressMessage(..., Justification = "...")]`), and only turn a rule off repo-wide for a reason like those.

## Conventions

- Mod names are PascalCase letters and digits; the folder name sets the assembly, namespace, plugin name and
  GUID `s992.<Name>` (config file `BepInEx/config/s992.<Name>.cfg`). Never rename a published mod's GUID.
- `assembly_valheim` is publicized, so private game members can be used directly. Add any other game assembly
  to the mod's csproj (`Directory.Build.props` lists the usual ones).
- Decompile the installed game before writing code against it. The notes in `docs/` say where to look, but the
  game updates and they go stale.
- Integrate with other mods optionally (by reflection), never as a hard dependency unless the user asks.
- Changes to game objects are reversible (the mod can be toggled off), and per-frame work stays cheap.
- Console commands with parameters register Server Devcommands hints (see MarkerCommand's `Hints.cs`).

Things that cost time before:
- Valheim uses Unity's new input system: `KeyboardShortcut.IsDown()` may miss presses. Read keys through `ZInput`.
- "Nearby" means near the **camera** (players use build-camera mods). Any limit must be visible to the player
  (a status line), or missing things look like bugs.
- Edit csproj/XML files with a real editor, not `sed`: an escape like `\a` once became a BEL character.
- BepInEx keeps saved config values, so a changed default doesn't reach existing configs. Say so when one changes.
- Players often have the dev build (`plugins/<Name>/`) and the Thunderstore copy in one profile; two copies or
  two GUIDs loading is a common cause of odd reports.

## Docs

- `docs/game-api.md`: where things live in the game (console, map pins, input, camera, building support, item
  drops, inventory UI, progression).
- `docs/integrations.md`: other mods (Epic Loot, Server Devcommands, Configuration Manager, Multi User Chest...).
- `docs/patterns.md`: config Configuration Manager renders well, hotkeys, optional integration, Harmony choices.
- `docs/icons.md`: the shared icon style and how to draw one.
- `docs/releasing.md`: CI, dry runs, releases, changelog rules, README review.
