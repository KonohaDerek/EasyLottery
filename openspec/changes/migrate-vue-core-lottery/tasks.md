# Tasks

- [x] Add failing Vitest coverage for lottery draw/state, participant normalization, JSON/CSV round-trip, YouTube-member weighting, chat policy/cooldown/cursor, and SuperChat mapping.
- [x] Implement small, dependency-free lottery domain helpers and CSV import/export that preserve the existing format and replacement behavior.
- [x] Add ETag-protected DrawingRules resource across application, YAML/SQLite repository, API authorization, and tests.
- [x] Add an admin-protected server-side YouTube adapter and minimal response DTOs; test with a fake provider and assert the API key never appears in responses/logs.
- [x] Add Vue home view, authenticated `/` route, API fallback, and Blazor rollback alias; keep existing lottery state in memory.
- [x] Add cancellable, single-flight YouTube polling, rule filtering/cooldown, accepted participant creation, and existing SuperChat feed integration.
- [x] Add focused Playwright coverage for direct route/refresh/auth, draw and imports, mocked YouTube member/chat flow, errors, and legacy route.
- [x] Run Vue, API, Domain, health and strict OpenSpec verification in the project Dev Container; record exact results and gaps.
- [x] Review full diff for parity, credential handling, races, ETag behavior, visual scope, and unrelated changes; complete any independent review required by the risk.
- [x] Push branch and open a Ready-for-review PR linked to #288 and related #252/#270; sync Project #1 to In review. Do not merge/deploy or claim Native OBS testing.
