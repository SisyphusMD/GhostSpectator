# #45 chore(gate): call the shared gate and AI review workflows

- Kind: pull, state: closed, by cody, created 2026-11-25T21:28:41-08:00
- Labels: none; milestone: none
- refs/pull/45/head -> main, merged: True

Closes #44

`ci.yml` and `ai-review.yml` carried copies of the gate and AI review jobs, and copies drift. They now call project-rules main's `gate.yml`, `gate-lint.yml` and `review.yml` (SisyphusMD/project-rules#78), as proven in SisyphusMD/homebrew-tap#34. They keep only the triggers, the concurrency groups and the one secret the jobs may see, `ISSUE_READ_TOKEN`. Check names stay the same; each calling job adds one check of its own (`CI / shared-gate` and so on), which nothing requires.

This PR is judged by the old copies, because `pull_request_target` runs main's files; the next PR here runs on the callers.

No tests: workflow wiring only, covered by project-rules' tests of the called files and the homebrew-tap proof run.

## forgejo-actions, 2026-11-25T21:30:08-08:00

<!-- ai-review -->
**AI review** of `edce741c4a`: Clean migration of ci.yml and ai-review.yml from inline job copies to reusable-workflow calls against project-rules main, following the proven homebrew-tap#34 pattern. Triggers, concurrency groups, and the one custom secret are correctly retained in the caller; the called workflows own the job logic. CHANGELOG updated, issue linked, no unrelated changes.

No findings.

Disagree? Reply on this PR with your reasoning; the reviewer re-reads every reply.
<!-- ai-review-state W10= -->
