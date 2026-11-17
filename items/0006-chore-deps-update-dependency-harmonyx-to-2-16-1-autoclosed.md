# #6 chore(deps): update dependency harmonyx to 2.16.1 - autoclosed

- Kind: pull, state: closed, by renovate, created 2026-08-09T18:36:10-07:00
- Labels: none; milestone: none
- refs/pull/6/head -> main, merged: False

This PR contains the following updates:

| Package | Change | [Age](https://docs.renovatebot.com/merge-confidence/) | [Confidence](https://docs.renovatebot.com/merge-confidence/) |
|---|---|---|---|
| [HarmonyX](https://github.com/BepInEx/HarmonyX) | `2.9.0` → `2.16.1` | ![age](https://developer.mend.io/api/mc/badges/age/nuget/HarmonyX/2.16.1?slim=true) | ![confidence](https://developer.mend.io/api/mc/badges/confidence/nuget/HarmonyX/2.9.0/2.16.1?slim=true) |

---

### Release Notes

<details>
<summary>BepInEx/HarmonyX (HarmonyX)</summary>

### [`v2.16.1`](https://github.com/BepInEx/HarmonyX/releases/tag/v2.16.1)

#### What's Changed

- Fix an invalid cast excpetion in CodeInstruction.LocalIndex() by [@&#8203;aldelaro5](https://github.com/aldelaro5) in [#&#8203;139](https://github.com/BepInEx/HarmonyX/pull/139)
- Bump MonoMod.RuntimeDetour version to 25.3.4 by [@&#8203;aldelaro5](https://github.com/aldelaro5) in [#&#8203;140](https://github.com/BepInEx/HarmonyX/pull/140)

**Full Changelog**: <https://github.com/BepInEx/HarmonyX/compare/v2.16.0...v2.16.1>

### [`v2.16.0`](https://github.com/BepInEx/HarmonyX/releases/tag/v2.16.0)

#### What's Changed

- Backport a ReversePatch fix from upstream by [@&#8203;aldelaro5](https://github.com/aldelaro5) in [#&#8203;134](https://github.com/BepInEx/HarmonyX/pull/134)
- Upgrade MonoMod.RuntimeDetour to 25.3.3 and Add .NET 10 support  by [@&#8203;hazre](https://github.com/hazre) in [#&#8203;136](https://github.com/BepInEx/HarmonyX/pull/136)

#### New Contributors

- [@&#8203;hazre](https://github.com/hazre) made their first contribution in [#&#8203;136](https://github.com/BepInEx/HarmonyX/pull/136)

**Full Changelog**: <https://github.com/BepInEx/HarmonyX/compare/v2.15.0...v2.16.0>

### [`v2.15.0`](https://github.com/BepInEx/HarmonyX/releases/tag/v2.15.0)

This version is the first to feature ARM support (including Apple Silicon). Be aware that while it should work, it was not tested yet. Please let us know if it works or not on the BepInEx discord server or in discussions.

**Warning:** There may be some issues with IL manipulation on mono runtimes (see [MonoMod/MonoMod#233](https://github.com/MonoMod/MonoMod/pull/233)). The v2.13 release is the last release without this problem.

#### What's Changed

- Add nuget publish to CI by [@&#8203;ManlyMarco](https://github.com/ManlyMarco) in [#&#8203;124](https://github.com/BepInEx/HarmonyX/pull/124)
- Update MonoMod to 25.3.0 by [@&#8203;aldelaro5](https://github.com/aldelaro5) in [#&#8203;132](https://github.com/BepInEx/HarmonyX/pull/132)
- Rebase upstream to 2.3.6 by [@&#8203;aldelaro5](https://github.com/aldelaro5) in [#&#8203;131](https://github.com/BepInEx/HarmonyX/pull/131)

**Full Changelog**: <https://github.com/BepInEx/HarmonyX/compare/v2.14.0...v2.15.0>

### [`v2.14.0`](https://github.com/BepInEx/HarmonyX/releases/tag/v2.14.0)

**Warning:** There may be some issues with IL manipulation on mono runtimes (see [MonoMod/MonoMod#233](https://github.com/MonoMod/MonoMod/pull/233)). The v2.13 release is the last release without this problem.

#### What's Changed

- .NET 9 Support by [@&#8203;ds5678](https://github.com/ds5678) in [#&#8203;121](https://github.com/BepInEx/HarmonyX/pull/121)
- Synchronizing dumper with monomod and fixing ILLabel dumping by [@&#8203;kohanis](https://github.com/kohanis) in [#&#8203;118](https://github.com/BepInEx/HarmonyX/pull/118)
- Support non-static signatures in NativeDetourMethodPatcher, fixes [#&#8203;122](https://github.com/BepInEx/HarmonyX/issues/122). by [@&#8203;CptMoore](https://github.com/CptMoore) in [#&#8203;123](https://github.com/BepInEx/HarmonyX/pull/123)

#### New Contributors

- [@&#8203;ds5678](https://github.com/ds5678) made their first contribution in [#&#8203;121](https://github.com/BepInEx/HarmonyX/pull/121)
- [@&#8203;CptMoore](https://github.com/CptMoore) made their first contribution in [#&#8203;123](https://github.com/BepInEx/HarmonyX/pull/123)

**Full Changelog**: <https://github.com/BepInEx/HarmonyX/compare/v2.13.0...v2.14.0>

### [`v2.13.0`](https://github.com/BepInEx/HarmonyX/releases/tag/v2.13.0)

This release includes all changes made to upstream since Harmony v2.2.1. See Harmony changelog [here](https://github.com/pardeike/Harmony/releases) for details on the merged changes.

#### What's Changed

- Merging upstream up to v2.3.1.1 by [@&#8203;kohanis](https://github.com/kohanis) in [#&#8203;97](https://github.com/BepInEx/HarmonyX/pull/97)
- Merging upstream up to v2.3.3.0 by [@&#8203;kohanis](https://github.com/kohanis) in [#&#8203;114](https://github.com/BepInEx/HarmonyX/pull/114)
- Add test build workflow for PRs by [@&#8203;ManlyMarco](https://github.com/ManlyMarco) in [#&#8203;113](https://github.com/BepInEx/HarmonyX/pull/113)
- Upgrade MonoMod.RuntimeDetour to 25.1.2 by [@&#8203;aldelaro5](https://github.com/aldelaro5) in [#&#8203;115](https://github.com/BepInEx/HarmonyX/pull/115)

#### New Contributors

- [@&#8203;aldelaro5](https://github.com/aldelaro5) made their first contribution in [#&#8203;115](https://github.com/BepInEx/HarmonyX/pull/115)

**Full Changelog**: <https://github.com/BepInEx/HarmonyX/compare/v2.12.0...v2.13.0>

### [`v2.12.0`](https://github.com/BepInEx/HarmonyX/releases/tag/v2.12.0)

#### What's Changed

- Fix for unintentional removal of some `leave` instructions by [@&#8203;kohanis](https://github.com/kohanis) in [#&#8203;96](https://github.com/BepInEx/HarmonyX/pull/96)
- Fix invalid file name when dumping compiler-generated methods by [@&#8203;Meivyn](https://github.com/Meivyn) in [#&#8203;99](https://github.com/BepInEx/HarmonyX/pull/99)
- CreateAndPatchAll - use a counter + target name as default ID instead of a GUID by [@&#8203;ManlyMarco](https://github.com/ManlyMarco) in [#&#8203;103](https://github.com/BepInEx/HarmonyX/pull/103)
- Add HarmonyOptional attribute by [@&#8203;ManlyMarco](https://github.com/ManlyMarco) in [#&#8203;105](https://github.com/BepInEx/HarmonyX/pull/105)

#### New Contributors

- [@&#8203;kohanis](https://github.com/kohanis) made their first contribution in [#&#8203;96](https://github.com/BepInEx/HarmonyX/pull/96)
- [@&#8203;Meivyn](https://github.com/Meivyn) made their first contribution in [#&#8203;99](https://github.com/BepInEx/HarmonyX/pull/99)

**Full Changelog**: <https://github.com/BepInEx/HarmonyX/compare/v2.11.0...v2.12.0>

### [`v2.10.2`](https://github.com/BepInEx/HarmonyX/releases/tag/v2.10.2)

#### Changelog

- [`4e71dc4`](https://github.com/BepInEx/HarmonyX/commit/4e71dc4f483a8432b001b34e0d2ba7faf47d59b1) [@&#8203;pardeike](https://github.com/pardeike)	Fix a bug where Harmony calls TargetMethods() always twice

**Full Changelog**: <https://github.com/BepInEx/HarmonyX/compare/v2.10.1...v2.10.2>

### [`v2.10.1`](https://github.com/BepInEx/HarmonyX/releases/tag/v2.10.1)

#### Changelog

- Print out lastError in `ThrowIfInvalid` ([#&#8203;48](https://github.com/BepInEx/HarmonyX/issues/48))

### [`v2.10.0`](https://github.com/BepInEx/HarmonyX/releases/tag/v2.10.0)

#### Changelog

- Merged code from upstream up to [`6006140`](https://github.com/BepInEx/HarmonyX/commit/6006140d929565cb49ee07d482f96ec0d51a7aed) ([see Harmony changelog](https://github.com/pardeike/Harmony/releases/tag/v2.2.1.0))
  - **Important:** The merge brings binary incompatibilities to `CodeMatch` class. If you distribute HarmonyX in games or mod loaders, please test for any regressions and apply shims where appropriate

    Starting this version, `CodeMatch` now inherits `CodeInstruction`. Because of this, some fields (labels, blocks) have been removed from `CodeMatch` as they are now provided by `CodeInstructon`. Any `CodeMatch` uses where fields are accessed directly will be binary-incompatible. However, this should generally be fairly rare: analysing a few dozen `CodeMatch` class uses across different GitHub projects revealed that most developers only use the `new CodeMatch` constructor, which hasn't been altered. Most use cases of `CodeMatch` should still be binary-compatible with HarmonyX 2.10.

  - All nested classes in the `Code` class have also been annotated with the `[EditorBrowsable(EditorBrowsableState.Never)]` attribute to prevent custom classes from cluttering most IDEs' code completion.

- Fix `HARMONY_NO_LOG` environment variable is not respected for file logger

- Fix `HarmonyEmitIL` attribute not working on methods with certain branches

- Fix handling methods that include `throws` OpCode on mono

- Fix net35 builds not working on CoreCLR because of `AccessTools.PrepForRemoting` fix

- Improve exception messages when applying patches (warn about missing target method or patch method not being static)

- Bump MonoMod version to 22.3.23.4 ([changelog](https://github.com/MonoMod/MonoMod/releases/tag/v22.03.23.04))

</details>

---

### Configuration

📅 **Schedule**: (UTC)

- Branch creation
  - At any time (no schedule defined)
- Automerge
  - At any time (no schedule defined)

🚦 **Automerge**: Disabled by config. Please merge this manually once you are satisfied.

♻ **Rebasing**: Whenever PR is behind base branch, or you tick the rebase/retry checkbox.

🔕 **Ignore**: Close this PR and you won't be reminded about this update again.

---

 - [ ] <!-- rebase-check -->If you want to rebase/retry this PR, check this box

---

This PR has been generated by [Mend Renovate CLI](https://github.com/renovatebot/renovate).
<!--renovate-debug:eyJjcmVhdGVkSW5WZXIiOiI0My4yODguMCIsInVwZGF0ZWRJblZlciI6IjQ0LjE1MS4yIiwidGFyZ2V0QnJhbmNoIjoibWFpbiIsImxhYmVscyI6WyJtaW5vci11cGRhdGUiLCJudWdldCJdfQ==-->
