---
name: valheim-mod
description: Step-by-step workflow for Valheim BepInEx mods in this repo (valheim-mods): designing with the user, decompiling the game, scaffolding with tools/new_mod.py, implementing, building, reporting, icons and releases. Use it whenever the user wants a new Valheim mod, a feature or fix in one under mods/ (MarkerCommand, StabilityView, LootFilter, EverythingsANail, CraftFromStorage, ...), an integration with another mod (Epic Loot, Server Devcommands, Configuration Manager), a mod icon, CI work or a Thunderstore release, even if they only describe a gameplay idea ("a mod that...", "can we make it so in valheim...") without saying "mod" or "BepInEx".
---

# Valheim mods: the workflow

`AGENTS.md` at the repo root (loaded through `CLAUDE.md`) has the ground rules, setup, layout, commands and
conventions; follow them. This skill is the order of work and how to talk to the user. The user may be the
maintainer (s992) or someone working from a clone or fork: check `git remote -v` and don't assume they can push to
`s992/valheim-mods` or publish to Thunderstore.

Users of these mods are engineers who like clear trade-offs but make the product calls. Lead messages with what
happened, keep them skimmable (short sections, tables for keybinds and settings), no recap of the conversation.

Work out where the request sits, then pick up at that step.

## 1. Understand and design

Match the ceremony to the request. A small, well-specified feature (a console command with obvious output, a
single tweak) can go straight to building; make the minor choices yourself and list them in the report so
they can be changed. A bigger or open-ended idea needs its behaviour settled before writing code: "loot
filter" could have meant several different things.
- Research first (step 2) so the questions are concrete and the options are actually feasible.
- Ask with `AskUserQuestion` when the choice belongs to the user: what the feature does, how players control it,
  defaults. Offer a recommended option. Don't ask about things the code or conventions already answer.
- For anything non-trivial, propose the design in a short message (tables work well for keybinds and settings),
  say what you're unsure of, and wait for a go-ahead. Expect the user to refine details at this stage.
- When they push back or suggest an alternative, take it seriously; if their idea has a flaw, say so with the
  reason and suggest a variant (e.g. "colour with alpha 0" couldn't express "keep the original colour", so a mode
  setting was added alongside it).
- Explain trade-offs, then decide routine things yourself, and say what you decided and why, especially when it
  differs from what the user suggested.

## 2. Research the game (and other mods) as installed

Decompile before writing code against any game or mod API (`tools/decompile.py`, see AGENTS.md "Commands").
The printed paths are Windows paths (use `cygpath -u` in bash); then Grep/Read the relevant parts. If the game
isn't where the defaults expect, the setup section of AGENTS.md says how to point at it.

Read `docs/game-api.md` for where things live and `docs/integrations.md` before touching another mod.

## 3. Scaffold (new mods) or open the mod (existing mods)

New mod: `python tools/new_mod.py <Name> --description "..." --categories tools,client-side` (`--help` lists the
options). It creates `mods/<Name>/` with a slim csproj, `Plugin.cs`, `thunderstore.toml` (the `ai-generated`
category is always included) and `CHANGELOG.md`, and adds it to `ValheimMods.sln`; the repo root supplies the rest.
A mod that isn't ready to be public goes on a local branch (`git switch -c <Name>/wip`).

Existing mod: `git pull`, read the files involved, and check `git status` is clean before starting. Ask before
touching a dirty tree.

The session should be at the repo root. If it isn't, move it there with the `change_directory` tool (the user
approves the folder). Never work in a copy under a scratch folder: those paths are long enough that git fails on
Windows ("Filename too long"), and they get deleted.

## 4. Implement

Read `docs/patterns.md` for the patterns that worked, and the closest existing mod. The "things that cost time
before" in AGENTS.md apply. Also:
- Edit csproj/XML with Edit/Write, not `sed` or shell one-liners.
- Multi-line commit messages: bash heredoc (`git commit -F - <<'EOF'`); PowerShell here-strings didn't work with `-F -`.

## 5. Build, deploy, commit, report

- Build the mod (or everything, after changing a root file); warnings fail the build. Run `dotnet test -c Release`
  and `dotnet format ValheimMods.sln` before committing. The build deploys to the profile.
- Changed logic that can run without the game (see AGENTS.md, Conventions): add or update its tests.
- CI also boots a server with every mod (docs/releasing.md, "Boot test"); a patch that fails there turns the mod
  off, so after pushing, check the Build run before calling the change done.
- Commit with a message explaining why, ending with the attribution line from the system reminder.
- Before a risky rework or when the user asks for a fallback point, make a checkpoint: commit, or if the tree is
  clean, tag the last good commit (`git tag -a <Name>/checkpoint/<what> -m ...`). Some machines have
  `fetch.pruneTags` on, which deletes local-only tags on the next fetch: push the tag (with a go-ahead) or fetch
  with `-c fetch.pruneTags=false`.
- Report plainly, in the shape that has worked:

```markdown
**WhereAmI is built and deployed to your profile** (`mods/WhereAmI`, committed locally, nothing pushed).
`whereami` in the console or `/whereami` in chat prints `Biome: Black Forest | X: 1234, Z: -567 | 1358 m from centre`.

**Checked:** builds with 0 warnings; chat handling and distance maths checked against the decompiled 1.0.17 code.
**Not checked:** anything in game.

**Test in game (riskiest first):**
1. `/whereami` in chat: should print in the chat window (failure: "unknown command").
2. Right after loading in: biome should be a real name, not `$biome_none`.

**Decided for you:** one line of output, no settings or hotkey. Say if you want a hotkey or an on-screen message.
```

## 6. Icon

Follow `docs/icons.md`: a `mods/<Name>/scripts/make_icon.py` on top of `tools/icon_kit.py` (Pillow in a throwaway
venv in the scratchpad is fine). Read the PNG to look at it before committing, and iterate.

## 7. Publish

Read `docs/releasing.md`. Pushing, pull requests, tags and publishes each need the user's go-ahead. Only the
maintainer tags and publishes; for anyone else, stop at a pushed branch, a pull request and (on a fork) a dry run.
Every change reaches `main` through a pull request: fill in each section of `.github/pull_request_template.md`
(the report above maps onto it: Checked, Not checked, In-game test list).
Before tagging, ask the maintainer to run `mods/<Name>/TESTING.md` in game on this build (with any checks this
release adds) and wait for a yes.
Every release needs a `## <version>` entry in `mods/<Name>/CHANGELOG.md`: write it from
`git log <Name>/v<previous>..HEAD -- mods/<Name>` and show it before tagging, since a published version can't be
changed. After tagging, watch the run and verify the GitHub release and the Thunderstore API.

## Keeping this current

When something is learned that the next mod would need, put it where it belongs: game facts in `docs/`, rules any
agent needs in `AGENTS.md`, Claude-specific workflow here.
