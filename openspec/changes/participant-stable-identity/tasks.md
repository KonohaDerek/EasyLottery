# Tasks

- [x] Confirm Issue #303 requirements, current code paths, YouTube channel-ID availability, and existing identity-link scope.
- [x] Record the approved bounded plan and route decision; move Project #1 #303 to In progress.
- [x] Add source-scoped YouTube author ID to the additive API projection and test its response contract.
- [x] Add optional participant identity, normalization, and compatible JSON/CSV import/export.
- [x] Deduplicate participants and apply cooldown by stable source/account ID; define and test conservative ID-less behavior while preserving explicit name rules.
- [x] Preserve YouTube Studio channel identity when canonical profile URL data is present; leave handles/unknown URLs ID-less.
- [x] Add unit tests for same ID rename/case, distinct IDs with same name, same ID across platforms, ID-less fallback, cooldown, and import/export compatibility.
- [x] Add a mocked lottery Playwright E2E covering stable-ID deduplication and distinct same-name accounts; do not contact real accounts.
- [x] Run full Vue tests, typecheck/build, API/Domain tests, focused E2E, strict OpenSpec, and local diff checks. The local container smoke script is unavailable because the Dev Container has no Docker CLI; final-commit CI remains the authoritative smoke check.
- [ ] Complete final review, push branch, open a Ready-for-review PR linked to #303, verify final-commit CI, and move Project #1 #303 to In review. Do not merge/deploy.
