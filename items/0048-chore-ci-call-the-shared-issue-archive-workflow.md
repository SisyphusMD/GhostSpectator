# #48 chore(ci): call the shared issue-archive workflow

- Kind: pull, state: closed, by cody, created 2026-11-27T07:11:10-08:00
- Labels: none; milestone: none
- refs/pull/48/head -> main, merged: True

Replaces the copied issue-archive job with the caller from project-rules main and deletes the copied script, which no longer runs.

Closes #47


## forgejo-actions, 2026-11-27T07:14:42-08:00

<!-- ai-review -->
**AI review** of `c0cf7f73d7`: Clean, focused PR that replaces the copied issue-archive job and script with a caller to project-rules' shared workflow, exactly as issue #47 specifies. The schedule, concurrency group, and secret passing are correctly retained in the caller; the CHANGELOG is updated per rule 10. No correctness, security, or test issues found.

No findings.

Disagree? Reply on this PR with your reasoning; the reviewer re-reads every reply.
<!-- ai-review-state W10= -->
