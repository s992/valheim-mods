# valheim-mods

s992's client-side Valheim (BepInEx) mods, one Thunderstore package per folder under `mods/`. The `/valheim-mod`
skill has the workflow, conventions and game knowledge; this file covers how the repo is laid out.

## Layout

- `mods/<Name>/`: one mod. `<Name>.csproj` (only `<Version>`, `<Description>` and extra game references),
  sources, `thunderstore.toml`, `README.md` (written by s992), `CHANGELOG.md` (written by Claude), `icon.png`.
- `Directory.Build.props`: everything shared: target framework, BepInEx packages, the common game references
  (`assembly_valheim` publicized), plugin GUID `s992.<Name>` from the folder name, game and profile paths.
- `Directory.Build.targets`: copies each build into the r2modman profile's `plugins/<Name>/` (skipped in CI).
- `ValheimMods.sln`: every mod; add new ones with `dotnet sln ValheimMods.sln add --in-root mods/<Name>/<Name>.csproj`.

## Commands

- Build and deploy everything: `dotnet build -c Release` (must finish with 0 warnings).
- One mod: `dotnet build mods/<Name>/<Name>.csproj -c Release`.
- Release dry run: `gh workflow run release.yml -f mod=<Name>`.
- Release: bump `<Version>`, add the `## <version>` changelog entry, push, then tag `<Name>/v<version>`.

## History

Each mod was imported from its own repo (`s992/Valheim<Name>`, archived) with its history; tags from those repos
carry a `<Name>/` prefix. GitHub releases before the move stay in the old repos.
