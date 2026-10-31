#!/usr/bin/env python3
"""Boot a Valheim dedicated server with BepInEx and the built mods, then check the log.

What it catches: patches that don't apply (a game update moved a method), transpilers whose counts changed, plugins
that throw in Awake/Start, missing references, and broken integrations with the mods given by --with. What it can't:
anything that needs a local player or the UI; those stay on each mod's in-game test list.

Run by build.yml on Linux. Usage:
    python tools/boot_test.py --server valheim-server --mods mods --bepinex 5.4.2360
    python tools/boot_test.py ... --with MSchmoecker-MultiUserChest JereKuusela-Server_devcommands
"""

import argparse
import io
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.request
import zipfile
from pathlib import Path

THUNDERSTORE = "https://thunderstore.io"
BEPINEX_PACK = "denikson-BepInExPack_Valheim"

# Lines of BepInEx's log that fail the test
FAILURE = re.compile(r"^\[(Error|Fatal)\s*:")
# The game's own errors on a headless server (no GPU, no video, no PlayFab party library). Only Unity's log source is
# matched, so a mod's own "[Error : <Mod>]" line can never be hidden by these.
ALLOWED = [re.compile(r"^\[Error\s*: Unity Log\] " + p) for p in [
    r"AsyncResourceUpload failed\.$",
    r"This custom render path shader needs to have at least 1 passes\.$",
    r"Could not find material Hidden/Video(Decode|Composite)\.",
    r"Could not find video decode shader pass \w+ in shader <not found>$",
    r"[\d/: ]+: Failed to play intro cinematic$",
    r"DllNotFoundException: libParty\.so ",
]]

CHAINLOADER_DONE = "Chainloader startup complete"
# After the world is loaded, Game.Start and every plugin's Start have run
SERVER_READY = re.compile(r"Game server connected|Opened Steam server|Load world:.*\n.*Zonesystem")


def fetch(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": "s992-valheim-mods-boot-test"})
    with urllib.request.urlopen(request, timeout=120) as response:
        return response.read()


def package_info(full_name: str) -> dict:
    namespace, name = full_name.split("-", 1)
    return json.loads(fetch(f"{THUNDERSTORE}/api/experimental/package/{namespace}/{name}/"))


def resolve(names: list[str]) -> dict[str, dict]:
    """Latest version of each package and its dependencies (as a mod manager installs them), minus BepInExPack."""
    resolved: dict[str, dict] = {}
    pending = list(names)
    while pending:
        full_name = pending.pop()
        # Dependencies are "Namespace-Name-1.2.3"; the latest version is used, like r2modman's default
        full_name = re.sub(r"-\d+(\.\d+)*$", "", full_name)
        if full_name == BEPINEX_PACK or full_name in resolved:
            continue
        info = package_info(full_name)
        resolved[full_name] = info["latest"]
        pending.extend(info["latest"]["dependencies"])
    return resolved


def install_bepinex(server: Path, version: str) -> None:
    namespace, name = BEPINEX_PACK.split("-", 1)
    data = fetch(f"{THUNDERSTORE}/package/download/{namespace}/{name}/{version}/")
    prefix = "BepInExPack_Valheim/"
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for entry in archive.infolist():
            if entry.is_dir() or not entry.filename.startswith(prefix):
                continue
            target = server / entry.filename[len(prefix):]
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(archive.read(entry))
    print(f"Installed BepInExPack_Valheim {version}")


def install_package(server: Path, full_name: str, latest: dict) -> None:
    """Lays a Thunderstore package out the way r2modman does: plugins/, patchers/ and config/ go to BepInEx/."""
    data = fetch(latest["download_url"])
    bepinex = server / "BepInEx"
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        names = [e.filename for e in archive.infolist() if not e.is_dir()]
        has_layout = any(n.split("/", 1)[0].lower() in ("plugins", "patchers", "config") for n in names if "/" in n)
        for entry in names:
            parts = entry.split("/", 1)
            top = parts[0].lower()
            if has_layout and len(parts) == 2 and top in ("plugins", "patchers"):
                target = bepinex / top / full_name / parts[1]
            elif has_layout and len(parts) == 2 and top == "config":
                target = bepinex / "config" / parts[1]
            elif not has_layout and entry.lower().endswith(".dll"):
                target = bepinex / "plugins" / full_name / entry
            else:
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(archive.read(entry))
    print(f"Installed {full_name} {latest['version_number']}")


def install_mods(server: Path, mods: Path) -> dict[str, str]:
    """Copies each built mod into BepInEx/plugins/<Name>/; returns name -> version from its csproj."""
    installed = {}
    for project in sorted(mods.glob("*/*.csproj")):
        name = project.stem
        dll = project.parent / "bin" / "Release" / "netstandard2.1" / f"{name}.dll"
        if not dll.exists():
            sys.exit(f"{dll} is missing; build the solution in Release first")
        version = re.search(r"<Version>([^<]+)</Version>", project.read_text(encoding="utf-8")).group(1)
        target = server / "BepInEx" / "plugins" / name
        target.mkdir(parents=True, exist_ok=True)
        shutil.copy2(dll, target)
        installed[name] = version
    print("Installed " + ", ".join(f"{n} {v}" for n, v in installed.items()))
    return installed


def run_server(server: Path, timeout: float) -> str:
    """Starts the server like start_server_bepinex.sh, waits until the world is up, stops it, returns the log."""
    log_file = server / "BepInEx" / "LogOutput.log"
    log_file.unlink(missing_ok=True)
    env = dict(os.environ)
    env.update({
        "DOORSTOP_ENABLED": "1",
        "DOORSTOP_TARGET_ASSEMBLY": "./BepInEx/core/BepInEx.Preloader.dll",
        "LD_LIBRARY_PATH": f"./doorstop_libs:./linux64:{env.get('LD_LIBRARY_PATH', '')}",
        "LD_PRELOAD": f"libdoorstop_x64.so:{env.get('LD_PRELOAD', '')}",
        "SteamAppId": "892970",
    })
    save_dir = tempfile.mkdtemp(prefix="valheim-boot-test-")
    (server / "valheim_server.x86_64").chmod(0o755)
    command = ["./valheim_server.x86_64", "-batchmode", "-nographics", "-name", "s992 boot test", "-port", "2456",
               "-world", "BootTest", "-password", "boottest-not-public", "-public", "0", "-savedir", save_dir]
    stdout_path = server / "boot-test-stdout.log"
    with open(stdout_path, "wb") as stdout:
        process = subprocess.Popen(command, cwd=server, env=env, stdout=stdout, stderr=subprocess.STDOUT)
        started = time.monotonic()
        ready_at = None
        try:
            while time.monotonic() - started < timeout:
                time.sleep(2)
                if process.poll() is not None:
                    print(f"Server exited early with code {process.returncode}")
                    break
                text = log_file.read_text(encoding="utf-8", errors="replace") if log_file.exists() else ""
                ready = SERVER_READY.search(text)
                if ready_at is None and CHAINLOADER_DONE in text and ready:
                    # A little longer, for anything plugins do on their first frames in the world
                    ready_at = time.monotonic()
                    print(f"Server ready after {ready_at - started:.0f} s ({ready.group(0)!r})")
                if ready_at is not None and time.monotonic() - ready_at > 10:
                    break
            else:
                print(f"Server wasn't ready after {timeout:.0f} s")
        finally:
            process.terminate()
            try:
                process.wait(timeout=30)
            except subprocess.TimeoutExpired:
                process.kill()
    elapsed = time.monotonic() - started
    print(f"Server ran for {elapsed:.0f} s")
    if not log_file.exists():
        print(stdout_path.read_text(encoding="utf-8", errors="replace")[-5000:])
        sys.exit("BepInEx wrote no LogOutput.log: it didn't start")
    return log_file.read_text(encoding="utf-8", errors="replace")


def check(log: str, ours: dict[str, str]) -> list[str]:
    problems = []
    if CHAINLOADER_DONE not in log:
        problems.append("BepInEx's chainloader didn't finish")
    if not SERVER_READY.search(log):
        problems.append("the server never finished loading the world")
    for name, version in ours.items():
        if not re.search(rf"^\[Info\s*:\s*{name}\] {name} {re.escape(version)} loaded", log, re.M):
            problems.append(f"{name} {version} didn't log that it loaded")
    for line in log.splitlines():
        if FAILURE.match(line) and not any(p.search(line) for p in ALLOWED):
            problems.append(f"log: {line.strip()}")
    return problems


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--server", type=Path, required=True, help="Valheim dedicated server install (modified)")
    parser.add_argument("--mods", type=Path, required=True, help="the repo's mods/ folder, built in Release")
    parser.add_argument("--bepinex", required=True, help="BepInExPack_Valheim version")
    parser.add_argument("--with", dest="others", nargs="*", default=[],
                        help="Thunderstore packages (Namespace-Name) to install too, latest versions")
    parser.add_argument("--timeout", type=float, default=300)
    args = parser.parse_args()

    server = args.server.resolve()
    if (server / "BepInEx").exists():
        shutil.rmtree(server / "BepInEx")
    install_bepinex(server, args.bepinex)
    others = resolve(args.others)
    for full_name, latest in others.items():
        install_package(server, full_name, latest)
    ours = install_mods(server, args.mods.resolve())

    log = run_server(server, args.timeout)
    problems = check(log, ours)

    print("\n--- BepInEx log, without the game's Info lines ---")
    for line in log.splitlines():
        if not line.startswith("[Info   : Unity Log]"):
            print(line)
    if problems:
        print("\nBoot test FAILED:")
        for problem in problems:
            print(f"  - {problem}")
        sys.exit(1)
    print(f"\nBoot test passed: {', '.join(ours)} loaded" + (f" with {', '.join(others)}" if others else ""))


if __name__ == "__main__":
    main()
