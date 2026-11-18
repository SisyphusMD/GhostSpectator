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

The build runs locally against PEAK. Forgejo tags the reviewed release commit and publishes
its prebuilt ZIP to Thunderstore and the release mirrors.

## Releasing

Create a Forgejo release issue. From a clean `main`, run the local preparation script.

```sh
scripts/release.sh patch
scripts/release.sh --dry-run patch
```

Choose `patch`, `minor` or `major`. Dry run stops after building the ZIP, leaving
the changed files for inspection.

1. The script runs PatchValidator against your PEAK installation and records the Steam build ID.
2. It promotes the changelog, updates the project and Thunderstore versions, and builds the ZIP in `releases/`.
3. Test that ZIP in PEAK and confirm the prompt. The script pushes only `release/vX.Y.Z`.
4. Open a normal PR to `main` that closes the release issue. Include the PEAK build ID and gameplay
   result and explain that the version updates and ZIP were qualified locally. Merge after checks
   pass while the branch is up to date. If `main` moves before the merge, close the stale PR,
   retire its local and remote release branches with approval, then prepare again from current `main`.
   If the initial branch push was interrupted, retry that push from the prepared local branch.
5. Dispatch **Publish Release** from `main` with the merged PR number. It verifies the exact
   merge, version stamps and ZIP manifest, then tags that commit. The existing Publish
   workflow uploads the ZIP to Thunderstore, Forgejo, the annex and GitHub.

## Game-code reference

The game's `Assembly-CSharp.dll` can be decompiled for reference reading:

```sh
dotnet tool install -g ilspycmd
ilspycmd "<PEAK>/PEAK_Data/Managed/Assembly-CSharp.dll" -p -o /tmp/peak-decompiled
```

## Repository

- Source of truth: [forgejo.bryantserver.com/SisyphusMD/GhostSpectator](https://forgejo.bryantserver.com/SisyphusMD/GhostSpectator)
- Read-only mirror: [github.com/SisyphusMD/GhostSpectator](https://github.com/SisyphusMD/GhostSpectator)
