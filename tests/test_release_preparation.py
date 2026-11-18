"""Prove that local release preparation leaves the primary branch and tags alone."""

import os
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOTNET_FIXTURE = """#!/usr/bin/env python3
import pathlib
import re
import sys

if sys.argv[1] == "run":
    print("PEAK buildid: fixture")
elif sys.argv[2] == "src/GhostSpectator":
    project = pathlib.Path("src/GhostSpectator/GhostSpectator.csproj")
    version = re.search("<Version>([^<]+)", project.read_text())[1]
    output = pathlib.Path("artifacts/thunderstore/release")
    output.mkdir(parents=True)
    (output / f"SisyphusMD-GhostSpectator-{version}.zip").write_bytes(b"fixture")
"""


class ReleasePreparationTest(unittest.TestCase):
    def test_gameplay_confirmation_controls_candidate_only_push(self):
        with tempfile.TemporaryDirectory() as scratch:
            fixture = Path(scratch)
            primary = fixture / "primary.git"
            seed = fixture / "seed"
            seed.mkdir()

            def git(cwd, *args):
                return subprocess.check_output(
                    ["git", *args], cwd=cwd, text=True, stderr=subprocess.DEVNULL,
                ).strip()

            def identity(checkout):
                git(checkout, "config", "user.name", "Release test")
                git(checkout, "config", "user.email", "release@example.test")
                git(checkout, "config", "commit.gpgsign", "false")

            for path in (
                "scripts/release.sh",
                "CHANGELOG.md",
                "README.md",
                "thunderstore.toml",
                "src/GhostSpectator/GhostSpectator.csproj",
            ):
                target = seed / path
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes((ROOT / path).read_bytes())
            (seed / "releases").mkdir()
            (seed / "releases/.gitkeep").touch()
            git(fixture, "init", "--bare", "--initial-branch=main", str(primary))
            git(seed, "init", "-b", "main")
            identity(seed)
            git(seed, "add", ".")
            git(seed, "commit", "-m", "Seed release inputs")
            git(seed, "remote", "add", "origin", str(primary))
            git(seed, "push", "origin", "main")
            base = git(seed, "rev-parse", "HEAD")

            dotnet = fixture / "dotnet"
            dotnet.write_text(DOTNET_FIXTURE)
            dotnet.chmod(0o700)
            env = dict(os.environ, PATH=f"{fixture}:{os.environ['PATH']}", ManagedDir=scratch)
            for answer in ("no", "yes"):
                with self.subTest(answer=answer):
                    work = fixture / answer
                    git(fixture, "clone", str(primary), str(work))
                    identity(work)
                    result = subprocess.run(
                        ["bash", "scripts/release.sh", "patch"], cwd=work, env=env,
                        input=f"{answer}\n", text=True, capture_output=True, check=False,
                    )
                    self.assertEqual(git(work, "ls-remote", "origin", "refs/heads/main").split()[0], base)
                    self.assertEqual(git(work, "ls-remote", "--tags", "origin"), "")
                    ref = git(work, "ls-remote", "origin", "refs/heads/release/v0.0.1")
                    if answer == "no":
                        self.assertNotEqual(result.returncode, 0)
                        self.assertIn("gameplay confirmation required", result.stderr)
                        self.assertEqual(ref, "")
                    else:
                        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                        self.assertEqual(ref.split()[0], git(work, "rev-parse", "HEAD"))
                        self.assertIn(f"(prepared from {base})", git(work, "log", "-1", "--format=%B"))
