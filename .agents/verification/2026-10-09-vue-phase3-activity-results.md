# Vue Phase 3 Activity Results verification

- Branch: `feat/vue-frontend-phase3`
- Base: `origin/main` at `fd49dbb`
- Scope: Issue #268 read-only activity-result browsing slice; no production, backend contract, Domain, payment, OBS, SignalR, or Blazor retirement changes.
- Environment: repository Dev Container Dockerfile, mounted isolated worktree; no `.env` file or production credentials loaded. Playwright used the Development-only session-token endpoint and local API.

## Checks

- `npm run typecheck` — passed.
- `npm test` — 6 files, 18 tests passed.
- `npm run build` — passed; Vite built 691 modules.
- `npm audit --omit=dev` — 0 production dependency vulnerabilities. Full install reports 3 advisories in the development dependency tree (1 moderate, 2 critical); no dependency changes were made in this slice.
- `dotnet restore EasyLottery.generated.sln` — passed.
- `dotnet build EasyLottery.generated.sln --configuration Release --no-restore` — passed, 0 warnings and 0 errors.
- Domain tests — 112 passed.
- API tests — 120 passed.
- Playwright `vue-activity-results.spec.mjs` — 4 passed: unauthenticated redirect/no API request, authenticated filtering/details/CSV download, API failure and retry, and Blazor-to-Vue full-page handoff.
- HTTP probes for `/activity-results` and the Development session-token endpoint — 200.
- `openspec validate --all --strict` — 31 passed, 0 failed. It emitted one pre-existing informational warning for `donate-draw-reveal-sequence/spec.md`, unrelated to this change.
- `git diff --check` — passed.

## Review notes

- Playwright exposed and verified a real auth-guard defect: the shared helper only redirected `/system/*`, despite routes using `requiresAuth`. It now protects every route for which the router invokes the helper; the new route has both unit and E2E coverage.
- The app had not registered its existing Vuetify components. Registering the components used by the app makes the buttons and layouts real Vuetify controls; the browser checks now exercise accessible buttons and CSV download.
- The Blazor navigation link uses an `aria-label`, so the E2E test targets that actual accessible name. The login title is exposed as a named region rather than a heading, and the unauthenticated test asserts that region.
- Independent second-pass review by the main agent confirmed the diff is limited to the first read-only slice, keeps the existing Blazor implementation as rollback, and introduces no API/Domain or production behavior change. No external second reviewer was used.
