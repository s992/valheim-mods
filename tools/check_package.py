#!/usr/bin/env python3
"""Checks a built Thunderstore package before it's released (run by release.yml after `tcli build`).

A published Thunderstore version can't be replaced, so this checks what used to be checked by hand: the files the
package must contain, the manifest against the mod's csproj, and that the DLL inside is the one CI just built.

    python tools/check_package.py --mod LootFilter --version 1.1.0 --zip mods/LootFilter/build/s992-LootFilter-1.1.0.zip
"""

import argparse
import hashlib
import json
import re
import sys
import zipfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
WEBSITE = "https://github.com/s992/valheim-mods/tree/main/mods/{mod}"
BEPINEX_DEPENDENCY = re.compile(r"^denikson-BepInExPack_Valheim-\d+\.\d+\.\d+$")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--mod", required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--zip", type=Path, required=True)
    args = parser.parse_args()

    mod_dir = REPO / "mods" / args.mod
    problems = []
    with zipfile.ZipFile(args.zip) as archive:
        names = set(archive.namelist())
        required = ["manifest.json", "icon.png", "README.md", f"plugins/{args.mod}.dll"]
        if (mod_dir / "CHANGELOG.md").exists():
            required.append("CHANGELOG.md")
        problems += [f"missing {name}" for name in required if name not in names]
        extra = [n for n in names if n.lower().endswith(".dll") and n != f"plugins/{args.mod}.dll"]
        problems += [f"unexpected DLL {name} (game, Unity and BepInEx DLLs must never ship)" for name in extra]

        if "manifest.json" in names:
            manifest = json.loads(archive.read("manifest.json").decode("utf-8-sig"))
            expect = {"name": args.mod, "version_number": args.version, "website_url": WEBSITE.format(mod=args.mod)}
            for key, value in expect.items():
                if manifest.get(key) != value:
                    problems.append(f"manifest {key} is {manifest.get(key)!r}, expected {value!r}")
            description = manifest.get("description", "")
            if not 0 < len(description) <= 250:
                problems.append(f"manifest description is {len(description)} characters (1 to 250)")
            dependencies = manifest.get("dependencies", [])
            if not any(BEPINEX_DEPENDENCY.match(d) for d in dependencies):
                problems.append(f"no BepInExPack_Valheim dependency in {dependencies}")

        built = mod_dir / "bin" / "Release" / "netstandard2.1" / f"{args.mod}.dll"
        packaged = f"plugins/{args.mod}.dll"
        if packaged in names and built.exists():
            if hashlib.sha256(archive.read(packaged)).digest() != hashlib.sha256(built.read_bytes()).digest():
                problems.append(f"{packaged} isn't the DLL this run built")

        if "README.md" in names and not archive.read("README.md").strip():
            problems.append("README.md is empty")

    if problems:
        print(f"{args.zip.name} FAILED:")
        for problem in problems:
            print(f"  - {problem}")
        sys.exit(1)
    print(f"{args.zip.name}: contents, manifest and DLL check out")


if __name__ == "__main__":
    main()
