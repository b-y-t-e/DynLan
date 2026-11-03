#!/usr/bin/env python3
"""
Publish DynLan to NuGet.org.

The "DynLan" package ships DynLan 2 (DynLan2/) from 2.0.0 on; --engine 1 publishes a 1.x maintenance
release of the old engine (DynLan/).

Usage:
    python publish.py                     # DynLan 2: test, bump the patch, build, pack, push
    python publish.py --engine 1          # same for the old engine (1.x line)
    python publish.py --dry-run           # everything except the push; leaves the version alone
    python publish.py --set-version 1.3.0 # for a release that is not a patch bump
    python publish.py --no-bump           # use the version already in DynLan.csproj
    python publish.py --skip-tests        # for a re-push after a failed upload, not for a release

Requires:
    - .NET SDK on PATH (dotnet)
    - Deploy/.env with NUGET_API_KEY=<key>  (never committed — see .gitignore)
"""

import argparse
import os
import re
import subprocess
import sys
from pathlib import Path

# ── Paths ─────────────────────────────────────────────────────────────────────

SCRIPT_DIR = Path(__file__).parent
REPO_ROOT  = SCRIPT_DIR.parent
ENGINES = {
    # engine: (project, test project, extra test runs)
    "1": (REPO_ROOT / "DynLan" / "DynLan.csproj", REPO_ROOT / "DynLanTests" / "DynLanTests.csproj", []),
    "2": (REPO_ROOT / "DynLan2" / "DynLan2.csproj", REPO_ROOT / "DynLan2.Tests" / "DynLan2.Tests.csproj",
          [["-p:EngineTarget=netstandard2.1"]]),  # also test the netstandard2.1 build
}
CSPROJ     = ENGINES["2"][0]
TESTS_PROJ = ENGINES["2"][1]
ARTIFACTS  = REPO_ROOT / "artifacts"
ENV_FILE   = SCRIPT_DIR / ".env"

PACKAGE_ID = "DynLan"
VERSION_RE = re.compile(r"<Version>(\d+)\.(\d+)\.(\d+)</Version>")

# ── Helpers ───────────────────────────────────────────────────────────────────

def load_env(path: Path) -> dict:
    env = {}
    if not path.exists():
        return env
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        env[key.strip()] = value.strip()
    return env


def run(cmd: list, **kwargs):
    printable = [("***" if c.startswith("oy2") else str(c)) for c in map(str, cmd)]
    print(f"\n>>> {' '.join(printable)}")
    result = subprocess.run(cmd, **kwargs)
    if result.returncode != 0:
        sys.exit(result.returncode)


def read_version() -> tuple:
    m = VERSION_RE.search(CSPROJ.read_text(encoding="utf-8"))
    if not m:
        print(f"ERROR: <Version> tag not found in {CSPROJ}")
        sys.exit(1)
    return int(m[1]), int(m[2]), int(m[3])


def current_version() -> str:
    return "{}.{}.{}".format(*read_version())


def write_version(new_ver: str) -> tuple:
    old_ver = current_version()
    content = CSPROJ.read_text(encoding="utf-8")
    CSPROJ.write_text(content.replace(f"<Version>{old_ver}</Version>",
                                       f"<Version>{new_ver}</Version>"), encoding="utf-8")
    print(f"Version: {old_ver} -> {new_ver}")
    return old_ver, new_ver


def bump_version() -> tuple:
    major, minor, patch = read_version()
    return write_version(f"{major}.{minor}.{patch + 1}")


def find_nupkg(package_id: str, version: str) -> Path:
    exact = ARTIFACTS / f"{package_id}.{version}.nupkg"
    if exact.exists():
        return exact
    # Exclude the symbol package: `dotnet nuget push` uploads the matching .snupkg by itself.
    matches = [p for p in ARTIFACTS.glob(f"{package_id}.{version}*.nupkg")
               if not p.name.endswith(".symbols.nupkg")]
    if not matches:
        print(f"ERROR: {package_id}.{version}.nupkg not found in {ARTIFACTS}")
        sys.exit(1)
    return matches[0]

# ── Main ──────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="Publish DynLan to NuGet.org")
    parser.add_argument("--dry-run",    action="store_true", help="Skip the push step (implies --no-bump)")
    parser.add_argument("--no-bump",    action="store_true", help="Skip the version bump")
    parser.add_argument("--set-version",                     help="Release this exact version (x.y.z)")
    parser.add_argument("--skip-tests", action="store_true", help="Skip the test run (re-push only)")
    parser.add_argument("--engine", choices=["1", "2"], default="2", help="2 = DynLan 2 (default), 1 = old engine")
    args = parser.parse_args()

    global CSPROJ, TESTS_PROJ
    CSPROJ, TESTS_PROJ, extra_test_runs = ENGINES[args.engine]

    if args.set_version and not re.fullmatch(r"\d+\.\d+\.\d+", args.set_version):
        print(f"ERROR: --set-version expects x.y.z, got {args.set_version!r}")
        sys.exit(1)

    # A rehearsal must not consume a version number. Bumping and then not pushing leaves the repo
    # claiming a release that does not exist, and the next real one skips a number for no reason.
    if args.dry_run and not args.no_bump and not args.set_version:
        args.no_bump = True

    env_vars = load_env(ENV_FILE)
    api_key  = env_vars.get("NUGET_API_KEY") or os.environ.get("NUGET_API_KEY")
    if not api_key and not args.dry_run:
        print(f"ERROR: NUGET_API_KEY not found in {ENV_FILE} or environment.")
        print("       Create Deploy/.env with:  NUGET_API_KEY=<your-key>")
        sys.exit(1)

    # 1. Tests. A pushed version can be unlisted but never replaced, so this gate is before the bump
    #    rather than after it — a red suite must not consume a version number.
    if args.skip_tests:
        print("Skipping tests — do this only to re-push a version that already passed.")
    else:
        run(["dotnet", "test", str(TESTS_PROJ), "-c", "Release"], cwd=REPO_ROOT)
        for extra in extra_test_runs:
            run(["dotnet", "test", str(TESTS_PROJ), "-c", "Release", *extra], cwd=REPO_ROOT)

    # 2. Version
    if args.set_version:
        _, version = write_version(args.set_version)
    elif args.no_bump:
        version = current_version()
        print(f"Using the version already in {CSPROJ.name}: {version}")
    else:
        _, version = bump_version()

    # 3. Build + pack.
    run(["dotnet", "build", str(CSPROJ), "-c", "Release"], cwd=REPO_ROOT)

    ARTIFACTS.mkdir(exist_ok=True)
    run(["dotnet", "pack", str(CSPROJ), "-c", "Release", "--no-build", "-o", str(ARTIFACTS)],
        cwd=REPO_ROOT)
    nupkg = find_nupkg(PACKAGE_ID, version)
    print(f"\nPackage:\n  {nupkg}")

    # 4. Push.
    if args.dry_run:
        print("\n[dry-run] Skipping push to NuGet.org.")
        return

    run([
        "dotnet", "nuget", "push", str(nupkg),
        "--api-key", api_key,
        "--source",  "https://api.nuget.org/v3/index.json",
        "--skip-duplicate",
    ], cwd=REPO_ROOT)

    print(f"\nPublished {PACKAGE_ID} {version} to NuGet.org")


if __name__ == "__main__":
    main()
