# Vue PokeBox management verification

## Scope

Validated the Vue `/pokebox` administration slice and existing PokeTemplate API contract, including the Blazor `/legacy/pokebox` rollback route and navigation handoff. Playwright intercepted template writes, asset reads, and OBS-session issuance; no production data or live OBS session was changed.

## Results

- `npm ci` in `src/EasyLotteryWeb`: passed. npm reported 3 existing development-dependency vulnerabilities (1 moderate, 2 critical); no dependency or lockfile changes were made.
- `npm test` in `src/EasyLotteryWeb`: passed, 9 test files / 33 tests.
- `npm run typecheck` in `src/EasyLotteryWeb`: passed.
- `npm run build` in `src/EasyLotteryWeb`: passed, 706 modules transformed.
- `BASE_URL=http://127.0.0.1:18933 PLAYWRIGHT_BROWSERS_PATH=/tmp/easylottery-playwright.fcCJV6 npm test` in `tests/e2e`: passed, 39 tests, including Passkey, legacy `/pokebox/editor/{id}` handoff, and all PokeBox/OBS scenarios. This used a fresh temporary storage directory and an explicit test-only allowed origin `http://localhost:18933`.
- `dotnet build EasyLottery.generated.sln --no-restore --configuration Release` in the repo Dev Container: passed, 0 warnings / 0 errors.
- `env -u Testing__EnableAdminSessionToken dotnet test EasyLottery.generated.sln --no-restore --configuration Release` in the repo Dev Container: passed, 112 Domain tests and 120 API tests; 232 total, 0 failed / 0 skipped.
- `openspec validate --all --strict` in the repo Dev Container: passed, 34/34 changes. OpenSpec reported one informational pre-existing delta for `donate-draw-reveal-sequence`; no validation failures.
- `git diff --check`: passed.

The first `dotnet test --no-restore` could not run because test-project NuGet assets were not present in the container. A normal restore then exposed the container's E2E-only `Testing__EnableAdminSessionToken=true` environment variable to the security test. Unsetting that variable for the test command restored the intended default and the full suite passed; the container setting itself was left unchanged.

Repeated Playwright runs against a reused test database are not independent: the Passkey scenario stores a credential while each run creates a new virtual authenticator. The final full run used fresh storage. When using a non-default local port, the Passkey test origin must also be explicitly allowed; no application or production origin configuration was changed.

## Review notes and limits

- Compared TypeScript PokeTemplate/PokeCell fields, enum values, CRUD endpoints, and grid behavior against the existing .NET models and Blazor page.
- Confirmed anonymous `/pokebox` navigation preserves the requested query through login and does not fetch templates before authentication.
- Confirmed test OBS credentials arrive via the URL fragment, are consumed by the existing Blazor bootstrap into session storage, and are removed from the visible URL; no token is placed in the path or query.
- Main-agent second-pass review completed; no separate reviewer task was created. No backend/domain contract, public draw, preview, OBS rendering, SignalR, production, or Blazorise dependency migration was included.
- The migration retains `/legacy/pokebox` for rollback; the Vue `/pokebox` shell is limited to the exact route.
