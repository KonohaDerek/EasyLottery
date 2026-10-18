# Proposal

## Why

PR #300 exposed unstable Passkey and YouTube settings browser coverage: retrying a Passkey test can reuse credential state, while the YouTube retry test currently writes through to API storage. The issue needs deterministic evidence for real WebAuthn flows and failed-load recovery before it can be considered resolved.

## What Changes

- Isolate Passkey credential state per E2E run and clear only its dedicated test state before each test attempt.
- Exercise Passkey registration, actual logout/relogin, protected admin routes, and rejection of assertions with a wrong origin, RP ID hash, or missing user verification.
- Make YouTube settings retry/save E2E fully mocked; assert no early save, one save after retry, preservation of unknown settings, correct success feedback, and separately reported API-key status.
- Add a Development-only, test-session-gated authentication rate-limit override for the expanded E2E flow; keep the production limit unchanged and test that boundary.
- Record CI/browser evidence and the Chromium virtual-authenticator versus human hardware smoke-test boundary.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. This is test infrastructure and verification coverage; production behavior and security policy do not change. `skip_specs: true` is set for this tooling-only change.

## Impact

Affected areas: `tests/e2e` Playwright tests and documentation, `.github/workflows/ci.yml`, the Development-only rate-limit configuration in `src/EasyLotteryAPI/Program.cs`, and `tests/EasyLotteryAPITests/SecurityEndpointTests.cs`. No frontend, Domain, stored production data, REST contract, or WebAuthn policy change is intended. CI will use a unique temporary storage directory and upload its existing test artifacts.
