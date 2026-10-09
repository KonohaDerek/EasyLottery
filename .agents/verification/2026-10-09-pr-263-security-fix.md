# PR #263 security middleware verification

Date: 2026-10-09

Scope: API middleware, CSP, regression tests, and security/deployment documentation. Worktree: `.worktrees/pr-263-security-fix`, based on PR head `b29554c`.

## Decision and risk

`TYPESAFE_API_KEY` was unavailable, so routing and verification level were decided manually. The fix keeps the configured trusted-proxy allowlist, processes forwarded headers before HTTPS redirection, and applies security headers to redirects, errors, static files, and endpoints. No production settings or data were changed. The existing `remediate-production-audit` OpenSpec change tracks #253 separately, so unrelated tasks were left unchanged.

## RED → GREEN

- Before the middleware change, `TrustedForwardedHttps_DoesNotRedirect_AndHttpResponsesKeepSecurityHeaders` failed because trusted `X-Forwarded-Proto: https` received HTTP 307 instead of 200.
- The first browser run then exposed a second issue: the CSP blocked all eight inline scripts in the Blazor app shell. `easyLotteryTheme.apply` was consequently undefined, and the OBS page rendered its error boundary.
- CSP now authorizes those fixed inline scripts by SHA-256 hash, without adding `unsafe-inline` to `script-src`. An API regression test fetches the app shell, verifies every inline script hash is present, and asserts that `script-src` does not allow `unsafe-inline`.
- `dotnet test tests/EasyLotteryAPITests/EasyLotteryApiTests.csproj --configuration Release --filter FullyQualifiedName~SecurityHeadersTests --no-restore`: 3 passed. Coverage includes trusted/untrusted forwarded HTTPS, ordinary HTTP redirects, headers on redirect/404, and CSP hashes.
- `dotnet test EasyLottery.generated.sln --configuration Release --no-restore`: Domain 112 passed; API 120 passed; 0 failures.
- Full Playwright suite on a fresh temporary storage directory with the CI session-token setting and a port-matched Passkey origin: 11 passed, including the previously failing overtime OBS flow.

## Review and boundaries

- The trusted-proxy allowlist remains the only source of forwarded scheme information; untrusted forwarded headers are ignored.
- CSP hash freshness is covered by an automated test and documented for future inline-script edits.
- Deployment documentation covers HTTPS offload, trusted proxy configuration, health checks, and temporary rollback. Production was not changed.
- `git diff --check` passed before final main-branch synchronization.
