# Verification — Vue core lottery workspace

- Branch: `feat/vue-core-lottery-workspace`
- Base: `fc1ddfcd4567e4bcf808c4a6210e511aa1a25f31` (`origin/main`, PR #287 merge)
- Environment: repository Dev Container `easylottery-vue-lottery-20261011`; production image smoke used a separate container and temporary storage, then both its container and image tag were removed.

## Results

- Vue unit tests: `npm test -- --run` passed, 21 files / 93 tests.
- Vue typecheck: `npm run typecheck` passed.
- Vue production build: `npm run build` passed. Rollup reported the two existing `@microsoft/signalr` `/*#__PURE__*/` annotation notices and removed those annotations.
- Focused Playwright (inside the project Dev Container): `BASE_URL=http://127.0.0.1:18930 npm test -- vue-lottery-home.spec.mjs` passed, 9 / 9. Covers unauthenticated root, `/legacy`, draw/rule save and mocked YouTube chat, canceled connection, edits made during a pending rule save, missing ETag, delayed/failed rate loading during YouTube member import, and mobile navigation/layout.
- .NET 10 Release solution build: `dotnet build EasyLottery.generated.sln --configuration Release --no-restore --verbosity minimal` passed with 0 warnings and 0 errors.
- .NET tests: `env -u Testing__EnableAdminSessionToken dotnet test EasyLottery.generated.sln --configuration Release --no-build --no-restore --verbosity minimal` passed, 112 Domain + 134 API tests, 246 total. Focused new SQLite configuration and JSON/SQLite feed mutation tests passed as part of the suite.
- Production image: final `docker build -f src/EasyLotteryAPI/Dockerfile` succeeded. An isolated production container returned HTTP 200 for `/health/live`, `/health/ready`, `/`, `/index.html`, `/legacy`, and `/_framework/blazor.webassembly.js`; assertions confirmed `/` serves Vue and `/legacy` serves Blazor. The exact temporary container and image tag were removed afterward.
- OpenSpec: `openspec validate --all --strict` passed, 41 changes / 0 failures. One existing info note remains for `donate-draw-reveal-sequence/spec.md` whose archive target is absent.
- `git diff --check` and `openspec validate --all --strict` passed; both were rerun after final task documentation changes.
- Production dependency audit: `npm audit --omit=dev` found 0 vulnerabilities.

## PR #291 CI follow-up

- The first GitHub CI run failed 5 browser tests: 3 legacy-to-Vue handoff tests still opened `/` after Vue became the root, and 2 Roulette tests exhausted the shared-IP `sensitive` API limit during the full suite.
- Updated those handoff tests to open `/legacy`. The E2E runner now opts into `Testing:SensitivePermitLimit=600`; the override is effective only in Development when the test Session Token is explicitly enabled. The production/default limit remains 60/minute, verified by a new API regression test.
- Rebuilt the Release solution in the Dev Container (0 warnings, 0 errors). Re-ran API tests (135/135), Domain tests (112/112), 22 focused browser tests (22/22), and the complete browser suite on a fresh test database (68/68). `git diff --check` passed.
- GitHub CI will be checked again after this follow-up is pushed; local green tests do not assert a remote green result.

## Review and limits

- Independent read-only reviews identified stale full-document settings/feed writes, full-feed retention, an in-flight settings-save UI race, missing mobile navigation, logout/token issuance ordering, and a YouTube member import race while DrawingRules load. These were addressed with regression tests; the final reviewer rechecked the CSV-loading fix and found no remaining issue in that scope. Main-agent full-diff review found no unrelated changes.
- Full dependency installation previously reported 3 development-tool advisories (1 moderate and 2 critical); dependencies were not upgraded as part of this slice. Production dependencies currently audit clean.
- YouTube E2E and API coverage use mocks/fakes; no live YouTube API key or channel was used. SignalR notification delivery was not exercised through a connected client.
- Native OBS verification was not run, per the user's instruction; the user will validate OBS separately. Browser E2E does not substitute for it.
- No raster image assets were added. The new lottery hero uses the existing polished gradient/sparkle animation treatment and the stylesheet includes reduced-motion handling; the 390 px mobile layout and navigation were exercised in Playwright.
