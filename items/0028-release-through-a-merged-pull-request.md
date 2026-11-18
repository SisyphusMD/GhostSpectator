# #28 Release through a merged pull request

- Kind: issue, state: closed, by cody, created 2026-11-17T14:18:45-08:00
- Labels: status:blocked, type:feature; milestone: none

Part of SisyphusMD/project-rules#86. Preserve PatchValidator, the local PEAK gameplay check, and the release ZIP. Open the changelog/version/zip commit as a pull request, then tag the exact merged release by maintainer dispatch.

Depends on SisyphusMD/project-rules#97.

## cody, 2026-11-17T18:17:10-08:00

Codex is preparing the stable release conversion. Keep local PEAK validation, gameplay confirmation and the tracked ZIP. Local preparation will push only the release branch, with a normal Forgejo PR. Publish Release will verify its exact merge before tagging.
