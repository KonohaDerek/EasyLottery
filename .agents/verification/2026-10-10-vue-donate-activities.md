# Vue Donate activities verification

- Branch: `feat/vue-donate-activities-slice`
- Base: `origin/main` at merge commit `ec313683` (PR #275)
- Scope: Issue #268 Donate activity management slice; no API/Domain changes, production cutover, draw execution, OBS/SignalR migration, or Blazorise removal.
- Environment: repo Dev Container image `easylottery-devcontainer:phase3`, mounted isolated worktree; no `.env`, production credentials, or credential mounts. Browser E2E used a fresh Development API storage directory under `/tmp` and the Development-only test-session endpoint.

## Checks

- Vue `npm test` — 7 test files, 24 passed.
- Vue `npm run typecheck` — passed.
- Vue `npm run build` — passed, 698 modules transformed.
- `dotnet restore EasyLottery.generated.sln` — passed.
- `dotnet build EasyLottery.generated.sln --configuration Release --no-restore` — passed, 0 warnings and 0 errors.
- `dotnet test EasyLottery.generated.sln --configuration Release --no-build` — Domain 112 passed; API 120 passed; 0 failed.
- `openspec validate --all --strict` — 32 passed, 0 failed. Existing informational warning for `donate-draw-reveal-sequence/spec.md` is unrelated to this change.
- Full Playwright suite — 31 passed. Includes unauthenticated redirect/no API call, authenticated CRUD payloads with mocked writes, asset-picker failure/retry and selection, probability boundary, API retry, Blazor-to-Vue handoff, token request/header and legacy Blazor rollback route.
- `git diff --check` — passed after final diff review.
- Dependency install reported 3 existing development-tree advisories (1 moderate, 2 critical); no dependency or lockfile changes were made.

## Review and coverage

- Main-agent second-pass review confirmed only the exact `/donate-activities` Vue fallback was added; existing activity REST endpoints, Domain, payment and draw behavior are unchanged.
- Edit payload retains activity/prize IDs, PublicId, type, and remaining stock; date-local input serializes to UTC; test OBS token is placed in the URL fragment and excluded from query parameters/storage/logging.
- Blazor remains available at `/legacy/donate-activities`; existing non-migrated routes are unchanged.
- New UI CRUD and OBS-session E2E requests are intercepted by Playwright; no real activity is created, edited, deleted, or drawn by the new tests. Existing regression suites exercise API behavior separately.
- TypeSafe Jev and Dev Container CLI were unavailable; the documented manual routing fallback was recorded in `.agents/decisions/2026-10-10-vue-donate-activities.json`. The existing Docker image was run directly without `.env` or credentials.
- Production smoke/cutover and every possible animation/asset combination remain out of scope; no production deployment was performed.
