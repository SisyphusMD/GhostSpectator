# #14 ci: verify TLS when the publish job sends tokens

- Kind: pull, state: closed, by cody, created 2026-10-08T16:59:32-07:00
- Labels: none; milestone: none
- refs/pull/14/head -> main, merged: True

Same finding as archiver #182: the release reconcile curl calls used -k while sending CLUSTER/NAS Forgejo and GitHub repo-write PATs. cluster Forgejo, NAS Forgejo, api.github.com and uploads.github.com all serve valid certificates.
