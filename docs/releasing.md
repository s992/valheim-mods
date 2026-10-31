# CI and releasing

How a commit becomes a published Thunderstore package. CI (building) applies to everyone; pushing to `main`,
tagging and publishing are for the maintainer (s992), who owns the `s992` Thunderstore team. Contributors open a
pull request instead and can stop at the dry run. The repo has one `build.yml` and one `release.yml` at the root;
each mod has its own `mods/<Name>/thunderstore.toml`.

## Contents
- Tools
- Pushing a new mod
- How CI builds without the game
- Release dry run
- Releasing a version
- Reviewing the README
- Gotchas we hit
- Changelog

## Tools

- `gh` (GitHub CLI) to read CI runs: `gh run list`, `gh run watch <id> --exit-status`, `gh run view <id> --log`,
  `gh release view`, `gh api`. Dispatching workflows on `s992/valheim-mods` needs write access; on a fork, run them
  there.
- Thunderstore team: `s992`. The repo has one `THUNDERSTORE_TOKEN` secret (a team service-account token) that the
  maintainer manages; it is only read by the publish job.
- `tcli` (Thunderstore CLI) 0.2.4 targets .NET 6/7; set `DOTNET_ROLL_FORWARD=Major` to run it on .NET 8. To try
  packaging locally without a global install: `dotnet tool install --tool-path <temp>/tools tcli --version 0.2.4`,
  then run `tcli build --package-version <v>` inside `mods/<Name>/` after a Release build.

## Pushing a new mod

The repo is public: pushing `main` publishes the mod's source, so agents ask first. A mod that isn't ready to show
stays on a local branch (`git switch -c <Name>/wip`) until its author says so. Contributors push a branch to their
fork and open a pull request; the maintainer merges.
1. `git pull`, then push `main`.
2. Watch the Build run (`gh run list --branch main --limit 1`, then `gh run watch`). It builds every mod; with the
   server cached it takes ~30 s, ~2 min when the game updated.
3. Do the release dry run below.

## How CI builds without the game

`build.yml` downloads the **Valheim Dedicated Server** (Steam app 896660, anonymous login) with SteamCMD and builds
`ValheimMods.sln` against its `valheim_server_Data/Managed` assemblies (same game code as the client). Only the
`Managed` folder is cached, keyed by the server's Steam build id from `api.steamcmd.net`, so it re-downloads only when
the game updates. `Directory.Build.props` falls back to `valheim_server_Data` automatically; CI passes
`-p:ValheimManagedDir=...`. The `plugins` artifact holds every mod's DLL at `<Name>/bin/Release/netstandard2.1/`.
Game DLLs are never committed (copyright). The stale `ValheimGameLibs` NuGet package (0.221.x) is pre-1.0; don't use it.

`release.yml` (on `<Name>/v*` tags, or run by hand with a mod name for a dry run): reuses build.yml, takes the mod
from the tag (or the `mod` input), checks the tag matches `<Version>` in `mods/<Name>/<Name>.csproj`, checks
`README.md` and the changelog entry exist, runs `tcli build` in `mods/<Name>/`, extracts that version's
`CHANGELOG.md` section as release notes (with an AI-written note), uploads both as the `thunderstore-package`
artifact, then (tags only) two separate jobs: create the GitHub release titled `<Name> <version>` (idempotent) and
`tcli publish` with the token. Separate jobs mean a failure in one can be re-run alone.

## Release dry run

```bash
gh workflow run release.yml -R s992/valheim-mods -f mod=<Name>
```
Watch it; github-release and thunderstore jobs are skipped. Then `gh run download <id> -n thunderstore-package`
and check the zip has `icon.png`, `README.md`, `CHANGELOG.md`, `manifest.json` and `plugins/<Name>.dll`, the
manifest's namespace/name/version/dependency/website_url, and `release-notes.md`. The dry run needs the README and a
`## <version>` changelog entry, so write the entry first (see "Changelog").

A new mod has no README until its author writes one. To dry-run before that, put a placeholder `README.md` on a throwaway
branch, push the branch, run `gh workflow run release.yml --ref <branch> -f mod=<Name>`, then delete the branch locally
and on the remote. `main` must not get the placeholder README.

## Releasing a version

Publishing is public and a Thunderstore version can't be replaced, only superseded, so confirm with the user first.
1. Bump `<Version>` in `mods/<Name>/<Name>.csproj` (semver; a behaviour-free metadata change is a patch).
2. Add a `## <version>` entry at the top of `mods/<Name>/CHANGELOG.md` (below the note) from
   `git log <Name>/v<previous>..HEAD -- mods/<Name>`, describing what players notice. Changes at the repo root
   (shared props, workflows) matter only if players would notice them. Show the entry to the user before tagging.
3. Commit, push, and wait for `main`'s Build run to be green.
4. `git tag -a <Name>/v<version> -m "<Name> v<version>"` and `git push origin <Name>/v<version>`.
5. Watch the Release run; all four jobs (build, package, github-release, thunderstore) should pass.
6. Verify: `gh release view <Name>/v<version>` lists the zip,
   `curl -s https://thunderstore.io/api/experimental/package/s992/<Name>/` shows the new `version_number` and the
   monorepo `website_url`, and `.../s992/<Name>/<version>/changelog/` returns the changelog.
   New packages went live immediately (no moderation wait) so far.

## Reviewing the README

The mod's human author writes the README (s992's are lowercase, with an AI-usage disclaimer). Agents don't write
it, but review it against the code when asked:
- every default in a pasted settings block matches `Settings.cs` (names, values, ranges, descriptions). The `#` lines
  (`Setting type`, `Default value`, `Acceptable values`, and for `[Flags]` enums "Multiple values can be set at the
  same time by separating them with , (e.g. Debug, Warning)") are written by BepInEx's `ConfigEntryBase`, not the mod;
  the `Debug, Warning` example is hard-coded and can be swapped for a real one in the README
- key names and behaviour descriptions match what the code does (e.g. when snapshots are taken)
- paths: `BepInEx/config/s992.<Name>.cfg` (capital I in BepInEx)
- image links resolve (curl the raw URL). Images in the monorepo are
  `https://raw.githubusercontent.com/s992/valheim-mods/main/mods/<Name>/<file>`. StabilityView's README still links
  its screenshot from the archived `s992/ValheimStabilityView`, which keeps working; suggest the new URL when it's next edited
- Markdown problems (unclosed backticks)
Point out mismatches and suggest wording; if the README describes better behaviour than the code has, offer to
change the code instead.

## Gotchas we hit

- SteamCMD's first `app_update` on a fresh install fails with "Missing configuration"; the second succeeds.
  build.yml retries up to 3 times and checks for `assembly_valheim.dll` instead of trusting exit codes.
- GitHub Actions caches are per branch (branches can read `main`'s, not each other's): the first run on a new branch
  may download the server again.
- Runners are pinned to `ubuntu-24.04` because SteamCMD needs 32-bit libraries (`lib32gcc-s1`) that an OS bump could break.
- Actions are pinned to commit SHAs with the version in a comment (`uses: actions/checkout@<sha> # v7.0.1`), since
  a moved tag could otherwise run new code next to the Thunderstore token. Dependabot proposes bumps monthly; to
  bump by hand, take the SHA from `git ls-remote --tags https://github.com/actions/<name>.git` and update every use.
  As of Oct 2026: checkout v7.0.1, setup-dotnet v6.0.0, cache v6.1.0, upload-artifact v7.0.2, download-artifact v8.0.2.
- `thunderstore.toml` categories and `websiteUrl` are sent with every publish and overwrite edits made on the website.
  Keep the toml in sync with any site edits. Category slugs: `https://thunderstore.io/api/experimental/community/valheim/category/`.
- Mods published before the monorepo (Oct 2026) still show their old `s992/Valheim<Name>` link on Thunderstore until
  their next release sends the new `websiteUrl`. The old repos are archived and still readable.
- Dependency string for BepInEx: `denikson-BepInExPack_Valheim` (check the latest version via
  `https://thunderstore.io/api/experimental/package/denikson/BepInExPack_Valheim/`).
- Mod names are letters and digits only: release.yml reads the name from the part of the tag before `/`.
- Pushing old tags that point at commits without a root `.github/workflows/release.yml` triggers nothing; that is how
  the imported `<Name>/v*` tags were pushed safely.
- In PowerShell, `git commit -F -` with a here-string didn't work; use bash heredocs for multi-line commit messages.

## Changelog

Thunderstore shows `CHANGELOG.md` from the package root as a Changelog tab (API:
`/api/experimental/package/s992/<Name>/<version>/changelog/`). tcli 0.2.4 has no changelog setting, so the mod
template's `thunderstore.toml` copies the file in with `[[build.copy]]`, and `release.yml`'s "Check changelog" step
fails a release (or dry run) without a `## <version>` heading. New mods get both from `new_mod.py`; the template's
`CHANGELOG.md` holds only the title and the AI note. The same section becomes the GitHub release notes.

Writing entries (s992's rule, Oct 2026): the AI agent writes them, newest first, in neutral sentence-case prose
about what players notice, under the note "*This changelog is written by Claude, an AI assistant, from the
project's commit history.*" (name the agent that actually wrote it). Never imitate the author's README voice: the
repo doesn't hide AI-generated content.

Older mods (MarkerCommand, StabilityView, LootFilter, CraftFromStorage) were scaffolded before this. On their next
release, copy `CHANGELOG.md` and the changelog `[[build.copy]]` block from the mod template (or from
`mods/EverythingsANail`), and backfill entries for earlier versions from the `<Name>/v*` tags. Published versions
can't gain a changelog afterwards; it shows from the next version, whose changelog can list the history.
