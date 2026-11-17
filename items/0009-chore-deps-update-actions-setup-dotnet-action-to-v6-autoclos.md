# #9 chore(deps): update actions/setup-dotnet action to v6 - autoclosed

- Kind: pull, state: closed, by renovate, created 2026-08-09T18:36:15-07:00
- Labels: none; milestone: none
- refs/pull/9/head -> main, merged: False

This PR contains the following updates:

| Package | Type | Update | Change |
|---|---|---|---|
| [actions/setup-dotnet](https://github.com/actions/setup-dotnet) | action | major | `v4` → `v6` |

---

### Release Notes

<details>
<summary>actions/setup-dotnet (actions/setup-dotnet)</summary>

### [`v6.0.0`](https://github.com/actions/setup-dotnet/releases/tag/v6.0.0)

[Compare Source](https://github.com/actions/setup-dotnet/compare/v5.4.0...v6.0.0)

##### What's Changed

- Migrate to ESM and upgrade dependencies by [@&#8203;priyagupta108](https://github.com/priyagupta108) in [#&#8203;752](https://github.com/actions/setup-dotnet/pull/752)
- Bump actions/checkout from 6.0.3 to 7.0.0 by [@&#8203;dependabot](https://github.com/dependabot)\[bot] in [#&#8203;751](https://github.com/actions/setup-dotnet/pull/751)
- chore(deps): bump [@&#8203;actions/cache](https://github.com/actions/cache) to 6.2.0 by [@&#8203;philip-gai](https://github.com/philip-gai) in [#&#8203;756](https://github.com/actions/setup-dotnet/pull/756)

##### New Contributors

- [@&#8203;philip-gai](https://github.com/philip-gai) made their first contribution in [#&#8203;756](https://github.com/actions/setup-dotnet/pull/756)

**Full Changelog**: <https://github.com/actions/setup-dotnet/compare/v5...v6.0.0>

### [`v5.4.0`](https://github.com/actions/setup-dotnet/releases/tag/v5.4.0)

[Compare Source](https://github.com/actions/setup-dotnet/compare/v5.3.0...v5.4.0)

##### What's Changed

##### Enhancements

- Pin actions to commit SHAs in workflows by [@&#8203;priya-kinthali](https://github.com/priya-kinthali) in [#&#8203;744](https://github.com/actions/setup-dotnet/pull/744)
- Expand the CSC problem matcher to light up more errors on GitHub. by [@&#8203;StephenCleary](https://github.com/StephenCleary) in [#&#8203;717](https://github.com/actions/setup-dotnet/pull/717)
- Improve global.json SDK version validation for rollForward by [@&#8203;priyagupta108](https://github.com/priyagupta108) in [#&#8203;742](https://github.com/actions/setup-dotnet/pull/742)

> The action now validates the `sdk.version` field in `global.json` when `rollForward` is specified. The version must be a fully-qualified SDK version (e.g., `8.0.100`, `10.0.100`). Wildcard versions (e.g., `10.0.*`) and runtime-style versions (e.g., `8.0.0`) will now fail. See the [.NET SDK version specification](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json#version) for details.

##### Documentation

- Docs(action): Explicitly mark all optional inputs with required: false by [@&#8203;kranthipoturaju](https://github.com/kranthipoturaju) in [#&#8203;737](https://github.com/actions/setup-dotnet/pull/737)

##### Bug Fixes

- Fix global.json creation command by [@&#8203;michal2612](https://github.com/michal2612) in [#&#8203;694](https://github.com/actions/setup-dotnet/pull/694)

##### Dependency Updates

- Upgrade [@&#8203;actions/cache](https://github.com/actions/cache) to 5.1.0, log cache write denied by [@&#8203;jasongin](https://github.com/jasongin) in [#&#8203;746](https://github.com/actions/setup-dotnet/pull/746)

##### New Contributors

- [@&#8203;jasongin](https://github.com/jasongin) made their first contribution in [#&#8203;746](https://github.com/actions/setup-dotnet/pull/746)
- [@&#8203;michal2612](https://github.com/michal2612) made their first contribution in [#&#8203;694](https://github.com/actions/setup-dotnet/pull/694)
- [@&#8203;kranthipoturaju](https://github.com/kranthipoturaju) made their first contribution in [#&#8203;737](https://github.com/actions/setup-dotnet/pull/737)
- [@&#8203;StephenCleary](https://github.com/StephenCleary) made their first contribution in [#&#8203;717](https://github.com/actions/setup-dotnet/pull/717)

**Full Changelog**: <https://github.com/actions/setup-dotnet/compare/v5.3.0...v5.4.0>

### [`v5.3.0`](https://github.com/actions/setup-dotnet/releases/tag/v5.3.0)

[Compare Source](https://github.com/actions/setup-dotnet/compare/v5.2.0...v5.3.0)

##### What's Changed

##### Enhancements

- Add dotnet-version: latest support with dotnet-channel input by [@&#8203;mahabaleshwars](https://github.com/mahabaleshwars) in [#&#8203;730](https://github.com/actions/setup-dotnet/pull/730)
- Support global.json's rollForward latest\* variants by [@&#8203;js6pak](https://github.com/js6pak) in [#&#8203;538](https://github.com/actions/setup-dotnet/pull/538)
- Improve version resolution by [@&#8203;akoeplinger](https://github.com/akoeplinger) in [#&#8203;560](https://github.com/actions/setup-dotnet/pull/560)

##### Dependency Updates

- Upgrade @&#8203;actions/\* and fast-xml-parser dependencies by [@&#8203;Copilot](https://github.com/Copilot) in [#&#8203;728](https://github.com/actions/setup-dotnet/pull/728)
- Update install scripts to v2026.05.19 (preserve archive links) by [@&#8203;MichaelSimons](https://github.com/MichaelSimons) in [#&#8203;736](https://github.com/actions/setup-dotnet/pull/736)
- Add rollForward note in README, improve proxy health check in e2e tests and bump version to v5.3.0 by [@&#8203;priyagupta108](https://github.com/priyagupta108) in [#&#8203;738](https://github.com/actions/setup-dotnet/pull/738)

##### Bug Fixes

- Update Test Proxy job by [@&#8203;priya-kinthali](https://github.com/priya-kinthali) in [#&#8203;703](https://github.com/actions/setup-dotnet/pull/703)

##### New Contributors

- [@&#8203;Copilot](https://github.com/Copilot) made their first contribution in [#&#8203;728](https://github.com/actions/setup-dotnet/pull/728)
- [@&#8203;akoeplinger](https://github.com/akoeplinger) made their first contribution in [#&#8203;560](https://github.com/actions/setup-dotnet/pull/560)
- [@&#8203;MichaelSimons](https://github.com/MichaelSimons) made their first contribution in [#&#8203;736](https://github.com/actions/setup-dotnet/pull/736)
- [@&#8203;js6pak](https://github.com/js6pak) made their first contribution in [#&#8203;538](https://github.com/actions/setup-dotnet/pull/538)

**Full Changelog**: <https://github.com/actions/setup-dotnet/compare/v5...v5.3.0>

### [`v5.2.0`](https://github.com/actions/setup-dotnet/releases/tag/v5.2.0)

[Compare Source](https://github.com/actions/setup-dotnet/compare/v5.1.0...v5.2.0)

##### What's changed

##### Enhancements

- Add support for workloads input by [@&#8203;gowridurgad](https://github.com/gowridurgad) in [#&#8203;693](https://github.com/actions/setup-dotnet/pull/693)
- Add support for optional architecture input for cross-architecture .NET installs by [@&#8203;priya-kinthali](https://github.com/priya-kinthali) in [#&#8203;700](https://github.com/actions/setup-dotnet/pull/700)

##### Dependency Updates

- Upgrade fast-xml-parser from 4.4.1 to 5.3.6 by [@&#8203;dependabot](https://github.com/dependabot) in [#&#8203;671](https://github.com/actions/setup-dotnet/pull/671)
- Upgrade minimatch from 3.1.2 to 3.1.5 by [@&#8203;dependabot](https://github.com/dependabot) in [#&#8203;705](https://github.com/actions/setup-dotnet/pull/705)

**Full Changelog**: <https://github.com/actions/setup-dotnet/compare/v5...v5.2.0>

### [`v5.1.0`](https://github.com/actions/setup-dotnet/releases/tag/v5.1.0)

[Compare Source](https://github.com/actions/setup-dotnet/compare/v5.0.1...v5.1.0)

##### What's Changed

##### Documentation

- Readme update for environment variable on self hosted linux runners by [@&#8203;priya-kinthali](https://github.com/priya-kinthali) in [#&#8203;689](https://github.com/actions/setup-dotnet/pull/689)
- Contributor icon updates by [@&#8203;Falco20019](https://github.com/Falco20019) in [#&#8203;604](https://github.com/actions/setup-dotnet/pull/604)

##### Dependency updates

- Upgrade actions/checkout from 5 to 6 by [@&#8203;dependabot](https://github.com/dependabot) in [#&#8203;684](https://github.com/actions/setup-dotnet/pull/684)
- Upgrade to latest actions packages by [@&#8203;salmanmkc](https://github.com/salmanmkc) in [#&#8203;687](https://github.com/actions/setup-dotnet/pull/687)
- Upgrade dependencies in testproject and checkout in Readme by [@&#8203;priya-kinthali](https://github.com/priya-kinthali) in [#&#8203;692](https://github.com/actions/setup-dotnet/pull/692)

##### New Contributors

- [@&#8203;priya-kinthali](https://github.com/priya-kinthali) made their first contribution in [#&#8203;689](https://github.com/actions/setup-dotnet/pull/689)
- [@&#8203;Falco20019](https://github.com/Falco20019) made their first contribution in [#&#8203;604](https://github.com/actions/setup-dotnet/pull/604)

**Full Changelog**: <https://github.com/actions/setup-dotnet/compare/v5...v5.1.0>

### [`v5.0.1`](https://github.com/actions/setup-dotnet/releases/tag/v5.0.1)

[Compare Source](https://github.com/actions/setup-dotnet/compare/v5.0.0...v5.0.1)

##### What's Changed

- Upgrade typescript from 5.4.2 to 5.9.2 and document breaking changes in v5 by [@&#8203;dependabot](https://github.com/dependabot) in [#&#8203;624](https://github.com/actions/setup-dotnet/pull/624)
- Upgrade eslint-plugin-jest from 27.9.0 to 29.0.1 by [@&#8203;dependabot](https://github.com/dependabot) in [#&#8203;648](https://github.com/actions/setup-dotnet/pull/648)
- Upgrade actions/publish-action from 0.3.0 to 0.4.0 and update macos-13 to macos-15-intel by [@&#8203;dependabot](https://github.com/dependabot) in [#&#8203;665](https://github.com/actions/setup-dotnet/pull/665)

**Full Changelog**: <https://github.com/actions/setup-dotnet/compare/v5...v5.0.1>

### [`v5.0.0`](https://github.com/actions/setup-dotnet/releases/tag/v5.0.0)

[Compare Source](https://github.com/actions/setup-dotnet/compare/v4.3.1...v5.0.0)

##### What's Changed

##### Breaking Changes

- Upgrade to Node.js 24 and modernize async usage by [@&#8203;salmanmkc](https://github.com/salmanmkc) in [#&#8203;654](https://github.com/actions/setup-dotnet/pull/654)

Make sure your runner is updated to this version or newer to use this release. v2.327.1 [Release Notes](https://github.com/actions/runner/releases/tag/v2.327.1)

##### Dependency Updates

- Upgrade [@&#8203;action/cache](https://github.com/action/cache) from 4.0.2 to 4.0.3 by [@&#8203;aparnajyothi-y](https://github.com/aparnajyothi-y) in [#&#8203;622](https://github.com/actions/setup-dotnet/pull/622)
- Upgrade husky from 8.0.3 to 9.1.7 by [@&#8203;dependabot](https://github.com/dependabot)\[bot] in [#&#8203;591](https://github.com/actions/setup-dotnet/pull/591)
- Upgrade [@&#8203;actions/glob](https://github.com/actions/glob) from 0.4.0 to 0.5.0 by [@&#8203;dependabot](https://github.com/dependabot)\[bot] in [#&#8203;594](https://github.com/actions/setup-dotnet/pull/594)
- Upgrade eslint-config-prettier from 9.1.0 to 10.1.5 by [@&#8203;dependabot](https://github.com/dependabot)\[bot] in [#&#8203;639](https://github.com/actions/setup-dotnet/pull/639)
- Upgrade undici from 5.28.5 to 5.29.0 by [@&#8203;dependabot](https://github.com/dependabot)\[bot] in [#&#8203;641](https://github.com/actions/setup-dotnet/pull/641)
- Upgrade form-data to bring in fix for critical vulnerability by [@&#8203;gowridurgad](https://github.com/gowridurgad) in [#&#8203;652](https://github.com/actions/setup-dotnet/pull/652)
- Upgrade actions/checkout from 4 to 5 by [@&#8203;dependabot](https://github.com/dependabot)\[bot] in [#&#8203;662](https://github.com/actions/setup-dotnet/pull/662)

##### Bug Fixes

- Remove Support for older .NET Versions and Update installers scripts by [@&#8203;gowridurgad](https://github.com/gowridurgad) in [#&#8203;647](https://github.com/actions/setup-dotnet/pull/647)

##### New Contributors

- [@&#8203;gowridurgad](https://github.com/gowridurgad) made their first contribution in [#&#8203;647](https://github.com/actions/setup-dotnet/pull/647)
- [@&#8203;salmanmkc](https://github.com/salmanmkc) made their first contribution in [#&#8203;654](https://github.com/actions/setup-dotnet/pull/654)

**Full Changelog**: <https://github.com/actions/setup-dotnet/compare/v4...v5.0.0>

</details>

---

### Configuration

📅 **Schedule**: (UTC)

- Branch creation
  - At any time (no schedule defined)
- Automerge
  - At any time (no schedule defined)

🚦 **Automerge**: Disabled by config. Please merge this manually once you are satisfied.

♻ **Rebasing**: Whenever PR becomes conflicted, or you tick the rebase/retry checkbox.

🔕 **Ignore**: Close this PR and you won't be reminded about this update again.

---

 - [ ] <!-- rebase-check -->If you want to rebase/retry this PR, check this box

---

This PR has been generated by [Mend Renovate CLI](https://github.com/renovatebot/renovate).
<!--renovate-debug:eyJjcmVhdGVkSW5WZXIiOiI0My4yODguMCIsInVwZGF0ZWRJblZlciI6IjQ0LjkzLjgiLCJ0YXJnZXRCcmFuY2giOiJtYWluIiwibGFiZWxzIjpbImdpdGh1Yi1hY3Rpb25zIiwibWFqb3ItdXBkYXRlIl19-->
