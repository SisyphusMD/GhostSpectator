# #27 Adopt the SisyphusMD project rules

- Kind: pull, state: closed, by cody, created 2026-11-17T14:00:45-08:00
- Labels: none; milestone: none
- refs/pull/27/head -> main, merged: True

Closes #26

Add the shared checks, issue templates, nightly issue archive, private security reporting and GitHub mirror lockdown. CONTRIBUTING lists the existing restore check and the local PEAK validation used for Harmony patches.

Large diff: the shared workflows, scripts, templates and rules are copied verbatim from project-rules.

Validation: the workflow trust guard, pinned Ruff, strict yamllint and the history secret scan passed. The local .NET restore reached package resolution but timed out. Forgejo's required restore check will validate the PR.
