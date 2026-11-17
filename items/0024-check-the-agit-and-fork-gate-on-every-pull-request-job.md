# #24 Check the AGit and fork gate on every pull-request job

- Kind: pull, state: closed, by cody, created 2026-11-17T13:37:35-08:00
- Labels: none; milestone: none
- refs/pull/24/head -> main, merged: True

Closes #23

Before restoring the project, Validate checks all jobs under `.forgejo/workflows/` that can run on `pull_request`. A job must make a same-repository branch and standard PR flow necessary for execution.

Tests: the checker passed on the current workflows and rejected copies with the whole guard removed, either clause removed, or OR used between clauses.
