# Local verification for #317–#323

- Tested code revision: a7ef818f2f97210798beb14667dbcde66c40b6ce.
- Date: 2026-11-12. Executor: Mac mini; local Node26.12.1, official .NET SDK10.0 Docker image. Container smoke builds Node22 from the repository Dockerfile.
- PR: https://github.com/KonohaDerek/EasyLottery/pull/334. Published as Draft pending independent reviews and CI.
- Original live worktree was not edited. Its uncommitted reports/screenshots/traces and unique commit remain in place.

## Commands and actual results

| Check | Command | Result |
| --- | --- | --- |
| Vue typecheck | npm run typecheck --prefix src/EasyLotteryWeb | exit0 |
| Vue unit | npm test --prefix src/EasyLotteryWeb | 159 passed,32 files |
| Vue production build | npm run build --prefix src/EasyLotteryWeb | exit0 |
| Release build | docker run --rm -v "$PWD:/workspace" -v /private/tmp/easy-lottery-livefix-nuget:/root/.nuget/packages -w /workspace mcr.microsoft.com/dotnet/sdk:10.0 dotnet build EasyLottery.generated.sln --configuration Release --nologo -v:q | exit0 |
| Domain | same disposable SDK container: dotnet test tests/EasyLotteryDomainTests/EasyLotteryDomainTests.csproj --configuration Release --no-build --nologo -v:q | 112 passed,0 skipped |
| API | same disposable SDK container: dotnet test tests/EasyLotteryAPITests/EasyLotteryApiTests.csproj --configuration Release --no-build --nologo -v:q | 161 passed,0 skipped |
| Browser | BASE_URL=http://127.0.0.1:18931 RUNNER_TEMP=/private/tmp E2E_STORAGE_DIRECTORY=/private/tmp/easy-lottery-e2e-livefix-20261112 npm test --prefix tests/e2e -- --reporter=line | 147 passed,3.6minutes |
| OpenSpec | npx --yes @fission-ai/openspec@1.7.0 validate --all --strict | 48 passed |
| Container smoke | bash local-smoke.sh, adapted from tests/smoke-health.sh only for unique image/container name, absolute checkout and loopback-only port18933 | exit0; health/live,health/ready,Vue root,Blazor index/framework assets pass |
| Diff | git diff --check | exit0 |

The Development browser API binds only127.0.0.1:18931. It uses fresh dedicated temporary storage, test-only session/rate-limit settings, RP localhost, origin http://localhost:18931, and the measured host-to-container source192.168.215.1 as a test allowlist entry. No production settings or data are used. Test storage is not a production account. Real virtual WebAuthn registration, logout/re-login, additional credential management, and rejection of wrong origin/RP/user verification all pass in the existing unmodified passkey-login.spec.mjs.

## Coverage

- Anonymous real and decoy options have the same status/shape across empty, one-credential and multi-credential stores. Decoys are stable across restart and do not replace real pending login. Failed login responses are uniform; register options/verify reject untrusted sources before inspecting email.
- API/Hub unknown paths return problem404; known endpoints and all three SignalR negotiation paths retain precedence. Unknown page uses Vue HTTP404. Legacy deep links remain reachable, missing legacy assets remain404. settings navigation redirects while programmatic GET/HEAD retain protection.
- All four OBS pages cover401/403/404/429/503/network and invalid URLs, with visible recovery information. Integrated no-session/missing-resource cases pass, and valid idle overlays remain transparent through transient Donate polling errors.
- Public viewport320/375/768/1280, keyboard navigation, heading/ARIA validation recovery, About wording, built PNG favicon and real root robots delivery pass.

## Boundaries

- Live suite test.fail markers remain historical production findings. The normal local suite contains no expected-failure masks for these repairs. #317 now has an explicit normal-suite regression; it did not have a dedicated original live marker.
- No production browsing/authentication, deployment, release or issue closure performed. PR merging is separate from deployed acceptance.
- Existing warnings: Node development dependency audit reports2moderate/2critical; production npm audit0. .NET test dependency System.Formats.Cbor9.0.0 reports existing moderate GHSA-vmv3-wvjp-wqpj. No lockfile/dependency changes were made. Two existing SignalR Rollup annotation warnings remain.
- Authoritative reviewer/round/CI evidence is outside this reviewed tree and attached to the PR. This file is the frozen pre-review local test snapshot, not a claim of review or CI approval.
