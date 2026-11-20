# #35 AI review: 40-minute deadline inside a 45-minute job

- Kind: pull, state: closed, by cody, created 2026-11-19T17:41:19-08:00
- Labels: none; milestone: none
- refs/pull/35/head -> main, merged: True

Closes #34

Sync from SisyphusMD/project-rules#108 (byte-identical copies of the script and workflow; tests live there). A 40-minute deadline from the review's start ends a slow review with an error status instead of the job being killed at 20 minutes with the required check left pending. The job limit is now 45 minutes.

## forgejo-actions, 2026-11-19T17:47:55-08:00

<!-- ai-review -->
**AI review** of `be39ce7863`: Clean sync of the AI review deadline mechanism from project-rules: a SIGALRM-based 40-minute deadline inside a 45-minute job, with correct alarm cancellation in a finally block, proper Deadline exception propagation, and a broadened catch-all that ensures the required check never stays pending. Tests live in the upstream repo as stated.

No findings.

Disagree? Reply on this PR with your reasoning; the reviewer re-reads every reply.
<!-- ai-review-state W10= -->
