# Contributing to GhostSpectator

Open issues and pull requests on [Forgejo][forge]. Sign in with GitHub and wait for account
activation. After activation, clone the Forgejo repository and send a pull request with AGit:

```sh
git push origin HEAD:refs/for/main -o topic=<short-name>
```

Use a Forgejo access token as the HTTPS password. Push again with the same topic to update
the pull request. GitHub serves a read-only mirror.

Read the [SisyphusMD project rules][project-rules] before contributing. Every pull request closes
an issue. Rule 9 excludes AI attribution, including a `Co-Authored-By` trailer for an assistant.
Report vulnerabilities privately as described in [SECURITY.md](SECURITY.md).

## Tests

Run `dotnet restore` to check project parsing and package resolution. The Forgejo validation
workflow also runs `python3 .forgejo/scripts/check-workflow-guard.py` with PyYAML installed.

[DEVELOPING.md](DEVELOPING.md) explains how to build against a local PEAK installation.
Changes to Harmony patches need a PatchValidator run against that installation and a gameplay
test of the affected behavior. Include the PEAK build ID and the result in the pull request.
Use a `No tests: <reason>` line if the change can only be tested in PEAK.

**Safety-critical code** for rule 13: none.

## Required checks

- `CI / Issue link (pull_request_target)`, `CI / Tests with code (pull_request_target)`,
  `CI / Diff size (pull_request_target)`, `CI / Lint (pull_request_target)`,
  `CI / Secrets (pull_request_target)` and `AI review`
- `Validate / Restore + project parse (pull_request)`

AGit and fork pull requests receive the standard gates. The restore and workflow-guard job
runs after a maintainer reviews the change and opens a pull request from a branch here.
Each new gate needs a passing and a failing run before it is added to branch protection.

## Break-glass

Rule 20 covers outages. For an urgent merge with Forgejo available, the maintainer can
remove the unavailable required context, restore it immediately after the merge, and record
the exception on the issue.

[forge]: https://forgejo.bryantserver.com/SisyphusMD/GhostSpectator
[project-rules]: https://forgejo.bryantserver.com/SisyphusMD/project-rules/src/branch/main/RULES.md
