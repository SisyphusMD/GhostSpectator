# #23 Check the AGit and fork trust gate on every pull_request job

- Kind: issue, state: closed, by cody, created 2026-11-17T13:11:51-08:00
- Labels: status:in-progress; milestone: none

Follow-up to the AGit gate (`github.event.pull_request.flow == 0` beside the head-repository check) and SisyphusMD/project-rules#79.

Nothing checks the gate. Add a check, run in CI, that walks every job of every `pull_request`-triggered workflow under `.forgejo/workflows/` and fails unless its `if:` carries both the head-repository clause and `flow == 0`, with named exemptions and a reason for each. A job that loses its whole `if:` must fail it.

This repo has no `CONTRIBUTING.md` yet; the note that AGit and fork pull requests get no CI arrives with the project-rules adoption (SisyphusMD/project-rules#75).

## cody, 2026-11-17T13:24:52-08:00

Status: not started beyond the issue. Plan: a check run in CI that fails any pull_request job missing either gate clause, with named exemptions. Handed off.

## cody, 2026-11-17T13:29:49-08:00

Taking over the guard check from the release follow-up handoff. The adoption PR remains separate.

## cody, 2026-11-17T13:40:54-08:00

Merged in #24 at 938021b. The Forgejo Validate job passed with the new check. Local mutations that remove either trust clause or the entire condition, or join the clauses with OR, fail the check.
