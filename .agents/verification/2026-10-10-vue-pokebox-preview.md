# Vue PokeBox preview relay verification

## Scope

Validated the authenticated Vue `/pokebox/preview/{id}` relay, exact API-host fallback, and `/legacy/pokebox/preview/{id}` Blazor rollback alias. The existing `/obs/pokebox/{publicId}` overlay, draw lifecycle, SignalR, API/Domain contracts, production, and persisted templates were not changed. E2E intercepted template/session requests and OBS navigation; no real OBS token or production data was used.

## Results

- `npm ci --prefix src/EasyLotteryWeb`: passed; npm reported 3 existing development dependency advisories (1 moderate, 2 critical); no dependency files changed.
- `npm ci --prefix tests/e2e`: passed; 0 vulnerabilities reported.
- `npm test --prefix src/EasyLotteryWeb`: passed, 9 files / 33 tests.
- `npm run typecheck --prefix src/EasyLotteryWeb`: passed.
- `npm run build --prefix src/EasyLotteryWeb`: passed, 709 modules transformed.
- `BASE_URL=http://127.0.0.1:18936 PLAYWRIGHT_BROWSERS_PATH=/tmp/easylottery-playwright-preview npm test --prefix tests/e2e`: passed, 47 tests. This used a fresh temporary storage directory and test-only allowed origin `http://localhost:18936` in the repository Dev Container image.
- Targeted preview E2E: 8/8 passed, including unauthenticated access, malformed id, valid scoped token, missing/empty public id, template and session retry paths, and legacy alias.
- API-host probe for `/pokebox/preview/not-an-id`: `200 text/html`, confirming the dynamic fallback serves the Vue shell before the client redirects safely.
- `dotnet build EasyLottery.generated.sln --no-restore --configuration Release`: passed, 0 warnings / 0 errors.
- `dotnet test EasyLottery.generated.sln --no-restore --configuration Release`: passed, 112 Domain + 120 API tests; 232 total, 0 failed / 0 skipped.
- `openspec validate --all --strict`: passed, 35/35 changes. OpenSpec emitted one pre-existing informational delta for `donate-draw-reveal-sequence`; no validation failures.
- `git diff --check`: passed after the final implementation and test changes.

The verification container was created from the existing `easylottery-devcontainer:phase3` image with only this worktree mounted; no `.env` or production credentials were mounted. The pre-existing PokeBox E2E container was left running and untouched.

## Review notes and limits

- Main-agent second-pass review completed: requirements and OpenSpec scenarios match the implementation; route authorization, invalid IDs, 404 distinctions, scoped OBS-session issuance, fragment-only token handoff, Vue fallback ordering, rollback alias, and untouched OBS/SignalR behavior were checked against source and E2E coverage. No findings.
- An independent reviewer was requested, but worktree thread setup did not produce a readable reviewer task in this environment; this is not an independent review.
- The Vue guard prevents anonymous template/session requests. The relay requests an OBS session only after a valid template load, scopes it to that template with `read` and `control`, and reuses the existing URL helper to put the token only in the fragment.
- Only a template-load 404 returns to `/pokebox`; an OBS-session API 404 remains a visible retryable error.
- No production OBS/browser-source smoke test was performed because this PR does not migrate or change the overlay.
