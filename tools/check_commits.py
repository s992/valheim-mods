#!/usr/bin/env python3
"""Checks the commits of a pull request against the repo's commit rules (run by build.yml on pull requests).

- One mod per commit: a commit may change several mods only when it also changes code they share (shared/, the root
  build files, tools/), since then the mods' changes are call sites of that one change.
- Humans write the READMEs: a commit with an AI co-author line may not touch a mod's README.md.

    python tools/check_commits.py <base-sha> <head-sha>
"""

import re
import subprocess
import sys

AI_COAUTHOR = re.compile(r"^Co-Authored-By: Claude\b", re.MULTILINE | re.IGNORECASE)
MOD_FILE = re.compile(r"^mods/([^/]+)/")
README = re.compile(r"^mods/[^/]+/README\.md$", re.IGNORECASE)
# Changing any of these makes a commit "shared": its edits to several mods are one change
SHARED = re.compile(r"^(shared/|tools/|Directory\.Build\.(props|targets)$|\.editorconfig$|BannedSymbols\.txt$)")


def git(*args: str) -> str:
    return subprocess.run(["git", *args], check=True, capture_output=True, text=True).stdout


def problems_in(sha: str) -> list[str]:
    message = git("log", "-1", "--format=%B", sha)
    files = [f for f in git("diff-tree", "--no-commit-id", "--name-only", "-r", "--root", sha).splitlines() if f]
    subject = message.splitlines()[0] if message else sha
    problems = []

    mods = sorted({m.group(1) for f in files if (m := MOD_FILE.match(f))})
    if len(mods) > 1 and not any(SHARED.match(f) for f in files):
        problems.append(f"changes {len(mods)} mods ({', '.join(mods)}) without a shared change; split it, one mod per commit")

    if AI_COAUTHOR.search(message):
        problems += [f"edits {f}: READMEs are written by humans" for f in files if README.match(f)]

    return [f"{sha[:9]} {subject}: {p}" for p in problems]


def main() -> None:
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    base, head = sys.argv[1:]
    shas = git("rev-list", "--reverse", f"{base}..{head}").split()
    problems = [p for sha in shas for p in problems_in(sha)]
    if problems:
        print("Commit rules (AGENTS.md, ground rules):")
        for problem in problems:
            print(f"  - {problem}")
        sys.exit(1)
    print(f"{len(shas)} commit(s) follow the commit rules")


if __name__ == "__main__":
    main()
