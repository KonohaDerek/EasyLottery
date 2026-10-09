# PR #263 security middleware verification

Date: 2026-10-09

Scope: API middleware, CSP, regression tests, and security/deployment documentation. Worktree: `.worktrees/pr-263-security-fix`, based on PR head `b29554c` and synchronized with `origin/main` at `306f396`.

## Decision and risk

`TYPESAFE_API_KEY` was unavailable, so routing and verification level were decided manually. The fix keeps the configured trusted-proxy allowlist, processes forwarded headers before HTTPS redirection, and applies security headers to redirects, errors, static files, and endpoints. No production settings or data were changed. The existing `remediate-production-audit` OpenSpec change tracks #253 separately, so unrelated tasks were left unchanged.

## RED → GREEN

- Before the middleware change, `TrustedForwardedHttps_DoesNotRedirect_AndHttpResponsesKeepSecurityHeaders` failed because trusted `X-Forwarded-Proto: https` received HTTP 307 instead of 200.
- The first browser run then exposed a second issue: the CSP blocked all eight inline scripts in the Blazor app shell. `easyLotteryTheme.apply` was consequently undefined, and the OBS page rendered its error boundary.
- CSP now authorizes those fixed inline scripts by SHA-256 hash, without adding `unsafe-inline` to `script-src`. An API regression test fetches the app shell, verifies every inline script hash is present, and asserts that `script-src` does not allow `unsafe-inline`.
- `dotnet test tests/EasyLotteryAPITests/EasyLotteryApiTests.csproj --configuration Release --filter FullyQualifiedName~SecurityHeadersTests --no-restore`: 3 passed. Coverage includes trusted/untrusted forwarded HTTPS, ordinary HTTP redirects, headers on redirect/404, and CSP hashes.
- `dotnet test EasyLottery.generated.sln --configuration Release --no-restore`: Domain 112 passed; API 120 passed; 0 failures. Build emitted two existing Fido2 `ServerDomain`/`ServerName` deprecation warnings in `AdminPasskeyOptions.cs`; these are outside this PR's diff.
- Full Playwright suite on a fresh temporary storage directory with the CI session-token setting and `BASE_URL=http://127.0.0.1:18931`: 13 passed, including the previously failing overtime OBS flow and Vue public pages. An initial local run used the default port 18930 while the test API was on 18931; after explicitly matching `BASE_URL`, the complete suite passed.
- Docker production smoke using a unique temporary image/container tag (the existing `easy-lottery-api-smoke` image was preserved): live/ready health, Vue shell, Blazor shell, and Blazor runtime all returned HTTP 200.
- `npm audit --omit=dev` in `src/EasyLotteryWeb`: 0 production dependency vulnerabilities. The install reports advisories in the existing development toolchain; no dependency changes were needed for this fix.

## Review and boundaries

- The trusted-proxy allowlist remains the only source of forwarded scheme information; untrusted forwarded headers are ignored.
- HTTPS redirection uses permanent HTTP 308, matching #253's accepted 301/308 behavior while preserving the request method.
- CSP hash freshness is covered by an automated test and documented for future inline-script edits.
- Deployment documentation covers HTTPS offload, trusted proxy configuration, health checks, and temporary rollback. No production settings, reverse-proxy configuration, or production data were changed.
- `git diff --check`: passed after synchronizing with current `origin/main`.
