# Developing GhostSpectator

## Prerequisites

- .NET SDK 10 or later (`brew install --cask dotnet-sdk` on macOS, or [Microsoft installer](https://dotnet.microsoft.com/download))
- A local PEAK install, the build references game DLLs from `<PEAK>/PEAK_Data/Managed/`

## Setup

```sh
cp Config.Build.user.props.template Config.Build.user.props
# Edit Config.Build.user.props to set:
#   - PEAKGameRootDir (path to your PEAK install)
#   - DeployModFiles=true to auto-copy built DLLs into BepInEx/plugins/
```

## Build

```sh
dotnet build                      # debug build, copies DLL to BepInEx/plugins/ if configured
dotnet build -c Release -v d      # release build, produces a Thunderstore .zip at ./artifacts/thunderstore/
```

## Why CI doesn't compile

PEAK has no public stripped-game-libs NuGet package (unlike Lethal Company's), and the actual game DLLs can't be redistributed. CI on Forgejo therefore only validates that the project file parses and NuGet feeds resolve (`dotnet restore`); the actual build runs on the maintainer's machine where PEAK is installed.

The compromise is **local build + CI publish**: `scripts/release.sh` builds the Thunderstore zip locally, commits it to `releases/`, and pushes a `v*.*.*` tag. The `publish.yml` workflow then fires on that tag and handles all the *publish* steps (Thunderstore upload, forgejo + github release reconciliation, asset attachment) -- none of which need to compile against game DLLs.

## Releasing

```sh
# Validates patch targets against your local PEAK, promotes CHANGELOG,
# bumps csproj, updates README compatibility, builds zip, commits, tags
# (annotated with the CHANGELOG section), and pushes.
scripts/release.sh patch    # or 'minor' / 'major'

# Add --dry-run to walk through the flow without committing/pushing:
scripts/release.sh --dry-run patch
```

What the script does in order:

1. **Preflight.** Refuses to run unless: on `main`, working tree is clean, in sync with `origin/main`, the `[Unreleased]` section exists, and the new tag doesn't already exist.
2. **PatchValidator CLI.** Runs `src/GhostSpectator.PatchValidatorCli` against your local `PEAK_Data/Managed/` and aborts if any `[HarmonyPatch]` target, field, or property doesn't resolve. Also extracts the Steam appmanifest `buildid` for use in CHANGELOG / README.
3. **Promote CHANGELOG.** Moves the contents of `[Unreleased]` into a new `[X.Y.Z] - YYYY-MM-DD` section, prefixed with a `### Validated against` block listing the PEAK buildid. Dep commits since the previous tag (matching `(chore|fix)(deps):`) become a `### Dependencies` block.
4. **Bump csproj `<Version>`** and **update the README `<!-- COMPAT:start --> ... <!-- COMPAT:end -->` block** with the new buildid + date.
5. **Build the Thunderstore zip** via `dotnet build -c Release` (ThunderPipe.Sdk packs it) and copy to `releases/SisyphusMD-GhostSpectator-X.Y.Z.zip`.
6. **Interactive gate.** Asks `Have you tested this build in PEAK?` -- Thunderstore is no-delete-only-deprecate, so the human check belongs here. The validator catches patch-target regressions but not gameplay ones.
7. **Commit, tag (annotated, with the CHANGELOG section as the message), push** the commit and the tag.

Tag push triggers `publish.yml`, which:

- Uploads the zip to Thunderstore via `tcli`.
- Reconciles releases on cluster forgejo, NAS forgejo, and github (walking ALL `v*.*.*` tags for self-healing -- a release that failed to create on a prior run is picked up the next time this workflow fires).
- Attaches the zip as a release asset on each registry.
- Per-registry mirror steps are `continue-on-error`, so a missing secret or pre-mirror-setup repo warns rather than fails the workflow.

## Game-code reference

The game's `Assembly-CSharp.dll` can be decompiled for reference reading:

```sh
dotnet tool install -g ilspycmd
ilspycmd "<PEAK>/PEAK_Data/Managed/Assembly-CSharp.dll" -p -o /tmp/peak-decompiled
```

## Repository

- Source of truth: [forgejo.bryantserver.com/SisyphusMD/GhostSpectator](https://forgejo.bryantserver.com/SisyphusMD/GhostSpectator)
- Read-only mirror: [github.com/SisyphusMD/GhostSpectator](https://github.com/SisyphusMD/GhostSpectator)
