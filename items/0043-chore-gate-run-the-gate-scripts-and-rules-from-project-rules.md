# #43 chore(gate): run the gate scripts and rules from project-rules main

- Kind: pull, state: closed, by cody, created 2026-11-24T19:11:30-08:00
- Labels: none; milestone: none
- refs/pull/43/head -> main, merged: True

Closes #42

This repo ran its own copies of the gate scripts and kept a copy of the rules for the AI review, and copies drift. `ci.yml` and `ai-review.yml` now fetch the scripts and `RULES.md` from project-rules main on every run and fail when they cannot (SisyphusMD/project-rules#78). The gate jobs and `ai-review.yml` are project-rules main's (6a6d41b4), and the copies are removed in the same change, so no run is left without either.

The removed copies also leave ruff.toml's exclude list. mirror-lockdown.yml is copied again for its source header.

Large diff: 890 of the removed lines are the deleted copies, and the added workflow lines come from project-rules main, where they were reviewed and tested.


## forgejo-actions, 2026-11-24T19:15:29-08:00

<!-- ai-review -->
**AI review** of `2d39b6c4cb`: PR correctly implements the decision from project-rules#78: gate scripts and RULES.md are now fetched from project-rules main at runtime instead of using local copies, the copies are removed, and the workflows are updated to match the canonical versions. The large-diff justification is sound (most lines are deletions of files replaced by a fetch).

No findings.

Disagree? Reply on this PR with your reasoning; the reviewer re-reads every reply.
<!-- ai-review-state W10= -->
