# #29 Publish the verified release pull request

- Kind: pull, state: closed, by cody, created 2026-11-17T18:32:17-08:00
- Labels: none; milestone: none
- refs/pull/29/head -> main, merged: True

Closes #28.

Preserves local PatchValidator and an explicit confirmation of gameplay with the exact ZIP. Preparation pushes only a release branch. Publish Release checks the final tree against the prepared source and validates the package and version stamps before pushing the tag. The existing Thunderstore and release mirror publishers still run from the tag.

Local automated test: a bare Git origin and a stub PEAK build prove rejecting confirmation never pushes and confirming pushes only the candidate branch, with its qualified source recorded. The real PEAK gameplay check remains a release-time activity.

Large diff: The qualification workflow, tag-only publisher, verified upstream helpers, and their tests must change together to keep a working release path in this commit.

## forgejo-actions, 2026-11-17T18:35:31-08:00

<!-- ai-review -->
**AI review** of `82d1ae6405`: The PR restructures the release flow so that local preparation pushes only a release branch, a normal PR is reviewed and merged, and a dispatchable Publish Release workflow verifies the exact merge before tagging. The earlier committed .pyc file has been removed. The vendored helpers, workflow logic, and local script changes are consistent and well-tested for the local path.

### Resolved

- **F1** (suggestion, `tests/__pycache__/test_release_preparation.cpython-311.pyc:1`) Compiled bytecode committed to repository: The .pyc file is no longer present in the diff. The .gitignore entry for __pycache__/ is in place and the file has been removed from the commit. (Not blocking: it did not cite a changed file and line.)


Disagree? Reply on this PR with your reasoning; the reviewer re-reads every reply.
<!-- ai-review-state W3siaWQiOiAiRjEiLCAiZmlsZSI6ICJ0ZXN0cy9fX3B5Y2FjaGVfXy90ZXN0X3JlbGVhc2VfcHJlcGFyYXRpb24uY3B5dGhvbi0zMTEucHljIiwgImxpbmUiOiAxLCAic2V2ZXJpdHkiOiAic3VnZ2VzdGlvbiIsICJzdGF0dXMiOiAicmVzb2x2ZWQiLCAidGl0bGUiOiAiQ29tcGlsZWQgYnl0ZWNvZGUgY29tbWl0dGVkIHRvIHJlcG9zaXRvcnkiLCAiZGV0YWlsIjogIlRoZSAucHljIGZpbGUgaXMgbm8gbG9uZ2VyIHByZXNlbnQgaW4gdGhlIGRpZmYuIFRoZSAuZ2l0aWdub3JlIGVudHJ5IGZvciBfX3B5Y2FjaGVfXy8gaXMgaW4gcGxhY2UgYW5kIHRoZSBmaWxlIGhhcyBiZWVuIHJlbW92ZWQgZnJvbSB0aGUgY29tbWl0LiAoTm90IGJsb2NraW5nOiBpdCBkaWQgbm90IGNpdGUgYSBjaGFuZ2VkIGZpbGUgYW5kIGxpbmUuKSIsICJyZXNwb25zZSI6ICIifV0= -->
