# #30 Prove the required checks before protecting main

- Kind: issue, state: closed, by cody, created 2026-11-17T19:17:23-08:00
- Labels: none; milestone: none

Part of SisyphusMD/project-rules#75. Verify each required Forgejo context rejects a deliberate failure on a disposable draft candidate. PR #29 provides the positive contexts from the adopted workflows. The draft must never be merged, tagged, or published. Record precise contexts and diagnostics before changing protection. The new release workflow still needs local licensed PEAK gameplay and a live release proof.

## cody, 2026-11-17T19:24:40-08:00

Draft #31 is running negative proofs on 22ac192dbd61cee47a51eae06b11cd7affcd9ccf. The pinned gitleaks binary rejected the synthetic fixture locally, and the project file produces the intended XML parse error. Never merge or tag this draft.

## cody, 2026-11-17T19:49:26-08:00

Draft #31 at 7f3c7a4edda4c7a9ea9e307e2e3ae8464ae474ed produced all eight expected failure diagnostics, including the exact project parse error and the AI review diff-size refusal. PR #29 supplied the positive required contexts. Draft #31 is closed unmerged. Protection still awaits the licensed PEAK release proof for the new tag-only path.

## cody, 2026-11-17T20:41:06-08:00

Proof complete. PR #29 passed every shared gate at exact head 82d1ae6405a2f74107bbd54fd10c7b5d0bad3227. Draft #31 recorded each expected rejection at 7f3c7a4edda4c7a9ea9e307e2e3ae8464ae474ed and was closed without merging. The parent adoption issue still tracks the live release dependency and branch protection.
