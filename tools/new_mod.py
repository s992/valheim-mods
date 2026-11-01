#!/usr/bin/env python3
"""Scaffold a new Valheim BepInEx mod in the valheim-mods monorepo.

Creates mods/<Name>/ with the csproj, Plugin.cs, thunderstore.toml, CHANGELOG.md and TESTING.md from tools/mod-template/,
and adds the project to ValheimMods.sln. Everything shared (build props, packages, game
references, CI and release workflows) already lives at the repo root. Nothing is committed or pushed.

Usage:
  python tools/new_mod.py LootFilter --description "Hide junk drops" --categories tools,client-side
"""
import argparse
import re
import subprocess
import sys
from pathlib import Path
from xml.sax.saxutils import escape as xml_escape

REPO = Path(__file__).resolve().parent.parent
TEMPLATE = REPO / "tools" / "mod-template"

# Valheim community category slugs on Thunderstore (checked 2026-10). "ai-generated" is always added.
KNOWN_CATEGORIES = {
    "ai-generated", "mods", "modpacks", "tweaks", "misc", "libraries", "tools", "gear", "enemies",
    "crafting", "skins", "language", "audio", "server-side", "client-side", "utility", "pvp",
    "vehicles", "transportation", "building", "npcs", "world-generation",
    "mistlands-update", "hildirs-request-update", "ashlands-update", "bog-witch-update", "deep-north-update",
}


def run(cmd, cwd):
    print("  $ " + " ".join(cmd))
    subprocess.run(cmd, cwd=cwd, check=True, stdout=subprocess.DEVNULL)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("name", help="PascalCase mod name, e.g. LootFilter (letters and digits)")
    parser.add_argument("--description", required=True, help="One sentence, max 250 characters (Thunderstore limit)")
    parser.add_argument("--categories", default="tools,client-side",
                        help="Comma-separated Thunderstore category slugs; ai-generated is always included")
    parser.add_argument("--repo", default=str(REPO), help="The repo checkout (default: the one this script is in)")
    parser.add_argument("--allow-unknown-categories", action="store_true", help="Accept category slugs not in the known list")
    parser.add_argument("--no-sln", action="store_true", help="Skip adding the project to ValheimMods.sln (no dotnet available)")
    args = parser.parse_args()

    name = args.name
    # Letters and digits only: release.yml reads the mod name from a <Name>/v<version> tag and checks it this way
    if not re.fullmatch(r"[A-Za-z][A-Za-z0-9]{0,127}", name):
        sys.exit(f"Invalid name '{name}': use letters and digits only (PascalCase)")
    if len(args.description) > 250:
        sys.exit(f"Description is {len(args.description)} characters; Thunderstore's limit is 250")

    categories = ["ai-generated"] + [c.strip() for c in args.categories.split(",") if c.strip() and c.strip() != "ai-generated"]
    unknown = [c for c in categories if c not in KNOWN_CATEGORIES]
    if unknown and not args.allow_unknown_categories:
        sys.exit(f"Unknown Thunderstore categories {unknown} (publishing would fail). Known: {sorted(KNOWN_CATEGORIES)}. "
                 "If Thunderstore added new ones (https://thunderstore.io/api/experimental/community/valheim/category/), "
                 "pass --allow-unknown-categories and update KNOWN_CATEGORIES.")

    # resolve() also tidies mixed separators from bash ($HOME/foo -> C:\...\foo)
    repo = Path(args.repo).expanduser().resolve()
    if not (repo / "Directory.Build.props").exists() or not (repo / "mods").is_dir():
        sys.exit(f"{repo} doesn't look like the valheim-mods checkout (no Directory.Build.props or mods/)")
    dest = repo / "mods" / name
    if dest.exists():
        sys.exit(f"{dest} already exists; not overwriting")

    replacements = {
        "__MODNAME__": name,
        "__CATEGORIES__": ", ".join(f'"{c}"' for c in categories),
    }

    print(f"Creating {dest}")
    dest.mkdir(parents=True)
    for src in TEMPLATE.iterdir():
        target = dest / src.name.replace("__MODNAME__", name)
        text = src.read_text(encoding="utf-8")
        for key, value in replacements.items():
            text = text.replace(key, value)
        # The description lands in XML (csproj) and TOML (thunderstore.toml), which escape differently
        if src.suffix == ".csproj":
            text = text.replace("__DESCRIPTION__", xml_escape(args.description))
        else:
            text = text.replace("__DESCRIPTION__", args.description.replace("\\", "\\\\").replace('"', '\\"'))
        # Write the platform's line endings (CRLF on Windows) so git's autocrlf doesn't warn about every
        # file; the repo's .gitattributes normalizes them to LF either way
        text = text.replace("\r\n", "\n")
        with open(target, "w", encoding="utf-8", newline=None) as f:
            f.write(text)

    if not args.no_sln:
        run(["dotnet", "sln", "ValheimMods.sln", "add", "--in-root", f"mods/{name}/{name}.csproj"], repo)

    print(f"""
Done: {dest}
Next:
  - add an icon.png (256x256) in mods/{name}/, drawn by mods/{name}/scripts/make_icon.py (see docs/icons.md)
  - dotnet build mods/{name}/{name}.csproj -c Release   (deploys to the r2modman profile unless CI=true)
  - fill in mods/{name}/TESTING.md with the in-game checks, riskiest first
  - commit; push only when the user says so
  - note player-visible changes under '## Unreleased' in mods/{name}/CHANGELOG.md; a release renames it to '## <version>'
""")


if __name__ == "__main__":
    main()
