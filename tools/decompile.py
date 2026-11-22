#!/usr/bin/env python3
"""Decompile Valheim (or another mod's) assemblies so code is written against the installed version.

Output is cached per assembly build (path + size + mtime), so repeat calls are instant and a game
update automatically gets a fresh decompile.

Examples:
  python decompile.py --type Terminal --type Minimap        # specific classes from assembly_valheim
  python decompile.py --project                              # whole assembly_valheim as a .cs tree (grep it)
  python decompile.py --assembly assembly_utils --type ZInput
  python decompile.py --plugin EpicLoot --project            # a mod DLL from the r2modman profile
  python decompile.py --list                                  # list type names in the assembly

Paths come from the same settings the build uses: the ValheimDir and PluginsDir environment variables, else
<repo>/Directory.Build.user.props, else the defaults below (Steam's default folder, r2modman's Default profile).
--valheim and --profile override them.

Prints the output path(s) on the last lines.
"""
import argparse
import hashlib
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
DEFAULT_VALHEIM = Path(r"C:\Program Files (x86)\Steam\steamapps\common\Valheim")
DEFAULT_PLUGINS = Path(os.environ.get("APPDATA", "")) / "r2modmanPlus-local" / "Valheim" / "profiles" / "Default" / "BepInEx" / "plugins"
# The newest ilspycmd needs a newer .NET than 8; this version runs on the .NET 8 SDK
ILSPY_FALLBACK_VERSION = "9.1.0.7988"


def find_ilspy():
    exe = shutil.which("ilspycmd")
    if exe:
        return exe
    candidate = Path.home() / ".dotnet" / "tools" / ("ilspycmd.exe" if os.name == "nt" else "ilspycmd")
    if candidate.exists():
        return str(candidate)

    print("ilspycmd not found; installing it as a global dotnet tool", file=sys.stderr)
    if subprocess.run(["dotnet", "tool", "install", "-g", "ilspycmd"]).returncode != 0:
        subprocess.run(["dotnet", "tool", "install", "-g", "ilspycmd", "--version", ILSPY_FALLBACK_VERSION], check=True)
    return str(candidate)


def resolve_assembly(args):
    if args.plugin:
        plugins = Path(args.profile) / "plugins"
        matches = [p for p in plugins.rglob("*.dll") if args.plugin.lower() in p.name.lower()]
        if not matches:
            sys.exit(f"No DLL matching '{args.plugin}' under {plugins}")
        if len(matches) > 1:
            print("Several matches, using the first: " + ", ".join(str(m) for m in matches), file=sys.stderr)
        return matches[0]

    asm = args.assembly
    if os.path.isfile(asm):
        return Path(asm)
    managed = Path(args.valheim) / "valheim_Data" / "Managed"
    if not managed.exists():
        managed = Path(args.valheim) / "valheim_server_Data" / "Managed"
    path = managed / (asm if asm.endswith(".dll") else asm + ".dll")
    if not path.exists():
        sys.exit(f"{path} not found (pass --valheim if the game is installed elsewhere)")
    return path


def build_setting(name, default):
    """A path the build also uses: environment variable, then Directory.Build.user.props, then the default."""
    if os.environ.get(name):
        return Path(os.environ[name])
    user_props = REPO / "Directory.Build.user.props"
    if user_props.exists():
        match = re.search(rf"<{name}[^>]*>([^<]+)</{name}>", user_props.read_text(encoding="utf-8"))
        if match:
            return Path(match.group(1).strip())
    return default


def cache_dir(assembly):
    stat = assembly.stat()
    key = hashlib.sha1(f"{assembly.resolve()}|{stat.st_size}|{stat.st_mtime}".encode()).hexdigest()[:10]
    return Path(tempfile.gettempdir()) / "valheim-decompile" / f"{assembly.stem}-{key}"


def run_ilspy(args):
    """Run ilspycmd, keeping its stderr for error messages.

    Decompiled code always goes to files (-o), never through stdout: on Windows ilspycmd writes redirected
    stdout in the console's OEM code page (Jotunn's "Jötunn" comes out as byte 0x94), which isn't UTF-8.
    """
    return subprocess.run(args, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, text=True,
                          encoding="utf-8", errors="replace")


def first_error_line(result):
    lines = [line for line in (result.stderr or "").splitlines()
             if line.strip() and "latest version" not in line.lower() and "please update" not in line.lower()]
    return lines[0].strip() if lines else f"ilspycmd exited with code {result.returncode}"


def has_sources(folder):
    return folder.is_dir() and any(folder.rglob("*.cs"))


def decompile_project(ilspy, assembly, project):
    # Decompile beside the cache entry and move it in only when it holds code, so a failed or interrupted run
    # never leaves an empty tree that later calls would take as cached
    staging = project.with_name(project.name + ".partial")
    shutil.rmtree(staging, ignore_errors=True)
    result = run_ilspy([ilspy, "-p", "-o", str(staging), str(assembly)])
    if result.returncode != 0 or not has_sources(staging):
        shutil.rmtree(staging, ignore_errors=True)
        sys.exit(f"Couldn't decompile {assembly.name} as a project (no .cs files written): {first_error_line(result)}")
    shutil.rmtree(project, ignore_errors=True)
    staging.rename(project)


def decompile_type(ilspy, assembly, type_name, target):
    with tempfile.TemporaryDirectory(dir=target.parent) as staging:
        result = run_ilspy([ilspy, "-t", type_name, "-o", staging, str(assembly)])
        written = [p for p in Path(staging).glob("*.cs") if p.stat().st_size > 0]
        if result.returncode != 0 or not written:
            # Usually "Could not find type definition": a typo, a missing namespace, or another assembly
            sys.exit(f"Couldn't decompile {type_name}: {first_error_line(result)}")
        shutil.move(str(written[0]), target)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--assembly", default="assembly_valheim", help="Assembly name in the game's Managed folder, or a path")
    parser.add_argument("--plugin", help="Substring of a mod DLL name in the r2modman profile's plugins folder")
    parser.add_argument("--type", action="append", default=[], help="Full type name to decompile (repeatable)")
    parser.add_argument("--project", action="store_true", help="Decompile the whole assembly to a folder of .cs files")
    parser.add_argument("--list", action="store_true", help="List the classes in the assembly")
    parser.add_argument("--valheim", default=str(build_setting("ValheimDir", DEFAULT_VALHEIM)), help="Game install folder")
    parser.add_argument("--profile", default=str(build_setting("PluginsDir", DEFAULT_PLUGINS).parent),
                        help="BepInEx folder of the mod profile (the parent of its plugins folder)")
    args = parser.parse_args()

    if not (args.type or args.project or args.list):
        parser.error("pass --type, --project or --list")

    ilspy = find_ilspy()
    assembly = resolve_assembly(args)
    out = cache_dir(assembly)
    out.mkdir(parents=True, exist_ok=True)
    print(f"Assembly: {assembly}", file=sys.stderr)

    if args.list:
        subprocess.run([ilspy, "-l", "c", str(assembly)], check=True)

    if args.project:
        project = out / "project"
        if not has_sources(project):
            decompile_project(ilspy, assembly, project)
        print(project)

    for type_name in args.type:
        target = out / f"{type_name}.cs"
        if not target.exists():
            decompile_type(ilspy, assembly, type_name, target)
        print(target)


if __name__ == "__main__":
    main()
