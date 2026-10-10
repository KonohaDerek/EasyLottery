# Vue Roulette preview relay verification

## Scope

Validate the authenticated Vue `/roulette/preview/{id}` relay, API-host fallback, and `/legacy/roulette/preview/{id}` Blazor rollback alias. Keep `/obs/roulette/{publicId}`, spin/result behavior, SignalR, API/Domain contracts, production, and persisted templates unchanged.

## Results

- `npm ci --prefix src/EasyLotteryWeb`: passed; npm reported 3 development-dependency advisories (1 moderate, 2 critical); no dependency files changed.
- `npm ci --prefix tests/e2e`: passed; 0 vulnerabilities reported.
- Vue unit tests: passed, 9 files / 33 tests.
- Vue typecheck: passed.
- Vue production build: passed, 712 modules transformed.
- Targeted Roulette preview Playwright: passed, 8/8.
- Full Playwright suite: passed, 55/55, using a fresh temporary storage directory in the repository Dev Container.
- .NET Release build: passed, 0 warnings / 0 errors.
- .NET Release tests: passed, 112 Domain + 120 API; 232 total, 0 failed / 0 skipped.
- `openspec validate --all --strict`: passed, 36/36 changes. One pre-existing informational delta for `donate-draw-reveal-sequence`; no validation failures.
- `git diff --check`: passed.

The API/E2E container mounted only this worktree; no `.env` or production credentials were mounted. Tests used a temporary storage directory and intercepted template/session calls. The initial browser launch reported missing Chromium system dependencies; these were installed only inside the temporary container, after which all targeted and full E2E tests passed.

## Review and limits

- Main-agent second-pass review completed: route authorization, invalid IDs, template-vs-session 404 behavior, OBS scope and fragment handoff, fallback ordering, rollback alias, and unchanged OBS/spin/SignalR behavior match the requirements and E2E assertions. No findings.
- An independent reviewer was not run; this implementation is a single coupled route slice and the main-agent second-pass is recorded here.
- No production OBS/browser-source smoke test is planned because the overlay is unchanged.
